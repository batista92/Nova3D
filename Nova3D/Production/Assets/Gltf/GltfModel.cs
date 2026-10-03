using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nova3D.Rendering;

namespace Nova3D.Production.Assets.Gltf;

public sealed record GltfMaterial(
    string Name,
    Vector4 BaseColorFactor,
    float MetallicFactor,
    float RoughnessFactor,
    int? BaseColorTexture,
    int? NormalTexture,
    float NormalScale,
    int? MetallicRoughnessTexture,
    int? OcclusionTexture,
    float OcclusionStrength,
    bool DoubleSided,
    string AlphaMode,
    float AlphaCutoff);

public sealed record GltfPrimitive(Mesh Mesh, int? MaterialIndex, BoundingBox Bounds)
{
    private readonly IReadOnlyList<int> _jointPalette = Array.Empty<int>();
    private readonly IReadOnlyList<BoundingBox> _jointBounds = Array.Empty<BoundingBox>();

    internal GltfPrimitive(Mesh mesh, int? materialIndex, BoundingBox bounds, int[] jointPalette,
        BoundingBox[] jointBounds)
        : this(mesh, materialIndex, bounds)
    {
        _jointPalette = Array.AsReadOnly(jointPalette ?? throw new ArgumentNullException(nameof(jointPalette)));
        _jointBounds = Array.AsReadOnly(jointBounds ?? throw new ArgumentNullException(nameof(jointBounds)));
        if (_jointPalette.Count != _jointBounds.Count)
            throw new ArgumentException("Joint palette and joint bounds counts differ.", nameof(jointBounds));
    }

    /// <summary>
    /// Indices into <see cref="GltfSkin.Joints"/> used by this primitive. Vertex blend indices
    /// address this local palette, which is limited to 48 entries in Nova3D v0.2.
    /// </summary>
    public IReadOnlyList<int> JointPalette => _jointPalette;

    /// <summary>Bind-pose vertex bounds for each entry in <see cref="JointPalette"/>.</summary>
    public IReadOnlyList<BoundingBox> JointBounds => _jointBounds;

    public bool IsSkinned => _jointPalette.Count > 0;
}

public sealed record GltfInstance(string Name, int PrimitiveIndex, Matrix World)
{
    public int NodeIndex { get; init; } = -1;
    public int? SkinIndex { get; init; }
}

public sealed class GltfNode
{
    private readonly IReadOnlyList<int> _children;

    internal GltfNode(string name, int parent, int[] children, Vector3 translation,
        Quaternion rotation, Vector3 scale, Matrix localTransform, bool usesMatrix,
        int? meshIndex, int? skinIndex)
    {
        Name = name;
        Parent = parent;
        _children = Array.AsReadOnly(children);
        Translation = translation;
        Rotation = rotation;
        Scale = scale;
        LocalTransform = localTransform;
        UsesMatrix = usesMatrix;
        MeshIndex = meshIndex;
        SkinIndex = skinIndex;
    }

    public string Name { get; }
    public int Parent { get; }
    public IReadOnlyList<int> Children => _children;
    public Vector3 Translation { get; }
    public Quaternion Rotation { get; }
    public Vector3 Scale { get; }
    public Matrix LocalTransform { get; }
    public bool UsesMatrix { get; }
    public int? MeshIndex { get; }
    public int? SkinIndex { get; }
}

public sealed class GltfSkin
{
    private readonly IReadOnlyList<int> _joints;
    private readonly IReadOnlyList<Matrix> _inverseBindMatrices;

    internal GltfSkin(string name, int[] joints, Matrix[] inverseBindMatrices, int? skeletonRoot)
    {
        Name = name;
        _joints = Array.AsReadOnly(joints);
        _inverseBindMatrices = Array.AsReadOnly(inverseBindMatrices);
        SkeletonRoot = skeletonRoot;
    }

    public string Name { get; }
    public IReadOnlyList<int> Joints => _joints;
    public IReadOnlyList<Matrix> InverseBindMatrices => _inverseBindMatrices;
    public int? SkeletonRoot { get; }
}

public enum GltfAnimationInterpolation
{
    Linear,
    Step
}

public enum GltfAnimationTargetPath
{
    Translation,
    Rotation,
    Scale
}

public sealed class GltfAnimationSampler
{
    private readonly IReadOnlyList<float> _times;
    private readonly IReadOnlyList<Vector4> _values;

    internal GltfAnimationSampler(float[] times, Vector4[] values,
        GltfAnimationInterpolation interpolation)
    {
        _times = Array.AsReadOnly(times);
        _values = Array.AsReadOnly(values);
        Interpolation = interpolation;
    }

    public IReadOnlyList<float> Times => _times;
    public IReadOnlyList<Vector4> Values => _values;
    public GltfAnimationInterpolation Interpolation { get; }
}

public sealed record GltfAnimationChannel(
    int SamplerIndex,
    int NodeIndex,
    GltfAnimationTargetPath TargetPath);

public sealed class GltfAnimationClip
{
    private readonly IReadOnlyList<GltfAnimationSampler> _samplers;
    private readonly IReadOnlyList<GltfAnimationChannel> _channels;

    internal GltfAnimationClip(string name, GltfAnimationSampler[] samplers,
        GltfAnimationChannel[] channels, float duration)
    {
        Name = name;
        _samplers = Array.AsReadOnly(samplers);
        _channels = Array.AsReadOnly(channels);
        Duration = duration;
    }

    public string Name { get; }
    public IReadOnlyList<GltfAnimationSampler> Samplers => _samplers;
    public IReadOnlyList<GltfAnimationChannel> Channels => _channels;
    public float Duration { get; }
}

public sealed class GltfModel : IDisposable
{
    private readonly Texture2D[] _textures;
    private bool _disposed;

    internal GltfModel(GltfPrimitive[] primitives, GltfMaterial[] materials,
        GltfInstance[] instances, Texture2D[] textures, GltfNode[] nodes,
        GltfSkin[] skins, GltfAnimationClip[] animations)
    {
        Primitives = primitives;
        Materials = materials;
        Instances = instances;
        Nodes = nodes;
        Skins = skins;
        Animations = animations;
        _textures = textures;
        Bounds = CalculateBounds(primitives, instances);
    }

    public IReadOnlyList<GltfPrimitive> Primitives { get; }
    public IReadOnlyList<GltfMaterial> Materials { get; }
    public IReadOnlyList<GltfInstance> Instances { get; }
    public IReadOnlyList<GltfNode> Nodes { get; }
    public IReadOnlyList<GltfSkin> Skins { get; }
    public IReadOnlyList<GltfAnimationClip> Animations { get; }
    public IReadOnlyList<Texture2D> Textures => _textures;
    public BoundingBox Bounds { get; }

    public void Dispose()
    {
        if (_disposed) return;
        foreach (var primitive in Primitives) primitive.Mesh.Dispose();
        foreach (var texture in _textures.Distinct()) texture.Dispose();
        _disposed = true;
    }

    private static BoundingBox CalculateBounds(GltfPrimitive[] primitives, GltfInstance[] instances)
    {
        if (instances.Length == 0) return new BoundingBox(Vector3.Zero, Vector3.Zero);
        var minimum = new Vector3(float.MaxValue);
        var maximum = new Vector3(float.MinValue);
        foreach (var instance in instances)
        {
            foreach (var corner in primitives[instance.PrimitiveIndex].Bounds.GetCorners())
            {
                var transformed = Vector3.Transform(corner, instance.World);
                minimum = Vector3.Min(minimum, transformed);
                maximum = Vector3.Max(maximum, transformed);
            }
        }
        return new BoundingBox(minimum, maximum);
    }
}
