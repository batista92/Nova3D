using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nova3D.UI.Gum;

namespace CityBuilder.Benchmarks.UiBenchmark;

/// <summary>Interactive U1-U4 validation using direct Gum controls.</summary>
internal sealed class GumUiSpike : IDisposable
{
    private static readonly GumUiTheme Theme = new()
    {
        SurfaceColor = new Color(28, 39, 54),
        ForegroundColor = new Color(238, 244, 250),
        AccentColor = new Color(64, 190, 255),
        BodyFont = new GumFontStyle(18),
        TitleFont = new GumFontStyle(25, isBold: true)
    };

    private readonly GumUiHost _host;
    private readonly GumUiScreenStack _screens;
    private readonly Label _metrics;
    private readonly GumValueBinding<int> _healthLabelBinding;
    private readonly GumValueBinding<int> _healthBarBinding;
    private readonly GumNotificationQueue _notifications;
    private readonly GumWorldMarker _worldMarker;
    private readonly Action _quit;
    private int _activationCount;
    private int _notificationCount;
    private int _metricsFrame;

    public GumUiSpike(GumUiHost host, Action quit)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _quit = quit ?? throw new ArgumentNullException(nameof(quit));
        _screens = new GumUiScreenStack(host);
        Theme.ValidateAccessibility(host.Accessibility);

        var root = CreateRoot();
        var menu = CreateStack(Anchor.TopLeft, 18f, 18f);
        root.AddChild(menu);
        menu.AddChild(CreateLabel("Nova3D UI — Gum U5", title: true));

        var status = CreateLabel("Mouse | Tab/Enter | D-pad/A");
        status.Width = 360f;
        menu.AddChild(status);

        var action = CreateButton("Activate");
        action.Click += (_, _) =>
        {
            _activationCount++;
            status.Text = $"Activated {_activationCount} time(s)";
        };
        menu.AddChild(action);

        var options = CreateButton("Options screen");
        options.Click += (_, _) => _screens.Push(CreateOptionsScreen());
        menu.AddChild(options);

        var pause = CreateButton("Pause modal");
        pause.Click += (_, _) => _screens.Push(CreatePauseScreen());
        menu.AddChild(pause);

        var slider = new Slider
        {
            Width = 220f,
            Minimum = 0f,
            Maximum = 100f,
            Value = 50f,
            SmallChange = 5f,
            LargeChange = 10f
        };
        Theme.Apply(slider, _host.Accessibility);
        slider.ValueChanged += (_, _) => status.Text = $"Slider: {slider.Value:F0}";
        menu.AddChild(slider);

        var notificationStack = CreateStack(Anchor.BottomRight, -18f, -18f);
        notificationStack.Width = 300f;
        root.AddChild(notificationStack);
        _notifications = new GumNotificationQueue(notificationStack, 4,
            _ =>
            {
                Label label = CreateLabel(string.Empty);
                label.Width = 290f;
                return label;
            });

        var notify = CreateButton("Notification");
        notify.Click += (_, _) =>
            _notifications.Enqueue($"Notification {++_notificationCount}", TimeSpan.FromSeconds(3));
        menu.AddChild(notify);

        var hud = CreateStack(Anchor.BottomLeft, 18f, -18f);
        root.AddChild(hud);
        var healthLabel = CreateLabel("Health 100");
        hud.AddChild(healthLabel);
        var healthBar = new Slider
        {
            Width = 220f,
            Minimum = 0f,
            Maximum = 100f,
            Value = 100f,
            IsEnabled = false
        };
        Theme.Apply(healthBar, _host.Accessibility);
        hud.AddChild(healthBar);
        _healthLabelBinding = new GumValueBinding<int>(value => healthLabel.Text = $"Health {value}");
        _healthBarBinding = new GumValueBinding<int>(value => healthBar.Value = value);

        var marker = CreateLabel("◆ world origin");
        marker.Width = 150f;
        marker.Visual.Visible = false;
        root.AddChild(marker);
        _worldMarker = new GumWorldMarker(host, marker)
        {
            Offset = new Vector2(8f, -24f)
        };

        _metrics = CreateLabel("UI measuring...");
        _metrics.Width = 270f;
        _metrics.Anchor(Anchor.TopRight);
        _metrics.X = -18f;
        _metrics.Y = 18f;
        root.AddChild(_metrics);

        _screens.Push(new GumUiScreen(root, "HUD")
        {
            InitialFocus = action,
            InputMode = GumUiInputMode.Overlay
        });
    }

    public bool HandleBack()
    {
        if (_screens.Count > 1) return _screens.Pop();
        _screens.Push(CreatePauseScreen());
        return true;
    }

    public void Update(GameTime gameTime, Vector3 markerPosition, Matrix view, Matrix projection, Viewport viewport)
    {
        _screens.Update(gameTime);
        _notifications.Update(gameTime);
        int health = (int)MathF.Round(75f + MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds) * 25f);
        _healthLabelBinding.Set(health);
        _healthBarBinding.Set(health);
        _worldMarker.Update(markerPosition, view, projection, viewport);

        if (++_metricsFrame < 30) return;
        _metricsFrame = 0;
        GumUiFrameStatistics statistics = _host.Statistics;
        _metrics.Text = $"Gum CPU update {statistics.UpdateMilliseconds:F3} ms\n" +
                        $"Gum CPU draw {statistics.DrawMilliseconds:F3} ms\n" +
                        $"Managed {statistics.ManagedBytes} B/frame\n" +
                        $"Screen {_screens.Current?.Name} ({_screens.Count})\n" +
                        $"Input {_host.InputMode}\n" +
                        $"Notices {_notifications.ActiveCount}/{_notifications.Capacity}";
    }

    public void Dispose() => _screens.Dispose();

    private GumUiScreen CreateOptionsScreen()
    {
        var root = CreateRoot();
        var menu = CreateStack(Anchor.Center, 0f, 0f);
        root.AddChild(menu);
        menu.AddChild(CreateLabel("OPTIONS", title: true));
        menu.AddChild(CreateLabel("A normal screen hides the HUD."));
        var scale = new Slider { Width = 240f, Minimum = 50f, Maximum = 150f, Value = 100f };
        Theme.Apply(scale, _host.Accessibility);
        menu.AddChild(scale);
        var back = CreateButton("Back");
        back.Click += (_, _) => _screens.Pop();
        menu.AddChild(back);
        return new GumUiScreen(root, "Options")
        {
            InitialFocus = back,
            InputMode = GumUiInputMode.Exclusive
        };
    }

    private GumUiScreen CreatePauseScreen()
    {
        var root = CreateRoot();
        var menu = CreateStack(Anchor.Center, 0f, 0f);
        root.AddChild(menu);
        menu.AddChild(CreateLabel("PAUSED", title: true));
        menu.AddChild(CreateLabel("The HUD remains visible behind this modal."));
        var resume = CreateButton("Resume");
        resume.Click += (_, _) => _screens.Pop();
        menu.AddChild(resume);
        var exit = CreateButton("Exit");
        exit.Click += (_, _) => _quit();
        menu.AddChild(exit);
        return new GumUiScreen(root, "Pause modal")
        {
            InitialFocus = resume,
            InputMode = GumUiInputMode.Exclusive,
            CoversPrevious = false
        };
    }

    private static Panel CreateRoot()
    {
        var root = new Panel();
        root.Dock(Dock.Fill);
        return root;
    }

    private static StackPanel CreateStack(Anchor anchor, float x, float y)
    {
        var stack = new StackPanel { X = x, Y = y, Width = 330f, Spacing = 6f };
        stack.Anchor(anchor);
        return stack;
    }

    private Label CreateLabel(string text, bool title = false)
    {
        var label = new Label { Text = text };
        Theme.Apply(label, title, _host.Accessibility);
        return label;
    }

    private Button CreateButton(string text)
    {
        var button = new Button { Text = text, Width = 190f, Height = 42f };
        Theme.Apply(button, _host.Accessibility);
        return button;
    }
}
