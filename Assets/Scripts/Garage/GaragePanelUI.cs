using UnityEngine;
using UnityEngine.UI;

// Full-screen info panel shown when the player interacts with a garage role station. Built entirely at
// runtime with legacy uGUI Text (brand body font via BrandFonts) so no prefab/font/TMP wiring is needed. Display-only:
// RoleStation opens it on interact and hides it on the next interact press.
public class GaragePanelUI : MonoBehaviour
{
    public static GaragePanelUI Instance { get; private set; }

    GameObject _root;
    Text _title;
    Text _body;

    // Create the singleton if it doesn't exist yet.
    public static GaragePanelUI Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("GaragePanelUI");
        return go.AddComponent<GaragePanelUI>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Build();
        _root.SetActive(false);
    }

    public bool IsOpen => _root != null && _root.activeSelf;

    public void Show(string title, string body)
    {
        _title.text = title;
        _body.text = body;
        _root.SetActive(true);
    }

    public void Hide()
    {
        if (_root != null) _root.SetActive(false);
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        gameObject.AddComponent<GraphicRaycaster>();

        var font = BrandFonts.Body;

        // Dimmer behind the panel.
        _root = new GameObject("Panel", typeof(RectTransform));
        _root.transform.SetParent(transform, false);
        var dim = _root.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);
        var drt = _root.GetComponent<RectTransform>();
        drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
        drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;

        // Card.
        var card = new GameObject("Card", typeof(RectTransform));
        card.transform.SetParent(_root.transform, false);
        var cbg = card.AddComponent<Image>();
        cbg.color = new Color(0.06f, 0.07f, 0.10f, 0.97f);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(960, 660);

        _title = MakeText("Title", card.transform, font, 42, FontStyle.Bold, TextAnchor.UpperLeft,
                          new Color(1f, 0.82f, 0.25f));
        var trt = _title.rectTransform;
        trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.offsetMin = new Vector2(48, -100); trt.offsetMax = new Vector2(-48, -36);

        _body = MakeText("Body", card.transform, font, 30, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
        var brt = _body.rectTransform;
        brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 1);
        brt.offsetMin = new Vector2(48, 72); brt.offsetMax = new Vector2(-48, -110);

        var hint = MakeText("Hint", card.transform, font, 24, FontStyle.Italic, TextAnchor.LowerRight,
                            new Color(1f, 1f, 1f, 0.5f));
        hint.text = "Press E to close";
        var hrt = hint.rectTransform;
        hrt.anchorMin = new Vector2(0, 0); hrt.anchorMax = new Vector2(1, 0);
        hrt.offsetMin = new Vector2(48, 24); hrt.offsetMax = new Vector2(-48, 60);
    }

    static Text MakeText(string name, Transform parent, Font font, int size, FontStyle style,
                         TextAnchor anchor, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = anchor;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.supportRichText = true;
        return t;
    }
}
