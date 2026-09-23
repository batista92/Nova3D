using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;

namespace Nova3D.Physics.Bepu;

public struct NarrowPhaseCallbacks : INarrowPhaseCallbacks
{
    private readonly CollidableProperty<CollisionFilter> _filters;
    private readonly CollidableProperty<PhysicsMaterial> _materials;
    private readonly CollidableProperty<byte> _triggers;
    private readonly TriggerEventCollector _triggerEvents;

    internal NarrowPhaseCallbacks(CollidableProperty<CollisionFilter> filters,
        CollidableProperty<PhysicsMaterial> materials,
        CollidableProperty<byte> triggers,
        TriggerEventCollector triggerEvents)
    {
        _filters = filters;
        _materials = materials;
        _triggers = triggers;
        _triggerEvents = triggerEvents;
    }

    public void Initialize(Simulation simulation)
    {
        _filters.Initialize(simulation);
        _materials.Initialize(simulation);
        _triggers.Initialize(simulation);
    }

    public bool AllowContactGeneration(
        int workerIndex,
        CollidableReference a,
        CollidableReference b,
        ref float speculativeMargin) =>
        (a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic) &&
        _filters[a].Allows(_filters[b]);

    public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB) => true;

    public bool ConfigureContactManifold<TManifold>(
        int workerIndex,
        CollidablePair pair,
        ref TManifold manifold,
        out PairMaterialProperties pairMaterial)
        where TManifold : unmanaged, IContactManifold<TManifold>
    {
        pairMaterial = PhysicsMaterial.Combine(_materials[pair.A], _materials[pair.B]);
        bool aIsTrigger = _triggers[pair.A] != 0;
        bool bIsTrigger = _triggers[pair.B] != 0;
        if (aIsTrigger || bIsTrigger)
        {
            _triggerEvents.Report(pair, aIsTrigger, bIsTrigger);
            return false;
        }
        return true;
    }

    public bool ConfigureContactManifold(
        int workerIndex,
        CollidablePair pair,
        int childIndexA,
        int childIndexB,
        ref ConvexContactManifold manifold) => true;

    public void Dispose() { }
}
