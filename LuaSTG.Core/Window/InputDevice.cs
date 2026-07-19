using Silk.NET.Input;
using Silk.NET.Windowing;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace LuaSTG.Core.Window;

public sealed class InputDevice
{
    private readonly IInputContext context;
    private readonly IKeyboard keyboard;
    private readonly IMouse mouse;
    private readonly IGamepad gamepad;

    public Key LastKeyPressed;

    public InputDevice(IWindow window)
    {
        context = window.CreateInput();
        keyboard = context.Keyboards[0];
        mouse = context.Mice[0];
        gamepad = context.Gamepads[0];

        keyboard.KeyDown += ChangeLastKeyPressed;
    }

    private void ChangeLastKeyPressed(IKeyboard arg1, Key arg2, int arg3)
    {
        LastKeyPressed = arg2;
    }

    public bool GetKeyState(int keyCode)
    {
        if (keyCode < 0 || keyCode > (int)Key.Menu)
            return false;

        return keyboard.IsKeyPressed((Key)keyCode);
    }

    public bool IsKeyDown(Key key)
    {
        return keyboard.IsKeyPressed(key);
    }

    public bool GetMouseState(int buttonCode)
    {
        if (buttonCode < 0 || buttonCode > (int)MouseButton.Button12)
            return false;

        return mouse.IsButtonPressed((MouseButton)buttonCode);
    }

    public bool IsMouseButtonDown(MouseButton button)
    {
        return mouse.IsButtonPressed(button);
    }

    //TODO: Fit with the viewport
    public Vector2 GetMousePosition()
    {
        return mouse.Position;
    }

    public float GetMouseWheelDelta()
    {
        if (mouse.ScrollWheels.Count == 0)
            return 0f;

        return mouse.ScrollWheels[0].Y;
    }
}
