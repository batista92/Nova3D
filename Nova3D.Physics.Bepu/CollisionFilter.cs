namespace Nova3D.Physics.Bepu;

/// <summary>Bit-mask based collision membership and acceptance rules.</summary>
public readonly record struct CollisionFilter(uint Membership, uint CollidesWith)
{
    public static CollisionFilter All { get; } = new(uint.MaxValue, uint.MaxValue);
    public static CollisionFilter None { get; } = new(0u, 0u);

    public static CollisionFilter Layer(int layer, uint collidesWith = uint.MaxValue)
    {
        if ((uint)layer >= 32u) throw new ArgumentOutOfRangeException(nameof(layer));
        return new CollisionFilter(1u << layer, collidesWith);
    }

    public bool Allows(in CollisionFilter other) =>
        (Membership & other.CollidesWith) != 0u &&
        (other.Membership & CollidesWith) != 0u;
}
