using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Draftmaster.Controls;
using Draftmaster.Sim;

// Broadcast mode: watch the race the way it goes out on TV. A square TV glyph sits in the HUD's bottom-right row,
// left of the crew chief's headset (CrewChiefController) and built the same way; tap it (or B) and the camera
// leaves the player's car to follow the race.
//
// - The camera cuts between cars on its own: a shot every `shotSeconds`, chosen by Draftmaster.Sim.BroadcastShots —
//   close battles first, the leader often, the rest of the field in turn.
// - A big hit (a car into the wall, or two cars together, at `bigHitMps` closing speed or more) cuts straight to the
//   car that took it and holds there for `incidentHoldSeconds`.
// - Along the top runs the broadcast ticker: a flag-coloured LAP x / y plate and the race clock, then the running
//   order crawling past — position, number, driver and their gap to the leader (or laps down). The car on camera
//   is picked out in it, and a caption under it says who that is. The driver's own HUD (IronOvalRaceHUD — POS, LAP,
//   speed) stands down while the ticker is up, since it would sit under it.
//
// The car changes hands the way the headset does it: a player driving when they switch over hands the car to Auto
// (DriveModeController), and on the way back Auto keeps it until they tap a pedal (DriveModeController.Toggle) —
// not mid-corner with the camera just back on them. A player already on Auto stays on Auto. A player on foot is
// stood still until the broadcast ends. Taking the car back some other way (V and a pedal) ends the broadcast.
//
// Only during the player's own race (RaceWeekend.SessionLive, race session): practice and qualifying have no running
// order to broadcast. Broadcast and the pit wall are both a camera away from the car, so one at a time: each hands
// over to the other without giving the car back in between.
public class BroadcastController : MonoBehaviour
{
    [Header("Refs (auto-found if empty)")]
    public DriveModeController driveMode;
    public CameraFollow cameraFollow;

    [Header("Input")]
    public Key toggleKey = Key.B;

    [Header("Director")]
    [Tooltip("Seconds on each car before the camera cuts to another.")]
    public float shotSeconds = 8f;
    [Tooltip("A car within this many seconds of the car ahead is in a battle, and the camera favours it.")]
    public float battleGapSeconds = 1f;
    [Tooltip("Seconds before a car the camera has just shown is worth showing again in full.")]
    public float revisitSeconds = 40f;
    [Tooltip("Closing speed (m/s) of a contact - wall or car - big enough to cut straight to it. Full-severity " +
             "damage is 18 m/s (VehicleCollision.damageSpeedFull).")]
    public float bigHitMps = 8f;
    [Tooltip("Seconds the camera stays on a car after cutting to its hit.")]
    public float incidentHoldSeconds = 8f;
    [Tooltip("A new hit never cuts away from a shot younger than this (s), so a pile-up doesn't ping the camera " +
             "between the cars in it.")]
    public float minShotSeconds = 2.5f;

    [Header("Ticker")]
    [Tooltip("How fast the running order crawls past, in 640x360 UI pixels per second.")]
    public float tickerSpeed = 36f;
    [Tooltip("Put the driver's own HUD (position, lap, speed) away while broadcasting; the ticker covers it.")]
    public bool hideDriverHud = true;

    [Header("HUD button")]
    [Tooltip("Side of the square icon button, in 640x360 UI pixels. Matches the crew chief's headset.")]
    public float buttonSize = 32f;
    [Tooltip("Gap from the bottom-right corner to the button, in 640x360 UI pixels: left of the headset " +
             "(CrewChiefController.buttonCorner.x + its buttonSize + 6). Where the headset is not offered, the " +
             "button takes the headset's own spot.")]
    public Vector2 buttonCorner = new Vector2(130f, 12f);

    public static bool IsBroadcasting { get; private set; }
    public static BroadcastController Current { get; private set; }

    bool _active;
    bool _keyPrev;
    bool _wasInCar;              // sat in the car (not walking) when the broadcast started
    bool _wasDriving;            // ...and driving it, rather than on Auto
    OnFootController _heldBody;  // the walking body this component froze for the duration
    bool _hudWasShown;
    bool _hudHidden;

    Transform _watching;
    float _shotTime;
    bool _showingHit;
    float _hitMps;
    readonly Dictionary<Transform, float> _lastShown = new();
    readonly List<float> _gapAhead = new();
    readonly List<float> _sinceShown = new();
    readonly List<Transform> _shotCars = new();

    float _raceClock;            // seconds the race has been running (green or caution), from this scene's start
    float _tickerOffset;         // UI px

    void OnEnable()
    {
        Current = this;
        VehicleCollision.AnyBarrierHit += OnBarrierHit;
        VehicleCollision.AnyCarHit += OnCarHit;
    }

    void OnDisable()
    {
        VehicleCollision.AnyBarrierHit -= OnBarrierHit;
        VehicleCollision.AnyCarHit -= OnCarHit;
        if (_active) Exit(giveCarBack: true);
        IsBroadcasting = false;
        if (Current == this) Current = null;
    }

    void Start()
    {
        if (driveMode == null) driveMode = GetComponent<DriveModeController>();
        if (driveMode == null) driveMode = FindFirstObjectByType<DriveModeController>();
        if (cameraFollow == null && Camera.main != null) cameraFollow = Camera.main.GetComponent<CameraFollow>();
        // With no headset on the HUD (a Master Draft race), the TV takes its corner spot.
        if (!GameSession.OnFootAllowed && !GameSession.IsSingleRace) buttonCorner.x = 92f;
        BuildButton();
        UpdateButton();
    }

    // The player's own race, while it runs.
    public bool Available => RaceWeekend.SessionLive && RaceWeekend.IsRaceSession && driveMode != null;

    // The TV is on the HUD in its bottom-right corner spot (not up in a phone's portrait top row), so anything
    // else in that row (TeamSwitchController's swap buttons) lines up to the left of it.
    public bool CornerButtonShown => _buttonRoot != null && _buttonRoot.activeInHierarchy && !_onTopRow;

    void Update()
    {
        if (RaceStart.IsRunning) _raceClock += Time.deltaTime;

        bool available = Available;
        if (_buttonRoot != null && _buttonRoot.activeSelf != available) _buttonRoot.SetActive(available);
        KeepClearOfTouchPedals();
        if (!available)
        {
            if (_active) Exit(giveCarBack: true);
            _keyPrev = Keyboard.current != null && toggleKey != Key.None && Keyboard.current[toggleKey].isPressed;
            return;
        }

        if (Keyboard.current != null && toggleKey != Key.None)
        {
            bool held = Keyboard.current[toggleKey].isPressed;
            if (held && !_keyPrev && !PadInput.ModalOpen) Toggle();
            _keyPrev = held;
        }

        if (!_active) return;

        // The player took the car back (V, then a pedal): they are driving, so the camera goes back to them.
        if (_wasInCar && driveMode != null && driveMode.IsDriving) { Exit(giveCarBack: false); return; }
        // Something else put them on the pit wall without asking us first.
        if (CrewChiefController.IsCrewChief) { Exit(giveCarBack: false); return; }

        _shotTime += Time.deltaTime;
        // A car that has retired is parked out of the race: nothing to watch. One due to retire is only held for
        // the end of its own crash shot - the camera staying on it is what keeps it from being moved.
        bool lost = _watching == null || !_watching.gameObject.activeInHierarchy
                    || AIRetirement.IsRetired(_watching) || (!_showingHit && AIRetirement.IsOutOfRace(_watching));
        if (lost || _shotTime >= (_showingHit ? incidentHoldSeconds : shotSeconds)) NextShot();

        if (cameraFollow != null && _watching != null && cameraFollow.target != _watching)
            cameraFollow.target = _watching;

        _tickerOffset = BroadcastShots.Scroll(_tickerOffset, tickerSpeed, Time.deltaTime, _stripWidthUi);
        _tickerItemsAge += Time.unscaledDeltaTime;
    }

    public void Toggle() { if (_active) Exit(giveCarBack: true); else Enter(); }

    void Enter()
    {
        if (!Available) return;

        // Off the pit wall first, keeping the car where it is (with the AI if the chief had given it to them).
        bool chiefWasDriving = false;
        bool fromChief = false;
        if (CrewChiefController.IsCrewChief)
        {
            var chief = FindFirstObjectByType<CrewChiefController>();
            if (chief != null) fromChief = chief.HandOverToBroadcast(out chiefWasDriving);
        }

        var body = OnFootController.Current;
        _wasInCar = body == null;
        _wasDriving = _wasInCar && driveMode != null && (driveMode.IsDriving || (fromChief && chiefWasDriving));

        if (driveMode != null)
        {
            driveMode.suppressBroadcastCamera = true;
            if (_wasInCar && driveMode.IsDriving) driveMode.SetDriving(false);
        }

        // Someone walking the paddock stands still while the camera is away, rather than wandering off unseen.
        _heldBody = null;
        if (body != null && !body.MovementLocked)
        {
            body.MovementLocked = true;
            _heldBody = body;
        }

        var zoom = FindFirstObjectByType<PitLaneStart>();
        if (zoom != null) zoom.SetZoomTarget(zoom.DrivingZoom);

        var hud = IronOvalRaceHUD.Instance;
        _hudHidden = false;
        if (hideDriverHud && hud != null)
        {
            _hudWasShown = hud.show;
            hud.show = false;
            _hudHidden = true;
        }

        _active = true;
        IsBroadcasting = true;
        _lastShown.Clear();
        _watching = null;
        _showingHit = false;
        _tickerItemsAge = float.PositiveInfinity;   // rebuild the crawl on the first draw
        NextShot();
        UpdateButton();
    }

    // Back to the player. `giveCarBack`: a player who was driving when the broadcast started gets their car back
    // on the next pedal tap (Auto's usual hand-back). Off when the car is already theirs, or is going to the pit
    // wall with the AI still driving it.
    void Exit(bool giveCarBack)
    {
        _active = false;
        IsBroadcasting = false;
        _watching = null;
        _showingHit = false;

        if (_hudHidden && IronOvalRaceHUD.Instance != null) IronOvalRaceHUD.Instance.show = _hudWasShown;
        _hudHidden = false;

        var zoom = FindFirstObjectByType<PitLaneStart>();
        if (_heldBody != null)
        {
            _heldBody.MovementLocked = false;
            if (cameraFollow != null) cameraFollow.target = _heldBody.transform;
            if (zoom != null) zoom.SetZoomTarget(zoom.OnFootZoom);
            _heldBody = null;
        }
        else if (OnFootController.Current != null)
        {
            // On foot but held by something else (a conversation, a cutscene): still their body the camera goes to.
            if (cameraFollow != null) cameraFollow.target = OnFootController.Current.transform;
            if (zoom != null) zoom.SetZoomTarget(zoom.OnFootZoom);
        }
        else if (driveMode != null)
        {
            if (giveCarBack && _wasDriving && !driveMode.IsDriving && !driveMode.HandBackPending) driveMode.Toggle();
            if (cameraFollow != null && driveMode.PlayerCar != null) cameraFollow.target = driveMode.PlayerCar.transform;
        }
        if (driveMode != null && !CrewChiefController.IsCrewChief) driveMode.suppressBroadcastCamera = false;

        _wasInCar = false;
        _wasDriving = false;
        UpdateButton();
    }

    // The crew chief's headset was tapped mid-broadcast. The broadcast ends without handing the car back - the
    // chief keeps it on the AI - and answers whether the player had been driving it, so the chief gives it back
    // when the headset comes off. False when there was no broadcast to end.
    public bool HandOverToCrewChief(out bool wasDriving)
    {
        wasDriving = false;
        if (!_active) return false;
        wasDriving = _wasDriving;
        Exit(giveCarBack: false);
        return true;
    }

    // ---- The director ----

    void NextShot()
    {
        var rt = RacePositionTracker.Instance;
        var order = rt != null ? rt.Order : null;
        _gapAhead.Clear();
        _sinceShown.Clear();
        _shotCars.Clear();
        int current = BroadcastShots.None;
        float now = Time.time;
        int prevUsable = -1;
        for (int i = 0; order != null && i < order.Count; i++)
        {
            var e = order[i];
            var tf = e != null ? e.tf : null;
            // Crashed and parked out of the race (or about to be): never a shot.
            bool usable = tf != null && tf.gameObject.activeInHierarchy && !AIRetirement.IsOutOfRace(tf);
            // The gap to the car ahead that is still racing, not to a wreck parked laps down between them.
            float gap = i == 0 || !usable ? 0f
                      : prevUsable < 0 ? float.PositiveInfinity   // nobody racing ahead of it: no battle
                      : Mathf.Max(0f, e.gapToLeaderSec - order[prevUsable].gapToLeaderSec);
            if (usable) prevUsable = i;
            _shotCars.Add(usable ? tf : null);
            _gapAhead.Add(i == 0 ? 0f : (usable ? gap : float.PositiveInfinity));
            // A car that cannot be shown is "just shown", so it is only ever picked when nothing else is left.
            _sinceShown.Add(!usable ? 0f : (_lastShown.TryGetValue(tf, out float t) ? now - t : float.PositiveInfinity));
            if (tf != null && tf == _watching) current = i;
        }

        int pick = BroadcastShots.Pick(_gapAhead, _sinceShown, current, battleGapSeconds, revisitSeconds);
        Transform car = pick >= 0 && pick < _shotCars.Count ? _shotCars[pick] : null;
        if (car == null && driveMode != null && driveMode.PlayerCar != null) car = driveMode.PlayerCar.transform;
        Show(car, hit: false, mps: 0f);
    }

    void Show(Transform car, bool hit, float mps)
    {
        _watching = car;
        _shotTime = 0f;
        _showingHit = hit;
        _hitMps = hit ? mps : 0f;
        if (car != null) _lastShown[car] = Time.time;
        if (cameraFollow != null && car != null) cameraFollow.target = car;
    }

    void OnBarrierHit(Transform car, float closingMps) => ConsiderHit(car, closingMps);
    void OnCarHit(Transform car, Transform other, float closingMps) => ConsiderHit(car, closingMps);

    void ConsiderHit(Transform car, float closingMps)
    {
        if (!_active || car == null || !RaceStart.IsRunning) return;
        if (closingMps < bigHitMps) return;   // the cheap test first: contacts fire every physics step
        // The same incident, still going: remember how hard it got, but stay on it.
        if (_showingHit && car == _watching) { _hitMps = Mathf.Max(_hitMps, closingMps); return; }
        if (!BroadcastShots.CutToHit(closingMps, bigHitMps, _shotTime, minShotSeconds, _showingHit, _hitMps)) return;
        if (!InRunningOrder(car)) return;   // the safety car, a parked car in the paddock
        if (AIRetirement.IsRetired(car)) return;   // a wreck already parked out of the race, being nudged
        Show(car, hit: true, mps: closingMps);
    }

    static bool InRunningOrder(Transform car)
    {
        var rt = RacePositionTracker.Instance;
        if (rt == null) return false;
        var order = rt.Order;
        for (int i = 0; i < order.Count; i++)
            if (order[i] != null && order[i].tf == car) return true;
        return false;
    }

    // ---- The ticker (IMGUI, the race HUD's own front end) ----

    struct TickerItem
    {
        public string pos, num, name, gap;
        public float wPos, wNum, wName, wGap, width;   // screen px at the scale they were measured at
        public Transform tf;
    }

    readonly List<TickerItem> _tickerItems = new();
    float _tickerItemsAge = float.PositiveInfinity;
    float _stripWidthUi;          // one full copy of the crawl, in UI px (Scroll works in UI px)
    GUIStyle _sData, _sLabel;
    int _stylesForScale;
    const float TickerRebuildSeconds = 0.5f;

    void OnGUI()
    {
        if (!_active) return;
        if (RacePauseMenu.IsPaused || PhoneUI.IsOpen) return;
        EnsureStyles();

        float scale = Mathf.Max(1, PixelGUI.Scale);
        float pad = PixelGUI.Px(4f);
        float barH = PixelGUI.SnapUp(_sData.CalcSize(new GUIContent("0")).y + PixelGUI.Px(2f));
        // On a phone the touch controls' top row (pause, Auto, the headset) owns the top edge; the ticker goes under it.
        float top = 0f;
        if (TouchDriveControls.Active)
        {
            var slot = TouchDriveControls.CrewChiefSlot;
            top = Mathf.Ceil(slot.yMax + PixelGUI.Px(4f));
        }

        var bar = new Rect(0f, top, Screen.width, barH);
        PixelGUI.Fill(bar, new Color(PixelGUI.PlateDeep.r, PixelGUI.PlateDeep.g, PixelGUI.PlateDeep.b, 0.92f));
        PixelGUI.Fill(new Rect(0f, bar.yMax, Screen.width, PixelGUI.Px(1f)), PixelGUI.Ink);

        // The flag plate: LAP x / y on the colour of the flag that is out.
        var rt = RacePositionTracker.Instance;
        string lap = LapText(rt);
        Color flag = RaceStart.IsCaution ? PixelGUI.Gold : RaceStart.IsGreen ? PixelGUI.Confirm : PixelGUI.TextDim;
        float lapW = PixelGUI.SnapUp(_sData.CalcSize(new GUIContent(lap)).x + pad * 2f);
        var lapRect = new Rect(0f, top, lapW, barH);
        PixelGUI.Fill(lapRect, flag);
        DrawText(new Rect(lapRect.x + pad, top, lapW, barH), lap, PixelGUI.Ink);

        // The race clock beside it.
        string clock = BroadcastShots.Clock(_raceClock);
        float clockW = PixelGUI.SnapUp(_sData.CalcSize(new GUIContent("0:00:00")).x + pad * 2f);
        var clockRect = new Rect(lapRect.xMax, top, clockW, barH);
        PixelGUI.Fill(clockRect, PixelGUI.Ink);
        DrawText(new Rect(clockRect.x + pad, top, clockW, barH), clock, PixelGUI.Text);

        // The crawl, clipped to the rest of the bar. Re-measured a couple of times a second (the gaps tick over), on
        // the layout pass so every pass of a frame draws the same strip.
        if (Event.current.type == EventType.Layout && _tickerItemsAge >= TickerRebuildSeconds)
        {
            RebuildTicker(rt);
            _tickerItemsAge = 0f;
        }

        var crawl = new Rect(clockRect.xMax, top, Mathf.Max(0f, Screen.width - clockRect.xMax), barH);
        float strip = _stripWidthUi * scale;
        if (strip > 0f && crawl.width > 0f)
        {
            GUI.BeginGroup(crawl);
            float x = -_tickerOffset * scale;
            while (x < crawl.width)
            {
                for (int i = 0; i < _tickerItems.Count && x < crawl.width; i++)
                {
                    var it = _tickerItems[i];
                    if (x + it.width > 0f) DrawItem(it, x, barH, pad);
                    x += it.width;
                }
            }
            GUI.EndGroup();
        }

        DrawOnCameraCaption(rt, bar.yMax + PixelGUI.Px(3f), pad);
    }

    void DrawItem(TickerItem it, float x, float h, float pad)
    {
        bool onCamera = it.tf != null && it.tf == _watching;
        if (onCamera)
            PixelGUI.Fill(new Rect(x + pad * 0.5f, PixelGUI.Px(1f), it.width - pad, h - PixelGUI.Px(2f)),
                          _showingHit ? PixelGUI.Danger : PixelGUI.Info);
        float cx = x + pad * 2f;
        DrawText(new Rect(cx, 0f, it.wPos, h), it.pos, PixelGUI.Gold); cx += it.wPos + pad;
        DrawText(new Rect(cx, 0f, it.wNum, h), it.num, PixelGUI.TextDim); cx += it.wNum + pad;
        DrawText(new Rect(cx, 0f, it.wName, h), it.name, PixelGUI.Text); cx += it.wName + pad;
        DrawText(new Rect(cx, 0f, it.wGap, h), it.gap, onCamera ? PixelGUI.Text : PixelGUI.TextDim);
    }

    // Under the ticker, left: who the camera is on, and CONTACT while it is on a hit.
    void DrawOnCameraCaption(RacePositionTracker rt, float y, float pad)
    {
        if (_watching == null || rt == null) return;
        RacePositionTracker.Entry e = null;
        var order = rt.Order;
        for (int i = 0; i < order.Count; i++)
            if (order[i] != null && order[i].tf == _watching) { e = order[i]; break; }
        if (e == null) return;

        string who = "P" + e.position + "  #" + e.carNumber + "  " + DisplayName(e).ToUpperInvariant();
        float h = PixelGUI.SnapUp(_sLabel.CalcSize(new GUIContent("P")).y + pad);
        float w = PixelGUI.SnapUp(_sLabel.CalcSize(new GUIContent(who)).x + pad * 2f);
        var plate = new Rect(PixelGUI.Px(8f), y, w, h);
        PixelGUI.Fill(plate, new Color(PixelGUI.PlateDeep.r, PixelGUI.PlateDeep.g, PixelGUI.PlateDeep.b, 0.92f));
        DrawText(new Rect(plate.x + pad, y, w, h), who, PixelGUI.Text, _sLabel);
        if (!_showingHit) return;

        const string tag = "CONTACT";
        float tw = PixelGUI.SnapUp(_sLabel.CalcSize(new GUIContent(tag)).x + pad * 2f);
        // Blinks, like the HUD's caution flag.
        if (Mathf.Repeat(Time.unscaledTime, 0.6f) > 0.4f) return;
        var tagRect = new Rect(plate.xMax, y, tw, h);
        PixelGUI.Fill(tagRect, PixelGUI.Danger);
        DrawText(new Rect(tagRect.x + pad, y, tw, h), tag, PixelGUI.Text, _sLabel);
    }

    void RebuildTicker(RacePositionTracker rt)
    {
        _tickerItems.Clear();
        var order = rt != null ? rt.Order : null;
        if (order == null || order.Count == 0) { _stripWidthUi = 0f; return; }
        float scale = Mathf.Max(1, PixelGUI.Scale);
        float pad = PixelGUI.Px(4f);
        float lead = order[0] != null ? order[0].progress : 0f;
        float width = 0f;
        for (int i = 0; i < order.Count; i++)
        {
            var e = order[i];
            if (e == null || e.tf == null) continue;
            var it = new TickerItem
            {
                pos = e.position.ToString(),
                num = "#" + e.carNumber,
                name = Surname(DisplayName(e)).ToUpperInvariant(),
                gap = BroadcastShots.Gap(e.position, e.gapToLeaderSec,
                                         BroadcastShots.LapsDown(lead, e.progress, rt.TrackLength)),
                tf = e.tf,
            };
            it.wPos = Mathf.Ceil(_sData.CalcSize(new GUIContent(it.pos)).x);
            it.wNum = Mathf.Ceil(_sData.CalcSize(new GUIContent(it.num)).x);
            it.wName = Mathf.Ceil(_sData.CalcSize(new GUIContent(it.name)).x);
            it.wGap = Mathf.Ceil(_sData.CalcSize(new GUIContent(it.gap)).x);
            // Two pads each side of the entry, one between its four pieces.
            it.width = PixelGUI.SnapUp(it.wPos + it.wNum + it.wName + it.wGap + pad * 7f);
            width += it.width;
            _tickerItems.Add(it);
        }
        float newStrip = width / scale;
        // Keep the crawl where it was in the strip when the strip changes length (gaps tick over every rebuild).
        if (_stripWidthUi > 0f && newStrip > 0f) _tickerOffset = _tickerOffset % newStrip;
        _stripWidthUi = newStrip;
    }

    // The player's car is "You" in the tracker; on TV it has their name like everyone else.
    static string DisplayName(RacePositionTracker.Entry e)
    {
        if (e.isPlayer && e.tf != null)
        {
            var label = e.tf.GetComponent<DriverLabel>();
            if (label != null && !string.IsNullOrEmpty(label.driverName)) return label.driverName;
        }
        return string.IsNullOrEmpty(e.name) ? "?" : e.name;
    }

    static string Surname(string name)
    {
        name = name.Trim();
        int sp = name.LastIndexOf(' ');
        return sp >= 0 && sp < name.Length - 1 ? name.Substring(sp + 1) : name;
    }

    // LAP 12/200, from the leader. No race distance (or none known yet): LAP 12.
    static string LapText(RacePositionTracker rt)
    {
        int lap = 1;
        if (rt != null && rt.Order.Count > 0 && rt.Order[0] != null) lap = Mathf.Max(1, rt.Order[0].lap + 1);
        var director = RaceDirector.Instance;
        int total = director != null ? director.raceLaps : 0;
        if (!RaceStart.IsRunning) return total > 0 ? "LAP 0/" + total : "LAP 0";
        return total > 0 ? "LAP " + Mathf.Min(lap, total) + "/" + total : "LAP " + lap;
    }

    void EnsureStyles()
    {
        int scale = PixelGUI.Scale;
        if (_sData != null && _stylesForScale == scale && _sData.fontSize == PixelGUI.Data.fontSize) return;
        _sData = Flat(PixelGUI.Data);
        _sLabel = Flat(PixelGUI.Label);
        _stylesForScale = scale;
        _tickerItemsAge = float.PositiveInfinity;   // widths were measured at the old size
    }

    static GUIStyle Flat(GUIStyle from)
    {
        var s = new GUIStyle(from)
        {
            alignment = TextAnchor.MiddleLeft,
            clipping = TextClipping.Overflow,   // Clip drops a line that doesn't fit whole; the bar bounds it anyway
            wordWrap = false,
            padding = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(0, 0, 0, 0),
        };
        return s;
    }

    void DrawText(Rect r, string text, Color colour, GUIStyle style = null)
    {
        style ??= _sData;
        var prev = style.normal.textColor;
        style.normal.textColor = colour;
        GUI.Label(r, text, style);
        style.normal.textColor = prev;
    }

    // ---- The HUD button (uGUI, alongside the crew chief's headset) ----

    GameObject _buttonRoot;
    Canvas _canvas;
    CanvasScaler _scaler;
    Image _face, _icon;
    Vector2 _iconSize;
    float _lift = -1f;
    bool _onTopRow;
    Rect _topSlot;
    float _topScale;

    void BuildButton()
    {
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        _canvas = PixelUI.CreateCanvas("BroadcastCanvas", 111);
        _scaler = _canvas.GetComponent<CanvasScaler>();
        if (_scaler != null) _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        FitCanvasToShortSide();

        var button = IronOvalUI.IconButton(_canvas.transform, "BroadcastButton", TvGlyph(), buttonSize);
        var shadow = (RectTransform)button.transform.parent;   // IconButton returns the face; its root is the shadow
        Corner(shadow, buttonCorner);
        button.onClick.AddListener(Toggle);
        _buttonRoot = shadow.gameObject;
        _buttonRoot.SetActive(Available);
        _face = button.GetComponent<Image>();
        var glyph = button.transform.Find("Icon");
        _icon = glyph != null ? glyph.GetComponent<Image>() : null;
        if (_icon != null) _iconSize = _icon.rectTransform.sizeDelta;
    }

    // Like the headset: on air the plate goes alarm red; off air it is the panel colour with the glyph dimmed.
    void UpdateButton()
    {
        if (_face != null) _face.color = _active ? PixelGUI.Danger : PixelGUI.PlateDeep;
        if (_icon != null) _icon.color = _active ? PixelGUI.Text : PixelGUI.TextDim;
    }

    // As CrewChiefController.KeepClearOfTouchPedals: stood on top of the phone's pedals while they show; on a phone
    // held upright, in the touch controls' top row, right of the headset.
    void KeepClearOfTouchPedals()
    {
        if (_buttonRoot == null || _canvas == null) return;
        float scale = FitCanvasToShortSide();
        var root = (RectTransform)_buttonRoot.transform;

        if (TouchDriveControls.Active && UnityEngine.Device.Screen.height > UnityEngine.Device.Screen.width)
        {
            var chief = TouchDriveControls.CrewChiefSlot;
            var slot = new Rect(chief.xMax + chief.width * 0.25f, chief.y, chief.width, chief.height);
            if (_onTopRow && slot == _topSlot && scale == _topScale) return;
            _onTopRow = true;
            _topSlot = slot;
            _topScale = scale;
            _lift = -1f;

            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(slot.x / scale, -slot.y / scale);
            SizeButton(slot.width / scale);
            return;
        }

        float lift = Mathf.Ceil(TouchDriveControls.PedalsTopFromBottom / scale);
        if (!_onTopRow && lift == _lift) return;
        _onTopRow = false;
        _lift = lift;
        SizeButton(buttonSize);
        Corner(root, new Vector2(buttonCorner.x, buttonCorner.y + lift));
    }

    float FitCanvasToShortSide()
    {
        float scale = DriveOrientation.CanvasScale(Screen.width, Screen.height, PixelUITheme.ReferenceHeight);
        if (_scaler != null && !Mathf.Approximately(_scaler.scaleFactor, scale)) _scaler.scaleFactor = scale;
        return scale;
    }

    void SizeButton(float size)
    {
        float k = buttonSize > 0f ? size / buttonSize : 1f;
        ((RectTransform)_buttonRoot.transform).sizeDelta = new Vector2(size, size);
        if (_face != null)
        {
            var face = _face.rectTransform;
            face.offsetMin = face.offsetMax = new Vector2(-3f * k, 3f * k);
        }
        if (_icon != null) _icon.rectTransform.sizeDelta = _iconSize * k;
    }

    // margin.x is stepped past the HUD's speed dial (IronOvalRaceHUD.DialCornerClearance).
    static void Corner(RectTransform rt, Vector2 margin)
    {
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-(margin.x + IronOvalRaceHUD.DialCornerClearance), margin.y);
    }

    // A 16x16 television, drawn here rather than imported so the kit's icon set needs no new slot: white on clear,
    // tinted by the button like the headset glyph. Rows top to bottom.
    static readonly string[] TvRows =
    {
        "................",
        "...#.......#....",
        "....#.....#.....",
        ".....#...#......",
        "......#.#.......",
        ".##############.",
        ".#............#.",
        ".#.#######..#.#.",
        ".#.#######....#.",
        ".#.#######..#.#.",
        ".#.#######....#.",
        ".#.#######....#.",
        ".#............#.",
        ".##############.",
        "..##........##..",
        "................",
    };

    static Sprite _tvGlyph;

    static Sprite TvGlyph()
    {
        if (_tvGlyph != null) return _tvGlyph;
        const int n = 16;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "BroadcastTvGlyph",
        };
        var px = new Color32[n * n];
        for (int row = 0; row < n; row++)
            for (int col = 0; col < n; col++)
                px[(n - 1 - row) * n + col] = TvRows[row][col] == '#' ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
        tex.SetPixels32(px);
        tex.Apply();
        _tvGlyph = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        _tvGlyph.name = "BroadcastTvGlyph";
        return _tvGlyph;
    }
}
