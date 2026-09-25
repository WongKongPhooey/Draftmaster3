using System.Collections.Generic;
using UnityEngine;

// Teach-as-you-play control prompts: a key cap and one line of text, low on the screen
// ("LEFT SHIFT — Hold to run", "E — Get in the car").
//
// Drawn with PixelGUI.Prompt — the same gold-framed plate, the same prose face and the same strip above the
// bottom edge the grandstand seat uses to tell you how to get back to the pits. It used to be an authored
// uGUI prefab (Resources/UI/ControlHint.prefab, now unused) set in a small Text, which came out too small to
// read at a glance and looked nothing like the rest of the kit. One prompt, one place, one look.
//
// Drive it through the ControlHints facade at the bottom of this file:
//     ControlHints.Show("run", "LEFT SHIFT", InputGlyphs.Pad(PadBindings.Run), "Hold to run", 5f);
//     ControlHints.Hide("run");
//
// The pad label is a button name in Xbox naming ("LB", "RT / LT"); while a pad is in use it is drawn as that
// button's icon for the pad actually in the player's hands.
//
// Hints marked `once` remember themselves through AppearanceConditions (OnceEver), so a returning player
// isn't taught to walk every session. Clear them with Draftmaster > NPCs > Clear Appearance Flags.
public class ControlHintUI : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Seconds to fade in / out.")]
    public float fade = 0.25f;

    class Hint
    {
        public string id;
        public string keyboardLabel, gamepadLabel, text;
        // What the hint says instead on a touch screen, with no keycap: a phone has no Left Shift, and the
        // control it has instead is a gesture ("Double-tap the left stick to run / walk"). Null = same text.
        public string touchText;
        public float secondsLeft;      // Infinity = until Hide(id)
        // A hint for a one-press action is also the button for it: tapped or clicked, it does what the key
        // does. Null for hints that teach a held or continuous control (run, throttle), which stay plain.
        public System.Action onPress;
        public PixelGUI.ActionIcon icon;
    }

    readonly List<Hint> _queue = new();
    Hint _current;
    float _alpha;

    static ControlHintUI _instance;

    // No prefab and no scene wiring: the spline scenes render through the 3D URP renderer where a Canvas
    // needs both, and this draws in IMGUI like every other panel in those scenes.
    public static ControlHintUI Instance
    {
        get
        {
            if (_instance != null) return _instance;
            var go = new GameObject("ControlHintUI");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ControlHintUI>();
            return _instance;
        }
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    void OnDestroy() { if (_instance == this) _instance = null; }

    // `urgent` goes to the front of the line: whatever is up fades out now and goes back in the queue with the
    // time it had left. For a prompt the player cannot get past without — a sticky hint already on screen
    // would otherwise hold it back for ever.
    public void Push(string id, string keyboardLabel, string gamepadLabel, string text, float seconds, bool urgent = false,
                     System.Action onPress = null, PixelGUI.ActionIcon icon = PixelGUI.ActionIcon.Next,
                     string touchText = null)
    {
        // Re-showing a live hint just refreshes its timer rather than queueing a duplicate.
        if (_current != null && _current.id == id)
        {
            _current.secondsLeft = seconds;
            _current.onPress = onPress; _current.icon = icon; _current.touchText = touchText;
            return;
        }

        var hint = new Hint
        {
            id = id, keyboardLabel = keyboardLabel, gamepadLabel = gamepadLabel, text = text, secondsLeft = seconds,
            onPress = onPress, icon = icon, touchText = touchText,
        };
        if (urgent)
        {
            _queue.RemoveAll(h => h.id == id);
            if (_current != null && _current.secondsLeft > 0f)
            {
                _queue.Insert(0, new Hint
                {
                    id = _current.id, keyboardLabel = _current.keyboardLabel, gamepadLabel = _current.gamepadLabel,
                    text = _current.text, secondsLeft = _current.secondsLeft,
                    onPress = _current.onPress, icon = _current.icon, touchText = _current.touchText,
                });
                _current.secondsLeft = 0f;
            }
            _queue.Insert(0, hint);
            return;
        }

        for (int i = 0; i < _queue.Count; i++)
            if (_queue[i].id == id) { _queue[i].secondsLeft = seconds; return; }

        _queue.Add(hint);
    }

    public void Dismiss(string id)
    {
        if (_current != null && _current.id == id) _current.secondsLeft = 0f;
        _queue.RemoveAll(h => h.id == id);
    }

    void Update()
    {
        if (_pressed != null)
        {
            var press = _pressed;
            _pressed = null;
            press.onPress?.Invoke();
        }

        if (_current == null && _queue.Count > 0)
        {
            _current = _queue[0];
            _queue.RemoveAt(0);
        }

        if (_current != null)
        {
            if (!float.IsInfinity(_current.secondsLeft)) _current.secondsLeft -= Time.unscaledDeltaTime;
            bool going = _current.secondsLeft <= 0f;
            _alpha = Mathf.MoveTowards(_alpha, going ? 0f : 1f, Time.unscaledDeltaTime / Mathf.Max(0.01f, fade));
            if (going && _alpha <= 0f) _current = null;
        }
        else _alpha = Mathf.MoveTowards(_alpha, 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, fade));
    }

    void OnGUI()
    {
        if (_current == null || _alpha <= 0.001f) return;
        if (Hidden) return;

        // The device is read at draw time, not when the hint was queued, so picking a pad up mid-hint
        // re-labels it on the next frame. A pad label that names buttons ("LB", "RT / LT") is drawn as
        // those buttons' icons for whichever pad is in use; anything else stays text.
        bool pad = InputGlyphs.UsingGamepad && !string.IsNullOrEmpty(_current.gamepadLabel);
        string key = _current.keyboardLabel;
        _icons.Clear();
        if (pad && !InputGlyphs.TryIcons(_current.gamepadLabel, _icons))
            key = InputGlyphs.PadLabel(_current.gamepadLabel);
        string text = _current.text;
        if (InputGlyphs.UsingTouch && !string.IsNullOrEmpty(_current.touchText))
        {
            key = null;
            _icons.Clear();
            text = _current.touchText;
        }

        var prev = GUI.color;
        GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * _alpha);
        if (_current.onPress == null) PixelGUI.Prompt(key, text, _icons);
        else
        {
            // Pressable only while it is properly on screen, not in the tail of its fade.
            var hint = _current;
            if (PixelGUI.PromptButton(key, text, _icons, hint.icon) && _alpha > 0.5f && hint.secondsLeft > 0f)
                _pressed = hint;
        }
        GUI.color = prev;
    }

    readonly List<Sprite> _icons = new();

    // A press seen in OnGUI, acted on in Update: the action may load a scene, open a panel or move the player,
    // none of which should happen halfway through the GUI pass.
    Hint _pressed;

    // Quiet behind anything the player is actually reading, and through a wipe — the same company the
    // grandstand's prompt keeps.
    static bool Hidden =>
        RacePauseMenu.IsPaused ||
        NPCInteractable.AnyConversationActive ||
        DialogueChoiceUI.IsOpen ||
        WeekendScheduleUI.IsOpen ||
        WeekendModal.AnyOpen ||
        ScreenFade.Busy;
}

// Call-site facade. Keeps the spawners free of null checks and owns the "only teach it once" memory.
public static class ControlHints
{
    static readonly Dictionary<string, AppearanceConditions> _once = new();

    // Show a hint. `once` remembers it forever (per save) so a returning player isn't re-taught the basics.
    //
    // `onPress` makes the hint a button as well (see ControlHintUI.Hint.onPress): give it whenever the hint says
    // "press X to do Y" and Y is one press — a touch screen has no X, and that button is its only way to do Y.
    // `icon` is the picture it wears on a touch screen.
    public static void Show(string id, string keyboardLabel, string gamepadLabel, string text,
                            float seconds = 5f, bool once = true, bool urgent = false,
                            System.Action onPress = null, PixelGUI.ActionIcon icon = PixelGUI.ActionIcon.Next,
                            string touchText = null)
    {
        if (once && AlreadyTaught(id)) return;
        var ui = ControlHintUI.Instance;
        if (ui == null) return;
        ui.Push(id, keyboardLabel, gamepadLabel, text, seconds, urgent, onPress, icon, touchText);
        if (once) MarkTaught(id);
    }

    // Show for as long as it stays relevant; call Hide when it stops being true.
    public static void ShowSticky(string id, string keyboardLabel, string gamepadLabel, string text,
                                  bool once = false, bool urgent = false,
                                  System.Action onPress = null, PixelGUI.ActionIcon icon = PixelGUI.ActionIcon.Next)
        => Show(id, keyboardLabel, gamepadLabel, text, Mathf.Infinity, once, urgent, onPress, icon);

    public static void Hide(string id) => ControlHintUI.Instance?.Dismiss(id);

    // Whether a once-only hint has been taught in this save. Callers that hold a hint back until its moment
    // ask this rather than remembering that they called Show — Show on a taught hint does nothing, and a
    // hint forgotten later (ChiefCheckInBeat re-arms "run") has to be teachable again.
    public static bool Taught(string id) => AlreadyTaught(id);

    // Teach it again: wipe a once-only hint's memory. Testing menus use this.
    public static void Forget(string id) => Memory(id).Forget();

    static AppearanceConditions Memory(string id)
    {
        if (!_once.TryGetValue(id, out var c))
        {
            c = new AppearanceConditions
            {
                repeat = AppearanceConditions.Repeat.OnceEver,
                saveKey = "hint." + id,
            };
            _once[id] = c;
        }
        return c;
    }

    static bool AlreadyTaught(string id) => Memory(id).AlreadySeen();
    static void MarkTaught(string id) => Memory(id).MarkSeen();
}
