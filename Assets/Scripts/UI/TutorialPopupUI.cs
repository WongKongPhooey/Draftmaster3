using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// A one-off "here's how this works" popup: a centred window with a title, a couple of lines of body text and
// a dismiss prompt, over a dimmed screen. Bigger and louder than a ControlHint — this is for the first time
// the player is dropped into a mechanic they've never seen (their first paddock fight), where a small badge
// at the bottom of the screen isn't enough.
//
//     TutorialPopup.ShowOnce("fight.basics", "FIGHT!",
//                            "You are now in a fight!\n\nPress {KEY} to push your opponent.",
//                            () => InputGlyphs.Shove);
//
// `{KEY}` in the body is replaced every frame with whatever the keyLabel callback returns, so the popup names
// the right button for the device in the player's hands — and re-labels itself if a pad is picked up while
// it's on screen.
//
// Drawn with IMGUI for the same reason DialogueChoiceUI and the debug panels are: the spline scenes render
// through the 3D URP renderer, where a Canvas needs wiring and a font asset, while OnGUI just works with no
// prefab and no scene setup. Its look comes from PixelGUI, so it matches the rest of the kit.
//
// ShowOnce remembers itself through AppearanceConditions (OnceEver), like ControlHints does — clear it with
// Draftmaster > NPCs > Clear Appearance Flags to see the popup again.
//
// This does NOT stop the game: callers decide what a popup means for their own beat (DriverFight freezes the
// fight behind it). Whether it's open is public so they can hold whatever they hold.
public class TutorialPopupUI : MonoBehaviour
{
    [Tooltip("Panel width in pixels, authored at 1x. PixelGUI scales it up with the screen.")]
    public float panelWidth = 560f;
    [Tooltip("How dark the screen behind the popup goes. 0 = no dimming.")]
    [Range(0f, 0.9f)] public float dimAlpha = 0.55f;
    [Tooltip("Seconds the popup must stay up before any input can dismiss it — the key that opened the beat is usually still held.")]
    public float minVisibleSeconds = 0.4f;

    static TutorialPopupUI _instance;

    // The live popup object, or null if nothing has ever put one up. Querying or closing must not create one.
    public static TutorialPopupUI Existing => _instance;

    public static TutorialPopupUI Instance
    {
        get
        {
            if (_instance != null) return _instance;
            var go = new GameObject("TutorialPopupUI");
            _instance = go.AddComponent<TutorialPopupUI>();
            return _instance;
        }
    }

    string _id, _title, _body;
    System.Func<string> _keyLabel;
    bool _open;
    float _shownAt;
    float _secondsLeft;            // Infinity = until dismissed
    bool _dismissHeldPrev;

    public bool IsOpen => _open;
    public string OpenId => _open ? _id : null;

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    void OnDestroy() { if (_instance == this) _instance = null; }

    public void Open(string id, string title, string body, System.Func<string> keyLabel, float autoCloseSeconds)
    {
        _id = id;
        _title = title;
        _body = body;
        _keyLabel = keyLabel;
        _open = true;
        _shownAt = Time.unscaledTime;
        _secondsLeft = autoCloseSeconds > 0f ? autoCloseSeconds : Mathf.Infinity;
        _dismissHeldPrev = true;   // whatever opened this is probably still held down
    }

    public void Close(string id)
    {
        if (!_open) return;
        if (!string.IsNullOrEmpty(id) && id != _id) return;
        _open = false;
        _keyLabel = null;
    }

    void Update()
    {
        if (!_open) return;

        // The pause menu owns the screen and the same keys — the popup waits behind it.
        if (RacePauseMenu.IsPaused) return;

        if (!float.IsInfinity(_secondsLeft))
        {
            _secondsLeft -= Time.unscaledDeltaTime;
            if (_secondsLeft <= 0f) { Close(null); return; }
        }

        bool held = DismissHeld();
        bool pressed = held && !_dismissHeldPrev;
        _dismissHeldPrev = held;

        if (pressed && Time.unscaledTime - _shownAt >= minVisibleSeconds) Close(null);
    }

    // Anything a player would reasonably press to get on with it: the interact key, the confirm keys, and the
    // south face button. Deliberately includes SPACE even when SPACE is the button being taught — pressing it
    // to clear the popup and then pressing it again to actually throw the shove reads fine.
    static bool DismissHeld()
    {
        bool held = false;
        var kb = Keyboard.current;
        if (kb != null)
            held |= kb.eKey.isPressed || kb.spaceKey.isPressed || kb.enterKey.isPressed ||
                    kb.numpadEnterKey.isPressed || kb.escapeKey.isPressed;
        var gp = Gamepad.current;
        if (gp != null) held |= gp.buttonSouth.isPressed || gp.buttonEast.isPressed || gp.startButton.isPressed;
        return held;
    }

    void OnGUI()
    {
        if (!_open || RacePauseMenu.IsPaused) return;

        GUI.depth = -100;   // in front of every other IMGUI panel in the scene

        if (dimAlpha > 0f) PixelGUI.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, dimAlpha));

        float pad = PixelGUI.Px(20f);
        float w = Mathf.Min(PixelGUI.Px(panelWidth), Screen.width - PixelGUI.Px(40f));
        float inner = w - pad * 2f;

        string body = Body();
        float titleH = string.IsNullOrEmpty(_title)
            ? 0f
            : PixelGUI.Heading.CalcHeight(new GUIContent(_title), inner) + PixelGUI.Px(12f);
        float bodyH = string.IsNullOrEmpty(body) ? 0f : BodyStyle.CalcHeight(new GUIContent(body), inner);
        float footerH = PixelGUI.Px(24f);
        float h = titleH + bodyH + footerH + PixelGUI.Px(36f);

        float x = (Screen.width - w) * 0.5f;
        float y = (Screen.height - h) * 0.5f;

        GUI.Box(new Rect(x, y, w, h), GUIContent.none, PixelGUI.Window);

        float cy = y + PixelGUI.Px(16f);
        if (titleH > 0f)
        {
            GUI.Label(new Rect(x + pad, cy, inner, titleH), _title, PixelGUI.Heading);
            cy += titleH;
        }
        if (bodyH > 0f)
        {
            GUI.Label(new Rect(x + pad, cy, inner, bodyH), body, BodyStyle);
            cy += bodyH;
        }

        GUI.Label(new Rect(x + pad, cy + PixelGUI.Px(8f), inner, footerH),
                  $"{InputGlyphs.Confirm} to continue", PixelGUI.Footer);
    }

    // {KEY} → the live device label, highlighted so the button jumps out of the sentence.
    string Body()
    {
        if (string.IsNullOrEmpty(_body)) return _body;
        if (_keyLabel == null) return _body;

        string key = _keyLabel();
        if (string.IsNullOrEmpty(key)) key = "?";

        var theme = PixelGUI.Theme;
        Color gold = theme != null ? theme.gold : new Color(1f, 0.83f, 0.42f);
        return _body.Replace("{KEY}", $"<color=#{ColorUtility.ToHtmlStringRGB(gold)}><b>{key}</b></color>");
    }

    static GUIStyle _bodyStyle;
    static int _bodyStyleScale;

    // PixelGUI.Row is a single-line list row; body copy needs to wrap and sit top-aligned.
    static GUIStyle BodyStyle
    {
        get
        {
            int scale = PixelGUI.Scale;
            if (_bodyStyle != null && _bodyStyleScale == scale) return _bodyStyle;
            _bodyStyleScale = scale;
            _bodyStyle = new GUIStyle(PixelGUI.Row)
            {
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                richText = true,
            };
            return _bodyStyle;
        }
    }
}

// Call-site facade, shaped like ControlHints: no null checks at the call site, and it owns the "only teach it
// once" memory.
public static class TutorialPopup
{
    static readonly Dictionary<string, AppearanceConditions> _once = new();

    public static bool IsOpen => TutorialPopupUI.Existing != null && TutorialPopupUI.Existing.IsOpen;
    public static string OpenId => TutorialPopupUI.Existing != null ? TutorialPopupUI.Existing.OpenId : null;

    // Show a popup the first time this id comes up, and never again on this save. Returns true if it went up,
    // so a caller that holds its beat behind the popup knows whether it has anything to wait for.
    public static bool ShowOnce(string id, string title, string body,
                                System.Func<string> keyLabel = null, float autoCloseSeconds = 15f)
    {
        if (AlreadyShown(id)) return false;
        if (!Show(id, title, body, keyLabel, autoCloseSeconds)) return false;
        MarkShown(id);
        return true;
    }

    // Show it every time it's asked for. Replaces whatever popup is already up (there is only ever one).
    public static bool Show(string id, string title, string body,
                            System.Func<string> keyLabel = null, float autoCloseSeconds = 15f)
    {
        var ui = TutorialPopupUI.Instance;
        if (ui == null) return false;
        ui.Open(id, title, body, keyLabel, autoCloseSeconds);
        return true;
    }

    // Close the popup. Pass an id to close only that one — a beat ending early shouldn't shut a popup that
    // some later beat has already put up in its place.
    // Goes through Existing rather than Instance on purpose: closing or querying when nothing has ever been
    // shown must not spawn a popup object.
    public static void Close(string id = null) => TutorialPopupUI.Existing?.Close(id);

    static AppearanceConditions Memory(string id)
    {
        if (!_once.TryGetValue(id, out var c))
        {
            c = new AppearanceConditions
            {
                repeat = AppearanceConditions.Repeat.OnceEver,
                saveKey = "tutorial." + id,
            };
            _once[id] = c;
        }
        return c;
    }

    static bool AlreadyShown(string id) => Memory(id).AlreadySeen();
    static void MarkShown(string id) => Memory(id).MarkSeen();
}
