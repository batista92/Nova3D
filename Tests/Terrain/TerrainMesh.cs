using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CityBuilder.Tests.Terrain;

internal sealed class TerrainMesh : IDisposable
{
    private readonly VertexBuffer _vertices;
    private readonly IndexBuffer _indices;

    public TerrainMesh(GraphicsDevice device, int segments = 128, float size = 28f)
    {
        var stride = segments + 1;
        var vertices = new TerrainVertex[stride * stride];
        for (var z = 0; z <= segments; z++)
        for (var x = 0; x <= segments; x++)
        {
            var worldX = (x / (float)segments - 0.5f) * size;
            var worldZ = (z / (float)segments - 0.5f) * size;
            var height = SampleHeight(worldX, worldZ);
            const float epsilon = 0.08f;
            var dx = SampleHeight(worldX + epsilon, worldZ) - SampleHeight(worldX - epsilon, worldZ);
            var dz = SampleHeight(worldX, worldZ + epsilon) - SampleHeight(worldX, worldZ - epsilon);
            var normal = Vector3.Normalize(new Vector3(-dx, epsilon * 2f, -dz));
            vertices[z * stride + x] = new TerrainVertex(new Vector3(worldX, height, worldZ), normal);
        }

        var indices = new ushort[segments * segments * 6];
        var cursor = 0;
        for (var z = 0; z < segments; z++)
        for (var x = 0; x < segments; x++)
        {
            var topLeft = (ushort)(z * stride + x);
            var topRight = (ushort)(topLeft + 1);
            var bottomLeft = (ushort)(topLeft + stride);
            var bottomRight = (ushort)(bottomLeft + 1);
            indices[cursor++] = topLeft;
            indices[cursor++] = bottomRight;
            indices[cursor++] = topRight;
            indices[cursor++] = topLeft;
            indices[cursor++] = bottomLeft;
            indices[cursor++] = bottomRight;
        }

        _vertices = new VertexBuffer(device, TerrainVertex.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
        _vertices.SetData(vertices);
        _indices = new IndexBuffer(device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
        _indices.SetData(indices);
        PrimitiveCount = indices.Length / 3;
    }

    public int PrimitiveCount { get; }

    public void Draw(GraphicsDevice device)
    {
        device.SetVertexBuffer(_vertices);
        device.Indices = _indices;
        device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, PrimitiveCount);
    }

    private static float SampleHeight(float x, float z)
    {
        var rolling = MathF.Sin(x * 0.32f) * MathF.Cos(z * 0.27f) * 0.55f;
        var mountain = 4.2f * MathF.Exp(-(x * x + z * z) / 32f);
        var ridge = 2.2f * MathF.Exp(-((x + 6f) * (x + 6f) + (z - 3f) * (z - 3f)) / 9f);
        var basin = -1.35f * MathF.Exp(-((x - 7f) * (x - 7f) + (z + 5f) * (z + 5f)) / 16f);
        return rolling + mountain + ridge + basin - 0.8f;
    }

    public void Dispose()
    {
        _vertices.Dispose();
        _indices.Dispose();
    }
}

internal readonly struct TerrainVertex : IVertexType
{
    public static readonly VertexDeclaration VertexDeclaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0));

    public TerrainVertex(Vector3 position, Vector3 normal) { Position = position; Normal = normal; }
    public Vector3 Position { get; }
    public Vector3 Normal { get; }
    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
}
