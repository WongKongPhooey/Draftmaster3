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
