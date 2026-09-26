#if (ui)
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Nova3D.UI.Gum;

namespace Nova3DGame.Game;

internal sealed class UiOverlay : IDisposable
{
    private readonly GumUiScreenStack _screens;

    public UiOverlay(GumUiHost host, Action exit)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(exit);

        var theme = new GumUiTheme();
        theme.ValidateAccessibility(host.Accessibility);

        var root = new Panel();
        root.Dock(Dock.Fill);

        var hud = new StackPanel { X = 18f, Y = 18f, Width = 300f, Spacing = 8f };
        hud.Anchor(Anchor.TopLeft);
        root.AddChild(hud);

        var title = new Label { Text = "Nova3D + Gum" };
        theme.Apply(title, title: true, host.Accessibility);
        hud.AddChild(title);

        var help = new Label { Text = "Tab/D-pad • Enter/A • Esc/B" };
        theme.Apply(help, accessibility: host.Accessibility);
        hud.AddChild(help);

        var exitButton = new Button { Text = "Exit", Width = 180f, Height = 44f };
        theme.Apply(exitButton, host.Accessibility);
        exitButton.Click += (_, _) => exit();
        hud.AddChild(exitButton);

        _screens = new GumUiScreenStack(host);
        _screens.Push(new GumUiScreen(root, "Starter HUD")
        {
            InitialFocus = exitButton,
            InputMode = GumUiInputMode.Overlay
        });
    }

    public void Update(GameTime gameTime) => _screens.Update(gameTime);
    public void Dispose() => _screens.Dispose();
}
#endif
