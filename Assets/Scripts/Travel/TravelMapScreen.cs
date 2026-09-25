using System.Collections.Generic;
using System.Text;
using Draftmaster.Controls;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The between-races road trip: a fullscreen authored Canvas (Resources/UI/TravelMap.prefab) with this
// thin binder on the root. Node layout is editable in Prefab Mode — each node is a TravelNodeMarker
// child whose RectTransform position is the node's map position; highway lines are rebuilt from marker
// positions at runtime (and via the "Rebuild Edges" context menu while editing). TravelGraph supplies
// topology and shop data only. Flow (unchanged from the old IMGUI version):
//   1. Choose the next race venue (any circuit node). Stop budget = direct route + DetourAllowance.
//   2. Drive node to node (click an adjacent node, 1 stop each). Minor locations show a side panel —
//      junkyards sell a weekly random salvage roll, engine shops a fixed catalog. Buying installs.
//      Your own Team Factory sits in the middle of the map (the teal wrench): the parts your shop has
//      built since your last visit are free there, and they only leave the rack if you drive out.
//   3. At the destination: START RACE WEEKEND resets the weekend and loads the circuit's scene (falls
//      back to reloading the current scene when the circuit isn't in the build).
// The board is 75 dots across the whole country, which at one-to-one is a wall of pinheads nobody can
// read. So the plot is a window onto the map rather than the whole of it: it opens zoomed in on this
// week's race (the destination, or wherever you are parked when there is no destination — during a race
// weekend that is the circuit itself), scroll wheel zooms about the pointer, right or middle drag pans,
// a two-finger pinch zooms and pans on a phone, the pad's triggers zoom (RT in, LT out), and it glides after the player as they hop from node to node. See OpenView / ApplyView.
// Out of stops away from the destination -> tow (costs money). State persists in TravelState, so closing
// the panel or the game mid-trip loses nothing. F9 opens the map any time (dev convenience).
// The prefab is generated once by Draftmaster > Travel Map > Build Prefab (see TravelMapPrefabBuilder).
public class TravelMapScreen : MonoBehaviour
{
    public static TravelMapScreen Instance { get; private set; }
    public static bool IsOpen => Instance != null;

    const int TowCost = 2000;
    const string PrefabResourcePath = "UI/TravelMap";

    // Iron Oval palette (PixelUITheme): gold on navy, red for alarm, one colour per kind of place so the
    // map reads by colour before it reads by label. The factory is the only teal dot on the board.
    // These are the same hexes the prefab builder bakes with (TravelMapPrefabBuilder.ApplyIronOvalSkin) —
    // the runtime is what the player sees, so if the two ever disagree, this file wins.
    static readonly Color CircuitColor = new Color32(0xf2, 0xc1, 0x4e, 0xff);    // gold - a racetrack
    static readonly Color JunkyardColor = new Color32(0xe0, 0x91, 0x3a, 0xff);   // rust - a yard full of it
    static readonly Color EngineShopColor = new Color32(0x53, 0xa8, 0xff, 0xff); // blue - somebody's business
    // The factory's colour is carried by the plate the builder puts under its wrench; the wrench itself
    // is drawn pale, or a steel spanner tinted teal on a teal plate is one dark blob.
    static readonly Color FactoryColor = new Color32(0xc6, 0xf7, 0xea, 0xff);    // the wrench on the plate
    static readonly Color FactoryLabel = new Color32(0x5f, 0xe8, 0xc8, 0xff);    // teal - the one place you own
    static readonly Color MysteryColor = new Color32(0x6b, 0x72, 0x8c, 0xff);    // not been there yet
    static readonly Color CurrentHalo = new Color32(0xf4, 0xea, 0xd7, 0xff);     // cream frame - you are here
    // Light blue, and the only light blue on the board: this week's race has to be findable at a glance
    // among fifty gold dots, and red reads as a warning next to the cream "you are here".
    static readonly Color DestHalo = new Color32(0x7f, 0xd4, 0xff, 0xff);        // this week's race
    static readonly Color ReachableHalo = new Color32(0xf2, 0xc1, 0x4e, 0x8c);   // gold, dimmed - one stop away

    // Roads read in three states, brightest first: the ones you can take from here (gold), your own
    // factory's slip roads (teal, always), and the rest of the country (steel blue).
    static readonly Color EdgeColor = new Color32(0x39, 0x5a, 0x94, 0xd8);       // highway
    static readonly Color EdgeLiveColor = new Color32(0xf2, 0xc1, 0x4e, 0xff);   // a road out of where you stand
    static readonly Color EdgeFactoryColor = new Color32(0x3f, 0x9d, 0x8b, 0xff);// the slip roads to your shop
    const float EdgeWidth = 3f, EdgeWidthLive = 5f;

    [Header("Header")]
    public Text titleLabel;
    public Text subLabel;
    public Text cashLabel;
    public Button closeButton;

    [Header("Map")]
    public RectTransform nodesRoot;   // TravelNodeMarker children — authored, draggable
    public RectTransform edgesRoot;   // highway lines — regenerated from marker positions
    public RectTransform herePin;     // cream pin that hops to wherever you are (optional)
    public RectTransform destPin;     // red chequered flag over this week's race (optional)

    [Header("Side panel")]
    public Text carRowsLabel;
    public Text locationHeader;
    public Text flavorLabel;
    public Button actionButton;       // context-dependent: START RACE WEEKEND or TOW
    public Button walkButton;         // minor locations: park and walk around (Landmark scene)
    public Text shopHeader;
    public RectTransform shopRowsRoot;
    public GameObject stockRowTemplate; // inactive child of shopRowsRoot, cloned per part

    [Header("Notice")]
    public Text noticeLabel;

    [Header("Zoom")]
    [Tooltip("How far in the map opens. 1 = the whole country in the window, which is what it used to do.")]
    public float openZoom = 2.4f;
    [Tooltip("Zoomed all the way out: the whole board, for picking a race on the other side of the map.")]
    public float minZoom = 1f;
    [Tooltip("Zoomed all the way in.")]
    public float maxZoom = 4.5f;

    readonly Dictionary<string, TravelNodeMarker> _markers = new();
    readonly List<EdgeLine> _edges = new();
    float _noticeUntil;

    // The window onto the map. `_focus` is a point in the nodes' own local space — the same space marker
    // localPositions and the baked highways live in — held at the middle of the plot; `_zoom` scales both
    // roots about their shared centre. The *Target pair is where the view is heading, so a hop between
    // nodes glides instead of cutting.
    RectTransform _plot;
    Vector2 _focus, _focusTarget;
    float _zoom = 1f, _zoomTarget = 1f;
    bool _panning;
    Vector2 _panFrom;
    bool _pinching;
    Vector2 _pinchMid;
    float _pinchGap;
    string _subBase;
    int _hintVersion = -1;
    bool _hintTouch;
    string _focusedOn;
    Vector2 _contentMin, _contentMax;
    bool _boundsKnown;

    // One baked highway line, remembered so Refresh can recolour it without rebuilding the whole map.
    class EdgeLine
    {
        public Image image;
        public RectTransform rect;
        public string a, b;
    }

    // ---------------- lifecycle ----------------

    public static void Open()
    {
        if (Instance != null) return;
        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogError($"TravelMap prefab missing at Resources/{PrefabResourcePath} — run Draftmaster > Travel Map > Build Prefab.");
            return;
        }
        Instantiate(prefab); // Awake sets Instance
    }

    public static void Close()
    {
        if (Instance != null) Destroy(Instance.gameObject);
    }

    void Awake()
    {
        Instance = this;

        // uGUI needs an EventSystem; race scenes don't all have one.
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        CacheMarkers();
        BuildEdges();

        closeButton.onClick.AddListener(Close);
        foreach (var m in _markers.Values)
        {
            var marker = m; // capture
            marker.button.onClick.AddListener(() => OnNodeClicked(marker));
        }

        noticeLabel.text = "";
        SetUpView();
        Refresh();
        OpenView();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        if (noticeLabel.text.Length > 0 && Time.unscaledTime >= _noticeUntil)
            noticeLabel.text = "";

        if (_subBase != null && (_hintVersion != InputGlyphs.Version || _hintTouch != InputGlyphs.UsingTouch))
            ApplySubLabel();

        UpdateView();
    }

    // --- Dev hotkey: F9 toggles the map in any scene (mirrors QuestHUD's self-bootstrap pattern). ---
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void BootstrapHotkey()
    {
        var go = new GameObject("TravelMapHotkey");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<TravelMapHotkey>();
    }

    class TravelMapHotkey : MonoBehaviour
    {
        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f9Key.wasPressedThisFrame)
            {
                if (IsOpen) Close(); else Open();
            }
        }
    }

    void Notice(string msg)
    {
        noticeLabel.text = msg;
        _noticeUntil = Time.unscaledTime + 3f;
    }

    // ---------------- the window onto the map ----------------

    // The plot is the fixed-size rect the two roots are stretched across (MapPlot in the prefab), so it is
    // the window; the roots are the paper being slid about behind it. Found rather than wired, so the
    // authored prefab needs no rebuild, and masked here for the same reason — without it a zoomed map
    // spills over the header and the side panel.
    void SetUpView()
    {
        _plot = nodesRoot != null ? nodesRoot.parent as RectTransform : null;
        if (_plot == null) return;
        if (_plot.GetComponent<RectMask2D>() == null) _plot.gameObject.AddComponent<RectMask2D>();
    }

    // Where the map opens: this week's race. While a destination is booked that is the destination; with
    // none booked the player is parked at the venue — during a race weekend the satnav is opened inside an
    // RV at the circuit — so it is wherever they stand.
    void OpenView()
    {
        if (_plot == null) return;
        var node = TravelGraph.Get(TravelState.DestinationId) ?? TravelGraph.Get(TravelState.CurrentNodeId);
        _focusedOn = node != null ? node.id : null;
        FocusOn(node, openZoom, instant: true);
    }

    // Glide to a node without touching how far in the player has zoomed. Called from Refresh as they drive,
    // and ignored while the view is already on that node so buying a part does not yank the map about.
    void FollowFocus(TravelNode n)
    {
        if (_plot == null || n == null || _focusedOn == n.id) return;
        _focusedOn = n.id;
        FocusOn(n, _zoomTarget, instant: false);
    }

    void FocusOn(TravelNode n, float zoom, bool instant)
    {
        if (_plot == null) return;
        _zoomTarget = Mathf.Clamp(zoom, minZoom, maxZoom);
        if (n != null) _focusTarget = ClampFocus(ContentPoint(n), _zoomTarget);
        if (instant) { _zoom = _zoomTarget; _focus = _focusTarget; }
        ApplyView();
    }

    // A node's position in the space the view moves in. Marker localPosition, which is exactly what
    // BuildEdges bakes the highways from, so dots and roads slide together.
    Vector2 ContentPoint(TravelNode n) =>
        n != null && _markers.TryGetValue(n.id, out var m) ? (Vector2)m.transform.localPosition : Vector2.zero;

    void UpdateView()
    {
        if (_plot == null) return;

        var mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 screen = mouse.position.ReadValue();
            // Two frames of reference: the plot's pivot is its top-left corner, while everything that
            // moves — marker localPositions, the baked highways, _focus — is measured from its centre.
            // Test the pointer against the rect in the first, then work in the second.
            bool overPlot = PlotPoint(screen, out Vector2 raw) && _plot.rect.Contains(raw);
            Vector2 local = raw - _plot.rect.center;

            // Wheel zooms about the pointer, and only while the pointer is over the map — the side panel
            // has its own list of parts to scroll.
            float wheel = mouse.scroll.ReadValue().y;
            if (overPlot && Mathf.Abs(wheel) > 0.01f)
                ZoomAt(wheel > 0f ? 1.15f : 1f / 1.15f, local);

            // Right or middle drag pans. Left is left alone: it is how a node is clicked.
            bool held = mouse.rightButton.isPressed || mouse.middleButton.isPressed;
            if (!held) _panning = false;
            else if (!_panning) { if (overPlot) { _panning = true; _panFrom = local; } }
            else
            {
                // A drag moves the paper under the window, so the focus moves the other way, by the drag
                // measured in map units rather than screen pixels.
                _focusTarget = ClampFocus(_focusTarget - (local - _panFrom) / Mathf.Max(_zoomTarget, 0.01f),
                                          _zoomTarget);
                _focus = _focusTarget;
                _zoom = _zoomTarget;
                _panFrom = local;
            }
        }

        UpdatePinch();
        UpdatePadZoom();

        if (Mathf.Abs(_zoom - _zoomTarget) > 0.0005f || (_focus - _focusTarget).sqrMagnitude > 0.01f)
        {
            float k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);   // frame-rate independent ease
            _zoom = Mathf.Lerp(_zoom, _zoomTarget, k);
            _focus = Vector2.Lerp(_focus, _focusTarget, k);
        }

        ApplyView();
    }

    // Two fingers: the gap between them zooms about their midpoint, and the midpoint moving pans — the
    // paper stays under the fingers. One finger is left alone: it is how a node is tapped, as left-click is.
    void UpdatePinch()
    {
        var ts = Touchscreen.current;
        int n = 0;
        Vector2 a = default, b = default;
        if (ts != null)
            foreach (var t in ts.touches)
            {
                if (!t.press.isPressed) continue;
                if (n == 0) a = t.position.ReadValue(); else if (n == 1) b = t.position.ReadValue();
                if (++n == 2) break;
            }

        if (n < 2 || !PlotPoint(a, out Vector2 ra) || !PlotPoint(b, out Vector2 rb)) { _pinching = false; return; }
        Vector2 mid = (ra + rb) * 0.5f - _plot.rect.center;
        float gap = Vector2.Distance(ra, rb);

        // A pinch only starts with both fingers down on the map, so one of them on the side panel's
        // parts list is a scroll there, not a zoom here.
        if (!_pinching)
        {
            if (!_plot.rect.Contains(ra) || !_plot.rect.Contains(rb)) return;
            _pinching = true;
        }
        else
        {
            // Pan first so the midpoint the zoom holds still is the one the fingers are on now.
            _focusTarget = ClampFocus(_focusTarget - (mid - _pinchMid) / Mathf.Max(_zoomTarget, 0.01f), _zoomTarget);
            if (_pinchGap > 1f && gap > 1f) ZoomAt(gap / _pinchGap, mid);
            // Fingers want the map glued to them, not easing along behind.
            _zoom = _zoomTarget;
            _focus = _focusTarget;
        }
        _pinchMid = mid;
        _pinchGap = gap;
    }

    // Triggers zoom about the middle of the window: the right one in, the left one out, faster the harder
    // they are squeezed — about one doubling a second held flat out.
    void UpdatePadZoom()
    {
        var gp = Gamepad.current;
        if (gp == null) return;
        float push = PadInput.Control(gp, PadBindings.MapZoomIn).ReadValue()
                   - PadInput.Control(gp, PadBindings.MapZoomOut).ReadValue();
        if (Mathf.Abs(push) < 0.1f) return;   // a resting trigger is not quite zero on every pad
        ZoomAt(Mathf.Pow(2f, push * Time.unscaledDeltaTime), Vector2.zero);
    }

    void ZoomAt(float factor, Vector2 plotLocal)
    {
        float z = Mathf.Clamp(_zoomTarget * factor, minZoom, maxZoom);
        if (Mathf.Approximately(z, _zoomTarget)) return;

        // Whatever is under the pointer stays under the pointer.
        Vector2 under = _focusTarget + plotLocal / Mathf.Max(_zoomTarget, 0.01f);
        _zoomTarget = z;
        _focusTarget = ClampFocus(under - plotLocal / z, z);
    }

    // Screen pixels to plot-local, measured from the plot's pivot (its top-left corner), which is what
    // Rect.Contains wants. The canvas is Screen Space Overlay, hence the null camera.
    bool PlotPoint(Vector2 screen, out Vector2 local) =>
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_plot, screen, null, out local);

    void ApplyView()
    {
        if (_plot == null) return;
        _focus = ClampFocus(_focus, _zoom);

        Vector2 offset = -_focus * _zoom;
        var scale = new Vector3(_zoom, _zoom, 1f);
        if (nodesRoot != null) { nodesRoot.localScale = scale; nodesRoot.anchoredPosition = offset; }
        if (edgesRoot != null) { edgesRoot.localScale = scale; edgesRoot.anchoredPosition = offset; }
    }

    // Keep the window on the country. An axis whose content is narrower than the window is centred on it
    // instead, so zooming out never parks the map against one edge with a void beside it.
    Vector2 ClampFocus(Vector2 want, float zoom)
    {
        EnsureBounds();
        Vector2 half = _plot.rect.size * 0.5f / Mathf.Max(zoom, 0.01f);
        Vector2 mid = (_contentMin + _contentMax) * 0.5f;

        return new Vector2(
            _contentMax.x - _contentMin.x <= half.x * 2f
                ? mid.x : Mathf.Clamp(want.x, _contentMin.x + half.x, _contentMax.x - half.x),
            _contentMax.y - _contentMin.y <= half.y * 2f
                ? mid.y : Mathf.Clamp(want.y, _contentMin.y + half.y, _contentMax.y - half.y));
    }

    // The box the dots occupy, plus a margin so an edge node's name is not against the frame. Markers are
    // authored and never move at runtime, so this is measured once.
    void EnsureBounds()
    {
        if (_boundsKnown) return;
        _boundsKnown = true;

        const float Margin = 70f;
        if (_markers.Count == 0)
        {
            Vector2 halfPlot = _plot.rect.size * 0.5f;
            _contentMin = -halfPlot; _contentMax = halfPlot;
            return;
        }

        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        foreach (var m in _markers.Values)
        {
            Vector2 p = m.transform.localPosition;
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
        _contentMin = min - new Vector2(Margin, Margin);
        _contentMax = max + new Vector2(Margin, Margin);
    }

    // ---------------- map ----------------

    void CacheMarkers()
    {
        _markers.Clear();
        foreach (var m in nodesRoot.GetComponentsInChildren<TravelNodeMarker>(true))
        {
            if (m.Node == null) { Debug.LogWarning($"TravelNodeMarker '{m.name}' has unknown nodeId '{m.nodeId}'"); continue; }
            _markers[m.nodeId] = m;
        }
        foreach (var n in TravelGraph.Nodes)
            if (!_markers.ContainsKey(n.id))
                Debug.LogWarning($"TravelGraph node '{n.id}' has no marker in the prefab — run Draftmaster > Travel Map > Sync Node Markers.");
    }

    // Highway lines from marker positions, so they follow when nodes are dragged in Prefab Mode.
    // Editor preview: right-click the component header -> Rebuild Edges.
    [ContextMenu("Rebuild Edges")]
    public void BuildEdges()
    {
        if (_markers.Count == 0) CacheMarkers();

        _edges.Clear();
        for (int i = edgesRoot.childCount - 1; i >= 0; i--)
        {
            var child = edgesRoot.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }

        foreach (var (a, b) in TravelGraph.Edges)
        {
            if (!_markers.TryGetValue(a, out var ma) || !_markers.TryGetValue(b, out var mb)) continue;
            Vector2 pa = ma.transform.localPosition, pb = mb.transform.localPosition;
            Vector2 d = pb - pa;
            float len = d.magnitude;
            if (len < 1f) continue;

            var go = new GameObject($"Edge_{a}_{b}", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(edgesRoot, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.localPosition = pa;
            rt.sizeDelta = new Vector2(len, EdgeWidth);
            rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            var img = go.GetComponent<Image>();
            img.color = EdgeColor;
            img.raycastTarget = false;
            _edges.Add(new EdgeLine { image = img, rect = rt, a = a, b = b });
        }

        // Baked look (also what Prefab Mode shows): plain highways, teal slip roads to your own shop.
        TintEdges(null);
    }

    // Recolours the baked highways for where the player is standing. Public because the editor's
    // Preview PNG dresses the map by hand — the canvas is Screen Space Overlay, so a PNG render is the
    // only way to look at a restyle without Play Mode, and it has to show the live colour rules.
    public void TintEdges(string currentId)
    {
        foreach (var e in _edges)
        {
            bool live = currentId != null && (e.a == currentId || e.b == currentId);
            bool factory = IsFactory(e.a) || IsFactory(e.b);

            e.image.color = live ? EdgeLiveColor : (factory ? EdgeFactoryColor : EdgeColor);
            e.rect.sizeDelta = new Vector2(e.rect.sizeDelta.x, live ? EdgeWidthLive : EdgeWidth);
            // Draw order carries the same ranking, or a gold road disappears under the fifty grey ones
            // that cross it.
            if (live || factory) e.rect.SetAsLastSibling();
        }
    }

    // Editor preview hook (Draftmaster > Travel Map > Preview PNG): park the pins and tint the roads as
    // if the player were standing at `currentId` on the way to `destId`. Same code the live map runs, so
    // the PNG is not a hand-made approximation of the look.
    public void PreviewState(string currentId, string destId)
    {
        if (_markers.Count == 0) CacheMarkers();
        PlacePin(herePin, TravelGraph.Get(currentId));
        PlacePin(destPin, TravelGraph.Get(destId));
        TintEdges(currentId);
    }

    static bool IsFactory(string nodeId)
    {
        var n = TravelGraph.Get(nodeId);
        return n != null && n.locationType == TravelLocationType.TeamFactory;
    }

    void OnNodeClicked(TravelNodeMarker marker)
    {
        var n = marker.Node;
        var current = TravelGraph.Get(TravelState.CurrentNodeId);
        var dest = TravelGraph.Get(TravelState.DestinationId);
        bool choosing = dest == null;
        bool isCurrent = n == current;
        bool reachable = !choosing && TravelGraph.AreAdjacent(current.id, n.id) && TravelState.StopsLeft > 0;

        if (isCurrent) { Refresh(); return; }
        if (choosing)
        {
            if (n.isCircuit && TravelState.ChooseDestination(n.id))
                Notice($"Destination set: {n.name}. {TravelState.StopsLeft} stops for a {TravelGraph.ShortestHops(TravelState.CurrentNodeId, n.id)}-stop direct run.");
            Refresh();
            return;
        }
        if (reachable && TravelState.MoveTo(n.id))
        {
            if (!n.isCircuit) Notice($"Pulled in at {n.name}.");
            Refresh();
        }
    }

    // ---------------- refresh (restyle everything from state) ----------------

    void Refresh()
    {
        var current = TravelGraph.Get(TravelState.CurrentNodeId);
        var dest = TravelGraph.Get(TravelState.DestinationId);
        if (TravelState.HasDestination && dest == null) TravelState.DestinationId = ""; // stale save vs renamed node
        bool choosing = dest == null;

        titleLabel.text = choosing
            ? "THE ROAD    —    choose your next race (click a circuit)"
            : $"THE ROAD TO {dest.name.ToUpperInvariant()}";
        _subBase = choosing
            ? $"Week {TravelState.Week}   ·   You are at {current.name}"
            : $"Week {TravelState.Week}   ·   At {current.name}   ·   STOPS LEFT: {TravelState.StopsLeft}";
        ApplySubLabel();
        cashLabel.text = PlayerWallet.CashText;

        // The view rides along with the player as they hop, keeping whatever zoom they chose.
        FollowFocus(current);

        RefreshMarkers(choosing, current, dest);
        RefreshSidePanel(choosing, current, dest);
    }

    // The hint rides on the sub line because the map no longer shows the whole country at once, and it
    // names the controls of whatever is in the player's hands — so it is redrawn when that changes.
    void ApplySubLabel()
    {
        _hintVersion = InputGlyphs.Version;
        _hintTouch = InputGlyphs.UsingTouch;
        string hint =
            InputGlyphs.UsingGamepad
                ? $"{InputGlyphs.PadName(PadBindings.MapZoomIn)} / {InputGlyphs.PadName(PadBindings.MapZoomOut)} zoom"
            : _hintTouch ? "pinch zooms, two-finger drag pans"
            : "wheel zooms, right-drag pans";
        subLabel.text = _subBase + "   ·   " + hint;
    }

    void RefreshMarkers(bool choosing, TravelNode current, TravelNode dest)
    {
        foreach (var marker in _markers.Values)
        {
            var n = marker.Node;
            bool isCurrent = n == current;
            bool isDest = n == dest;
            bool adjacent = TravelGraph.AreAdjacent(current.id, n.id);
            bool reachable = !choosing && adjacent && TravelState.StopsLeft > 0;
            bool clickable = (choosing && n.isCircuit && !isCurrent) || reachable || isCurrent;

            marker.button.interactable = clickable;

            if (isCurrent) { marker.halo.enabled = true; marker.halo.color = CurrentHalo; }
            else if (isDest) { marker.halo.enabled = true; marker.halo.color = DestHalo; }
            else if (reachable || (choosing && n.isCircuit)) { marker.halo.enabled = true; marker.halo.color = ReachableHalo; }
            else marker.halo.enabled = false;

            marker.dot.color = DotColor(n);

            marker.label.text = LabelFor(n);
            // The nodes that matter right now get the bigger type; the other fifty stay small so the
            // middle of the country does not turn into a wall of overlapping names.
            bool shout = isCurrent || isDest || n.locationType == TravelLocationType.TeamFactory;
            marker.label.fontSize = shout ? 16 : 8;
            marker.label.color = isCurrent ? CurrentHalo : (isDest ? DestHalo : LabelColor(n));
        }

        // The two pins are single objects that hop about rather than one per node: where you are, and
        // the race you are driving to. They are what the eye should find first on a board of 75 dots.
        PlacePin(herePin, current);
        PlacePin(destPin, dest);
        TintEdges(current != null ? current.id : null);
    }

    // Parks a pin just above a node's dot, or hides it when there is nothing to point at.
    void PlacePin(RectTransform pin, TravelNode node)
    {
        if (pin == null) return;
        if (node == null || !_markers.TryGetValue(node.id, out var marker)) { pin.gameObject.SetActive(false); return; }
        pin.gameObject.SetActive(true);
        // The flag over the race is baked red in the prefab; the halo under it is light blue now, and a
        // pin that disagrees with its own halo reads as two different things being marked.
        if (pin == destPin)
        {
            var flag = pin.GetComponent<Image>();
            if (flag != null) flag.color = DestHalo;
        }
        // Above the dot, except at the factory, whose own name is already up there (the builder puts it
        // there because a name under that dot runs straight into Indianapolis Raceway Park's).
        float dy = node.locationType == TravelLocationType.TeamFactory ? -38f : 26f;
        pin.localPosition = marker.transform.localPosition + new Vector3(0f, dy, 0f);
        pin.SetAsLastSibling();
    }

    // One colour per kind of place. Minor locations stay grey until you have actually pulled in - that
    // discovery is the point - but your own factory is never a mystery.
    static Color DotColor(TravelNode n)
    {
        if (n.isCircuit) return CircuitColor;
        if (n.locationType == TravelLocationType.TeamFactory) return FactoryColor;
        if (!TravelState.IsVisited(n.id)) return MysteryColor;
        return n.locationType == TravelLocationType.Junkyard ? JunkyardColor : EngineShopColor;
    }

    // Names are painted in their dot's colour so a glance down the map matches a glance at the key —
    // except the factory, whose wrench is pale and whose name is the teal the node is known by.
    static Color LabelColor(TravelNode n) =>
        n.locationType == TravelLocationType.TeamFactory ? FactoryLabel : DotColor(n);

    static string LabelFor(TravelNode n)
    {
        if (n.isCircuit || n.locationType == TravelLocationType.TeamFactory) return n.name.ToUpperInvariant();
        if (!TravelState.IsVisited(n.id)) return "?";
        return (n.locationType == TravelLocationType.Junkyard ? "[J] " : "[E] ") + n.name.ToUpperInvariant();
    }

    // ---------------- side panel ----------------

    void RefreshSidePanel(bool choosing, TravelNode current, TravelNode dest)
    {
        // Car build summary — what's installed, so shop comparisons are one glance.
        var sb = new StringBuilder();
        foreach (PartSlot slot in System.Enum.GetValues(typeof(PartSlot)))
            sb.AppendLine(PlayerCarBuild.DescribeSlot(slot));
        carRowsLabel.text = sb.ToString().TrimEnd();

        ClearShopRows();
        actionButton.gameObject.SetActive(false);
        // Optional: an older prefab may have no walk button at all (Draftmaster > Travel Map > Restyle
        // rebuilds it). Missing it should cost you the button, not the whole side panel.
        if (walkButton != null) walkButton.gameObject.SetActive(false);
        shopHeader.gameObject.SetActive(false);

        if (choosing)
        {
            locationHeader.text = "";
            flavorLabel.text = "Pick the next race on the map. Your stop budget covers the direct route plus a small detour — plan it past somewhere useful.";
            return;
        }

        locationHeader.text = current.name.ToUpperInvariant();
        string flavor;

        if (current.isCircuit)
        {
            if (current == dest)
            {
                flavor = "This is the place. Time to go racing.";
                ShowAction("START RACE WEEKEND", () => StartRaceWeekend(current));
            }
            else flavor = $"A racetrack, but not this week's. {dest.name} is waiting.";
        }
        else
        {
            flavor = current.flavor;
            BuildShopRows(current);

            // Park up and walk the place on foot (shared Landmark scene; costs nothing).
            if (walkButton != null)
            {
                walkButton.gameObject.SetActive(true);
                walkButton.GetComponentInChildren<Text>().text =
                    current.locationType == TravelLocationType.TeamFactory ? "WALK THE SHOP FLOOR" : "STOP & LOOK AROUND";
                walkButton.onClick.RemoveAllListeners();
                walkButton.onClick.AddListener(() =>
                {
                    if (!LandmarkLoader.SceneInBuild)
                    {
                        Notice("Landmark scene missing from Build Settings — run Draftmaster > Travel Map > Build Landmark Scene.");
                        return;
                    }
                    Close();
                    LandmarkLoader.Visit(TravelState.CurrentNodeId);
                });
            }
        }

        // Stranded? Tow covers the rest of the way, for a price. Never a softlock.
        if (current != dest && TravelState.StopsLeft <= 0)
        {
            flavor += "\n\nOut of stops. The flatbed knows the way.";
            ShowAction($"TOW TO {dest.name.ToUpperInvariant()} ({PlayerWallet.Format(TowCost)})", () =>
            {
                PlayerWallet.Add(-TowCost); // clamped at $0 — the tow always runs
                TravelState.CurrentNodeId = dest.id;
                Notice($"Towed to {dest.name}. The driver talked the whole way.");
                Refresh();
            });
        }

        flavorLabel.text = flavor;
    }

    void ShowAction(string label, UnityEngine.Events.UnityAction onClick)
    {
        actionButton.gameObject.SetActive(true);
        actionButton.GetComponentInChildren<Text>().text = label;
        actionButton.onClick.RemoveAllListeners();
        actionButton.onClick.AddListener(onClick);
    }

    void ClearShopRows()
    {
        for (int i = shopRowsRoot.childCount - 1; i >= 0; i--)
        {
            var child = shopRowsRoot.GetChild(i).gameObject;
            if (child != stockRowTemplate) Destroy(child);
        }
        shopRowsRoot.sizeDelta = new Vector2(shopRowsRoot.sizeDelta.x, 0f);
    }

    void BuildShopRows(TravelNode loc)
    {
        if (loc.locationType == TravelLocationType.None) return;
        shopHeader.gameObject.SetActive(true);

        int count = 0;
        if (loc.locationType == TravelLocationType.Junkyard)
        {
            shopHeader.text = "SALVAGE THIS WEEK";
            foreach (var (part, price) in PartCatalog.JunkyardStock(loc.id, TravelState.Week))
            {
                if (TravelState.WasBought(loc.id, part.id)) continue;
                AddStockRow(part, price, () => TravelState.MarkBought(loc.id, part.id));
                count++;
            }
        }
        else if (loc.locationType == TravelLocationType.TeamFactory)
        {
            // Everything your own shop has finished and nobody has driven out to fetch yet. Free, and
            // gone from the rack once taken - unlike a junkyard shelf, it does not reroll next week.
            shopHeader.text = "ON THE RACK - YOURS TO TAKE";
            foreach (var (part, _) in PartCatalog.FactoryStock(TravelState.Week))
            {
                if (TravelState.WasCollected(part.id)) continue;
                AddStockRow(part, 0, () => TravelState.MarkCollected(part.id), free: true);
                count++;
            }
            if (count == 0) shopHeader.text = "NOTHING ON THE RACK - THEY ARE STILL BUILDING";
        }
        else // EngineShop
        {
            shopHeader.text = "FOR SALE";
            if (loc.shopStock != null)
                foreach (var id in loc.shopStock)
                {
                    var part = PartCatalog.Get(id);
                    if (part == null) continue;
                    AddStockRow(part, part.price, null);
                    count++;
                }
        }

        // Outer layout group reads child heights, so the container must claim its own space.
        float rowH = ((RectTransform)stockRowTemplate.transform).sizeDelta.y + 4f;
        shopRowsRoot.sizeDelta = new Vector2(shopRowsRoot.sizeDelta.x, count * rowH);
    }

    void AddStockRow(PartDef part, int price, System.Action onBought, bool free = false)
    {
        var row = Instantiate(stockRowTemplate, shopRowsRoot);
        row.SetActive(true);

        bool installed = PlayerCarBuild.InstalledId(part.slot) == part.id;
        row.transform.Find("NameLabel").GetComponent<Text>().text = part.name + (installed ? "   [INSTALLED]" : "");
        row.transform.Find("EffectLabel").GetComponent<Text>().text = part.EffectSummary() + "\n" + PlayerCarBuild.DescribeSlot(part.slot);

        var buy = row.transform.Find("BuyButton").GetComponent<Button>();
        if (installed) buy.gameObject.SetActive(false);
        else
        {
            buy.GetComponentInChildren<Text>().text = free ? "COLLECT" : PlayerWallet.Format(price);
            buy.onClick.AddListener(() =>
            {
                if (free || PlayerWallet.TrySpend(price))
                {
                    PlayerCarBuild.Install(part);
                    PlayerStatsLedger.Increment(free ? "partscollected" : "partsbought");
                    onBought?.Invoke();
                    Notice(free
                        ? $"{part.name} collected and fitted. The shop already knew it would fit."
                        : $"{part.name} installed. Old {part.slot.ToString().ToLowerInvariant()} scrapped.");
                }
                else Notice("Not enough cash.");
                Refresh();
            });
        }
    }

    void StartRaceWeekend(TravelNode circuit)
    {
        TravelState.ArriveAndClearDestination();
        RaceWeekend.ResetWeekend();
        // Load the circuit's scene when it's in the build; otherwise re-run the current (dev) scene —
        // the travel position still advances, so the map keeps working before every track is wired up.
        string scene = Application.CanStreamedLevelBeLoaded(circuit.id) ? circuit.id : SceneManager.GetActiveScene().name;
        CoopScene.Load(scene);   // fast travel takes a co-op guest with it
    }
}
