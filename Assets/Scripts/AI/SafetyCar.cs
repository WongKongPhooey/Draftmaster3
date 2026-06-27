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
    [Tooltip("Seconds after pit-in before the safety car is hidden (lets it roll out of sight down the pit).")]
    public float despawnAfterPitSeconds = 6f;
    [Tooltip("Park this far (m) BEFORE the first pit box (the one nearest the pit entrance), so the pace car stops at the start of the lane rather than rolling down it.")]
    public float parkGapBeforeFirstBox = 4f;

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
    float _triggerTravel;
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

        float lap = _spline.TrackLength;
        if (_spline.track != null && _spline.track.track != null && lap > 0f)
        {
            float entry = _spline.track.track.pitEntryDistance;
            float exit = _spline.track.track.pitExitDistance;
            // Forward arc from where we start (pit exit) round to the pit entry node.
            _triggerTravel = (((entry - exit) % lap) + lap) % lap;
            // If entry sits just after exit, that arc is ~a full lap already; otherwise enforce the floor.
            _triggerTravel = Mathf.Max(_triggerTravel, lap * minLapFractionBeforePit);
        }
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

        if (_travelled >= _triggerTravel) PitIn();
    }

    void PitIn()
    {
        _pitting = true;
        _despawnTimer = despawnAfterPitSeconds;

        // Park just before the first box at the pit ENTRANCE (PitLane.LastBox = highest index = nearest the start
        // of the lane = smallest box distance). Fall back to a short distance in if the boxes aren't configured.
        float pitLen = _spline.PitLength;
        float target;
        if (PitLane.Configured && pitLen > 0f)
            target = PitLane.BoxDistance(PitLane.LastBox, pitLen) - parkGapBeforeFirstBox;
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
