namespace Nova3D.Production.Scenes;

/// <summary>Explicit setup-time registry for scene component descriptors.</summary>
public sealed class SceneComponentRegistry
{
    private readonly Dictionary<string, ISceneComponentDescriptor> _descriptors =
        new(StringComparer.Ordinal);

    public int Count => _descriptors.Count;
    public IReadOnlyCollection<string> Types => Array.AsReadOnly(_descriptors.Keys.ToArray());

    public void Register(ISceneComponentDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (!SceneDocumentValidator.IsValidComponentType(descriptor.Type))
        {
            throw new ArgumentException(
                "Component type must contain 3-128 characters and be a lower-case dot-separated identifier.",
                nameof(descriptor));
        }

        if (!_descriptors.TryAdd(descriptor.Type, descriptor))
            throw new InvalidOperationException($"Scene component type '{descriptor.Type}' is already registered.");
    }

    public bool TryGet(string type, out ISceneComponentDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(type);
        return _descriptors.TryGetValue(type, out descriptor!);
    }

    public ISceneComponentDescriptor GetRequired(string type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return _descriptors.TryGetValue(type, out var descriptor)
            ? descriptor
            : throw new KeyNotFoundException($"Scene component type '{type}' is not registered.");
    }
}
