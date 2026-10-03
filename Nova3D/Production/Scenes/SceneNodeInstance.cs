using Microsoft.Xna.Framework;

namespace Nova3D.Production.Scenes;

/// <summary>A runtime scene node and its created component values.</summary>
public sealed class SceneNodeInstance
{
    private readonly IReadOnlyList<SceneComponentInstance> _components;

    internal SceneNodeInstance(ScenePlannedNode plannedNode, IEnumerable<SceneComponentInstance> components)
    {
        Document = plannedNode.Document;
        LocalTransform = plannedNode.LocalTransform;
        WorldTransform = plannedNode.WorldTransform;
        _components = Array.AsReadOnly(components.ToArray());
    }

    public SceneNodeDocument Document { get; }
    public string Id => Document.Id;
    public string? ParentId => Document.ParentId;
    public Matrix LocalTransform { get; }
    public Matrix WorldTransform { get; }
    public IReadOnlyList<SceneComponentInstance> Components => _components;

    public bool TryGetComponent<T>(string componentId, out T value) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentId);
        foreach (var component in _components)
        {
            if (string.Equals(component.Id, componentId, StringComparison.Ordinal) && component.Value is T typed)
            {
                value = typed;
                return true;
            }
        }

        value = null!;
        return false;
    }
}
