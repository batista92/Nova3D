using System.Buffers.Binary;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nova3D.Production.Assets.Gltf;

if (args.Length > 0 && args[0] == "--import")
{
    using var game = new ImportSmokeGame(args.Skip(1).ToArray());
    game.Run();
    return;
}

const int jointsPerDraw = 48;
const int characters = 100;
const int measuredFrames = 1000;

var locals = new Matrix[jointsPerDraw];
var inverseBind = new Matrix[jointsPerDraw];
var world = new Matrix[characters * jointsPerDraw];
var palette = new Matrix[characters * jointsPerDraw];
for (int joint = 0; joint < jointsPerDraw; joint++)
{
    locals[joint] = Matrix.CreateFromYawPitchRoll(joint * 0.003f, joint * 0.002f, 0f) *
                    Matrix.CreateTranslation(0f, 0.08f, 0f);
    inverseBind[joint] = Matrix.CreateTranslation(0f, joint * -0.08f, 0f);
}

UpdatePalettes();
var stopwatch = new Stopwatch();
long before = GC.GetAllocatedBytesForCurrentThread();
stopwatch.Start();
for (int frame = 0; frame < measuredFrames; frame++) UpdatePalettes();
stopwatch.Stop();
long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
if (allocated != 0)
    throw new InvalidOperationException($"Palette update allocated {allocated} bytes.");

double milliseconds = stopwatch.Elapsed.TotalMilliseconds / measuredFrames;
int uploadBytes = characters * jointsPerDraw * 64;
Console.WriteLine($"Skinning G5.1 CPU | characters {characters} | joints {jointsPerDraw} | " +
                  $"{milliseconds:F4} ms/frame | alloc {allocated} B | palette {uploadBytes / 1024.0:F1} KiB/frame | " +
                  $"checksum {palette[^1].M44:F3}");
Console.WriteLine("Skinning G5.1 strategy | uniform float4x4 | 48 joints/draw | 0 samplers | 64 B/joint");
RunAnimationRuntimeRegression();

foreach (string path in args)
{
    using JsonDocument json = OpenGltf(path);
    JsonElement root = json.RootElement;
    int skins = Count(root, "skins");
    int animations = Count(root, "animations");
    int maxJoints = 0;
    if (root.TryGetProperty("skins", out var skinValues))
        foreach (var skin in skinValues.EnumerateArray())
            maxJoints = Math.Max(maxJoints, skin.GetProperty("joints").GetArrayLength());
    int channels = 0;
    var interpolations = new HashSet<string>(StringComparer.Ordinal);
    if (root.TryGetProperty("animations", out var animationValues))
    {
        foreach (var animation in animationValues.EnumerateArray())
        {
            channels += animation.GetProperty("channels").GetArrayLength();
            foreach (var sampler in animation.GetProperty("samplers").EnumerateArray())
                interpolations.Add(sampler.TryGetProperty("interpolation", out var interpolation)
                    ? interpolation.GetString()! : "LINEAR");
        }
    }
    int skinnedPrimitives = 0;
    if (root.TryGetProperty("meshes", out var meshes))
    {
        foreach (var mesh in meshes.EnumerateArray())
        foreach (var primitive in mesh.GetProperty("primitives").EnumerateArray())
        {
            var attributes = primitive.GetProperty("attributes");
            bool joints = attributes.TryGetProperty("JOINTS_0", out _);
            bool weights = attributes.TryGetProperty("WEIGHTS_0", out _);
            if (joints != weights)
                throw new InvalidDataException("JOINTS_0 and WEIGHTS_0 must be present together.");
            if (joints) skinnedPrimitives++;
        }
    }
    Console.WriteLine($"Asset {Path.GetFileName(path)} | skins {skins} | max joints {maxJoints} | " +
                      $"skinned primitives {skinnedPrimitives} | clips {animations} | channels {channels} | " +
                      $"interpolation {string.Join(',', interpolations.Order())}");
}

void UpdatePalettes()
{
    for (int character = 0; character < characters; character++)
    {
        int offset = character * jointsPerDraw;
        world[offset] = locals[0];
        palette[offset] = inverseBind[0] * world[offset];
        for (int joint = 1; joint < jointsPerDraw; joint++)
        {
            world[offset + joint] = locals[joint] * world[offset + joint - 1];
            palette[offset + joint] = inverseBind[joint] * world[offset + joint];
        }
    }
}

static int Count(JsonElement root, string property) =>
    root.TryGetProperty(property, out var values) ? values.GetArrayLength() : 0;

static JsonDocument OpenGltf(string path)
{
    byte[] bytes = File.ReadAllBytes(path);
    if (!path.EndsWith(".glb", StringComparison.OrdinalIgnoreCase))
        return JsonDocument.Parse(bytes);
    if (bytes.Length < 20 || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0x46546C67 ||
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)) != 2)
        throw new InvalidDataException("Invalid GLB 2.0 header.");
    int cursor = 12;
    while (cursor + 8 <= bytes.Length)
    {
        int length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(cursor)));
        uint type = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(cursor + 4));
        cursor += 8;
        if (length < 0 || cursor + length > bytes.Length)
            throw new InvalidDataException("Invalid GLB chunk.");
        if (type == 0x4E4F534A) return JsonDocument.Parse(bytes.AsMemory(cursor, length));
        cursor += length;
    }
    throw new InvalidDataException("GLB has no JSON chunk.");
}

static void RunAnimationRuntimeRegression()
{
    var identity = Matrix.Identity;
    var nodes = new[]
    {
        new GltfNode("Root", -1, [1], Vector3.Zero, Quaternion.Identity, Vector3.One,
            identity, false, null, null),
        new GltfNode("Joint A", 0, [2], new Vector3(0f, 1f, 0f), Quaternion.Identity,
            Vector3.One, Matrix.CreateTranslation(0f, 1f, 0f), false, null, null),
        new GltfNode("Joint B", 1, [], new Vector3(0f, 1f, 0f), Quaternion.Identity,
            Vector3.One, Matrix.CreateTranslation(0f, 1f, 0f), false, null, null)
    };
    var skins = new[]
    {
        new GltfSkin("Test skin", [1, 2], [identity, Matrix.CreateTranslation(0f, -1f, 0f)], 1)
    };
    var times = new[] { 0f, 1f };
    var clipA = new GltfAnimationClip("Walk",
        [
            new GltfAnimationSampler(times,
                [Vector4.Zero, new Vector4(10f, 0f, 0f, 0f)], GltfAnimationInterpolation.Linear),
            new GltfAnimationSampler(times,
                [new Vector4(0f, 0f, 0f, 1f), new Vector4(0f, 1f, 0f, 0f)],
                GltfAnimationInterpolation.Linear)
        ],
        [
            new GltfAnimationChannel(0, 0, GltfAnimationTargetPath.Translation),
            new GltfAnimationChannel(1, 1, GltfAnimationTargetPath.Rotation)
        ], 1f);
    var clipB = new GltfAnimationClip("Idle",
        [
            new GltfAnimationSampler(times,
                [new Vector4(10f, 0f, 0f, 0f), new Vector4(20f, 0f, 0f, 0f)],
                GltfAnimationInterpolation.Step),
            new GltfAnimationSampler(times,
                [new Vector4(1f, 1f, 1f, 0f), new Vector4(2f, 2f, 2f, 0f)],
                GltfAnimationInterpolation.Linear)
        ],
        [
            new GltfAnimationChannel(0, 0, GltfAnimationTargetPath.Translation),
            new GltfAnimationChannel(1, 1, GltfAnimationTargetPath.Scale)
        ], 1f);

    var pose = new GltfSkeletonPose(nodes, skins);
    var player = new GltfAnimationPlayer(nodes, [clipA, clipB], pose);
    player.Play("Walk", loop: true);
    player.Update(0.25f);
    AssertNear(pose.GetTranslation(0).X, 2.5f, "LINEAR translation");
    player.Play("Idle", loop: true, blendDuration: 1f);
    player.Update(0.5f);
    AssertNear(pose.GetTranslation(0).X, 8.75f, "two-clip blend");
    AssertNear(pose.GetScale(1).X, 1.25f, "TRS scale blend");

    Span<Matrix> palette = stackalloc Matrix[2];
    pose.WriteSkinPalette(0, 0, new[] { 0, 1 }, palette);
    if (!float.IsFinite(palette[0].M44) || !float.IsFinite(palette[1].M44))
        throw new InvalidOperationException("Skin palette contains non-finite matrices.");

    player.Play(0, loop: true, speed: 2f);
    player.Update(0.25f);
    AssertNear(player.Time, 0.5f, "playback speed");
    player.Update(0.25f);
    AssertNear(player.Time, 0f, "loop wrap");
    player.Stop();
    AssertNear(pose.GetTranslation(0).X, 0f, "stop bind reset");

    player.Play(0, loop: true);
    for (var i = 0; i < 32; i++) player.Update(1f / 120f);
    var runtimeWatch = new Stopwatch();
    var beforeRuntime = GC.GetAllocatedBytesForCurrentThread();
    runtimeWatch.Start();
    for (var i = 0; i < 10_000; i++) player.Update(1f / 120f);
    runtimeWatch.Stop();
    var runtimeAllocated = GC.GetAllocatedBytesForCurrentThread() - beforeRuntime;
    if (runtimeAllocated != 0)
        throw new InvalidOperationException($"Animation update allocated {runtimeAllocated} bytes.");
    Console.WriteLine($"Skinning G5.3 runtime PASS | LINEAR + STEP | loop/speed/stop | blend | " +
                      $"{runtimeWatch.Elapsed.TotalMilliseconds / 10_000:F5} ms/update | " +
                      $"alloc {runtimeAllocated} B/update");
}

static void AssertNear(float actual, float expected, string operation)
{
    if (MathF.Abs(actual - expected) > 0.0001f)
        throw new InvalidOperationException($"{operation}: expected {expected}, got {actual}.");
}

sealed class ImportSmokeGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly string[] _paths;

    public ImportSmokeGame(string[] paths)
    {
        _paths = paths;
        _graphics = new GraphicsDeviceManager(this) { GraphicsProfile = GraphicsProfile.HiDef };
        IsMouseVisible = false;
    }

    protected override void LoadContent()
    {
        try
        {
            var importer = new GltfImporter(GraphicsDevice);
            Span<Matrix> importedPalette = stackalloc Matrix[48];
            foreach (var path in _paths)
            {
                using var model = importer.Load(path);
                var pose = new GltfSkeletonPose(model);
                var player = new GltfAnimationPlayer(model, pose);
                if (model.Animations.Count > 0)
                {
                    player.Play(0);
                    player.Update(1f / 60f);
                    if (model.Animations.Count > 1)
                    {
                        player.Play(1, blendDuration: 0.25f);
                        player.Update(1f / 60f);
                    }
                }
                foreach (var instance in model.Instances)
                {
                    var primitive = model.Primitives[instance.PrimitiveIndex];
                    if (primitive.IsSkinned && instance.SkinIndex is int skinIndex)
                        pose.WriteSkinPalette(skinIndex, instance.NodeIndex,
                            primitive.JointPalette, importedPalette);
                }
                Console.WriteLine($"Imported {Path.GetFileName(path)} | nodes {model.Nodes.Count} | " +
                                  $"skins {model.Skins.Count} | clips {model.Animations.Count} | " +
                                  $"skinned primitives {model.Primitives.Count(value => value.IsSkinned)} | " +
                                  "runtime pose PASS");
            }
        }
        finally
        {
            Exit();
        }
    }
}
