namespace Nova3D.Production.Scenes.Assets;

/// <summary>Graphics-thread-affine, reference-counted cache for scene assets.</summary>
public sealed class SceneAssetCache<T> : IDisposable where T : class
{
    private sealed class Entry
    {
        public Entry(T value) => Value = value;
        public T Value { get; }
        public int References { get; set; }
    }

    private readonly Dictionary<string, Entry> _entries = new(PathComparer());
    private readonly Func<string, CancellationToken, T> _load;
    private readonly Action<T> _destroy;
    private readonly int _owningThreadId = Environment.CurrentManagedThreadId;
    private readonly bool _keepUnusedAssets;
    private bool _disposed;

    public SceneAssetCache(
        Func<string, CancellationToken, T> load,
        Action<T> destroy,
        bool keepUnusedAssets = true)
    {
        _load = load ?? throw new ArgumentNullException(nameof(load));
        _destroy = destroy ?? throw new ArgumentNullException(nameof(destroy));
        _keepUnusedAssets = keepUnusedAssets;
    }

    public int Count => _entries.Count;
    public int OwningThreadId => _owningThreadId;

    public SceneAssetLease<T> Acquire(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        VerifyAvailable();
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = Path.GetFullPath(path);

        if (_entries.TryGetValue(fullPath, out var existing))
        {
            existing.References++;
            return new SceneAssetLease<T>(this, fullPath, existing.Value);
        }

        var value = _load(fullPath, cancellationToken) ??
            throw new InvalidOperationException($"Scene asset loader returned null for '{fullPath}'.");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = new Entry(value) { References = 1 };
            _entries.Add(fullPath, entry);
            return new SceneAssetLease<T>(this, fullPath, value);
        }
        catch (Exception acquisitionFailure)
        {
            try
            {
                _destroy(value);
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException(
                    $"Scene asset '{fullPath}' could not be acquired and cleanup also failed.",
                    acquisitionFailure,
                    cleanupFailure);
            }
            throw;
        }
    }

    internal void Release(string path)
    {
        VerifyThread();
        if (_disposed || !_entries.TryGetValue(path, out var entry))
            return;
        if (entry.References <= 0)
            throw new InvalidOperationException($"Scene asset '{path}' has no active reference to release.");

        entry.References--;
        if (entry.References == 0 && !_keepUnusedAssets)
        {
            _entries.Remove(path);
            _destroy(entry.Value);
        }
    }

    internal void VerifyLeaseAccess(string path)
    {
        VerifyAvailable();
        if (!_entries.ContainsKey(path))
            throw new ObjectDisposedException(nameof(SceneAssetLease<T>));
    }

    internal bool IsDisposed => _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;
        VerifyThread();
        _disposed = true;

        List<Exception>? failures = null;
        foreach (var entry in _entries.Values)
        {
            try
            {
                _destroy(entry.Value);
            }
            catch (Exception exception)
            {
                failures ??= [];
                failures.Add(exception);
            }
        }
        _entries.Clear();

        if (failures is not null)
            throw new AggregateException("One or more cached scene assets failed to destroy.", failures);
    }

    private void VerifyAvailable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        VerifyThread();
    }

    private void VerifyThread()
    {
        if (Environment.CurrentManagedThreadId != _owningThreadId)
        {
            throw new InvalidOperationException(
                $"Scene asset cache work must execute on owning thread {_owningThreadId}; " +
                $"current thread is {Environment.CurrentManagedThreadId}.");
        }
    }

    private static StringComparer PathComparer() =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
