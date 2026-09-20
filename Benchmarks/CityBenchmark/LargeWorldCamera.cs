using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Nova3D.Rendering;

namespace CityBuilder.Benchmarks.CityBenchmark;

internal sealed class LargeWorldCamera
{
    private readonly Camera3D _camera = new()
    {
        Position = new Vector3(0f, 180f, 430f),
        NearPlane = 1f,
        FarPlane = 3200f,
        FieldOfView = MathHelper.PiOver4
    };
    private MouseState _previousMouse;
    private bool _mouseInitialized;
    private float _yaw = MathHelper.Pi;
    private float _pitch = -0.35f;

    public Camera3D Camera => _camera;
    public Vector3 Position => _camera.Position;
    public Matrix View => _camera.View;
    public Matrix Projection => _camera.Projection;
    public Vector3 Direction => Forward;

    public void SetViewport(Viewport viewport) => _camera.SetViewport(viewport);

    private Vector3 Forward => Vector3.Normalize(new Vector3(
        MathF.Sin(_yaw) * MathF.Cos(_pitch), MathF.Sin(_pitch), MathF.Cos(_yaw) * MathF.Cos(_pitch)));

    public void Update(GameTime gameTime, GameWindow window)
    {
        var mouse = Mouse.GetState(window);
        if (!_mouseInitialized) { _previousMouse = mouse; _mouseInitialized = true; }
        if (mouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Pressed)
        {
            _yaw -= (mouse.X - _previousMouse.X) * 0.004f;
            _pitch = MathHelper.Clamp(_pitch - (mouse.Y - _previousMouse.Y) * 0.004f, -1.45f, 1.45f);
        }
        _previousMouse = mouse;

        var keyboard = Keyboard.GetState();
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var speed = keyboard.IsKeyDown(Keys.LeftShift) ? 520f : 130f;
        var flatForward = Vector3.Normalize(new Vector3(Forward.X, 0f, Forward.Z));
        var right = Vector3.Normalize(Vector3.Cross(flatForward, Vector3.Up));
        var movement = Vector3.Zero;
        if (keyboard.IsKeyDown(Keys.W)) movement += flatForward;
        if (keyboard.IsKeyDown(Keys.S)) movement -= flatForward;
        if (keyboard.IsKeyDown(Keys.D)) movement += right;
        if (keyboard.IsKeyDown(Keys.A)) movement -= right;
        if (keyboard.IsKeyDown(Keys.E)) movement += Vector3.Up;
        if (keyboard.IsKeyDown(Keys.Q)) movement -= Vector3.Up;
        if (movement.LengthSquared() > 0f) _camera.Position += Vector3.Normalize(movement) * speed * dt;

        // Keep the free camera above the surface. Looking at the scene from
        // below made one-sided road/water geometry appear inverted.
        var minimumHeight = LargeWorldTerrain.SampleHeight(Position.X, Position.Z) + 4f;
        if (Position.Y < minimumHeight)
            _camera.Position = new Vector3(Position.X, minimumHeight, Position.Z);
        _camera.Direction = Forward;
    }
}
