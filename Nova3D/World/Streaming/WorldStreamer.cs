using Microsoft.Xna.Framework;

namespace Nova3D.World.Streaming;

/// <summary>
/// Budgeted main-thread streaming for grid-aligned world resources. The load
/// callback may safely create MonoGame GPU resources.
/// </summary>
public sealed class WorldStreamer<T> : IDisposable where T : class
{
    private readonly WorldStreamingSettings _settings;
    private readonly Func<WorldCell, T> _load;
    private readonly Action<T> _unload;
    private readonly Func<WorldCell, bool>? _canLoad;
    private readonly Dictionary<WorldCell, T> _active = new();
    private readonly HashSet<WorldCell> _desired = new();
    private readonly List<(WorldCell Cell, float DistanceSquared)> _work = new();
    private bool _disposed;

    public WorldStreamer(WorldStreamingSettings settings, Func<WorldCell, T> load, Action<T> unload,
        Func<WorldCell, bool>? canLoad = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settings.Validate();
        _load = load ?? throw new ArgumentNullException(nameof(load));
        _unload = unload ?? throw new ArgumentNullException(nameof(unload));
        _canLoad = canLoad;
    }

    public int ActiveCount => _active.Count;
    public int DesiredCount => _desired.Count;
    public int PendingLoadCount { get; private set; }
    public int LoadsLastUpdate { get; private set; }
    public int UnloadsLastUpdate { get; private set; }
    public long TotalLoads { get; private set; }
    public long TotalUnloads { get; private set; }
    public IEnumerable<KeyValuePair<WorldCell, T>> ActiveCells => _active;

    public void Update(Vector3 focus) => Update(new Vector2(focus.X, focus.Z));

    public void Update(Vector2 focus)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        LoadsLastUpdate = 0;
        UnloadsLastUpdate = 0;
        BuildDesiredSet(focus);
        UnloadOutsideRetention(focus);
        LoadDesired(focus);
        PendingLoadCount = _desired.Count(cell => !_active.ContainsKey(cell));
    }

    public bool IsActive(WorldCell cell) => _active.ContainsKey(cell);
    public bool TryGet(WorldCell cell, out T? resource) => _active.TryGetValue(cell, out resource);

    public bool Reload(WorldCell cell)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_active.Remove(cell, out var previous)) return false;
        _unload(previous);
        _active.Add(cell, _load(cell) ?? throw new InvalidOperationException($"Loader returned null for cell {cell}."));
        TotalUnloads++;
        TotalLoads++;
        return true;
    }

    private void BuildDesiredSet(Vector2 focus)
    {
        _desired.Clear();
        var radius = _settings.LoadRadius;
        var minX = ToCell(focus.X - radius, _settings.Origin.X);
        var maxX = ToCell(focus.X + radius, _settings.Origin.X);
        var minZ = ToCell(focus.Y - radius, _settings.Origin.Y);
        var maxZ = ToCell(focus.Y + radius, _settings.Origin.Y);
        var radiusSquared = radius * radius;
        for (var z = minZ; z <= maxZ; z++)
        for (var x = minX; x <= maxX; x++)
        {
            var cell = new WorldCell(x, z);
            if ((_canLoad is null || _canLoad(cell)) &&
                Vector2.DistanceSquared(cell.Center(_settings.CellSize, _settings.Origin), focus) <= radiusSquared)
                _desired.Add(cell);
        }
    }

    private void UnloadOutsideRetention(Vector2 focus)
    {
        _work.Clear();
        var retainSquared = _settings.RetainRadius * _settings.RetainRadius;
        foreach (var cell in _active.Keys)
        {
            var distanceSquared = Vector2.DistanceSquared(cell.Center(_settings.CellSize, _settings.Origin), focus);
            if (distanceSquared > retainSquared) _work.Add((cell, distanceSquared));
        }
        _work.Sort(static (a, b) => b.DistanceSquared.CompareTo(a.DistanceSquared));
        var count = Math.Min(_settings.MaxUnloadsPerUpdate, _work.Count);
        for (var i = 0; i < count; i++)
        {
            var cell = _work[i].Cell;
            var resource = _active[cell];
            _active.Remove(cell);
            _unload(resource);
            UnloadsLastUpdate++;
            TotalUnloads++;
        }
    }

    private void LoadDesired(Vector2 focus)
    {
        _work.Clear();
        foreach (var cell in _desired)
            if (!_active.ContainsKey(cell))
                _work.Add((cell, Vector2.DistanceSquared(cell.Center(_settings.CellSize, _settings.Origin), focus)));
        _work.Sort(static (a, b) => a.DistanceSquared.CompareTo(b.DistanceSquared));
        var count = Math.Min(_settings.MaxLoadsPerUpdate, _work.Count);
        for (var i = 0; i < count; i++)
        {
            var cell = _work[i].Cell;
            var resource = _load(cell) ?? throw new InvalidOperationException($"Loader returned null for cell {cell}.");
            _active.Add(cell, resource);
            LoadsLastUpdate++;
            TotalLoads++;
        }
    }

    private int ToCell(float coordinate, float origin) =>
        (int)MathF.Floor((coordinate - origin) / _settings.CellSize);

    public void Dispose()
    {
        if (_disposed) return;
        foreach (var resource in _active.Values) _unload(resource);
        TotalUnloads += _active.Count;
        _active.Clear();
        _desired.Clear();
        _disposed = true;
    }
}
