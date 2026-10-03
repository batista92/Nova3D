using Microsoft.Xna.Framework;

namespace Nova3D.Production.Scenes;

/// <summary>Serializable local transform expressed with MonoGame vectors.</summary>
public sealed class SceneTransformDocument
{
    public SceneTransformDocument(Vector3 position, Vector3 rotationDegrees, Vector3 scale)
    {
        Position = position;
        RotationDegrees = rotationDegrees;
        Scale = scale;
    }

    public Vector3 Position { get; }
    public Vector3 RotationDegrees { get; }
    public Vector3 Scale { get; }

    public Matrix CreateLocalMatrix()
    {
        return Matrix.CreateScale(Scale) *
               Matrix.CreateFromYawPitchRoll(
                   MathHelper.ToRadians(RotationDegrees.Y),
                   MathHelper.ToRadians(RotationDegrees.X),
                   MathHelper.ToRadians(RotationDegrees.Z)) *
               Matrix.CreateTranslation(Position);
    }
}
