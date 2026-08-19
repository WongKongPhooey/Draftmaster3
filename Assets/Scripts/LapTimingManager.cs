using System.Collections.Generic;
using UnityEngine;

// Times every car's laps off the RacePositionTracker's lap counter. A lap only counts if the car
// stayed on legal surface and never hit a wall: going fully off onto grass/gravel or a barrier
// contact invalidates the lap in progress (paved tarmac runoff is legal). Driving the
// pit lane voids the lap outright — timing re-arms at the next start/finish crossing.
// The crew-chief timing screen (TimingScreenUI) reads Rows; a small HUD shows the player's
// last/best lap and flashes when their current lap is invalidated.
public class LapTimingManager : MonoBehaviour
{
    public static LapTimingManager Instance { get; private set; }

    [Tooltip("Track used for the off-surface check. Auto-found if empty.")]
    public TrackBuilder track;
    [Tooltip("Barrier closing speed (m/s) below which a wall brush does NOT invalidate the lap.")]
    public float wallHitMinClosingSpeed = 1.5f;
    [Tooltip("Draw the player's last/best lap readout.")]
    public bool showPlayerHud = true;
    [Tooltip("Key toggling the lap readout (iRacing-style F1).")]
    public KeyCode toggleKey = KeyCode.F1;

    public class CarTimes
    {
        public Transform tf;
        public string name;
        public int carNumber;
        public bool isPlayer;

        public bool lapStarted;       // a timed lap is in progress
        public bool valid;            // ... and it hasn't been invalidated yet
        public float lapStartTime;
        public int prevLap;

        public float lastLap = -1f;
        public bool lastValid;
        public float bestLap = -1f;
        public int lapsCompleted;     // valid laps only

        public bool hooked;           // wall-hit event wired
        public float invalidFlashUntil;

        public float CurrentLapTime(float now) => lapStarted ? now - lapStartTime : -1f;
    }

    readonly Dictionary<Transform, CarTimes> _cars = new();
    readonly List<CarTimes> _rows = new();
    public IReadOnlyList<CarTimes> Rows => _rows;

    CarTimes _player;

    // Session order: fastest lap first, cars with no time yet at the back, ordered by laps run. This is
    // what decides a practice or qualifying session, so the timing screen, the Tab board and the grid
    // PracticeDirector captures all rank on it.
    public void RankByBest(List<CarTimes> into)
    {
        into.Clear();
        for (int i = 0; i < _rows.Count; i++)
            if (_rows[i] != null && _rows[i].tf != null) into.Add(_rows[i]);
        into.Sort(CompareByBest);
    }

    public static int CompareByBest(CarTimes a, CarTimes b)
    {
        bool aHas = a.bestLap > 0f, bHas = b.bestLap > 0f;
        if (aHas != bHas) return aHas ? -1 : 1;
        if (aHas) return a.bestLap.CompareTo(b.bestLap);
        return b.lapsCompleted.CompareTo(a.lapsCompleted);
    }

    public static LapTimingManager Ensure()
    {
        if (Instance == null)
        {
            var go = new GameObject("LapTimingManager");
            Instance = go.AddComponent<LapTimingManager>();
        }
        return Instance;
    }

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey)) showPlayerHud = !showPlayerHud;
        if (track == null) track = FindFirstObjectByType<TrackBuilder>();
        var rt = RacePositionTracker.Instance;
        if (rt == null || track == null) return;

        var order = rt.Order;
        for (int i = 0; i < order.Count; i++)
        {
            var e = order[i];
            if (e == null || e.tf == null) continue;

            if (!_cars.TryGetValue(e.tf, out var c))
            {
                c = new CarTimes { tf = e.tf, prevLap = e.lap };
                _cars[e.tf] = c;
                _rows.Add(c);
            }
            c.name = e.name;
            c.carNumber = e.carNumber;
            c.isPlayer = e.isPlayer;
            if (c.isPlayer) _player = c;
            HookWallHits(c);

            // Pit lane voids the lap in progress; timing re-arms at the next line crossing.
            bool onPit = e.spline != null && e.spline.enabled && e.spline.IsOnPit;
            if (onPit)
            {
                c.lapStarted = false;
                c.prevLap = e.lap;
                continue;
            }

            // Fully off the track surface (grass/gravel) invalidates the running lap. Paved (tarmac)
            // runoff is legal — running wide onto it costs time but keeps the lap — and so is a kerb,
            // which sits outboard of the road ribbon but is track as far as the rules are concerned.
            if (c.lapStarted && c.valid && !track.IsOnSurface(e.tf.position, out _))
            {
                bool onPaved = SurfaceField.TryGetSurface(e.tf.position, out var surf)
                               && (surf == TrackEnvironment.SurfaceType.TarmacRunoff
                                   || surf == TrackEnvironment.SurfaceType.Kerb);
                if (!onPaved) Invalidate(c);
            }

            if (e.lap > c.prevLap)
            {
                // Crossed the line going forward. The crossing was detected up to a frame late, so pull the
                // instant back by how far past the line the car already is.
                float dist = (e.spline != null && e.spline.enabled && e.spline.TrackLength > 0f)
                    ? e.spline.DistanceOnTrack
                    : track.NearestCenterlineDistance(e.tf.position);
                float overshoot = dist / Mathf.Max(e.speedMps, 1f);
                float crossTime = Time.time - Mathf.Min(overshoot, 0.25f);

                if (c.lapStarted)
                {
                    float t = crossTime - c.lapStartTime;
                    c.lastLap = t;
                    c.lastValid = c.valid;
                    if (c.valid && t > 5f)
                    {
                        c.lapsCompleted++;
                        if (c.bestLap < 0f || t < c.bestLap) c.bestLap = t;
                    }
                }
                c.lapStartTime = crossTime;
                c.lapStarted = true;
                c.valid = true;
            }
            else if (e.lap < c.prevLap)
            {
                // Backed up across the line — abandon the lap.
                c.lapStarted = false;
            }
            c.prevLap = e.lap;
        }

        // Drop despawned cars.
        for (int i = _rows.Count - 1; i >= 0; i--)
        {
            if (_rows[i].tf == null)
            {
                if (_rows[i] == _player) _player = null;
                _rows.RemoveAt(i);
            }
        }
        var dead = new List<Transform>();
        foreach (var kv in _cars) if (kv.Key == null) dead.Add(kv.Key);
        for (int i = 0; i < dead.Count; i++) _cars.Remove(dead[i]);
    }

    void Invalidate(CarTimes c)
    {
        if (!c.lapStarted || !c.valid) return;
        c.valid = false;
        if (c.isPlayer) c.invalidFlashUntil = Time.time + 2.5f;
    }

    void HookWallHits(CarTimes c)
    {
        if (c.hooked || c.tf == null) return;
        var vc = c.tf.GetComponent<VehicleCollision>();
        if (vc != null)
            vc.BarrierHit += closingSpeed =>
            {
                if (closingSpeed >= wallHitMinClosingSpeed) Invalidate(c);
            };
        c.hooked = true;
    }

    public static string Format(float t)
    {
        if (t < 0f) return "--:--.---";
        int mins = (int)(t / 60f);
        return $"{mins}:{t - mins * 60f:00.000}";
    }

    void OnGUI()
    {
        if (!showPlayerHud || _player == null) return;

        // The timing strip, Iron Oval: framed plate, VT323 so the digits hold their columns as they run,
        // and the invalidation notice in alarm red under it — the one thing here worth shouting.
        float w = PixelGUI.Px(212f), h = PixelGUI.Px(16f);
        float x = Mathf.Round((Screen.width - w) * 0.5f), y = PixelGUI.Px(6f);
        bool invalid = Time.time < _player.invalidFlashUntil;
        float panelH = h + PixelGUI.Px(10f) + (invalid ? h : 0f);

        PixelGUI.Panel(new Rect(x, y, w, panelH));
        var c = PixelGUI.PanelContent(new Rect(x, y, w, panelH), 1f);

        string cur = _player.lapStarted
            ? Format(_player.CurrentLapTime(Time.time)) + (_player.valid ? "" : " ✕")
            : "--:--.---";

        var style = PixelGUI.Data;
        var prevAlign = style.alignment;
        style.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(c.x, c.y, c.width, h),
                  $"LAP {cur}   LAST {Format(_player.lastLap)}   BEST {Format(_player.bestLap)}", style);

        if (invalid)
        {
            var prev = style.normal.textColor;
            style.normal.textColor = PixelGUI.Danger;
            GUI.Label(new Rect(c.x, c.y + h, c.width, h), "LAP INVALIDATED", style);
            style.normal.textColor = prev;
        }
        style.alignment = prevAlign;
    }
}
