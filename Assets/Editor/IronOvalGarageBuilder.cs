#if UNITY_EDITOR
using System.IO;
using Draftmaster.Controls;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Builds Assets/Scenes/GarageScreen.unity — the laptop's DriveScout sheet — to the Draftmaster3_UI_Unity
// design handoff (Designs/.../Layout/DriveScout_PC.json and Reference/DriveScout_PC@3x.png, 2026-11-11):
//
//   Draftmaster > Art > Build Garage Screen Scene
//
// Every position, size and colour below is the design's, on the same 640x360 canvas: a 30px header band
// (title, the series and driver pickers, the [ESC] X close box), then three columns 8px in from every edge
// and 8px apart — car slot over Personal / Career / Accolades on the left (150 wide), Driver Stats in the
// middle (whatever is left), Traits over Relationship on the right (156 wide). Panels are a navy plate with
// a 3px cream border outside it; everything you read is monogram Bold 16 (headings in gold capitals, labels
// sand, values cream), with Silkscreen 8 only for the ESC keycap and the relationship footnote.
//
// Wider than 16:9 (a phone), the side columns hold their edges and the middle and the pickers stretch,
// exactly as the design's anchors do. On a phone the ESC keycap drops out of the close box and the box
// narrows to the X (KeyboardLegend), as DriveScout_Mobile draws it.
//
// The design is one static state; the sheet is live. The middle block is every attribute the Drivers table
// keeps, in DriverAttributeSheet order (GarageScreenUI fills the rows by index), and the parts with no
// place in the design keep one that disturbs it least: the scouting count sits right-aligned on the
// Driver Stats heading's line, a rival's standing and score sit where the design's N/A is, and the
// relationship's line of explanation is the design's footnote at the foot of that panel.
//
// Generated, not hand-authored: re-running rebuilds it. GarageScreenUI's class comment says what each
// field is bound to.
public static class IronOvalGarageBuilder
{
    const string kScenePath = "Assets/Scenes/GarageScreen.unity";
    const string kStripesPath = "Assets/UI/IronOval/stripes-slot_16x16.png";
    const string kMonogramPath = "Assets/Resources/Fonts/Monogram Pixel.asset";
    const string kSilkscreenPath = "Assets/Resources/Fonts/Silkscreen Pixel.asset";

    // monogram's native size, and the box every line of it is set in: the design's 16px row, with the
    // glyphs' own 13px line placed 1px down inside it.
    const float Px = 16f;
    const float RowPitch = 17f;     // one stat / career row to the next
    const float TraitPitch = 19f;   // trait lines are a touch looser
    const float HeadToRow = 21f;    // a gold heading to the first row under it
    const float GroupGap = 9f;      // after a group's last row, before the next heading
    const int ScanlineAlpha = 41;   // the design's scanline tile, out of 255

    static TMP_FontAsset s_mono, s_silk;

    [MenuItem("Draftmaster/Art/Build Garage Screen Scene", priority = 126)]
    public static void Build()
    {
        var theme = PixelUITheme.Instance;
        s_mono = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(kMonogramPath);
        s_silk = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(kSilkscreenPath);
        if (theme == null || s_mono == null || s_silk == null)
        {
            EditorUtility.DisplayDialog("DriveScout",
                "The Monogram / Silkscreen pixel fonts or the theme are missing. Run Draftmaster > Art > " +
                "Set Up Iron Oval Kit first.", "OK");
            return;
        }
        var stripes = StripesSprite();

        int H = PixelUITheme.ReferenceHeight;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("GarageCamera", typeof(Camera));
        var cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = H / 2f / PixelUITheme.ReferencePixelsPerUnit;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = DriveScoutStyle.Background;
        camGo.transform.position = new Vector3(0f, 0f, -10f);

        var canvasGo = new GameObject("GarageCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 1f;
        PixelUI.ApplyScaler(canvasGo.GetComponent<CanvasScaler>());
        var root = (RectTransform)canvasGo.transform;

        var ui = canvasGo.AddComponent<GarageScreenUI>();

        var background = Box(root, "Background", DriveScoutStyle.Background);
        Fill(background, 0f, 0f, 0f, 0f);

        // --- header band ------------------------------------------------------------------------------
        var band = Box(root, "HeaderBand", DriveScoutStyle.HeaderBand);
        band.anchorMin = new Vector2(0f, 1f);
        band.anchorMax = new Vector2(1f, 1f);
        band.pivot = new Vector2(0.5f, 1f);
        band.sizeDelta = new Vector2(0f, 30f);
        band.anchoredPosition = Vector2.zero;
        var bandRule = Box(band, "HeaderRule", DriveScoutStyle.HeaderRule);
        StretchTop(bandRule, 0f, 0f, 27f, 3f);

        Text(band, "Title", "DRIVESCOUT", 10f, 7f, 72f, DriveScoutStyle.Text);
        Text(band, "Version", "v3.1", 88f, 7f, 30f, DriveScoutStyle.Meta);

        // The two pickers: which championship, and which of its entries the sheet is drawn for. The series
        // picker runs from x128 to 27px past the middle, the driver picker from 35px past the middle to
        // 66px short of the right edge, so on a wider screen both grow.
        var seriesPicker = Picker(band, "SeriesPicker", visibleItems: 7);
        Span((RectTransform)seriesPicker.transform, 0f, 0.5f, 128f, 27f, 3f, 22f);
        ui.seriesDropdown = seriesPicker;

        var driverPicker = Picker(band, "DriverPicker", visibleItems: 8);
        Span((RectTransform)driverPicker.transform, 0.5f, 1f, 35f, -66f, 3f, 22f);
        ui.driverDropdown = driverPicker;

        BuildCloseBox(band, (RectTransform)driverPicker.transform);

        // --- the three columns ---------------------------------------------------------------------------
        var body = new GameObject("Body", typeof(RectTransform)).transform as RectTransform;
        body.SetParent(root, false);
        Fill(body, 0f, 0f, 0f, 30f);

        BuildLeftColumn(ui, body, stripes);
        BuildDriverStats(ui, body);
        BuildRightColumn(ui, body);

        var lines = new GameObject("IronOvalScanlines", typeof(IronOvalScanlines));
        // The kit's scanline tile is darker than the design's (107 alpha to 41), so it is laid on lighter.
        float tileAlpha = theme.scanline != null ? TileAlpha(theme.scanline) : 107;
        lines.GetComponent<IronOvalScanlines>().opacity = Mathf.Clamp01(ScanlineAlpha / Mathf.Max(1f, tileAlpha));

        // Fill it in once so the saved scene shows real values rather than placeholders — the pickers
        // included, so the header reads as a real series and a real field in the editor too.
        ui.BuildPickers();
        ui.Refresh();

        Directory.CreateDirectory(Path.GetDirectoryName(kScenePath));
        EditorSceneManager.SaveScene(scene, kScenePath);
        AssetDatabase.SaveAssets();

        Debug.Log($"DriveScout: built the laptop sheet at {kScenePath}.");
    }

    // ------------------------------------------------------------------ header pieces

    // [ESC] X: a slate box with a 2px near-black frame, a cream keycap on a 2px shadow, and the X. Pressing
    // it is Back (GarageScreenUI wires the Button by this object's name — a builder-time AddListener does not
    // survive the save). The keycap is a KeyboardLegend: gone on a phone or with a pad in hand, taking 30px
    // of the box with it and handing them to the driver picker.
    static void BuildCloseBox(RectTransform band, RectTransform driverPicker)
    {
        var go = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
        var box = (RectTransform)go.transform;
        box.SetParent(band, false);
        box.anchorMin = box.anchorMax = new Vector2(1f, 1f);
        box.pivot = new Vector2(1f, 1f);
        box.sizeDelta = new Vector2(52f, 22f);
        box.anchoredPosition = new Vector2(-6f, -3f);

        var plate = go.GetComponent<Image>();
        plate.color = Color.white;
        var button = go.GetComponent<Button>();
        button.targetGraphic = plate;
        var colours = button.colors;
        colours.normalColor = DriveScoutStyle.Edge;
        colours.highlightedColor = Lighter(DriveScoutStyle.Edge);
        colours.selectedColor = Lighter(DriveScoutStyle.Edge);
        colours.pressedColor = DriveScoutStyle.HeaderRule;
        colours.disabledColor = DriveScoutStyle.Edge;
        colours.fadeDuration = 0f;
        button.colors = colours;
        Frame(box, 2f, DriveScoutStyle.HeaderRule);

        var keys = new GameObject("Keys", typeof(RectTransform), typeof(CanvasGroup), typeof(KeyboardLegend));
        var keysRt = (RectTransform)keys.transform;
        keysRt.SetParent(box, false);
        Fill(keysRt, 0f, 0f, 0f, 0f);
        var legend = keys.GetComponent<KeyboardLegend>();
        legend.collapseBox = box;
        legend.collapsedWidth = 22f;
        legend.stretchIntoGap = new[] { driverPicker };

        Place(Box(keysRt, "KeyShadow", DriveScoutStyle.KeyShadow), 6f, 8f, 28f, 10f);
        var cap = Box(keysRt, "Key", DriveScoutStyle.Text);
        Place(cap, 6f, 6f, 28f, 10f);
        Small(cap, "Text_ESC", "ESC", 4f, 0f, 24f, DriveScoutStyle.PickerPlate, trackingPx: 0f);

        // Right-anchored, so it stays put when the box narrows to just the X.
        var x = Text(box, "X", "X", 0f, 4f, 9f, DriveScoutStyle.Text);
        var xr = x.rectTransform;
        xr.anchorMin = xr.anchorMax = xr.pivot = new Vector2(1f, 1f);
        xr.anchoredPosition = new Vector2(-4f, -4f);
    }

    // A picker in the design's paint: near-black plate, 2px slate frame outside it, the value in cream
    // monogram 6px in, and a gold down-arrow 6px from the right. TMP's own control underneath
    // (IronOvalUI.Dropdown), so the list still opens the way TMP expects; only the paint is the design's.
    static TMP_Dropdown Picker(RectTransform band, string name, int visibleItems)
    {
        var dropdown = IronOvalUI.Dropdown(band, name, new Vector2(219f, 22f), visibleItems);
        var rt = (RectTransform)dropdown.transform;

        var colours = dropdown.colors;
        colours.normalColor = DriveScoutStyle.PickerPlate;
        colours.highlightedColor = DriveScoutStyle.HeaderBand;
        colours.selectedColor = DriveScoutStyle.HeaderBand;
        colours.pressedColor = DriveScoutStyle.Background;
        colours.disabledColor = DriveScoutStyle.PickerPlate;
        colours.fadeDuration = 0f;
        dropdown.colors = colours;
        Frame(rt, 2f, DriveScoutStyle.Edge);

        var caption = dropdown.captionText;
        SetFace(caption, s_mono, Px, DriveScoutStyle.Text);
        caption.alignment = TextAlignmentOptions.TopLeft;
        caption.textWrappingMode = TextWrappingModes.NoWrap;
        caption.overflowMode = TextOverflowModes.Truncate;
        StretchTop(caption.rectTransform, 6f, 16f, 4f, Px);

        // The arrow is a 5x3 pixel triangle: the pixel faces in the project carry no ▼.
        var arrow = rt.Find("Arrow") as RectTransform;
        if (arrow != null)
        {
            var caret = arrow.GetComponent<TMP_Text>();
            if (caret != null) Object.DestroyImmediate(caret);
            arrow.anchorMin = arrow.anchorMax = arrow.pivot = new Vector2(1f, 1f);
            arrow.sizeDelta = new Vector2(5f, 3f);
            arrow.anchoredPosition = new Vector2(-6f, -10f);
            Place(Box(arrow, "Row0", DriveScoutStyle.Gold), 0f, 0f, 5f, 1f);
            Place(Box(arrow, "Row1", DriveScoutStyle.Gold), 1f, 1f, 3f, 1f);
            Place(Box(arrow, "Row2", DriveScoutStyle.Gold), 2f, 2f, 1f, 1f);
        }

        // The list: the same plate under the frame, rows in monogram, the slate edge colour on the hovered row.
        var template = dropdown.template;
        template.anchoredPosition = new Vector2(0f, -2f);
        template.GetComponent<Image>().color = DriveScoutStyle.HeaderRule;
        var viewport = template.Find("Viewport");
        if (viewport != null && viewport.TryGetComponent<Image>(out var viewportImage))
            viewportImage.color = DriveScoutStyle.PickerPlate;
        var item = template.Find("Viewport/Content/Item");
        if (item != null && item.TryGetComponent<Toggle>(out var toggle))
        {
            var rows = toggle.colors;
            rows.normalColor = DriveScoutStyle.PickerPlate;
            rows.highlightedColor = DriveScoutStyle.Edge;
            rows.selectedColor = DriveScoutStyle.Edge;
            rows.pressedColor = DriveScoutStyle.Edge;
            rows.disabledColor = DriveScoutStyle.PickerPlate;
            rows.fadeDuration = 0f;
            toggle.colors = rows;
        }
        if (dropdown.itemText != null) SetFace(dropdown.itemText, s_mono, Px, DriveScoutStyle.Text);
        var track = template.Find("Scrollbar");
        if (track != null && track.TryGetComponent<Image>(out var trackImage)) trackImage.color = DriveScoutStyle.HeaderBand;
        var handle = template.Find("Scrollbar/Sliding Area/Handle");
        if (handle != null && handle.TryGetComponent<Image>(out var handleImage)) handleImage.color = DriveScoutStyle.Gold;

        return dropdown;
    }

    // ------------------------------------------------------------------ left: car, Personal / Career / Accolades

    static void BuildLeftColumn(GarageScreenUI ui, RectTransform body, Sprite stripes)
    {
        var column = new GameObject("LeftColumn", typeof(RectTransform)).transform as RectTransform;
        column.SetParent(body, false);
        column.anchorMin = new Vector2(0f, 0f);
        column.anchorMax = new Vector2(0f, 1f);
        column.pivot = new Vector2(0f, 1f);
        column.offsetMin = new Vector2(8f, 8f);
        column.offsetMax = new Vector2(158f, -8f);

        // The car slot: diagonal stripes, a 3px cream border, the car at 2x (GarageScreenUI picks the
        // largest whole-pixel scale that leaves 4px round it — 128x64 here, 11px in and 6px down as drawn).
        var slot = Box(column, "CarSlot", Color.white);
        Place(slot, 0f, 0f, 150f, 76f);
        var slotImage = slot.GetComponent<Image>();
        slotImage.sprite = stripes;
        slotImage.type = Image.Type.Tiled;
        if (stripes == null) slotImage.color = DriveScoutStyle.HeaderBand;
        Frame(slot, 3f, DriveScoutStyle.PanelFrame);

        var carGo = new GameObject("Car", typeof(RectTransform), typeof(Image));
        carGo.transform.SetParent(slot, false);
        var carRt = (RectTransform)carGo.transform;
        carRt.anchorMin = carRt.anchorMax = carRt.pivot = new Vector2(0.5f, 0.5f);
        carRt.anchoredPosition = Vector2.zero;
        carRt.sizeDelta = new Vector2(128f, 64f);
        var carImage = carGo.GetComponent<Image>();
        carImage.raycastTarget = false;
        carImage.preserveAspect = true;
        carImage.enabled = false;
        ui.carImage = carImage;

        var caption = Text(slot, "Caption", "[ No car ]", 0f, 30f, 150f, DriveScoutStyle.Meta);
        caption.alignment = TextAlignmentOptions.Top;
        ui.carSlotCaption = caption;

        var panel = Panel(column, "Identity");
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = new Vector2(0f, -84f);

        // Personal: label left, value right-aligned to the panel's inner edge.
        Head(panel, "PersonalHead", "PERSONAL", 10f, 8f);
        Text(panel, "NameLabel", "Name", 10f, 30f, 28f, DriveScoutStyle.Label);
        // 72px, as the design draws it (a 70px value plus a pixel either side): wide enough for "A. Emerson",
        // not for "Ace Emerson", so GarageScreenUI.FitName initials the first name exactly as the design does.
        ui.driverName = ValueRight(panel, "NameValue", 10f, 30f, 72f);
        ui.driverName.overflowMode = TextOverflowModes.Truncate;
        Text(panel, "AgeLabel", "Age", 10f, 47f, 28f, DriveScoutStyle.Label);
        ui.driverAge = ValueRight(panel, "AgeValue", 10f, 47f, 40f);

        StretchTop(Box(panel, "Rule0", DriveScoutStyle.Edge), 10f, 10f, 68f, 1f);

        Head(panel, "CareerHead", "CAREER", 10f, 75f);
        for (int i = 0; i < 3; i++)
        {
            float y = 97f + i * RowPitch;
            var label = Text(panel, "CareerLabel_" + i, "-", 10f, y, 90f, DriveScoutStyle.Label);
            var value = ValueRight(panel, "CareerValue_" + i, 10f, y, 40f);
            value.text = "0";
            ui.careerRows.Add(new GarageScreenUI.TextPair { label = label, value = value });
        }

        StretchTop(Box(panel, "Rule1", DriveScoutStyle.Edge), 10f, 10f, 152f, 1f);

        Head(panel, "AccoladesHead", "ACCOLADES", 10f, 159f);
        for (int i = 0; i < 3; i++)
        {
            var line = Text(panel, "Accolade_" + i, "", 10f, 181f + i * RowPitch, 130f, DriveScoutStyle.Meta);
            ui.accoladeLines.Add(line);
        }
    }

    // ------------------------------------------------------------------ middle: Driver Stats

    static void BuildDriverStats(GarageScreenUI ui, RectTransform body)
    {
        var panel = Panel(body, "DriverStats");
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.offsetMin = new Vector2(166f, 8f);
        panel.offsetMax = new Vector2(-172f, -8f);

        Head(panel, "Head", "DRIVER STATS", 12f, 8f);

        // How much of a rival's sheet is known, right-aligned on the heading's line. Not in the design (it
        // only shows the player's own sheet, which is always fully known); empty there.
        var scouted = ValueRight(panel, "Scouted", 12f, 9f, 150f);
        scouted.color = DriveScoutStyle.Meta;
        ui.scoutingLabel = scouted;

        // Rows go in DriverAttributeSheet.All order — ability across the top, then the left column (track
        // types, standing) and the right column (craft) — because GarageScreenUI fills them by index.
        float y = 30f;
        foreach (var attribute in DriverAttributeSheet.Ability)
        {
            var row = new GameObject("Ability_" + ui.driverStats.Count, typeof(RectTransform)).transform as RectTransform;
            row.SetParent(panel, false);
            StretchTop(row, 12f, 12f, y, 17f);

            var label = Text(row, "Label", attribute.Name, 0f, 2f, 72f, DriveScoutStyle.Label);

            // Ten 14x10 cells 2px apart, gold up to the rating, slate past it (GarageScreenUI recolours them).
            var cells = new GameObject("Cells", typeof(RectTransform)).transform as RectTransform;
            cells.SetParent(row, false);
            Place(cells, 80f, 4f, 158f, 10f);
            for (int c = 0; c < 10; c++)
                Place(Box(cells, "Cell" + c, c < 8 ? DriveScoutStyle.Gold : DriveScoutStyle.Edge), c * 16f, 0f, 14f, 10f);

            var value = ValueRight(row, "Value", 0f, 2f, 40f);
            value.text = "-";

            ui.driverStats.Add(new GarageScreenUI.StatRow
            {
                label = label, cells = cells, value = value, tint = DriveScoutStyle.Gold, max = attribute.Max,
            });
            y += 18f;
        }

        StretchTop(Box(panel, "StatsRule", DriveScoutStyle.Edge), 12f, 12f, 71f, 1f);

        // The sixteen 0-20 skills in two columns, split 8px either side of the middle.
        var colA = Column(panel, "ColumnA", 0f, 0.5f, 12f, 8f, 79f);
        var colB = Column(panel, "ColumnB", 0.5f, 1f, 8f, 12f, 79f);

        float ay = SkillGroup(ui, colA, "TRACK TYPES", DriverAttributeSheet.TrackTypes, 0f);
        SkillGroup(ui, colA, "STANDING", DriverAttributeSheet.Standing, ay);
        SkillGroup(ui, colB, "CRAFT", DriverAttributeSheet.Craft, 0f);
    }

    // One headed group of 0-20 skills down a column: name left, number right. Returns where the next
    // group's heading goes.
    static float SkillGroup(GarageScreenUI ui, RectTransform column, string heading,
                            DriverAttributeSheet.Attribute[] attributes, float y)
    {
        Head(column, heading.Replace(' ', '_') + "Head", heading, 0f, y);
        float rowY = y + HeadToRow;
        foreach (var attribute in attributes)
        {
            var row = new GameObject("Stat_" + ui.driverStats.Count, typeof(RectTransform)).transform as RectTransform;
            row.SetParent(column, false);
            StretchTop(row, 0f, 0f, rowY, Px);

            var label = Text(row, "Label", attribute.Name, 0f, 1f, 110f, DriveScoutStyle.Label);
            var value = ValueRight(row, "Value", 0f, 1f, 20f);
            value.text = "-";

            ui.driverStats.Add(new GarageScreenUI.StatRow
            {
                label = label, cells = null, value = value, tint = DriveScoutStyle.Gold, max = attribute.Max,
            });
            rowY += RowPitch;
        }
        return rowY + GroupGap;
    }

    // ------------------------------------------------------------------ right: Traits over Relationship

    static void BuildRightColumn(GarageScreenUI ui, RectTransform body)
    {
        var column = new GameObject("RightColumn", typeof(RectTransform)).transform as RectTransform;
        column.SetParent(body, false);
        column.anchorMin = new Vector2(1f, 0f);
        column.anchorMax = new Vector2(1f, 1f);
        column.pivot = new Vector2(1f, 1f);
        column.offsetMin = new Vector2(-164f, 8f);
        column.offsetMax = new Vector2(-8f, -8f);

        // Half the column each, 8px apart.
        var traits = Panel(column, "Traits");
        traits.anchorMin = new Vector2(0f, 0.5f);
        traits.anchorMax = Vector2.one;
        traits.offsetMin = new Vector2(0f, 4f);
        traits.offsetMax = Vector2.zero;

        Head(traits, "TraitsHead", "TRAITS", 10f, 8f);
        for (int i = 0; i < 6; i++)
        {
            float y = 30f + i * TraitPitch;
            var bullet = Box(traits, "TraitBullet_" + i, DriveScoutStyle.Gold);
            Place(bullet, 10f, y + 5f, 6f, 6f);
            var bulletImage = bullet.GetComponent<Image>();
            bulletImage.enabled = false;
            ui.traitBullets.Add(bulletImage);
            ui.traitLines.Add(Text(traits, "Trait_" + i, "", 22f, y + 1f, 124f, DriveScoutStyle.Text));
        }

        var relationship = Panel(column, "Relationship");
        relationship.anchorMin = Vector2.zero;
        relationship.anchorMax = new Vector2(1f, 0.5f);
        relationship.offsetMin = Vector2.zero;
        relationship.offsetMax = new Vector2(0f, -4f);

        Head(relationship, "RelationshipHead", "RELATIONSHIP", 10f, 8f);
        for (int i = 0; i < 2; i++)
        {
            float y = 30f + i * RowPitch;
            var label = Text(relationship, "RelLabel_" + i, "", 10f, y, 70f, DriveScoutStyle.Label);
            var value = ValueRight(relationship, "RelValue_" + i, 10f, y, 80f);
            ui.relationshipRows.Add(new GarageScreenUI.TextPair { label = label, value = value });
        }

        // The footnote: Silkscreen 8, tracked 1px, at the foot of the panel; a longer note grows upward.
        var note = Small(relationship, "RelNote", "", 0f, 0f, 136f, DriveScoutStyle.Meta, trackingPx: 1f);
        var nr = note.rectTransform;
        nr.anchorMin = new Vector2(0f, 0f);
        nr.anchorMax = new Vector2(1f, 0f);
        nr.pivot = new Vector2(0f, 0f);
        nr.offsetMin = new Vector2(10f, 9f);
        nr.offsetMax = new Vector2(-10f, 48f);
        note.alignment = TextAlignmentOptions.BottomLeft;
        note.textWrappingMode = TextWrappingModes.Normal;
        note.lineSpacing = (12f - 10.24f) * 100f / 8f;   // the design's 12px line
        ui.relationshipNote = note;
    }

    // ------------------------------------------------------------------ pieces

    // A panel: the navy plate with a 3px cream border outside it.
    static RectTransform Panel(RectTransform parent, string name)
    {
        var rt = Box(parent, name, DriveScoutStyle.Panel);
        Frame(rt, 3f, DriveScoutStyle.PanelFrame);
        return rt;
    }

    // A gold heading, in capitals as the design sets them.
    static TextMeshProUGUI Head(RectTransform parent, string name, string text, float x, float y)
        => Text(parent, name, text, x, y + 1f, 150f, DriveScoutStyle.Gold);

    // One line of monogram with its box's top-left at (x, y) down from the parent's top-left.
    static TextMeshProUGUI Text(RectTransform parent, string name, string text, float x, float y, float w, Color colour)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        Place(rt, x, y, w, Px);
        var t = go.GetComponent<TextMeshProUGUI>();
        SetFace(t, s_mono, Px, colour);
        t.alignment = TextAlignmentOptions.TopLeft;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        t.text = text;
        return t;
    }

    // A cream value right-aligned `right` px in from the parent's right edge.
    static TextMeshProUGUI ValueRight(RectTransform parent, string name, float right, float y, float w)
    {
        var t = Text(parent, name, "", 0f, y, w, DriveScoutStyle.Text);
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-right, -y);
        t.alignment = TextAlignmentOptions.TopRight;
        return t;
    }

    // Silkscreen 8: the keycap and the footnote.
    static TextMeshProUGUI Small(RectTransform parent, string name, string text, float x, float y, float w,
                                 Color colour, float trackingPx)
    {
        var t = Text(parent, name, text, x, y, w, colour);
        SetFace(t, s_silk, 8f, colour);
        t.rectTransform.sizeDelta = new Vector2(w, 10f);
        t.characterSpacing = trackingPx * 100f / 8f;   // TMP tracks in hundredths of an em
        return t;
    }

    static void SetFace(TMP_Text t, TMP_FontAsset face, float size, Color colour)
    {
        t.font = face;
        if (face.material != null) t.fontSharedMaterial = face.material;
        t.fontSize = size;
        t.fontStyle = FontStyles.Normal;
        t.characterSpacing = 0f;
        t.enableAutoSizing = false;
        t.color = colour;
    }

    // A solid rect (Image with no sprite), not hit by clicks.
    static RectTransform Box(Transform parent, string name, Color colour)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = colour;
        img.raycastTarget = false;
        return (RectTransform)go.transform;
    }

    // A border `b` px thick drawn just outside `rt`, as four strips (the design's px_frame).
    static void Frame(RectTransform rt, float b, Color colour)
    {
        var top = Box(rt, "FrameTop", colour);
        top.anchorMin = new Vector2(0f, 1f); top.anchorMax = new Vector2(1f, 1f);
        top.offsetMin = new Vector2(-b, 0f); top.offsetMax = new Vector2(b, b);
        var bottom = Box(rt, "FrameBottom", colour);
        bottom.anchorMin = new Vector2(0f, 0f); bottom.anchorMax = new Vector2(1f, 0f);
        bottom.offsetMin = new Vector2(-b, -b); bottom.offsetMax = new Vector2(b, 0f);
        var left = Box(rt, "FrameLeft", colour);
        left.anchorMin = new Vector2(0f, 0f); left.anchorMax = new Vector2(0f, 1f);
        left.offsetMin = new Vector2(-b, 0f); left.offsetMax = new Vector2(0f, 0f);
        var right = Box(rt, "FrameRight", colour);
        right.anchorMin = new Vector2(1f, 0f); right.anchorMax = new Vector2(1f, 1f);
        right.offsetMin = new Vector2(0f, 0f); right.offsetMax = new Vector2(b, 0f);
    }

    static Color Lighter(Color c) => Color.Lerp(c, Color.white, 0.12f);

    // The car slot's diagonal stripes, from the design's Sprites/UI/stripes_5ed9a6fe.png, imported as a
    // point-filtered tiling sprite at the canvas's pixels-per-unit so one texel is one UI pixel.
    static Sprite StripesSprite()
    {
        var importer = AssetImporter.GetAtPath(kStripesPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"DriveScout: {kStripesPath} not found — the car slot gets a flat plate.");
            return null;
        }
        bool dirty = importer.textureType != TextureImporterType.Sprite
                     || importer.filterMode != FilterMode.Point
                     || importer.wrapMode != TextureWrapMode.Repeat
                     || !Mathf.Approximately(importer.spritePixelsPerUnit, PixelUITheme.ReferencePixelsPerUnit)
                     || importer.textureCompression != TextureImporterCompression.Uncompressed
                     || importer.mipmapEnabled;
        if (dirty)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = PixelUITheme.ReferencePixelsPerUnit;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;   // Tiled needs a full-rect mesh
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(kStripesPath);
    }

    // The darkest alpha in the kit's scanline tile, so the overlay can be scaled to the design's.
    static float TileAlpha(Sprite sprite)
    {
        try
        {
            float best = 0f;
            foreach (var c in sprite.texture.GetPixels32()) best = Mathf.Max(best, c.a);
            return best > 0f ? best : 107f;
        }
        catch (UnityException) { return 107f; }   // not readable: the tile as shipped
    }

    // ------------------------------------------------------------------ layout helpers

    // Top-left placement in UI pixels: (x, y) is the corner, y measured DOWN from the parent's top.
    static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, -y);
    }

    // Across the parent's width, `left` and `right` in from its edges, `y` down from its top, `h` tall.
    static void StretchTop(RectTransform rt, float left, float right, float y, float h)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.offsetMin = new Vector2(left, -y - h);
        rt.offsetMax = new Vector2(-right, -y);
    }

    // From fraction `from` to `to` of the parent's width (plus offsets), `y` down from its top, `h` tall.
    static void Span(RectTransform rt, float from, float to, float left, float right, float y, float h)
    {
        rt.anchorMin = new Vector2(from, 1f);
        rt.anchorMax = new Vector2(to, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.offsetMin = new Vector2(left, -y - h);
        rt.offsetMax = new Vector2(right, -y);
    }

    // Fills the parent, inset by the four margins.
    static void Fill(RectTransform rt, float left, float right, float bottom, float top)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    // A column from fraction `from` to `to` of the parent's width, inset `left`/`right`, from `y` down to
    // 8px off the parent's foot.
    static RectTransform Column(RectTransform parent, string name, float from, float to, float left, float right, float y)
    {
        var rt = new GameObject(name, typeof(RectTransform)).transform as RectTransform;
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(from, 0f);
        rt.anchorMax = new Vector2(to, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.offsetMin = new Vector2(left, 8f);
        rt.offsetMax = new Vector2(-right, -y);
        return rt;
    }
}
#endif
