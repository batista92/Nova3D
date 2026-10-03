using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Nova3D.Production.Input;
using Nova3D.Production.Persistence;

using var game = new InputActionsGame();
game.Run();

sealed class InputActionsGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly InputContextRouter _input = new();
    private readonly string _settingsPath;
    private VersionedJsonStore<JsonElement>? _bindingStore;
    private InputContext? _menuContext;
    private InputAction? _move;
    private InputAction? _pulse;
    private InputAction? _menuConfirm;
    private InputAction? _toggleMenu;
    private InputAction? _remap;
    private InputAction? _restore;
    private SpriteBatch? _batch;
    private SpriteFont? _font;
    private Texture2D? _pixel;
    private Vector2 _player = new(400f, 360f);
    private float _pulseTime;
    private int _activationCount;
    private int _menuConfirmCount;
    private bool _menuOpen;
    private string _bindingLabel = "Space / gamepad A";
    private string _message = "Default bindings active";

    public InputActionsGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 960,
            PreferredBackBufferHeight = 640,
            GraphicsProfile = GraphicsProfile.HiDef
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = "Nova3D Sample - InputActions";
        _settingsPath = new GameDataPaths("Nova3D", "InputActionsSample").BindingsFile;
    }

    protected override void Initialize()
    {
        var system = _input.CreateContext("system", 100, active: true);
        _menuContext = _input.CreateContext("menu", 50,
            blocksLowerContexts: true);
        var gameplay = _input.CreateContext("gameplay", 0, active: true);

        _toggleMenu = system.Actions.Add("toggle-menu", InputActionKind.Digital,
            InputBinding.Key(Keys.Escape),
            InputBinding.GamePadButtonInput(Buttons.Start));
        _remap = system.Actions.Add("remap", InputActionKind.Digital,
            InputBinding.Key(Keys.F2),
            InputBinding.GamePadButtonInput(Buttons.Y));
        _restore = system.Actions.Add("restore-defaults", InputActionKind.Digital,
            InputBinding.Key(Keys.F3),
            InputBinding.GamePadButtonInput(Buttons.X));
        _menuConfirm = _menuContext.Actions.Add("confirm", InputActionKind.Digital,
            InputBinding.Key(Keys.Enter),
            InputBinding.GamePadButtonInput(Buttons.A));
        _move = gameplay.Actions.Add("move", InputActionKind.Axis2D,
            InputBinding.KeyboardVector(Keys.A, Keys.D, Keys.S, Keys.W),
            InputBinding.KeyboardVector(Keys.Left, Keys.Right, Keys.Down, Keys.Up),
            InputBinding.GamePadStickInput(InputGamePadStick.Left, deadzone: 0.2f));
        _pulse = gameplay.Actions.Add("pulse", InputActionKind.Digital,
            InputBinding.Key(Keys.Space),
            InputBinding.GamePadButtonInput(Buttons.A));
        gameplay.Actions.CaptureDefaults();
        _bindingStore = new VersionedJsonStore<JsonElement>(
            _settingsPath, "sample.input-bindings", 1,
            () => ParseBindings(gameplay.Actions),
            validate: profile => InputBindingJson.Apply(gameplay.Actions, profile.GetRawText()));
        LoadBindings(gameplay.Actions);
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        _font = Content.Load<SpriteFont>("DebugFont");
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();
        var gamePad = GamePad.GetState(PlayerIndex.One);
        _input.Update(keyboard, mouse, gamePad);

        if (_toggleMenu!.Pressed)
        {
            _menuOpen = !_menuOpen;
            _menuContext!.SetActive(_menuOpen);
            _message = _menuOpen
                ? "Menu context active: gameplay is blocked"
                : "Gameplay context resumed after neutral input";
        }
        if (_remap!.Pressed) ApplyAlternateBindings();
        if (_restore!.Pressed) RestoreDefaultBindings();

        if (_menuOpen)
        {
            if (_menuConfirm!.Pressed)
            {
                _menuConfirmCount++;
                _message = "Menu confirm received; gameplay pulse stayed blocked";
            }
        }
        else
        {
            var direction = new Vector2(_move!.Value.X, -_move.Value.Y);
            if (direction.LengthSquared() > 1f) direction.Normalize();
            _player += direction * (260f * (float)gameTime.ElapsedGameTime.TotalSeconds);
            var viewport = GraphicsDevice.Viewport;
            _player.X = MathHelper.Clamp(_player.X, 35f, viewport.Width - 35f);
            _player.Y = MathHelper.Clamp(_player.Y, 185f, viewport.Height - 35f);
            if (_pulse!.Pressed)
            {
                _pulseTime = 0.25f;
                _activationCount++;
                _message = "Gameplay action received";
            }
        }
        _pulseTime = MathF.Max(0f,
            _pulseTime - (float)gameTime.ElapsedGameTime.TotalSeconds);
        base.Update(gameTime);
    }

    private void ApplyAlternateBindings()
    {
        var gameplay = _input.GetRequired("gameplay").Actions;
        gameplay.ReplaceBindings("pulse",
            InputBinding.Key(Keys.J),
            InputBinding.GamePadButtonInput(Buttons.B));
        _bindingLabel = "J / gamepad B";
        SaveBindings(gameplay);
        _message = "Alternate pulse binding saved";
    }

    private void RestoreDefaultBindings()
    {
        var gameplay = _input.GetRequired("gameplay").Actions;
        gameplay.RestoreDefaults();
        _bindingLabel = "Space / gamepad A";
        SaveBindings(gameplay);
        _message = "Default bindings restored and saved";
    }

    private void LoadBindings(InputActionMap gameplay)
    {
        var result = _bindingStore!.Load();
        string json = result.Value.GetRawText();
        _bindingLabel = json.Contains("\"key\":\"J\"", StringComparison.Ordinal) ||
            json.Contains("\"key\": \"J\"", StringComparison.Ordinal)
            ? "J / gamepad B" : "Space / gamepad A";
        _message = result.Source switch
        {
            PersistenceLoadSource.Primary => "Saved bindings loaded",
            PersistenceLoadSource.Backup => "Bindings recovered from backup",
            PersistenceLoadSource.DefaultInvalid => "Invalid settings ignored; defaults restored",
            _ => "Default bindings active"
        };
    }

    private void SaveBindings(InputActionMap gameplay)
    {
        try
        {
            _bindingStore!.Save(ParseBindings(gameplay));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _message = "Bindings changed, but the settings file could not be written";
        }
    }

    private static JsonElement ParseBindings(InputActionMap map)
    {
        using JsonDocument document = JsonDocument.Parse(InputBindingJson.Serialize(map));
        return document.RootElement.Clone();
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(19, 25, 36));
        _batch!.Begin(samplerState: SamplerState.PointClamp);
        DrawRectangle(new Rectangle(20, 165, GraphicsDevice.Viewport.Width - 40,
            GraphicsDevice.Viewport.Height - 185), new Color(35, 48, 61));
        int size = _pulseTime > 0f ? 68 : 54;
        DrawRectangle(new Rectangle((int)_player.X - size / 2,
            (int)_player.Y - size / 2, size, size),
            _pulseTime > 0f ? new Color(255, 190, 50) : new Color(55, 170, 255));

        DrawText("Nova3D Input Actions - G4.4", new Vector2(20, 16), Color.White);
        DrawText("Move: WASD / arrows / left stick", new Vector2(20, 48), Color.LightGray);
        DrawText($"Pulse: {_bindingLabel} | activations: {_activationCount}",
            new Vector2(20, 74), Color.LightGray);
        DrawText("Menu: Esc / Start | Remap: F2 / Y | Defaults: F3 / X",
            new Vector2(20, 100), Color.LightGray);
        DrawText(_message, new Vector2(20, 130), new Color(120, 225, 155));

        if (_menuOpen)
        {
            var viewport = GraphicsDevice.Viewport;
            DrawRectangle(new Rectangle(0, 0, viewport.Width, viewport.Height),
                new Color(0, 0, 0, 170));
            DrawRectangle(new Rectangle(viewport.Width / 2 - 245,
                viewport.Height / 2 - 90, 490, 180), new Color(27, 39, 54));
            DrawText("MENU CONTEXT", new Vector2(viewport.Width / 2 - 95,
                viewport.Height / 2 - 58), Color.White);
            DrawText("Enter / A confirms only this menu",
                new Vector2(viewport.Width / 2 - 180, viewport.Height / 2 - 16), Color.LightGray);
            DrawText($"Confirmations: {_menuConfirmCount}   Esc / Start closes",
                new Vector2(viewport.Width / 2 - 205, viewport.Height / 2 + 22), Color.LightGray);
        }
        _batch.End();
        base.Draw(gameTime);
    }

    private void DrawRectangle(Rectangle rectangle, Color color) =>
        _batch!.Draw(_pixel!, rectangle, color);

    private void DrawText(string text, Vector2 position, Color color) =>
        _batch!.DrawString(_font!, text, position, color);

    protected override void UnloadContent()
    {
        _pixel?.Dispose();
        _batch?.Dispose();
        base.UnloadContent();
    }
}
