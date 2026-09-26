using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Nova3D.UI.Gum;

/// <summary>Frame-edge navigation actions shared by keyboard and MonoGame gamepads.</summary>
public sealed class GumUiNavigationInput
{
    private bool _backWasDown;

    public bool BackPressed { get; private set; }
    public PlayerIndex? BackPlayer { get; private set; }

    internal void Reset(bool keyboardEnabled, bool gamePadsEnabled)
    {
        _backWasDown = IsBackDown(keyboardEnabled, gamePadsEnabled, out _);
        BackPressed = false;
        BackPlayer = null;
    }

    internal void Update(bool keyboardEnabled, bool gamePadsEnabled)
    {
        bool backDown = IsBackDown(keyboardEnabled, gamePadsEnabled, out PlayerIndex? player);
        BackPressed = backDown && !_backWasDown;
        BackPlayer = BackPressed ? player : null;
        _backWasDown = backDown;
    }

    private static bool IsBackDown(bool keyboardEnabled, bool gamePadsEnabled, out PlayerIndex? player)
    {
        if (keyboardEnabled && Keyboard.GetState().IsKeyDown(Keys.Escape))
        {
            player = null;
            return true;
        }

        for (int i = 0; gamePadsEnabled && i < 4; i++)
        {
            var index = (PlayerIndex)i;
            GamePadState state = GamePad.GetState(index);
            if (state.IsButtonDown(Buttons.B) || state.IsButtonDown(Buttons.Back))
            {
                player = index;
                return true;
            }
        }

        player = null;
        return false;
    }
}
