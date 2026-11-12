using UnityEngine;
using UnityEngine.InputSystem;

// A modal "pick one line" panel for branching on-foot dialogue: a question header and a list of answers the
// player chooses between with W/S (or the stick / d-pad / mouse) and confirms with E / Space / gamepad A.
//
// Drawn with IMGUI for the same reason SpawnIntroUI and the debug panels are — the spline scenes use the 3D
// URP renderer, where a world/screen-space Canvas needs wiring and a font asset, while OnGUI just works with
// no prefab and no scene setup. Its look is the Draftmaster3_UI_Unity design's (Choice_PC; see OnGUI), drawn with
// PixelGUI's fonts and fills, so it sits on the same pixel grid as the rest of the kit.
//
// Only one panel is open at a time. The caller owns the conversation:
//
//     DialogueChoiceUI.Open(this, "So what do you want to be?", options, i => Picked(i));
//
// The pick is dispatched from Update (never from OnGUI), so the callback can start coroutines and speech
// bubbles safely. Callers should treat themselves as still talking while IsOpen is true, so the player stays
// put — see CareerPathNPC.IsTalking.
public class DialogueChoiceUI : MonoBehaviour
{
    [Tooltip("Stick tilt that counts as a nudge up/down the list.")]
    [Range(0.2f, 0.95f)] public float stickThreshold = 0.5f;

    static DialogueChoiceUI _instance;

    // True while a choice is on screen and waiting for an answer.
    public static bool IsOpen => _instance != null && _instance._open;
    // Who opened the live choice (null when nothing is open).
    public static MonoBehaviour Owner => _instance != null ? _instance._owner : null;

    bool _open;
    string _question;
    string[] _options;
    System.Action<int> _picked;
    MonoBehaviour _owner;          // conversation that opened it; the panel closes itself if this dies
    int _index;
    int _clicked = -1;             // set by OnGUI on a mouse click, dispatched in Update
    float _openedAt;               // unscaled time the question went up; earlier fingers are not answers
    bool _confirmHeldPrev, _upHeldPrev, _downHeldPrev;
    Vector2 _lastMousePos;
    float _mouseMovedAt = -99f;    // in-game OnGUI gets no MouseMove events, so track the pointer ourselves

    // Open a choice. Replaces any panel already up (the last caller wins), which can only happen if two
    // conversations race — the player can talk to one NPC at a time.
    public static void Open(MonoBehaviour owner, string question, string[] options, System.Action<int> onPicked)
    {
        if (options == null || options.Length == 0)
        {
            Debug.LogWarning("DialogueChoiceUI.Open: no options — nothing to choose.");
            onPicked?.Invoke(-1);
            return;
        }

        var ui = Instance();
        if (ui == null) { onPicked?.Invoke(-1); return; }

        ui._owner = owner;
        ui._question = question;
        ui._options = options;
        ui._picked = onPicked;
        ui._index = 0;
        ui._clicked = -1;
        ui._open = true;
        ui._openedAt = Time.unscaledTime;
        // Whatever key opened this panel is probably still held — start latched so it can't instantly confirm.
        ui._confirmHeldPrev = true;
        ui._upHeldPrev = ui._downHeldPrev = true;
        // Base the pointer tracking on where the mouse actually is, so an untouched mouse doesn't read as a
        // move on the first frame and grab the selection.
        ui._lastMousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        ui._mouseMovedAt = -99f;
    }

    // Close without answering (the owner went away mid-question). The callback is not invoked.
    public static void Cancel()
    {
        if (_instance == null) return;
        _instance._open = false;
        _instance._picked = null;
        _instance._owner = null;
    }

    static DialogueChoiceUI Instance()
    {
        if (_instance != null) return _instance;
        var go = new GameObject("DialogueChoiceUI");
        _instance = go.AddComponent<DialogueChoiceUI>();
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

        // The conversation that asked the question has gone (NPC destroyed, scene beat cancelled, player
        // switched to the car) — drop the question rather than leaving a panel nobody can answer.
        if (_owner == null || !_owner.isActiveAndEnabled) { Cancel(); return; }

        // The pause menu owns the screen and the same keys — the question waits until it closes.
        if (RacePauseMenu.IsPaused) return;

        TrackMouse();

        int move = ReadMoveStep();
        if (move != 0) _index = Mathf.Clamp(_index + move, 0, _options.Length - 1);

        int pick = -1;
        if (_clicked >= 0) { pick = _clicked; _clicked = -1; }
        else if (ReadConfirmPressed()) pick = _index;
        if (pick < 0) return;

        // Close BEFORE dispatching: the callback usually opens the next line of dialogue, and it must not
        // find a panel still claiming to be open.
        var callback = _picked;
        _open = false;
        _picked = null;
        _owner = null;
        callback?.Invoke(pick);
    }

    // Hover only steals the selection while the pointer is actually being moved — an idle mouse sat over a
    // row would otherwise fight every keyboard press, and in-game OnGUI never receives MouseMove events.
    void TrackMouse()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 pos = mouse.position.ReadValue();
        if ((pos - _lastMousePos).sqrMagnitude > 4f) _mouseMovedAt = Time.unscaledTime;
        _lastMousePos = pos;
    }

    bool MouseSteering => Time.unscaledTime - _mouseMovedAt < 1.5f;

    // One step up (-1) / down (+1) per press, from keyboard, d-pad or a stick flick.
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

    // Same keys that advance dialogue (E / Space / gamepad south), plus Enter for the mouse-and-keyboard hand.
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

    // ------------------------------------------------------------------ drawing

    // Laid out as the Draftmaster3_UI_Unity design draws it (Choice_PC, 2026-11-11), in its kit pixels through
    // PixelGUI.Px: a deep-navy panel with a 3px cream border and a 5px drop shadow, across the screen 20px in
    // from each side and 18px under the top letterbox bar. Inside, 12px in: the speaker in tracked gold
    // Silkscreen, the question in cream monogram, then the answers — the chosen one on a lighter band boxed in
    // 2px of gold with a blinking ▶ in front of it, the rest in sand — and the key legend under them
    // (W S CHOOSE, E ANSWER) as keycaps, in the keys of the device in hand.
    const float SideMargin = 20f, BelowBar = 18f, Inset = 12f, Border = 3f, Shadow = 5f;
    const float RowH = 26f, RowPitch = 30f, RowTextX = 26f, CursorX = 10f;
    const float LegendGap = 10f, LegendH = 10f;
    const float BlinkPeriod = 0.9f;

    GUIStyle _speakerStyle, _questionStyle, _answerStyle, _legendStyle, _keyStyle;
    int _stylesAt;

    void OnGUI()
    {
        if (!_open || _options == null || RacePauseMenu.IsPaused) return;
        EnsureStyles();
        float px = PixelGUI.Px(1f);

        float x = PixelGUI.Px(SideMargin);
        float w = Mathf.Max(PixelGUI.Px(200f), Screen.width - x * 2f);
        float inner = w - PixelGUI.Px(Inset) * 2f;
        float textW = inner - PixelGUI.Px(RowTextX) - PixelGUI.Px(10f);

        string speaker = SpeakerLabel();
        // Measured first: the panel is as tall as what it holds.
        float speakerH = string.IsNullOrEmpty(speaker) ? 0f : PixelGUI.Px(16f);
        float questionH = string.IsNullOrEmpty(_question) ? 0f
            : Mathf.Max(PixelGUI.Px(13f), _questionStyle.CalcHeight(new GUIContent(_question), inner));
        var rowHeights = new float[_options.Length];
        float rowsH = 0f;
        for (int i = 0; i < _options.Length; i++)
        {
            float textH = _answerStyle.CalcHeight(new GUIContent(_options[i]), textW);
            rowHeights[i] = Mathf.Max(PixelGUI.Px(RowH), Mathf.Ceil(textH + PixelGUI.Px(13f)));
            rowsH += rowHeights[i] + (i > 0 ? PixelGUI.Px(RowPitch - RowH) : 0f);
        }
        float top = PixelGUI.Px(10f);
        float afterQuestion = questionH > 0f ? PixelGUI.Px(13f) : 0f;
        float h = top + speakerH + questionH + afterQuestion + rowsH + PixelGUI.Px(LegendGap + LegendH + 10f);

        float y = Mathf.Round(CinematicBars.BarCoverPixels + PixelGUI.Px(BelowBar));
        var panel = new Rect(x, y, w, h);

        float s = PixelGUI.Px(Shadow), b = PixelGUI.Px(Border);
        PixelGUI.Fill(new Rect(panel.x + s, panel.y + s, panel.width, panel.height), DriveScoutStyle.HeaderRule);
        PixelGUI.Fill(new Rect(panel.x - b, panel.y - b, panel.width + b * 2f, panel.height + b * 2f), DriveScoutStyle.PanelFrame);
        PixelGUI.Fill(panel, DriveScoutStyle.DialoguePanel);

        float cx = panel.x + PixelGUI.Px(Inset);
        float cy = panel.y + top;
        if (speakerH > 0f)
        {
            PixelGUI.LabelTracked(new Rect(cx, cy, inner, PixelGUI.Px(10f)), speaker, _speakerStyle, 2f);
            cy += speakerH;
        }
        if (questionH > 0f)
        {
            GUI.Label(new Rect(cx, cy, inner, questionH), _question, _questionStyle);
            cy += questionH + afterQuestion;
        }

        bool cursorOn = Mathf.Repeat(Time.unscaledTime, BlinkPeriod) < BlinkPeriod * 0.5f;
        for (int i = 0; i < _options.Length; i++)
        {
            var row = new Rect(cx, cy, inner, rowHeights[i]);

            // Mouse hover moves the selection, so pointer and keyboard never disagree.
            if (MouseSteering && _index != i && row.Contains(Event.current.mousePosition)) _index = i;
            bool selected = i == _index;

            if (selected)
            {
                float fb = PixelGUI.Px(2f);
                PixelGUI.Fill(new Rect(row.x - fb, row.y - fb, row.width + fb * 2f, row.height + fb * 2f), DriveScoutStyle.Gold);
                PixelGUI.Fill(row, DriveScoutStyle.ChoiceSelected);
                if (cursorOn)
                    DrawCursorArrow(row.x + PixelGUI.Px(CursorX), row.y + PixelGUI.Px(Mathf.Floor((RowH - 7f) * 0.5f)), px);
            }

            _answerStyle.normal.textColor = selected ? DriveScoutStyle.Text : DriveScoutStyle.Label;
            GUI.Label(new Rect(row.x + PixelGUI.Px(RowTextX), row.y + PixelGUI.Px(6f), textW, row.height - PixelGUI.Px(6f)),
                      _options[i], _answerStyle);

            // A finger that was already down when the question came up was tapping through the lines before it;
            // only one put down on the panel itself answers it.
            if (TouchTaps.Button(row, GUIContent.none, GUIStyle.none) &&
                (!TouchTaps.Driven || TouchTaps.TapDownAt > _openedAt))
                _clicked = i;
            cy += rowHeights[i] + PixelGUI.Px(RowPitch - RowH);
        }

        cy += PixelGUI.Px(LegendGap - (RowPitch - RowH));
        DrawLegend(cx, cy);
    }

    // The ▶ in front of the chosen answer: 4 wide, 7 tall, in whole kit pixels.
    static void DrawCursorArrow(float x, float y, float px)
    {
        for (int i = 0; i < 4; i++)
            PixelGUI.Fill(new Rect(x + i * px, y + i * px, px, (7 - 2 * i) * px), DriveScoutStyle.Gold);
    }

    // W S CHOOSE   E ANSWER, in the keys of the device in hand: keycaps for a keyboard, the pad's button names
    // on caps for a pad, and a line of words on a phone (it is tapped; there are no keys).
    void DrawLegend(float x, float y)
    {
        if (InputGlyphs.UsingTouch)
        {
            PixelGUI.LabelTracked(new Rect(x, y, PixelGUI.Px(200f), PixelGUI.Px(LegendH)), "TAP AN ANSWER", _legendStyle, 2f);
            return;
        }

        bool pad = InputGlyphs.UsingGamepad;
        x = Keycap(x, y, pad ? "D-PAD" : "W", pad ? 0f : 16f);
        if (!pad) x = Keycap(x + PixelGUI.Px(6f), y, "S", 15f);
        x += PixelGUI.Px(6f);
        PixelGUI.LabelTracked(new Rect(x, y, PixelGUI.Px(60f), PixelGUI.Px(LegendH)), "CHOOSE", _legendStyle, 2f);
        x += PixelGUI.TrackedWidth("CHOOSE", _legendStyle, 2f) + PixelGUI.Px(14f);
        x = Keycap(x, y, pad ? InputGlyphs.Confirm : "E", pad ? 0f : 14f);
        x += PixelGUI.Px(6f);
        PixelGUI.LabelTracked(new Rect(x, y, PixelGUI.Px(60f), PixelGUI.Px(LegendH)), "ANSWER", _legendStyle, 2f);
    }

    // A cream keycap on a 2px slate shadow with the key's name on it. `widthPx` 0 = as wide as the name needs.
    // Returns the x just past it.
    float Keycap(float x, float y, string key, float widthPx)
    {
        float w = widthPx > 0f ? PixelGUI.Px(widthPx)
            : Mathf.Ceil(_keyStyle.CalcSize(new GUIContent(key)).x) + PixelGUI.Px(8f);
        float h = PixelGUI.Px(LegendH);
        PixelGUI.Fill(new Rect(x, y + PixelGUI.Px(2f), w, h), DriveScoutStyle.KeyShadow);
        PixelGUI.Fill(new Rect(x, y, w, h), DriveScoutStyle.Text);
        GUI.Label(new Rect(x, y, w, h), key, _keyStyle);
        return x + w;
    }

    // Who is asking, from the conversation that opened the question: the NPC's own speaker name, filled the
    // way their speech bubbles fill it, in the design's capitals. A question nobody owns has no label.
    string SpeakerLabel()
    {
        var npc = _owner as NPCInteractable;
        if (npc == null || string.IsNullOrEmpty(npc.speakerName)) return null;
        return Draftmaster.Chatter.SpeakerIdentity.Fill(npc.speakerName).ToUpperInvariant();
    }

    void EnsureStyles()
    {
        if (_speakerStyle != null && _stylesAt == PixelGUI.Scale) return;
        _stylesAt = PixelGUI.Scale;
        _speakerStyle = Bare(PixelGUI.HeadingSmall, DriveScoutStyle.Gold, TextAnchor.MiddleLeft, false);
        _legendStyle = Bare(PixelGUI.HeadingSmall, DriveScoutStyle.Meta, TextAnchor.MiddleLeft, false);
        _keyStyle = Bare(PixelGUI.HeadingSmall, DriveScoutStyle.PickerPlate, TextAnchor.MiddleCenter, false);
        _questionStyle = Bare(PixelGUI.Data, DriveScoutStyle.Text, TextAnchor.UpperLeft, true);
        _answerStyle = Bare(PixelGUI.Data, DriveScoutStyle.Label, TextAnchor.UpperLeft, true);
    }

    static GUIStyle Bare(GUIStyle from, Color colour, TextAnchor anchor, bool wrap)
    {
        var st = new GUIStyle(from)
        {
            alignment = anchor, wordWrap = wrap, clipping = TextClipping.Overflow, richText = false,
            padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0),
        };
        st.normal.textColor = colour;
        st.hover.textColor = colour;
        return st;
    }
}
