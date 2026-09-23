using XnaMatrix = Microsoft.Xna.Framework.Matrix;
using XnaQuaternion = Microsoft.Xna.Framework.Quaternion;
using XnaVector3 = Microsoft.Xna.Framework.Vector3;
using NumericsMatrix = System.Numerics.Matrix4x4;
using NumericsQuaternion = System.Numerics.Quaternion;
using NumericsVector3 = System.Numerics.Vector3;

namespace Nova3D.Physics.Bepu;

public static class BepuConversions
{
    public static NumericsVector3 ToNumerics(this XnaVector3 value) =>
        new(value.X, value.Y, value.Z);

    public static XnaVector3 ToMonoGame(this NumericsVector3 value) =>
        new(value.X, value.Y, value.Z);

    public static NumericsQuaternion ToNumerics(this XnaQuaternion value) =>
        new(value.X, value.Y, value.Z, value.W);

    public static XnaQuaternion ToMonoGame(this NumericsQuaternion value) =>
        new(value.X, value.Y, value.Z, value.W);

    public static XnaMatrix ToMonoGameMatrix(this NumericsVector3 position, NumericsQuaternion orientation)
    {
        NumericsMatrix numerics = NumericsMatrix.CreateFromQuaternion(orientation) *
                                  NumericsMatrix.CreateTranslation(position);
        return new XnaMatrix(
            numerics.M11, numerics.M12, numerics.M13, numerics.M14,
            numerics.M21, numerics.M22, numerics.M23, numerics.M24,
            numerics.M31, numerics.M32, numerics.M33, numerics.M34,
            numerics.M41, numerics.M42, numerics.M43, numerics.M44);
    }
}
