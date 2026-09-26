using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nova3D.UI.Gum;

using var game = new GumMenusGame();
game.Run();

sealed class GumMenusGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private GumUiHost? _host;
    private GumUiScreenStack? _screens;
    private readonly GumUiTheme _theme = new();

    public GumMenusGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            GraphicsProfile = GraphicsProfile.HiDef
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = "Nova3D Sample - GumMenus";
    }

    protected override void Initialize()
    {
        base.Initialize();
        _host = new GumUiHost(this, new GumUiHostOptions
        {
            InputMode = GumUiInputMode.Exclusive
        });
        _theme.ValidateAccessibility(_host.Accessibility);
        _screens = new GumUiScreenStack(_host);
        ShowMainMenu();
    }

    private void ShowMainMenu()
    {
        var root = CreateRoot(out StackPanel panel);
        AddLabel(panel, "Gum Menus", title: true);
        Button options = AddButton(panel, "Options", ShowOptions);
        AddButton(panel, "Exit", Exit);
        _screens!.Push(new GumUiScreen(root, "Main menu") { InitialFocus = options });
    }

    private void ShowOptions()
    {
        var root = CreateRoot(out StackPanel panel);
        AddLabel(panel, "Options", title: true);
        AddLabel(panel, "UI scale and audio belong to game settings.");
        var volume = new Slider { Minimum = 0, Maximum = 100, Value = 80, Width = 260f };
        _theme.Apply(volume, _host!.Accessibility);
        panel.AddChild(volume);
        Button back = AddButton(panel, "Back", () => _screens!.Pop());
        _screens!.Push(new GumUiScreen(root, "Options") { InitialFocus = back });
    }

    private Panel CreateRoot(out StackPanel panel)
    {
        var root = new Panel();
        root.Dock(Dock.Fill);
        panel = new StackPanel { X = 48f, Y = 48f, Width = 360f, Spacing = 12f };
        root.AddChild(panel);
        return root;
    }

    private void AddLabel(StackPanel panel, string text, bool title = false)
    {
        var label = new Label { Text = text };
        _theme.Apply(label, title, _host!.Accessibility);
        panel.AddChild(label);
    }

    private Button AddButton(StackPanel panel, string text, Action action)
    {
        var button = new Button { Text = text, Width = 240f, Height = 48f };
        _theme.Apply(button, _host!.Accessibility);
        button.Click += (_, _) => action();
        panel.AddChild(button);
        return button;
    }

    protected override void Update(GameTime gameTime)
    {
        _host!.Update(gameTime);
        if (_host.Navigation.BackPressed)
        {
            if (_screens!.Count > 1) _screens.Pop();
            else Exit();
        }
        _screens!.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(18, 25, 36));
        _host!.Draw();
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        _screens?.Dispose();
        _host?.Dispose();
        base.UnloadContent();
    }
}
