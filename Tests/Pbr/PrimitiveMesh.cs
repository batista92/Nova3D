using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CityBuilder.Tests.Pbr;

internal sealed class PrimitiveMesh : IDisposable
{
    private readonly VertexBuffer _vertexBuffer;
    private readonly IndexBuffer _indexBuffer;

    private PrimitiveMesh(GraphicsDevice device, VertexPositionNormal[] vertices, ushort[] indices)
    {
        _vertexBuffer = new VertexBuffer(device, VertexPositionNormal.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
        _vertexBuffer.SetData(vertices);
        _indexBuffer = new IndexBuffer(device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
        _indexBuffer.SetData(indices);
        PrimitiveCount = indices.Length / 3;
    }

    public int PrimitiveCount { get; }

    public void Draw(GraphicsDevice device)
    {
        device.SetVertexBuffer(_vertexBuffer);
        device.Indices = _indexBuffer;
        device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, PrimitiveCount);
    }

    public static PrimitiveMesh CreateSphere(GraphicsDevice device, int tessellation = 48)
    {
        var horizontalSegments = tessellation * 2;
        var vertices = new List<VertexPositionNormal>();
        var indices = new List<ushort>();

        for (var i = 0; i <= tessellation; i++)
        {
            var latitude = (i * MathF.PI / tessellation) - MathF.PI / 2f;
            var dy = MathF.Sin(latitude);
            var dxz = MathF.Cos(latitude);

            for (var j = 0; j <= horizontalSegments; j++)
            {
                var longitude = j * MathF.Tau / horizontalSegments;
                var normal = new Vector3(MathF.Cos(longitude) * dxz, dy, MathF.Sin(longitude) * dxz);
                vertices.Add(new VertexPositionNormal(normal, normal));
            }
        }

        var stride = horizontalSegments + 1;
        for (var i = 0; i < tessellation; i++)
        for (var j = 0; j < horizontalSegments; j++)
        {
            var next = (ushort)(i * stride + j);
            var nextRow = (ushort)(next + stride);
            indices.Add(next);
            indices.Add(nextRow);
            indices.Add((ushort)(next + 1));
            indices.Add((ushort)(next + 1));
            indices.Add(nextRow);
            indices.Add((ushort)(nextRow + 1));
        }

        return new PrimitiveMesh(device, vertices.ToArray(), indices.ToArray());
    }

    public static PrimitiveMesh CreatePlane(GraphicsDevice device, float halfExtent = 6f)
    {
        var normal = Vector3.Up;
        var vertices = new[]
        {
            new VertexPositionNormal(new Vector3(-halfExtent, 0f, -halfExtent), normal),
            new VertexPositionNormal(new Vector3(halfExtent, 0f, -halfExtent), normal),
            new VertexPositionNormal(new Vector3(halfExtent, 0f, halfExtent), normal),
            new VertexPositionNormal(new Vector3(-halfExtent, 0f, halfExtent), normal)
        };
        // Winding visto de cima, coerente com a normal +Y e com as esferas.
        return new PrimitiveMesh(device, vertices, new ushort[] { 0, 2, 1, 0, 3, 2 });
    }

    public static PrimitiveMesh CreateCube(GraphicsDevice device)
    {
        var p = new[]
        {
            new Vector3(-1, -1, -1), new Vector3(1, -1, -1),
            new Vector3(1, 1, -1), new Vector3(-1, 1, -1),
            new Vector3(-1, -1, 1), new Vector3(1, -1, 1),
            new Vector3(1, 1, 1), new Vector3(-1, 1, 1)
        };
        var vertices = p.Select(position => new VertexPositionNormal(position, Vector3.Normalize(position))).ToArray();
        ushort[] indices =
        {
            0, 2, 1, 0, 3, 2, 1, 6, 5, 1, 2, 6,
            5, 7, 4, 5, 6, 7, 4, 3, 0, 4, 7, 3,
            3, 6, 2, 3, 7, 6, 4, 1, 5, 4, 0, 1
        };
        return new PrimitiveMesh(device, vertices, indices);
    }

    public void Dispose()
    {
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
    }
}

internal readonly struct VertexPositionNormal : IVertexType
{
    public static readonly VertexDeclaration VertexDeclaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0));

    public VertexPositionNormal(Vector3 position, Vector3 normal, Vector2 textureCoordinate = default)
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
