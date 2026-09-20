using System.Diagnostics;

namespace Nova3D.Production.Profiling;

public sealed class FrameProfiler
{
    private readonly Dictionary<string, double> _current = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _last = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _smoothed = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, double> LastMilliseconds => _last;
    public IReadOnlyDictionary<string, double> SmoothedMilliseconds => _smoothed;

    public void BeginFrame()
    {
        _last.Clear();
        foreach (var pair in _current)
        {
            _last[pair.Key] = pair.Value;
            _smoothed[pair.Key] = _smoothed.TryGetValue(pair.Key, out var previous)
                ? previous * 0.9 + pair.Value * 0.1
                : pair.Value;
        }
        _current.Clear();
    }

    public Scope Measure(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Scope(this, name, Stopwatch.GetTimestamp());
    }

    public double GetSmoothedMilliseconds(string name) =>
        _smoothed.TryGetValue(name, out var value) ? value : 0.0;

    private void Record(string name, long startTimestamp)
    {
        var milliseconds = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        _current[name] = _current.TryGetValue(name, out var existing)
            ? existing + milliseconds
            : milliseconds;
    }

    public readonly struct Scope : IDisposable
    {
        private readonly FrameProfiler _owner;
        private readonly string _name;
        private readonly long _startTimestamp;

        internal Scope(FrameProfiler owner, string name, long startTimestamp)
        {
            _owner = owner;
            _name = name;
            _startTimestamp = startTimestamp;
        }

        public void Dispose() => _owner.Record(_name, _startTimestamp);
    }
}
