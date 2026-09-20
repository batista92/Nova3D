using Microsoft.Xna.Framework;

namespace Nova3D.Rendering.Lighting;

public sealed class DirectionalLight
{
    private Vector3 _direction;

    public DirectionalLight(Vector3 direction, Vector3 color, float intensity = 1f)
    {
        Direction = direction;
        Color = color;
        Intensity = intensity;
    }

    public Vector3 Direction
    {
        get => _direction;
        set
        {
            if (!IsFinite(value) || value.LengthSquared() < 0.000001f)
                throw new ArgumentOutOfRangeException(nameof(value));
            _direction = Vector3.Normalize(value);
        }
    }

    public Vector3 Color { get; set; }
    public float Intensity { get; set; }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
