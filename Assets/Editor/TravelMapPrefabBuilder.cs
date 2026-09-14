using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// One-shot generator for the authored travel-map Canvas prefab (Resources/UI/TravelMap.prefab).
// Seeds one TravelNodeMarker per TravelGraph node at its code-defined position, wires the
// TravelMapScreen binder refs, and bakes an initial set of highway lines. After building, the prefab
// is YOURS: open it in Prefab Mode, drag nodes, restyle — Build Prefab refuses to overwrite.
//   - Sync Node Markers: adds markers for nodes added to TravelGraph later (never moves existing ones).
//   - Force Rebuild: deletes the prefab and starts over (loses hand edits!).
//   - Open (Play Mode): opens the map without needing the F9 key (MCP/automation convenience).
public static class TravelMapPrefabBuilder
{
    const string PrefabPath = "Assets/Resources/UI/TravelMap.prefab";
    const float MapW = 1470f, MapH = 950f; // MapPlot size at the 1920x1080 reference resolution

    static readonly Color HeadGreen = new Color(0.4f, 1f, 0.5f);
    static readonly Color BrandYellow = new Color(1f, 0.85f, 0.3f);
    static readonly Color BodyGrey = new Color(0.85f, 0.85f, 0.85f);
    static readonly Color FlavorGrey = new Color(0.75f, 0.75f, 0.8f);

    static Font Mania => AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/mania.ttf");
    static Font NowMedium => AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Now-Medium.otf");
    static Font NowBold => AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Now-Bold.otf");
    static Sprite BlueButton => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUI/blue-button.png");

    [MenuItem("Draftmaster/Travel Map/Build Prefab")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            Debug.LogWarning($"TravelMap prefab already exists at {PrefabPath} — it holds your hand-authored layout. " +
                             "Use Sync Node Markers to add new nodes, or Force Rebuild to start over.");
            return;
        }
        BuildInternal();
    }

    [MenuItem("Draftmaster/Travel Map/Force Rebuild Prefab (loses hand edits)")]
    public static void ForceRebuild()
    {
        AssetDatabase.DeleteAsset(PrefabPath);
        BuildInternal();
    }

    // Re-skins the existing prefab in place: Iron Oval frames, the pixel faces, the palette, header
    // icons and the per-node marker sizes. Layout (where the nodes sit) is NOT touched, so a hand-dragged
    // map survives a restyle — only the look changes. Safe to run again after editing the kit.
    [MenuItem("Draftmaster/Travel Map/Restyle (Iron Oval)")]
    public static void Restyle()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var screen = root.GetComponent<TravelMapScreen>();
            if (screen == null) { Debug.LogError("TravelMap prefab has no TravelMapScreen on its root."); return; }

            ApplyIronOvalSkin(screen);

            // Re-bake the highways so they pick up the new road colour (and any node added since).
            // Cleared here, not in BuildEdges: prefab contents count as assets, so its plain
            // DestroyImmediate(child) is refused and every bake would stack another set of lines.
            for (int i = screen.edgesRoot.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(screen.edgesRoot.GetChild(i).gameObject, true);
            Canvas.ForceUpdateCanvases();
            screen.BuildEdges();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"TravelMap: restyled (Iron Oval), {TravelGraph.Edges.Count} highways re-baked.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Draftmaster/Travel Map/Open (Play Mode)")]
    public static void OpenInPlayMode()
    {
        if (!Application.isPlaying) { Debug.LogWarning("Enter Play Mode first."); return; }
        TravelMapScreen.Open();
    }

    // Adds markers for TravelGraph nodes missing from the prefab; never touches existing markers.
    [MenuItem("Draftmaster/Travel Map/Sync Node Markers")]
    public static void SyncMarkers()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var screen = root.GetComponent<TravelMapScreen>();
            var existing = new System.Collections.Generic.HashSet<string>();
            foreach (var m in screen.nodesRoot.GetComponentsInChildren<TravelNodeMarker>(true))
            {
                existing.Add(m.nodeId);
                if (TravelGraph.Get(m.nodeId) == null)
                    Debug.LogWarning($"Marker '{m.name}' references unknown TravelGraph node '{m.nodeId}' (renamed or removed in code?)");
            }

            int added = 0;
            foreach (var n in TravelGraph.Nodes)
                if (!existing.Contains(n.id)) { CreateMarker(screen.nodesRoot, n); added++; }

            if (added > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"TravelMap: added {added} marker(s). Position them in Prefab Mode, then right-click TravelMapScreen > Rebuild Edges.");
            }
            else Debug.Log("TravelMap: markers already in sync.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // Pushes TravelGraph's code-defined layout back onto the existing markers and re-bakes the highway
    // lines. Use after moving nodes in code (the lattice); it DOES overwrite hand-dragged positions.
    [MenuItem("Draftmaster/Travel Map/Snap Markers To Graph Layout")]
    public static void SnapMarkersToGraph()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var screen = root.GetComponent<TravelMapScreen>();
            int moved = 0, unknown = 0;
            foreach (var m in screen.nodesRoot.GetComponentsInChildren<TravelNodeMarker>(true))
            {
                var n = TravelGraph.Get(m.nodeId);
                if (n == null) { Debug.LogWarning($"Marker '{m.name}' references unknown TravelGraph node '{m.nodeId}'"); unknown++; continue; }
                var rt = (RectTransform)m.transform;
                rt.anchoredPosition = new Vector2(n.pos.x * MapW, -n.pos.y * MapH);
                moved++;
            }

            // Markers added since the prefab was built would otherwise be missing entirely.
            var have = new System.Collections.Generic.HashSet<string>();
            foreach (var m in screen.nodesRoot.GetComponentsInChildren<TravelNodeMarker>(true)) have.Add(m.nodeId);
            foreach (var n in TravelGraph.Nodes)
                if (!have.Contains(n.id)) { CreateMarker(screen.nodesRoot, n); moved++; }

            // Clear the baked highways HERE, not in BuildEdges: objects inside prefab contents count as
            // assets, so its plain DestroyImmediate(child) is refused and every bake would stack another
            // full set of lines on top of the old ones.
            for (int i = screen.edgesRoot.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(screen.edgesRoot.GetChild(i).gameObject, true);

            Canvas.ForceUpdateCanvases();
            screen.BuildEdges();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"TravelMap: snapped {moved} marker(s) to the graph layout, {TravelGraph.Edges.Count} highways baked" +
                      (unknown > 0 ? $" ({unknown} stale marker(s) left alone)" : "."));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void BuildInternal()
    {
        if (Mania == null || NowMedium == null || NowBold == null || BlueButton == null)
        {
            Debug.LogError("TravelMapPrefabBuilder: missing brand assets (mania.ttf / Now-Medium.otf / Now-Bold.otf / blue-button.png).");
            return;
        }

        // Build in the open scene so RectTransform stretch anchors resolve before edges are baked.
        var root = new GameObject("TravelMap", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        try
        {
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500; // above the HUD canvases

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var screen = root.AddComponent<TravelMapScreen>();

            // Backdrop eats clicks to the game behind it.
            var backdrop = MakeImage(root.transform, "Backdrop", new Color(0.05f, 0.07f, 0.10f, 0.97f), true);
            Stretch((RectTransform)backdrop.transform);

            // ----- header -----
            screen.titleLabel = MakeText(root.transform, "TitleLabel", Mania, 22, BrandYellow, TextAnchor.MiddleLeft);
            Place(screen.titleLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -14), new Vector2(1200, 30));

            screen.subLabel = MakeText(root.transform, "SubLabel", NowMedium, 13, BodyGrey, TextAnchor.MiddleLeft);
            Place(screen.subLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -46), new Vector2(900, 20));

            screen.cashLabel = MakeText(root.transform, "CashLabel", Mania, 20, BrandYellow, TextAnchor.MiddleRight);
            Place(screen.cashLabel.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-146, -14), new Vector2(220, 28));

            screen.closeButton = MakeButton(root.transform, "CloseButton", "CLOSE", 14);
            Place((RectTransform)screen.closeButton.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -14), new Vector2(106, 30));

            // ----- map plot -----
            var plot = MakeImage(root.transform, "MapPlot", new Color(1f, 1f, 1f, 0.05f), false);
            Place((RectTransform)plot.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -80), new Vector2(MapW, MapH));

            var edges = new GameObject("Edges", typeof(RectTransform));
            edges.transform.SetParent(plot.transform, false);
            Stretch((RectTransform)edges.transform);
            screen.edgesRoot = (RectTransform)edges.transform;

            var nodes = new GameObject("Nodes", typeof(RectTransform));
            nodes.transform.SetParent(plot.transform, false);
            Stretch((RectTransform)nodes.transform);
            screen.nodesRoot = (RectTransform)nodes.transform;

            foreach (var n in TravelGraph.Nodes)
                CreateMarker(screen.nodesRoot, n);

            // ----- side panel -----
            var panel = MakeImage(root.transform, "SidePanel", new Color(0f, 0f, 0f, 0.55f), true);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = new Vector2(1, 0); prt.anchorMax = new Vector2(1, 1); prt.pivot = new Vector2(1, 1);
            prt.anchoredPosition = new Vector2(-24, -80);
            prt.sizeDelta = new Vector2(360, -130); // 80px top margin, 50px bottom

            var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 10, 10);
            vlg.spacing = 6;
            vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperLeft;

            var carHead = MakeText(panel.transform, "CarHeader", NowBold, 14, HeadGreen, TextAnchor.MiddleLeft, FontStyle.Bold);
            carHead.text = "YOUR CAR";
            carHead.rectTransform.sizeDelta = new Vector2(0, 20);

            screen.carRowsLabel = MakeText(panel.transform, "CarRows", NowBold, 13, Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
            screen.carRowsLabel.rectTransform.sizeDelta = new Vector2(0, 76);

            screen.locationHeader = MakeText(panel.transform, "LocationHeader", NowBold, 14, HeadGreen, TextAnchor.MiddleLeft, FontStyle.Bold);
            screen.locationHeader.rectTransform.sizeDelta = new Vector2(0, 22);

            screen.flavorLabel = MakeText(panel.transform, "FlavorLabel", NowMedium, 12, FlavorGrey, TextAnchor.UpperLeft, FontStyle.Italic);
            screen.flavorLabel.rectTransform.sizeDelta = new Vector2(0, 100);

            screen.actionButton = MakeButton(panel.transform, "ActionButton", "START RACE WEEKEND", 14);
            ((RectTransform)screen.actionButton.transform).sizeDelta = new Vector2(0, 40);

            screen.walkButton = MakeButton(panel.transform, "WalkButton", "STOP & LOOK AROUND", 14);
            ((RectTransform)screen.walkButton.transform).sizeDelta = new Vector2(0, 40);

            screen.shopHeader = MakeText(panel.transform, "ShopHeader", NowBold, 14, HeadGreen, TextAnchor.MiddleLeft, FontStyle.Bold);
            screen.shopHeader.rectTransform.sizeDelta = new Vector2(0, 20);

            var shopRows = new GameObject("ShopRows", typeof(RectTransform));
            shopRows.transform.SetParent(panel.transform, false);
            screen.shopRowsRoot = (RectTransform)shopRows.transform;
            screen.shopRowsRoot.sizeDelta = new Vector2(0, 0);
            var rowsVlg = shopRows.AddComponent<VerticalLayoutGroup>();
            rowsVlg.spacing = 4;
            rowsVlg.childControlWidth = true; rowsVlg.childControlHeight = false;
            rowsVlg.childForceExpandWidth = true; rowsVlg.childForceExpandHeight = false;

            screen.stockRowTemplate = BuildStockRowTemplate(screen.shopRowsRoot);

            // ----- notice -----
            screen.noticeLabel = MakeText(root.transform, "NoticeLabel", NowBold, 14, HeadGreen, TextAnchor.MiddleLeft, FontStyle.Bold);
            var nrt = screen.noticeLabel.rectTransform;
            nrt.anchorMin = new Vector2(0, 0); nrt.anchorMax = new Vector2(1, 0); nrt.pivot = new Vector2(0.5f, 0);
            nrt.anchoredPosition = new Vector2(0, 12);
            nrt.sizeDelta = new Vector2(-48, 24);

            // Same skin a Restyle applies, so a force-rebuilt prefab comes out looking like the game.
            ApplyIronOvalSkin(screen);

            // Bake initial highway lines so the prefab reads as a map in Prefab Mode.
            Canvas.ForceUpdateCanvases();
            screen.BuildEdges();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"TravelMap prefab built at {PrefabPath} ({TravelGraph.Nodes.Count} nodes). Open it in Prefab Mode to adjust the layout.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // Renders the map to a PNG so a restyle can be looked at without entering Play Mode - the canvas is
    // Screen Space Overlay, which never appears in a normal editor screenshot. Dresses the labels with a
    // representative week first, because the prefab's own text is empty until the binder runs.
    [MenuItem("Draftmaster/Travel Map/Preview PNG")]
    public static void PreviewPng()
    {
        const int W = 1920, H = 1080;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogError($"No TravelMap prefab at {PrefabPath}."); return; }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var camGo = new GameObject("TravelMapPreviewCam", typeof(Camera));
        var rt = new RenderTexture(W, H, 24);
        try
        {
            var screen = go.GetComponent<TravelMapScreen>();
            DressForPreview(screen);

            var canvas = go.GetComponent<Canvas>();
            var scaler = go.GetComponent<CanvasScaler>();
            // The scaler reads Screen.*, which is the game view, not this render texture - pin it to 1:1
            // so the preview is the 1920x1080 the canvas is authored against whatever the editor is doing.
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.targetTexture = rt;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;

            Canvas.ForceUpdateCanvases();
            // The layout group has to be rebuilt by hand: ForceRebuildLayoutImmediate only rebuilds the
            // rect it is handed, and nothing marks a freshly instantiated asset dirty outside Play Mode.
            // (At runtime LayoutGroup.OnEnable does this itself, so this is a preview-only concern.)
            if (go.transform.Find("SidePanel") is RectTransform panel)
                LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            Directory.CreateDirectory("Assets/Screenshots");
            const string outPath = "Assets/Screenshots/travelmap_preview.png";
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(outPath);
            Debug.Log($"TravelMap preview written to {outPath}.");
        }
        finally
        {
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(go);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }

    // A plausible mid-career week, so the preview shows the states that matter: where you are, where you
    // are going, what is one stop away, and the factory bench with something on it.
    static void DressForPreview(TravelMapScreen screen)
    {
        screen.titleLabel.text = "THE ROAD TO DAYTONA";
        screen.subLabel.text = "Week 7  -  At Team Factory  -  STOPS LEFT: 3";
        screen.cashLabel.text = "$18,400";
        screen.locationHeader.text = "TEAM FACTORY";
        screen.flavorLabel.text = TravelGraph.Get("team_factory")?.flavor ?? "";
        screen.carRowsLabel.text = "Engine: Fresh 358 Smallblock\nGearbox: Stock\nTires: Soft Compound Set\nChassis: Stock";
        screen.shopHeader.text = "ON THE RACK - YOURS TO TAKE";
        screen.shopHeader.gameObject.SetActive(true);
        screen.noticeLabel.text = "Team-Built Spec Engine collected and fitted.";
        if (screen.actionButton != null) screen.actionButton.GetComponentInChildren<Text>(true).text = "TOW TO DAYTONA ($2,000)";
        if (screen.walkButton != null) screen.walkButton.GetComponentInChildren<Text>(true).text = "WALK THE SHOP FLOOR";

        foreach (var (part, _) in PartCatalog.FactoryStock(7))
        {
            var row = Object.Instantiate(screen.stockRowTemplate, screen.shopRowsRoot);
            row.SetActive(true);
            row.transform.Find("NameLabel").GetComponent<Text>().text = part.name;
            row.transform.Find("EffectLabel").GetComponent<Text>().text = part.EffectSummary() + "\n" + PlayerCarBuild.DescribeSlot(part.slot);
            row.transform.Find("BuyButton").GetComponentInChildren<Text>(true).text = "COLLECT";
        }
        float rowH = ((RectTransform)screen.stockRowTemplate.transform).sizeDelta.y + 4f;
        screen.shopRowsRoot.sizeDelta = new Vector2(screen.shopRowsRoot.sizeDelta.x,
                                                    PartCatalog.FactoryStock(7).Count * rowH);

        foreach (var m in screen.nodesRoot.GetComponentsInChildren<TravelNodeMarker>(true))
        {
            bool here = m.nodeId == "team_factory";
            bool dest = m.nodeId == "Daytona";
            bool near = TravelGraph.AreAdjacent("team_factory", m.nodeId);
            m.halo.enabled = here || dest || near;
            if (here) m.halo.color = new Color32(0xf4, 0xea, 0xd7, 0xff);
            else if (dest) m.halo.color = new Color32(0xe5, 0x48, 0x4d, 0xff);
            else if (near) m.halo.color = new Color32(0xf2, 0xc1, 0x4e, 0x8c);
        }
    }

    // ---------------- Iron Oval skin ----------------
    // The kit (Docs/IronOvalKit.md): deep navy plates inside cream 9-slice frames, gold Silkscreen for
    // headings, VT323 for anything that has to line up in a column, Pixelify Sans for prose, red kept for
    // the one button that commits. This canvas is authored at 1920x1080 rather than the kit's 640x360
    // grid, and its scale factor is 1 at 1080p, so every size here is a WHOLE multiple of the face's own
    // pixel cell (Silkscreen 8, VT323 16, Pixelify 20) and every 9-slice runs at 2x via
    // pixelsPerUnitMultiplier 0.5. Anything off those ladders resamples and the pixels go soft.
    const float Slice2x = 0.5f; // effective PPU = sprite PPU * this, so 0.5 draws the border at 2x

    static readonly Color Gold = new Color32(0xf2, 0xc1, 0x4e, 0xff);
    static readonly Color Cream = new Color32(0xf4, 0xf1, 0xe8, 0xff);
    static readonly Color TextDim = new Color32(0x9a, 0xa3, 0xb8, 0xff);
    static readonly Color Ink = new Color32(0x0a, 0x0b, 0x10, 0xfa);
    static readonly Color PlateNavy = new Color32(0x18, 0x24, 0x42, 0xff);
    static readonly Color Teal = new Color32(0x4e, 0xc9, 0xb0, 0xff);

    static Font Silkscreen => AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Silkscreen-Regular.ttf");
    static Font VT323 => AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/VT323-Regular.ttf");
    static Font Pixelify => AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/PixelifySans-Variable.ttf");
    static Sprite Kit(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);
    static Sprite FrameCream => Kit("Assets/UI/IronOval/frame-cream_9slice.png");
    static Sprite FrameGold => Kit("Assets/UI/IronOval/frame-gold_9slice.png");
    static Sprite ButtonRed => Kit("Assets/UI/IronOval/button-red_9slice.png");
    static Sprite ButtonNavy => Kit("Assets/UI/Pixel/button.png");
    static Sprite PanelFill => Kit("Assets/UI/IronOval/panel-fill_8x8.png");
    static Sprite PanelFillDeep => Kit("Assets/UI/IronOval/panel-fill-deep_8x8.png");
    static Sprite Icon(string name) => Kit("Assets/UI/Pixel/Icons/" + name + ".png");

    public static void ApplyIronOvalSkin(TravelMapScreen screen)
    {
        if (Silkscreen == null || VT323 == null || FrameCream == null)
        {
            Debug.LogError("TravelMap restyle: the pixel kit is missing (Silkscreen / VT323 / Assets/UI/IronOval). " +
                           "Run Draftmaster > Art > Set Up Pixel UI Kit first.");
            return;
        }
        var root = screen.transform;

        EnsureSidePanelParts(screen);

        // ----- backdrop: the void, dithered rather than flat -----
        var backdrop = Find<Image>(root, "Backdrop");
        if (backdrop != null) { backdrop.sprite = PanelFillDeep; backdrop.type = Image.Type.Tiled; backdrop.color = Ink; }

        // ----- header: icon, gold Silkscreen title, VT323 status line -----
        var titleIcon = EnsureImage(root, "TitleIcon", Icon("map"), Gold);
        Place(titleIcon.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -8), new Vector2(32, 32));
        titleIcon.transform.SetSiblingIndex(1); // straight after the backdrop, under everything else

        Style(screen.titleLabel, Silkscreen, 24, Gold, TextAnchor.MiddleLeft);
        Place(screen.titleLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(66, -6), new Vector2(1200, 32));

        Style(screen.subLabel, VT323, 32, TextDim, TextAnchor.MiddleLeft);
        Place(screen.subLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(66, -40), new Vector2(1200, 34));

        var cashIcon = EnsureImage(root, "CashIcon", Icon("money"), Gold);
        Place(cashIcon.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-372, -8), new Vector2(32, 32));

        Style(screen.cashLabel, Silkscreen, 24, Gold, TextAnchor.MiddleRight);
        Place(screen.cashLabel.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-146, -6), new Vector2(220, 32));

        SkinButton(screen.closeButton, ButtonNavy, Cream);
        Place((RectTransform)screen.closeButton.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -6), new Vector2(106, 32));

        // ----- the map itself: a framed navy plate the roads are drawn on -----
        var plot = Find<Image>(root, "MapPlot");
        if (plot != null)
        {
            plot.sprite = PanelFill;
            plot.type = Image.Type.Tiled;
            plot.color = Color.white;
            var frame = EnsureImage(plot.transform, "Frame", FrameCream, Cream);
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = Slice2x;
            frame.fillCenter = false;   // the dithered plate shows through; only the border is drawn
            Stretch(frame.rectTransform);
            frame.transform.SetAsFirstSibling(); // behind the roads and the nodes
        }

        // ----- side panel -----
        var panel = Find<Image>(root, "SidePanel");
        if (panel != null)
        {
            panel.sprite = FrameCream;
            panel.type = Image.Type.Sliced;
            panel.pixelsPerUnitMultiplier = Slice2x;
            panel.color = Color.white;
            var vlg = panel.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) { vlg.padding = new RectOffset(18, 18, 14, 14); vlg.spacing = 8; }
        }

        StyleChild(root, "SidePanel/CarHeader", Silkscreen, 16, Gold);
        Style(screen.carRowsLabel, VT323, 16, Cream, TextAnchor.UpperLeft);
        Style(screen.locationHeader, Silkscreen, 16, Gold, TextAnchor.MiddleLeft);
        Style(screen.flavorLabel, Pixelify != null ? Pixelify : VT323, 20, TextDim, TextAnchor.UpperLeft);
        // Blurbs run to five or six lines at Pixelify's 20px grid and the built rect only held three, so
        // the tail slid under the buttons. Given height rather than a ContentSizeFitter: the layout group
        // does not control child height, so a fitter resizes the rect without re-running the layout and
        // the text lands on top of the rows above it. A fixed box also keeps the buttons in one place.
        screen.flavorLabel.rectTransform.sizeDelta = new Vector2(0, 200);
        Style(screen.shopHeader, Silkscreen, 16, Gold, TextAnchor.MiddleLeft);
        Style(screen.noticeLabel, Silkscreen, 16, Gold, TextAnchor.MiddleCenter);

        // Red is the commit button and nothing else (the kit's one alarm colour); walking around is not
        // a commitment, so it gets the navy plate.
        SkinButton(screen.actionButton, ButtonRed, Cream);
        SkinButton(screen.walkButton, ButtonNavy, Cream);

        // ----- shop / bench row template -----
        if (screen.stockRowTemplate != null)
        {
            var row = screen.stockRowTemplate.transform;
            StyleChild(row, "NameLabel", VT323, 16, Cream);
            StyleChild(row, "EffectLabel", VT323, 16, TextDim);
            var buy = row.Find("BuyButton")?.GetComponent<Button>();
            SkinButton(buy, ButtonNavy, Gold);
        }

        // ----- markers -----
        foreach (var marker in screen.nodesRoot.GetComponentsInChildren<TravelNodeMarker>(true))
            SkinMarker(marker);

        // The vertical layout group's child positions are BAKED into the prefab, and nothing recomputes
        // them for an asset. Without this the panel keeps the positions it was saved with and the rows
        // land on top of each other the moment any of their heights change.
        if (panel != null) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)panel.transform);
    }

    // The prefab predates STOP & LOOK AROUND: the binder grew a walkButton field that the built prefab
    // never had a button for, which left it null and every Refresh throwing on the side panel. Build
    // Prefab refuses to overwrite an authored prefab, so the repair belongs here - and it is a no-op
    // once the button exists, so a restyle can be run as often as you like.
    static void EnsureSidePanelParts(TravelMapScreen screen)
    {
        if (screen.walkButton != null) return;
        var panel = screen.transform.Find("SidePanel");
        if (panel == null) { Debug.LogWarning("TravelMap: no SidePanel to repair."); return; }

        var existing = panel.Find("WalkButton");
        var btn = existing != null ? existing.GetComponent<Button>() : null;
        if (btn == null)
        {
            btn = MakeButton(panel, "WalkButton", "STOP & LOOK AROUND", 14);
            ((RectTransform)btn.transform).sizeDelta = new Vector2(0, 40);
            var action = panel.Find("ActionButton");
            if (action != null) btn.transform.SetSiblingIndex(action.GetSiblingIndex() + 1);
        }
        screen.walkButton = btn;
        Debug.Log("TravelMap: side panel was missing its WalkButton - rebuilt and re-wired.");
    }

    // One dot per node, sized by what it is. The team factory is deliberately the odd one out: twice the
    // size of a circuit, teal where everything else is gold or grey, and wearing the wrench icon — it is
    // the one place on the map you own, and the parts your shop builds are only collectable there.
    static void SkinMarker(TravelNodeMarker marker)
    {
        var n = TravelGraph.Get(marker.nodeId);
        if (n == null || marker.dot == null || marker.halo == null || marker.label == null) return;

        bool factory = n.locationType == TravelLocationType.TeamFactory;
        float s = factory ? 32f : (n.isCircuit ? 12f : 9f);

        var rt = (RectTransform)marker.transform;
        rt.sizeDelta = new Vector2(Mathf.Max(28f, s + 6f), Mathf.Max(28f, s + 6f));

        marker.dot.sprite = factory ? Icon("wrench-set") : null;
        marker.dot.type = Image.Type.Simple;
        if (factory) marker.dot.color = Teal;
        Place(marker.dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(s, s));

        // The ring is a real 9-slice frame rather than a white blob: gold for a node you can drive to,
        // cream for where you are, red for the race you are heading to (tinted by TravelMapScreen).
        marker.halo.sprite = factory ? FrameGold : FrameCream;
        marker.halo.type = Image.Type.Sliced;
        marker.halo.pixelsPerUnitMultiplier = 1f;
        marker.halo.fillCenter = false;
        Place(marker.halo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(s + 10f, s + 10f));

        // Baked text so the prefab reads like the running map in Prefab Mode. TravelMapScreen rewrites
        // it every Refresh anyway - minor locations go back to "?" until you have pulled in once.
        marker.label.text = n.isCircuit || factory ? n.name.ToUpperInvariant() : "?";
        // Names hang under their dot, except the factory's: it sits in the busiest part of the map and
        // its bigger name would run straight into Indianapolis Raceway Park's, so it goes above instead,
        // into the empty country between Chicagoland and Springfield.
        Style(marker.label, Silkscreen, factory ? 16 : 8, Cream, factory ? TextAnchor.LowerCenter : TextAnchor.UpperCenter);
        var lrt = marker.label.rectTransform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
        lrt.pivot = new Vector2(0.5f, factory ? 0f : 1f);
        lrt.anchoredPosition = new Vector2(0f, (s * 0.5f + 8f) * (factory ? 1f : -1f));
        lrt.sizeDelta = new Vector2(180f, 18f);
        marker.label.horizontalOverflow = HorizontalWrapMode.Overflow;
    }

    // ---------------- skin helpers ----------------

    static T Find<T>(Transform root, string path) where T : Component
    {
        var child = root.Find(path);
        return child != null ? child.GetComponent<T>() : null;
    }

    static void Style(Text t, Font font, int size, Color colour, TextAnchor anchor)
    {
        if (t == null) return;
        t.font = font;
        t.fontSize = size;
        t.fontStyle = FontStyle.Normal;  // a bitmap face has no bold cut; faux-bold smears the stems
        t.color = colour;
        t.alignment = anchor;
        t.lineSpacing = 1f;
    }

    static void StyleChild(Transform root, string path, Font font, int size, Color colour)
    {
        var t = Find<Text>(root, path);
        if (t != null) Style(t, font, size, colour, t.alignment);
    }

    static void SkinButton(Button btn, Sprite plate, Color labelColour)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = plate;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = Slice2x;
            img.color = Color.white;
        }
        var label = btn.GetComponentInChildren<Text>(true);
        if (label != null) Style(label, Silkscreen, 16, labelColour, TextAnchor.MiddleCenter);
    }

    // Idempotent: a restyle run twice must not leave two icons stacked on each other.
    static Image EnsureImage(Transform parent, string name, Sprite sprite, Color colour)
    {
        var existing = parent.Find(name);
        var img = existing != null ? existing.GetComponent<Image>() : null;
        if (img == null)
        {
            var go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
        }
        img.sprite = sprite;
        img.color = colour;
        img.raycastTarget = false;
        return img;
    }

    static void CreateMarker(RectTransform nodesRoot, TravelNode n)
    {
        var go = new GameObject($"Node_{n.id}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(TravelNodeMarker));
        var rt = (RectTransform)go.transform;
        rt.SetParent(nodesRoot, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(n.pos.x * MapW, -n.pos.y * MapH);
        rt.sizeDelta = new Vector2(28, 28);

        var hit = go.GetComponent<Image>();          // invisible click target
        hit.color = new Color(1f, 1f, 1f, 0f);
        hit.raycastTarget = true;

        var button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.None; // halo/color signal clickability, not tint

        var marker = go.GetComponent<TravelNodeMarker>();
        marker.nodeId = n.id;
        marker.button = button;

        marker.halo = MakeImage(go.transform, "Halo", Color.white, false);
        Place((RectTransform)marker.halo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
        marker.halo.enabled = false;

        float s = n.isCircuit ? 16f : 11f;
        marker.dot = MakeImage(go.transform, "Dot", n.isCircuit ? BrandYellow : new Color(0.6f, 0.6f, 0.6f), false);
        Place((RectTransform)marker.dot.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(s, s));

        marker.label = MakeText(go.transform, "Label", NowMedium, 10, new Color(0.9f, 0.9f, 0.9f), TextAnchor.UpperCenter);
        var lrt = marker.label.rectTransform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
        lrt.pivot = new Vector2(0.5f, 1f);
        lrt.anchoredPosition = new Vector2(0, -(s * 0.5f + 1f));
        lrt.sizeDelta = new Vector2(140, 16);
        marker.label.horizontalOverflow = HorizontalWrapMode.Overflow;
        marker.label.text = n.isCircuit ? n.name : "?";
    }

    static GameObject BuildStockRowTemplate(RectTransform shopRowsRoot)
    {
        var row = new GameObject("StockRowTemplate", typeof(RectTransform));
        var rt = (RectTransform)row.transform;
        rt.SetParent(shopRowsRoot, false);
        rt.sizeDelta = new Vector2(0, 56);

        var name = MakeText(row.transform, "NameLabel", NowBold, 13, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
        var nrt = name.rectTransform;
        nrt.anchorMin = new Vector2(0, 1); nrt.anchorMax = new Vector2(1, 1); nrt.pivot = new Vector2(0.5f, 1);
        nrt.anchoredPosition = Vector2.zero;
        nrt.sizeDelta = new Vector2(0, 17);

        var effect = MakeText(row.transform, "EffectLabel", NowMedium, 11, BodyGrey, TextAnchor.UpperLeft);
        var ert = effect.rectTransform;
        ert.anchorMin = new Vector2(0, 1); ert.anchorMax = new Vector2(1, 1); ert.pivot = new Vector2(0.5f, 1);
        ert.anchoredPosition = new Vector2(-48, -17);
        ert.sizeDelta = new Vector2(-96, 36); // leave room for the buy button on the right

        var buy = MakeButton(row.transform, "BuyButton", "$0", 12);
        Place((RectTransform)buy.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -17), new Vector2(92, 26));

        row.SetActive(false);
        return row;
    }

    // ---------------- small factories ----------------

    static Image MakeImage(Transform parent, string name, Color color, bool raycastTarget)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = raycastTarget;
        return img;
    }

    static Text MakeText(Transform parent, string name, Font font, int size, Color color, TextAnchor anchor, FontStyle style = FontStyle.Normal)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = anchor;
        t.raycastTarget = false;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static Button MakeButton(Transform parent, string name, string label, int fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = BlueButton;
        img.type = Image.Type.Sliced;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;

        var text = MakeText(go.transform, "Label", NowBold, fontSize, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        Stretch(text.rectTransform);
        text.text = label;
        text.raycastTarget = false;
        return btn;
    }

    static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.pivot = anchorMin == anchorMax ? anchorMin : rt.pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
