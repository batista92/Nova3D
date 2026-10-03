using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Nova3D.Production.Assets.Gltf;
using Nova3D.Production.Debugging;
using Nova3D.Rendering;
using Nova3D.Rendering.Lighting;
using Nova3D.Rendering.Models;
using NovaDirectionalLight = Nova3D.Rendering.Lighting.DirectionalLight;

using var game = new AnimatedCharacterGame();
game.Run();

sealed class AnimatedCharacterGame : Game
{
    private static readonly CubeMapFace[] CubeFaces = Enum.GetValues<CubeMapFace>();
    private readonly GraphicsDeviceManager _graphics;
    private readonly Camera3D _camera = new()
    {
        Position = new Vector3(0f, 1.8f, 5f),
        Direction = Vector3.Normalize(new Vector3(0f, -0.12f, -1f)),
        FarPlane = 100f
    };
    private RenderContext? _context;
    private GltfModel? _model;
    private GltfSkeletonPose? _pose;
    private GltfAnimationPlayer? _player;
    private GltfModelRenderer? _renderer;
    private DebugRenderer? _debug;
    private SpriteBatch? _spriteBatch;
    private SpriteFont? _font;
    private TextureCube? _environmentMap;
    private TextureCube? _irradianceMap;
    private TextureCube? _prefilteredMap;
    private Texture2D? _brdfLut;
    private Texture2D? _whiteShadow;
    private Effect? _shadowEffect;
    private RenderTarget2D? _shadowTarget;
    private KeyboardState _previousKeyboard;
    private bool _showDebug = true;
    private string? _error;
    private int _clipIndex;
    private readonly bool _smoke = Environment.GetCommandLineArgs().Contains("--smoke");
    private int _smokeFrames;

    public AnimatedCharacterGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            GraphicsProfile = GraphicsProfile.HiDef,
            SynchronizeWithVerticalRetrace = true
        };
        Content.RootDirectory = "Content";
        Window.Title = "Nova3D Sample - AnimatedCharacter";
        Window.AllowUserResizing = true;
        IsMouseVisible = true;
    }

    protected override void LoadContent()
    {
        _context = new RenderContext(GraphicsDevice);
        _debug = new DebugRenderer(GraphicsDevice);
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _font = Content.Load<SpriteFont>("DebugFont");
        try
        {
            var path = ResolveModelPath();
            _model = new GltfImporter(GraphicsDevice).Load(path);
            if (_model.Animations.Count == 0 || _model.Skins.Count == 0)
                throw new InvalidDataException("The selected GLB has no skin and animation clips.");
            _pose = new GltfSkeletonPose(_model);
            _player = new GltfAnimationPlayer(_model, _pose);
            _player.Play(0);

            var pbr = Content.Load<Effect>("Shaders/PBR");
            _shadowEffect = Content.Load<Effect>("Shaders/ShadowDepth");
            _shadowTarget = new RenderTarget2D(GraphicsDevice, 512, 512, false,
                SurfaceFormat.Single, DepthFormat.Depth24);
            CreateNeutralEnvironment();
            ConfigureUnshadowedPbr(pbr);
            var light = new NovaDirectionalLight(new Vector3(-0.4f, -1f, -0.25f), Vector3.One, 3f);
            var environment = new ImageBasedLighting(_environmentMap!, _irradianceMap!,
                _prefilteredMap!, _brdfLut!, 1);
            _renderer = new GltfModelRenderer(_model, pbr, light, environment, _pose);

            var bounds = _model.Bounds;
            var size = bounds.Max - bounds.Min;
            var largest = MathF.Max(size.X, MathF.Max(size.Y, size.Z));
            var scale = largest > 0.0001f ? 2.5f / largest : 1f;
            var centerX = (bounds.Min.X + bounds.Max.X) * 0.5f;
            var centerZ = (bounds.Min.Z + bounds.Max.Z) * 0.5f;
            _renderer.Transform = Matrix.CreateTranslation(-centerX, -bounds.Min.Y, -centerZ) *
                                  Matrix.CreateScale(scale);
            _renderer.UpdateBounds();
            Window.Title = $"Nova3D AnimatedCharacter - {Path.GetFileName(path)}";
        }
        catch (Exception exception)
        {
            _error = exception.Message;
        }
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        if (Pressed(keyboard, Keys.Escape)) Exit();
        if (_player is not null)
        {
            if (Pressed(keyboard, Keys.Tab)) SelectClip((_clipIndex + 1) % _model!.Animations.Count);
            for (var key = Keys.D1; key <= Keys.D9; key++)
                if (Pressed(keyboard, key))
                {
                    var index = (int)key - (int)Keys.D1;
                    if (index < _model!.Animations.Count) SelectClip(index);
                }
            if (Pressed(keyboard, Keys.D)) _showDebug = !_showDebug;
            _player.Update(gameTime.ElapsedGameTime);
            _renderer!.UpdateBounds();
        }
        _camera.SetViewport(GraphicsDevice.Viewport);
        _previousKeyboard = keyboard;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_renderer is not null)
        {
            _context!.BeginFrame(_camera);
            GraphicsDevice.SetRenderTarget(_shadowTarget);
            GraphicsDevice.Clear(Color.White);
            var lightView = Matrix.CreateLookAt(new Vector3(4f, 6f, 4f), Vector3.Up, Vector3.Up);
            var lightProjection = Matrix.CreateOrthographic(8f, 8f, 0.1f, 20f);
            _renderer.DrawShadows(_context, _shadowEffect!, lightView * lightProjection);
            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Clear(new Color(20, 24, 32));
            _renderer.Draw(_context);
            if (_showDebug)
            {
                GltfSkeletonDebug.Draw(_debug!, _model!, _pose!, _renderer.Transform,
                    Color.Cyan, Color.Yellow, 0.025f);
                _debug!.BoundingBox(_renderer.Bounds, Color.Lime);
                _debug.Flush(_context);
            }
        }

        _spriteBatch!.Begin();
        var message = _error is not null
            ? $"Could not load animated GLB:\n{_error}\n\nRun eng/fetch-skinning-fixtures.ps1 first."
            : $"Clip {_clipIndex + 1}/{_model!.Animations.Count}: {_player!.CurrentClip!.Name}\n" +
              "Tab or 1-9: cross-fade clip | D: skeleton/bounds | Esc: exit";
        _spriteBatch.DrawString(_font!, message, new Vector2(16f), Color.White);
        _spriteBatch.End();
        if (_smoke && ++_smokeFrames >= 5)
        {
            if (_renderer is null || _renderer.DrawCallsLastFrame == 0)
                throw new InvalidOperationException(_error ?? "Animated renderer produced no draws.");
            var boundsSize = _renderer.Bounds.Max - _renderer.Bounds.Min;
            if (!float.IsFinite(boundsSize.X) || !float.IsFinite(boundsSize.Y) ||
                !float.IsFinite(boundsSize.Z) || boundsSize.LengthSquared() <= 0f)
                throw new InvalidOperationException("Animated renderer produced invalid bounds.");
            Console.WriteLine($"AnimatedCharacter smoke PASS | clip {_player!.CurrentClip!.Name} | " +
                              $"draws {_renderer.DrawCallsLastFrame} | tris {_renderer.TrianglesLastFrame} | " +
                              $"bounds {boundsSize.X:F2},{boundsSize.Y:F2},{boundsSize.Z:F2}");
            Exit();
        }
        base.Draw(gameTime);
    }

    private void SelectClip(int index)
    {
        _clipIndex = index;
        _player!.Play(index, loop: true, blendDuration: 0.25f);
    }

    private bool Pressed(KeyboardState keyboard, Keys key) =>
        keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);

    private static string ResolveModelPath()
    {
        var commandLine = Environment.GetCommandLineArgs();
        for (var i = 1; i < commandLine.Length; i++)
            if (File.Exists(commandLine[i]) &&
                Path.GetExtension(commandLine[i]).Equals(".glb", StringComparison.OrdinalIgnoreCase))
                return Path.GetFullPath(commandLine[i]);
        var directory = Path.Combine(AppContext.BaseDirectory, "LocalAssets", "Animation");
        foreach (var name in new[] { "Fox.glb", "CesiumMan.glb", "RiggedSimple.glb" })
        {
            var candidate = Path.Combine(directory, name);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("No animated fixture was found in LocalAssets/Animation.");
    }

    private void CreateNeutralEnvironment()
    {
        _environmentMap = CreateCube(new Color(70, 78, 92));
        _irradianceMap = CreateCube(new Color(95, 102, 115));
        _prefilteredMap = CreateCube(new Color(110, 116, 128));
        _brdfLut = new Texture2D(GraphicsDevice, 1, 1, false, SurfaceFormat.Vector2);
        _brdfLut.SetData(new[] { new Vector2(0.8f, 0.05f) });
        _whiteShadow = new Texture2D(GraphicsDevice, 1, 1);
        _whiteShadow.SetData(new[] { Color.White });
    }

    private TextureCube CreateCube(Color color)
    {
        var result = new TextureCube(GraphicsDevice, 1, false, SurfaceFormat.Color);
        foreach (var face in CubeFaces) result.SetData(face, new[] { color });
        return result;
    }

    private void ConfigureUnshadowedPbr(Effect effect)
    {
        for (var i = 0; i < 4; i++)
        {
            effect.Parameters[$"ShadowMap{i}"]?.SetValue(_whiteShadow);
            effect.Parameters[$"LightViewProjection{i}"]?.SetValue(Matrix.Identity);
        }
        effect.Parameters["ShadowMapTexelSize"]?.SetValue(Vector2.One);
        effect.Parameters["CascadeSplits"]?.SetValue(new Vector4(25f, 50f, 75f, 100f));
        effect.Parameters["CascadeBlendStarts"]?.SetValue(new Vector4(24f, 49f, 74f, 99f));
    }

    protected override void UnloadContent()
    {
        _renderer?.Dispose();
        _debug?.Dispose();
        _model?.Dispose();
        _spriteBatch?.Dispose();
        _whiteShadow?.Dispose();
        _shadowTarget?.Dispose();
        _brdfLut?.Dispose();
        _prefilteredMap?.Dispose();
        _irradianceMap?.Dispose();
        _environmentMap?.Dispose();
        base.UnloadContent();
    }
}
