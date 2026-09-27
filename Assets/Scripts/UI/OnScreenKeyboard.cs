using UnityEngine;

// The phone's own keyboard, for the few places the game takes typed text (the player's name in OPTIONS, a
// co-op join code). Those fields read typed characters from the Input System's keyboard text events, and a
// phone has no keyboard until something asks the OS for one — so without this a touch player could open a
// field and never put a letter in it.
//
// Hand-rolled fields keep their own buffer and rules; this only opens the OS keyboard with the field's current
// text, reports what it holds now, and says when the player pressed done, backed out, or tapped away.
public sealed class OnScreenKeyboard
{
    public enum Result { None, Typing, Done, Cancelled }

    TouchScreenKeyboard _kb;

    // Somewhere a finger is doing the typing: a phone or tablet, or a desktop with a touch screen that Unity can
    // raise a keyboard on.
    public static bool Wanted =>
        TouchScreenKeyboard.isSupported && (InputGlyphs.UsingTouch || Application.isMobilePlatform);

    public bool Open => _kb != null;
    public string Text => _kb != null ? _kb.text : null;

    public void Show(string text, int characterLimit = 0,
                     TouchScreenKeyboardType type = TouchScreenKeyboardType.Default)
    {
        if (!Wanted) return;
        _kb = TouchScreenKeyboard.Open(text ?? "", type, autocorrection: false, multiline: false,
                                       secure: false, alert: false);
        if (_kb != null && characterLimit > 0) _kb.characterLimit = characterLimit;
    }

    // Once a frame while open. Tapping away from the keyboard keeps what was typed, the same as done: that is
    // what a phone player expects from a text box.
    public Result Poll()
    {
        if (_kb == null) return Result.None;
        switch (_kb.status)
        {
            case TouchScreenKeyboard.Status.Done:
            case TouchScreenKeyboard.Status.LostFocus:
                _kb = null;
                return Result.Done;
            case TouchScreenKeyboard.Status.Canceled:
                _kb = null;
                return Result.Cancelled;
            default:
                return Result.Typing;
        }
    }

    public void Hide()
    {
        if (_kb == null) return;
        if (_kb.active) _kb.active = false;
        _kb = null;
    }
}
