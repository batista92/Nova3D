using Nova3D.Benchmarks.CityBenchmark;
using Nova3D.Benchmarks.UiBenchmark;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nova3D.Production.Assets;
using Nova3D.Resources;
using Nova3D.Production.Configuration;
using Nova3D.Production.Logging;
using Nova3D.Production.VisualTesting;
using Nova3D.UI.Gum;
using Nova3D.Benchmarks.Validation.Pbr;
using Nova3D.Benchmarks.Validation.VisualCoverage;
using Nova3D.Benchmarks.Validation.Performance;
using System.Diagnostics;

namespace Nova3D.Benchmarks;

public enum VisualBenchmarkScene
{
    City,
    Pbr,
    Gltf
}

public sealed class Nova3DBenchmarkGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly FileLogger _logger;
    private readonly FileAssetManager _fileAssets;
    private readonly Nova3DConfiguration _configuration;
    private LargeWorldScene? _scene;
    private PbrScene? _pbrScene;
    private GltfVisualScene? _gltfScene;
    private ResourceLibrary? _resources;
    private ShaderLibrary? _shaders;
    private GumUiSpike? _ui;
    private GumUiHost? _uiHost;
    private readonly string? _captureDirectory;
    private readonly VisualCapturePlan? _capturePlan;
    private VisualCaptureSession? _capture;
    private readonly VisualBenchmarkScene _captureScene;
    private PerformanceReportSession? _performance;
    private readonly string? _performanceReportPath;
    private double _lastUpdateCpuMilliseconds;
    private long _lastDrawStartTimestamp;
    private TimeSpan _benchmarkTotalTime;

    public Nova3DBenchmarkGame(
        string? captureDirectory = null,
        VisualBenchmarkScene captureScene = VisualBenchmarkScene.City,
        string? performanceReportPath = null)
    {
        _captureDirectory = captureDirectory;
        _captureScene = captureScene;
        _performanceReportPath = performanceReportPath;
        _capturePlan = captureDirectory is null
            ? null
            : new VisualCapturePlan(
                captureScene switch
                {
                    VisualBenchmarkScene.City => "city-benchmark",
                    VisualBenchmarkScene.Pbr => "pbr-material-csm",
                    VisualBenchmarkScene.Gltf => "gltf-static-animated",
                    _ => throw new ArgumentOutOfRangeException(nameof(captureScene))
                },
                1280,
                720,
                seed: 9127,
                fixedTimeStep: TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60),
                warmupFrames: captureScene == VisualBenchmarkScene.City ? 120 : 60);
        bool automatedBenchmark = _capturePlan is not null || performanceReportPath is not null;
        _logger = new FileLogger(Path.Combine(AppContext.BaseDirectory, "Logs", "nova3d-benchmarks.log"));
        _fileAssets = new FileAssetManager(_logger);
        _configuration = ConfigurationLoader.LoadOrDefault(
            Path.Combine(AppContext.BaseDirectory, "Config", "nova3d.json"), _logger);
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = automatedBenchmark ? 1280 : _configuration.Window.Width,
            PreferredBackBufferHeight = automatedBenchmark ? 720 : _configuration.Window.Height,
            SynchronizeWithVerticalRetrace = !automatedBenchmark && _configuration.Window.VSync,
            PreferMultiSampling = !automatedBenchmark,
            GraphicsProfile = GraphicsProfile.HiDef
        };

        _graphics.PreparingDeviceSettings += (_, args) =>
            args.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount =
                automatedBenchmark ? 0 : 4;

        Content.RootDirectory = "Content";
        IsFixedTimeStep = _capturePlan is not null;
        if (automatedBenchmark)
            InactiveSleepTime = TimeSpan.Zero;
        if (_capturePlan is not null)
            TargetElapsedTime = _capturePlan.FixedTimeStep;
        IsMouseVisible = !automatedBenchmark;
        Window.AllowUserResizing = !automatedBenchmark;
        Window.Title = "Nova3D - CityBenchmark";
        _logger.Log(LogLevel.Information, "Game", "Nova3D benchmark host initialized.");
    }

    protected override void Initialize()
    {
        base.Initialize();
        if (_capturePlan is not null)
        {
            _capture = new VisualCaptureSession(
                GraphicsDevice,
                _capturePlan,
                _captureDirectory!,
                backend: "DesktopGL");
            _logger.Log(LogLevel.Information, "Capture",
                $"Deterministic capture armed for frame {_capturePlan.CaptureFrame}.");
            return;
        }

        if (_performanceReportPath is not null)
        {
            _performance = new PerformanceReportSession(GraphicsDevice, _performanceReportPath);
            _logger.Log(LogLevel.Information, "Performance", "Performance report session armed.");
            return;
        }

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
        switch (_captureScene)
        {
            case VisualBenchmarkScene.City:
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
                break;
            case VisualBenchmarkScene.Pbr:
                _pbrScene = new PbrScene(
                    GraphicsDevice,
                    _shaders.Get("pbr"),
                    _shaders.Get("skybox"),
                    _shaders.Get("shadow-depth"));
                break;
            case VisualBenchmarkScene.Gltf:
                _gltfScene = new GltfVisualScene(
                    GraphicsDevice,
                    _shaders.Get("pbr"),
                    _shaders.Get("shadow-depth"));
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        _logger.Log(LogLevel.Information, "Game", $"{_captureScene} benchmark scene loaded.");
    }

    protected override void Update(GameTime gameTime)
    {
        // File-backed resources are polled and swapped on the graphics thread.
        _fileAssets.Update();
        if (_capture is not null)
        {
            _scene?.Update(gameTime, Window, acceptInput: false);
            _pbrScene?.Update(gameTime, Window, acceptInput: false);
            _gltfScene?.Update(gameTime);
            base.Update(gameTime);
            return;
        }

        if (_performance is not null)
        {
            TimeSpan step = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);
            _benchmarkTotalTime += step;
            long updateStart = Stopwatch.GetTimestamp();
            _scene?.Update(new GameTime(_benchmarkTotalTime, step), Window, acceptInput: false);
            _lastUpdateCpuMilliseconds = Stopwatch.GetElapsedTime(updateStart).TotalMilliseconds;
            base.Update(gameTime);
            return;
        }

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
        long drawStart = Stopwatch.GetTimestamp();
        double frameIntervalMilliseconds = _lastDrawStartTimestamp == 0
            ? 0
            : Stopwatch.GetElapsedTime(_lastDrawStartTimestamp, drawStart).TotalMilliseconds;
        _lastDrawStartTimestamp = drawStart;
        GraphicsDevice.Clear(new Color(18, 22, 30));
        _scene?.Draw(GraphicsDevice.Viewport.AspectRatio);
        _pbrScene?.Draw(GraphicsDevice.Viewport.AspectRatio);
        _gltfScene?.Draw();
        double drawCpuMilliseconds = Stopwatch.GetElapsedTime(drawStart).TotalMilliseconds;
        _uiHost?.Draw();
        VisualCaptureResult? captureResult = _capture?.CompleteFrame();
        if (captureResult is not null)
        {
            string message = $"VISUAL CAPTURE PASS | frame {captureResult.Metadata.CaptureFrame} | " +
                $"{captureResult.Metadata.Width}x{captureResult.Metadata.Height} | {captureResult.ImagePath}";
            Console.WriteLine(message);
            _logger.Log(LogLevel.Information, "Capture", message);
            Exit();
        }
        if (_performance is not null && _scene is not null)
        {
            PerformanceReport? report = _performance.Record(
                frameIntervalMilliseconds,
                _lastUpdateCpuMilliseconds,
                drawCpuMilliseconds,
                _scene.GetPerformanceSnapshot());
            if (report is not null)
            {
                string message = $"PERFORMANCE CAPTURE PASS | frames {report.SampleFrames} | " +
                    $"p95 {report.Metrics.FrameIntervalMilliseconds.P95:F3} ms | " +
                    $"draws {report.Metrics.DrawCalls.Maximum} | tris {report.Metrics.Triangles.Maximum} | " +
                    _performanceReportPath;
                Console.WriteLine(message);
                _logger.Log(LogLevel.Information, "Performance", message);
                Exit();
            }
        }
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
        _pbrScene?.Dispose();
        _pbrScene = null;
        _gltfScene?.Dispose();
        _gltfScene = null;
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
