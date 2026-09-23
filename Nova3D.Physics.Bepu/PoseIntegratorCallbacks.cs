using System.Numerics;
using BepuPhysics;
using BepuUtilities;

namespace Nova3D.Physics.Bepu;

public struct PoseIntegratorCallbacks : IPoseIntegratorCallbacks
{
    private Vector3Wide _gravityDt;

    public PoseIntegratorCallbacks(Vector3 gravity) => Gravity = gravity;

    public Vector3 Gravity { get; set; }
    public AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;
    public bool AllowSubstepsForUnconstrainedBodies => false;
    public bool IntegrateVelocityForKinematics => false;

    public void Initialize(Simulation simulation) { }

    public void PrepareForIntegration(float dt) =>
        Vector3Wide.Broadcast(Gravity * dt, out _gravityDt);

    public void IntegrateVelocity(
        Vector<int> bodyIndices,
        Vector3Wide position,
        QuaternionWide orientation,
        BodyInertiaWide localInertia,
        Vector<int> integrationMask,
        int workerIndex,
        Vector<float> dt,
        ref BodyVelocityWide velocity) =>
        velocity.Linear += _gravityDt;
}
