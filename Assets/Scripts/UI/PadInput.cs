using Draftmaster.Controls;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// Reads a PadBindings button off whichever pad is current. The shortcut components used to reach into
// Gamepad.current themselves with a face-button name of their own choosing; going through here keeps the
// button they read and the button their prompt draws the same PadButton.
public static class PadInput
{
    public static ButtonControl Control(Gamepad gp, PadButton b)
    {
        if (gp == null) return null;
        switch (b)
        {
            case PadButton.South:         return gp.buttonSouth;
            case PadButton.East:          return gp.buttonEast;
            case PadButton.West:          return gp.buttonWest;
            case PadButton.North:         return gp.buttonNorth;
            case PadButton.LeftShoulder:  return gp.leftShoulder;
            case PadButton.RightShoulder: return gp.rightShoulder;
            case PadButton.LeftTrigger:   return gp.leftTrigger;
            case PadButton.RightTrigger:  return gp.rightTrigger;
            case PadButton.Start:         return gp.startButton;
            case PadButton.Select:        return gp.selectButton;
            case PadButton.DpadUp:        return gp.dpad.up;
            case PadButton.DpadDown:      return gp.dpad.down;
            case PadButton.DpadLeft:      return gp.dpad.left;
            case PadButton.DpadRight:     return gp.dpad.right;
            default:                      return null;
        }
    }

    public static bool WasPressed(PadButton b)
    {
        var c = Control(Gamepad.current, b);
        return c != null && c.wasPressedThisFrame;
    }

    public static bool IsHeld(PadButton b)
    {
        var c = Control(Gamepad.current, b);
        return c != null && c.isPressed;
    }

    // Menu steering: the d-pad or the left stick, -1 up / +1 down, on the frame it is pushed. The stick is
    // read through its up/down buttons, so it steps once per push rather than once per frame.
    public static int VerticalStep()
    {
        var gp = Gamepad.current;
        if (gp == null) return 0;
        if (gp.dpad.up.wasPressedThisFrame || gp.leftStick.up.wasPressedThisFrame) return -1;
        if (gp.dpad.down.wasPressedThisFrame || gp.leftStick.down.wasPressedThisFrame) return 1;
        return 0;
    }

    public static int HorizontalStep()
    {
        var gp = Gamepad.current;
        if (gp == null) return 0;
        if (gp.dpad.left.wasPressedThisFrame || gp.leftStick.left.wasPressedThisFrame) return -1;
        if (gp.dpad.right.wasPressedThisFrame || gp.leftStick.right.wasPressedThisFrame) return 1;
        return 0;
    }

    // The player is walking about rather than sat in the car. The crew chief's pit-wall body counts: it is a
    // walking body too, and the pad's walking shortcuts are the ones that apply to it.
    public static bool OnFoot => OnFootController.Current != null;

    static int _consumedFrame = -1;

    // A menu that closed itself on a pad press this frame calls this, so the same press cannot fall through to
    // a gameplay shortcut on the same button in a component that happens to update after it.
    public static void Consume() => _consumedFrame = Time.frameCount;

    // Something modal owns the pad's face buttons and d-pad right now, so a gameplay shortcut on the same
    // button must not answer as well.
    public static bool MenuOpen =>
        _consumedFrame == Time.frameCount ||
        RacePauseMenu.IsPaused ||
        PhoneUI.IsOpen ||
        WeekendScheduleUI.IsOpen ||
        WeekendModal.AnyOpen ||
        DialogueChoiceUI.IsOpen ||
        NPCInteractable.AnyConversationActive;

    // A gameplay shortcut on a context-shared button may answer: nothing modal is up.
    public static bool Pressed(PadButton b) => !MenuOpen && WasPressed(b);

    // ...and the player is sat in the car.
    public static bool PressedDriving(PadButton b) => !OnFoot && Pressed(b);

    // ...and the player is walking.
    public static bool PressedOnFoot(PadButton b) => OnFoot && Pressed(b);
}
