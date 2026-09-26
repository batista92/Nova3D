using Gum.Forms.Controls;
using Microsoft.Xna.Framework;

namespace Nova3D.UI.Gum;

/// <summary>Basic readable-text, hit-target, contrast and motion preferences.</summary>
public sealed class GumUiAccessibilitySettings
{
    public float TextScale { get; init; } = 1f;
    public float MinimumHitTarget { get; init; } = 40f;
    public float MinimumContrastRatio { get; init; } = 4.5f;
    public bool ReducedMotion { get; init; }

    public void Validate()
    {
        if (!float.IsFinite(TextScale) || TextScale <= 0f)
            throw new ArgumentOutOfRangeException(nameof(TextScale));
        if (!float.IsFinite(MinimumHitTarget) || MinimumHitTarget < 0f)
            throw new ArgumentOutOfRangeException(nameof(MinimumHitTarget));
        if (!float.IsFinite(MinimumContrastRatio) || MinimumContrastRatio is < 1f or > 21f)
            throw new ArgumentOutOfRangeException(nameof(MinimumContrastRatio));
    }

    public void ApplyHitTarget(FrameworkElement control)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.Width = MathF.Max(control.Width, MinimumHitTarget);
        control.Height = MathF.Max(control.Height, MinimumHitTarget);
    }

    public bool HasSufficientContrast(Color foreground, Color background) =>
        GetContrastRatio(foreground, background) >= MinimumContrastRatio;

    public static float GetContrastRatio(Color first, Color second)
    {
        float firstLuminance = RelativeLuminance(first);
        float secondLuminance = RelativeLuminance(second);
        float lighter = MathF.Max(firstLuminance, secondLuminance);
        float darker = MathF.Min(firstLuminance, secondLuminance);
        return (lighter + 0.05f) / (darker + 0.05f);
    }

    private static float RelativeLuminance(Color color) =>
        0.2126f * Linear(color.R / 255f) +
        0.7152f * Linear(color.G / 255f) +
        0.0722f * Linear(color.B / 255f);

    private static float Linear(float value) =>
        value <= 0.04045f ? value / 12.92f : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
}
