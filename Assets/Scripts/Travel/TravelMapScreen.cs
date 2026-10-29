using System.Collections.Generic;
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
// topology and shop data only. Flow:
//   1. The next race venue. Parked at a round of the calendar it is booked the moment the map opens;
//      anywhere else, click any circuit. Stop budget = direct route + DetourAllowance.
//   2. The route is drawn (light blue roads) from where you are to the race. Tap any other place to call in
//      there on the way — it slots in wherever it lengthens the drive least and the route is redrawn
//      (TravelRoute); tap it again to take it off. GO sends the RV sprite down the route: it rolls straight
//      through racetracks and pulls in at every other place it crosses (shop, salvage yard, Team HQ),
//      where you VISIT or CONTINUE. The first leg between two races is the tutorial: the car will not move
//      until Team HQ is on the route (TravelTutorial). Minor locations are shopped on foot (VISIT ->
//      Landmark scene, LandmarkShopPanel): junkyards sell a weekly random salvage roll, engine shops a
//      fixed catalog, and your own Team Factory (the teal wrench) racks what your shop has built.
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
    // The planned route outranks all three, in the race's light blue: it is the way to get there.
    static readonly Color EdgeRouteColor = DestHalo;
    const float EdgeWidth = 3f, EdgeWidthLive = 5f, EdgeWidthRoute = 7f;

    [Header("Header")]
    public Text subLabel;             // the one header line: week, where you are, the route
    public Button closeButton;

    [Header("Map")]
    public RectTransform nodesRoot;   // TravelNodeMarker children — authored, draggable
    public RectTransform edgesRoot;   // highway lines — regenerated from marker positions
    public RectTransform herePin;     // cream pin that hops to wherever you are (optional)
    public RectTransform destPin;     // red chequered flag over this week's race (optional)

    [Header("Footer")]
    public Text noticeLabel;          // a notice for three seconds, then the zoom/pan controls, dimmed
    public Button actionButton;       // context-dependent: GO / CONTINUE, START RACE WEEKEND or TOW
    public Button walkButton;         // minor locations: park and walk around (Landmark scene)

    [Header("Zoom")]
    [Tooltip("How far in the map opens. 1 = the whole country in the window, which is what it used to do.")]
    public float openZoom = 2.4f;
    [Tooltip("Zoomed all the way out: the whole board, for picking a race on the other side of the map.")]
    public float minZoom = 1f;
    [Tooltip("Zoomed all the way in.")]
    public float maxZoom = 4.5f;

    [Header("Drive")]
    [Tooltip("How fast the RV crosses the board, in board pixels a second.")]
    public float rvSpeed = 80f;
    [Tooltip("No hop is quicker than this, however short the road, so a two-towns-apart hop still reads.")]
    public float rvMinHopSeconds = 1f;
    [Tooltip("The RV sprite's size on the board, in board pixels (the art is 2:1, nose to the left).")]
    public Vector2 rvSize = new Vector2(36f, 18f);

    readonly Dictionary<string, TravelNodeMarker> _markers = new();
    // How much each name matters right now, set by the marker refresh and read by DeclutterLabels.
    readonly Dictionary<TravelNodeMarker, int> _labelRank = new();
    float _labelScaleLaidOut = -1f;
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
    string _hint;
    Color _noticeColor;
    static readonly Color HintColor = new Color32(0x9a, 0xa3, 0xb8, 0xff); // the kit's dim text
    int _hintVersion = -1;
    bool _hintTouch;
    string _focusedOn;

    // The drive. `_rv` is the RV sprite, a child of the nodes root so it pans and zooms with the board.
    // While `_driving` it runs from `_hopFrom` to `_hopTo` over `_hopSeconds`; `_stoppedOnRoute` is set when
    // it pulls in somewhere on the way, which turns GO into CONTINUE; `_rvParked` keeps it on the board
    // wherever it last stopped.
    const string RvSpritePath = "Environment/motorhome";
    RectTransform _rv;
    bool _driving, _stoppedOnRoute, _rvParked;
    TravelNode _hopTo;
    Vector2 _hopFrom, _hopEnd;
    float _hopT, _hopSeconds;
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

        _noticeColor = noticeLabel.color;
        ApplyHint();
        // Between two rounds the next race is already decided, so the route to it is drawn from the off.
        // Not while this week's weekend is still running: the map is only a look at the road until then.
        if (WeekendHoldsTheCar) Notice(Draftmaster.Weekend.WeekendDeparture.HeldReason);
        else if (TravelState.BookNextCalendarRace())
            Notice(TravelTutorial.LegBooked
                ? Draftmaster.Tracks.TravelTutorialRoute.Prompt
                : $"Next race: {TravelGraph.Get(TravelState.DestinationId).name}. Tap places to call in on the way, then GO.");
        SetUpView();
        Refresh();
        OpenView();
    }

    // A career parked at this week's race stays there until its weekend is done (WeekendDeparture). The map
    // still opens — it is where the next round is — but nothing on it moves the car. Only at the venue the
    // weekend belongs to: somebody already on the road, or a co-op guest carried along on the host's travel,
    // is not held.
    public static bool WeekendHoldsTheCar
    {
        get
        {
            if (!GameSession.CareerActive || Coop.IsGuest) return false;
            if (Draftmaster.Weekend.WeekendDeparture.WeekendDone(RaceWeekend.WeekendId, WeekendObjectiveHUD.RoadCalled))
                return false;
            var here = TravelGraph.Get(TravelState.CurrentNodeId);
            return here != null && here.isCircuit && here.id == TrackSelection.CurrentId;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_tutorialPrompt != null) ControlHints.Hide(_tutorialPrompt);
    }

    void Update()
    {
        if (_hint == null && Time.unscaledTime >= _noticeUntil)
            ApplyHint();
        else if (_hint != null && (_hintVersion != InputGlyphs.Version || _hintTouch != InputGlyphs.UsingTouch))
            ApplyHint();

        UpdateDrive();
        UpdateView();
    }

    // --- Dev hotkey: F9 toggles the map in any scene (self-bootstraps like RacePauseMenu). ---
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

    // A notice shows for three seconds; a sticky one stays until the next notice (a question waiting on
    // the footer's buttons, like VISIT or CONTINUE).
    void Notice(string msg, bool sticky = false)
    {
        _hint = null;
        noticeLabel.text = msg;
        noticeLabel.color = _noticeColor;
        _noticeUntil = sticky ? float.PositiveInfinity : Time.unscaledTime + 3f;
    }

    // ---------------- the window onto the map ----------------

    // The plot is the fixed-size rect the two roots are stretched across (MapPlot in the prefab), so it is
    // the window; the roots are the paper being slid about behind it. Found rather than wired, so the
    // authored prefab needs no rebuild, and masked here for the same reason — without it a zoomed map
    // spills over the header and the footer.
    void SetUpView()
    {
        _plot = nodesRoot != null ? nodesRoot.parent as RectTransform : null;
        if (_plot == null) return;
        if (_plot.GetComponent<RectMask2D>() == null) _plot.gameObject.AddComponent<RectMask2D>();
    }

    // Where the map opens: this week's race. While a destination is booked that is the destination; with
    // none booked the player is parked at the venue — during a race weekend the satnav is opened inside an
    // RV at the circuit — so it is wherever they stand. Mid-weekend the map is only for browsing, so it
    // opens on where they stand whatever is booked.
    void OpenView()
    {
        if (_plot == null) return;
        var here = TravelGraph.Get(TravelState.CurrentNodeId);
        var node = WeekendHoldsTheCar ? here : TravelGraph.Get(TravelState.DestinationId) ?? here;
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
        if (Mathf.Abs(LabelScale - _labelScaleLaidOut) > 0.04f) DeclutterLabels();
    }

    // ---------------- place names ----------------
    //
    // The middle of the country is packed — Charlotte, Concord, Rockingham and North Wilkesboro inside a
    // thumb's width of each other — so every name cannot be printed at once and be read. Names are laid out
    // most important first; each takes the first side of its dot (under, over, right, left) where it would
    // not land on a name already placed or on somebody else's dot, and a name with no clear side is left
    // off. The dots always stay, and the names that matter right now (where you are, where you are going,
    // your stops, your factory) are always printed.
    //
    // Names grow with the map up to the zoom it opens at, then hold their size: zooming in past that
    // spreads the dots apart under names that no longer grow, so the ones left off come back.
    float LabelScale => _zoom <= openZoom ? 1f : openZoom / Mathf.Max(_zoom, 0.01f);

    // 4 the names in play right now, 3 where the car can go next, 2 racetracks, 1 a place you have been, 0 "?".
    static int LabelRank(TravelNode n, bool shout, bool reachable)
    {
        if (shout) return 4;
        if (reachable) return 3;
        if (n.isCircuit) return 2;
        return TravelState.IsVisited(n.id) ? 1 : 0;
    }

    const float LabelGap = 3f;   // board pixels between a dot and its name, and between two names

    void DeclutterLabels()
    {
        float k = LabelScale;
        _labelScaleLaidOut = k;

        var order = new List<TravelNodeMarker>(_markers.Values);
        order.Sort((a, b) =>
        {
            int ra = _labelRank.TryGetValue(a, out int x) ? x : 0, rb = _labelRank.TryGetValue(b, out int y) ? y : 0;
            if (ra != rb) return rb.CompareTo(ra);
            return string.CompareOrdinal(a.nodeId, b.nodeId);
        });

        // Everything a name must stay off: every dot, and the two pins, in the nodes root's own space.
        var dots = new List<(TravelNodeMarker owner, Rect rect)>(order.Count + 2);
        foreach (var m in order)
            if (m.dot != null) dots.Add((m, Around(m.transform.localPosition, m.dot.rectTransform.rect.size)));
        foreach (var pin in new[] { herePin, destPin })
            if (pin != null && pin.gameObject.activeSelf && pin.parent == nodesRoot)
                dots.Add((null, Around(pin.localPosition, pin.rect.size)));

        var placed = new List<Rect>(order.Count);
        foreach (var m in order)
        {
            var label = m.label;
            if (label == null) continue;
            if (string.IsNullOrEmpty(label.text)) { label.enabled = false; continue; }

            var lrt = label.rectTransform;
            lrt.localScale = new Vector3(k, k, 1f);
            Vector2 size = new Vector2(label.preferredWidth, label.preferredHeight) * k;
            Vector2 c = m.transform.localPosition;
            float reach = (m.dot != null ? m.dot.rectTransform.rect.height : 12f) * 0.5f + LabelGap;
            float side = (m.dot != null ? m.dot.rectTransform.rect.width : 12f) * 0.5f + LabelGap;

            // The factory's name goes over its dot first: Indianapolis is packed in right under it.
            bool overFirst = m.Node != null && m.Node.locationType == TravelLocationType.TeamFactory;
            Vector2 under = new Vector2(0f, -(reach + size.y * 0.5f));
            Vector2 over = new Vector2(0f, reach + size.y * 0.5f);
            Vector2 right = new Vector2(side + size.x * 0.5f, 0f);
            Vector2 left = new Vector2(-(side + size.x * 0.5f), 0f);
            var tries = overFirst ? new[] { over, under, right, left } : new[] { under, over, right, left };

            Vector2? chosen = null;
            foreach (var offset in tries)
            {
                var r = Around(c + offset, size);
                if (Clear(r, placed, dots, m)) { chosen = offset; break; }
            }
            int rank = _labelRank.TryGetValue(m, out int rk) ? rk : 0;
            if (chosen == null && rank >= 4) chosen = tries[0];   // where you are and where you're going: always
            if (chosen == null) { label.enabled = false; continue; }

            label.enabled = true;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.anchoredPosition = chosen.Value;
            label.alignment = TextAnchor.MiddleCenter;
            placed.Add(Around(c + chosen.Value, size));
        }
    }

    static Rect Around(Vector2 centre, Vector2 size) => new Rect(centre - size * 0.5f, size);

    static bool Clear(Rect r, List<Rect> placed, List<(TravelNodeMarker owner, Rect rect)> dots, TravelNodeMarker self)
    {
        var padded = new Rect(r.x - LabelGap * 0.5f, r.y - LabelGap * 0.5f, r.width + LabelGap, r.height + LabelGap);
        for (int i = 0; i < placed.Count; i++) if (padded.Overlaps(placed[i])) return false;
        for (int i = 0; i < dots.Count; i++) if (dots[i].owner != self && padded.Overlaps(dots[i].rect)) return false;
        return true;
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

    // Recolours the baked highways for where the player is standing and the route they are on (`route` =
    // the nodes after `currentId`, as TravelRoute.Path gives them). Public because the editor's
    // Preview PNG dresses the map by hand — the canvas is Screen Space Overlay, so a PNG render is the
    // only way to look at a restyle without Play Mode, and it has to show the live colour rules.
    public void TintEdges(string currentId, IReadOnlyList<string> route = null)
    {
        var routed = new HashSet<(string, string)>();
        if (currentId != null && route != null)
        {
            string prev = currentId;
            foreach (var id in route) { routed.Add(EdgeKey(prev, id)); prev = id; }
        }

        var onTop = new List<RectTransform>();
        foreach (var e in _edges)
        {
            bool onRoute = routed.Contains(EdgeKey(e.a, e.b));
            bool live = currentId != null && (e.a == currentId || e.b == currentId);
            bool factory = IsFactory(e.a) || IsFactory(e.b);

            e.image.color = onRoute ? EdgeRouteColor
                          : live ? EdgeLiveColor : (factory ? EdgeFactoryColor : EdgeColor);
            e.rect.sizeDelta = new Vector2(e.rect.sizeDelta.x,
                                           onRoute ? EdgeWidthRoute : live ? EdgeWidthLive : EdgeWidth);
            // Draw order carries the same ranking, or a gold road disappears under the fifty grey ones
            // that cross it — and the route goes over everything.
            if (onRoute) onTop.Add(e.rect);
            else if (live || factory) e.rect.SetAsLastSibling();
        }
        foreach (var rt in onTop) rt.SetAsLastSibling();
    }

    static (string, string) EdgeKey(string a, string b) => string.CompareOrdinal(a, b) < 0 ? (a, b) : (b, a);

    // Editor preview hook (Draftmaster > Travel Map > Preview PNG): park the pins and tint the roads as
    // if the player were standing at `currentId` on the way to `destId`. Same code the live map runs, so
    // the PNG is not a hand-made approximation of the look.
    public void PreviewState(string currentId, string destId)
    {
        if (_markers.Count == 0) CacheMarkers();
        PlacePin(herePin, TravelGraph.Get(currentId));
        PlacePin(destPin, TravelGraph.Get(destId));
        TintEdges(currentId, Draftmaster.Tracks.TravelRoutePlan.Route(currentId, null, destId, id => TravelGraph.Neighbors(id)));
        foreach (var m in _markers.Values)
        {
            var n = m.Node;
            if (n == null) continue;
            bool shout = n.id == currentId || n.id == destId || n.locationType == TravelLocationType.TeamFactory;
            _labelRank[m] = LabelRank(n, shout, false);
        }
        DeclutterLabels();
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

        if (_driving) return;
        if (isCurrent) { Refresh(); return; }
        if (WeekendHoldsTheCar) { Notice(Draftmaster.Weekend.WeekendDeparture.HeldReason); return; }
        if (choosing)
        {
            if (n.isCircuit && !TravelTutorial.CanChoose(n.id))
            {
                Notice($"Not this week. {TravelGraph.Get(Draftmaster.Tracks.TravelTutorialRoute.To).name} is next on the calendar.");
                return;
            }
            if (n.isCircuit && TravelState.ChooseDestination(n.id))
                Notice(TravelTutorial.LegBooked
                    ? $"Destination set: {n.name}. {Draftmaster.Tracks.TravelTutorialRoute.Prompt}"
                    : $"Destination set: {n.name}. {TravelState.StopsLeft} stops for a {TravelGraph.ShortestHops(TravelState.CurrentNodeId, n.id)}-stop direct run.");
            Refresh();
            return;
        }

        // On the road: a tap adds a stop to the route — or, tapped again, takes it off — and the route is
        // redrawn either way. GO drives it.
        if (n == dest) { Notice($"The route already ends at {dest.name}."); return; }

        if (TravelRoute.HasStop(n.id))
        {
            if (!TravelTutorial.CanRemoveStop(n.id))
            {
                Notice("Team HQ stays on the route - the new parts and sponsors are waiting there.");
                return;
            }
            TravelRoute.Remove(n.id);
            Notice($"{MapName(n)} taken off the route. {RouteSummary()}");
            Refresh();
            return;
        }

        int need = TravelRoute.LengthWith(n.id);
        if (need < 0) { Notice($"No road to {MapName(n)} from here."); return; }
        if (need > TravelState.StopsLeft)
        {
            Notice($"Too far - via {MapName(n)} is {need} stops and you have {TravelState.StopsLeft}.");
            return;
        }
        TravelRoute.Add(n.id);
        Notice($"Rerouted via {MapName(n)}. {RouteSummary()}");
        Refresh();
    }

    // ---------------- the drive ----------------

    // GO / CONTINUE: the RV appears where you stand and sets off down the route.
    void StartDrive()
    {
        if (WeekendHoldsTheCar) { Notice(Draftmaster.Weekend.WeekendDeparture.HeldReason); return; }
        _stoppedOnRoute = false;
        _driving = true;
        PlaceRv(ContentPoint(TravelGraph.Get(TravelState.CurrentNodeId)), Vector2.zero);
        if (!NextHop()) StopDriving();
        Refresh();
    }

    // Lines up the next road along the route, or says why there isn't one and returns false.
    bool NextHop()
    {
        var path = TravelRoute.Path();
        if (path == null || path.Count == 0 || TravelState.StopsLeft <= 0) return false;
        var next = TravelGraph.Get(path[0]);
        if (next == null) return false;
        if (!TravelTutorial.CanMoveTo(next.id)) { Notice(TravelTutorial.BlockedReason(next.id)); return false; }

        _hopTo = next;
        _hopFrom = ContentPoint(TravelGraph.Get(TravelState.CurrentNodeId));
        _hopEnd = ContentPoint(next);
        _hopT = 0f;
        _hopSeconds = Mathf.Max(rvMinHopSeconds, Vector2.Distance(_hopFrom, _hopEnd) / Mathf.Max(1f, rvSpeed));
        return true;
    }

    void UpdateDrive()
    {
        if (!_driving || _hopTo == null) return;

        _hopT += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(_hopT / _hopSeconds);
        Vector2 at = Vector2.Lerp(_hopFrom, _hopEnd, Mathf.SmoothStep(0f, 1f, k));
        PlaceRv(at, _hopEnd - _hopFrom);
        // The view rides along with the RV rather than hopping node to node behind it.
        _focusTarget = ClampFocus(at, _zoomTarget);
        if (k < 1f) return;

        var arrived = _hopTo;
        _hopTo = null;
        if (!TravelState.MoveTo(arrived.id)) { StopDriving(); Refresh(); return; }
        _focusedOn = arrived.id;

        var dest = TravelGraph.Get(TravelState.DestinationId);
        if (arrived == dest)
        {
            StopDriving();
            Notice($"Made it to {arrived.name}.");
        }
        else if (!arrived.isCircuit)
        {
            // Anywhere that is not a racetrack is somewhere to stop: a shop, a yard, your own factory.
            StopDriving();
            _stoppedOnRoute = true;
            Notice($"Pulled in at {MapName(arrived)}. Visit, or continue on?", sticky: true);
        }
        else if (!NextHop())
        {
            StopDriving();
            if (TravelState.StopsLeft <= 0) Notice("Out of stops. The flatbed knows the way.", sticky: true);
        }
        Refresh();
    }

    void StopDriving()
    {
        _driving = false;
        _hopTo = null;
        _rvParked = true;
    }

    // The RV sprite, made on first use. Pointing along `heading` when it has one; the art faces left.
    void PlaceRv(Vector2 at, Vector2 heading)
    {
        if (_rv == null)
        {
            var go = new GameObject("RV", typeof(RectTransform), typeof(Image));
            _rv = (RectTransform)go.transform;
            _rv.SetParent(nodesRoot, false);
            _rv.anchorMin = _rv.anchorMax = _rv.pivot = new Vector2(0.5f, 0.5f);
            _rv.sizeDelta = rvSize;
            var img = go.GetComponent<Image>();
            img.sprite = Resources.Load<Sprite>(RvSpritePath);
            img.preserveAspect = true;
            img.raycastTarget = false;
        }
        _rv.gameObject.SetActive(true);
        _rv.localPosition = at;
        if (heading.sqrMagnitude > 0.01f)
            _rv.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg + 180f);
        _rv.SetAsLastSibling();
    }

    // Once it has set off, the RV stays on the board — on the road or parked where it stopped — and does
    // the "you are here" pin's job; before that (or after a tow) the pin does.
    void ShowRvOrPin(TravelNode current)
    {
        bool rv = _driving || _rvParked;
        if (_rv != null) _rv.gameObject.SetActive(rv);
        if (rv && herePin != null) herePin.gameObject.SetActive(false);
        if (rv && _rv != null) _rv.SetAsLastSibling();
    }

    // A place's name as the map shows it: somewhere never visited is still a "?".
    static string MapName(TravelNode n) =>
        n.isCircuit || n.locationType == TravelLocationType.TeamFactory || TravelState.IsVisited(n.id)
            ? n.name : "somewhere new";

    static string RouteSummary()
    {
        var path = TravelRoute.Path();
        var dest = TravelGraph.Get(TravelState.DestinationId);
        return path == null || dest == null ? "" : $"{path.Count} stops to {dest.name}.";
    }

    // ---------------- refresh (restyle everything from state) ----------------

    void Refresh()
    {
        var current = TravelGraph.Get(TravelState.CurrentNodeId);
        var dest = TravelGraph.Get(TravelState.DestinationId);
        if (TravelState.HasDestination && dest == null) TravelState.DestinationId = ""; // stale save vs renamed node
        bool choosing = dest == null;
        // Mid-weekend the car is going nowhere, so the map is just for browsing: no route, no next race,
        // no roads lit out of where you stand — only the "you are here" halo.
        bool browsing = WeekendHoldsTheCar;

        var path = choosing || browsing ? null : TravelRoute.Path();
        // The clock ticks half a day a stop, Mon AM to Fri AM (TravelDays), so it reads down with the stops.
        subLabel.text = browsing
            ? $"Race weekend · At {current.name}"
            : choosing
            ? $"{TravelState.DayLabel} · At {current.name} · Pick your next race"
            : $"{TravelState.DayLabel} · At {current.name} · ROUTE: {path?.Count ?? 0} · STOPS LEFT: {TravelState.StopsLeft}";

        // The view rides along with the player as they hop, keeping whatever zoom they chose.
        FollowFocus(current);

        if (browsing) RefreshMarkersBrowsing(current);
        else RefreshMarkers(choosing, current, dest, path);
        ShowRvOrPin(current);
        RefreshActions(choosing, current, dest, path);
        RefreshTutorialPrompt();
    }

    // The tutorial leg's instruction, in the strip the control hints use. Each wording is its own hint id:
    // re-pushing a live id only refreshes its timer, so a changed line has to replace the old one.
    string _tutorialPrompt;

    void RefreshTutorialPrompt()
    {
        // Mid-weekend there is no route to follow, so no instruction about one either.
        string text = WeekendHoldsTheCar ? null : TravelTutorial.PromptText();
        string id = text == null ? null : TravelTutorial.PromptId + ":" + text;
        if (id == _tutorialPrompt) return;

        if (_tutorialPrompt != null) ControlHints.Hide(_tutorialPrompt);
        _tutorialPrompt = id;
        if (id != null) ControlHints.ShowSticky(id, null, null, text, once: false, urgent: true);
    }

    // The controls hint sits in the footer whenever no notice is up, because the map no longer shows the
    // whole country at once. It names the controls of whatever is in the player's hands, so it is redrawn
    // when that changes.
    void ApplyHint()
    {
        _hintVersion = InputGlyphs.Version;
        _hintTouch = InputGlyphs.UsingTouch;
        _hint =
            InputGlyphs.UsingGamepad
                ? $"{InputGlyphs.PadName(PadBindings.MapZoomIn)} / {InputGlyphs.PadName(PadBindings.MapZoomOut)} zoom"
            : _hintTouch ? "Pinch zooms, two-finger drag pans"
            : "Wheel zooms, right-drag pans";
        noticeLabel.text = _hint;
        noticeLabel.color = HintColor;
    }

    // The browsing board: every place in its own colour, where you are framed in cream, and nothing else
    // marked — no destination flag, no route, no stops, no roads lit as the way out. Places stay clickable
    // so a tap can say why the car is staying put.
    void RefreshMarkersBrowsing(TravelNode current)
    {
        foreach (var marker in _markers.Values)
        {
            var n = marker.Node;
            bool isCurrent = n == current;
            marker.button.interactable = true;
            marker.halo.enabled = isCurrent;
            if (isCurrent) marker.halo.color = CurrentHalo;
            marker.dot.color = DotColor(n);
            marker.label.text = LabelFor(n);
            bool shout = isCurrent || n.locationType == TravelLocationType.TeamFactory;
            marker.label.fontSize = shout ? 16 : 8;
            marker.label.color = isCurrent ? CurrentHalo : LabelColor(n);
            _labelRank[marker] = LabelRank(n, shout, false);
        }
        PlacePin(herePin, current);
        PlacePin(destPin, null);
        TintEdges(null, null);
        DeclutterLabels();
    }

    void RefreshMarkers(bool choosing, TravelNode current, TravelNode dest, List<string> path)
    {
        string next = path != null && path.Count > 0 ? path[0] : null;
        var stops = choosing ? new List<string>() : TravelRoute.Stops();
        foreach (var marker in _markers.Values)
        {
            var n = marker.Node;
            bool isCurrent = n == current;
            bool isDest = n == dest;
            bool reachable = n.id == next && TravelState.StopsLeft > 0 && TravelTutorial.CanMoveTo(n.id);
            bool choosable = choosing && n.isCircuit && !isCurrent && TravelTutorial.CanChoose(n.id);
            bool waypoint = TravelTutorial.IsPendingWaypoint(n.id) || stops.Contains(n.id);
            // On the road every place is clickable — the next stop drives, the rest join the route. Choosing,
            // a race the tutorial rules out stays clickable so the click can say why not.
            bool ruledOut = choosing && n.isCircuit && !isCurrent;
            bool clickable = !choosing || choosable || ruledOut || isCurrent;

            marker.button.interactable = clickable;

            if (isCurrent) { marker.halo.enabled = true; marker.halo.color = CurrentHalo; }
            else if (isDest) { marker.halo.enabled = true; marker.halo.color = DestHalo; }
            else if (waypoint) { marker.halo.enabled = true; marker.halo.color = DestHalo; }
            else if (reachable || choosable) { marker.halo.enabled = true; marker.halo.color = ReachableHalo; }
            else marker.halo.enabled = false;

            marker.dot.color = DotColor(n);

            marker.label.text = (waypoint ? TravelTutorial.WaypointLabel(n.id) : null) ?? LabelFor(n);
            // The nodes that matter right now get the bigger type; the other fifty stay small so the
            // middle of the country does not turn into a wall of overlapping names.
            bool shout = isCurrent || isDest || waypoint || n.locationType == TravelLocationType.TeamFactory;
            marker.label.fontSize = shout ? 16 : 8;
            marker.label.color = isCurrent ? CurrentHalo : (isDest || waypoint ? DestHalo : LabelColor(n));
            _labelRank[marker] = LabelRank(n, shout, reachable || choosable);
        }

        // The two pins are single objects that hop about rather than one per node: where you are, and
        // the race you are driving to. They are what the eye should find first on a board of 75 dots.
        PlacePin(herePin, current);
        PlacePin(destPin, dest);
        TintEdges(current != null ? current.id : null, path);
        DeclutterLabels();
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
        if (n.isCircuit || n.locationType == TravelLocationType.TeamFactory) return n.name;
        if (!TravelState.IsVisited(n.id)) return "?";
        return (n.locationType == TravelLocationType.Junkyard ? "[J] " : "[E] ") + n.name;
    }

    // ---------------- footer buttons ----------------

    // The way on, in the footer beside the notice: GO down the route, START RACE WEEKEND at the race,
    // TOW when the stops run out, and the walk-around button at a minor location. The map itself says the
    // rest (where you are is the cream halo, the route is the light-blue road, the header has the counts).
    void RefreshActions(bool choosing, TravelNode current, TravelNode dest, List<string> path)
    {
        actionButton.gameObject.SetActive(false);
        // Optional: an older prefab may have no walk button at all (Draftmaster > Travel Map > Restyle
        // rebuilds it). Missing it should cost you the button, not the whole footer.
        if (walkButton != null) walkButton.gameObject.SetActive(false);

        // Mid-weekend or still choosing: nothing to press until a race is booked. On the road: nothing to
        // press until the RV pulls in.
        if (WeekendHoldsTheCar || choosing || _driving) return;

        if (current.isCircuit)
        {
            if (current == dest && TravelTutorial.CanStartWeekend)
                ShowAction("Start Race Weekend", () => StartRaceWeekend(current));
        }
        else if (walkButton != null)
        {
            // Park up and walk the place on foot (shared Landmark scene; costs nothing). The shops are there.
            walkButton.gameObject.SetActive(true);
            walkButton.GetComponentInChildren<Text>().text =
                current.locationType == TravelLocationType.TeamFactory ? "VISIT TEAM HQ" : "VISIT";
            walkButton.onClick.RemoveAllListeners();
            bool teamHQ = current.locationType == TravelLocationType.TeamFactory;
            walkButton.onClick.AddListener(() =>
            {
                if (teamHQ)
                {
                    if (!LandmarkLoader.TeamHQInBuild)
                    {
                        Notice("TeamGarage scene missing from Build Settings.");
                        return;
                    }
                    Close();
                    LandmarkLoader.VisitTeamHQ();
                    return;
                }
                if (!LandmarkLoader.SceneInBuild)
                {
                    Notice("Landmark scene missing from Build Settings — run Draftmaster > Travel Map > Build Landmark Scene.");
                    return;
                }
                Close();
                LandmarkLoader.Visit(TravelState.CurrentNodeId);
            });
        }

        // The way on: GO down the route (CONTINUE once pulled in on the way), unless the tutorial is still
        // waiting on Team HQ.
        string next = path != null && path.Count > 0 ? path[0] : null;
        if (current != dest && next != null && TravelState.StopsLeft > 0 && TravelTutorial.RouteReady
            && TravelTutorial.CanMoveTo(next))
            ShowAction(_stoppedOnRoute ? "Continue" : "Go", StartDrive);

        // Stranded? Tow covers the rest of the way, for a price. Never a softlock.
        if (current != dest && TravelState.StopsLeft <= 0)
        {
            ShowAction($"Tow to {dest.name} ({PlayerWallet.Format(TowCost)})", () =>
            {
                PlayerWallet.Add(-TowCost); // clamped at $0 — the tow always runs
                TravelState.CurrentNodeId = dest.id;
                TravelState.LegHops = Draftmaster.Tracks.TravelDays.HalfDaysOnTheRoad; // in for Friday morning
                TravelRoute.Clear();
                _stoppedOnRoute = _rvParked = false;
                Notice($"Towed to {dest.name}. The driver talked the whole way.");
                Refresh();
            });
        }
    }

    void ShowAction(string label, UnityEngine.Events.UnityAction onClick)
    {
        actionButton.gameObject.SetActive(true);
        actionButton.GetComponentInChildren<Text>().text = label;
        actionButton.onClick.RemoveAllListeners();
        actionButton.onClick.AddListener(onClick);
    }

    void StartRaceWeekend(TravelNode circuit)
    {
        if (WeekendHoldsTheCar) { Notice(Draftmaster.Weekend.WeekendDeparture.HeldReason); return; }
        if (TravelTutorial.LegBooked) TravelTutorial.Complete();
        TravelState.ArriveAndClearDestination();
        // The race scene builds whatever TrackSelection names, and a saved selection beats the travel
        // position — so arriving has to say so, or the weekend reloads the venue just left.
        TrackSelection.Select(circuit.id);
        RaceWeekend.ResetWeekend();
        // Load the circuit's scene when it's in the build; otherwise re-run the current (dev) scene —
        // the travel position still advances, so the map keeps working before every track is wired up.
        string scene = Application.CanStreamedLevelBeLoaded(circuit.id) ? circuit.id : SceneManager.GetActiveScene().name;
        CoopScene.Load(scene);   // fast travel takes a co-op guest with it
    }
}
