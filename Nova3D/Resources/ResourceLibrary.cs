using Microsoft.Xna.Framework.Content;

namespace Nova3D.Resources;

public sealed record ResourceRegistration(string Name, string AssetName, Type AssetType);

/// <summary>
/// Project-level names for ContentManager assets. Assets are never wrapped or
/// disposed here: ContentManager remains their cache and lifetime owner.
/// </summary>
public sealed class ResourceLibrary
{
    private sealed record Entry(string AssetName, Type AssetType, object Resource);

    private readonly ContentManager _content;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public ResourceLibrary(ContentManager content)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public int Count => _entries.Count;
    public int Generation { get; private set; }

    public IReadOnlyCollection<ResourceRegistration> Registrations => _entries
        .Select(pair => new ResourceRegistration(pair.Key, pair.Value.AssetName, pair.Value.AssetType))
        .ToArray();

    public T Load<T>(string name, string assetName)
    {
        ValidateName(name, nameof(name));
        ValidateName(assetName, nameof(assetName));

        if (_entries.TryGetValue(name, out var existing))
        {
            if (existing.AssetType == typeof(T) &&
                string.Equals(existing.AssetName, assetName, StringComparison.OrdinalIgnoreCase))
                return (T)existing.Resource;

            throw new InvalidOperationException(
                $"Resource '{name}' is already registered as {existing.AssetType.Name} from '{existing.AssetName}', " +
                $"not {typeof(T).Name} from '{assetName}'.");
        }

        var resource = _content.Load<T>(assetName);
        _entries.Add(name, new Entry(assetName, typeof(T), resource!));
        Generation++;
        return resource;
    }

    public T Get<T>(string name)
    {
        ValidateName(name, nameof(name));
        if (!_entries.TryGetValue(name, out var entry))
            throw new KeyNotFoundException($"Resource '{name}' is not registered.");
        if (entry.AssetType != typeof(T))
            throw new InvalidOperationException(
                $"Resource '{name}' is {entry.AssetType.Name}, not {typeof(T).Name}.");
        return (T)entry.Resource;
    }

    public bool TryGet<T>(string name, out T? resource)
    {
        if (_entries.TryGetValue(name, out var entry) && entry.AssetType == typeof(T))
        {
            resource = (T)entry.Resource;
            return true;
        }

        resource = default;
        return false;
    }

    public bool Remove<T>(string name)
    {
        if (!_entries.TryGetValue(name, out var entry) || entry.AssetType != typeof(T))
            return false;
        _entries.Remove(name);
        Generation++;
        return true;
    }

    public void Clear()
    {
        if (_entries.Count == 0) return;
        _entries.Clear();
        Generation++;
    }

    private static void ValidateName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Resource names cannot be empty.", parameterName);
    }
}
