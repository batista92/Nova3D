using CityBuilder.Benchmarks.CityBenchmark;
using CityBuilder.Benchmarks.UiBenchmark;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nova3D.Production.Assets;
using Nova3D.Resources;
using Nova3D.Production.Configuration;
using Nova3D.Production.Logging;
using Nova3D.UI.Gum;

namespace CityBuilder;

public sealed class CityBuilderGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly FileLogger _logger;
    private readonly FileAssetManager _fileAssets;
    private readonly Nova3DConfiguration _configuration;
    private LargeWorldScene? _scene;
    private ResourceLibrary? _resources;
    private ShaderLibrary? _shaders;
    private GumUiSpike? _ui;
    private GumUiHost? _uiHost;

    public CityBuilderGame()
    {
        _logger = new FileLogger(Path.Combine(AppContext.BaseDirectory, "Logs", "citybuilder.log"));
        _fileAssets = new FileAssetManager(_logger);
        _configuration = ConfigurationLoader.LoadOrDefault(
            Path.Combine(AppContext.BaseDirectory, "Config", "nova3d.json"), _logger);
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = _configuration.Window.Width,
            PreferredBackBufferHeight = _configuration.Window.Height,
            SynchronizeWithVerticalRetrace = _configuration.Window.VSync,
            PreferMultiSampling = true,
            GraphicsProfile = GraphicsProfile.HiDef
        };

        _graphics.PreparingDeviceSettings += (_, args) =>
            args.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = 4;

        Content.RootDirectory = "Content";
        IsFixedTimeStep = false;
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = "CityBuilder - Teste 01: PBR";
        _logger.Log(LogLevel.Information, "Game", "CityBuilder initialized.");
    }

    protected override void Initialize()
    {
        base.Initialize();
        _uiHost = new GumUiHost(this, new GumUiHostOptions
        {
            ScalingMode = GumUiScalingMode.Expand,
            InputMode = GumUiInputMode.Overlay,
            Accessibility = new GumUiAccessibilitySettings
            {
                TextScale = 1f,
                MinimumHitTarget = 44f,
                MinimumContrastRatio = 4.5f,
                ReducedMotion = true
            }
        });
        _ui = new GumUiSpike(_uiHost, Exit);
        _logger.Log(LogLevel.Information, "UI", "Gum U1 spike initialized.");
    }

    protected override void LoadContent()
    {
        _resources = new ResourceLibrary(Content);
        _shaders = new ShaderLibrary(_resources);
        _shaders.Load("large-world", "Shaders/LargeWorld");
        _shaders.Load("large-terrain", "Shaders/LargeTerrain");
        _shaders.Load("water", "Shaders/Water");
        _shaders.Load("vegetation", "Shaders/Vegetation");
        _shaders.Load("skybox", "Shaders/Skybox");
        _shaders.Load("shadow-depth", "Shaders/ShadowDepth");
        _shaders.Load("instanced-shadow", "Shaders/InstancedShadow");
        _shaders.Load("post-process", "Shaders/PostProcess");
        _shaders.Load("pbr", "Shaders/PBR");
        _scene = new LargeWorldScene(
            GraphicsDevice,
            _shaders.Get("large-world"),
            _shaders.Get("large-terrain"),
            _shaders.Get("water"),
            _shaders.Get("vegetation"),
            _shaders.Get("skybox"),
            _shaders.Get("shadow-depth"),
            _shaders.Get("instanced-shadow"),
            _shaders.Get("post-process"),
            _shaders.Get("pbr"),
            _configuration,
            _logger);
        _logger.Log(LogLevel.Information, "Game", "CityBenchmark loaded.");
    }

    protected override void Update(GameTime gameTime)
    {
        // File-backed resources are polled and swapped on the graphics thread.
        _fileAssets.Update();
        _uiHost?.Update(gameTime);
        if (_uiHost?.Navigation.BackPressed == true && !(_ui?.HandleBack() ?? false))
            Exit();
        if (_ui is not null && _scene is not null)
            _ui.Update(gameTime, _scene.HudMarkerPosition, _scene.View, _scene.Projection, GraphicsDevice.Viewport);
        if (_uiHost is null || !(_uiHost.CapturesMouse || _uiHost.CapturesKeyboard || _uiHost.CapturesGamePad))
            _scene?.Update(gameTime, Window);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(18, 22, 30));
        _scene?.Draw(GraphicsDevice.Viewport.AspectRatio);
        _uiHost?.Draw();
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        _ui?.Dispose();
        _ui = null;
        _uiHost?.Dispose();
        _uiHost = null;
        _scene?.Dispose();
        _scene = null;
        _shaders?.Clear();
        _shaders = null;
        _resources?.Clear();
        _resources = null;
        _logger.Log(LogLevel.Information, "Game", "Content unloaded.");
        base.UnloadContent();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        _fileAssets.Dispose();
        _logger.Dispose();
    }
}
