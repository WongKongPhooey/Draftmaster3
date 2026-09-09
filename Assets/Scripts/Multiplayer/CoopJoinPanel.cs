using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

// The guest's way in, opened by the title screen's MULTIPLAYER row.
//
// Joining needs a six-character code typed in, which a menu row on its own cannot take, so the row opens
// this instead. Drawn in the Iron Oval kit through PixelGUI so it sits with the rest of the game's panels
// rather than looking like a debug window.
//
// The other half of co-op — opening YOUR career to a friend — is not here and should not be: it only means
// anything once you are in a career, so it lives on the race-scene pause menu.
public class CoopJoinPanel : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    string _code = "";
    string _status = "";
    bool _joining;

    // The frame this panel appeared on. The keypress that opened it — ENTER, or E on the menu's own
    // confirm — is still down when the panel's first Update runs, so without this the panel opens and
    // immediately joins on an empty code, or types that E as the first character of the code.
    int _openedFrame = -1;

    public static void Open()
    {
        if (IsOpen) return;
        var go = new GameObject("CoopJoinPanel");
        go.AddComponent<CoopJoinPanel>();
        DontDestroyOnLoad(go);
    }

    public static void Close()
    {
        var panel = FindFirstObjectByType<CoopJoinPanel>();
        if (panel != null) Destroy(panel.gameObject);
    }

    void OnEnable()
    {
        IsOpen = true;
        _openedFrame = Time.frameCount;
        if (Keyboard.current != null) Keyboard.current.onTextInput += OnTextInput;
    }

    void OnDisable()
    {
        IsOpen = false;
        if (Keyboard.current != null) Keyboard.current.onTextInput -= OnTextInput;
    }

    void Start()
    {
        var launcher = Launcher();
        if (launcher != null) launcher.StatusChanged += OnStatus;
    }

    void OnDestroy()
    {
        if (NetworkLauncher.Instance != null) NetworkLauncher.Instance.StatusChanged -= OnStatus;
    }

    void OnStatus(string s)
    {
        _status = s;
        // Connected: the host's scene is on its way over and this panel has done its job.
        if (Coop.IsGuest && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            Destroy(gameObject);
    }

    // The join code is typed, and the kit's panels are IMGUI — so the keyboard is read directly, the same
    // way the multiplayer menu's code field does it. GUI.TextField cannot see the new Input System.
    void OnTextInput(char c)
    {
        if (_joining) return;
        if (Time.frameCount == _openedFrame) return;   // the keypress that opened us is not code

        if (!char.IsLetterOrDigit(c)) return;      // join codes are alphanumeric
        if (_code.Length >= 8) return;
        _code += char.ToUpperInvariant(c);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (Time.frameCount == _openedFrame) return;

        if (kb.escapeKey.wasPressedThisFrame) { Destroy(gameObject); return; }
        if (kb.backspaceKey.wasPressedThisFrame && _code.Length > 0 && !_joining)
            _code = _code.Substring(0, _code.Length - 1);
        if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) Join();
    }

    void Join()
    {
        if (_joining) return;
        if (string.IsNullOrWhiteSpace(_code)) { _status = "Enter the code your friend gave you."; return; }

        var launcher = Launcher();
        if (launcher == null) { _status = "No network launcher."; return; }

        _joining = true;
        launcher.JoinCoop(_code);
    }

    // Menu scenes carry no NetworkLauncher, so make one. It puts itself in DontDestroyOnLoad and survives
    // the scene load the host is about to pull us through.
    static NetworkLauncher Launcher()
    {
        if (NetworkLauncher.Instance != null) return NetworkLauncher.Instance;
        return new GameObject("NetworkLauncher").AddComponent<NetworkLauncher>();
    }

    // The kit's Display face, centred in the field. Cached because a GUIStyle per frame is waste, and
    // rebuilt when the kit rescales (a window resize changes the point size under us).
    static GUIStyle _codeStyle;
    static int _codeStyleSize;
    static GUIStyle CodeStyle()
    {
        var baseStyle = PixelGUI.Display;
        if (_codeStyle == null || _codeStyleSize != baseStyle.fontSize)
        {
            _codeStyle = new GUIStyle(baseStyle) { alignment = TextAnchor.MiddleCenter };
            _codeStyleSize = baseStyle.fontSize;
        }
        return _codeStyle;
    }

    void OnGUI()
    {
        PixelGUI.Scrim();

        float row = PixelGUI.LineH;
        float gap = PixelGUI.Px(4f);
        float buttonH = row + PixelGUI.Px(6f);
        float w = PixelGUI.Px(220f);
        float fieldH = PixelGUI.Display.fontSize + PixelGUI.Px(8f);
        float h = PixelGUI.Px(20f) + PixelGUI.Heading.fontSize + gap * 6f
                  + row * 3f + fieldH + buttonH * 2f + PixelGUI.LineH;
        float x = Mathf.Round((Screen.width - w) * 0.5f);
        float y = Mathf.Round((Screen.height - h) * 0.5f);

        PixelGUI.Panel(new Rect(x, y, w, h), focused: true);
        var c = PixelGUI.PanelContent(new Rect(x, y, w, h), 10f);
        float cy = c.y;

        // Named for the row the player just pressed, not for what the panel does — a heading that does not
        // echo the thing you clicked reads as a different screen than the one you asked for.
        GUI.Label(new Rect(c.x, cy, c.width, PixelGUI.Heading.fontSize), "MULTIPLAYER", PixelGUI.Heading);
        cy += PixelGUI.Heading.fontSize + gap;
        PixelGUI.Rule(c.x, cy, c.width);
        cy += gap * 2f;

        GUI.Label(new Rect(c.x, cy, c.width, row), "Ask them to open their career", PixelGUI.LabelDim);
        cy += row;
        GUI.Label(new Rect(c.x, cy, c.width, row), "to a friend, then type their code.", PixelGUI.LabelDim);
        cy += row + gap;

        // The typed code. A bare line of text does not read as somewhere you can type, so it sits on a
        // sunk plate inside a gold border — the same yellow as the heading — and the empty slots are shown
        // as underscores so the six characters are countable before any are typed.
        var field = new Rect(c.x, cy, c.width, fieldH);
        PixelGUI.Fill(field, PixelGUI.Ink);
        PixelGUI.Frame(field, PixelGUI.Gold);
        string shown = _code.PadRight(6, '_');
        GUI.Label(field, shown, CodeStyle());
        cy += fieldH + gap;

        if (!string.IsNullOrEmpty(_status))
        {
            GUI.Label(new Rect(c.x, cy, c.width, row), _status, PixelGUI.LabelDim);
        }
        cy += row + gap;

        if (PixelGUI.Button(new Rect(c.x, cy, c.width, buttonH), _joining ? "JOINING…" : "JOIN") && !_joining)
            Join();
        cy += buttonH + gap;

        if (PixelGUI.Button(new Rect(c.x, cy, c.width, buttonH), "BACK")) Destroy(gameObject);

        GUI.Label(new Rect(c.x, c.yMax - PixelGUI.LineH, c.width, PixelGUI.LineH),
                  "TYPE THE CODE  ·  ENTER JOIN  ·  ESC BACK", PixelGUI.Footer);
    }
}
