namespace Nova3D.World.Terrain;

using Nova3D.World.Streaming;

public sealed class ChunkedTerrainSettings
{
    public float WorldSize { get; init; } = 2048f;
    public int ChunksPerAxis { get; init; } = 32;
    public int[] LodSegments { get; init; } = { 16, 8, 4 };
    public float[] LodDistances { get; init; } = { 300f, 760f };
    public int ShadowSegments { get; init; } = 128;
    public float BoundsPadding { get; init; } = 8f;
    public WorldStreamingSettings? Streaming { get; init; }

    internal void Validate()
    {
        if (WorldSize <= 0f) throw new ArgumentOutOfRangeException(nameof(WorldSize));
        if (ChunksPerAxis <= 0) throw new ArgumentOutOfRangeException(nameof(ChunksPerAxis));
        if (LodSegments.Length == 0) throw new ArgumentException("At least one terrain LOD is required.");
        if (LodDistances.Length != LodSegments.Length - 1)
            throw new ArgumentException("LOD distances must contain one threshold between each LOD.");
        var previous = 0f;
        foreach (var distance in LodDistances)
        {
            if (distance <= previous) throw new ArgumentException("LOD distances must be ascending.");
            previous = distance;
        }
        foreach (var segments in LodSegments)
            if (segments <= 0) throw new ArgumentOutOfRangeException(nameof(LodSegments));
        if (ShadowSegments <= 0 || (ShadowSegments + 1) * (ShadowSegments + 1) > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(ShadowSegments));
        Streaming?.Validate();
    }
}
