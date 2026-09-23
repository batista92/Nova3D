using BepuPhysics.Collidables;

namespace Nova3D.Physics.Bepu;

public enum TriggerEventType { Enter, Stay, Exit }

public readonly record struct TriggerPair(
    CollidableReference Trigger,
    CollidableReference Other);

public readonly record struct TriggerEvent(
    TriggerEventType Type,
    CollidableReference Trigger,
    CollidableReference Other);
