using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Nova3D.Production.Input;

public enum InputActionKind { Digital, Axis1D, Axis2D }
public enum InputMouseButton { Left, Middle, Right, X1, X2 }
public enum InputGamePadAxis { LeftX, LeftY, RightX, RightY, LeftTrigger, RightTrigger }
public enum InputGamePadStick { Left, Right }
public enum InputMouseAxis { X, Y, Wheel }

internal enum InputBindingKind
{
    Key, MouseButton, GamePadButton, KeyboardAxis, GamePadAxis,
    MouseAxis, KeyboardVector, GamePadStick, MouseDelta
}

/// <summary>Immutable, device-specific source for one action. Create bindings during setup.</summary>
public readonly struct InputBinding
{
    private InputBinding(InputBindingKind source, InputActionKind actionKind,
        Keys negative = default, Keys positive = default, Keys down = default, Keys up = default,
        InputMouseButton mouseButton = default, Buttons gamePadButton = default,
        InputGamePadAxis gamePadAxis = default, InputGamePadStick gamePadStick = default,
        InputMouseAxis mouseAxis = default, float deadzone = 0f, float scale = 1f, bool invert = false)
    {
        Source = source;
        ActionKind = actionKind;
        Negative = negative;
        Positive = positive;
        Down = down;
        Up = up;
        MouseButton = mouseButton;
        GamePadButton = gamePadButton;
        GamePadAxis = gamePadAxis;
        GamePadStick = gamePadStick;
        MouseAxis = mouseAxis;
        Deadzone = deadzone;
        Scale = scale;
        Invert = invert;
    }

    internal InputBindingKind Source { get; }
    public InputActionKind ActionKind { get; }
    internal Keys Negative { get; }
    internal Keys Positive { get; }
    internal Keys Down { get; }
    internal Keys Up { get; }
    internal InputMouseButton MouseButton { get; }
    internal Buttons GamePadButton { get; }
    internal InputGamePadAxis GamePadAxis { get; }
    internal InputGamePadStick GamePadStick { get; }
    internal InputMouseAxis MouseAxis { get; }
    public float Deadzone { get; }
    public float Scale { get; }
    public bool Invert { get; }
    internal bool IsConfigured => Scale > 0f;
    internal InputDeviceCapture Device => Source switch
    {
        InputBindingKind.Key or InputBindingKind.KeyboardAxis or InputBindingKind.KeyboardVector =>
            InputDeviceCapture.Keyboard,
        InputBindingKind.MouseButton or InputBindingKind.MouseAxis or InputBindingKind.MouseDelta =>
            InputDeviceCapture.Mouse,
        _ => InputDeviceCapture.GamePad
    };

    internal bool Overlaps(InputBinding other)
    {
        if (Device != other.Device) return false;
        if (Device == InputDeviceCapture.Keyboard)
        {
            if (Positive != Keys.None && other.UsesKey(Positive)) return true;
            if (Negative != Keys.None && other.UsesKey(Negative)) return true;
            if (Down != Keys.None && other.UsesKey(Down)) return true;
            return Up != Keys.None && other.UsesKey(Up);
        }
        if (Device == InputDeviceCapture.Mouse)
        {
            if (Source == InputBindingKind.MouseButton || other.Source == InputBindingKind.MouseButton)
                return Source == InputBindingKind.MouseButton &&
                       other.Source == InputBindingKind.MouseButton && MouseButton == other.MouseButton;
            if (Source == InputBindingKind.MouseDelta || other.Source == InputBindingKind.MouseDelta)
            {
                var axis = Source == InputBindingKind.MouseAxis ? MouseAxis : other.MouseAxis;
                return (Source == InputBindingKind.MouseDelta && other.Source == InputBindingKind.MouseDelta) ||
                       axis is InputMouseAxis.X or InputMouseAxis.Y;
            }
            return MouseAxis == other.MouseAxis;
        }
        if (Source == InputBindingKind.GamePadButton || other.Source == InputBindingKind.GamePadButton)
            return Source == InputBindingKind.GamePadButton &&
                   other.Source == InputBindingKind.GamePadButton &&
                   (GamePadButton & other.GamePadButton) != 0;
        if (Source == InputBindingKind.GamePadStick || other.Source == InputBindingKind.GamePadStick)
        {
            if (Source == InputBindingKind.GamePadStick && other.Source == InputBindingKind.GamePadStick)
                return GamePadStick == other.GamePadStick;
            var stick = Source == InputBindingKind.GamePadStick ? GamePadStick : other.GamePadStick;
            var axis = Source == InputBindingKind.GamePadAxis ? GamePadAxis : other.GamePadAxis;
            return stick == InputGamePadStick.Left
                ? axis is InputGamePadAxis.LeftX or InputGamePadAxis.LeftY
                : axis is InputGamePadAxis.RightX or InputGamePadAxis.RightY;
        }
        return GamePadAxis == other.GamePadAxis;
    }

    private bool UsesKey(Keys key) => Source switch
    {
        InputBindingKind.Key => Positive == key,
        InputBindingKind.KeyboardAxis => Positive == key || Negative == key,
        InputBindingKind.KeyboardVector => Positive == key || Negative == key ||
                                           Down == key || Up == key,
        _ => false
    };

    public static InputBinding Key(Keys key) =>
        new(InputBindingKind.Key, InputActionKind.Digital, positive: key);
    public static InputBinding MouseButtonInput(InputMouseButton button) =>
        new(InputBindingKind.MouseButton, InputActionKind.Digital, mouseButton: button);
    public static InputBinding GamePadButtonInput(Buttons button) =>
        new(InputBindingKind.GamePadButton, InputActionKind.Digital, gamePadButton: button);
    public static InputBinding KeyboardAxis(Keys negative, Keys positive,
        float scale = 1f, bool invert = false) =>
        new(InputBindingKind.KeyboardAxis, InputActionKind.Axis1D,
            negative: negative, positive: positive, scale: ValidScale(scale), invert: invert);
    public static InputBinding GamePadAxisInput(InputGamePadAxis axis,
        float deadzone = 0.15f, float scale = 1f, bool invert = false) =>
        new(InputBindingKind.GamePadAxis, InputActionKind.Axis1D,
            gamePadAxis: axis, deadzone: ValidDeadzone(deadzone), scale: ValidScale(scale), invert: invert);
    public static InputBinding MouseAxisInput(InputMouseAxis axis,
        float scale = 1f, bool invert = false) =>
        new(InputBindingKind.MouseAxis, InputActionKind.Axis1D,
            mouseAxis: axis, scale: ValidScale(scale), invert: invert);
    public static InputBinding KeyboardVector(Keys left, Keys right, Keys down, Keys up,
        float scale = 1f, bool invert = false) =>
        new(InputBindingKind.KeyboardVector, InputActionKind.Axis2D,
            negative: left, positive: right, down: down, up: up,
            scale: ValidScale(scale), invert: invert);
    public static InputBinding GamePadStickInput(InputGamePadStick stick,
        float deadzone = 0.15f, float scale = 1f, bool invert = false) =>
        new(InputBindingKind.GamePadStick, InputActionKind.Axis2D,
            gamePadStick: stick, deadzone: ValidDeadzone(deadzone), scale: ValidScale(scale), invert: invert);
    public static InputBinding MouseDelta(float scale = 1f, bool invert = false) =>
        new(InputBindingKind.MouseDelta, InputActionKind.Axis2D,
            scale: ValidScale(scale), invert: invert);

    private static float ValidScale(float value) =>
        float.IsFinite(value) && value > 0f ? value : throw new ArgumentOutOfRangeException(nameof(value));

    private static float ValidDeadzone(float value) =>
        float.IsFinite(value) && value >= 0f && value < 1f
            ? value : throw new ArgumentOutOfRangeException(nameof(value));

    internal Vector2 Read(KeyboardState keyboard, MouseState mouse, GamePadState gamePad,
        int mouseX, int mouseY, int wheel)
    {
        float scalar = Source switch
        {
            InputBindingKind.Key => keyboard.IsKeyDown(Positive) ? 1f : 0f,
            InputBindingKind.MouseButton => ReadMouseButton(mouse) ? 1f : 0f,
            InputBindingKind.GamePadButton => gamePad.IsConnected && gamePad.IsButtonDown(GamePadButton) ? 1f : 0f,
            InputBindingKind.KeyboardAxis => (keyboard.IsKeyDown(Positive) ? 1f : 0f) -
                                             (keyboard.IsKeyDown(Negative) ? 1f : 0f),
            InputBindingKind.GamePadAxis => gamePad.IsConnected ? ReadGamePadAxis(gamePad) : 0f,
            InputBindingKind.MouseAxis => MouseAxis switch
            {
                InputMouseAxis.X => mouseX,
                InputMouseAxis.Y => mouseY,
                _ => wheel
            },
            _ => 0f
        };
        if (ActionKind != InputActionKind.Axis2D)
        {
            if (MathF.Abs(scalar) <= Deadzone) scalar = 0f;
            return new Vector2(scalar * Scale * (Invert ? -1f : 1f), 0f);
        }

        Vector2 vector = Source switch
        {
            InputBindingKind.KeyboardVector => new Vector2(
                (keyboard.IsKeyDown(Positive) ? 1f : 0f) - (keyboard.IsKeyDown(Negative) ? 1f : 0f),
                (keyboard.IsKeyDown(Up) ? 1f : 0f) - (keyboard.IsKeyDown(Down) ? 1f : 0f)),
            InputBindingKind.GamePadStick when gamePad.IsConnected =>
                GamePadStick == InputGamePadStick.Left ? gamePad.ThumbSticks.Left : gamePad.ThumbSticks.Right,
            InputBindingKind.MouseDelta => new Vector2(mouseX, mouseY),
            _ => Vector2.Zero
        };
        if (vector.LengthSquared() <= Deadzone * Deadzone) vector = Vector2.Zero;
        return vector * (Scale * (Invert ? -1f : 1f));
    }

    private bool ReadMouseButton(MouseState mouse) => MouseButton switch
    {
        InputMouseButton.Left => mouse.LeftButton == ButtonState.Pressed,
        InputMouseButton.Middle => mouse.MiddleButton == ButtonState.Pressed,
        InputMouseButton.Right => mouse.RightButton == ButtonState.Pressed,
        InputMouseButton.X1 => mouse.XButton1 == ButtonState.Pressed,
        _ => mouse.XButton2 == ButtonState.Pressed
    };

    private float ReadGamePadAxis(GamePadState gamePad) => GamePadAxis switch
    {
        InputGamePadAxis.LeftX => gamePad.ThumbSticks.Left.X,
        InputGamePadAxis.LeftY => gamePad.ThumbSticks.Left.Y,
        InputGamePadAxis.RightX => gamePad.ThumbSticks.Right.X,
        InputGamePadAxis.RightY => gamePad.ThumbSticks.Right.Y,
        InputGamePadAxis.LeftTrigger => gamePad.Triggers.Left,
        _ => gamePad.Triggers.Right
    };
}
