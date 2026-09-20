using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Nova3D.Rendering;

/// <summary>
/// Camera data used by Nova3D render systems. Input and camera behavior remain
/// responsibilities of the consuming game.
/// </summary>
public sealed class Camera3D
{
    private float _aspectRatio = 16f / 9f;

    public Vector3 Position { get; set; }
    public Vector3 Direction { get; set; } = Vector3.Forward;
    public Vector3 Up { get; set; } = Vector3.Up;
    public float FieldOfView { get; set; } = MathHelper.PiOver4;
    public float NearPlane { get; set; } = 0.1f;
    public float FarPlane { get; set; } = 1000f;

    public Matrix View => Matrix.CreateLookAt(Position, Position + Vector3.Normalize(Direction), Up);
    public Matrix Projection => Matrix.CreatePerspectiveFieldOfView(FieldOfView, _aspectRatio, NearPlane, FarPlane);
    public Matrix ViewProjection => View * Projection;
    public BoundingFrustum Frustum => new(ViewProjection);

    public void SetViewport(Viewport viewport) => SetAspectRatio(viewport.AspectRatio);

    public void SetAspectRatio(float aspectRatio)
    {
        if (!float.IsFinite(aspectRatio) || aspectRatio <= 0f)
            throw new ArgumentOutOfRangeException(nameof(aspectRatio));
        _aspectRatio = aspectRatio;
    }
}
