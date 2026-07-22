using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Teach-as-you-play control prompts: a small key badge + one line of text at the bottom of the screen
// ("LEFT SHIFT — Run", "E — Get in the car"). Authored prefab at Assets/Resources/UI/ControlHint.prefab;
// this binder only swaps the text, picks the keyboard or gamepad label for the device in use, and fades.
//
// Drive it through the ControlHints facade at the bottom of this file:
//     ControlHints.Show("run", "LEFT SHIFT", "LB", "Run", 5f);
//     ControlHints.Hide("run");
//
// Hints marked `once` remember themselves through AppearanceConditions (OnceEver), so a returning player
// isn't taught to walk every session. Clear them with Draftmaster > NPCs > Clear Appearance Flags.
public class ControlHintUI : MonoBehaviour
{
    const string PrefabPath = "UI/ControlHint";

    [Header("Authored children (auto-wired)")]
    public CanvasGroup group;
    public Text keyText;
    public Text hintText;

    [Header("Timing")]
    [Tooltip("Seconds to fade in / out.")]
    public float fade = 0.25f;

    class Hint
    {
        public string id;
        public string keyboardLabel, gamepadLabel, text;
        public float secondsLeft;      // Infinity = until Hide(id)
    }

    readonly List<Hint> _queue = new();
    Hint _current;
    float _alpha;

    static ControlHintUI _instance;

    public static ControlHintUI Instance
    {
        get
        {
            if (_instance != null) return _instance;
            var prefab = Resources.Load<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"ControlHintUI: no prefab at Resources/{PrefabPath} — control hints are off.");
                return null;
            }
            var go = Instantiate(prefab);
            go.name = "ControlHintUI";
            _instance = go.GetComponent<ControlHintUI>();
            if (_instance == null) _instance = go.AddComponent<ControlHintUI>();
            return _instance;
        }
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        ResolveRefs();
        if (group != null) group.alpha = 0f;
    }

    void OnDestroy() { if (_instance == this) _instance = null; }

    public void Push(string id, string keyboardLabel, string gamepadLabel, string text, float seconds)
    {
        // Re-showing a live hint just refreshes its timer rather than queueing a duplicate.
        if (_current != null && _current.id == id) { _current.secondsLeft = seconds; return; }
        for (int i = 0; i < _queue.Count; i++)
            if (_queue[i].id == id) { _queue[i].secondsLeft = seconds; return; }

        _queue.Add(new Hint { id = id, keyboardLabel = keyboardLabel, gamepadLabel = gamepadLabel, text = text, secondsLeft = seconds });
    }

    public void Dismiss(string id)
    {
        if (_current != null && _current.id == id) _current.secondsLeft = 0f;
        _queue.RemoveAll(h => h.id == id);
    }

    void Update()
    {
        if (_current == null && _queue.Count > 0)
        {
            _current = _queue[0];
            _queue.RemoveAt(0);
            Paint(_current);
        }

        if (_current != null)
        {
            if (!float.IsInfinity(_current.secondsLeft)) _current.secondsLeft -= Time.unscaledDeltaTime;
            bool going = _current.secondsLeft <= 0f;
            _alpha = Mathf.MoveTowards(_alpha, going ? 0f : 1f, Time.unscaledDeltaTime / Mathf.Max(0.01f, fade));
            if (going && _alpha <= 0f) _current = null;
            else Paint(_current); // device may change mid-hint (pad picked up)
        }
        else _alpha = Mathf.MoveTowards(_alpha, 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, fade));

        if (group != null) group.alpha = _alpha;
    }

    void Paint(Hint h)
    {
        if (h == null) return;
        bool pad = Gamepad.current != null;
        if (keyText != null) keyText.text = pad && !string.IsNullOrEmpty(h.gamepadLabel) ? h.gamepadLabel : h.keyboardLabel;
        if (hintText != null) hintText.text = h.text;
    }

    void ResolveRefs()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        if (keyText == null) keyText = Find<Text>("Panel/KeyBadge/Key");
        if (hintText == null) hintText = Find<Text>("Panel/Hint");
    }

    T Find<T>(string path) where T : Component
    {
        var t = transform.Find(path);
        return t != null ? t.GetComponent<T>() : null;
    }

#if UNITY_EDITOR
    void OnValidate() => ResolveRefs();
#endif
}

// Call-site facade. Keeps the spawners free of null checks and owns the "only teach it once" memory.
public static class ControlHints
{
    static readonly Dictionary<string, AppearanceConditions> _once = new();

    // Show a hint. `once` remembers it forever (per save) so a returning player isn't re-taught the basics.
    public static void Show(string id, string keyboardLabel, string gamepadLabel, string text,
                            float seconds = 5f, bool once = true)
    {
        if (once && AlreadyTaught(id)) return;
        var ui = ControlHintUI.Instance;
        if (ui == null) return;
        ui.Push(id, keyboardLabel, gamepadLabel, text, seconds);
        if (once) MarkTaught(id);
    }

    // Show for as long as it stays relevant; call Hide when it stops being true.
    public static void ShowSticky(string id, string keyboardLabel, string gamepadLabel, string text, bool once = false)
        => Show(id, keyboardLabel, gamepadLabel, text, Mathf.Infinity, once);

    public static void Hide(string id) => ControlHintUI.Instance?.Dismiss(id);

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
