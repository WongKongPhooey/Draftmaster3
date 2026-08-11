using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Parts shop panel for landmark locations — the on-foot counterpart of the travel map's side-panel
// shop. Runtime-built uGUI (GaragePanelUI pattern, brand body font via BrandFonts, no prefab wiring) since the
// authored-layout requirement is the map, not this. Same stock rules as the map: junkyards sell the
// weekly salvage roll (bought items gone until the week ticks), engine shops a fixed catalog; buying
// installs immediately and scraps the old part. ShopCounterNPC opens/closes it.
public class LandmarkShopPanel : MonoBehaviour
{
    public static LandmarkShopPanel Instance { get; private set; }

    GameObject _root;
    Text _title, _cash, _flavor, _status, _hint;
    RectTransform _rows;
    Font _font;

    public static LandmarkShopPanel Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("LandmarkShopPanel");
        return go.AddComponent<LandmarkShopPanel>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        Build();
        _root.SetActive(false);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    public bool IsOpen => _root != null && _root.activeSelf;

    public void Show()
    {
        _status.text = "";
        Refresh();
        _root.SetActive(true);
    }

    public void Hide()
    {
        if (_root != null) _root.SetActive(false);
    }

    // ---------------- content ----------------

    void Refresh()
    {
        var loc = TravelGraph.Get(TravelState.CurrentNodeId);
        if (loc == null) { _title.text = "NOWHERE"; return; }

        _title.text = loc.name.ToUpperInvariant();
        _cash.text = PlayerWallet.CashText;
        _flavor.text = loc.flavor ?? "";

        for (int i = _rows.childCount - 1; i >= 0; i--) Destroy(_rows.GetChild(i).gameObject);

        float y = 0f;
        if (loc.locationType == TravelLocationType.Junkyard)
        {
            y = AddHeading("SALVAGE THIS WEEK", y);
            bool any = false;
            foreach (var (part, price) in PartCatalog.JunkyardStock(loc.id, TravelState.Week))
            {
                if (TravelState.WasBought(loc.id, part.id)) continue;
                y = AddStockRow(part, price, y, () => TravelState.MarkBought(loc.id, part.id));
                any = true;
            }
            if (!any) y = AddHeading("Shelf's bare. Come back next week.", y);
        }
        else if (loc.locationType == TravelLocationType.EngineShop)
        {
            y = AddHeading("FOR SALE", y);
            if (loc.shopStock != null)
                foreach (var id in loc.shopStock)
                {
                    var part = PartCatalog.Get(id);
                    if (part != null) y = AddStockRow(part, part.price, y, null);
                }
        }
    }

    float AddHeading(string text, float y)
    {
        var t = MakeText("Heading", _rows, 24, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.4f, 1f, 0.5f));
        t.text = text;
        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, -y);
        rt.sizeDelta = new Vector2(0, 30);
        return y + 36;
    }

    float AddStockRow(PartDef part, int price, float y, System.Action onBought)
    {
        var row = new GameObject("Row", typeof(RectTransform));
        row.transform.SetParent(_rows, false);
        var rrt = (RectTransform)row.transform;
        rrt.anchorMin = new Vector2(0, 1); rrt.anchorMax = new Vector2(1, 1); rrt.pivot = new Vector2(0.5f, 1);
        rrt.anchoredPosition = new Vector2(0, -y);
        rrt.sizeDelta = new Vector2(0, 92);

        bool installed = PlayerCarBuild.InstalledId(part.slot) == part.id;

        var name = MakeText("Name", row.transform, 26, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        name.text = part.name + (installed ? "   [INSTALLED]" : "");
        var nrt = name.rectTransform;
        nrt.anchorMin = new Vector2(0, 1); nrt.anchorMax = new Vector2(1, 1); nrt.pivot = new Vector2(0.5f, 1);
        nrt.anchoredPosition = Vector2.zero;
        nrt.sizeDelta = new Vector2(0, 32);

        var effect = MakeText("Effect", row.transform, 20, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.8f, 0.8f, 0.85f));
        effect.text = part.EffectSummary() + "\n" + PlayerCarBuild.DescribeSlot(part.slot);
        var ert = effect.rectTransform;
        ert.anchorMin = new Vector2(0, 1); ert.anchorMax = new Vector2(1, 1); ert.pivot = new Vector2(0.5f, 1);
        ert.anchoredPosition = new Vector2(-95, -34);
        ert.sizeDelta = new Vector2(-190, 54);

        if (!installed)
        {
            var btnGo = new GameObject("Buy", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(row.transform, false);
            var brt = (RectTransform)btnGo.transform;
            brt.anchorMin = new Vector2(1, 1); brt.anchorMax = new Vector2(1, 1); brt.pivot = new Vector2(1, 1);
            brt.anchoredPosition = new Vector2(0, -34);
            brt.sizeDelta = new Vector2(170, 50);
            btnGo.GetComponent<Image>().color = new Color(0.18f, 0.42f, 0.80f);

            var label = MakeText("Label", btnGo.transform, 22, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            label.text = PlayerWallet.Format(price);
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;

            btnGo.GetComponent<Button>().onClick.AddListener(() =>
            {
                if (PlayerWallet.TrySpend(price))
                {
                    PlayerCarBuild.Install(part);
                    PlayerStatsLedger.Increment("partsbought");
                    onBought?.Invoke();
                    _status.text = $"{part.name} installed. Old {part.slot.ToString().ToLowerInvariant()} scrapped.";
                }
                else _status.text = "Not enough cash.";
                Refresh();
            });
        }

        return y + 96;
    }

    // ---------------- construction ----------------

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 520;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        gameObject.AddComponent<GraphicRaycaster>();

        _font = BrandFonts.Body;

        _root = new GameObject("Panel", typeof(RectTransform));
        _root.transform.SetParent(transform, false);
        var dim = _root.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);
        var drt = (RectTransform)_root.transform;
        drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
        drt.offsetMin = drt.offsetMax = Vector2.zero;

        var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(_root.transform, false);
        card.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.10f, 0.97f);
        var crt = (RectTransform)card.transform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(1100, 760);

        _title = MakeText("Title", card.transform, 40, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 0.82f, 0.25f));
        var trt = _title.rectTransform;
        trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0.5f, 1);
        trt.offsetMin = new Vector2(48, -92); trt.offsetMax = new Vector2(-260, -32);

        _cash = MakeText("Cash", card.transform, 32, FontStyle.Bold, TextAnchor.MiddleRight, new Color(1f, 0.82f, 0.25f));
        var cart = _cash.rectTransform;
        cart.anchorMin = new Vector2(1, 1); cart.anchorMax = new Vector2(1, 1); cart.pivot = new Vector2(1, 1);
        cart.anchoredPosition = new Vector2(-48, -40);
        cart.sizeDelta = new Vector2(220, 40);

        _flavor = MakeText("Flavor", card.transform, 22, FontStyle.Italic, TextAnchor.UpperLeft, new Color(0.75f, 0.75f, 0.8f));
        var frt = _flavor.rectTransform;
        frt.anchorMin = new Vector2(0, 1); frt.anchorMax = new Vector2(1, 1); frt.pivot = new Vector2(0.5f, 1);
        frt.offsetMin = new Vector2(48, -168); frt.offsetMax = new Vector2(-48, -96);

        var rowsGo = new GameObject("Rows", typeof(RectTransform));
        rowsGo.transform.SetParent(card.transform, false);
        _rows = (RectTransform)rowsGo.transform;
        _rows.anchorMin = new Vector2(0, 0); _rows.anchorMax = new Vector2(1, 1);
        _rows.offsetMin = new Vector2(48, 110); _rows.offsetMax = new Vector2(-48, -180);

        _status = MakeText("Status", card.transform, 22, FontStyle.Bold, TextAnchor.LowerLeft, new Color(0.4f, 1f, 0.5f));
        var srt = _status.rectTransform;
        srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(1, 0); srt.pivot = new Vector2(0.5f, 0);
        srt.offsetMin = new Vector2(48, 24); srt.offsetMax = new Vector2(-360, 64);

        _hint = MakeText("Hint", card.transform, 22, FontStyle.Italic, TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.5f));
        _hint.text = "Press E to close";
        var hrt = _hint.rectTransform;
        hrt.anchorMin = new Vector2(1, 0); hrt.anchorMax = new Vector2(1, 0); hrt.pivot = new Vector2(1, 0);
        hrt.anchoredPosition = new Vector2(-48, 24);
        hrt.sizeDelta = new Vector2(300, 40);
    }

    Text MakeText(string name, Transform parent, int size, FontStyle style, TextAnchor anchor, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = _font;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = anchor;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }
}
