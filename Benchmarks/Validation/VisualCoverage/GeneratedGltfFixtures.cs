using System.Text.Json;

namespace Nova3D.Benchmarks.Validation.VisualCoverage;

internal sealed class GeneratedGltfFixtures : IDisposable
{
    private readonly string _directory;

    public GeneratedGltfFixtures()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            "nova3d-visual-gltf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        StaticPath = Path.Combine(_directory, "static-triangle.glb");
        AnimatedPath = Path.Combine(_directory, "animated-triangle.glb");
        WriteFixture(StaticPath, animated: false);
        WriteFixture(AnimatedPath, animated: true);
    }

    public string StaticPath { get; }
    public string AnimatedPath { get; }

    private static void WriteFixture(string path, bool animated)
    {
        using MemoryStream binary = new();
        Segment positions = AddFloats(binary,
            -1f, -1f, 0f,
             1f, -1f, 0f,
             0f,  1.4f, 0f);
        Segment normals = AddFloats(binary,
            0f, 0f, 1f,
            0f, 0f, 1f,
            0f, 0f, 1f);
        Segment texCoords = AddFloats(binary, 0f, 1f, 1f, 1f, 0.5f, 0f);
        Segment joints = default;
        Segment weights = default;
        if (animated)
        {
            joints = AddBytes(binary,
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0);
            weights = AddFloats(binary,
                1f, 0f, 0f, 0f,
                1f, 0f, 0f, 0f,
                1f, 0f, 0f, 0f);
        }
        Segment indices = AddUShorts(binary, 0, 1, 2);
        Segment inverseBind = default;
        Segment times = default;
        Segment rotations = default;
        if (animated)
        {
            inverseBind = AddFloats(binary,
                1f, 0f, 0f, 0f,
                0f, 1f, 0f, 0f,
                0f, 0f, 1f, 0f,
                0f, 0f, 0f, 1f);
            times = AddFloats(binary, 0f, 2f);
            float halfAngle = MathF.PI * (70f / 180f) * 0.5f;
            rotations = AddFloats(binary,
                0f, 0f, 0f, 1f,
                0f, 0f, MathF.Sin(halfAngle), MathF.Cos(halfAngle));
        }
        Align(binary);

        List<object> views = new();
        List<object> accessors = new();
        int positionAccessor = AddAccessor(positions, 5126, 3, "VEC3", 34962);
        int normalAccessor = AddAccessor(normals, 5126, 3, "VEC3", 34962);
        int texCoordAccessor = AddAccessor(texCoords, 5126, 3, "VEC2", 34962);
        int jointAccessor = -1;
        int weightAccessor = -1;
        if (animated)
        {
            jointAccessor = AddAccessor(joints, 5121, 3, "VEC4", 34962);
            weightAccessor = AddAccessor(weights, 5126, 3, "VEC4", 34962);
        }
        int indexAccessor = AddAccessor(indices, 5123, 3, "SCALAR", 34963);
        int inverseBindAccessor = -1;
        int timeAccessor = -1;
        int rotationAccessor = -1;
        if (animated)
        {
            inverseBindAccessor = AddAccessor(inverseBind, 5126, 1, "MAT4", null);
            timeAccessor = AddAccessor(times, 5126, 2, "SCALAR", null);
            rotationAccessor = AddAccessor(rotations, 5126, 2, "VEC4", null);
        }

        Dictionary<string, object> attributes = new()
        {
            ["POSITION"] = positionAccessor,
            ["NORMAL"] = normalAccessor,
            ["TEXCOORD_0"] = texCoordAccessor
        };
        if (animated)
        {
            attributes["JOINTS_0"] = jointAccessor;
            attributes["WEIGHTS_0"] = weightAccessor;
        }

        Dictionary<string, object> root = new()
        {
            ["asset"] = new { version = "2.0", generator = "Nova3D G8.3 deterministic fixture" },
            ["buffers"] = new[] { new { byteLength = checked((int)binary.Length) } },
            ["bufferViews"] = views,
            ["accessors"] = accessors,
            ["materials"] = new[]
            {
                new
                {
                    name = animated ? "Animated blue" : "Static orange",
                    pbrMetallicRoughness = new
                    {
                        baseColorFactor = animated
                            ? new[] { 0.08f, 0.35f, 0.95f, 1f }
                            : new[] { 1f, 0.28f, 0.04f, 1f },
                        metallicFactor = animated ? 0.15f : 0.7f,
                        roughnessFactor = animated ? 0.35f : 0.22f
                    },
                    doubleSided = true
                }
            },
            ["meshes"] = new[]
            {
                new
                {
                    name = animated ? "Animated mesh" : "Static mesh",
                    primitives = new[] { new { attributes, indices = indexAccessor, material = 0 } }
                }
            }
        };

        if (animated)
        {
            root["nodes"] = new object[]
            {
                new { name = "Joint" },
                new { name = "Animated triangle", mesh = 0, skin = 0 }
            };
            root["skins"] = new[]
            {
                new { name = "Single joint", inverseBindMatrices = inverseBindAccessor,
                    joints = new[] { 0 }, skeleton = 0 }
            };
            root["animations"] = new[]
            {
                new
                {
                    name = "Swing",
                    samplers = new[] { new { input = timeAccessor, output = rotationAccessor, interpolation = "LINEAR" } },
                    channels = new[] { new { sampler = 0, target = new { node = 0, path = "rotation" } } }
                }
            };
            root["scenes"] = new[] { new { nodes = new[] { 0, 1 } } };
        }
        else
        {
            root["nodes"] = new[] { new { name = "Static triangle", mesh = 0 } };
            root["scenes"] = new[] { new { nodes = new[] { 0 } } };
        }
        root["scene"] = 0;

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(root);
        int jsonLength = Align4(json.Length);
        int binaryLength = Align4(checked((int)binary.Length));
        using FileStream stream = File.Create(path);
        using BinaryWriter writer = new(stream);
        writer.Write(0x46546C67u);
        writer.Write(2u);
        writer.Write(checked((uint)(12 + 8 + jsonLength + 8 + binaryLength)));
        writer.Write(checked((uint)jsonLength));
        writer.Write(0x4E4F534Au);
        writer.Write(json);
        for (int i = json.Length; i < jsonLength; i++) writer.Write((byte)' ');
        writer.Write(checked((uint)binaryLength));
        writer.Write(0x004E4942u);
        writer.Write(binary.ToArray());
        for (long i = binary.Length; i < binaryLength; i++) writer.Write((byte)0);

        int AddAccessor(Segment segment, int componentType, int count, string type, int? target)
        {
            int view = views.Count;
            Dictionary<string, object> viewValue = new()
            {
                ["buffer"] = 0,
                ["byteOffset"] = segment.Offset,
                ["byteLength"] = segment.Length
            };
            if (target is int targetValue) viewValue["target"] = targetValue;
            views.Add(viewValue);
            int accessor = accessors.Count;
            accessors.Add(new { bufferView = view, componentType, count, type });
            return accessor;
        }
    }

    private static Segment AddFloats(MemoryStream stream, params float[] values)
    {
        Align(stream);
        int offset = checked((int)stream.Position);
        using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        foreach (float value in values) writer.Write(value);
        return new Segment(offset, checked((int)stream.Position - offset));
    }

    private static Segment AddUShorts(MemoryStream stream, params ushort[] values)
    {
        Align(stream);
        int offset = checked((int)stream.Position);
        using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        foreach (ushort value in values) writer.Write(value);
        return new Segment(offset, checked((int)stream.Position - offset));
    }

    private static Segment AddBytes(MemoryStream stream, params byte[] values)
    {
        Align(stream);
        int offset = checked((int)stream.Position);
        stream.Write(values);
        return new Segment(offset, values.Length);
    }

    private static void Align(MemoryStream stream)
    {
        while ((stream.Position & 3) != 0) stream.WriteByte(0);
    }

    private static int Align4(int value) => checked((value + 3) & ~3);

    public void Dispose()
    {
        if (!Directory.Exists(_directory)) return;
        string full = Path.GetFullPath(_directory);
        string temporary = Path.GetFullPath(Path.GetTempPath());
        if (!full.StartsWith(temporary, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(full).StartsWith("nova3d-visual-gltf-", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to remove an unexpected fixture directory.");
        Directory.Delete(full, recursive: true);
    }

    private readonly record struct Segment(int Offset, int Length);
}
