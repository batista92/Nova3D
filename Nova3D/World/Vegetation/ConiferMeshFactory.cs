using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nova3D.Rendering;

namespace Nova3D.World.Vegetation;

public static class ConiferMeshFactory
{
    public static Mesh Create(GraphicsDevice device, int sides, int crownLevels)
    {
        if (sides < 3) throw new ArgumentOutOfRangeException(nameof(sides));
        if (crownLevels < 1) throw new ArgumentOutOfRangeException(nameof(crownLevels));
        var vertices = new List<VegetationVertex>();
        var indices = new List<ushort>();
        AddCylinder(vertices, indices, sides, 0.16f, 1.8f, new Color(0.26f, 0.10f, 0.035f));
        for (var level = 0; level < crownLevels; level++)
        {
            var bottom = 1.15f + level * 0.75f;
            var radius = 1.05f - level * 0.18f;
            AddCone(vertices, indices, sides, bottom, bottom + 1.8f, radius,
                new Color(0.035f + level * 0.006f, 0.28f + level * 0.025f, 0.055f));
        }
        return Mesh.Create(device, vertices.ToArray(), indices.ToArray());
    }

    private static void AddCylinder(List<VegetationVertex> vertices, List<ushort> indices,
        int sides, float radius, float height, Color color)
    {
        var start = vertices.Count;
        for (var i = 0; i < sides; i++)
        {
            var angle = i * MathF.Tau / sides;
            var normal = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
            vertices.Add(new VegetationVertex(normal * radius, normal, color));
            vertices.Add(new VegetationVertex(normal * radius + Vector3.Up * height, normal, color));
        }
        for (var i = 0; i < sides; i++)
        {
            var next = (i + 1) % sides;
            AddQuad(indices, start + i * 2, start + next * 2, start + next * 2 + 1, start + i * 2 + 1);
        }
    }

    private static void AddCone(List<VegetationVertex> vertices, List<ushort> indices,
        int sides, float bottom, float top, float radius, Color color)
    {
        var start = vertices.Count;
        for (var i = 0; i < sides; i++)
        {
            var angle = i * MathF.Tau / sides;
            var radial = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
            var normal = Vector3.Normalize(radial + Vector3.Up * (radius / (top - bottom)));
            vertices.Add(new VegetationVertex(radial * radius + Vector3.Up * bottom, normal, color));
        }
        var apex = vertices.Count;
        vertices.Add(new VegetationVertex(Vector3.Up * top, Vector3.Up, color));
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

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct VegetationVertex : IVertexType
    {
        public static readonly VertexDeclaration VertexDeclaration = new(
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
            new VertexElement(24, VertexElementFormat.Color, VertexElementUsage.Color, 0));
        public VegetationVertex(Vector3 position, Vector3 normal, Color color)
        {
            Position = position; Normal = normal; Color = color;
        }
        public readonly Vector3 Position;
        public readonly Vector3 Normal;
        public readonly Color Color;
        VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
    }
}
