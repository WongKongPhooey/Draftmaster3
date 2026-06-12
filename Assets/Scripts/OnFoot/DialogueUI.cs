using UnityEngine;
using UnityEngine.UI;

// Screen-space dialogue panel. Auto-builds its UI. Singleton; NPCs push lines to it.
public class DialogueUI : MonoBehaviour
{
    public static DialogueUI Instance { get; private set; }

    public Color panelColor = new Color(0.05f, 0.05f, 0.07f, 0.92f);
    public Color textColor = Color.white;
    public Color speakerColor = new Color(1f, 0.8f, 0.2f);

    GameObject _panel;
    Text _speakerText;
    Text _bodyText;
    Text _hintText;

    public bool IsOpen { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildUI();
        Hide();
    }

    public void Show(string speaker, string line)
    {
        if (_panel == null) return;
        _panel.SetActive(true);
        IsOpen = true;
        if (_speakerText != null) _speakerText.text = speaker;
        if (_bodyText != null) _bodyText.text = line;
    }

    public void Hide()
    {
        if (_panel != null) _panel.SetActive(false);
        IsOpen = false;
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("DialogueCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        _panel = new GameObject("Panel", typeof(RectTransform));
        _panel.transform.SetParent(canvasGO.transform, false);
        var prt = _panel.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.1f, 0.04f);
        prt.anchorMax = new Vector2(0.9f, 0.26f);
        prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
        var pimg = _panel.AddComponent<Image>();
        pimg.color = panelColor;

        _speakerText = MakeText("Speaker", _panel.transform, speakerColor, 34, FontStyle.Bold,
            new Vector2(0.03f, 0.6f), new Vector2(0.97f, 0.95f), TextAnchor.UpperLeft);
        _bodyText = MakeText("Body", _panel.transform, textColor, 28, FontStyle.Normal,
            new Vector2(0.03f, 0.18f), new Vector2(0.97f, 0.62f), TextAnchor.UpperLeft);
        _hintText = MakeText("Hint", _panel.transform, new Color(1,1,1,0.5f), 20, FontStyle.Italic,
            new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.16f), TextAnchor.LowerRight);
        _hintText.text = "Interact to continue";
    }

    static Text MakeText(string name, Transform parent, Color color, int size, FontStyle style,
        Vector2 anchorMin, Vector2 anchorMax, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var txt = go.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.color = color; txt.fontSize = size; txt.fontStyle = style; txt.alignment = align;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        return txt;
    }
}
