using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Nova3D.Rendering;
using Nova3D.Samples;

using var game = new Minimal3DGame();
game.Run();

sealed class Minimal3DGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private Mesh? _cube;
    private BasicEffect? _effect;
    private readonly Camera3D _camera = new()
    {
        Position = new Vector3(0f, 2.5f, 6f),
        Direction = Vector3.Normalize(new Vector3(0f, -0.25f, -1f))
    };
    private float _rotation;

    public Minimal3DGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            GraphicsProfile = GraphicsProfile.HiDef
        };
        Content.RootDirectory = "Content";
        Window.Title = "Nova3D Sample - Minimal3D";
        Window.AllowUserResizing = true;
    }

    protected override void LoadContent()
    {
        _cube = SampleMeshFactory.CreateCube(GraphicsDevice, Color.CornflowerBlue);
        _effect = new BasicEffect(GraphicsDevice) { VertexColorEnabled = true };
    }

    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape)) Exit();
        _rotation += (float)gameTime.ElapsedGameTime.TotalSeconds;
        _camera.SetViewport(GraphicsDevice.Viewport);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(18, 22, 30));
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        _effect!.World = Matrix.CreateRotationY(_rotation) *
                         Matrix.CreateRotationX(_rotation * 0.35f);
        _effect.View = _camera.View;
        _effect.Projection = _camera.Projection;
        foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _cube!.Draw(GraphicsDevice);
        }
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        _effect?.Dispose();
        _cube?.Dispose();
        base.UnloadContent();
    }
}
