using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Constraints;
using BepuPhysics.Trees;
using BepuUtilities;
using BepuUtilities.Memory;
using Microsoft.Xna.Framework;
using Nova3D.Production.Debugging;
using NumericsQuaternion = System.Numerics.Quaternion;
using NumericsVector3 = System.Numerics.Vector3;

namespace Nova3D.Physics.Bepu;

public sealed class BepuPhysicsWorld : IDisposable
{
    private readonly BufferPool _bufferPool = new();
    private readonly ThreadDispatcher? _threadDispatcher;
    private readonly CollidableProperty<CollisionFilter> _collisionFilters;
    private readonly CollidableProperty<PhysicsMaterial> _materials;
    private readonly CollidableProperty<byte> _triggers;
    private readonly TriggerEventCollector _triggerEventCollector = new();
    private readonly float _fixedTimeStep;
    private readonly int _maximumStepsPerUpdate;
    private readonly Dictionary<BodyHandle, ColliderEntry> _bodyShapes = new();
    private readonly Dictionary<StaticHandle, ColliderEntry> _staticShapes = new();
    private float _accumulator;
    private bool _disposed;

    public BepuPhysicsWorld(PhysicsWorldOptions? options = null)
    {
        options ??= new PhysicsWorldOptions();
        options.Validate();
        _fixedTimeStep = options.FixedTimeStep;
        _maximumStepsPerUpdate = options.MaximumStepsPerUpdate;
        _threadDispatcher = options.WorkerCount > 1 ? new ThreadDispatcher(options.WorkerCount) : null;
        _collisionFilters = new CollidableProperty<CollisionFilter>(_bufferPool);
        _materials = new CollidableProperty<PhysicsMaterial>(_bufferPool);
        _triggers = new CollidableProperty<byte>(_bufferPool);
        Simulation = Simulation.Create(
            _bufferPool,
            new NarrowPhaseCallbacks(_collisionFilters, _materials, _triggers, _triggerEventCollector),
            new PoseIntegratorCallbacks(options.Gravity.ToNumerics()),
            new SolveDescription(options.SolverIterationCount, 1));
    }

    public Simulation Simulation { get; }
    public float FixedTimeStep => _fixedTimeStep;
    public float Accumulator => _accumulator;
    public int LastStepCount { get; private set; }
    public int BodyCount => _bodyShapes.Count;
    public int StaticCount => _staticShapes.Count;
    public double LastStepMilliseconds { get; private set; }
    public IReadOnlyList<TriggerEvent> TriggerEvents { get; private set; } = Array.Empty<TriggerEvent>();

    public int Update(float elapsedSeconds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));

        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        _triggerEventCollector.BeginUpdate();
        _accumulator += Math.Min(elapsedSeconds, _fixedTimeStep * _maximumStepsPerUpdate);
        int steps = 0;
        while (_accumulator >= _fixedTimeStep && steps < _maximumStepsPerUpdate)
        {
            Simulation.Timestep(_fixedTimeStep, _threadDispatcher);
            _accumulator -= _fixedTimeStep;
            steps++;
        }

        LastStepCount = steps;
        if (steps > 0) TriggerEvents = _triggerEventCollector.CompleteUpdate();
        else TriggerEvents = Array.Empty<TriggerEvent>();
        LastStepMilliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        return steps;
    }

    public BepuBody CreateDynamicBox(Vector3 position, Vector3 size, float mass = 1f,
        Quaternion? rotation = null, CollisionFilter? collisionFilter = null,
        PhysicsMaterial? material = null)
    {
        ValidateSize(size);
        ValidateMass(mass);
        var shape = new Box(size.X, size.Y, size.Z);
        BodyInertia inertia = shape.ComputeInertia(mass);
        TypedIndex shapeIndex = Simulation.Shapes.Add(shape);
        BodyHandle handle = Simulation.Bodies.Add(BodyDescription.CreateDynamic(
            new RigidPose(position.ToNumerics(), (rotation ?? Quaternion.Identity).ToNumerics()),
            inertia,
            new CollidableDescription(shapeIndex, 0.1f),
            new BodyActivityDescription(0.01f)));
        _bodyShapes.Add(handle, new ColliderEntry(shapeIndex, ColliderKind.Box, size));
        _collisionFilters.Allocate(handle) = collisionFilter ?? CollisionFilter.All;
        _materials.Allocate(handle) = (material ?? PhysicsMaterial.Default).Validate();
        _triggers.Allocate(handle) = 0;
        return new BepuBody(this, handle);
    }

    public BepuBody CreateDynamicSphere(Vector3 position, float radius, float mass = 1f,
        CollisionFilter? collisionFilter = null, PhysicsMaterial? material = null)
    {
        ValidatePositive(radius, nameof(radius));
        ValidateMass(mass);
        var shape = new Sphere(radius);
        BodyInertia inertia = shape.ComputeInertia(mass);
        TypedIndex shapeIndex = Simulation.Shapes.Add(shape);
        BodyHandle handle = Simulation.Bodies.Add(BodyDescription.CreateDynamic(
            new RigidPose(position.ToNumerics()), inertia,
            new CollidableDescription(shapeIndex, 0.1f),
            new BodyActivityDescription(0.01f)));
        _bodyShapes.Add(handle, new ColliderEntry(shapeIndex, ColliderKind.Sphere,
            new Vector3(radius)));
        _collisionFilters.Allocate(handle) = collisionFilter ?? CollisionFilter.All;
        _materials.Allocate(handle) = (material ?? PhysicsMaterial.Default).Validate();
        _triggers.Allocate(handle) = 0;
        return new BepuBody(this, handle);
    }

    public BepuBody CreateDynamicCapsule(Vector3 position, float radius, float length, float mass = 1f,
        CollisionFilter? collisionFilter = null, PhysicsMaterial? material = null)
    {
        ValidatePositive(radius, nameof(radius));
        ValidatePositive(length, nameof(length));
        ValidateMass(mass);
        var shape = new Capsule(radius, length);
        BodyInertia inertia = shape.ComputeInertia(mass);
        TypedIndex shapeIndex = Simulation.Shapes.Add(shape);
        BodyHandle handle = Simulation.Bodies.Add(BodyDescription.CreateDynamic(
            new RigidPose(position.ToNumerics()), inertia,
            new CollidableDescription(shapeIndex, 0.1f),
            new BodyActivityDescription(0.01f)));
        _bodyShapes.Add(handle, new ColliderEntry(shapeIndex, ColliderKind.Capsule,
            new Vector3(radius, length, 0f)));
        _collisionFilters.Allocate(handle) = collisionFilter ?? CollisionFilter.All;
        _materials.Allocate(handle) = (material ?? PhysicsMaterial.Default).Validate();
        _triggers.Allocate(handle) = 0;
        return new BepuBody(this, handle);
    }

    public BepuBody CreateKinematicBox(Vector3 position, Vector3 size,
        Quaternion? rotation = null, CollisionFilter? collisionFilter = null,
        PhysicsMaterial? material = null, bool isTrigger = false)
    {
        ValidateSize(size);
        var shape = new Box(size.X, size.Y, size.Z);
        TypedIndex shapeIndex = Simulation.Shapes.Add(shape);
        BodyHandle handle = Simulation.Bodies.Add(BodyDescription.CreateKinematic(
            new RigidPose(position.ToNumerics(), (rotation ?? Quaternion.Identity).ToNumerics()),
            new CollidableDescription(shapeIndex, 0.1f),
            new BodyActivityDescription(0.01f)));
        _bodyShapes.Add(handle, new ColliderEntry(shapeIndex, ColliderKind.Box, size));
        _collisionFilters.Allocate(handle) = collisionFilter ?? CollisionFilter.All;
        _materials.Allocate(handle) = (material ?? PhysicsMaterial.Default).Validate();
        _triggers.Allocate(handle) = isTrigger ? (byte)1 : (byte)0;
        return new BepuBody(this, handle);
    }

    public BepuBody CreateKinematicCapsule(Vector3 position, float radius, float length,
        Quaternion? rotation = null, CollisionFilter? collisionFilter = null,
        PhysicsMaterial? material = null, bool isTrigger = false)
    {
        ValidatePositive(radius, nameof(radius));
        ValidatePositive(length, nameof(length));
        var shape = new Capsule(radius, length);
        TypedIndex shapeIndex = Simulation.Shapes.Add(shape);
        BodyHandle handle = Simulation.Bodies.Add(BodyDescription.CreateKinematic(
            new RigidPose(position.ToNumerics(), (rotation ?? Quaternion.Identity).ToNumerics()),
            new CollidableDescription(shapeIndex, 0.1f),
            new BodyActivityDescription(0.01f)));
        _bodyShapes.Add(handle, new ColliderEntry(shapeIndex, ColliderKind.Capsule,
            new Vector3(radius, length, 0f)));
        _collisionFilters.Allocate(handle) = collisionFilter ?? CollisionFilter.All;
        _materials.Allocate(handle) = (material ?? PhysicsMaterial.Default).Validate();
        _triggers.Allocate(handle) = isTrigger ? (byte)1 : (byte)0;
        return new BepuBody(this, handle);
    }

    public StaticHandle CreateStaticBox(Vector3 position, Vector3 size, Quaternion? rotation = null,
        CollisionFilter? collisionFilter = null, PhysicsMaterial? material = null)
    {
        ValidateSize(size);
        TypedIndex shapeIndex = Simulation.Shapes.Add(new Box(size.X, size.Y, size.Z));
        StaticHandle handle = Simulation.Statics.Add(new StaticDescription(
            position.ToNumerics(),
            (rotation ?? Quaternion.Identity).ToNumerics(),
            shapeIndex));
        _staticShapes.Add(handle, new ColliderEntry(shapeIndex, ColliderKind.Box, size));
        _collisionFilters.Allocate(handle) = collisionFilter ?? CollisionFilter.All;
        _materials.Allocate(handle) = (material ?? PhysicsMaterial.Default).Validate();
        _triggers.Allocate(handle) = 0;
        return handle;
    }

    public StaticHandle CreateStaticTriangleMesh(Vector3 position, ReadOnlySpan<Vector3> vertices,
        ReadOnlySpan<int> indices, CollisionFilter? collisionFilter = null,
        PhysicsMaterial? material = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (vertices.Length < 3) throw new ArgumentException("A mesh requires vertices.", nameof(vertices));
        if (indices.Length == 0 || indices.Length % 3 != 0)
            throw new ArgumentException("Triangle indices must be non-empty and divisible by three.", nameof(indices));

        int triangleCount = indices.Length / 3;
        _bufferPool.Take<Triangle>(triangleCount, out Buffer<Triangle> triangles);
        try
        {
            for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
            {
                int indexOffset = triangleIndex * 3;
                int a = indices[indexOffset];
                int b = indices[indexOffset + 1];
                int c = indices[indexOffset + 2];
                if ((uint)a >= vertices.Length || (uint)b >= vertices.Length || (uint)c >= vertices.Length)
                    throw new ArgumentOutOfRangeException(nameof(indices), "An index is outside the vertex span.");
                NumericsVector3 va = vertices[a].ToNumerics();
                NumericsVector3 vb = vertices[b].ToNumerics();
                NumericsVector3 vc = vertices[c].ToNumerics();
                triangles[triangleIndex] = new Triangle(va, vb, vc);
            }

            var mesh = new BepuPhysics.Collidables.Mesh(triangles, NumericsVector3.One, _bufferPool);
            TypedIndex shapeIndex = Simulation.Shapes.Add(mesh);
            StaticHandle handle = Simulation.Statics.Add(new StaticDescription(
                position.ToNumerics(), NumericsQuaternion.Identity, shapeIndex));
            _staticShapes.Add(handle, new ColliderEntry(shapeIndex, ColliderKind.Mesh,
                Vector3.Zero, OwnsPooledResources: true));
            _collisionFilters.Allocate(handle) = collisionFilter ?? CollisionFilter.All;
            _materials.Allocate(handle) = (material ?? PhysicsMaterial.Default).Validate();
            _triggers.Allocate(handle) = 0;
            return handle;
        }
        catch
        {
            _bufferPool.Return(ref triangles);
            throw;
        }
    }

    public void Remove(BepuBody body)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (BodyExists(body.Handle))
        {
            Simulation.Bodies.Remove(body.Handle);
            if (_bodyShapes.Remove(body.Handle, out ColliderEntry shape))
                Simulation.Shapes.Remove(shape.Shape);
        }
    }

    public void Remove(StaticHandle handle)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!Simulation.Statics.StaticExists(handle)) return;
        Simulation.Statics.Remove(handle);
        if (_staticShapes.Remove(handle, out ColliderEntry shape))
        {
            if (shape.OwnsPooledResources) Simulation.Shapes.RemoveAndDispose(shape.Shape, _bufferPool);
            else Simulation.Shapes.Remove(shape.Shape);
        }
    }

    public ConstraintHandle CreateDistanceConstraint(BepuBody bodyA, BepuBody bodyB,
        float targetDistance, float springFrequency = 30f, float springDampingRatio = 1f)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!BodyExists(bodyA.Handle) || !BodyExists(bodyB.Handle))
            throw new InvalidOperationException("Both constraint bodies must exist in this world.");
        ValidatePositive(targetDistance, nameof(targetDistance));
        ValidatePositive(springFrequency, nameof(springFrequency));
        if (!float.IsFinite(springDampingRatio) || springDampingRatio < 0f)
            throw new ArgumentOutOfRangeException(nameof(springDampingRatio));
        var spring = new SpringSettings(springFrequency, springDampingRatio);
        var servo = new DistanceServo(NumericsVector3.Zero, NumericsVector3.Zero,
            targetDistance, spring, ServoSettings.Default);
        return Simulation.Solver.Add(bodyA.Handle, bodyB.Handle, servo);
    }

    public void Remove(ConstraintHandle handle)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Simulation.Solver.ConstraintExists(handle)) Simulation.Solver.Remove(handle);
    }

    public void DebugDraw(DebugRenderer renderer, Color? color = null)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ObjectDisposedException.ThrowIf(_disposed, this);
        Color lineColor = color ?? Color.Lime;
        foreach ((BodyHandle handle, ColliderEntry collider) in _bodyShapes)
        {
            RigidPose pose = Simulation.Bodies.GetBodyReference(handle).Pose;
            DrawCollider(renderer, pose, collider, lineColor);
        }
        foreach ((StaticHandle handle, ColliderEntry collider) in _staticShapes)
        {
            RigidPose pose = Simulation.Statics.GetStaticReference(handle).Pose;
            DrawCollider(renderer, pose, collider, lineColor);
        }
    }

    public bool Raycast(Vector3 origin, Vector3 direction, float maximumDistance, out PhysicsRayHit hit)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidatePositive(maximumDistance, nameof(maximumDistance));
        if (direction.LengthSquared() < 1e-12f)
            throw new ArgumentException("Ray direction must be nonzero.", nameof(direction));

        direction.Normalize();
        var handler = new ClosestRayHitHandler();
        NumericsVector3 rayOrigin = origin.ToNumerics();
        NumericsVector3 rayDirection = direction.ToNumerics();
        Simulation.RayCast(rayOrigin, rayDirection, maximumDistance, ref handler);
        if (!handler.Hit)
        {
            hit = default;
            return false;
        }

        hit = new PhysicsRayHit(
            origin + direction * handler.Distance,
            handler.Normal.ToMonoGame(),
            handler.Distance,
            handler.Collidable);
        return true;
    }

    public bool SphereCast(Vector3 origin, float radius, Vector3 direction,
        float maximumDistance, out ShapeCastHit hit, CollisionFilter? filter = null,
        bool includeTriggers = false, BepuBody? ignoredBody = null)
    {
        ValidatePositive(radius, nameof(radius));
        var shape = new Sphere(radius);
        return Sweep(shape, origin, Quaternion.Identity, direction, maximumDistance,
            filter ?? CollisionFilter.All, includeTriggers, ignoredBody, out hit);
    }

    public bool CapsuleCast(Vector3 origin, float radius, float length, Quaternion rotation,
        Vector3 direction, float maximumDistance, out ShapeCastHit hit,
        CollisionFilter? filter = null, bool includeTriggers = false,
        BepuBody? ignoredBody = null)
    {
        ValidatePositive(radius, nameof(radius));
        ValidatePositive(length, nameof(length));
        var shape = new Capsule(radius, length);
        return Sweep(shape, origin, rotation, direction, maximumDistance,
            filter ?? CollisionFilter.All, includeTriggers, ignoredBody, out hit);
    }

    private bool Sweep<TShape>(TShape shape, Vector3 origin, Quaternion rotation,
        Vector3 direction, float maximumDistance, CollisionFilter filter,
        bool includeTriggers, BepuBody? ignoredBody, out ShapeCastHit hit)
        where TShape : unmanaged, IConvexShape
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidatePositive(maximumDistance, nameof(maximumDistance));
        if (direction.LengthSquared() < 1e-12f)
            throw new ArgumentException("Sweep direction must be nonzero.", nameof(direction));
        direction.Normalize();
        var pose = new RigidPose(origin.ToNumerics(), rotation.ToNumerics());
        var velocity = new BodyVelocity(direction.ToNumerics());
        int ignoredHandle = ignoredBody?.Handle.Value ?? -1;
        var handler = new ClosestSweepHitHandler(
            _collisionFilters, _triggers, filter, includeTriggers, ignoredHandle);
        Simulation.Sweep(in shape, in pose, in velocity, maximumDistance, _bufferPool, ref handler);
        if (!handler.Hit)
        {
            hit = default;
            return false;
        }
        hit = new ShapeCastHit(handler.Position.ToMonoGame(), handler.Normal.ToMonoGame(),
            handler.Distance, handler.Collidable);
        return true;
    }

    internal bool BodyExists(BodyHandle handle) => !_disposed && Simulation.Bodies.BodyExists(handle);

    internal Vector3 GetPosition(BodyHandle handle) => GetBodyReference(handle).Pose.Position.ToMonoGame();
    internal Quaternion GetRotation(BodyHandle handle) => GetBodyReference(handle).Pose.Orientation.ToMonoGame();
    internal Microsoft.Xna.Framework.Matrix GetWorldMatrix(BodyHandle handle)
    {
        RigidPose pose = GetBodyReference(handle).Pose;
        return pose.Position.ToMonoGameMatrix(pose.Orientation);
    }
    internal Vector3 GetLinearVelocity(BodyHandle handle) => GetBodyReference(handle).Velocity.Linear.ToMonoGame();
    internal void SetLinearVelocity(BodyHandle handle, Vector3 value) => GetBodyReference(handle).Velocity.Linear = value.ToNumerics();
    internal bool IsKinematic(BodyHandle handle) => GetBodyReference(handle).Kinematic;
    internal bool GetIsTrigger(BodyHandle handle)
    {
        GetBodyReference(handle);
        return _triggers[handle] != 0;
    }
    internal void SetIsTrigger(BodyHandle handle, bool value)
    {
        GetBodyReference(handle);
        _triggers[handle] = value ? (byte)1 : (byte)0;
    }
    internal void SetPose(BodyHandle handle, Vector3 position, Quaternion rotation)
    {
        BodyReference body = GetBodyReference(handle);
        body.Pose = new RigidPose(position.ToNumerics(), rotation.ToNumerics());
        body.Awake = true;
    }
    internal CollisionFilter GetCollisionFilter(BodyHandle handle)
    {
        GetBodyReference(handle);
        return _collisionFilters[handle];
    }
    internal void SetCollisionFilter(BodyHandle handle, CollisionFilter value)
    {
        GetBodyReference(handle);
        _collisionFilters[handle] = value;
    }
    internal PhysicsMaterial GetMaterial(BodyHandle handle)
    {
        GetBodyReference(handle);
        return _materials[handle];
    }
    internal void SetMaterial(BodyHandle handle, PhysicsMaterial value)
    {
        GetBodyReference(handle);
        _materials[handle] = value.Validate();
    }

    public void SetCollisionFilter(StaticHandle handle, CollisionFilter value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!Simulation.Statics.StaticExists(handle)) throw new InvalidOperationException("The static collider no longer exists.");
        _collisionFilters[handle] = value;
    }

    public void SetMaterial(StaticHandle handle, PhysicsMaterial value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!Simulation.Statics.StaticExists(handle)) throw new InvalidOperationException("The static collider no longer exists.");
        _materials[handle] = value.Validate();
    }

    private BodyReference GetBodyReference(BodyHandle handle)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!Simulation.Bodies.BodyExists(handle)) throw new InvalidOperationException("The physics body no longer exists.");
        return Simulation.Bodies.GetBodyReference(handle);
    }

    private static void ValidateSize(Vector3 size)
    {
        ValidatePositive(size.X, nameof(size));
        ValidatePositive(size.Y, nameof(size));
        ValidatePositive(size.Z, nameof(size));
    }

    private static void ValidateMass(float mass) => ValidatePositive(mass, nameof(mass));
    private static void ValidatePositive(float value, string name)
    {
        if (!float.IsFinite(value) || value <= 0f) throw new ArgumentOutOfRangeException(name);
    }

    private static void DrawCollider(DebugRenderer renderer, RigidPose pose,
        ColliderEntry collider, Color color)
    {
        Microsoft.Xna.Framework.Matrix world = pose.Position.ToMonoGameMatrix(pose.Orientation);
        switch (collider.Kind)
        {
            case ColliderKind.Box:
                renderer.OrientedBox(world, collider.Dimensions, color);
                break;
            case ColliderKind.Sphere:
                renderer.WireSphere(pose.Position.ToMonoGame(), collider.Dimensions.X, color);
                break;
            case ColliderKind.Capsule:
                float halfLength = collider.Dimensions.Y * 0.5f;
                Vector3 top = Vector3.Transform(new Vector3(0f, halfLength, 0f), world);
                Vector3 bottom = Vector3.Transform(new Vector3(0f, -halfLength, 0f), world);
                renderer.WireSphere(top, collider.Dimensions.X, color, 16);
                renderer.WireSphere(bottom, collider.Dimensions.X, color, 16);
                Vector3 right = Vector3.TransformNormal(Vector3.Right * collider.Dimensions.X, world);
                Vector3 backward = Vector3.TransformNormal(Vector3.Backward * collider.Dimensions.X, world);
                renderer.Line(top + right, bottom + right, color);
                renderer.Line(top - right, bottom - right, color);
                renderer.Line(top + backward, bottom + backward, color);
                renderer.Line(top - backward, bottom - backward, color);
                break;
            case ColliderKind.Mesh:
                break;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        Simulation.Dispose();
        _collisionFilters.Dispose();
        _materials.Dispose();
        _triggers.Dispose();
        _threadDispatcher?.Dispose();
        _bufferPool.Clear();
        _bodyShapes.Clear();
        _staticShapes.Clear();
        _disposed = true;
    }

    private struct ClosestRayHitHandler : IRayHitHandler
    {
        public bool Hit;
        public float Distance;
        public NumericsVector3 Normal;
        public CollidableReference Collidable;

        public bool AllowTest(CollidableReference collidable) => true;
        public bool AllowTest(CollidableReference collidable, int childIndex) => true;

        public void OnRayHit(
            in RayData ray,
            ref float maximumT,
            float t,
            in NumericsVector3 normal,
            CollidableReference collidable,
            int childIndex)
        {
            Hit = true;
            Distance = t;
            Normal = normal;
            Collidable = collidable;
            maximumT = t;
        }
    }

    private struct ClosestSweepHitHandler : ISweepHitHandler
    {
        private readonly CollidableProperty<CollisionFilter> _filters;
        private readonly CollidableProperty<byte> _triggers;
        private readonly CollisionFilter _filter;
        private readonly bool _includeTriggers;
        private readonly int _ignoredBodyHandle;

        public ClosestSweepHitHandler(CollidableProperty<CollisionFilter> filters,
            CollidableProperty<byte> triggers, CollisionFilter filter, bool includeTriggers,
            int ignoredBodyHandle)
        {
            _filters = filters;
            _triggers = triggers;
            _filter = filter;
            _includeTriggers = includeTriggers;
            _ignoredBodyHandle = ignoredBodyHandle;
            Hit = false;
            Distance = 0f;
            Position = default;
            Normal = default;
            Collidable = default;
        }

        public bool Hit;
        public float Distance;
        public NumericsVector3 Position;
        public NumericsVector3 Normal;
        public CollidableReference Collidable;

        public bool AllowTest(CollidableReference collidable) =>
            (collidable.Mobility == CollidableMobility.Static ||
             collidable.RawHandleValue != _ignoredBodyHandle) &&
            (_includeTriggers || _triggers[collidable] == 0) &&
            _filter.Allows(_filters[collidable]);
        public bool AllowTest(CollidableReference collidable, int childIndex) => AllowTest(collidable);

        public void OnHit(ref float maximumT, float t, in NumericsVector3 hitLocation,
            in NumericsVector3 hitNormal, CollidableReference collidable)
        {
            Hit = true;
            Distance = t;
            Position = hitLocation;
            Normal = hitNormal;
            Collidable = collidable;
            maximumT = t;
        }

        public void OnHitAtZeroT(ref float maximumT, CollidableReference collidable)
        {
            Hit = true;
            Distance = 0f;
            Position = default;
            Normal = default;
            Collidable = collidable;
            maximumT = 0f;
        }
    }

    private enum ColliderKind { Box, Sphere, Capsule, Mesh }
    private readonly record struct ColliderEntry(TypedIndex Shape, ColliderKind Kind,
        Vector3 Dimensions, bool OwnsPooledResources = false);
}
