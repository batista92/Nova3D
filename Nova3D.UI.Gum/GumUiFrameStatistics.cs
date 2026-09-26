namespace Nova3D.UI.Gum;

public readonly record struct GumUiFrameStatistics(
    double UpdateMilliseconds,
    double DrawMilliseconds,
    long ManagedBytes);
