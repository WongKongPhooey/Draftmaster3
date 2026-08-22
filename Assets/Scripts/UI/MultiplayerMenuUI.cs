using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Thin binder for the authored multiplayer-menu Canvas (Assets/Prefabs/UI/MultiplayerMenu.prefab).
//
// The menu is authored in the editor — Canvas, yellow background, Mania title, blue/red GUI buttons, code field —
// so it is visible and editable without entering Play mode. This script only:
//   * caches the child references (auto-found in the editor via OnValidate, re-found at runtime as a fallback),
//   * wires the button clicks,
//   * captures the keyboard for the hand-rolled join-code field (the legacy uGUI InputField can't read the new
//     Input System), and
//   * mirrors NetworkLauncher's connection status / join code into the labels.
public class MultiplayerMenuUI : MonoBehaviour
{
    [SerializeField] string raceSceneName = "RaceScene";

    [Header("Authored children (auto-wired in editor)")]
    [SerializeField] Button singlePlayerButton;
    [SerializeField] Button hostButton;
    [SerializeField] Button joinButton;
    [SerializeField] Text statusText;
    [SerializeField] Text codeDisplay;
    [SerializeField] Text codeText;         // typed join code
    [SerializeField] Text codePlaceholder;  // shown only while the field is empty

    string _code = "";

    void Awake()
    {
        ResolveRefs();
        if (singlePlayerButton != null) singlePlayerButton.onClick.AddListener(OnSinglePlayer);
        if (hostButton != null) hostButton.onClick.AddListener(OnHost);
        if (joinButton != null) joinButton.onClick.AddListener(OnJoin);
    }

    void Start()
    {
        if (NetworkLauncher.Instance != null)
            NetworkLauncher.Instance.StatusChanged += OnStatus;
        RefreshCodeField();
    }

    void OnEnable()
    {
        if (Keyboard.current != null) Keyboard.current.onTextInput += OnTextInput;
    }

    void OnDisable()
    {
        if (Keyboard.current != null) Keyboard.current.onTextInput -= OnTextInput;
    }

    void OnDestroy()
    {
        if (NetworkLauncher.Instance != null)
            NetworkLauncher.Instance.StatusChanged -= OnStatus;
    }

    // New Input System keyboard capture for the join-code field (the legacy InputField can't read it).
    void OnTextInput(char c)
    {
        if (char.IsControl(c)) return;                 // backspace/enter handled in Update
        if (!char.IsLetterOrDigit(c)) return;          // join codes are alphanumeric
        if (_code.Length >= 12) return;
        _code += char.ToUpperInvariant(c);
        RefreshCodeField();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.backspaceKey.wasPressedThisFrame && _code.Length > 0)
        {
            _code = _code.Substring(0, _code.Length - 1);
            RefreshCodeField();
        }
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            OnJoin();
    }

    void RefreshCodeField()
    {
        if (codeText != null) codeText.text = _code;
        if (codePlaceholder != null) codePlaceholder.enabled = string.IsNullOrEmpty(_code);
    }

    void OnStatus(string s)
    {
        if (statusText != null) statusText.text = s;
        var code = NetworkLauncher.Instance != null ? NetworkLauncher.Instance.JoinCode : null;
        if (codeDisplay != null && !string.IsNullOrEmpty(code))
            codeDisplay.text = "Join code: " + code;
    }

    // ---- button handlers ----

    void OnSinglePlayer()
    {
        GameSession.CurrentMode = GameSession.Mode.SinglePlayer;
        SceneManager.LoadScene(raceSceneName);
    }

    void OnHost()
    {
        if (NetworkLauncher.Instance == null) { SetStatus("No NetworkLauncher in scene."); return; }
        NetworkLauncher.Instance.HostGame();
    }

    void OnJoin()
    {
        if (NetworkLauncher.Instance == null) { SetStatus("No NetworkLauncher in scene."); return; }
        NetworkLauncher.Instance.JoinGame(_code);
    }

    void SetStatus(string s) { if (statusText != null) statusText.text = s; }

    // Locate the authored children by name. Used as a runtime fallback and to bake references in the editor.
    void ResolveRefs()
    {
        if (singlePlayerButton == null) singlePlayerButton = FindButton("SinglePlayerButton");
        if (hostButton == null) hostButton = FindButton("HostButton");
        if (joinButton == null) joinButton = FindButton("JoinButton");
        if (statusText == null) statusText = FindText("Status");
        if (codeDisplay == null) codeDisplay = FindText("CodeDisplay");
        if (codeText == null) codeText = FindText("CodeInput/Text");
        if (codePlaceholder == null) codePlaceholder = FindText("CodeInput/Placeholder");
    }

    Button FindButton(string path) { var t = transform.Find(path); return t != null ? t.GetComponent<Button>() : null; }
    Text FindText(string path) { var t = transform.Find(path); return t != null ? t.GetComponent<Text>() : null; }

#if UNITY_EDITOR
    // Bake the authored child references into the prefab/scene so they're visible in the inspector.
    void OnValidate()
    {
        if (!Application.isPlaying) ResolveRefs();
    }
#endif
}
