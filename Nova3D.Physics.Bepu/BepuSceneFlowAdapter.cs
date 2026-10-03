using Nova3D.Production.Scenes;

namespace Nova3D.Physics.Bepu;

/// <summary>Steps a borrowed BEPU world only while its borrowed scene flow is playing.</summary>
public sealed class BepuSceneFlowAdapter
{
    private readonly SceneFlowController _flow;
    private readonly BepuPhysicsWorld _world;

    public BepuSceneFlowAdapter(SceneFlowController flow, BepuPhysicsWorld world)
    {
        _flow = flow ?? throw new ArgumentNullException(nameof(flow));
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public bool CanStep => _flow.State == SceneFlowState.Playing && _flow.ActiveScene is not null;

    /// <summary>Returns zero while paused, loading, in menu or showing a result.</summary>
    public int Update(float elapsedSeconds)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        return CanStep ? _world.Update(elapsedSeconds) : 0;
    }
}
