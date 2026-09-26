using System.Diagnostics;
using Gum;
using Gum.Forms;
using Gum.Forms.Controls;
using Microsoft.Xna.Framework;

namespace Nova3D.UI.Gum;

/// <summary>Owns one Gum service lifecycle without wrapping Gum controls.</summary>
public sealed class GumUiHost : IDisposable
{
    private static GumUiHost? _active;
    private readonly GumUiHostOptions _options;
    private long _updateAllocatedBytes;
    private bool _disposed;

    public GumUiHost(Game game, GumUiHostOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (_active is not null)
            throw new InvalidOperationException("Only one GumUiHost can be active at a time.");

        _options = options ?? new GumUiHostOptions();
        _options.Validate();
        bool initialized = false;
        try
        {
            Service.Initialize(game, DefaultVisualsVersion.V3);
            initialized = true;
            Service.ContentLoader!.XnaContentManager = game.Content;
            ConfigureScaling();

            FrameworkElement.KeyboardsForUiControl.Clear();
            if (_options.EnableKeyboard)
                FrameworkElement.KeyboardsForUiControl.Add(Service.Keyboard);
            FrameworkElement.GamePadsForUiControl.Clear();
            if (_options.EnableGamePads)
                FrameworkElement.GamePadsForUiControl.AddRange(Service.Gamepads);
            InputMode = _options.InputMode;
            Navigation.Reset(_options.EnableKeyboard, _options.EnableGamePads);
            _active = this;
        }
        catch
        {
            if (initialized) Service.Uninitialize();
            throw;
        }
    }

    public GumService Service => GumService.Default;
    public GumUiInputMode InputMode { get; set; }
    public GumUiAccessibilitySettings Accessibility => _options.Accessibility;
    public GumUiNavigationInput Navigation { get; } = new();
    public GumUiFrameStatistics Statistics { get; private set; }
    public bool PointerOverUi { get; private set; }
    public bool CapturesMouse => InputMode == GumUiInputMode.Exclusive || PointerOverUi;
    public bool CapturesKeyboard => InputMode == GumUiInputMode.Exclusive;
    public bool CapturesGamePad => InputMode == GumUiInputMode.Exclusive;

    public void Update(GameTime gameTime)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        Service.Update(gameTime);
        Navigation.Update(_options.EnableKeyboard, _options.EnableGamePads);
        double elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        _updateAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        PointerOverUi = Service.Cursor.FrameworkElementOver is not null ||
                        (Service.Cursor.PrimaryDown &&
                         Service.Cursor.FrameworkElementPushed is not null);
        Statistics = Statistics with
        {
            UpdateMilliseconds = Smooth(Statistics.UpdateMilliseconds, elapsed)
        };
    }

    /// <summary>Draw after the world and post-processing have resolved to the back buffer.</summary>
    public void Draw()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        Service.Draw();
        double elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        long allocated = _updateAllocatedBytes +
                         GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Statistics = new GumUiFrameStatistics(
            Statistics.UpdateMilliseconds,
            Smooth(Statistics.DrawMilliseconds, elapsed),
            allocated);
    }

    public void Dispose()
    {
        if (_disposed) return;
        FrameworkElement.KeyboardsForUiControl.Clear();
        FrameworkElement.GamePadsForUiControl.Clear();
        Service.Uninitialize();
        _active = null;
        _disposed = true;
    }

    private void ConfigureScaling()
    {
        switch (_options.ScalingMode)
        {
            case GumUiScalingMode.Expand:
                Service.EnableExpandToWindow(_options.DefaultZoom);
                break;
            case GumUiScalingMode.ZoomHeight:
                Service.EnableZoomToWindow(WindowZoomMode.HeightDominant, _options.DefaultZoom);
                break;
            case GumUiScalingMode.ZoomWidth:
                Service.EnableZoomToWindow(WindowZoomMode.WidthDominant, _options.DefaultZoom);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static double Smooth(double previous, double current) =>
        previous <= 0d ? current : previous * 0.95d + current * 0.05d;
}
