using Nova3D.Production.Logging;

namespace Nova3D.Production.Assets;

/// <summary>Versioned file assets reloaded synchronously on the owning thread.</summary>
public sealed class FileAssetManager : IDisposable
{
    private interface IRegistration : IDisposable
    {
        string Name { get; }
        bool Poll(DateTime utcNow);
    }

    private sealed class Registration<T> : IRegistration where T : class
    {
        private readonly Func<string, T> _load;
        private readonly Action<T>? _dispose;
        private readonly ILogger? _logger;
        private DateTime _lastWriteUtc;
        private DateTime _nextRetryUtc;

        public Registration(AssetHandle<T> handle, Func<string, T> load,
            Action<T>? dispose, ILogger? logger)
        {
            Handle = handle;
            _load = load;
            _dispose = dispose;
            _logger = logger;
            _lastWriteUtc = File.GetLastWriteTimeUtc(handle.SourcePath);
        }

        public AssetHandle<T> Handle { get; }
        public string Name => Handle.Name;

        public bool Poll(DateTime utcNow)
        {
            if (utcNow < _nextRetryUtc || !File.Exists(Handle.SourcePath)) return false;
            var writeTime = File.GetLastWriteTimeUtc(Handle.SourcePath);
            if (writeTime <= _lastWriteUtc) return false;
            try
            {
                var replacement = _load(Handle.SourcePath);
                var previous = Handle.Replace(replacement);
                _dispose?.Invoke(previous);
                _lastWriteUtc = writeTime;
                _logger?.Log(LogLevel.Information, "Assets",
                    $"Reloaded '{Name}' to version {Handle.Version}.");
                return true;
            }
            catch (Exception exception)
            {
                _nextRetryUtc = utcNow.AddMilliseconds(500);
                _logger?.Log(LogLevel.Error, "Assets",
                    $"Failed to reload '{Name}'; keeping version {Handle.Version}.", exception);
                return false;
            }
        }

        public void Dispose() => _dispose?.Invoke(Handle.Value);
    }

    private readonly Dictionary<string, IRegistration> _registrations =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger? _logger;
    private DateTime _nextPollUtc;
    private bool _disposed;

    public FileAssetManager(ILogger? logger = null)
    {
        _logger = logger;
    }

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(250);
    public int Count => _registrations.Count;
    public int ReloadsLastPoll { get; private set; }

    public AssetHandle<T> Load<T>(string name, string sourcePath, Func<string, T> loader,
        Action<T>? dispose = null) where T : class
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(loader);
        if (_registrations.ContainsKey(name))
            throw new InvalidOperationException($"A file asset named '{name}' is already registered.");
        var fullPath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Asset source was not found.", fullPath);
        var handle = new AssetHandle<T>(name, fullPath, loader(fullPath));
        _registrations.Add(name, new Registration<T>(handle, loader, dispose, _logger));
        _logger?.Log(LogLevel.Information, "Assets", $"Loaded '{name}' from '{fullPath}'.");
        return handle;
    }

    public void Update()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var now = DateTime.UtcNow;
        if (now < _nextPollUtc) return;
        _nextPollUtc = now + PollInterval;
        ReloadsLastPoll = 0;
        foreach (var registration in _registrations.Values)
            if (registration.Poll(now)) ReloadsLastPoll++;
    }

    public void Dispose()
    {
        if (_disposed) return;
        foreach (var registration in _registrations.Values) registration.Dispose();
        _registrations.Clear();
        _disposed = true;
    }
}
