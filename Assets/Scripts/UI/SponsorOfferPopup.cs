using UnityEngine;
using UnityEngine.InputSystem;

// The paper, put in front of you.
//
// A rep who has walked across the paddock to poach a driver does not haggle — they hand over finished
// terms and wait. So that conversation ends in a term sheet rather than another list of spoken lines:
// the brand, how many races, what it pays a race, and the one target they want hit before it runs out,
// with SIGN IT and NOT NOW underneath.
//
// IMGUI over PixelGUI for the same reason DialogueChoiceUI is: the on-foot scenes draw through the 3D URP
// renderer, where a screen-space Canvas needs wiring and a font asset, while OnGUI needs neither. Same
// input conventions as the choice panel too (W/S or stick to move, E / Space / Enter / south to confirm,
// mouse click to answer directly), so it is the same panel to the hands.
//
// One popup at a time, opened from code:
//
//     SponsorOfferPopup.Show(this, "Voltage Energy", "Term sheet", terms, note, signed => { ... });
public class SponsorOfferPopup : MonoBehaviour
{
    [Tooltip("Panel width in pixels, at 1x. PixelGUI steps it up in whole numbers with the screen.")]
    public float panelWidth = 560f;
    [Tooltip("Height of one answer row, pixels.")]
    public float rowHeight = 34f;
    [Tooltip("Stick tilt that counts as a nudge up/down the answers.")]
    [Range(0.2f, 0.95f)] public float stickThreshold = 0.5f;

    static SponsorOfferPopup _instance;

    // True while a term sheet is on screen waiting for an answer. Callers treat themselves as still
    // talking while this is up, so the player stays planted in front of it.
    public static bool IsOpen => _instance != null && _instance._open;
    public static MonoBehaviour Owner => _instance != null ? _instance._owner : null;

    static readonly string[] kAnswers = { "Sign it", "Not now" };

    bool _open;
    string _brand = "";
    string _subtitle = "";
    string[] _terms = new string[0];
    string _footnote = "";
    System.Action<bool> _answered;
    MonoBehaviour _owner;
    int _index;
    int _clicked = -1;
    float _openedAt;               // unscaled time the offer went up; earlier fingers are not answers
    bool _confirmHeldPrev, _upHeldPrev, _downHeldPrev;
    Vector2 _lastMousePos;
    float _mouseMovedAt = -99f;

    // Put a term sheet up. `terms` is one line per clause — the popup does no formatting of its own, so
    // the money and the wording stay with whoever knows the deal.
    public static void Show(MonoBehaviour owner, string brand, string subtitle, string[] terms,
                            string footnote, System.Action<bool> answered)
    {
        var ui = Instance();
        if (ui == null) { answered?.Invoke(false); return; }

        ui._owner = owner;
        ui._brand = brand ?? "";
        ui._subtitle = subtitle ?? "";
        ui._terms = terms ?? new string[0];
        ui._footnote = footnote ?? "";
        ui._answered = answered;
        ui._index = 0;
        ui._clicked = -1;
        ui._open = true;
        ui._openedAt = Time.unscaledTime;
        // Whatever key ended the conversation is probably still held — start latched so it cannot
        // instantly answer the question it has only just opened.
        ui._confirmHeldPrev = true;
        ui._upHeldPrev = ui._downHeldPrev = true;
        ui._lastMousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        ui._mouseMovedAt = -99f;
    }

    // Close without answering (the owner went away). The callback is not invoked.
    public static void Cancel()
    {
        if (_instance == null) return;
        _instance._open = false;
        _instance._answered = null;
        _instance._owner = null;
    }

    static SponsorOfferPopup Instance()
    {
        if (_instance != null) return _instance;
        var go = new GameObject("SponsorOfferPopup");
        _instance = go.AddComponent<SponsorOfferPopup>();
        return _instance;
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    void OnDestroy() { if (_instance == this) _instance = null; }

    void Update()
    {
        if (!_open) return;

        // The conversation that produced the offer has gone (NPC destroyed, player back in the car) —
        // drop the paper rather than leaving a panel nobody can answer.
        if (_owner == null || !_owner.isActiveAndEnabled) { Cancel(); return; }

        // The pause menu owns the screen and the same keys.
        if (RacePauseMenu.IsPaused) return;

        TrackMouse();

        int move = ReadMoveStep();
        if (move != 0) _index = Mathf.Clamp(_index + move, 0, kAnswers.Length - 1);

        int pick = -1;
        if (_clicked >= 0) { pick = _clicked; _clicked = -1; }
        else if (ReadConfirmPressed()) pick = _index;
        if (pick < 0) return;

        // Close BEFORE dispatching: the callback speaks the next line, and it must not find a panel still
        // claiming to be open.
        var callback = _answered;
        _open = false;
        _answered = null;
        _owner = null;
        callback?.Invoke(pick == 0);
    }

    // Hover only steals the selection while the pointer is actually moving — in-game OnGUI never receives
    // MouseMove events, so the pointer is tracked here instead.
    void TrackMouse()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 pos = mouse.position.ReadValue();
        if ((pos - _lastMousePos).sqrMagnitude > 4f) _mouseMovedAt = Time.unscaledTime;
        _lastMousePos = pos;
    }

    bool MouseSteering => Time.unscaledTime - _mouseMovedAt < 1.5f;

    int ReadMoveStep()
    {
        bool up = false, down = false;
        var kb = Keyboard.current;
        if (kb != null)
        {
            up |= kb.wKey.isPressed || kb.upArrowKey.isPressed;
            down |= kb.sKey.isPressed || kb.downArrowKey.isPressed;
        }
        var gp = Gamepad.current;
        if (gp != null)
        {
            float y = gp.leftStick.ReadValue().y;
            up |= gp.dpad.up.isPressed || y > stickThreshold;
            down |= gp.dpad.down.isPressed || y < -stickThreshold;
        }

        int step = 0;
        if (up && !_upHeldPrev) step = -1;
        else if (down && !_downHeldPrev) step = 1;
        _upHeldPrev = up;
        _downHeldPrev = down;
        return step;
    }

    bool ReadConfirmPressed()
    {
        bool held = false;
        var kb = Keyboard.current;
        if (kb != null) held |= kb.eKey.isPressed || kb.spaceKey.isPressed || kb.enterKey.isPressed || kb.numpadEnterKey.isPressed;
        var gp = Gamepad.current;
        if (gp != null) held |= gp.buttonSouth.isPressed;

        bool pressed = held && !_confirmHeldPrev;
        _confirmHeldPrev = held;
        return pressed;
    }

    void OnGUI()
    {
        if (!_open || RacePauseMenu.IsPaused) return;

        var theme = PixelGUI.Theme;
        float pad = PixelGUI.Px(20f);
        float w = Mathf.Min(PixelGUI.Px(panelWidth), Screen.width - PixelGUI.Px(40f));
        float x = (Screen.width - w) * 0.5f;
        float row = PixelGUI.Px(rowHeight);
        float inner = w - pad * 2f;

        float titleH = PixelGUI.Heading.CalcHeight(new GUIContent(_brand), inner);
        float subH = string.IsNullOrEmpty(_subtitle) ? 0f
                   : PixelGUI.LabelDim.CalcHeight(new GUIContent(_subtitle), inner);
        float termsH = 0f;
        for (int i = 0; i < _terms.Length; i++)
            termsH += PixelGUI.Data.CalcHeight(new GUIContent(_terms[i]), inner) + PixelGUI.Px(3f);
        float noteH = string.IsNullOrEmpty(_footnote) ? 0f
                    : PixelGUI.LabelDim.CalcHeight(new GUIContent(_footnote), inner) + PixelGUI.Px(6f);
        float footerH = PixelGUI.Px(24f);

        float h = PixelGUI.Px(14f) + titleH + subH + PixelGUI.Px(12f) + termsH + noteH
                + PixelGUI.Px(10f) + kAnswers.Length * row + footerH + PixelGUI.Px(14f);

        // Centred, and dimmed behind: this is a decision, not a caption, so the paddock goes quiet for it.
        float y = Mathf.Max(PixelGUI.Px(20f), (Screen.height - h) * 0.5f);
        PixelGUI.Scrim(0.55f);
        GUI.Box(new Rect(x, y, w, h), GUIContent.none, PixelGUI.Window);

        float cy = y + PixelGUI.Px(14f);
        GUI.Label(new Rect(x + pad, cy, inner, titleH), _brand, PixelGUI.Heading);
        cy += titleH;
        if (subH > 0f)
        {
            GUI.Label(new Rect(x + pad, cy, inner, subH), _subtitle, PixelGUI.LabelDim);
            cy += subH;
        }
        cy += PixelGUI.Px(6f);
        PixelGUI.Rule(x + pad, cy, inner);
        cy += PixelGUI.Px(6f);

        for (int i = 0; i < _terms.Length; i++)
        {
            float th = PixelGUI.Data.CalcHeight(new GUIContent(_terms[i]), inner);
            GUI.Label(new Rect(x + pad, cy, inner, th), _terms[i], PixelGUI.Data);
            cy += th + PixelGUI.Px(3f);
        }

        if (noteH > 0f)
        {
            cy += PixelGUI.Px(6f);
            GUI.Label(new Rect(x + pad, cy, inner, noteH), _footnote, PixelGUI.LabelDim);
            cy += noteH - PixelGUI.Px(6f);
        }

        cy += PixelGUI.Px(10f);

        // Rows indented to leave a gutter for the selection cursor, the same JRPG convention the dialogue
        // choice panel uses.
        float gutter = PixelGUI.Px(20f);
        float inset = PixelGUI.Px(14f);
        for (int i = 0; i < kAnswers.Length; i++)
        {
            var r = new Rect(x + inset, cy, w - inset * 2f, row);
            bool selected = i == _index;

            if (MouseSteering && _index != i && r.Contains(Event.current.mousePosition))
            {
                _index = i;
                selected = true;
            }

            if (selected)
            {
                var band = theme != null ? theme.gold : new Color(1f, 0.83f, 0.42f);
                band.a = 0.16f;
                PixelGUI.Fill(r, band);
                PixelGUI.DrawCursor(r, PixelGUI.Px(12f));
            }

            var textRect = new Rect(r.x + gutter, r.y, r.width - gutter, r.height);
            // Only a finger put down on the offer answers it — not the one still tapping through the rep's pitch.
            if (TouchTaps.Button(textRect, kAnswers[i], selected ? PixelGUI.RowSelected : PixelGUI.Row) &&
                (!TouchTaps.Driven || TouchTaps.TapDownAt > _openedAt))
                _clicked = i;
            cy += row;
        }

        string keys = InputGlyphs.UsingGamepad
            ? $"Left stick / D-pad to choose    {InputGlyphs.Confirm} to answer"
            : "W / S to choose    E to answer";
        GUI.Label(new Rect(x + pad, cy + PixelGUI.Px(2f), inner, footerH), keys, PixelGUI.Footer);
    }
}
