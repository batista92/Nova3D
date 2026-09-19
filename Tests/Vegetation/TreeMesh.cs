using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CityBuilder.Tests.Vegetation;

internal sealed class TreeMesh : IDisposable
{
    public TreeMesh(GraphicsDevice device, int sides, int crownLevels)
    {
        var vertices = new List<TreeVertex>();
        var indices = new List<ushort>();
        AddCylinder(vertices, indices, sides, 0.16f, 1.8f, new Color(0.26f, 0.10f, 0.035f));
        for (var level = 0; level < crownLevels; level++)
        {
            var bottom = 1.15f + level * 0.75f;
            var radius = 1.05f - level * 0.18f;
            AddCone(vertices, indices, sides, bottom, bottom + 1.8f, radius,
                new Color(0.035f + level * 0.006f, 0.28f + level * 0.025f, 0.055f));
        }
        VertexBuffer = new VertexBuffer(device, TreeVertex.VertexDeclaration, vertices.Count, BufferUsage.WriteOnly);
        VertexBuffer.SetData(vertices.ToArray());
        IndexBuffer = new IndexBuffer(device, IndexElementSize.SixteenBits, indices.Count, BufferUsage.WriteOnly);
        IndexBuffer.SetData(indices.ToArray());
        VertexCount = vertices.Count;
        PrimitiveCount = indices.Count / 3;
    }

    public VertexBuffer VertexBuffer { get; }
    public IndexBuffer IndexBuffer { get; }
    public int VertexCount { get; }
    public int PrimitiveCount { get; }

    private static void AddCylinder(List<TreeVertex> vertices, List<ushort> indices, int sides, float radius, float height, Color color)
    {
        var start = vertices.Count;
        for (var i = 0; i < sides; i++)
        {
            var angle = i * MathF.Tau / sides;
            var normal = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
            vertices.Add(new TreeVertex(normal * radius, normal, color));
            vertices.Add(new TreeVertex(normal * radius + Vector3.Up * height, normal, color));
        }
        for (var i = 0; i < sides; i++)
        {
            var next = (i + 1) % sides;
            AddQuad(indices, start + i * 2, start + next * 2, start + next * 2 + 1, start + i * 2 + 1);
        }
    }

    private static void AddCone(List<TreeVertex> vertices, List<ushort> indices, int sides, float bottom, float top, float radius, Color color)
    {
        var start = vertices.Count;
        for (var i = 0; i < sides; i++)
        {
            var angle = i * MathF.Tau / sides;
            var radial = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
            var normal = Vector3.Normalize(radial + Vector3.Up * (radius / (top - bottom)));
            vertices.Add(new TreeVertex(radial * radius + Vector3.Up * bottom, normal, color));
        }
        var apex = vertices.Count;
        vertices.Add(new TreeVertex(Vector3.Up * top, Vector3.Up, color));
        for (var i = 0; i < sides; i++)
        {
            indices.Add((ushort)(start + i));
            indices.Add((ushort)apex);
            indices.Add((ushort)(start + (i + 1) % sides));
        }
    }

    private static void AddQuad(List<ushort> indices, int a, int b, int c, int d)
    {
        indices.Add((ushort)a); indices.Add((ushort)c); indices.Add((ushort)b);
        indices.Add((ushort)a); indices.Add((ushort)d); indices.Add((ushort)c);
    }

    public void Dispose() { VertexBuffer.Dispose(); IndexBuffer.Dispose(); }
}

internal readonly struct TreeVertex : IVertexType
{
    public static readonly VertexDeclaration VertexDeclaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Color, VertexElementUsage.Color, 0));
    public TreeVertex(Vector3 position, Vector3 normal, Color color) { Position = position; Normal = normal; Color = color; }
    public Vector3 Position { get; }
    public Vector3 Normal { get; }
    public Color Color { get; }
    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
}
