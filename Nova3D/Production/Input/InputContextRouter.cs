using System.Collections.ObjectModel;
using Microsoft.Xna.Framework.Input;

namespace Nova3D.Production.Input;

/// <summary>Updates active action contexts from highest priority to lowest without UI dependencies.</summary>
public sealed class InputContextRouter
{
    private readonly Dictionary<string, InputContext> _byName = new(StringComparer.Ordinal);
    private readonly List<InputContext> _ordered = [];
    private readonly ReadOnlyCollection<InputContext> _view;
    private bool _hasUpdated;

    public InputContextRouter() => _view = _ordered.AsReadOnly();

    public IReadOnlyList<InputContext> Contexts => _view;

    public InputContext CreateContext(string name, int priority,
        bool blocksLowerContexts = false, bool active = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (_byName.ContainsKey(name))
            throw new InvalidOperationException($"Input context '{name}' is already registered.");
        var context = new InputContext(name, priority, blocksLowerContexts, active, _ordered.Count);
        _byName.Add(name, context);
        _ordered.Add(context);
        _ordered.Sort(static (left, right) =>
        {
            var priorityOrder = right.Priority.CompareTo(left.Priority);
            return priorityOrder != 0 ? priorityOrder : left.Order.CompareTo(right.Order);
        });
        return context;
    }

    public InputContext GetRequired(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _byName.TryGetValue(name, out var context)
            ? context : throw new KeyNotFoundException($"Input context '{name}' is not registered.");
    }

    /// <summary>Call once per frame after UI has updated its capture flags.</summary>
    public void Update(KeyboardState keyboard, MouseState mouse, GamePadState gamePad,
        InputDeviceCapture captured = InputDeviceCapture.None)
    {
        if ((captured & ~InputDeviceCapture.All) != 0)
            throw new ArgumentOutOfRangeException(nameof(captured));
        bool blocked = false;
        foreach (var context in _ordered)
        {
            bool receiving = context.IsActive && !blocked;
            if (receiving)
            {
                if (!context.IsReceivingInput && _hasUpdated)
                    context.Actions.Reset();
                context.Actions.Update(keyboard, mouse, gamePad, captured);
                if (context.BlocksLowerContexts) blocked = true;
            }
            else if (context.IsReceivingInput)
            {
                context.Actions.Reset();
            }
            context.IsReceivingInput = receiving;
        }
        _hasUpdated = true;
    }

    /// <summary>Reset all contexts after focus loss; held controls wait for a neutral sample.</summary>
    public void Reset()
    {
        foreach (var context in _ordered)
        {
            context.Actions.Reset();
            context.IsReceivingInput = false;
        }
        _hasUpdated = false;
    }
}
