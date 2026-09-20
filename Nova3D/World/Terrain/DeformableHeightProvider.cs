using Microsoft.Xna.Framework;

namespace Nova3D.World.Terrain;

public readonly record struct TerrainRegion(float MinX, float MinZ, float MaxX, float MaxZ)
{
    public TerrainRegion Expand(float amount) =>
        new(MinX - amount, MinZ - amount, MaxX + amount, MaxZ + amount);
}

public interface IDeformableHeightProvider : IHeightProvider
{
    TerrainRegion ApplyRadialDelta(float worldX, float worldZ, float radius, float delta);
}

/// <summary>Additive radial deformation layered over any base height provider.</summary>
public sealed class DeformableHeightProvider : IDeformableHeightProvider
{
    private readonly record struct Stamp(float X, float Z, float Radius, float Delta);
    private readonly IHeightProvider _baseHeights;
    private readonly List<Stamp> _stamps = new();

    public DeformableHeightProvider(IHeightProvider baseHeights)
    {
        _baseHeights = baseHeights ?? throw new ArgumentNullException(nameof(baseHeights));
    }

    public int DeformationCount => _stamps.Count;

    public float SampleHeight(float worldX, float worldZ)
    {
        var height = _baseHeights.SampleHeight(worldX, worldZ);
        foreach (var stamp in _stamps)
        {
            var dx = worldX - stamp.X;
            var dz = worldZ - stamp.Z;
            var distanceSquared = dx * dx + dz * dz;
            if (distanceSquared >= stamp.Radius * stamp.Radius) continue;
            var falloff = 1f - MathF.Sqrt(distanceSquared) / stamp.Radius;
            falloff = falloff * falloff * (3f - 2f * falloff);
            height += stamp.Delta * falloff;
        }
        return height;
    }

    public TerrainRegion ApplyRadialDelta(float worldX, float worldZ, float radius, float delta)
    {
        if (!float.IsFinite(worldX) || !float.IsFinite(worldZ)) throw new ArgumentOutOfRangeException(nameof(worldX));
        if (!float.IsFinite(radius) || radius <= 0f) throw new ArgumentOutOfRangeException(nameof(radius));
        if (!float.IsFinite(delta) || delta == 0f) throw new ArgumentOutOfRangeException(nameof(delta));
        _stamps.Add(new Stamp(worldX, worldZ, radius, delta));
        return new TerrainRegion(worldX - radius, worldZ - radius, worldX + radius, worldZ + radius);
    }

    public void Clear() => _stamps.Clear();
}
