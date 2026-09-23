using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// UnityEngine.KeyCode, read through the Input System.
//
// The project runs on the Input System alone — "Both" is unsupported on Android — and UnityEngine.Input
// throws the moment it is called under that setting. A handful of older panels expose their toggle as a
// serialized KeyCode, though, and those values live in binary scenes: retyping the fields to Key would reset
// every one of them to its default. So the fields stay KeyCode and are read through here instead.
//
// Covers the keyboard and the three mouse buttons; anything with no Input System equivalent (joystick
// buttons) reads as never pressed rather than throwing.
public static class LegacyKeys
{
    static readonly Dictionary<KeyCode, Key> _keys = new();

    // Pressed this frame, as Input.GetKeyDown.
    public static bool Down(KeyCode code)
    {
        var control = Control(code);
        return control != null && control.wasPressedThisFrame;
    }

    // Held, as Input.GetKey.
    public static bool Held(KeyCode code)
    {
        var control = Control(code);
        return control != null && control.isPressed;
    }

    static ButtonControl Control(KeyCode code)
    {
        if (code == KeyCode.None) return null;

        switch (code)
        {
            case KeyCode.Mouse0: return Mouse.current?.leftButton;
            case KeyCode.Mouse1: return Mouse.current?.rightButton;
            case KeyCode.Mouse2: return Mouse.current?.middleButton;
        }

        var keyboard = Keyboard.current;
        if (keyboard == null) return null;
        Key key = ToKey(code);
        return key == Key.None ? null : keyboard[key];
    }

    // KeyCode's name for a key where the Input System's differs; everything else shares its name.
    public static Key ToKey(KeyCode code)
    {
        if (_keys.TryGetValue(code, out Key cached)) return cached;

        Key key;
        // By name, not by offset: the Input System lists the digit row 1..9 then 0, so Digit0 + n is wrong.
        if (code >= KeyCode.Alpha0 && code <= KeyCode.Alpha9)
            key = System.Enum.Parse<Key>("Digit" + (code - KeyCode.Alpha0));
        else if (code >= KeyCode.Keypad0 && code <= KeyCode.Keypad9)
            key = System.Enum.Parse<Key>("Numpad" + (code - KeyCode.Keypad0));
        else
        {
            switch (code)
            {
                case KeyCode.Return: key = Key.Enter; break;
                case KeyCode.KeypadEnter: key = Key.NumpadEnter; break;
                case KeyCode.KeypadPlus: key = Key.NumpadPlus; break;
                case KeyCode.KeypadMinus: key = Key.NumpadMinus; break;
                case KeyCode.KeypadMultiply: key = Key.NumpadMultiply; break;
                case KeyCode.KeypadDivide: key = Key.NumpadDivide; break;
                case KeyCode.KeypadPeriod: key = Key.NumpadPeriod; break;
                case KeyCode.KeypadEquals: key = Key.NumpadEquals; break;
                case KeyCode.LeftControl: key = Key.LeftCtrl; break;
                case KeyCode.RightControl: key = Key.RightCtrl; break;
                case KeyCode.LeftCommand: key = Key.LeftMeta; break;
                case KeyCode.RightCommand: key = Key.RightMeta; break;
                case KeyCode.BackQuote: key = Key.Backquote; break;
                case KeyCode.Numlock: key = Key.NumLock; break;
                default:
                    if (!System.Enum.TryParse(code.ToString(), out key)) key = Key.None;
                    break;
            }
        }

        _keys[code] = key;
        return key;
    }
}
