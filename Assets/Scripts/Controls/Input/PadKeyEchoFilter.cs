using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Draftmaster.Controls
{
    // Drops the keyboard presses Android makes out of pad buttons (see PadKeyEcho), so a pad on a phone
    // behaves as it does on a PC. InputDeviceWatcher installs it on Android only.
    //
    // It listens inside the input system's update, before an event reaches its device: a keyboard event
    // marked handled here never changes the keyboard's state, so nothing downstream — wasPressedThisFrame,
    // an action, the UI — sees the key go down at all.
    public static class PadKeyEchoFilter
    {
        static readonly PadKeyEcho _echo = new PadKeyEcho();
        static readonly Action<InputEventPtr, InputDevice> _hook = OnEvent;

        public static bool Installed { get; private set; }

        public static void Install()
        {
            if (Installed) return;
            InputSystem.onEvent += _hook;
            Installed = true;
        }

        public static void Uninstall()
        {
            if (!Installed) return;
            InputSystem.onEvent -= _hook;
            Installed = false;
            _echo.Reset();
        }

        static void OnEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;

            if (device is Gamepad || device is Joystick)
            {
                if (eventPtr.HasButtonPress()) _echo.PadPressed(eventPtr.time);
            }
            else if (device is Keyboard)
            {
                if (_echo.IsEcho(eventPtr.time) && eventPtr.HasButtonPress()) eventPtr.handled = true;
            }
        }
    }
}
