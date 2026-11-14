using System.Collections.Generic;
using Draftmaster.MasterDraft;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The Series tab: an overworld of championships (Core SeriesTree, built from the "tree" placements in
// Resources/MasterDraft/Series.json and Challenges.json). The tree fills the left of the screen, read left to right —
// Rookie Midgets at the start, branches splitting toward the different kinds of racing, with the challenges as
// nodes on the branches. Finishing a
// series with enough points opens the nodes after it. The picked node's panel is on the right: a series'
// (SeriesPanel) or a challenge's (ChallengePanel).
//
// The tree is bigger than its window, so the window scrolls to keep the picked node in view. Arrows / d-pad on a
// node walk the tree (picking as they go) rather than the screen's items; Enter on a node steps into its panel.
public partial class MasterDraftUI
{
    const float MapW = 334f, MapH = 236f;                 // the tree's window, the left of the content area
    const float MapTileW = 30f, MapTileH = 30f, MapPad = 6f;   // square tiles
    const int MapVisibleRows = 4;                         // rows the window shows without scrolling
    const float MapColStep = 54f;                         // column pitch
    // Row pitch: MapVisibleRows rows fill the window top to bottom (64 px: tiles 34 px apart).
    const float MapRowStep = (MapH - 2f * MapPad - MapTileH) / (MapVisibleRows - 1);
    const float MapIconSize = 24f;                        // the icon inside, in kit px (art: 24x24 PNG)

    string _treeNode;          // TreeNode.id picked; null = none yet (or the pick is off the tree)
    bool _pickIsChallenge;     // the panel shows _challengeChapter, not _seriesPick
    Vector2 _mapScroll;        // how far the tree is scrolled, x right and y down, in kit px
    SeriesTree _treeOverride, _treeLoaded;

    SeriesTree TreeData => _treeOverride ?? S?.Tree ?? (_treeLoaded ??= SeriesTree.Build(SeriesData, ChallengeData));

    // What the tree is showing, for EditMode tests (SeriesTreeScreenTests).
    public string TreePickedNode => _treeNode;

    // Draw the tree against a profile and catalogues with no session (EditMode tests); nodeId null = the default pick.
    public void BuildOfflineTree(MasterDraftProfile profile, CarCatalog cars, DraftSeriesCatalog series, ChallengeCatalog challenges,
        SeriesTree tree, string nodeId = null)
    {
        _profileOverride = profile;
        _carsOverride = cars;
        _seriesOverride = series;
        _challengesOverride = challenges;
        _treeOverride = tree ?? new SeriesTree(null);
        _busy = false;
        if (_root == null) BuildChrome();
        _tab = Tab.Series;
        _overlay = Overlay.None;
        _treeNode = null;
        _seriesPick = null;
        _challengeChapter = null;
        if (nodeId != null) PickNode(TreeData.Get(nodeId), false);
        else Rebuild();
    }

    // Pick a node by id (EditMode tests and the race-return path).
    public void PickTreeNode(string nodeId) => PickNode(TreeData?.Get(nodeId), false);

    // ------------------------------------------------------------------ the screen

    void BuildSeries()
    {
        var p = P;
        var series = SeriesData;
        var cars = Cars;
        SeriesEligibleCount = 0;
        SeriesCanStart = SeriesInProgress = false;
        if (series == null || series.Menus.Count == 0 || cars == null)
        {
            Text(_content, series == null || series.Menus.Count == 0
                    ? $"No series data (Resources/{DraftSeriesCatalog.ResourcePath}.json)."
                    : $"No car catalogue (Resources/{CarCatalog.ResourcePath}.json).",
                 IronOvalUI.Role.Body, T.danger, Vector2.zero, new Vector2(600f, 20f));
            return;
        }

        var tree = TreeData;
        // Nothing picked yet: the tree's frontier.
        if (_treeNode == null && _seriesPick == null && _challengeChapter == null) PickNode(DefaultNode(tree, p), false, rebuild: false);

        BuildMap(tree, p);

        if (_pickIsChallenge) BuildChallengePanel();
        else
        {
            var picked = series.Get(_seriesPick);
            if (picked != null) SeriesPanel(p, cars, picked);
            else Text(_content, tree == null || tree.Nodes.Count == 0
                          ? "Nothing in Series.json or Challenges.json is placed on the tree."
                          : "Pick a series on the tree.",
                      IronOvalUI.Role.Data, T.textDim, new Vector2(SeriesPanelX, 0f), new Vector2(SeriesPanelW, 32f), wrap: true);
        }
    }

    // Where the tree opens: a championship in progress, else the leftmost node that's open and not cleared, else the
    // start.
    TreeNode DefaultNode(SeriesTree tree, MasterDraftProfile p)
    {
        if (tree == null || p == null) return null;
        TreeNode running = null, open = null, first = null;
        foreach (var n in tree.Nodes)
        {
            if (!Resolves(n)) continue;
            if (first == null || n.col < first.col) first = n;
            var state = tree.State(n, p, SeriesData, ChallengeData);
            if (state == TreeNodeState.Running && n.IsSeries && (running == null || n.col < running.col)) running = n;
            if (state == TreeNodeState.Open && (open == null || n.col < open.col)) open = n;
        }
        return running ?? open ?? first;
    }

    // Pick a node: its series or its event's next chapter in the panel, scrolled into view. focusPanel moves the
    // cursor onto the panel's start button; otherwise it stays on the node.
    void PickNode(TreeNode node, bool focusPanel, bool rebuild = true)
    {
        if (node == null || !Resolves(node)) return;
        _treeNode = node.id;
        if (node.IsSeries)
        {
            _pickIsChallenge = false;
            if (_seriesPick != node.target) { _seriesEntry = null; _seriesQuitArmed = null; }
            _seriesPick = node.target;
        }
        else
        {
            _pickIsChallenge = true;
            var data = ChallengeData;
            var chapter = data.GetChapter(node.target);
            var ev = chapter != null ? data.EventOf(chapter) : data.Get(node.target);
            if (chapter == null && ev != null)
            {
                // Keep the chapter already showing if it's this event's; else its next to win.
                var current = data.GetChapter(_challengeChapter);
                chapter = current != null && current.eventId == ev.id ? current : NextChapter(ev);
            }
            if (_challengeChapter != chapter?.id) _challengeEntry = null;
            _challengeEvent = ev?.id;
            _challengeChapter = chapter?.id;
        }
        ScrollMapTo(node);
        _lastFocusKey = focusPanel ? (node.IsSeries ? "se.go" : "ch.go") : "tn." + node.id;
        if (rebuild && _content != null) Rebuild();
    }

    bool Resolves(TreeNode n) =>
        n != null && (n.IsSeries ? SeriesData?.Get(n.target) != null : SeriesTree.Chapters(n, ChallengeData).Count > 0);

    // The node's name on its tile and in "opens" lines: the series', the chapter's or the event's.
    string NodeName(TreeNode n)
    {
        if (n == null) return "";
        if (n.IsSeries) return SeriesData?.Get(n.target)?.name ?? n.target;
        var data = ChallengeData;
        return data?.GetChapter(n.target)?.name ?? data?.Get(n.target)?.name ?? n.target;
    }

    // Why a node can't be raced yet ("Score 90 pts in Find The Draft or Hit The Front to open it."); null when it can
    // (or it's off the tree).
    string LockReason(TreeNode node)
    {
        var tree = TreeData;
        if (tree == null || node == null || P == null || tree.IsOpen(node, P, SeriesData, ChallengeData)) return null;
        var before = new List<string>();
        foreach (var parent in tree.Parents(node))
            before.Add(parent.IsSeries
                ? $"{SeriesTree.UnlockPoints(parent, SeriesData)} pts in {NodeName(parent)}"
                : $"win {NodeName(parent)}");
        return before.Count == 0 ? "Locked." : $"Locked. Score {string.Join(" or ", before)} to open it.".Replace("Score win", "Win");
    }

    // The panel's title row: the name, and the branch it's on at the right.
    void PanelTitle(string name, TreeNode node, float x, float w)
    {
        string branch = node?.branch ?? "";
        float branchW = branch.Length > 0 ? 86f : 0f;
        Text(_content, name, IronOvalUI.Role.Body, T.gold, new Vector2(x, 0f), new Vector2(w - branchW - 4f, 18f))
            .overflowMode = TextOverflowModes.Ellipsis;
        if (branchW > 0f)
        {
            var b = Text(_content, branch, IronOvalUI.Role.Data, T.textDim, new Vector2(x + w - branchW, -2f), new Vector2(branchW, 16f));
            b.alignment = TextAlignmentOptions.TopRight;
            b.overflowMode = TextOverflowModes.Ellipsis;
        }
    }

    void CollectStarter(TreeNode node)
    {
        var car = TreeData?.CollectStarter(node, P, SeriesData, ChallengeData, Cars);
        if (car == null) { SetStatus("That car can't be collected."); return; }
        var def = Cars?.Get(car.id);
        _seriesEntry = car.id;
        _lastFocusKey = "se.go";
        SaveTeam(def != null ? $"The #{def.number} {def.driver} joins your garage." : "A new car joins your garage.");
    }

    // ------------------------------------------------------------------ the map

    // The tree's grid bounds over the nodes that resolve.
    bool MapBounds(SeriesTree tree, out int minCol, out int maxCol, out int minRow, out int maxRow)
    {
        minCol = minRow = int.MaxValue;
        maxCol = maxRow = int.MinValue;
        if (tree != null)
            foreach (var n in tree.Nodes)
            {
                if (!Resolves(n)) continue;
                minCol = Mathf.Min(minCol, n.col); maxCol = Mathf.Max(maxCol, n.col);
                minRow = Mathf.Min(minRow, n.row); maxRow = Mathf.Max(maxRow, n.row);
            }
        return minCol != int.MaxValue;
    }

    // A tile's top-left in the tree's own space (y down, positive).
    Vector2 TilePos(TreeNode n, int minCol, int minRow) =>
        new Vector2(MapPad + (n.col - minCol) * MapColStep, MapPad + (n.row - minRow) * MapRowStep);

    // Scroll just enough to bring the node's tile into the window.
    void ScrollMapTo(TreeNode node)
    {
        var tree = TreeData;
        if (node == null || !MapBounds(tree, out int minCol, out int maxCol, out int minRow, out int maxRow)) return;
        var pos = TilePos(node, minCol, minRow);
        float margin = MapPad + 4f;
        if (pos.x - _mapScroll.x < margin) _mapScroll.x = pos.x - margin;
        if (pos.x + MapTileW - _mapScroll.x > MapW - margin) _mapScroll.x = pos.x + MapTileW - (MapW - margin);
        if (pos.y - _mapScroll.y < margin) _mapScroll.y = pos.y - margin;
        if (pos.y + MapTileH - _mapScroll.y > MapH - margin) _mapScroll.y = pos.y + MapTileH - (MapH - margin);
        ClampMapScroll(minCol, maxCol, minRow, maxRow);
    }

    void ClampMapScroll(int minCol, int maxCol, int minRow, int maxRow)
    {
        float contentW = MapPad * 2f + (maxCol - minCol) * MapColStep + MapTileW;
        float contentH = MapPad * 2f + (maxRow - minRow) * MapRowStep + MapTileH;
        _mapScroll.x = Mathf.Clamp(_mapScroll.x, 0f, Mathf.Max(0f, contentW - MapW));
        _mapScroll.y = Mathf.Clamp(_mapScroll.y, 0f, Mathf.Max(0f, contentH - MapH));
    }

    void BuildMap(SeriesTree tree, MasterDraftProfile p)
    {
        var window = new GameObject("TreeMap", typeof(RectTransform), typeof(Image), typeof(RectMask2D)).GetComponent<RectTransform>();
        window.SetParent(_content, false);
        TopLeft(window, Vector2.zero, new Vector2(MapW, MapH));
        window.GetComponent<Image>().color = T.screenBase;
        window.GetComponent<Image>().raycastTarget = false;

        if (!MapBounds(tree, out int minCol, out int maxCol, out int minRow, out int maxRow))
        {
            Text(window, "No series tree.", IronOvalUI.Role.Data, T.textDim, new Vector2(8f, -8f), new Vector2(MapW - 16f, 16f));
            return;
        }
        ClampMapScroll(minCol, maxCol, minRow, maxRow);

        var canvas = new GameObject("Tree", typeof(RectTransform)).GetComponent<RectTransform>();
        canvas.SetParent(window, false);
        TopLeft(canvas, new Vector2(-_mapScroll.x, _mapScroll.y), new Vector2(MapW, MapH));

        var states = new Dictionary<string, TreeNodeState>();
        foreach (var n in tree.Nodes)
            if (Resolves(n)) states[n.id] = tree.State(n, p, SeriesData, ChallengeData);

        // Links under the tiles: a straight line from each parent's right edge to the child's left. Lit once the
        // parent is cleared (the way is open). Straight, not elbows: elbows shared one trunk between two columns, so
        // one parent's line ran on into another's and read as a path that isn't there (Legend Cars looked like it
        // led to the Snowball Derby). Straight lines only ever meet at a tile.
        foreach (var n in tree.Nodes)
        {
            if (!states.ContainsKey(n.id)) continue;
            var to = TilePos(n, minCol, minRow);
            foreach (var parent in tree.Parents(n))
            {
                if (!states.TryGetValue(parent.id, out var ps)) continue;
                var from = TilePos(parent, minCol, minRow);
                Color c = ps == TreeNodeState.Cleared ? T.goldShade : T.plateLight;
                float x0 = from.x + MapTileW, y0 = from.y + MapTileH * 0.5f;
                float x1 = to.x, y1 = to.y + MapTileH * 0.5f;
                MapLine(canvas, x0, y0, x1, y1, c);
            }
        }

        // Tiles. The picked one is registered first, so the cursor lands on it when the screen opens.
        var picked = tree.Get(_treeNode);
        var order = new List<TreeNode>();
        if (picked != null && states.ContainsKey(picked.id)) order.Add(picked);
        foreach (var n in tree.Nodes) if (n != picked && states.ContainsKey(n.id)) order.Add(n);
        foreach (var n in order) MapTile(canvas, n, states[n.id], TilePos(n, minCol, minRow), n == picked);

        // Arrows on a node walk the tree, scrolling it, rather than the screen's items.
        _navOverride = dir =>
        {
            string key = FocusKey;
            if (key == null || !key.StartsWith("tn.")) return false;
            var from = tree.Get(key.Substring(3));
            if (from == null) return false;
            TreeNode best = null;
            float bestScore = float.MaxValue;
            var a = new Vector2(from.col * MapColStep, -from.row * MapRowStep);
            foreach (var n in tree.Nodes)
            {
                if (n == from || !states.ContainsKey(n.id)) continue;
                var d = new Vector2(n.col * MapColStep, -n.row * MapRowStep) - a;
                float along = Vector2.Dot(d, dir);
                if (along <= 1f) continue;
                float score = along + (d - along * dir).magnitude * 2f;
                if (score < bestScore) { bestScore = score; best = n; }
            }
            if (best == null) return dir.x > 0.5f ? false : true;   // off the right edge: on into the panel
            PickNode(best, false);
            return true;
        };
    }

    // A 2 px line from (x0, y0) to (x1, y1) in the tree's space (y down): a bar pivoted on its start, rotated.
    void MapLine(RectTransform parent, float x0, float y0, float x1, float y1, Color colour)
    {
        const float thick = 2f;
        var img = new GameObject("Link", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.raycastTarget = false;
        img.color = colour;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(x0, -y0);
        float dx = x1 - x0, dy = y1 - y0;
        rt.sizeDelta = new Vector2(Mathf.Sqrt(dx * dx + dy * dy), thick);
        rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(-dy, dx) * Mathf.Rad2Deg);
    }

    // One node: a square plate the colour of its state with its icon inside (Resources/MasterDraft/TreeIcons,
    // SeriesTree.IconFor; two letters until the icon is drawn), a border for progress (gold running, green cleared),
    // an orange corner for a challenge, dimmed while locked. The name is in the panel. Only tiles wholly inside the
    // window take the cursor.
    void MapTile(RectTransform parent, TreeNode n, TreeNodeState state, Vector2 pos, bool picked)
    {
        var go = new GameObject("Node_" + n.id, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        TopLeft(rt, new Vector2(pos.x, -pos.y), new Vector2(MapTileW, MapTileH));
        bool locked = state == TreeNodeState.Locked;
        go.GetComponent<Image>().color = picked ? T.plateLight : locked ? T.plateDeep : T.plate;

        string icon = SeriesTree.IconFor(n, SeriesData);
        var sprite = TreeIcon(icon);
        float inset = (MapTileW - MapIconSize) * 0.5f;
        if (sprite != null)
        {
            var img = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(rt, false);
            img.raycastTarget = false;
            img.sprite = sprite;
            img.preserveAspect = true;
            img.color = locked ? new Color(0.45f, 0.47f, 0.55f, 0.6f) : Color.white;
            TopLeft(img.rectTransform, new Vector2(inset, -inset), new Vector2(MapIconSize, MapIconSize));
        }
        else
        {
            var code = Text(rt, SeriesTree.IconPlaceholder(icon), IronOvalUI.Role.Data,
                            locked ? T.textDisabled : picked ? T.gold : T.text, Vector2.zero, new Vector2(MapTileW, MapTileH));
            code.alignment = TextAlignmentOptions.Center;
        }

        if (state == TreeNodeState.Running || state == TreeNodeState.Cleared)
            TileBorder(rt, state == TreeNodeState.Cleared ? T.confirm : T.gold);
        if (n.IsChallenge)
        {
            var corner = new GameObject("Challenge", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            corner.transform.SetParent(rt, false);
            corner.raycastTarget = false;
            corner.color = locked ? T.textDisabled : T.caution;
            TopLeft(corner.rectTransform, new Vector2(MapTileW - 6f, 0f), new Vector2(6f, 6f));
        }

        float left = pos.x - _mapScroll.x, top = pos.y - _mapScroll.y;
        bool inView = left >= 0f && left + MapTileW <= MapW && top >= 0f && top + MapTileH <= MapH;
        if (!inView) return;
        // Locked tiles still take the cursor (their panel says what opens them); they just can't be raced.
        AddItem(rt, "tn." + n.id, () => PickNode(n, !locked));
    }

    // A 2 px frame just inside the tile's edge.
    void TileBorder(RectTransform tile, Color colour)
    {
        const float t = 2f;
        void Edge(Vector2 pos, Vector2 size)
        {
            var img = new GameObject("Border", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(tile, false);
            img.raycastTarget = false;
            img.color = colour;
            TopLeft(img.rectTransform, pos, size);
        }
        Edge(Vector2.zero, new Vector2(MapTileW, t));
        Edge(new Vector2(0f, -(MapTileH - t)), new Vector2(MapTileW, t));
        Edge(Vector2.zero, new Vector2(t, MapTileH));
        Edge(new Vector2(MapTileW - t, 0f), new Vector2(t, MapTileH));
    }

    // Icons are loaded once each; a missing one is remembered as missing (and drawn as two letters).
    readonly Dictionary<string, Sprite> _treeIcons = new Dictionary<string, Sprite>();

    Sprite TreeIcon(string icon)
    {
        if (string.IsNullOrEmpty(icon)) return null;
        if (!_treeIcons.TryGetValue(icon, out var sprite))
            _treeIcons[icon] = sprite = Resources.Load<Sprite>(SeriesTree.IconFolder + "/" + icon);
        return sprite;
    }
}
