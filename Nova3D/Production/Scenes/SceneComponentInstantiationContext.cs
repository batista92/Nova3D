using Microsoft.Xna.Framework;

namespace Nova3D.Production.Scenes;

/// <summary>Immutable node/component data supplied to a runtime component factory.</summary>
public sealed class SceneComponentInstantiationContext
{
    internal SceneComponentInstantiationContext(
        SceneLoadPlan plan,
        ScenePlannedNode node,
        SceneComponentDocument component,
        CancellationToken cancellationToken)
    {
        Plan = plan;
        Node = node;
        Component = component;
        CancellationToken = cancellationToken;
    }

    public SceneLoadPlan Plan { get; }
    public ScenePlannedNode Node { get; }
    public SceneComponentDocument Component { get; }
    public CancellationToken CancellationToken { get; }
    public Matrix LocalTransform => Node.LocalTransform;
    public Matrix WorldTransform => Node.WorldTransform;
}
