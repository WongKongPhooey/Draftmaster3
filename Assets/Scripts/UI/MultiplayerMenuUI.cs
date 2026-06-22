using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// Self-contained demo menu. Builds its own uGUI (Single Player / Host / Join-by-code / status)
// at runtime so no prefab references or UnityEvent wiring are needed — drop this on one GameObject
// in the demo menu scene and it works. Legacy uGUI (Text/Button/InputField) keeps dependencies light.
public class MultiplayerMenuUI : MonoBehaviour
{
    [SerializeField] string raceSceneName = "WatkinsGlen";

    Text statusText;
    Text codeDisplay;
    InputField codeInput;

    void Start()
    {
        BuildUI();
        if (NetworkLauncher.Instance != null)
            NetworkLauncher.Instance.StatusChanged += OnStatus;
    }

    void OnDestroy()
    {
        if (NetworkLauncher.Instance != null)
            NetworkLauncher.Instance.StatusChanged -= OnStatus;
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
        NetworkLauncher.Instance.JoinGame(codeInput != null ? codeInput.text : "");
    }

    void SetStatus(string s) { if (statusText != null) statusText.text = s; }

    // ---- UI construction ----

    void BuildUI()
    {
        var canvasGo = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        if (FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            es.transform.SetParent(transform, false);
        }

        // Title
        MakeText(canvas.transform, "Title", "DRAFTMASTER — MULTIPLAYER", 54,
            new Vector2(0.5f, 1f), new Vector2(0, -120), new Vector2(1200, 90), TextAnchor.MiddleCenter);

        // Single Player
        MakeButton(canvas.transform, "Single Player", new Vector2(0, 120), OnSinglePlayer);

        // Host
        MakeButton(canvas.transform, "Host Game", new Vector2(0, 20), OnHost);

        // Join code input + button
        codeInput = MakeInput(canvas.transform, "CodeInput", "Enter join code…", new Vector2(-110, -80));
        MakeButton(canvas.transform, "Join", new Vector2(170, -80), OnJoin, 180);

        // Host's code display
        codeDisplay = MakeText(canvas.transform, "CodeDisplay", "", 30,
            new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(900, 50), TextAnchor.MiddleCenter);

        // Status line
        statusText = MakeText(canvas.transform, "Status", "Single player, or host / join a multiplayer race.", 26,
            new Vector2(0.5f, 0f), new Vector2(0, 80), new Vector2(1400, 50), TextAnchor.MiddleCenter);
    }

    static Font UiFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    Text MakeText(Transform parent, string name, string content, int size, Vector2 anchor,
        Vector2 anchoredPos, Vector2 sizeDelta, TextAnchor align)
    {
        var go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = UiFont;
        t.text = content;
        t.fontSize = size;
        t.color = Color.white;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        return t;
    }

    Button MakeButton(Transform parent, string label, Vector2 anchoredPos,
        UnityEngine.Events.UnityAction onClick, float width = 360)
    {
        var go = new GameObject(label + "Button", typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.12f, 0.14f, 0.20f, 0.95f);
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(width, 72);

        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(onClick);

        var txt = MakeText(go.transform, "Label", label, 30,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 72), TextAnchor.MiddleCenter);
        txt.color = Color.white;
        return btn;
    }

    InputField MakeInput(Transform parent, string name, string placeholder, Vector2 anchoredPos)
    {
        var go = new GameObject(name, typeof(Image), typeof(InputField));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.08f, 0.09f, 0.12f, 0.95f); // dark field so the typed code reads clearly
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(420, 72);

        var input = go.GetComponent<InputField>();
        input.characterLimit = 12;

        var ph = MakeText(go.transform, "Placeholder", placeholder, 28,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 60), TextAnchor.MiddleLeft);
        ph.color = new Color(0.7f, 0.7f, 0.72f, 1f);
        ph.fontStyle = FontStyle.Italic;
        ph.rectTransform.offsetMin = new Vector2(15, 0);

        var text = MakeText(go.transform, "Text", "", 28,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 60), TextAnchor.MiddleLeft);
        text.color = Color.white; // white text on the dark field
        text.supportRichText = false;
        text.rectTransform.offsetMin = new Vector2(15, 0);

        input.textComponent = text;
        input.placeholder = ph;
        return input;
    }
}
