using Microsoft.Xna.Framework;

namespace Nova3D.Production.Scenes;

/// <summary>A validated node in deterministic parent-before-child load order.</summary>
public sealed class ScenePlannedNode
{
    private readonly IReadOnlyList<PlannedComponent> _components;

    internal ScenePlannedNode(
        SceneNodeDocument document,
        Matrix localTransform,
        Matrix worldTransform,
        IEnumerable<PlannedComponent> components)
    {
        Document = document;
        LocalTransform = localTransform;
        WorldTransform = worldTransform;
        _components = Array.AsReadOnly(components.ToArray());
    }

    public SceneNodeDocument Document { get; }
    public string Id => Document.Id;
    public string? ParentId => Document.ParentId;
    public Matrix LocalTransform { get; }
    public Matrix WorldTransform { get; }

    internal IReadOnlyList<PlannedComponent> Components => _components;

    internal sealed record PlannedComponent(
        SceneComponentDocument Document,
        ISceneComponentDescriptor Descriptor);
}
