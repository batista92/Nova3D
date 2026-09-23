using System.Collections.Concurrent;
using BepuPhysics.CollisionDetection;

namespace Nova3D.Physics.Bepu;

internal sealed class TriggerEventCollector
{
    private readonly ConcurrentDictionary<TriggerPair, byte> _current = new();
    private HashSet<TriggerPair> _previous = new();

    public void BeginUpdate() => _current.Clear();

    public void Report(in CollidablePair pair, bool aIsTrigger, bool bIsTrigger)
    {
        if (aIsTrigger && bIsTrigger)
        {
            var ordered = pair.A.Packed <= pair.B.Packed
                ? new TriggerPair(pair.A, pair.B)
                : new TriggerPair(pair.B, pair.A);
            _current.TryAdd(ordered, 0);
        }
        else if (aIsTrigger)
        {
            _current.TryAdd(new TriggerPair(pair.A, pair.B), 0);
        }
        else if (bIsTrigger)
        {
            _current.TryAdd(new TriggerPair(pair.B, pair.A), 0);
        }
    }

    public IReadOnlyList<TriggerEvent> CompleteUpdate()
    {
        if (_current.IsEmpty && _previous.Count == 0)
            return Array.Empty<TriggerEvent>();

        var events = new List<TriggerEvent>(_current.Count + _previous.Count);
        foreach (TriggerPair pair in _current.Keys)
            events.Add(new TriggerEvent(
                _previous.Contains(pair) ? TriggerEventType.Stay : TriggerEventType.Enter,
                pair.Trigger, pair.Other));
        foreach (TriggerPair pair in _previous)
            if (!_current.ContainsKey(pair))
                events.Add(new TriggerEvent(TriggerEventType.Exit, pair.Trigger, pair.Other));
        _previous = new HashSet<TriggerPair>(_current.Keys);
        return events;
    }
}
