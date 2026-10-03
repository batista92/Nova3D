using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Nova3D.Production.Debugging;
using Nova3D.Production.Scenes;
using Nova3D.Production.Scenes.Assets;
using Nova3D.Production.Scenes.BuiltIns;
using Nova3D.Production.Scenes.Prefabs;
using Nova3D.Rendering;
using Nova3D.Samples;

using var game = new DataDrivenSceneGame();
game.Run();

sealed class DataDrivenSceneGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly List<(string Name, Vector3 Position)> _labels = [];
    private SceneInstance? _scene;
    private Camera3D? _camera;
    private RenderContext? _context;
    private Mesh? _cube;
    private BasicEffect? _effect;
    private DebugRenderer? _debug;
    private SpriteBatch? _spriteBatch;
    private SpriteFont? _font;
    private bool _showDebug = true;
    private KeyboardState _previousKeyboard;

    public DataDrivenSceneGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            GraphicsProfile = GraphicsProfile.HiDef,
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720
        };
        Content.RootDirectory = "Content";
        Window.Title = "Nova3D Sample - DataDrivenScene | F3 debug";
        Window.AllowUserResizing = true;
    }

    protected override void LoadContent()
    {
        var assetsRoot = Path.Combine(AppContext.BaseDirectory, "Assets");
        var scenePath = Path.Combine(assetsRoot, "Scenes", "level.scene.json");
        var components = new SceneComponentRegistry();
        components.RegisterNova3DBuiltIns();
        var plan = ScenePrefabLoader.Prepare(scenePath, components, new SceneAssetResolver(assetsRoot));
        _scene = new SceneInstantiator().Instantiate(plan);
        _camera = _scene.GetComponents<SceneCameraComponent>().Single(camera => camera.IsPrimary).Camera;
        _context = new RenderContext(GraphicsDevice);
        _cube = SampleMeshFactory.CreateCube(GraphicsDevice, Color.CornflowerBlue);
        _effect = new BasicEffect(GraphicsDevice) { VertexColorEnabled = true, LightingEnabled = false };
        _debug = new DebugRenderer(GraphicsDevice);
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _font = Content.Load<SpriteFont>("DebugFont");
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        if (keyboard.IsKeyDown(Keys.Escape)) Exit();
        if (keyboard.IsKeyDown(Keys.F3) && !_previousKeyboard.IsKeyDown(Keys.F3))
            _showDebug = !_showDebug;
        _previousKeyboard = keyboard;
        _camera?.SetViewport(GraphicsDevice.Viewport);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(18, 22, 30));
        var scene = _scene!;
        var camera = _camera!;
        var context = _context!;
        context.BeginFrame(camera);
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.BlendState = BlendState.Opaque;
        GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        _effect!.View = camera.View;
        _effect.Projection = camera.Projection;
        foreach (var node in scene.Nodes)
        {
            if (!node.TryGetComponent<SceneTag>("tag", out _))
                continue;
            _effect.World = Matrix.CreateScale(0.55f) * node.WorldTransform;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _cube!.Draw(GraphicsDevice);
                context.Statistics.RecordDraw(_cube.PrimitiveCount);
            }
        }

        if (_showDebug)
        {
            _labels.Clear();
            SceneDebugVisualization.Queue(scene, _debug!, (name, position) =>
                _labels.Add((name, position)));
            _debug!.Flush(context);
            _spriteBatch!.Begin();
            foreach (var (name, world) in _labels)
            {
                var screen = GraphicsDevice.Viewport.Project(world, camera.Projection, camera.View, Matrix.Identity);
                if (screen.Z is >= 0f and <= 1f)
                    _spriteBatch.DrawString(_font!, name, new Vector2(screen.X, screen.Y), Color.White);
            }
            _spriteBatch.End();
        }
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        _scene?.Dispose();
        _spriteBatch?.Dispose();
        _debug?.Dispose();
        _effect?.Dispose();
        _cube?.Dispose();
        base.UnloadContent();
    }
}
