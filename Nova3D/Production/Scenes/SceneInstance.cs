namespace Nova3D.Production.Scenes;

/// <summary>Owns all runtime component values created for one scene plan.</summary>
public sealed class SceneInstance : IDisposable
{
    private readonly IReadOnlyList<SceneNodeInstance> _nodes;
    private readonly Dictionary<string, SceneNodeInstance> _nodesById;
    private readonly int _owningThreadId;
    private bool _disposed;

    internal SceneInstance(
        SceneLoadPlan plan,
        IEnumerable<SceneNodeInstance> nodes,
        int owningThreadId)
    {
        Plan = plan;
        _owningThreadId = owningThreadId;
        var copy = nodes.ToArray();
        _nodes = Array.AsReadOnly(copy);
        _nodesById = copy.ToDictionary(node => node.Id, StringComparer.Ordinal);
    }

    public SceneLoadPlan Plan { get; }
    public IReadOnlyList<SceneNodeInstance> Nodes => _nodes;
    public int OwningThreadId => _owningThreadId;
    public bool IsDisposed => _disposed;

    public bool TryGetNode(string id, out SceneNodeInstance node)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _nodesById.TryGetValue(id, out node!);
    }

    public SceneNodeInstance GetRequiredNode(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _nodesById.TryGetValue(id, out var node)
            ? node
            : throw new KeyNotFoundException($"Scene node '{id}' does not exist.");
    }

    /// <summary>Enumerates runtime values of one type in deterministic scene order.</summary>
    public IEnumerable<T> GetComponents<T>() where T : class
    {
        foreach (var node in _nodes)
        {
            foreach (var component in node.Components)
            {
                if (component.Value is T typed)
                    yield return typed;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        if (Environment.CurrentManagedThreadId != _owningThreadId)
        {
            throw new InvalidOperationException(
                $"Scene runtime resources must be destroyed on owning thread {_owningThreadId}; " +
                $"current thread is {Environment.CurrentManagedThreadId}.");
        }
        _disposed = true;

        List<Exception>? failures = null;
        for (var nodeIndex = _nodes.Count - 1; nodeIndex >= 0; nodeIndex--)
        {
            var components = _nodes[nodeIndex].Components;
            for (var componentIndex = components.Count - 1; componentIndex >= 0; componentIndex--)
            {
                var component = components[componentIndex];
                try
                {
                    component.Descriptor.Destroy(component.Value);
                }
                catch (Exception exception)
                {
                    failures ??= [];
                    failures.Add(exception);
                }
            }
        }

        if (failures is not null)
            throw new AggregateException("One or more scene runtime components failed to destroy.", failures);
    }
}
