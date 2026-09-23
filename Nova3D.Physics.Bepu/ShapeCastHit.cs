using BepuPhysics.Collidables;
using Microsoft.Xna.Framework;

namespace Nova3D.Physics.Bepu;

public readonly record struct ShapeCastHit(
    Vector3 Position,
    Vector3 Normal,
    float Distance,
    CollidableReference Collidable);
