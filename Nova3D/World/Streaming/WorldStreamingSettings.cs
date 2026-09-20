namespace Nova3D.World.Streaming;

using Microsoft.Xna.Framework;

public sealed class WorldStreamingSettings
{
    public float CellSize { get; init; } = 128f;
    public Vector2 Origin { get; init; }
    public float LoadRadius { get; init; } = 768f;
    public float RetainRadius { get; init; } = 896f;
    public int MaxLoadsPerUpdate { get; init; } = 4;
    public int MaxUnloadsPerUpdate { get; init; } = 8;

    internal void Validate()
    {
        if (!float.IsFinite(CellSize) || CellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(CellSize));
        if (!float.IsFinite(LoadRadius) || LoadRadius < 0f) throw new ArgumentOutOfRangeException(nameof(LoadRadius));
        if (!float.IsFinite(RetainRadius) || RetainRadius < LoadRadius)
            throw new ArgumentOutOfRangeException(nameof(RetainRadius), "RetainRadius must be at least LoadRadius.");
        if (MaxLoadsPerUpdate <= 0) throw new ArgumentOutOfRangeException(nameof(MaxLoadsPerUpdate));
        if (MaxUnloadsPerUpdate <= 0) throw new ArgumentOutOfRangeException(nameof(MaxUnloadsPerUpdate));
    }
}
