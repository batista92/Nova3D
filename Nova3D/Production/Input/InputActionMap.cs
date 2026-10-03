using Microsoft.Xna.Framework.Input;
using System.Collections.ObjectModel;

namespace Nova3D.Production.Input;

/// <summary>Evaluates named actions once per frame from native MonoGame input states.</summary>
public sealed class InputActionMap
{
    private readonly Dictionary<string, InputAction> _actions = new(StringComparer.Ordinal);
    private readonly List<InputAction> _ordered = [];
    private readonly ReadOnlyCollection<InputAction> _actionView;
    private MouseState _previousMouse;
    private bool _hasPreviousMouse;
    private InputDeviceCapture _previousCapture;
    private Dictionary<string, InputBinding[]>? _defaults;

    public InputActionMap() => _actionView = _ordered.AsReadOnly();

    public IReadOnlyList<InputAction> Actions => _actionView;

    public InputAction Add(string name, InputActionKind kind, params InputBinding[] bindings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(bindings);
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        var action = new InputAction(name, kind);
        foreach (var binding in bindings)
            action.AddBinding(binding);
        if (!_actions.TryAdd(name, action))
            throw new InvalidOperationException($"Input action '{name}' is already registered.");
        _ordered.Add(action);
        return action;
    }

    public InputAction GetRequired(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _actions.TryGetValue(name, out var action)
            ? action : throw new KeyNotFoundException($"Input action '{name}' is not registered.");
    }

    public bool TryGet(string name, out InputAction action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _actions.TryGetValue(name, out action!);
    }

    /// <summary>Replaces an action's bindings without replacing the action reference.</summary>
    public void ReplaceBindings(string name, params InputBinding[] bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        GetRequired(name).ReplaceBindings(bindings);
    }

    /// <summary>Records the setup bindings for later RestoreDefaults calls.</summary>
    public void CaptureDefaults()
    {
        _defaults = new Dictionary<string, InputBinding[]>(_actions.Count, StringComparer.Ordinal);
        foreach (var action in _ordered)
            _defaults.Add(action.Name, action.Bindings.ToArray());
    }

    public void RestoreDefaults()
    {
        if (_defaults is null)
            throw new InvalidOperationException("CaptureDefaults must be called before RestoreDefaults.");
        foreach (var action in _ordered)
        {
            if (!_defaults.ContainsKey(action.Name))
                throw new InvalidOperationException($"No defaults captured for action '{action.Name}'.");
        }
        foreach (var action in _ordered)
        {
            var bindings = _defaults[action.Name];
            action.ReplaceBindings(bindings);
        }
    }

    /// <summary>Reports shared physical controls across different actions; does not impose a conflict policy.</summary>
    public IReadOnlyList<InputBindingConflict> FindConflicts()
    {
        var conflicts = new List<InputBindingConflict>();
        for (int leftAction = 0; leftAction < _ordered.Count; leftAction++)
        for (int rightAction = leftAction + 1; rightAction < _ordered.Count; rightAction++)
        {
            var left = _ordered[leftAction];
            var right = _ordered[rightAction];
            for (int leftBinding = 0; leftBinding < left.Bindings.Count; leftBinding++)
            for (int rightBinding = 0; rightBinding < right.Bindings.Count; rightBinding++)
            {
                if (left.Bindings[leftBinding].Overlaps(right.Bindings[rightBinding]))
                    conflicts.Add(new InputBindingConflict(left.Name, leftBinding,
                        right.Name, rightBinding));
            }
        }
        return conflicts;
    }

    /// <summary>Call once after sampling devices; first mouse sample has zero delta.</summary>
    public void Update(KeyboardState keyboard, MouseState mouse, GamePadState gamePad)
        => Update(keyboard, mouse, gamePad, InputDeviceCapture.None);

    /// <summary>Captured devices contribute no value; held inputs cannot pass through on release.</summary>
    public void Update(KeyboardState keyboard, MouseState mouse, GamePadState gamePad,
        InputDeviceCapture captured)
    {
        if ((captured & ~InputDeviceCapture.All) != 0)
            throw new ArgumentOutOfRangeException(nameof(captured));
        var changed = captured ^ _previousCapture;
        if (changed != InputDeviceCapture.None)
        {
            foreach (var action in _ordered)
            {
                if (action.UsesDevice(changed)) action.Reset();
            }
        }
        _previousCapture = captured;
        int mouseX = _hasPreviousMouse ? mouse.X - _previousMouse.X : 0;
        int mouseY = _hasPreviousMouse ? mouse.Y - _previousMouse.Y : 0;
        int wheel = _hasPreviousMouse ? mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue : 0;
        _previousMouse = mouse;
        _hasPreviousMouse = true;
        if ((captured & InputDeviceCapture.Mouse) != 0)
        {
            mouse = default;
            mouseX = 0;
            mouseY = 0;
            wheel = 0;
        }
        if ((captured & InputDeviceCapture.Keyboard) != 0) keyboard = default;
        if ((captured & InputDeviceCapture.GamePad) != 0) gamePad = default;
        foreach (var action in _ordered)
            action.Update(keyboard, mouse, gamePad, mouseX, mouseY, wheel);
    }

    /// <summary>Clears held actions and the mouse delta baseline, e.g. after focus loss.</summary>
    public void Reset()
    {
        _hasPreviousMouse = false;
        _previousCapture = InputDeviceCapture.None;
        foreach (var action in _ordered)
            action.Reset();
    }
}
