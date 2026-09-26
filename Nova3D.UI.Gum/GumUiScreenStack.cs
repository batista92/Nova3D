using Microsoft.Xna.Framework;

namespace Nova3D.UI.Gum;

/// <summary>Owns pushed screens and synchronizes their Gum roots and input mode.</summary>
public sealed class GumUiScreenStack : IDisposable
{
    private readonly GumUiHost _host;
    private readonly List<GumUiScreen> _screens = new();
    private readonly GumUiInputMode _emptyInputMode;
    private bool _disposed;

    public GumUiScreenStack(GumUiHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _emptyInputMode = host.InputMode;
    }

    public int Count => _screens.Count;
    public GumUiScreen? Current => _screens.Count == 0 ? null : _screens[^1];
    public IReadOnlyList<GumUiScreen> Screens => _screens;

    public void Push(GumUiScreen screen)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(screen);
        if (screen.IsDisposed) throw new ObjectDisposedException(screen.Name);
        if (_screens.Contains(screen))
            throw new InvalidOperationException("A UI screen instance cannot be pushed twice.");

        GumUiScreen? previous = Current;
        previous?.Leave();
        if (previous is not null && screen.CoversPrevious)
            previous.Root.IsVisible = false;

        _screens.Add(screen);
        screen.Root.IsVisible = true;
        screen.Root.Visual.AddToRoot();
        _host.InputMode = screen.InputMode;
        screen.Enter();
        if (screen.InitialFocus is not null)
            screen.InitialFocus.IsFocused = true;
    }

    public bool Pop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_screens.Count == 0) return false;

        GumUiScreen removed = _screens[^1];
        _screens.RemoveAt(_screens.Count - 1);
        removed.Leave();
        removed.Root.Visual.RemoveFromRoot();
        removed.Dispose();

        GumUiScreen? revealed = Current;
        if (revealed is null)
        {
            _host.InputMode = _emptyInputMode;
            return true;
        }

        revealed.Root.IsVisible = true;
        _host.InputMode = revealed.InputMode;
        revealed.Enter();
        if (revealed.InitialFocus is not null)
            revealed.InitialFocus.IsFocused = true;
        return true;
    }

    public void Replace(GumUiScreen screen)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_screens.Count > 0) Pop();
        Push(screen);
    }

    public void Update(GameTime gameTime)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Current?.Update(gameTime);
    }

    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        while (_screens.Count > 0) Pop();
    }

    public void Dispose()
    {
        if (_disposed) return;
        Clear();
        _disposed = true;
    }
}
