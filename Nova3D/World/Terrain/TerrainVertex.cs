using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Nova3D.World.Terrain;

[StructLayout(LayoutKind.Sequential)]
public readonly struct TerrainVertex : IVertexType
{
    public static readonly VertexDeclaration VertexDeclaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Color, VertexElementUsage.Color, 0));

    public TerrainVertex(Vector3 position, Vector3 normal, Color color)
    {
        Position = position;
        Normal = normal;
        Color = color;
    }

    public readonly Vector3 Position;
    public readonly Vector3 Normal;
    public readonly Color Color;
    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
}
