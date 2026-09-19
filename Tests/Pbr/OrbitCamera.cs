using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CityBuilder.Tests.Pbr;

internal sealed class OrbitCamera
{
    private float _yaw = MathHelper.ToRadians(35f);
    private float _pitch = MathHelper.ToRadians(-20f);
    private float _distance;
    private readonly float _minimumDistance;
    private readonly float _maximumDistance;
    private MouseState _previousMouse;
    private bool _initialized;

    public OrbitCamera(Vector3? target = null, float distance = 11f, float minimumDistance = 4f, float maximumDistance = 24f)
    {
        Target = target ?? new Vector3(0f, 0.8f, 0f);
        _distance = distance;
        _minimumDistance = minimumDistance;
        _maximumDistance = maximumDistance;
    }

    public Vector3 Target { get; }
    public Vector3 Position { get; private set; }
    public Matrix View { get; private set; }

    public void Update(GameWindow window)
    {
        var mouse = Mouse.GetState(window);
        if (!_initialized)
        {
            _previousMouse = mouse;
            _initialized = true;
        }

        if (mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Pressed)
        {
            _yaw -= (mouse.X - _previousMouse.X) * 0.01f;
            _pitch -= (mouse.Y - _previousMouse.Y) * 0.01f;
            _pitch = MathHelper.Clamp(_pitch, -1.35f, 1.35f);
        }

        _distance -= (mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue) * 0.005f;
        _distance = MathHelper.Clamp(_distance, _minimumDistance, _maximumDistance);
        _previousMouse = mouse;

        var rotation = Matrix.CreateFromYawPitchRoll(_yaw, _pitch, 0f);
        Position = Target + Vector3.Transform(Vector3.Backward * _distance, rotation);
        View = Matrix.CreateLookAt(Position, Target, Vector3.Up);
    }
}
