using Draftmaster.Sponsors;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// The sponsorship board in the team garage: what's signed, what's on the car, and which panel each decal
// goes on. Signing happens out at the track (SponsorRepNPC); this is where a deal turns into money, because
// a sponsor only pays while its decal is on a panel.
//
// Runtime-built uGUI in the GaragePanelUI / LandmarkShopPanel pattern — no prefab or font wiring. Opened by
// the SponsorshipManager RoleStation.
public class SponsorBoardPanel : MonoBehaviour
{
    public static SponsorBoardPanel Instance { get; private set; }

    GameObject _root;
    Text _title, _income, _status;
    RectTransform _rows;
    Font _font;

    // Which deal the player is currently placing, 0 = none. Two clicks: pick a deal, then pick a panel.
    int _picking;

    [Tooltip("Debug key that opens the board anywhere, so placement can be checked at the track without walking to the garage. F1 tuner, F2 leaderboard, F3 telemetry, F4 rivalries, F5 dossier, F6 here.")]
    public Key toggleKey = Key.F6;

    public static SponsorBoardPanel Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("SponsorBoardPanel");
        return go.AddComponent<SponsorBoardPanel>();
    }

    // The garage station opens this properly; the F-key is the dev route in from anywhere else.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("SponsorBoardPanel");
        DontDestroyOnLoad(go);
        go.AddComponent<SponsorBoardPanel>();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb[toggleKey].wasPressedThisFrame) return;
        if (IsOpen) Hide(); else Show();
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
        _picking = 0;
        _status.text = "";
        Refresh();
        _root.SetActive(true);
    }

    public void Hide() { if (_root != null) _root.SetActive(false); }

    // ---------------------------------------------------------------- content

    void Refresh()
    {
        _title.text = "SPONSORSHIP — WHAT'S ON THE CAR";
        _income.text = $"On the car: ${SponsorBook.PerRaceIncome():N0} per race";

        for (int i = _rows.childCount - 1; i >= 0; i--) Destroy(_rows.GetChild(i).gameObject);
        float y = 0f;

        // Panels first: the car itself, one row per slot, showing what's painted there.
        y = AddHeading("PANELS", y);
        foreach (var slot in SponsorSlots.All)
        {
            var occupant = SponsorBook.InSlot(slot);
            string worth = $"{SponsorSlots.PayMultiplier(slot):P0} rate";
            string label = occupant == null
                ? $"{SponsorSlots.DisplayName(slot),-14}  empty                      {worth}"
                : $"{SponsorSlots.DisplayName(slot),-14}  {occupant.sponsorName,-24}  ${(int)(occupant.perRace * SponsorSlots.PayMultiplier(slot)):N0}/race";

            SponsorSlot captured = slot;
            if (_picking != 0)
                y = AddButton(label, "PLACE HERE", y, () => PlacePicked(captured));
            else if (occupant != null)
                y = AddButton(label, "TAKE OFF", y, () => { SponsorBook.Unplace(occupant.id); _status.text = $"{occupant.sponsorName} came off the {SponsorSlots.DisplayName(captured).ToLowerInvariant()}."; Refresh(); });
            else
                y = AddRow(label, y);
        }

        y += 12f;
        y = AddHeading("SIGNED DEALS", y);
        if (SponsorBook.Count == 0)
        {
            y = AddRow("Nothing signed. Sponsors' reps work the pit lane on a race weekend — go and talk to them.", y);
        }
        else
        {
            foreach (var deal in SponsorBook.Deals)
            {
                string where = deal.IsPlaced ? SponsorSlots.DisplayName(deal.slot).ToLowerInvariant() : "not on the car";
                string label = $"{deal.sponsorName,-24}  ${deal.perRace:N0}/race  {deal.racesRemaining} races left  ({where})\n" +
                               $"    {deal.ClauseText}";
                var captured = deal;
                y = AddButton(label, _picking == deal.id ? "CHOOSING…" : "PLACE", y,
                              () => { _picking = _picking == captured.id ? 0 : captured.id; _status.text = _picking == 0 ? "" : $"Pick a panel for {captured.sponsorName}."; Refresh(); });
            }
        }

        _rows.sizeDelta = new Vector2(_rows.sizeDelta.x, y);
    }

    void PlacePicked(SponsorSlot slot)
    {
        var deal = SponsorBook.ById(_picking);
        if (deal == null) { _picking = 0; Refresh(); return; }

        var bumped = SponsorBook.InSlot(slot);
        SponsorBook.Place(deal.id, slot);
        _status.text = bumped != null && bumped.id != deal.id
            ? $"{deal.sponsorName} went on the {SponsorSlots.DisplayName(slot).ToLowerInvariant()} — {bumped.sponsorName} came off."
            : $"{deal.sponsorName} went on the {SponsorSlots.DisplayName(slot).ToLowerInvariant()}.";
        _picking = 0;
        Refresh();
    }

    // ---------------------------------------------------------------- build

    float AddHeading(string text, float y)
    {
        var t = MakeText("Heading", _rows, BrandFonts.Display, 24, FontStyle.Normal, TextAnchor.UpperLeft, PixelGUI.Gold);
        t.text = text;
        Place(t.rectTransform, y, 32f);
        return y + 36f;
    }

    float AddRow(string text, float y)
    {
        var t = MakeText("Row", _rows, _font, 24, FontStyle.Normal, TextAnchor.UpperLeft, PixelGUI.TextDim);
        t.text = text;
        float h = text.Contains("\n") ? 56f : 32f;
        Place(t.rectTransform, y, h);
        return y + h + 4f;
    }

    float AddButton(string label, string action, float y, UnityEngine.Events.UnityAction onClick)
    {
        float h = label.Contains("\n") ? 60f : 36f;

        var go = new GameObject("Row", typeof(RectTransform));
        go.transform.SetParent(_rows, false);
        Place(go.GetComponent<RectTransform>(), y, h);

        var text = MakeText("Label", go.GetComponent<RectTransform>(), _font, 24, FontStyle.Normal, TextAnchor.UpperLeft, PixelGUI.Text);
        text.text = label;
        var lrt = text.rectTransform;
        lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(1, 1);
        lrt.offsetMin = new Vector2(0, 0); lrt.offsetMax = new Vector2(-180, 0);

        var btnGo = new GameObject("Action", typeof(RectTransform));
        btnGo.transform.SetParent(go.transform, false);
        var img = btnGo.AddComponent<Image>();
        img.color = PixelGUI.PlateLight;
        var brt = btnGo.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(1, 0); brt.anchorMax = new Vector2(1, 1);
        brt.pivot = new Vector2(1, 0.5f);
        brt.sizeDelta = new Vector2(168, 0);
        brt.anchoredPosition = new Vector2(0, 0);

        var btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var btnText = MakeText("Text", brt, BrandFonts.Display, 16, FontStyle.Normal, TextAnchor.MiddleCenter, PixelGUI.Gold);
        btnText.text = action;
        var trt = btnText.rectTransform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

        return y + h + 6f;
    }

    static void Place(RectTransform rt, float y, float h)
    {
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(0, 0); rt.offsetMax = new Vector2(0, 0);
        rt.anchoredPosition = new Vector2(0, -y);
        rt.sizeDelta = new Vector2(0, h);
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 510;                       // above GaragePanelUI
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        gameObject.AddComponent<GraphicRaycaster>();

        _font = BrandFonts.Body;

        _root = new GameObject("Panel", typeof(RectTransform));
        _root.transform.SetParent(transform, false);
        var dim = _root.AddComponent<Image>();
        // Scrim in the kit's screen void rather than plain black, so the pit lane behind it stays readable.
        dim.color = new Color(PixelGUI.ScreenBase.r, PixelGUI.ScreenBase.g, PixelGUI.ScreenBase.b, 0.85f);
        var drt = _root.GetComponent<RectTransform>();
        drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
        drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;

        var card = new GameObject("Card", typeof(RectTransform));
        card.transform.SetParent(_root.transform, false);
        var cbg = card.AddComponent<Image>();
        cbg.color = PixelGUI.Plate;
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(1120, 760);

        _title = MakeText("Title", card.GetComponent<RectTransform>(), BrandFonts.Display, 32, FontStyle.Normal,
                          TextAnchor.UpperLeft, PixelGUI.Gold);
        var trt = _title.rectTransform;
        trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.offsetMin = new Vector2(44, -96); trt.offsetMax = new Vector2(-44, -32);

        _income = MakeText("Income", card.GetComponent<RectTransform>(), _font, 26, FontStyle.Bold, TextAnchor.UpperRight,
                           PixelGUI.Confirm);
        var irt = _income.rectTransform;
        irt.anchorMin = new Vector2(0, 1); irt.anchorMax = new Vector2(1, 1);
        irt.pivot = new Vector2(0.5f, 1f);
        irt.offsetMin = new Vector2(44, -132); irt.offsetMax = new Vector2(-44, -96);

        // Scrollable list of panels + deals.
        var viewport = new GameObject("Viewport", typeof(RectTransform));
        viewport.transform.SetParent(card.transform, false);
        var vmask = viewport.AddComponent<Image>();
        vmask.color = new Color(1f, 1f, 1f, 0.02f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        var vrt = viewport.GetComponent<RectTransform>();
        vrt.anchorMin = new Vector2(0, 0); vrt.anchorMax = new Vector2(1, 1);
        vrt.offsetMin = new Vector2(44, 96); vrt.offsetMax = new Vector2(-44, -140);

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        _rows = content.GetComponent<RectTransform>();
        _rows.anchorMin = new Vector2(0, 1); _rows.anchorMax = new Vector2(1, 1);
        _rows.pivot = new Vector2(0.5f, 1f);
        _rows.offsetMin = new Vector2(0, 0); _rows.offsetMax = new Vector2(0, 0);

        var scroll = viewport.AddComponent<ScrollRect>();
        scroll.content = _rows;
        scroll.viewport = vrt;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        _status = MakeText("Status", card.GetComponent<RectTransform>(), _font, 24, FontStyle.Normal, TextAnchor.LowerLeft,
                           PixelGUI.TextDim);
        var srt = _status.rectTransform;
        srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(1, 0);
        srt.offsetMin = new Vector2(44, 56); srt.offsetMax = new Vector2(-44, 92);

        var hint = MakeText("Hint", card.GetComponent<RectTransform>(), _font, 22, FontStyle.Normal, TextAnchor.LowerRight,
                            PixelGUI.TextDisabled);
        hint.text = "A sponsor pays nothing until its decal is on a panel  •  Press E to close";
        var hrt = hint.rectTransform;
        hrt.anchorMin = new Vector2(0, 0); hrt.anchorMax = new Vector2(1, 0);
        hrt.offsetMin = new Vector2(44, 20); hrt.offsetMax = new Vector2(-44, 56);
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
