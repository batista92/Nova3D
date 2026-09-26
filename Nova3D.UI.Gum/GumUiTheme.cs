using Gum.Forms.Controls;
using Gum.Forms.DefaultVisuals.V3;
using Microsoft.Xna.Framework;

namespace Nova3D.UI.Gum;

/// <summary>Reusable V3 visual tokens applied to ordinary Gum Forms controls.</summary>
public sealed class GumUiTheme
{
    public static GumUiTheme Default { get; } = new();

    public Color SurfaceColor { get; init; } = new(35, 43, 55);
    public Color ForegroundColor { get; init; } = new(236, 241, 247);
    public Color AccentColor { get; init; } = new(72, 168, 255);
    public GumFontStyle BodyFont { get; init; } = new(18);
    public GumFontStyle TitleFont { get; init; } = new(26, isBold: true);

    public void Apply(Button button, GumUiAccessibilitySettings? accessibility = null)
    {
        ArgumentNullException.ThrowIfNull(button);
        if (button.Visual is not ButtonVisual visual)
            throw UnsupportedVisual(button, nameof(ButtonVisual));
        visual.BackgroundColor = SurfaceColor;
        visual.ForegroundColor = ForegroundColor;
        visual.FocusedIndicatorColor = AccentColor;
        BodyFont.Apply(visual.TextInstance, accessibility?.TextScale ?? 1f);
        accessibility?.ApplyHitTarget(button);
    }

    public void Apply(Label label, bool title = false, GumUiAccessibilitySettings? accessibility = null)
    {
        ArgumentNullException.ThrowIfNull(label);
        if (label.Visual is not LabelVisual visual)
            throw UnsupportedVisual(label, nameof(LabelVisual));
        (title ? TitleFont : BodyFont).Apply(visual, accessibility?.TextScale ?? 1f);
    }

    public void Apply(Slider slider, GumUiAccessibilitySettings? accessibility = null)
    {
        ArgumentNullException.ThrowIfNull(slider);
        if (slider.Visual is not SliderVisual visual)
            throw UnsupportedVisual(slider, nameof(SliderVisual));
        visual.TrackBackgroundColor = SurfaceColor;
        visual.FocusedIndicatorColor = AccentColor;
        accessibility?.ApplyHitTarget(slider);
    }

    public void ValidateAccessibility(GumUiAccessibilitySettings accessibility)
    {
        ArgumentNullException.ThrowIfNull(accessibility);
        accessibility.Validate();
        if (!accessibility.HasSufficientContrast(ForegroundColor, SurfaceColor))
            throw new InvalidOperationException(
                $"Theme foreground/surface contrast is {GumUiAccessibilitySettings.GetContrastRatio(ForegroundColor, SurfaceColor):F2}:1; " +
                $"required is {accessibility.MinimumContrastRatio:F2}:1.");
    }

    private static InvalidOperationException UnsupportedVisual(FrameworkElement control, string expected) =>
        new($"{control.GetType().Name} does not use the expected Gum V3 {expected}. " +
            "Apply a custom theme directly to custom visuals.");
}
