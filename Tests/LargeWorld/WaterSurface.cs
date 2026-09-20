using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CityBuilder.Tests.LargeWorld;

internal sealed class WaterSurface : IDisposable
{
    private readonly VertexBuffer _vertices;
    private readonly IndexBuffer _indices;
    public int PrimitiveCount { get; }

    public WaterSurface(GraphicsDevice device)
    {
        const int segments = 64;
        const float centerX = 420f, centerZ = -330f;
        var waterLevel = LargeWorldTerrain.SampleHeight(centerX, centerZ) + 5f;
        var vertices = new WorldVertex[segments + 1];
        vertices[0] = new WorldVertex(new Vector3(centerX, waterLevel, centerZ), Vector3.Up, new Color(25, 91, 132));
        for (var i = 0; i < segments; i++)
        {
            var angle = i * MathF.Tau / segments;
            var shoreline = 0.92f + MathF.Sin(angle * 5f) * 0.05f + MathF.Sin(angle * 9f) * 0.025f;
            var wx = centerX + MathF.Cos(angle) * 235f * shoreline;
            var wz = centerZ + MathF.Sin(angle) * 165f * shoreline;
            var ripple = MathF.Sin(wx * 0.08f + wz * 0.05f) * 0.18f;
            vertices[i + 1] = new WorldVertex(new Vector3(wx, waterLevel + ripple, wz), Vector3.Up, new Color(32, 105, 145));
        }
        var indices = new ushort[segments * 3];
        for (var i = 0; i < segments; i++)
        {
            indices[i * 3] = 0;
            indices[i * 3 + 1] = (ushort)((i + 1) % segments + 1);
            indices[i * 3 + 2] = (ushort)(i + 1);
        }
        _vertices = new VertexBuffer(device, WorldVertex.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
        _vertices.SetData(vertices);
        _indices = new IndexBuffer(device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
        _indices.SetData(indices);
        PrimitiveCount = indices.Length / 3;
    }

    public void Draw(GraphicsDevice device, Effect effect)
    {
        device.SetVertexBuffer(_vertices);
        device.Indices = _indices;
        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, PrimitiveCount);
        }
    }

    public void Dispose() { _vertices.Dispose(); _indices.Dispose(); }
}
