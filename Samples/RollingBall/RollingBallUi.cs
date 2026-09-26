using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Nova3D.UI.Gum;

namespace Nova3D.Samples.RollingBall;

internal sealed class RollingBallUi : IDisposable
{
    private readonly GumUiHost _host;
    private readonly GumUiScreenStack _screens;
    private readonly GumUiTheme _theme = new();
    private GumValueBinding<int>? _timeBinding;
    private GumValueBinding<int>? _checkpointBinding;

    public RollingBallUi(GumUiHost host)
    {
        _host = host;
        _theme.ValidateAccessibility(host.Accessibility);
        _screens = new GumUiScreenStack(host);
    }

    public void ShowMenu(Action start, Action exit)
    {
        _timeBinding = null;
        _checkpointBinding = null;
        _screens.Clear();
        var root = CreateRoot(out StackPanel panel);
        AddLabel(panel, "RollingBall", true);
        AddLabel(panel, "WASD / arrows to roll");
        Button play = AddButton(panel, "Play", start);
        AddButton(panel, "Exit", exit);
        _screens.Push(new GumUiScreen(root, "Main menu") { InitialFocus = play });
    }

    public void ShowHud()
    {
        _screens.Clear();
        var root = new Panel();
        root.Dock(Dock.Fill);
        var panel = new StackPanel { X = 18f, Y = 18f, Width = 280f, Spacing = 6f };
        root.AddChild(panel);
        var timer = AddLabel(panel, "Time 00:00");
        var checkpoint = AddLabel(panel, "Checkpoint 0/1");
        _timeBinding = new GumValueBinding<int>(seconds =>
            timer.Text = $"Time {TimeSpan.FromSeconds(seconds):mm\\:ss}");
        _checkpointBinding = new GumValueBinding<int>(value =>
            checkpoint.Text = $"Checkpoint {value}/1");
        _screens.Push(new GumUiScreen(root, "HUD") { InputMode = GumUiInputMode.Overlay });
    }

    public void ShowPause(Action resume, Action restart, Action menu)
    {
        var root = CreateRoot(out StackPanel panel);
        AddLabel(panel, "Paused", true);
        Button resumeButton = AddButton(panel, "Resume", resume);
        AddButton(panel, "Restart", restart);
        AddButton(panel, "Main menu", menu);
        _screens.Push(new GumUiScreen(root, "Pause")
        {
            InitialFocus = resumeButton,
            CoversPrevious = false
        });
    }

    public void HidePause() => _screens.Pop();

    public void ShowEnd(bool victory, int seconds, Action restart, Action menu)
    {
        _timeBinding = null;
        _checkpointBinding = null;
        _screens.Clear();
        var root = CreateRoot(out StackPanel panel);
        AddLabel(panel, victory ? "Victory!" : "Try again", true);
        AddLabel(panel, $"Time {TimeSpan.FromSeconds(seconds):mm\\:ss}");
        Button restartButton = AddButton(panel, "Restart", restart);
        AddButton(panel, "Main menu", menu);
        _screens.Push(new GumUiScreen(root, victory ? "Victory" : "Defeat")
        {
            InitialFocus = restartButton
        });
    }

    public void Update(GameTime gameTime, int seconds, bool checkpointReached)
    {
        _screens.Update(gameTime);
        _timeBinding?.Set(seconds);
        _checkpointBinding?.Set(checkpointReached ? 1 : 0);
    }

    private Panel CreateRoot(out StackPanel panel)
    {
        var root = new Panel();
        root.Dock(Dock.Fill);
        panel = new StackPanel { X = 48f, Y = 48f, Width = 340f, Spacing = 10f };
        root.AddChild(panel);
        return root;
    }

    private Label AddLabel(StackPanel panel, string text, bool title = false)
    {
        var label = new Label { Text = text };
        _theme.Apply(label, title, _host.Accessibility);
        panel.AddChild(label);
        return label;
    }

    private Button AddButton(StackPanel panel, string text, Action action)
    {
        var button = new Button { Text = text, Width = 230f, Height = 48f };
        _theme.Apply(button, _host.Accessibility);
        button.Click += (_, _) => action();
        panel.AddChild(button);
        return button;
    }

    public void Dispose() => _screens.Dispose();
}
