#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Builds Assets/Scenes/GarageScreen.unity from the Iron Oval design file's GARAGE screen (the updated
// pass: car slot + career + accolades left, driver stats over an XP meter in the middle, car stats and
// fitted parts right).
//
//   Draftmaster > Art > Build Garage Screen Scene
//
// The sheet is 844x390; this is the project's 640x360 canvas at the same proportions — left column 208
// becomes 158, right 212 becomes 161, and the middle takes the rest. Driver-stat cells are gold, car-stat
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
// Generated, not hand-authored: re-running rebuilds it.
public static class IronOvalGarageBuilder
{
    const string kScenePath = "Assets/Scenes/GarageScreen.unity";

    // Row metrics follow the label FACE. This screen was authored against Silkscreen, which is drawn on
    // an 8px cell: a label box was 10px and rows sat 12-24px apart. VT323 is drawn on a 16px cell and
    // cannot be rendered below it without going soft, so its label box is 18px and every pitch grows to
    // match. Expressed as metrics rather than literals so the screen re-flows if the face changes again.
    static float LabelH => IronOvalUI.Snap(PixelUITheme.Instance != null ? PixelUITheme.Instance.display : null, 8) + 2f;
    // One text row to the next, in a list of plain text rows.
    static float TextPitch => LabelH + 1f;
    // A heading to the first row under it.
    static float HeadPitch => LabelH + 2f;

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

        int W = PixelUITheme.ReferenceWidth;
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
        var band = Plate(root, "HeaderBand", theme.plateLight);
        band.anchorMin = new Vector2(0f, 1f);
        band.anchorMax = new Vector2(1f, 1f);
        band.pivot = new Vector2(0.5f, 1f);
        band.sizeDelta = new Vector2(0f, 16f);
        band.anchoredPosition = Vector2.zero;

        var title = IronOvalUI.Label(band, "Title", "GARAGE", IronOvalUI.Role.Header, theme.text);
        Place(title.rectTransform, 8f, -3f, 200f, 14f);

        var cash = IronOvalUI.Label(band, "Cash", "$0", IronOvalUI.Role.Header, theme.gold);
        Anchor(cash.rectTransform, new Vector2(1f, 1f), new Vector2(-8f, -3f), new Vector2(200f, 14f));
        cash.alignment = TextAlignmentOptions.Right;
        ui.cashLabel = cash;

        float top = -22f;
        // Band above, weekend strip below. The strip is one text row tall, so the columns get back the
        // height a taller label face would otherwise eat twice over.
        float stripH = LabelH + 8f;
        float colH = H - 22f - (stripH + 8f);

        // --- left: the car, then who is driving it -----------------------------------------------------
        float leftW = 158f;
        var carSlot = IronOvalUI.ArtSlot(root, "CarSlot", new Vector2(leftW, 50f),
                                         "[ car sprite ]", keyline: true);
        Place(carSlot, 8f, top, leftW, 50f);
        var caption = carSlot.Find("Caption");
        if (caption != null) ui.carSlotCaption = caption.GetComponent<TextMeshProUGUI>();

        var identity = Plate(root, "Identity", theme.plate);
        Place(identity, 8f, top - 54f, leftW, colH - 54f);

        // The block runs off one cursor so a taller label face pushes everything below it down
        // instead of landing on top of the next line.
        float iy = -6f;

        var name = IronOvalUI.Label(identity, "Name", "YOU", IronOvalUI.Role.Body, theme.text);
        Place(name.rectTransform, 8f, iy, leftW - 16f, 20f);
        ui.driverName = name;
        iy -= 22f;

        var number = IronOvalUI.Label(identity, "Number", "#8", IronOvalUI.Role.HeaderSmall, theme.danger);
        Place(number.rectTransform, 8f, iy, 40f, LabelH);
        ui.driverNumber = number;

        var level = IronOvalUI.Label(identity, "Level", "LV 1", IronOvalUI.Role.HeaderSmall, theme.textDim);
        Place(level.rectTransform, 50f, iy, 60f, LabelH);
        ui.driverLevel = level;
        iy -= TextPitch;

        // The rest of the driver's database row: team, then age / manufacturer / nickname on one line.
        var team = IronOvalUI.Label(identity, "Team", "NO TEAM", IronOvalUI.Role.HeaderSmall, theme.text);
        Place(team.rectTransform, 8f, iy, leftW - 16f, LabelH);
        team.textWrappingMode = TextWrappingModes.NoWrap;
        ui.driverTeam = team;
        iy -= TextPitch;

        var bio = IronOvalUI.Label(identity, "Bio", "", IronOvalUI.Role.HeaderSmall, theme.textDim);
        Place(bio.rectTransform, 8f, iy, leftW - 16f, LabelH);
        bio.textWrappingMode = TextWrappingModes.NoWrap;
        ui.driverBio = bio;
        iy -= LabelH + 4f;

        Place(Plate(identity, "Rule0", theme.plateLight), 8f, iy, leftW - 16f, 1f);
        iy -= 6f;

        var careerHead = IronOvalUI.Label(identity, "CareerHead", "CAREER", IronOvalUI.Role.HeaderSmall, theme.gold);
        Place(careerHead.rectTransform, 8f, iy, 100f, LabelH);
        iy -= HeadPitch;

        for (int i = 0; i < 3; i++)
        {
            var label = IronOvalUI.Label(identity, "CareerLabel_" + i, "-", IronOvalUI.Role.HeaderSmall, theme.textDim);
            Place(label.rectTransform, 8f, iy, 90f, LabelH);

            var value = IronOvalUI.Label(identity, "CareerValue_" + i, "0", IronOvalUI.Role.HeaderSmall, theme.text);
            Anchor(value.rectTransform, new Vector2(1f, 1f), new Vector2(-8f, iy), new Vector2(60f, LabelH));
            value.alignment = TextAlignmentOptions.Right;

            ui.careerRows.Add(new GarageScreenUI.TextPair { label = label, value = value });
            iy -= TextPitch;
        }

        iy -= 2f;
        Place(Plate(identity, "Rule1", theme.plateLight), 8f, iy, leftW - 16f, 1f);
        iy -= 6f;

        var accHead = IronOvalUI.Label(identity, "AccoladesHead", "ACCOLADES", IronOvalUI.Role.HeaderSmall, theme.gold);
        Place(accHead.rectTransform, 8f, iy, 120f, LabelH);
        iy -= HeadPitch;

        for (int i = 0; i < 3; i++)
        {
            var line = IronOvalUI.Label(identity, "Accolade_" + i, "", IronOvalUI.Role.HeaderSmall, theme.textDim);
            Place(line.rectTransform, 8f, iy, leftW - 16f, LabelH);
            ui.accoladeLines.Add(line);
            iy -= TextPitch;
        }

        // --- middle: the driver ------------------------------------------------------------------------
        float midX = 8f + leftW + 8f;
        float midW = W - midX - 161f - 16f;
        var middle = Plate(root, "DriverStats", theme.plate);
        Place(middle, midX, top, midW, colH);

        var statsHead = IronOvalUI.Label(middle, "Head", "DRIVER STATS", IronOvalUI.Role.HeaderSmall, theme.gold);
        Place(statsHead.rectTransform, 10f, -6f, 200f, LabelH);

        // Rows are added in DriverAttributeSheet.All order — ability across the top, then the left column
        // (track types, standing) and the right column (craft) — because GarageScreenUI fills them by
        // index. Change the order here and the sheet has to change with it, not the other way round.
        float sy = -(6f + HeadPitch);
        foreach (var attribute in DriverAttributeSheet.Ability)
        {
            var rowRt = Row(middle, "Ability_" + ui.driverStats.Count, 10f, sy, midW - 20f, LabelH);

            var label = IronOvalUI.Label(rowRt, "Label", attribute.Label, IronOvalUI.Role.HeaderSmall, theme.textDim);
            Place(label.rectTransform, 0f, 0f, 90f, LabelH);
            label.textWrappingMode = TextWrappingModes.NoWrap;

            // The bar sits on the text's baseline half of the row, not its full height, so a taller
            // label face does not drag the cells out of line with the number on the right.
            var cells = IronOvalUI.StatCells(rowRt, "Cells", 0, 10);   // gold: the driver's own numbers
            Place(cells, 94f, -(LabelH - 14f), 110f, 10f);

            var value = IronOvalUI.Label(rowRt, "Value", "-", IronOvalUI.Role.HeaderSmall, theme.text);
            Anchor(value.rectTransform, new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(40f, LabelH));
            value.alignment = TextAlignmentOptions.Right;

            ui.driverStats.Add(new GarageScreenUI.StatRow { label = label, cells = cells, value = value });
            sy -= TextPitch;
        }

        Place(Plate(middle, "StatsRule", theme.plateLight), 10f, sy - 2f, midW - 20f, 1f);

        // The sixteen 0-20 skills, two columns of grouped rows. Each row is the car-stats shape — name and
        // number on one line, the cell bar under it — because a 130px column has no room for all three
        // side by side.
        const float colW = 132f;
        const float colGap = 6f;
        float colTop = sy - 8f;
        float colAx = 10f;
        float colBx = colAx + colW + colGap;

        float ay = SkillGroup(ui, middle, theme, "TRACK TYPES", DriverAttributeSheet.TrackTypes, colAx, colTop, colW);
        SkillGroup(ui, middle, theme, "STANDING", DriverAttributeSheet.Standing, colAx, ay, colW);
        SkillGroup(ui, middle, theme, "CRAFT", DriverAttributeSheet.Craft, colBx, colTop, colW);

        // XP row, pinned to the foot of the block as the sheet has it (the columns above are near enough
        // full now, so it can't float below the last one any more).
        float xpY = -(colH - LabelH - 6f);

        var xpHead = IronOvalUI.Label(middle, "XPHead", "FAN APPEAL", IronOvalUI.Role.HeaderSmall, theme.gold);
        Place(xpHead.rectTransform, 10f, xpY, 90f, LabelH);

        var xpTrack = Plate(middle, "XPTrack", theme.plateLight);
        Place(xpTrack, 94f, xpY - (LabelH - 14f), midW - 150f, 9f);
        var xpFill = Plate(xpTrack, "XPFill", theme.confirm);
        Place(xpFill, 0f, 0f, 0f, 9f);
        ui.xpBar = xpFill;

        var xpValue = IronOvalUI.Label(middle, "XPValue", "0/100", IronOvalUI.Role.HeaderSmall, theme.textDim);
        Anchor(xpValue.rectTransform, new Vector2(1f, 1f), new Vector2(-10f, xpY), new Vector2(46f, LabelH));
        xpValue.alignment = TextAlignmentOptions.Right;
        ui.xpLabel = xpValue;

        // --- right: the car's numbers and what's bolted on ---------------------------------------------
        float rightW = 161f;
        var right = Plate(root, "CarStats", theme.plate);
        Place(right, W - rightW - 8f, top, rightW, colH);

        var carHead = IronOvalUI.Label(right, "Head", "CAR STATS", IronOvalUI.Role.HeaderSmall, theme.gold);
        Place(carHead.rectTransform, 8f, -6f, 140f, LabelH);

        float ry = -(6f + HeadPitch);
        float carRowH = LabelH + 10f;
        for (int i = 0; i < 4; i++)
        {
            var rowRt = Row(right, "CarStat_" + i, 8f, ry, rightW - 16f, carRowH);

            var label = IronOvalUI.Label(rowRt, "Label", "-", IronOvalUI.Role.HeaderSmall, theme.textDim);
            Place(label.rectTransform, 0f, 0f, 80f, LabelH);

            var value = IronOvalUI.Label(rowRt, "Value", "-", IronOvalUI.Role.HeaderSmall, theme.text);
            Anchor(value.rectTransform, new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(64f, LabelH));
            value.alignment = TextAlignmentOptions.Right;

            // Telemetry blue: the sheet keeps the car's cells a different colour from the driver's.
            // The bar clears the text row rather than a fixed 11px, so it cannot land on the descenders.
            var cells = IronOvalUI.StatCells(rowRt, "Cells", 0, 10, theme.info);
            Place(cells, 0f, -(LabelH + 1f), rightW - 16f, 8f);

            ui.carStats.Add(new GarageScreenUI.StatRow { label = label, cells = cells, value = value, tint = theme.info });
            ry -= carRowH + 4f;
        }

        Place(Plate(right, "Rule", theme.plateLight), 8f, ry, rightW - 16f, 1f);
        ry -= 8f;

        var partsHead = IronOvalUI.Label(right, "PartsHead", "FITTED PARTS", IronOvalUI.Role.HeaderSmall, theme.gold);
        Place(partsHead.rectTransform, 8f, ry, 140f, LabelH);
        ry -= HeadPitch;

        for (int i = 0; i < 4; i++)
        {
            var slot = IronOvalUI.Label(right, "Slot_" + i, "SLOT", IronOvalUI.Role.HeaderSmall, theme.textDisabled);
            Place(slot.rectTransform, 8f, ry, 60f, LabelH);

            var item = IronOvalUI.Label(right, "Item_" + i, "stock", IronOvalUI.Role.HeaderSmall, theme.text);
            Anchor(item.rectTransform, new Vector2(1f, 1f), new Vector2(-8f, ry), new Vector2(96f, LabelH));
            item.alignment = TextAlignmentOptions.Right;

            ui.partRows.Add(new GarageScreenUI.TextPair { label = slot, value = item });
            ry -= TextPitch;
        }

        // --- weekend strip (the sheet's roster row) ----------------------------------------------------
        var strip = Plate(root, "WeekendStrip", theme.plateDeep);
        strip.anchorMin = new Vector2(0f, 0f);
        strip.anchorMax = new Vector2(1f, 0f);
        strip.pivot = new Vector2(0.5f, 0f);
        strip.sizeDelta = new Vector2(-16f, stripH);
        strip.anchoredPosition = new Vector2(0f, 4f);

        var weekend = IronOvalUI.Label(strip, "Weekend", "NO TRACK SELECTED", IronOvalUI.Role.HeaderSmall, theme.text);
        Place(weekend.rectTransform, 8f, -4f, 300f, LabelH);
        weekend.textWrappingMode = TextWrappingModes.NoWrap;
        ui.weekendLabel = weekend;

        var status = IronOvalUI.Label(strip, "Status", "", IronOvalUI.Role.HeaderSmall, theme.caution);
        Anchor(status.rectTransform, new Vector2(1f, 1f), new Vector2(-152f, -4f), new Vector2(180f, LabelH),
               pivot: new Vector2(1f, 1f));
        status.alignment = TextAlignmentOptions.Right;
        status.textWrappingMode = TextWrappingModes.NoWrap;
        ui.statusLabel = status;

        var race = IronOvalUI.Button(strip, "Race", "RACE", new Vector2(72f, 18f));
        Anchor((RectTransform)race.transform, new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(72f, 18f),
               pivot: new Vector2(1f, 0.5f));
        race.onClick.AddListener(ui.Race);

        // TabButton returns the FACE, stretched inside its shadow parent — position the parent, or the
        // face detaches from its plate and the label wraps a letter per line.
        var back = IronOvalUI.TabButton(strip, "Back", "BACK", new Vector2(56f, 18f));
        Anchor((RectTransform)back.transform.parent, new Vector2(1f, 0.5f), new Vector2(-88f, 0f),
               new Vector2(56f, 18f), pivot: new Vector2(1f, 0.5f));
        back.onClick.AddListener(ui.Back);

        var lines = new GameObject("IronOvalScanlines", typeof(IronOvalScanlines));
        lines.GetComponent<IronOvalScanlines>().opacity = 0.6f;

        // Fill it in once so the saved scene shows real values rather than placeholders.
        ui.Refresh();

        Directory.CreateDirectory(Path.GetDirectoryName(kScenePath));
        EditorSceneManager.SaveScene(scene, kScenePath);
        AssetDatabase.SaveAssets();

        Debug.Log($"Iron Oval: built the garage screen at {kScenePath}.");
    }

    // One headed group of 0-20 skills down a column. Returns the y the next group starts at.
    static float SkillGroup(GarageScreenUI ui, RectTransform parent, PixelUITheme theme, string heading,
                            DriverAttributeSheet.Attribute[] attributes, float x, float y, float w)
    {
        // Name and number on one line, the cell bar under it — that was the sheet's shape while the
        // label face was Silkscreen 8 and a row cost 20px. On VT323's 16px cell the same row costs 28,
        // and sixteen of them no longer fit the column, so the skills read as name + number and the
        // bars stay where there is room for them: the two ability rows above and the car's stats.
        float rowPitch = LabelH;

        var head = IronOvalUI.Label(parent, heading.Replace(' ', '_') + "Head", heading,
                                    IronOvalUI.Role.HeaderSmall, theme.gold);
        Place(head.rectTransform, x, y, w, LabelH);
        y -= HeadPitch;

        foreach (var attribute in attributes)
        {
            var rowRt = Row(parent, "Stat_" + ui.driverStats.Count, x, y, w, LabelH);

            var label = IronOvalUI.Label(rowRt, "Label", attribute.Label, IronOvalUI.Role.HeaderSmall, theme.textDim);
            Place(label.rectTransform, 0f, 0f, w - 30f, LabelH);
            label.textWrappingMode = TextWrappingModes.NoWrap;

            var value = IronOvalUI.Label(rowRt, "Value", "-", IronOvalUI.Role.HeaderSmall, theme.text);
            Anchor(value.rectTransform, new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(28f, LabelH));
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
