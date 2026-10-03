using Nova3D.Production.Input;

namespace Nova3D.UI.Gum;

/// <summary>Converts Gum's current device capture into core input-routing flags.</summary>
public static class GumInputCapture
{
    public static InputDeviceCapture Read(GumUiHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        var captured = InputDeviceCapture.None;
        if (host.CapturesKeyboard) captured |= InputDeviceCapture.Keyboard;
        if (host.CapturesMouse) captured |= InputDeviceCapture.Mouse;
        if (host.CapturesGamePad) captured |= InputDeviceCapture.GamePad;
        return captured;
    }
}
