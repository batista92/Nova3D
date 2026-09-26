using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nova3D.Physics.Bepu;
using Nova3D.Rendering;

namespace Nova3D.Samples.RollingBall;

internal sealed class RollingBallWorld : IDisposable
{
    private const float MarbleRadius = 0.6f;
    private readonly GraphicsDevice _device;
    private readonly BepuPhysicsWorld _physics = new(new PhysicsWorldOptions
    {
        Gravity = new Vector3(0f, -18f, 0f),
        FixedTimeStep = 1f / 60f
    });
    private readonly Mesh _cube;
    private readonly Mesh _sphere;
    private readonly BasicEffect _effect;
    private readonly List<(Vector3 Position, Vector3 Size)> _obstacles = new();
    private readonly Camera3D _camera = new()
    {
        Position = new Vector3(0f, 8f, 19f),
        Direction = Vector3.Normalize(new Vector3(0f, -6f, -11f)),
        NearPlane = 0.1f,
        FarPlane = 150f
    };
    private readonly BepuBody _marble;
    private Vector3 _spawn = new(0f, 2f, 8f);

    public RollingBallWorld(GraphicsDevice device)
    {
        _device = device;
        _cube = SampleMeshFactory.CreateCube(device, Color.White);
        _sphere = SampleMeshFactory.CreateSphere(device, Color.White);
        _effect = new BasicEffect(device) { VertexColorEnabled = false };

        AddStatic(new Vector3(0f, -0.5f, -6f), new Vector3(22f, 1f, 38f));
        AddStatic(new Vector3(-4f, 1f, 1f), new Vector3(7f, 2f, 1f));
        AddStatic(new Vector3(4f, 1f, -5f), new Vector3(7f, 2f, 1f));
        AddStatic(new Vector3(0f, 0.75f, -12f), new Vector3(3f, 1.5f, 3f));
        _marble = _physics.CreateDynamicSphere(_spawn, MarbleRadius, 2f);
    }

    public Vector3 MarblePosition => _marble.Position;
    public BoundingBox CheckpointBounds { get; } =
        new(new Vector3(-9f, -1f, -8f), new Vector3(9f, 4f, -6f));
    public BoundingBox FinishBounds { get; } =
        new(new Vector3(-9f, -1f, -24f), new Vector3(9f, 4f, -20f));

    private void AddStatic(Vector3 position, Vector3 size)
    {
        _physics.CreateStaticBox(position, size);
        _obstacles.Add((position, size));
    }

    public void Update(GameTime gameTime, Vector2 input)
    {
        if (input.LengthSquared() > 1f) input.Normalize();
        Vector3 velocity = _marble.LinearVelocity;
        Vector3 desired = new(input.X * 9f, velocity.Y, input.Y * 9f);
        float steering = 1f - MathF.Exp(-10f * (float)gameTime.ElapsedGameTime.TotalSeconds);
        _marble.LinearVelocity = Vector3.Lerp(velocity, desired, steering);
        _physics.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

        Vector3 target = _marble.Position;
        Vector3 wanted = target + new Vector3(0f, 7f, 11f);
        float follow = 1f - MathF.Exp(-7f * (float)gameTime.ElapsedGameTime.TotalSeconds);
        _camera.Position = Vector3.Lerp(_camera.Position, wanted, follow);
        _camera.Direction = Vector3.Normalize(target - _camera.Position);
        _camera.SetViewport(_device.Viewport);
    }

    public void ActivateCheckpoint()
    {
        _spawn = new Vector3(0f, 2f, -5f);
    }

    public void Respawn(bool resetCheckpoint)
    {
        if (resetCheckpoint) _spawn = new Vector3(0f, 2f, 8f);
        _marble.SetPose(_spawn, Quaternion.Identity);
        _marble.LinearVelocity = Vector3.Zero;
        _camera.Position = _spawn + new Vector3(0f, 7f, 11f);
    }

    public void Draw(bool checkpointReached)
    {
        _device.DepthStencilState = DepthStencilState.Default;
        _device.BlendState = BlendState.Opaque;
        _device.RasterizerState = RasterizerState.CullCounterClockwise;
        foreach ((Vector3 position, Vector3 size) in _obstacles)
            DrawMesh(_cube, Matrix.CreateScale(size * 0.5f) *
                Matrix.CreateTranslation(position), new Color(70, 86, 110));

        DrawMarker(new Vector3(0f, 0.08f, -7f),
            checkpointReached ? Color.LimeGreen : Color.Gold);
        DrawMarker(new Vector3(0f, 0.08f, -22f), Color.CornflowerBlue);
        DrawMesh(_sphere, Matrix.CreateScale(MarbleRadius) * _marble.WorldMatrix,
            Color.OrangeRed);
    }

    private void DrawMarker(Vector3 position, Color color) =>
        DrawMesh(_cube, Matrix.CreateScale(9f, 0.05f, 1f) *
            Matrix.CreateTranslation(position), color);

    private void DrawMesh(Mesh mesh, Matrix world, Color color)
    {
        _effect.World = world;
        _effect.View = _camera.View;
        _effect.Projection = _camera.Projection;
        _effect.DiffuseColor = color.ToVector3();
        foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            mesh.Draw(_device);
        }
    }

    public void Dispose()
    {
        _physics.Dispose();
        _effect.Dispose();
        _sphere.Dispose();
        _cube.Dispose();
    }
}
