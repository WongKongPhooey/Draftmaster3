using UnityEngine;
using UnityEngine.InputSystem;

// Taps for the IMGUI screens.
//
// Every menu in the race scene — pause, weekend sheet, result cards, dialogue choices, the phone — is drawn
// with IMGUI, and an IMGUI button only fires if the platform turns a touch into a mouse event. The Device
// Simulator never does, and a phone may not either, so on a touch device every one of those buttons was dead
// to a finger.
//
// This reads the finger straight off the Input System instead (the same source as the walk stick and the
// driving pedals). A press that lifts inside TapSeconds without wandering further than TapSlop is a tap; it
// stands for the whole of the frame it lands in, and the first button drawn under it that asks takes it.
//
// Callers swap GUI.Button / GUI.Toggle for TouchTaps.Button / TouchTaps.Toggle. On a touch device the
// button answers the finger and ignores IMGUI's own click, so a platform that DOES forward touches as mouse
// clicks cannot press anything twice; everywhere else it is exactly GUI.Button.
[DefaultExecutionOrder(-950)]
public class TouchTaps : MonoBehaviour
{
    const float TapSeconds = 0.45f;
    const float TapSlopPx = 14f;

    static TouchTaps _instance;

    static bool _pending;
    static Vector2 _tapAt;            // IMGUI screen space: top-left origin, y down

    bool _tracking;
    Vector2 _down, _last;             // Input System screen space: y up
    float _downAt, _travel;

    // What the finger has moved this frame while held — for a screen that scrolls by dragging (the phone).
    // Input System space, so +y is the finger moving UP the glass.
    public static Vector2 DragDelta { get; private set; }
    public static bool Dragging { get; private set; }

    // A touch device with nothing else in the player's hands: buttons answer the finger, not the mouse.
    public static bool Driven => InputGlyphs.UsingTouch && Touchscreen.current != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (_instance != null) return;
        var go = new GameObject("TouchTaps");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<TouchTaps>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    void OnDestroy()
    {
        if (_instance != this) return;
        _instance = null;
        _pending = false;
    }

    // A tap stands for the frame it was made in: every OnGUI of that frame can see it, and it is gone
    // before the next one, so a tap that landed on nothing is never saved up for a button drawn later.
    void Update()
    {
        _pending = false;
        DragDelta = Vector2.zero;

        var screen = Touchscreen.current;
        if (screen == null) { _tracking = false; Dragging = false; return; }

        var t = screen.primaryTouch;
        Vector2 pos = t.position.ReadValue();

        if (t.press.wasPressedThisFrame)
        {
            _tracking = true;
            _down = _last = pos;
            _downAt = Time.unscaledTime;
            _travel = 0f;
        }
        else if (_tracking && t.press.isPressed)
        {
            Vector2 d = pos - _last;
            _travel += d.magnitude;
            if (_travel > TapSlopPx) DragDelta = d;
            _last = pos;
        }
        Dragging = _tracking && _travel > TapSlopPx && t.press.isPressed;

        if (_tracking && t.press.wasReleasedThisFrame)
        {
            _tracking = false;
            if (_travel <= TapSlopPx && Time.unscaledTime - _downAt <= TapSeconds)
            {
                _pending = true;
                _tapAt = new Vector2(_down.x, Screen.height - _down.y);
                TapDownAt = _downAt;
            }
        }
    }

    // When the finger behind the current tap came down (unscaled time). A screen that has only just opened
    // compares this with its own opening time: a finger that was already on the glass was aimed at whatever
    // was there before, not at the new screen.
    public static float TapDownAt { get; private set; } = -1f;

    // The same lift has already been spent elsewhere — the walk controls advanced a line of dialogue with it.
    // Without this one lift is two taps: the one that advanced the conversation, and one left pending for the
    // same frame that lands on whatever the conversation opened (a choice panel, picked before it was seen).
    public static void Consume() => _pending = false;

    // True once, on the repaint pass, for a tap inside `r` — given in whatever GUI space is current, since
    // ScreenToGUIPoint undoes the matrix and any groups the caller is inside. `visible`, when given, is the
    // part of that space actually on screen (a scroll window), so a row scrolled out of it cannot be hit.
    public static bool Hit(Rect r, Rect? visible = null)
    {
        if (!_pending || Event.current == null || Event.current.type != EventType.Repaint) return false;
        Vector2 p = GUIUtility.ScreenToGUIPoint(_tapAt);
        if (!r.Contains(p)) return false;
        if (visible.HasValue && !visible.Value.Contains(p)) return false;
        _pending = false;
        return true;
    }

    // Drop-in for GUI.Button. Drawing is still GUI.Button's own, so hover and pressed looks are unchanged.
    public static bool Button(Rect r, GUIContent content, GUIStyle style)
    {
        bool clicked = GUI.Button(r, content, style);
        if (!Driven) return clicked;
        // Keep the walk controls off it: a finger landing here is this button's, not the stick's and not a tap
        // on somebody stood behind it (see TouchWalkControls.Claim).
        if (Event.current != null && Event.current.type == EventType.Repaint)
            TouchWalkControls.Claim(GUIUtility.GUIToScreenRect(r));
        return GUI.enabled && Hit(r);
    }

    public static bool Button(Rect r, string text, GUIStyle style) => Button(r, new GUIContent(text), style);

    // Drop-in for GUI.Toggle: a tap flips it.
    public static bool Toggle(Rect r, bool value, string text, GUIStyle style)
    {
        bool changed = GUI.Toggle(r, value, text, style);
        if (!Driven) return changed;
        return GUI.enabled && Hit(r) ? !value : value;
    }
}
