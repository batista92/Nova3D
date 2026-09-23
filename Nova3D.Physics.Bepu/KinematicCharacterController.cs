using Microsoft.Xna.Framework;

namespace Nova3D.Physics.Bepu;

/// <summary>Minimal capsule-based kinematic movement with sweep-and-slide.</summary>
public sealed class KinematicCharacterController
{
    private readonly BepuPhysicsWorld _world;
    private readonly KinematicCharacterSettings _settings;

    public KinematicCharacterController(BepuPhysicsWorld world, Vector3 position,
        KinematicCharacterSettings? settings = null)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _settings = settings ?? new KinematicCharacterSettings();
        _settings.Validate();
        Body = _world.CreateKinematicCapsule(position, _settings.Radius,
            _settings.CapsuleLength, collisionFilter: _settings.CollisionFilter);
    }

    public BepuBody Body { get; }
    public Vector3 Position => Body.Position;
    public bool IsGrounded { get; private set; }
    public Vector3 GroundNormal { get; private set; } = Vector3.Up;

    public Vector3 Move(Vector3 displacement)
    {
        Vector3 position = Body.Position;
        Vector3 remaining = displacement;
        for (int iteration = 0; iteration < _settings.MaximumSlideIterations; iteration++)
        {
            float distance = remaining.Length();
            if (distance < 1e-6f) break;
            Vector3 direction = remaining / distance;
            if (!_world.CapsuleCast(position, _settings.Radius, _settings.CapsuleLength,
                    Body.Rotation, direction, distance + _settings.SkinWidth, out ShapeCastHit hit,
                    _settings.CollisionFilter, ignoredBody: Body))
            {
                position += remaining;
                remaining = Vector3.Zero;
                break;
            }

            float travel = MathF.Max(0f, MathF.Min(distance, hit.Distance - _settings.SkinWidth));
            position += direction * travel;
            Vector3 untraveled = remaining - direction * travel;
            float intoSurface = Vector3.Dot(untraveled, hit.Normal);
            remaining = intoSurface < 0f ? untraveled - hit.Normal * intoSurface : untraveled;
            if (travel <= 1e-6f && remaining.LengthSquared() >= untraveled.LengthSquared() - 1e-8f)
                break;
        }

        Body.SetPose(position, Body.Rotation);
        RefreshGrounded();
        return position;
    }

    public void Teleport(Vector3 position)
    {
        Body.SetPose(position, Body.Rotation);
        RefreshGrounded();
    }

    public void RefreshGrounded()
    {
        IsGrounded = _world.CapsuleCast(Body.Position, _settings.Radius,
            _settings.CapsuleLength, Body.Rotation, Vector3.Down,
            _settings.GroundProbeDistance + _settings.SkinWidth, out ShapeCastHit hit,
            _settings.CollisionFilter, ignoredBody: Body) &&
            Vector3.Dot(hit.Normal, Vector3.Up) >=
            MathF.Cos(MathHelper.ToRadians(_settings.MaximumSlopeDegrees));
        GroundNormal = IsGrounded ? hit.Normal : Vector3.Up;
    }
}
