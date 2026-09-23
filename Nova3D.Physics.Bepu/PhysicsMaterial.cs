using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;

namespace Nova3D.Physics.Bepu;

public readonly record struct PhysicsMaterial(
    float Friction,
    float MaximumRecoveryVelocity,
    float SpringFrequency,
    float SpringDampingRatio)
{
    public static PhysicsMaterial Default { get; } = new(1f, 2f, 30f, 1f);

    public PhysicsMaterial Validate()
    {
        if (!float.IsFinite(Friction) || Friction < 0f)
            throw new ArgumentOutOfRangeException(nameof(Friction));
        if (!float.IsFinite(MaximumRecoveryVelocity) || MaximumRecoveryVelocity < 0f)
            throw new ArgumentOutOfRangeException(nameof(MaximumRecoveryVelocity));
        if (!float.IsFinite(SpringFrequency) || SpringFrequency <= 0f)
            throw new ArgumentOutOfRangeException(nameof(SpringFrequency));
        if (!float.IsFinite(SpringDampingRatio) || SpringDampingRatio < 0f)
            throw new ArgumentOutOfRangeException(nameof(SpringDampingRatio));
        return this;
    }

    internal static PairMaterialProperties Combine(in PhysicsMaterial a, in PhysicsMaterial b)
    {
        return new PairMaterialProperties
        {
            FrictionCoefficient = MathF.Sqrt(a.Friction * b.Friction),
            MaximumRecoveryVelocity = MathF.Min(a.MaximumRecoveryVelocity, b.MaximumRecoveryVelocity),
            SpringSettings = new SpringSettings(
                MathF.Min(a.SpringFrequency, b.SpringFrequency),
                MathF.Max(a.SpringDampingRatio, b.SpringDampingRatio))
        };
    }
}
