namespace Nova3D.Production.VisualTesting;

/// <summary>
/// Immutable inputs that a game must apply while producing a visual-regression capture.
/// </summary>
public sealed class VisualCapturePlan
{
    public VisualCapturePlan(
        string sceneName,
        int width,
        int height,
        int seed,
        TimeSpan fixedTimeStep,
        int warmupFrames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sceneName);
        if (sceneName is "." or ".." ||
            sceneName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            sceneName.Contains(Path.DirectorySeparatorChar) ||
            sceneName.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("Scene name must be one safe file-name segment.", nameof(sceneName));
        }
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (fixedTimeStep <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(fixedTimeStep));
        if (warmupFrames < 0) throw new ArgumentOutOfRangeException(nameof(warmupFrames));

        SceneName = sceneName;
        Width = width;
        Height = height;
        Seed = seed;
        FixedTimeStep = fixedTimeStep;
        WarmupFrames = warmupFrames;
    }

    public string SceneName { get; }
    public int Width { get; }
    public int Height { get; }
    public int Seed { get; }
    public TimeSpan FixedTimeStep { get; }
    public int WarmupFrames { get; }
    public int CaptureFrame => checked(WarmupFrames + 1);
}
