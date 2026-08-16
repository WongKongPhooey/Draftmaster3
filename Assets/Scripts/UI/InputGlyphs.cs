using UnityEngine.InputSystem;

// One place that knows what a button is *called* on the device in the player's hands. Prompts elsewhere in
// the game hard-coded a keyboard label and a pad label side by side ("SPACE" / "X"), which is fine until a
// PlayStation pad is plugged in and the game asks for a button that isn't on it.
//
//     ControlHints.Show("fight", InputGlyphs.ShoveKeyboard, InputGlyphs.ShovePad, "Shove");
//     popupBody = $"Press {InputGlyphs.Shove} to push your opponent";
//
// Labels are read live, so picking a pad up mid-hint re-labels it on the next frame (ControlHintUI and
// TutorialPopupUI both repaint every frame for exactly this reason).
//
// Anything that isn't recognisably a PlayStation pad gets Xbox naming: that is what an unbranded PC pad
// almost always reports, and it's the labelling players expect on Windows.
public static class InputGlyphs
{
    // A gamepad is in use if one is connected at all — the same test ControlHintUI has always made.
    public static bool UsingGamepad => Gamepad.current != null;

    public static bool UsingPlayStationPad => IsPlayStation(Gamepad.current);

    // Pick the label for the device in use. `playstation` falls back to the Xbox label when not given.
    public static string Label(string keyboard, string xbox, string playstation = null)
    {
        if (!UsingGamepad) return keyboard;
        return UsingPlayStationPad && !string.IsNullOrEmpty(playstation) ? playstation : xbox;
    }

    // ---------------------------------------------------------------- named actions
    //
    // Keep these next to the code that reads the button, not spread across call sites: the label and the
    // binding have to move together or a prompt starts lying about the controls.

    // Paddock fight shove — DriverFight.ReadPlayerMoves reads Keyboard.spaceKey / Gamepad.buttonWest.
    public const string ShoveKeyboard = "SPACE";
    public static string ShovePad => UsingPlayStationPad ? "SQUARE" : "X";
    public static string Shove => UsingGamepad ? ShovePad : ShoveKeyboard;

    // Advance dialogue / dismiss a prompt — E everywhere on keyboard, south face button on a pad.
    public static string Confirm => Label("E", "A", "CROSS");

    static bool IsPlayStation(Gamepad gp)
    {
        if (gp == null) return false;

        // Layout name catches pads the input system recognises (DualShock4GamepadHID, DualSenseGamepadHID).
        string layout = gp.layout;
        if (!string.IsNullOrEmpty(layout) &&
            (layout.Contains("DualShock") || layout.Contains("DualSense"))) return true;

        // Anything else: go by what the device says it is. Cheap string work, but only on a device change —
        // Gamepad.current is stable, and this runs once per prompt repaint at most.
        var d = gp.description;
        string product = ((d.product ?? "") + " " + (d.manufacturer ?? "")).ToLowerInvariant();
        return product.Contains("dualshock") || product.Contains("dualsense") ||
               product.Contains("playstation") || product.Contains("sony");
    }
}
