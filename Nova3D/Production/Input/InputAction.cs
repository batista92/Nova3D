using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Collections.ObjectModel;

namespace Nova3D.Production.Input;

/// <summary>Current value and frame transitions of one named gameplay intention.</summary>
public sealed class InputAction
{
    private readonly List<InputBinding> _bindings = [];
    private readonly ReadOnlyCollection<InputBinding> _bindingView;
    private bool _suppressUntilNeutral;

    internal InputAction(string name, InputActionKind kind)
    {
        Name = name;
        Kind = kind;
        _bindingView = _bindings.AsReadOnly();
    }

    public string Name { get; }
    public InputActionKind Kind { get; }
    public Vector2 Value { get; private set; }
    public float Value1D => Value.X;
    public bool Down { get; private set; }
    public bool Pressed { get; private set; }
    public bool Released { get; private set; }
    public IReadOnlyList<InputBinding> Bindings => _bindingView;

    public void AddBinding(InputBinding binding)
    {
        if (binding.ActionKind != Kind || !binding.IsConfigured)
            throw new ArgumentException("Binding kind must match the action and be configured.", nameof(binding));
        _bindings.Add(binding);
    }

    internal void ReplaceBindings(IReadOnlyList<InputBinding> bindings)
    {
        foreach (var binding in bindings)
        {
            if (binding.ActionKind != Kind || !binding.IsConfigured)
                throw new ArgumentException("Binding kind must match the action and be configured.", nameof(bindings));
        }
        _bindings.Clear();
        foreach (var binding in bindings) _bindings.Add(binding);
        Reset();
    }

    internal void Update(KeyboardState keyboard, MouseState mouse, GamePadState gamePad,
        int mouseX, int mouseY, int wheel)
    {
        Vector2 value = Vector2.Zero;
        foreach (var binding in _bindings)
            value += binding.Read(keyboard, mouse, gamePad, mouseX, mouseY, wheel);

        bool down = Kind == InputActionKind.Digital
            ? value.X > 0f
            : value.LengthSquared() > 0.000001f;
        if (_suppressUntilNeutral)
        {
            if (!down) _suppressUntilNeutral = false;
            down = false;
            value = Vector2.Zero;
        }
        if (Kind == InputActionKind.Digital)
            value = down ? Vector2.UnitX : Vector2.Zero;

        Pressed = down && !Down;
        Released = !down && Down;
        Down = down;
        Value = value;
    }

    internal void Reset()
    {
        Value = Vector2.Zero;
        Down = false;
        Pressed = false;
        Released = false;
        _suppressUntilNeutral = true;
    }

    internal bool UsesDevice(InputDeviceCapture device)
    {
        foreach (var binding in _bindings)
        {
            if ((binding.Device & device) != 0)
                return true;
        }
        return false;
    }
}
