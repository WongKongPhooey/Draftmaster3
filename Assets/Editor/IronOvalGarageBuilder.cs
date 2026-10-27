#if UNITY_EDITOR
using System.IO;
using Draftmaster.Controls;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Builds Assets/Scenes/GarageScreen.unity from the Iron Oval design file's GARAGE screen (the updated
// pass: car slot + career + accolades left, driver stats over an XP meter in the middle, traits over the
// driver's relationship with the player right).
//
//   Draftmaster > Art > Build Garage Screen Scene
//
// The sheet is 844x390; this is the project's 640x360 canvas at roughly the same proportions — left column 208
// becomes 158, right 212 becomes 145 (narrowed so the stats fit at the Data size), and the middle takes the rest. The canvas matches height, so a
// screen wider than 16:9 (a phone) is more than 640 wide: the left column holds the left edge, the right
// column the right edge, and the middle stretches between them so the whole width is used. Driver-stat cells are gold, car-stat
// cells telemetry blue, exactly as the sheet separates the two.
//
// The middle block is every attribute the Drivers table keeps, in DriverAttributeSheet order: the two
// 0-100 ability ratings full width, then the sixteen 0-20 skills in two grouped columns. GarageScreenUI
// fills those rows by index, so the order rows are added in here is the order that sheet declares.
//
// What each field is bound to (and why nothing is faked) is in GarageScreenUI's class comment. The one
// deliberate substitution: the sheet's bottom strip switches between four team drivers, and this game has
// one car, so that strip carries the weekend and the way out to the track.
//
// The top bar carries the sheet's two pickers — series, then the field entered in that series — which is
// where the design's roster switching ended up: one bar that reaches every driver in the world rather
// than a strip that reaches four.
//
// Generated, not hand-authored: re-running rebuilds it.
public static class IronOvalGarageBuilder
{
    const string kScenePath = "Assets/Scenes/GarageScreen.unity";

    // Row metrics follow the row FACE. The sheet's text is the type scale's Data role (Fixedsys, a 16px
    // cell) rather than the 8px Silkscreen captions it was first authored in, so it reads at twice the
    // size and fills the screen it has: a row box is 18px and every pitch follows it. Expressed as
    // metrics rather than literals so the screen re-flows if the face changes again.
    const IronOvalUI.Role Text = IronOvalUI.Role.Data;
    static float LabelH => IronOvalUI.Snap(PixelUITheme.Instance != null ? PixelUITheme.Instance.data : null,
                                           UIType.KitPx(TypeRole.Data)) + 2f;
    // One text row to the next, in a list of plain text rows.
    // A pixel under the box: Fixedsys draws its glyphs inside the top 16 of the 18, so rows can share one.
    static float TextPitch => LabelH - 1f;
    // A heading to the first row under it.
    static float HeadPitch => LabelH + 1f;
    // The stats block's side margins: as narrow as reads cleanly, since its rows are the tightest fit.
    const float Inset = 6f;

    [MenuItem("Draftmaster/Art/Build Garage Screen Scene", priority = 126)]
    public static void Build()
    {
        var theme = PixelUITheme.Instance;
        if (theme == null || theme.body == null)
        {
            EditorUtility.DisplayDialog("Iron Oval",
                "The theme has no Iron Oval fonts yet. Run Draftmaster > Art > Set Up Iron Oval Kit first.", "OK");
            return;
        }

        int H = PixelUITheme.ReferenceHeight;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("GarageCamera", typeof(Camera));
        var cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = H / 2f / PixelUITheme.ReferencePixelsPerUnit;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = theme.screenBase;
        camGo.transform.position = new Vector3(0f, 0f, -10f);

        var canvasGo = new GameObject("GarageCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 1f;
        PixelUI.ApplyScaler(canvasGo.GetComponent<CanvasScaler>());
        var root = (RectTransform)canvasGo.transform;

        var ui = canvasGo.AddComponent<GarageScreenUI>();

        // --- header band ------------------------------------------------------------------------------
        // The pickers are the standard button's height (UIButton, every screen's), and the band is a picker
        // plus 1px of air either side.
        float pickerH = UIButton.KitPx;
        var band = Plate(root, "HeaderBand", theme.plateLight);
        band.anchorMin = new Vector2(0f, 1f);
        band.anchorMax = new Vector2(1f, 1f);
        band.pivot = new Vector2(0.5f, 1f);
        band.sizeDelta = new Vector2(0f, pickerH + 2f);
        band.anchoredPosition = Vector2.zero;

        var title = IronOvalUI.Label(band, "Title", "Garage", IronOvalUI.Role.Header, theme.text);
        Place(title.rectTransform, 8f, -(band.sizeDelta.y - LabelH) / 2f, 88f, LabelH);
        // The word is one word: let it run past its box rather than break "Garage" into "Garag" + "e".
        title.textWrappingMode = TextWrappingModes.NoWrap;

        // The two pickers: which championship, and which of its entries the sheet is drawn for. The
        // driver list is the longer one — a stock-car field is 38 cars — so it gets the wider plate and
        // the deeper list. GarageScreenUI fills both and redraws the sheet when either changes.
        float pickerY = -(band.sizeDelta.y - pickerH) / 2f;
        var seriesPicker = IronOvalUI.Dropdown(band, "SeriesPicker", new Vector2(196f, pickerH), visibleItems: 7);
        Place((RectTransform)seriesPicker.transform, 100f, pickerY, 196f, pickerH);
        ui.seriesDropdown = seriesPicker;

        var driverPicker = IronOvalUI.Dropdown(band, "DriverPicker", new Vector2(220f, pickerH), visibleItems: 8);
        Place((RectTransform)driverPicker.transform, 304f, pickerY, 220f, pickerH);
        ui.driverDropdown = driverPicker;

        float top = -(band.sizeDelta.y + 4f);
        // Band above, weekend strip below. The strip is one button tall (UIButton, every screen's) with 1px
        // above and below, so the columns keep as much of the height as they can.
        float stripH = Mathf.Max(LabelH + 8f, UIButton.KitPx + 2f);
        float colH = H + top - (stripH + 8f);

        // --- left: the car, then who is driving it -----------------------------------------------------
        float leftW = 158f;
        // Tall enough for a 64x32 car at 2x with 4px of plate round it, so the car fills the box.
        const float carSlotH = 72f;
        var carSlot = IronOvalUI.ArtSlot(root, "CarSlot", new Vector2(leftW, carSlotH),
                                         "[ No car ]", keyline: true);
        Place(carSlot, 8f, top, leftW, carSlotH);
        // A real car sits in it now, so the inside is a plain deep plate rather than the placeholder hatch.
        var hatch = carSlot.Find("Hatch");
        if (hatch != null && hatch.TryGetComponent<Image>(out var hatchImage))
        {
            hatchImage.sprite = null;
            hatchImage.type = Image.Type.Simple;
            hatchImage.color = theme.plateDeep;
        }
        // The viewed driver's car, centred; GarageScreenUI sizes it to a whole-pixel scale of its sprite.
        var carGo = new GameObject("Car", typeof(RectTransform), typeof(Image));
        carGo.transform.SetParent(carSlot, false);
        var carRt = (RectTransform)carGo.transform;
        carRt.anchorMin = carRt.anchorMax = carRt.pivot = new Vector2(0.5f, 0.5f);
        carRt.anchoredPosition = Vector2.zero;
        carRt.sizeDelta = new Vector2(64f, 32f);
        var carImage = carGo.GetComponent<Image>();
        carImage.raycastTarget = false;
        carImage.preserveAspect = true;
        carImage.enabled = false;
        ui.carImage = carImage;
        var caption = carSlot.Find("Caption");
        if (caption != null) ui.carSlotCaption = caption.GetComponent<TextMeshProUGUI>();

        var identity = Plate(root, "Identity", theme.plate);
        Place(identity, 8f, top - carSlotH - 4f, leftW, colH - carSlotH - 4f);

        // The block runs off one cursor so a taller label face pushes everything below it down
        // instead of landing on top of the next line.
        float iy = -4f;

        var name = IronOvalUI.Label(identity, "Name", "You", IronOvalUI.Role.Body, theme.text);
        // Pixelify draws its 16 on a 20px grid, so the name's row is 20 tall rather than a Data row's 18.
        Place(name.rectTransform, 8f, iy, leftW - 16f, 20f);
        name.textWrappingMode = TextWrappingModes.NoWrap;
        ui.driverName = name;
        iy -= 20f;

        var number = IronOvalUI.Label(identity, "Number", "#8", Text, theme.danger);
        Place(number.rectTransform, 8f, iy, 40f, LabelH);
        ui.driverNumber = number;

        var level = IronOvalUI.Label(identity, "Level", "Level 1", Text, theme.textDim);
        Place(level.rectTransform, 50f, iy, 90f, LabelH);
        ui.driverLevel = level;
        iy -= TextPitch;

        // The rest of the driver's database row: team, then age / manufacturer / nickname on one line.
        var team = IronOvalUI.Label(identity, "Team", "No team", Text, theme.text);
        Place(team.rectTransform, 8f, iy, leftW - 16f, LabelH);
        team.textWrappingMode = TextWrappingModes.NoWrap;
        team.overflowMode = TextOverflowModes.Ellipsis;   // a long team or nickname ends in "..." inside the panel
        ui.driverTeam = team;
        iy -= TextPitch;

        var bio = IronOvalUI.Label(identity, "Bio", "", Text, theme.textDim);
        Place(bio.rectTransform, 8f, iy, leftW - 16f, LabelH);
        bio.textWrappingMode = TextWrappingModes.NoWrap;
        bio.overflowMode = TextOverflowModes.Ellipsis;   // a long team or nickname ends in "..." inside the panel
        ui.driverBio = bio;
        iy -= LabelH + 2f;

        Place(Plate(identity, "Rule0", theme.plateLight), 8f, iy, leftW - 16f, 1f);
        iy -= 4f;

        var careerHead = IronOvalUI.Label(identity, "CareerHead", "Career", Text, theme.gold);
        Place(careerHead.rectTransform, 8f, iy, 100f, LabelH);
        iy -= HeadPitch;

        for (int i = 0; i < 3; i++)
        {
            var label = IronOvalUI.Label(identity, "CareerLabel_" + i, "-", Text, theme.textDim);
            Place(label.rectTransform, 8f, iy, 112f, LabelH);
            label.textWrappingMode = TextWrappingModes.NoWrap;

            var value = IronOvalUI.Label(identity, "CareerValue_" + i, "0", Text, theme.text);
            Anchor(value.rectTransform, new Vector2(1f, 1f), new Vector2(-8f, iy), new Vector2(40f, LabelH));
            value.alignment = TextAlignmentOptions.Right;

            ui.careerRows.Add(new GarageScreenUI.TextPair { label = label, value = value });
            iy -= TextPitch;
        }

        iy -= 2f;
        Place(Plate(identity, "Rule1", theme.plateLight), 8f, iy, leftW - 16f, 1f);
        iy -= 4f;

        var accHead = IronOvalUI.Label(identity, "AccoladesHead", "Accolades", Text, theme.gold);
        Place(accHead.rectTransform, 8f, iy, 120f, LabelH);
        iy -= HeadPitch;

        // As many lines as the block has room for under the heading, up to three.
        float identityH = colH - carSlotH - 4f;
        int accolades = Mathf.Clamp(Mathf.FloorToInt((identityH + iy - 2f) / TextPitch), 1, 3);
        for (int i = 0; i < accolades; i++)
        {
            var line = IronOvalUI.Label(identity, "Accolade_" + i, "", Text, theme.textDim);
            Place(line.rectTransform, 8f, iy, leftW - 16f, LabelH);
            line.textWrappingMode = TextWrappingModes.NoWrap;
            ui.accoladeLines.Add(line);
            iy -= TextPitch;
        }

        // --- middle: the driver ------------------------------------------------------------------------
        float rightW = 145f;   // traits and relationship read in one short column; the stats need the width more
        float midX = 8f + leftW + 8f;
        float midRight = rightW + 16f;
        var middle = Plate(root, "DriverStats", theme.plate);
        Stretch(middle, midX, midRight, top, colH);

        var statsHead = IronOvalUI.Label(middle, "Head", "Driver Stats", Text, theme.gold);
        Place(statsHead.rectTransform, Inset, -6f, 120f, LabelH);
        statsHead.textWrappingMode = TextWrappingModes.NoWrap;

        // How much of a rival's sheet is known, on the heading's line, right-aligned.
        var scouted = IronOvalUI.Label(middle, "Scouted", "", Text, theme.textDim);
        Stretch(scouted.rectTransform, 130f, Inset, -6f, LabelH);
        scouted.alignment = TextAlignmentOptions.Right;
        scouted.textWrappingMode = TextWrappingModes.NoWrap;
        ui.scoutingLabel = scouted;

        // Rows are added in DriverAttributeSheet.All order — ability across the top, then the left column
        // (track types, standing) and the right column (craft) — because GarageScreenUI fills them by
        // index. Change the order here and the sheet has to change with it, not the other way round.
        float sy = -(6f + HeadPitch);
        foreach (var attribute in DriverAttributeSheet.Ability)
        {
            var rowRt = Row(middle, "Ability_" + ui.driverStats.Count, Inset, sy, 0f, LabelH);
            Stretch(rowRt, Inset, Inset, sy, LabelH);

            var label = IronOvalUI.Label(rowRt, "Label", attribute.Name, Text, theme.textDim);
            Place(label.rectTransform, 0f, 0f, 90f, LabelH);
            label.textWrappingMode = TextWrappingModes.NoWrap;

            // The bar is centred on the row, so it lines up with the text either side of it.
            var cells = IronOvalUI.StatCells(rowRt, "Cells", 0, 10);   // gold: the driver's own numbers
            Place(cells, 94f, -(LabelH - 10f) / 2f, 110f, 10f);

            var value = IronOvalUI.Label(rowRt, "Value", "-", Text, theme.text);
            Anchor(value.rectTransform, new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(40f, LabelH));
            value.alignment = TextAlignmentOptions.Right;

            ui.driverStats.Add(new GarageScreenUI.StatRow { label = label, cells = cells, value = value });
            sy -= TextPitch;
        }

        Stretch(Plate(middle, "StatsRule", theme.plateLight), Inset, Inset, sy - 2f, 1f);

        // The sixteen 0-20 skills, two columns of grouped rows, splitting the block's width so a wider
        // screen widens both. The left column's longest names ("Sponsor Appeal", "Superspeedway") are a
        // character or two longer than the right's ("Adaptability"), so it takes a little more than half.
        float colTop = sy - 6f;
        const float split = 0.53f;
        var colA = Column(middle, "ColumnA", 0f, split, Inset, 5f, colTop, colH);
        var colB = Column(middle, "ColumnB", split, 1f, 5f, Inset, colTop, colH);

        float ay = SkillGroup(ui, colA, theme, "TRACK TYPES", DriverAttributeSheet.TrackTypes, 0f);
        SkillGroup(ui, colA, theme, "STANDING", DriverAttributeSheet.Standing, ay);
        SkillGroup(ui, colB, theme, "CRAFT", DriverAttributeSheet.Craft, 0f);

        // XP row, pinned to the foot of the block as the sheet has it (the columns above are near enough
        // full now, so it can't float below the last one any more).
        float xpY = -(colH - LabelH - 6f);

        var xpHead = IronOvalUI.Label(middle, "XPHead", "Fan Appeal", Text, theme.gold);
        Place(xpHead.rectTransform, Inset, xpY, 96f, LabelH);
        xpHead.textWrappingMode = TextWrappingModes.NoWrap;

        var xpTrack = Plate(middle, "XPTrack", theme.plateLight);
        Stretch(xpTrack, Inset + 100f, 72f, xpY - (LabelH - 9f) / 2f, 9f);
        var xpFill = Plate(xpTrack, "XPFill", theme.confirm);
        Place(xpFill, 0f, 0f, 0f, 9f);
        ui.xpBar = xpFill;

        var xpValue = IronOvalUI.Label(middle, "XPValue", "0/100", Text, theme.textDim);
        Anchor(xpValue.rectTransform, new Vector2(1f, 1f), new Vector2(-Inset, xpY), new Vector2(60f, LabelH));
        xpValue.alignment = TextAlignmentOptions.Right;
        ui.xpLabel = xpValue;

        // --- right: traits over the relationship, half the column each ----------------------------------
        float halfH = Mathf.Floor((colH - 4f) / 2f);
        var traits = Plate(root, "Traits", theme.plate);
        Anchor(traits, new Vector2(1f, 1f), new Vector2(-8f, top), new Vector2(rightW, halfH));

        float ty = -6f;
        var traitsHead = IronOvalUI.Label(traits, "TraitsHead", "Traits", Text, theme.gold);
        Place(traitsHead.rectTransform, 8f, ty, 140f, LabelH);
        ty -= HeadPitch;
        for (int i = 0; i < 6; i++)
        {
            var line = IronOvalUI.Label(traits, "Trait_" + i, "", Text, theme.text);
            Place(line.rectTransform, 8f, ty, rightW - 16f, LabelH);
            line.textWrappingMode = TextWrappingModes.NoWrap;
            ui.traitLines.Add(line);
            ty -= TextPitch;
        }

        var relationship = Plate(root, "Relationship", theme.plate);
        Anchor(relationship, new Vector2(1f, 1f), new Vector2(-8f, top - halfH - 4f),
               new Vector2(rightW, colH - halfH - 4f));

        float ly = -6f;
        var relHead = IronOvalUI.Label(relationship, "RelationshipHead", "Relationship", Text, theme.gold);
        Place(relHead.rectTransform, 8f, ly, 140f, LabelH);
        ly -= HeadPitch;
        for (int i = 0; i < 2; i++)
        {
            var label = IronOvalUI.Label(relationship, "RelLabel_" + i, "", Text, theme.textDim);
            Place(label.rectTransform, 8f, ly, 70f, LabelH);
            var value = IronOvalUI.Label(relationship, "RelValue_" + i, "", Text, theme.text);
            Anchor(value.rectTransform, new Vector2(1f, 1f), new Vector2(-8f, ly), new Vector2(80f, LabelH));
            value.alignment = TextAlignmentOptions.Right;
            ui.relationshipRows.Add(new GarageScreenUI.TextPair { label = label, value = value });
            ly -= TextPitch;
        }
        ly -= 4f;
        var relNote = IronOvalUI.Label(relationship, "RelNote", "", Text, theme.textDim);
        Place(relNote.rectTransform, 8f, ly, rightW - 16f, LabelH * 4f);
        relNote.textWrappingMode = TextWrappingModes.Normal;
        relNote.alignment = TextAlignmentOptions.TopLeft;
        ui.relationshipNote = relNote;

        // --- weekend strip (the sheet's roster row) ----------------------------------------------------
        var strip = Plate(root, "WeekendStrip", theme.plateDeep);
        strip.anchorMin = new Vector2(0f, 0f);
        strip.anchorMax = new Vector2(1f, 0f);
        strip.pivot = new Vector2(0.5f, 0f);
        strip.sizeDelta = new Vector2(-16f, stripH);
        strip.anchoredPosition = new Vector2(0f, 4f);

        var weekend = IronOvalUI.Label(strip, "Weekend", "No track selected", Text, theme.text);
        Place(weekend.rectTransform, 8f, -(stripH - LabelH) / 2f, 300f, LabelH);
        weekend.textWrappingMode = TextWrappingModes.NoWrap;
        ui.weekendLabel = weekend;

        var status = IronOvalUI.Label(strip, "Status", "", Text, theme.caution);
        Anchor(status.rectTransform, new Vector2(1f, 1f), new Vector2(-152f, -(stripH - LabelH) / 2f), new Vector2(180f, LabelH),
               pivot: new Vector2(1f, 1f));
        status.alignment = TextAlignmentOptions.Right;
        status.textWrappingMode = TextWrappingModes.NoWrap;
        ui.statusLabel = status;

        var race = IronOvalUI.Button(strip, "Race", "Race", new Vector2(72f, 18f));
        Anchor((RectTransform)race.transform, new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(72f, UIButton.KitPx),
               pivot: new Vector2(1f, 0.5f));
        race.onClick.AddListener(ui.Race);

        // TabButton returns the FACE, stretched inside its shadow parent — position the parent, or the
        // face detaches from its plate and the label wraps a letter per line.
        var back = IronOvalUI.TabButton(strip, "Back", "Back", new Vector2(56f, 18f));
        Anchor((RectTransform)back.transform.parent, new Vector2(1f, 0.5f), new Vector2(-88f, 0f),
               new Vector2(56f, UIButton.KitPx), pivot: new Vector2(1f, 0.5f));
        back.onClick.AddListener(ui.Back);

        var lines = new GameObject("IronOvalScanlines", typeof(IronOvalScanlines));
        lines.GetComponent<IronOvalScanlines>().opacity = 0.6f;

        // Fill it in once so the saved scene shows real values rather than placeholders — the pickers
        // included, so the header reads as a real series and a real field in the editor too.
        ui.BuildPickers();
        ui.Refresh();

        Directory.CreateDirectory(Path.GetDirectoryName(kScenePath));
        EditorSceneManager.SaveScene(scene, kScenePath);
        AssetDatabase.SaveAssets();

        Debug.Log($"Iron Oval: built the garage screen at {kScenePath}.");
    }

    // One headed group of 0-20 skills down a column. Returns the y the next group starts at.
    static float SkillGroup(GarageScreenUI ui, RectTransform parent, PixelUITheme theme, string heading,
                            DriverAttributeSheet.Attribute[] attributes, float y)
    {
        // Name and number on one line, the cell bar under it — that was the sheet's shape while the
        // label face was Silkscreen 8 and a row cost 20px. On VT323's 16px cell the same row costs 28,
        // and sixteen of them no longer fit the column, so the skills read as name + number and the
        // bars stay where there is room for them: the two ability rows above and the car's stats.
        float rowPitch = LabelH;

        var head = IronOvalUI.Label(parent, heading.Replace(' ', '_') + "Head", UIText.TitleCase(heading),
                                    Text, theme.gold);
        Stretch(head.rectTransform, 0f, 0f, y, LabelH);
        y -= HeadPitch;

        foreach (var attribute in attributes)
        {
            var rowRt = Row(parent, "Stat_" + ui.driverStats.Count, 0f, y, 0f, LabelH);
            Stretch(rowRt, 0f, 0f, y, LabelH);

            var label = IronOvalUI.Label(rowRt, "Label", attribute.Name, Text, theme.textDim);
            Stretch(label.rectTransform, 0f, 18f, 0f, LabelH);
            label.textWrappingMode = TextWrappingModes.NoWrap;

            // Two digits ("20", "??") and nothing more, so the name gets every pixel left.
            var value = IronOvalUI.Label(rowRt, "Value", "-", Text, theme.text);
            Anchor(value.rectTransform, new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(18f, LabelH));
            value.alignment = TextAlignmentOptions.Right;

            ui.driverStats.Add(new GarageScreenUI.StatRow { label = label, cells = null, value = value });
            y -= rowPitch;
        }

        return y - 4f;   // a group's worth of air before the next heading
    }

    // ------------------------------------------------------------------ layout helpers

    static RectTransform Row(Transform parent, string name, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        Place(rt, x, y, w, h);
        return rt;
    }

    static RectTransform Plate(Transform parent, string name, Color colour)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = colour;
        img.raycastTarget = false;
        return (RectTransform)go.transform;
    }

    // Top-left placement in UI pixels: (x, y) is the corner, y running negative down the column.
    static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
    }

    // Across the parent's width, `left` and `right` in from its edges, at (y, h) as Place has them.
    static void Stretch(RectTransform rt, float left, float right, float y, float h)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.offsetMin = new Vector2(left, y - h);
        rt.offsetMax = new Vector2(-right, y);
    }

    // A column spanning the parent's width from fraction `from` to `to`, inset `left`/`right`, its top at y.
    static RectTransform Column(Transform parent, string name, float from, float to, float left, float right, float y, float h)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(from, 1f);
        rt.anchorMax = new Vector2(to, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.offsetMin = new Vector2(left, y - h);
        rt.offsetMax = new Vector2(-right, y);
        return rt;
    }

    static void Anchor(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot ?? anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
    }
}
#endif
