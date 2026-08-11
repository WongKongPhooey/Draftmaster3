using UnityEngine;
using UnityEngine.InputSystem;

// A modal "pick one line" panel for branching on-foot dialogue: a question header and a list of answers the
// player chooses between with W/S (or the stick / d-pad / mouse) and confirms with E / Space / gamepad A.
//
// Drawn with IMGUI for the same reason SpawnIntroUI and the debug panels are — the spline scenes use the 3D
// URP renderer, where a world/screen-space Canvas needs wiring and a font asset, while OnGUI just works with
// no prefab and no scene setup. Its look comes from PixelGUI (the shared window plate, pixel font and
// palette), so it matches the rest of the kit instead of the Unity default skin.
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
    [Tooltip("Panel width in pixels.")]
    public float panelWidth = 720f;
    [Tooltip("Height of one answer row, pixels.")]
    public float rowHeight = 34f;
    [Tooltip("Distance from the bottom of the screen to the bottom of the panel, pixels.")]
    public float bottomMargin = 90f;
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

    void OnGUI()
    {
        if (!_open || _options == null || RacePauseMenu.IsPaused) return;

        var theme = PixelGUI.Theme;
        // panelWidth/rowHeight/bottomMargin are authored at 1x; PixelGUI steps the whole panel up in whole
        // numbers with the screen height, so the box grows with the text instead of the rows crowding.
        float scaledWidth = PixelGUI.Px(panelWidth);
        float scaledRow = PixelGUI.Px(rowHeight);

        float w = Mathf.Min(scaledWidth, Screen.width - PixelGUI.Px(40f));
        float x = (Screen.width - w) * 0.5f;
        float pad = PixelGUI.Px(20f);
        float headerH = string.IsNullOrEmpty(_question)
            ? 0f
            : PixelGUI.Heading.CalcHeight(new GUIContent(_question), w - pad * 2f) + PixelGUI.Px(10f);
        float footerH = PixelGUI.Px(24f);
        float h = headerH + _options.Length * scaledRow + footerH + PixelGUI.Px(28f);
        float y = Screen.height - PixelGUI.Px(bottomMargin) - h;

        // The shared 9-sliced window plate, so a dialogue choice is framed exactly like a menu.
        GUI.Box(new Rect(x, y, w, h), GUIContent.none, PixelGUI.Window);

        float cy = y + PixelGUI.Px(14f);
        if (headerH > 0f)
        {
            GUI.Label(new Rect(x + pad, cy, w - pad * 2f, headerH), _question, PixelGUI.Heading);
            cy += headerH;
        }

        // Rows are indented to leave a gutter for the selection cursor, the JRPG convention: the cursor
        // marks the choice rather than a highlight bar doing all the work.
        float gutter = PixelGUI.Px(20f);
        float rowInset = PixelGUI.Px(14f);
        for (int i = 0; i < _options.Length; i++)
        {
            var row = new Rect(x + rowInset, cy, w - rowInset * 2f, scaledRow);
            bool selected = i == _index;

            // Mouse hover moves the selection, so pointer and keyboard never disagree.
            if (MouseSteering && _index != i && row.Contains(Event.current.mousePosition))
            {
                _index = i;
                selected = true;
            }

            if (selected)
            {
                var band = theme != null ? theme.gold : new Color(1f, 0.83f, 0.42f);
                band.a = 0.16f;
                PixelGUI.Fill(row, band);
                PixelGUI.DrawCursor(row, PixelGUI.Px(12f));
            }

            var textRect = new Rect(row.x + gutter, row.y, row.width - gutter, row.height);
            if (GUI.Button(textRect, _options[i], selected ? PixelGUI.RowSelected : PixelGUI.Row))
                _clicked = i;
            cy += scaledRow;
        }

        // E answers on every device — it is the one interact key the game prompts for.
        bool hasPad = Gamepad.current != null;
        string keys = hasPad ? "Left stick / D-pad to choose    E to answer" : "W / S to choose    E to answer";
        GUI.Label(new Rect(x + pad, cy + PixelGUI.Px(2f), w - pad * 2f, footerH), keys, PixelGUI.Footer);
    }
}
