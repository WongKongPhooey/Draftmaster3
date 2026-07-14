using System;
using UnityEngine;

// The pace/safety car. Leads the field around one formation lap at a fixed cruise pace, then dives
// into the pit lane and parks. Rides a kinematic SplineDriver on the main spline; this component only
// caps its pace, counts the lap, triggers the pit-in, blinks its roof light and despawns afterwards.
[RequireComponent(typeof(SplineDriver))]
public class SafetyCar : MonoBehaviour
{
    [Tooltip("Constant pace around the formation lap (mph).")]
    public float cruiseMph = 60f;
    [Tooltip("Fraction of a full lap that must be covered before the car is allowed to pit. Guards against an early trigger.")]
    [Range(0.5f, 1f)] public float minLapFractionBeforePit = 0.8f;
    [Tooltip("Commit to the pit lane when within this distance (m) of the authored pit-entry node — the same continuous merge an AI pit stop uses. Lane-flipping anywhere else snaps onto the nearest pit-spline point, which past the entry means driving across the pit wall.")]
    public float pitEntryWindow = 20f;
    [Tooltip("Seconds after pit-in before the safety car is hidden (lets it roll out of sight down the pit).")]
    public float despawnAfterPitSeconds = 6f;
    [Tooltip("Park this far (m) BEFORE the first pit box (the one nearest the pit entrance), so the pace car stops at the start of the lane rather than rolling down it.")]
    public float parkGapBeforeFirstBox = 4f;

    [Header("Close-up / peel-away")]
    [Tooltip("When this far (m) or less before the start/finish line, the field bunches up into tight rows AND the pace car begins peeling away (true to life).")]
    public float closeUpDistanceM = 500f;
    [Tooltip("Pace (mph) the pace car accelerates to through the close-up zone, pulling AWAY from the leader so it opens a clear gap and reaches the pit lane before the green. Must exceed cruiseMph or the field rear-ends it.")]
    public float peelAwayMph = 95f;

    // True while the pace car is in the close-up zone near the line: the field packs into tight two-wide rows
    // (read by FormationControllers via FormationDirector) while the pace car simultaneously peels away to the pit.
    public bool ClosingUp { get; private set; }

    [Header("Roof light")]
    public Color rooflightColor = new Color(1f, 0.55f, 0f, 1f);
    public float blinkInterval = 0.35f;
    public Vector3 rooflightLocalOffset = new Vector3(0f, 0.6f, 0f);
    public float rooflightSize = 0.6f;

    // Fired once, the instant the car commits to pit entry — the cue for the race to go green.
    public event Action OnPitEntry;

    SplineDriver _spline;
    float _travelled;
    float _prevDist;
    bool _hasPrev;
    bool _pitting;
    float _pitEntryDistance;
    float _despawnTimer;

    SpriteRenderer _light;
    float _blinkTimer;
    bool _lightOn;

    void Awake()
    {
        _spline = GetComponent<SplineDriver>();
        // Drive the main spline at cruise, once round, and DON'T auto-rejoin from the pit — we park there.
        _spline.aiMaxSpeedMph = cruiseMph;
        _spline.loop = true;
        _spline.autoPitExit = false;
        _spline.freezeUntilFormation = true;
        _spline.tacticalLateralOffset = 0f;
    }

    void Start()
    {
        BuildRoofLight();

        // Pit-in triggers on PROXIMITY to the authored entry node, not on accumulated travel. The old
        // travel-arc trigger measured from pitExitDistance, but the car actually spawns at pitExitDistance
        // + safetyCarStartOffset (FormationDirector) and the min-lap floor could push the trigger further
        // still — firing PAST the entry, where the lane flip snaps across the pit wall.
        _pitEntryDistance = (_spline.track != null && _spline.track.track != null)
            ? _spline.track.track.PitEntryDistanceOnLap
            : -1f;
    }

    void FixedUpdate()
    {
        BlinkLight();

        if (RaceStart.Current != RaceStart.Phase.Formation) return;
        _spline.aiMaxSpeedMph = cruiseMph;

        if (_pitting)
        {
            // The car hard-parks at _spline.pitParkDistance (set in PitIn) — just before the first box at the
            // pit-lane entrance — so it stops at the start of the lane, not out by the exit.
            _despawnTimer -= Time.fixedDeltaTime;
            if (_despawnTimer <= 0f) gameObject.SetActive(false);
            return;
        }

        // Accumulate distance covered on the main spline (ignore once we've dived into the pit).
        float lap = _spline.TrackLength;
        float cur = _spline.DistanceOnTrack;
        if (_hasPrev && lap > 0f)
        {
            float delta = cur - _prevDist;
            if (delta < -lap * 0.5f) delta += lap;   // forward wrap past start/finish
            else if (delta > lap * 0.5f) delta -= lap;
            if (delta > 0f) _travelled += delta;
        }
        _prevDist = cur;
        _hasPrev = true;

        // Close-up: within closeUpDistanceM of the start/finish line (distance 0), the field concertinas up tight
        // (ClosingUp tells the FormationControllers to pack into rows and hold pace) while the pace car ACCELERATES
        // to peelAwayMph and dives for the pit — opening a clear gap so it's off the racing surface before the green.
        // Slowing here (the old behaviour) just let the bunched field pile onto it. Guarded by _travelled so the car
        // doesn't bolt off the line if it happens to spawn inside the zone.
        ClosingUp = false;
        if (lap > 0f && _travelled > closeUpDistanceM)
        {
            float toLine = lap - cur; // cur in [0, lap); distance forward to the next start/finish crossing
            if (toLine <= closeUpDistanceM)
            {
                ClosingUp = true;
                _spline.aiMaxSpeedMph = Mathf.Max(peelAwayMph, cruiseMph);
            }
        }

        // Dive in as the car reaches the entry node, once it's covered enough of the lap. If the floor isn't
        // met at the first pass the car simply paces another lap and pits next time round — never a wall shot.
        if (_pitEntryDistance >= 0f && lap > 0f && _travelled >= lap * minLapFractionBeforePit)
        {
            float gap = _pitEntryDistance - cur;
            if (gap < 0f) gap += lap;
            if (gap <= pitEntryWindow) PitIn();
        }
    }

    void PitIn()
    {
        _pitting = true;
        ClosingUp = false; // pace car is leaving the surface; the field launches at green, not row-packs any more
        _despawnTimer = despawnAfterPitSeconds;

        // Park in an "invisible box" one box pitch before the first real box at the pit ENTRANCE
        // (PitLane.LastBox = highest index = nearest the start of the lane = smallest box distance), and on
        // the grey box-lane lateral — NOT the pit centerline, which is the driving line the field files down.
        // Fall back to a short distance in if the boxes aren't configured.
        float pitLen = _spline.PitLength;
        float target;
        if (PitLane.Configured && pitLen > 0f)
        {
            target = PitLane.BoxDistance(PitLane.LastBox, pitLen) - Mathf.Max(parkGapBeforeFirstBox, PitLane.Spacing);
            // Set before the lane flip: the rejoin carries the car's current lateral as an easing bias, so it
            // drifts from the entry onto the box strip over the drive down instead of snapping sideways.
            _spline.lateralOffset = PitLane.ParkLateral;
        }
        else
            target = Mathf.Min(8f, pitLen > 0f ? pitLen * 0.15f : 8f);
        _spline.pitParkDistance = Mathf.Max(2f, target);

        _spline.usePitLane = true; // SplineDriver hops onto the pit spline at the entry node next step
        OnPitEntry?.Invoke();
    }

    void BuildRoofLight()
    {
        var go = new GameObject("Rooflight");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = rooflightLocalOffset;
        go.transform.localScale = Vector3.one * rooflightSize;

        _light = go.AddComponent<SpriteRenderer>();
        _light.sprite = UnitSquareSprite();
        _light.color = rooflightColor;
        _light.sortingLayerName = "Vehicles";
        _light.sortingOrder = 100;
    }

    void BlinkLight()
    {
        if (_light == null) return;
        _blinkTimer -= Time.deltaTime;
        if (_blinkTimer <= 0f)
        {
            _blinkTimer = blinkInterval;
            _lightOn = !_lightOn;
            var c = rooflightColor;
            c.a = _lightOn ? 1f : 0.15f;
            _light.color = c;
        }
    }

    static Sprite _square;
    static Sprite UnitSquareSprite()
    {
        if (_square != null) return _square;
        var tex = new Texture2D(2, 2);
        var px = new Color[] { Color.white, Color.white, Color.white, Color.white };
        tex.SetPixels(px);
        tex.Apply();
        _square = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
        return _square;
    }
}
