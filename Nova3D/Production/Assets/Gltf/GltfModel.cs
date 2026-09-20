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

public sealed record GltfPrimitive(Mesh Mesh, int? MaterialIndex, BoundingBox Bounds);

public sealed record GltfInstance(string Name, int PrimitiveIndex, Matrix World);

public sealed class GltfModel : IDisposable
{
    private readonly Texture2D[] _textures;
    private bool _disposed;

    internal GltfModel(GltfPrimitive[] primitives, GltfMaterial[] materials,
        GltfInstance[] instances, Texture2D[] textures)
    {
        Primitives = primitives;
        Materials = materials;
        Instances = instances;
        _textures = textures;
        Bounds = CalculateBounds(primitives, instances);
    }

    public IReadOnlyList<GltfPrimitive> Primitives { get; }
    public IReadOnlyList<GltfMaterial> Materials { get; }
    public IReadOnlyList<GltfInstance> Instances { get; }
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
