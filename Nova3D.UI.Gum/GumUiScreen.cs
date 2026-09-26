using Gum.Forms.Controls;
using Microsoft.Xna.Framework;

namespace Nova3D.UI.Gum;

/// <summary>A navigation unit whose visual tree is made of regular Gum controls.</summary>
public class GumUiScreen : IDisposable
{
    private bool _disposed;

    public GumUiScreen(FrameworkElement root, string? name = null)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        Name = string.IsNullOrWhiteSpace(name) ? GetType().Name : name;
    }

    public string Name { get; }
    public FrameworkElement Root { get; }
    public FrameworkElement? InitialFocus { get; init; }
    public GumUiInputMode InputMode { get; init; } = GumUiInputMode.Exclusive;
    public bool CoversPrevious { get; init; } = true;
    public bool IsDisposed => _disposed;

    public virtual void Enter() { }
    public virtual void Leave() { }
    public virtual void Update(GameTime gameTime) { }

    public virtual void Dispose()
    {
        _disposed = true;
    }
}
