using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CityBuilder.Tests.LargeWorld;

internal sealed class LargeWorldCamera
{
    private MouseState _previousMouse;
    private bool _mouseInitialized;
    private float _yaw = MathHelper.Pi;
    private float _pitch = -0.35f;

    public Vector3 Position { get; private set; } = new(0f, 180f, 430f);
    public Matrix View => Matrix.CreateLookAt(Position, Position + Forward, Vector3.Up);
    public Vector3 Direction => Forward;

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
        if (movement.LengthSquared() > 0f) Position += Vector3.Normalize(movement) * speed * dt;

        // Keep the free camera above the surface. Looking at the scene from
        // below made one-sided road/water geometry appear inverted.
        var minimumHeight = LargeWorldTerrain.SampleHeight(Position.X, Position.Z) + 4f;
        if (Position.Y < minimumHeight)
            Position = new Vector3(Position.X, minimumHeight, Position.Z);
    }
}
