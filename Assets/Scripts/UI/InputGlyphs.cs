using System.Collections.Generic;
using Draftmaster.Controls;
using UnityEngine;
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
// "Using a pad" means the pad is the last thing the player touched, not that one is plugged in. An idle pad
// on the desk (or a wheel that reports itself as one) used to flip every prompt to face buttons while the
// player was still on the keyboard — which is why the interact prompts had been pinned to E. The last-used
// device is watched by InputDeviceWatcher, once a frame.
//
// Anything that isn't recognisably a PlayStation pad gets Xbox naming: that is what an unbranded PC pad
// almost always reports, and it's the labelling players expect on Windows.
public static class InputGlyphs
{
    static bool _padActive;
    static bool _touchActive;
    static PadFamily _family;
    static Gamepad _lastPad;
    static uint _padPrevMask;

    // The pad is the device in the player's hands: touched more recently than the keyboard or mouse, and
    // still connected.
    public static bool UsingGamepad => _padActive && Gamepad.current != null;

    // The player is driving this with their thumbs: a phone or tablet (or the editor's Device Simulator
    // pretending to be one) with no pad in their hands. Prompts then show what to do with a finger rather
    // than a key nobody has.
    public static bool UsingTouch =>
        !UsingGamepad && UnityEngine.Device.Application.isMobilePlatform && Touchscreen.current != null;

    public static PadFamily Family => UsingGamepad ? _family : PadFamily.Xbox;

    public static bool UsingPlayStationPad => UsingGamepad && _family == PadFamily.PlayStation;

    // Bumped whenever the answer to "which icons?" changes, so world-space prompts can swap their sprite
    // without asking every frame.
    public static int Version { get; private set; }

    // Pick the label for the device in use. `playstation` falls back to the Xbox label when not given.
    public static string Label(string keyboard, string xbox, string playstation = null)
    {
        if (!UsingGamepad) return keyboard;
        return UsingPlayStationPad && !string.IsNullOrEmpty(playstation) ? playstation : xbox;
    }

    // The keyboard key's label, or the pad button's name when the pad is in use.
    public static string Label(string keyboard, PadButton pad)
        => UsingGamepad && pad != PadButton.None ? PadGlyphs.Name(pad, _family) : keyboard;

    // A button's name on the pad in use (Xbox naming when none is).
    public static string PadName(PadButton b) => PadGlyphs.Name(b, Family);

    // The canonical (Xbox-named) label for a button, for APIs that store a pad label and resolve it at draw
    // time — ControlHints parses it back and draws the icon for whichever pad is in use then.
    public static string Pad(PadButton b) => PadGlyphs.Name(b, PadFamily.Xbox);

    // A stored pad label ("LB", "RT / LT") re-named for the pad in use: "L1", "R2 / L2" on a PlayStation.
    // Free text that isn't a button comes back unchanged.
    public static string PadLabel(string label)
    {
        _parse.Clear();
        return PadGlyphs.TryParse(label, _parse) ? PadGlyphs.Name(_parse, Family) : label;
    }

    // ---------------------------------------------------------------- icons

    static readonly Dictionary<(PadButton, PadFamily), Sprite> _icons = new();
    static readonly List<PadButton> _parse = new();

    // The button's icon for the pad in use. Null when the art is missing — callers fall back to text.
    public static Sprite Icon(PadButton b) => Icon(b, Family);

    public static Sprite Icon(PadButton b, PadFamily f)
    {
        if (b == PadButton.None) return null;
        if (_icons.TryGetValue((b, f), out var cached)) return cached;
        string path = PadGlyphs.IconResource(b, f);
        var sprite = path != null ? Resources.Load<Sprite>(path) : null;
        if (sprite == null) Debug.LogWarning($"InputGlyphs: no pad icon at Resources/{path} — that prompt falls back to text.");
        _icons[(b, f)] = sprite;
        return sprite;
    }

    // Icons for a stored pad label, in order. False (and `into` left empty) when the label isn't all buttons
    // or any icon is missing, so the caller draws the label as text instead of half a row of pictures.
    public static bool TryIcons(string padLabel, List<Sprite> into)
    {
        into.Clear();
        _parse.Clear();
        if (!PadGlyphs.TryParse(padLabel, _parse)) return false;
        for (int i = 0; i < _parse.Count; i++)
        {
            var s = Icon(_parse[i]);
            if (s == null) { into.Clear(); return false; }
            into.Add(s);
        }
        return into.Count > 0;
    }

    // ---------------------------------------------------------------- named actions
    //
    // Keep these next to the code that reads the button, not spread across call sites: the label and the
    // binding have to move together or a prompt starts lying about the controls. The buttons themselves are
    // PadBindings'.

    // Paddock fight shove — DriverFight.ReadPlayerMoves reads Keyboard.spaceKey / PadBindings.Shove.
    public const string ShoveKeyboard = "SPACE";
    public static string ShovePad => PadName(PadBindings.Shove);
    public static string Shove => UsingGamepad ? ShovePad : ShoveKeyboard;

    // Advance dialogue / dismiss a prompt — E everywhere on keyboard, south face button on a pad.
    public static string Confirm => Label("E", PadBindings.Confirm);

    // Backing out of a menu — Esc on keyboard, east face button on a pad.
    public static string Back => Label("ESC", PadBindings.Back);

    // The phone — PhoneUI reads its toggleKey (P) / Gamepad.selectButton, the small button left of centre.
    public static string PhonePad => PadName(PadBindings.Phone);

    // ---------------------------------------------------------------- which device

    // Called once a frame by InputDeviceWatcher. A pad counts as used on a fresh press (or a stick/trigger
    // pushed past half way), the keyboard on any key, the mouse on a click or a scroll and a touchscreen on a
    // finger landing — edges only, so a pedal resting at full travel or a key held down cannot pin the answer.
    // Touching the screen is putting the pad down: on a phone it brings the on-screen driving controls back.
    internal static void Poll()
    {
        var gp = Gamepad.current;
        bool pad = _padActive;
        var family = _family;

        if (gp == null)
        {
            pad = false;
            _lastPad = null;
        }
        else
        {
            uint mask = PadMask(gp);
            if (gp != _lastPad)
            {
                // A different pad: learn what it is, and take what it is already holding as its rest state
                // rather than as a press.
                _lastPad = gp;
                _padPrevMask = mask;
                family = IsPlayStation(gp) ? PadFamily.PlayStation : PadFamily.Xbox;
            }
            bool padUsed = (mask & ~_padPrevMask) != 0;
            _padPrevMask = mask;

            bool kbUsed = KeyboardMouseOrTouchUsed();
            if (padUsed && !kbUsed) pad = true;
            else if (kbUsed && !padUsed) pad = false;
        }

        // A touchscreen arriving or going away changes which art every prompt wants just as much as a pad
        // does — in the editor that is switching the Device Simulator on, on a phone it is the only state
        // there is. Computed after _padActive is settled, because thumbs only win when no pad is in use.
        bool touch = !(pad && gp != null) &&
                     UnityEngine.Device.Application.isMobilePlatform && Touchscreen.current != null;

        if (pad != _padActive || family != _family || touch != _touchActive)
        {
            _padActive = pad;
            _family = family;
            _touchActive = touch;
            Version++;
        }
    }

    static uint PadMask(Gamepad gp)
    {
        uint m = 0;
        int i = 0;
        void Bit(bool on) { if (on) m |= 1u << i; i++; }

        Bit(gp.buttonSouth.isPressed);
        Bit(gp.buttonEast.isPressed);
        Bit(gp.buttonWest.isPressed);
        Bit(gp.buttonNorth.isPressed);
        Bit(gp.leftShoulder.isPressed);
        Bit(gp.rightShoulder.isPressed);
        Bit(gp.startButton.isPressed);
        Bit(gp.selectButton.isPressed);
        Bit(gp.leftStickButton.isPressed);
        Bit(gp.rightStickButton.isPressed);
        Bit(gp.dpad.up.isPressed);
        Bit(gp.dpad.down.isPressed);
        Bit(gp.dpad.left.isPressed);
        Bit(gp.dpad.right.isPressed);
        Bit(gp.leftTrigger.ReadValue() > 0.5f);
        Bit(gp.rightTrigger.ReadValue() > 0.5f);
        Bit(gp.leftStick.ReadValue().sqrMagnitude > 0.25f);
        Bit(gp.rightStick.ReadValue().sqrMagnitude > 0.25f);
        return m;
    }

    static bool KeyboardMouseOrTouchUsed()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
        var screen = Touchscreen.current;
        if (screen != null && screen.primaryTouch.press.wasPressedThisFrame) return true;
        var mouse = Mouse.current;
        if (mouse == null) return false;
        return mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame ||
               mouse.middleButton.wasPressedThisFrame || mouse.scroll.ReadValue().sqrMagnitude > 0.01f;
    }

    static bool IsPlayStation(Gamepad gp)
    {
        if (gp == null) return false;

        // Layout name catches pads the input system recognises (DualShock4GamepadHID, DualSenseGamepadHID).
        string layout = gp.layout;
        if (!string.IsNullOrEmpty(layout) &&
            (layout.Contains("DualShock") || layout.Contains("DualSense"))) return true;

        // Anything else: go by what the device says it is. Cheap string work, and only on a device change.
        var d = gp.description;
        string product = ((d.product ?? "") + " " + (d.manufacturer ?? "")).ToLowerInvariant();
        return product.Contains("dualshock") || product.Contains("dualsense") ||
               product.Contains("playstation") || product.Contains("sony");
    }
}
