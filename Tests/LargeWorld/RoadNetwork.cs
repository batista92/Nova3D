using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CityBuilder.Tests.LargeWorld;

internal sealed class RoadNetwork : IDisposable
{
    private readonly VertexBuffer _vertices;
    private readonly IndexBuffer _indices;
    public int PrimitiveCount { get; }

    public RoadNetwork(GraphicsDevice device)
    {
        var vertices = new List<WorldVertex>();
        var indices = new List<ushort>();
        for (var column = 0; column < 48; column += 6)
            AddStrip(vertices, indices, true, (column - 23.5f) * 32f, -700f, 900f);
        for (var row = 0; row <= 36; row += 6)
            AddStrip(vertices, indices, false, (row - 14f) * 38f, -900f, 900f);

        _vertices = new VertexBuffer(device, WorldVertex.VertexDeclaration, vertices.Count, BufferUsage.WriteOnly);
        _vertices.SetData(vertices.ToArray());
        _indices = new IndexBuffer(device, IndexElementSize.SixteenBits, indices.Count, BufferUsage.WriteOnly);
        _indices.SetData(indices.ToArray());
        PrimitiveCount = indices.Count / 3;
    }

    private static void AddStrip(List<WorldVertex> vertices, List<ushort> indices, bool vertical,
        float fixedAxis, float start, float end)
    {
        const int segments = 48;
        const float halfWidth = 8f;
        var first = vertices.Count;
        for (var i = 0; i <= segments; i++)
        {
            var along = MathHelper.Lerp(start, end, i / (float)segments);
            for (var side = -1; side <= 1; side += 2)
            {
                var x = vertical ? fixedAxis + side * halfWidth : along;
                var z = vertical ? along : fixedAxis + side * halfWidth;
                var y = LargeWorldTerrain.SampleHeight(x, z) + 0.28f;
                const float epsilon = 1.5f;
                var normal = Vector3.Normalize(new Vector3(
                    LargeWorldTerrain.SampleHeight(x - epsilon, z) - LargeWorldTerrain.SampleHeight(x + epsilon, z),
                    epsilon * 2f,
                    LargeWorldTerrain.SampleHeight(x, z - epsilon) - LargeWorldTerrain.SampleHeight(x, z + epsilon)));
                vertices.Add(new WorldVertex(new Vector3(x, y, z), normal, new Color(47, 50, 54)));
            }
        }
        for (var i = 0; i < segments; i++)
        {
            var a = (ushort)(first + i * 2); var b = (ushort)(a + 1);
            var c = (ushort)(a + 2); var d = (ushort)(a + 3);
            if (vertical)
            {
                indices.Add(a); indices.Add(c); indices.Add(d);
                indices.Add(a); indices.Add(d); indices.Add(b);
            }
            else
            {
                // Swapping the strip axes reverses its winding. Keep the
                // geometric face consistent with the upward vertex normals.
                indices.Add(a); indices.Add(d); indices.Add(c);
                indices.Add(a); indices.Add(b); indices.Add(d);
            }
        }
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
