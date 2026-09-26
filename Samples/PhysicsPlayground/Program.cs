using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Nova3D.Physics.Bepu;
using Nova3D.Rendering;
using Nova3D.Samples;

using var game = new PhysicsPlaygroundGame();
game.Run();

sealed class PhysicsPlaygroundGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly BepuPhysicsWorld _physics = new();
    private readonly List<BepuBody> _bodies = new();
    private readonly Camera3D _camera = new()
    {
        Position = new Vector3(12f, 10f, 18f),
        Direction = Vector3.Normalize(new Vector3(-12f, -8f, -18f))
    };
    private Mesh? _cube;
    private Mesh? _sphere;
    private BasicEffect? _effect;
    private KeyboardState _previousKeyboard;

    public PhysicsPlaygroundGame()
    {
        _graphics = new GraphicsDeviceManager(this) { GraphicsProfile = GraphicsProfile.HiDef };
        Content.RootDirectory = "Content";
        Window.Title = "Nova3D Sample - PhysicsPlayground | R resets";
        IsMouseVisible = true;
    }

    protected override void LoadContent()
    {
        _cube = SampleMeshFactory.CreateCube(GraphicsDevice, Color.DarkSlateGray);
        _sphere = SampleMeshFactory.CreateSphere(GraphicsDevice, Color.Orange);
        _effect = new BasicEffect(GraphicsDevice) { VertexColorEnabled = true };
        _physics.CreateStaticBox(new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 20f));
        Spawn();
    }

    private void Spawn()
    {
        foreach (BepuBody body in _bodies) _physics.Remove(body);
        _bodies.Clear();
        for (int y = 0; y < 5; y++)
        for (int x = 0; x < 5; x++)
            _bodies.Add(_physics.CreateDynamicSphere(
                new Vector3(x * 1.15f - 2.3f, y * 1.2f + 1f, 0f), 0.5f));
    }

    protected override void Update(GameTime gameTime)
    {
        KeyboardState keyboard = Keyboard.GetState();
        if (keyboard.IsKeyDown(Keys.Escape)) Exit();
        if (keyboard.IsKeyDown(Keys.R) && _previousKeyboard.IsKeyUp(Keys.R)) Spawn();
        _previousKeyboard = keyboard;
        _physics.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
        _camera.SetViewport(GraphicsDevice.Viewport);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(22, 26, 34));
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        DrawMesh(_cube!, Matrix.CreateScale(10f, 0.5f, 10f) *
            Matrix.CreateTranslation(0f, -0.5f, 0f));
        foreach (BepuBody body in _bodies)
            DrawMesh(_sphere!, Matrix.CreateScale(0.5f) * body.WorldMatrix);
        base.Draw(gameTime);
    }

    private void DrawMesh(Mesh mesh, Matrix world)
    {
        _effect!.World = world;
        _effect.View = _camera.View;
        _effect.Projection = _camera.Projection;
        foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            mesh.Draw(GraphicsDevice);
        }
    }

    protected override void UnloadContent()
    {
        _physics.Dispose();
        _effect?.Dispose();
        _sphere?.Dispose();
        _cube?.Dispose();
        base.UnloadContent();
    }
}
