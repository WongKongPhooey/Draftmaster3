using UnityEngine;

// AI pit strategy: once the tyres are worn past a threshold, the car peels into the pit lane at the entry node,
// drives to its box, holds for a service stop (fresh tyres + repaired bodywork), then rejoins. The racing brain
// is parked while on pit road so it doesn't try to race down the lane.
[RequireComponent(typeof(SplineDriver))]
public class PitStopController : MonoBehaviour
{
    [Tooltip("Pit when average tyre wear exceeds this (0..1). A small random spread is added per car so the field doesn't all stop together.")]
    [Range(0.2f, 0.95f)] public float wearThreshold = 0.6f;
    [Tooltip("Seconds stationary in the box being serviced.")]
    public float serviceSeconds = 3.5f;
    [Tooltip("Where the service box sits along the pit lane (0..1).")]
    [Range(0.1f, 0.9f)] public float serviceFrac = 0.5f;
    [Tooltip("Commit to the pit lane when within this distance (m) of the pit-entry node.")]
    public float pitEntryWindow = 25f;
    [Tooltip("Reset bodywork damage during the stop too.")]
    public bool repairDamage = true;

    enum State { Racing, HeadingToPit, Servicing, Leaving }
    State _state = State.Racing;

    SplineDriver _spline;
    AIRacingBehaviour _ai;
    TireModel _tires;
    VehicleDamage _bodywork;
    float _serviceTimer;
    float _threshold;

    public bool IsPitting => _state != State.Racing;

    void Awake() { _spline = GetComponent<SplineDriver>(); _ai = GetComponent<AIRacingBehaviour>(); }

    void Start()
    {
        _tires = GetComponent<TireModel>();
        _bodywork = GetComponentInChildren<VehicleDamage>();
        _threshold = Mathf.Clamp01(wearThreshold + Random.Range(-0.06f, 0.06f));
    }

    void FixedUpdate()
    {
        if (!RaceStart.IsGreen || _spline == null || _spline.TrackLength <= 0f) return;
        var tb = _spline.track;
        if (tb == null || tb.track == null) return;

        switch (_state)
        {
            case State.Racing:
                if (WantsPit()) _state = State.HeadingToPit;
                break;

            case State.HeadingToPit:
                if (!_spline.IsOnPit)
                {
                    // Commit to the lane as we reach the entry node so the merge stays continuous.
                    float d = _spline.DistanceOnTrack;
                    float gap = tb.track.pitEntryDistance - d;
                    if (gap < 0f) gap += _spline.TrackLength;
                    if (gap < pitEntryWindow) _spline.usePitLane = true;
                }
                else
                {
                    if (_ai != null) _ai.enabled = false;     // park the racing brain on pit road
                    _spline.tacticalLateralOffset = 0f;
                    // Stop in this car's ASSIGNED box (= its grid position), not a generic fraction of the lane,
                    // so every car services in its own marked box. Falls back to serviceFrac if unconfigured.
                    float targetFrac = serviceFrac;
                    if (PitLane.Configured && _spline.PitLength > 0f)
                        targetFrac = PitLane.BoxDistance(_spline.qualifyingPosition, _spline.PitLength) / _spline.PitLength;
                    if (_spline.PitProgress01 >= targetFrac)
                    {
                        _spline.pitStopHold = true;
                        _serviceTimer = serviceSeconds;
                        _state = State.Servicing;
                    }
                }
                break;

            case State.Servicing:
                _serviceTimer -= Time.fixedDeltaTime;
                if (_serviceTimer <= 0f)
                {
                    if (_tires != null) _tires.PitReset();
                    if (repairDamage && _bodywork != null) _bodywork.RepairFull();
                    _spline.pitStopHold = false;
                    _state = State.Leaving;
                }
                break;

            case State.Leaving:
                if (!_spline.IsOnPit) // SplineDriver auto-exits the pit; we're back on track
                {
                    if (_ai != null) _ai.enabled = true;
                    _state = State.Racing;
                }
                break;
        }
    }

    bool WantsPit()
    {
        if (_spline.IsOnPit || _tires == null) return false;
        float avgWear = 0.5f * (_tires.FrontWear + _tires.RearWear);
        return avgWear >= _threshold;
    }
}
