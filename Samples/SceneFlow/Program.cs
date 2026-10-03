using System.Text.Json;
using BepuPhysics;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Nova3D.Physics.Bepu;
using Nova3D.Production.Scenes;
using Nova3D.Rendering;
using Nova3D.Samples;
using Nova3D.UI.Gum;

if (args.Contains("--smoke", StringComparer.Ordinal))
{
    SceneFlowSmoke.Run();
}
else
{
    using var game = new SceneFlowGame();
    game.Run();
}

sealed class SceneFlowGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly GumUiTheme _theme = new();
    private GumUiHost? _host;
    private GumUiScreenStack? _screens;
    private SceneService? _scenes;
    private SceneFlowController? _flow;
    private BepuPhysicsWorld? _physics;
    private BepuSceneFlowAdapter? _physicsAdapter;
    private SceneComponentRegistry? _components;
    private RenderContext? _context;
    private Camera3D? _camera;
    private Mesh? _cube;
    private BasicEffect? _effect;
    private Label? _status;
    private Action? _pendingAction;
    private KeyboardState _previousKeyboard;
    private float _statusInterval;

    public SceneFlowGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            GraphicsProfile = GraphicsProfile.HiDef,
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720
        };
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = "Nova3D Sample - SceneFlow";
    }

    protected override void LoadContent()
    {
        _host = new GumUiHost(this, new GumUiHostOptions { InputMode = GumUiInputMode.Exclusive });
        _theme.ValidateAccessibility(_host.Accessibility);
        _screens = new GumUiScreenStack(_host);
        _physics = new BepuPhysicsWorld(new PhysicsWorldOptions { WorkerCount = 1 });
        _components = new SceneComponentRegistry();
        _components.Register(new SampleRigidBoxDescriptor(_physics));
        _scenes = new SceneService(new SceneInstantiator());
        _flow = new SceneFlowController(_scenes);
        _flow.StateChanged += (_, next) => ShowScreen(next); // UI observes; flow owns the phase.
        _physicsAdapter = new BepuSceneFlowAdapter(_flow, _physics);
        _camera = new Camera3D { Position = new Vector3(19f, 16f, 24f) };
        _camera.Direction = Vector3.Normalize(new Vector3(0f, 3f, 0f) - _camera.Position);
        _context = new RenderContext(GraphicsDevice);
        _cube = SampleMeshFactory.CreateCube(GraphicsDevice, Color.White);
        _effect = new BasicEffect(GraphicsDevice) { VertexColorEnabled = true, LightingEnabled = false };
        _flow.ReturnToMenu();
    }

    private void StartLevel(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Scenes", name + ".scene.json");
        _flow!.Start(token =>
        {
            token.ThrowIfCancellationRequested();
            return SceneLoader.Prepare(path, _components!);
        });
    }

    private void ShowScreen(SceneFlowState state)
    {
        _screens!.Clear();
        var root = new Panel();
        root.Dock(Dock.Fill);
        var panel = new StackPanel { X = 28f, Y = 26f, Width = 325f, Spacing = 10f };
        root.AddChild(panel);

        AddLabel(panel, "SCENE FLOW  /  G3", title: true);
        AddLabel(panel, $"Phase: {state}");
        _status = AddLabel(panel, "Physics bodies: 0 | statics: 0");
        AddLabel(panel, "F1/F2 levels  |  P pause  |  R restart");
        AddLabel(panel, "Space result  |  M menu  |  Esc back");

        Button? focus = null;
        switch (state)
        {
            case SceneFlowState.Menu:
                focus = AddButton(panel, "Start Level A", () => StartLevel("level-a"));
                AddButton(panel, "Start Level B", () => StartLevel("level-b"));
                AddButton(panel, "Exit", Exit);
                break;
            case SceneFlowState.Playing:
                focus = AddButton(panel, "Pause", () => _flow!.Pause());
                AddButton(panel, "Show Result", () => _flow!.ShowResult());
                AddButton(panel, "Restart", () => _flow!.Restart());
                AddButton(panel, "Level A", () => StartLevel("level-a"));
                AddButton(panel, "Level B", () => StartLevel("level-b"));
                AddButton(panel, "Return to Menu", () => _flow!.ReturnToMenu());
                break;
            case SceneFlowState.Paused:
                focus = AddButton(panel, "Resume", () => _flow!.Resume());
                AddButton(panel, "Restart", () => _flow!.Restart());
                AddButton(panel, "Return to Menu", () => _flow!.ReturnToMenu());
                break;
            case SceneFlowState.Result:
                focus = AddButton(panel, "Restart", () => _flow!.Restart());
                AddButton(panel, "Return to Menu", () => _flow!.ReturnToMenu());
                break;
        }

        _screens.Push(new GumUiScreen(root, state.ToString())
        {
            InitialFocus = focus,
            InputMode = GumUiInputMode.Exclusive
        });
        UpdateStatus();
    }

    private Label AddLabel(StackPanel panel, string text, bool title = false)
    {
        var label = new Label { Text = text };
        _theme.Apply(label, title, _host!.Accessibility);
        panel.AddChild(label);
        return label;
    }

    private Button AddButton(StackPanel panel, string text, Action action)
    {
        var button = new Button { Text = text, Width = 250f, Height = 44f };
        _theme.Apply(button, _host!.Accessibility);
        button.Click += (_, _) => _pendingAction = action;
        panel.AddChild(button);
        return button;
    }

    protected override void Update(GameTime gameTime)
    {
        _host!.Update(gameTime);
        _screens!.Update(gameTime);
        var keyboard = Keyboard.GetState();
        bool Pressed(Keys key) => keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
        if (_pendingAction is null)
        {
            if (Pressed(Keys.F1)) _pendingAction = () => StartLevel("level-a");
            else if (Pressed(Keys.F2)) _pendingAction = () => StartLevel("level-b");
            else if (Pressed(Keys.M)) _pendingAction = () => _flow!.ReturnToMenu();
            else if (Pressed(Keys.R) && _flow!.ActiveScene is not null) _pendingAction = () => _flow!.Restart();
            else if (Pressed(Keys.Space) && _flow!.State == SceneFlowState.Playing)
                _pendingAction = () => _flow!.ShowResult();
            else if (Pressed(Keys.P) && _flow!.State == SceneFlowState.Playing)
                _pendingAction = () => _flow!.Pause();
            else if (Pressed(Keys.P) && _flow!.State == SceneFlowState.Paused)
                _pendingAction = () => _flow!.Resume();
            else if (_host.Navigation.BackPressed)
                _pendingAction = _flow!.State switch
                {
                    SceneFlowState.Playing => () => _flow.Pause(),
                    SceneFlowState.Paused => () => _flow.Resume(),
                    SceneFlowState.Result => () => _flow.ReturnToMenu(),
                    _ => Exit
                };
        }
        _previousKeyboard = keyboard;

        var action = _pendingAction;
        _pendingAction = null;
        action?.Invoke();
        _physicsAdapter!.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
        _camera!.SetViewport(GraphicsDevice.Viewport);
        _statusInterval += (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_statusInterval >= 0.2f)
        {
            _statusInterval = 0f;
            UpdateStatus();
        }
        base.Update(gameTime);
    }

    private void UpdateStatus()
    {
        if (_status is null || _physics is null || _flow is null)
            return;
        var dynamicBox = _flow.ActiveScene?.GetComponents<SampleBox>()
            .FirstOrDefault(box => box.Body.HasValue);
        var height = dynamicBox?.Body?.Position.Y;
        var text = $"Bodies: {_physics.BodyCount} | Statics: {_physics.StaticCount}" +
                   (height.HasValue ? $" | first Y: {height.Value:F2}" : "");
        if (_status.Text != text)
            _status.Text = text;
        Window.Title = $"Nova3D SceneFlow | {_flow.State} | {text}";
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(15, 21, 31));
        var camera = _camera!;
        _context!.BeginFrame(camera);
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.BlendState = BlendState.Opaque;
        GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        _effect!.View = camera.View;
        _effect.Projection = camera.Projection;
        foreach (var box in _flow!.ActiveScene?.GetComponents<SampleBox>() ?? [])
        {
            _effect.World = box.World;
            _effect.DiffuseColor = box.Color.ToVector3();
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _cube!.Draw(GraphicsDevice);
                _context.Statistics.RecordDraw(_cube.PrimitiveCount);
            }
        }
        _host!.Draw();
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        _screens?.Dispose();
        _host?.Dispose();
        _scenes?.Dispose();
        _physics?.Dispose();
        _effect?.Dispose();
        _cube?.Dispose();
        base.UnloadContent();
    }
}

sealed class SampleBox
{
    public required Vector3 Size { get; init; }
    public required Color Color { get; init; }
    public required Vector3 StaticPosition { get; init; }
    public BepuBody? Body { get; init; }
    public StaticHandle? Static { get; init; }
    public Matrix World => Matrix.CreateScale(Size * 0.5f) *
        (Body?.WorldMatrix ?? Matrix.CreateTranslation(StaticPosition));
}

sealed class SampleRigidBoxDescriptor(BepuPhysicsWorld physics) : ISceneRuntimeComponentDescriptor
{
    public string Type => "sample.rigid-box";

    public void Validate(SceneComponentValidationContext context)
    {
        var properties = context.Component.Properties;
        if (!properties.TryGetProperty("dynamic", out var dynamic) ||
            dynamic.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            context.Report("SMP001", "dynamic must be a boolean.", "dynamic");
        if (!properties.TryGetProperty("size", out var size) || !TryReadSize(size, out _))
            context.Report("SMP002", "size must be three positive finite numbers.", "size");
        if (!properties.TryGetProperty("color", out var color) ||
            color.ValueKind != JsonValueKind.String || !TryReadColor(color.GetString(), out _))
            context.Report("SMP003", "color must be blue, cyan, orange, gold or slate.", "color");
        foreach (var property in properties.EnumerateObject())
        {
            if (property.Name is not ("dynamic" or "size" or "color"))
                context.Report("SMP004", "Unknown rigid-box property.", property.Name);
        }
    }

    public object Create(SceneComponentInstantiationContext context)
    {
        var properties = context.Component.Properties;
        TryReadSize(properties.GetProperty("size"), out var size);
        TryReadColor(properties.GetProperty("color").GetString(), out var color);
        var position = context.WorldTransform.Translation;
        if (properties.GetProperty("dynamic").GetBoolean())
            return new SampleBox { Size = size, Color = color, StaticPosition = position,
                Body = physics.CreateDynamicBox(position, size) };
        return new SampleBox { Size = size, Color = color, StaticPosition = position,
            Static = physics.CreateStaticBox(position, size) };
    }

    public void Destroy(object instance)
    {
        var box = (SampleBox)instance;
        if (box.Body is { } body) physics.Remove(body);
        if (box.Static is { } handle) physics.Remove(handle);
    }

    private static bool TryReadSize(JsonElement value, out Vector3 size)
    {
        size = default;
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != 3)
            return false;
        var values = value.EnumerateArray().ToArray();
        if (values.Any(item => item.ValueKind != JsonValueKind.Number || !item.TryGetSingle(out _)))
            return false;
        size = new Vector3(values[0].GetSingle(), values[1].GetSingle(), values[2].GetSingle());
        return float.IsFinite(size.X) && float.IsFinite(size.Y) && float.IsFinite(size.Z) &&
               size.X > 0f && size.Y > 0f && size.Z > 0f;
    }

    private static bool TryReadColor(string? name, out Color color)
    {
        color = name switch
        {
            "blue" => new Color(62, 115, 230),
            "cyan" => new Color(56, 190, 215),
            "orange" => new Color(242, 122, 48),
            "gold" => new Color(242, 193, 72),
            "slate" => new Color(65, 82, 101),
            _ => default
        };
        return name is "blue" or "cyan" or "orange" or "gold" or "slate";
    }
}

static class SceneFlowSmoke
{
    public static void Run()
    {
        using var physics = new BepuPhysicsWorld(new PhysicsWorldOptions { WorkerCount = 1 });
        using var scenes = new SceneService(new SceneInstantiator());
        var flow = new SceneFlowController(scenes);
        var stepper = new BepuSceneFlowAdapter(flow, physics);
        var registry = new SceneComponentRegistry();
        registry.Register(new SampleRigidBoxDescriptor(physics));
        SceneLoadPlan Plan(string name) => SceneLoader.Prepare(
            Path.Combine(AppContext.BaseDirectory, "Assets", "Scenes", name + ".scene.json"), registry);

        var first = Plan("level-a");
        var second = Plan("level-b");
        flow.ReturnToMenu();
        for (var cycle = 0; cycle < 4; cycle++)
        {
            flow.Start(cycle % 2 == 0 ? first : second);
            int expectedBodies = cycle % 2 == 0 ? 3 : 2;
            int expectedStatics = cycle % 2 == 0 ? 1 : 3;
            if (physics.BodyCount != expectedBodies || physics.StaticCount != expectedStatics ||
                stepper.Update(1f / 30f) == 0)
                throw new InvalidOperationException("Sample scene did not create or step its expected bodies.");
            flow.Pause();
            if (stepper.Update(1f) != 0)
                throw new InvalidOperationException("Sample physics advanced while paused.");
            flow.Resume();
            flow.Restart();
            if (physics.BodyCount != expectedBodies || physics.StaticCount != expectedStatics)
                throw new InvalidOperationException("Sample restart retained stale physics handles.");
            flow.ReturnToMenu();
            if (physics.BodyCount != 0 || physics.StaticCount != 0 || stepper.CanStep)
                throw new InvalidOperationException("Sample menu retained physics resources.");
        }
        Console.WriteLine("SceneFlow smoke PASS | two JSON levels | pause | restart | cleanup");
    }
}
