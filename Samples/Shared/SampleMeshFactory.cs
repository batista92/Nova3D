using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nova3D.Rendering;

namespace Nova3D.Samples;

internal static class SampleMeshFactory
{
    public static Mesh CreateCube(GraphicsDevice device, Color color)
    {
        var vertices = new[]
        {
            new VertexPositionColor(new(-1, -1, -1), color),
            new VertexPositionColor(new( 1, -1, -1), color),
            new VertexPositionColor(new( 1,  1, -1), color),
            new VertexPositionColor(new(-1,  1, -1), color),
            new VertexPositionColor(new(-1, -1,  1), color),
            new VertexPositionColor(new( 1, -1,  1), color),
            new VertexPositionColor(new( 1,  1,  1), color),
            new VertexPositionColor(new(-1,  1,  1), color)
        };
        ushort[] indices =
        {
            0, 2, 1, 0, 3, 2, 1, 6, 5, 1, 2, 6,
            5, 7, 4, 5, 6, 7, 4, 3, 0, 4, 7, 3,
            3, 6, 2, 3, 7, 6, 4, 1, 5, 4, 0, 1
        };
        return Mesh.Create(device, vertices, indices);
    }

    public static Mesh CreateSphere(GraphicsDevice device, Color color,
        int latitudeSegments = 12, int longitudeSegments = 20)
    {
        var vertices = new List<VertexPositionColor>();
        var indices = new List<ushort>();
        for (int latitude = 0; latitude <= latitudeSegments; latitude++)
        {
            float vertical = MathHelper.Pi * latitude / latitudeSegments - MathHelper.PiOver2;
            float ring = MathF.Cos(vertical);
            float y = MathF.Sin(vertical);
            for (int longitude = 0; longitude <= longitudeSegments; longitude++)
            {
                float horizontal = MathHelper.TwoPi * longitude / longitudeSegments;
                vertices.Add(new VertexPositionColor(
                    new Vector3(ring * MathF.Cos(horizontal), y,
                        ring * MathF.Sin(horizontal)), color));
            }
        }

        int stride = longitudeSegments + 1;
        for (int latitude = 0; latitude < latitudeSegments; latitude++)
        for (int longitude = 0; longitude < longitudeSegments; longitude++)
        {
            ushort a = (ushort)(latitude * stride + longitude);
            ushort b = (ushort)(a + stride);
            indices.Add(a); indices.Add(b); indices.Add((ushort)(a + 1));
            indices.Add((ushort)(a + 1)); indices.Add(b); indices.Add((ushort)(b + 1));
        }
        return Mesh.Create(device, vertices.ToArray(), indices.ToArray());
    }
}
