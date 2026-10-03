namespace Nova3D.Production.Input;

/// <summary>Devices currently owned by UI or another input consumer.</summary>
[Flags]
public enum InputDeviceCapture
{
    None = 0,
    Keyboard = 1,
    Mouse = 2,
    GamePad = 4,
    All = Keyboard | Mouse | GamePad
}
