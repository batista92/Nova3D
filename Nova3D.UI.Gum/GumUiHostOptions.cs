namespace Nova3D.UI.Gum;

public enum GumUiScalingMode
{
    Expand,
    ZoomHeight,
    ZoomWidth
}

public enum GumUiInputMode
{
    Overlay,
    Exclusive
}

public sealed class GumUiHostOptions
{
    public GumUiScalingMode ScalingMode { get; init; } = GumUiScalingMode.Expand;
    public float DefaultZoom { get; init; } = 1f;
    public GumUiInputMode InputMode { get; init; } = GumUiInputMode.Overlay;
    public bool EnableKeyboard { get; init; } = true;
    public bool EnableGamePads { get; init; } = true;
    public GumUiAccessibilitySettings Accessibility { get; init; } = new();

    internal void Validate()
    {
        if (!float.IsFinite(DefaultZoom) || DefaultZoom <= 0f)
            throw new ArgumentOutOfRangeException(nameof(DefaultZoom));
        ArgumentNullException.ThrowIfNull(Accessibility);
        Accessibility.Validate();
    }
}
