using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Nova3D.Rendering.Instancing;

[StructLayout(LayoutKind.Sequential)]
public readonly struct InstanceTransform : IVertexType
{
    public static readonly VertexDeclaration VertexDeclaration = new(
        new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
        new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2),
        new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3),
        new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 4));

    public InstanceTransform(Matrix matrix)
    {
        Row0 = new Vector4(matrix.M11, matrix.M12, matrix.M13, matrix.M14);
        Row1 = new Vector4(matrix.M21, matrix.M22, matrix.M23, matrix.M24);
        Row2 = new Vector4(matrix.M31, matrix.M32, matrix.M33, matrix.M34);
        Row3 = new Vector4(matrix.M41, matrix.M42, matrix.M43, matrix.M44);
    }

    public readonly Vector4 Row0;
    public readonly Vector4 Row1;
    public readonly Vector4 Row2;
    public readonly Vector4 Row3;
    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
}
