using BepuPhysics;
using Microsoft.Xna.Framework;

namespace Nova3D.Physics.Bepu;

public readonly struct BepuBody
{
    private readonly BepuPhysicsWorld _world;

    internal BepuBody(BepuPhysicsWorld world, BodyHandle handle)
    {
        _world = world;
        Handle = handle;
    }

    public BodyHandle Handle { get; }
    public bool Exists => _world.BodyExists(Handle);
    public Vector3 Position => _world.GetPosition(Handle);
    public Quaternion Rotation => _world.GetRotation(Handle);
    public Matrix WorldMatrix => _world.GetWorldMatrix(Handle);
    public bool IsKinematic => _world.IsKinematic(Handle);
    public bool IsTrigger
    {
        get => _world.GetIsTrigger(Handle);
        set => _world.SetIsTrigger(Handle, value);
    }
    public Vector3 LinearVelocity
    {
        get => _world.GetLinearVelocity(Handle);
        set => _world.SetLinearVelocity(Handle, value);
    }
    public CollisionFilter CollisionFilter
    {
        get => _world.GetCollisionFilter(Handle);
        set => _world.SetCollisionFilter(Handle, value);
    }
    public PhysicsMaterial Material
    {
        get => _world.GetMaterial(Handle);
        set => _world.SetMaterial(Handle, value);
    }

    public void SetPose(Vector3 position, Quaternion rotation) =>
        _world.SetPose(Handle, position, rotation);
}
