namespace Nova3D.Production.Configuration;

public sealed class Nova3DConfiguration
{
    public WindowConfiguration Window { get; init; } = new();
    public RendererConfiguration Renderer { get; init; } = new();
    public StreamingConfiguration Streaming { get; init; } = new();

    public void Validate()
    {
        Window.Validate();
        Renderer.Validate();
        Streaming.Validate();
    }
}

public sealed class WindowConfiguration
{
    public int Width { get; init; } = 1280;
    public int Height { get; init; } = 720;
    public bool VSync { get; init; }
    internal void Validate()
    {
        if (Width < 640 || Height < 360) throw new InvalidOperationException("Window resolution is too small.");
    }
}

public sealed class RendererConfiguration
{
    public int ShadowResolution { get; init; } = 4096;
    public float Exposure { get; init; } = 0.95f;
    public float BloomStrength { get; init; } = 0.12f;
    internal void Validate()
    {
        if (ShadowResolution is < 512 or > 8192 || (ShadowResolution & (ShadowResolution - 1)) != 0)
            throw new InvalidOperationException("ShadowResolution must be a power of two between 512 and 8192.");
        if (!float.IsFinite(Exposure) || Exposure <= 0f) throw new InvalidOperationException("Exposure must be positive.");
        if (!float.IsFinite(BloomStrength) || BloomStrength < 0f) throw new InvalidOperationException("BloomStrength cannot be negative.");
    }
}

public sealed class StreamingConfiguration
{
    public float LoadRadius { get; init; } = 2200f;
    public float RetainRadius { get; init; } = 2350f;
    public int MaxLoadsPerFrame { get; init; } = 32;
    public int MaxUnloadsPerFrame { get; init; } = 64;
    internal void Validate()
    {
        if (LoadRadius <= 0f || RetainRadius < LoadRadius)
            throw new InvalidOperationException("Streaming radii are invalid.");
        if (MaxLoadsPerFrame <= 0 || MaxUnloadsPerFrame <= 0)
            throw new InvalidOperationException("Streaming budgets must be positive.");
    }
}
