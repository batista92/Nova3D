using Microsoft.Xna.Framework;

namespace Nova3D.Physics.Bepu;

public sealed class KinematicCharacterSettings
{
    public float Radius { get; init; } = 0.45f;
    public float CapsuleLength { get; init; } = 0.9f;
    public float SkinWidth { get; init; } = 0.02f;
    public float GroundProbeDistance { get; init; } = 0.12f;
    public float MaximumSlopeDegrees { get; init; } = 50f;
    public int MaximumSlideIterations { get; init; } = 3;
    public CollisionFilter CollisionFilter { get; init; } = CollisionFilter.All;

    internal void Validate()
    {
        if (!float.IsFinite(Radius) || Radius <= 0f) throw new ArgumentOutOfRangeException(nameof(Radius));
        if (!float.IsFinite(CapsuleLength) || CapsuleLength <= 0f) throw new ArgumentOutOfRangeException(nameof(CapsuleLength));
        if (!float.IsFinite(SkinWidth) || SkinWidth < 0f) throw new ArgumentOutOfRangeException(nameof(SkinWidth));
        if (!float.IsFinite(GroundProbeDistance) || GroundProbeDistance <= 0f)
            throw new ArgumentOutOfRangeException(nameof(GroundProbeDistance));
        if (!float.IsFinite(MaximumSlopeDegrees) || MaximumSlopeDegrees is < 0f or >= 90f)
            throw new ArgumentOutOfRangeException(nameof(MaximumSlopeDegrees));
        if (MaximumSlideIterations < 1) throw new ArgumentOutOfRangeException(nameof(MaximumSlideIterations));
    }
}
