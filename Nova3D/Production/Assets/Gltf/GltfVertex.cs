using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Nova3D.Production.Assets.Gltf;

public readonly struct GltfVertex : IVertexType
{
    public static readonly VertexDeclaration VertexDeclaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0));

    public GltfVertex(Vector3 position, Vector3 normal, Vector2 textureCoordinate)
    {
        Position = position;
        Normal = normal;
        TextureCoordinate = textureCoordinate;
    }

    public Vector3 Position { get; }
    public Vector3 Normal { get; }
    public Vector2 TextureCoordinate { get; }
    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
}

public readonly struct GltfSkinnedVertex : IVertexType
{
    public static readonly VertexDeclaration VertexDeclaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
        new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.BlendIndices, 0),
        new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.BlendWeight, 0));

    public GltfSkinnedVertex(Vector3 position, Vector3 normal, Vector2 textureCoordinate,
        Vector4 blendIndices, Vector4 blendWeights)
    {
        Position = position;
        Normal = normal;
        TextureCoordinate = textureCoordinate;
        BlendIndices = blendIndices;
        BlendWeights = blendWeights;
    }

    public Vector3 Position { get; }
    public Vector3 Normal { get; }
    public Vector2 TextureCoordinate { get; }
    public Vector4 BlendIndices { get; }
    public Vector4 BlendWeights { get; }
    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
}
