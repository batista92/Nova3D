namespace Nova3D.Physics.Bepu;

using Nova3D.World.Streaming;

public sealed class TerrainPhysicsSettings
{
    public float WorldSize { get; init; } = 2048f;
    public int ChunksPerAxis { get; init; } = 32;
    public int SegmentsPerChunk { get; init; } = 16;
    public CollisionFilter CollisionFilter { get; init; } = CollisionFilter.All;
    public PhysicsMaterial Material { get; init; } = PhysicsMaterial.Default;
    public WorldStreamingSettings? Streaming { get; init; }

    internal void Validate()
    {
        if (!float.IsFinite(WorldSize) || WorldSize <= 0f)
            throw new ArgumentOutOfRangeException(nameof(WorldSize));
        if (ChunksPerAxis < 1) throw new ArgumentOutOfRangeException(nameof(ChunksPerAxis));
        if (SegmentsPerChunk < 1) throw new ArgumentOutOfRangeException(nameof(SegmentsPerChunk));
        Material.Validate();
    }
}
