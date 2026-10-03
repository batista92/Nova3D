namespace Nova3D.Production.Scenes;

/// <summary>A named scene node with a local transform and component data.</summary>
public sealed class SceneNodeDocument
{
    private readonly IReadOnlyList<SceneComponentDocument> _components;

    public SceneNodeDocument(
        string id,
        string name,
        string? parentId,
        SceneTransformDocument transform,
        IEnumerable<SceneComponentDocument> components)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(transform);
        ArgumentNullException.ThrowIfNull(components);

        Id = id;
        Name = name;
        ParentId = parentId;
        Transform = transform;
        _components = Array.AsReadOnly(components.ToArray());
    }

    public string Id { get; }
    public string Name { get; }
    public string? ParentId { get; }
    public SceneTransformDocument Transform { get; }
    public IReadOnlyList<SceneComponentDocument> Components => _components;
}
