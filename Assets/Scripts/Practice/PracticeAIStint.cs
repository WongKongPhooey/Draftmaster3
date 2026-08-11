using UnityEngine;

// Practice-session brain for one AI car: park in the assigned pit box, wait to be released by the
// PracticeDirector, drive out for a stint of N laps, then pit in and park again. Mirrors
// PitStopController's pit-entry mechanics (commit to the lane at the entry node, stop at the
// car's own box) but parks indefinitely instead of servicing and rejoining.
[RequireComponent(typeof(SplineDriver))]
public class PracticeAIStint : MonoBehaviour
{
    [Tooltip("Commit to the pit lane when within this distance (m) of the pit-entry node.")]
    public float pitEntryWindow = 25f;
    [Tooltip("Lateral speed (m/s) for moving between the parked box lane (PitLane.ParkLateral, wall side) and the pit-lane centerline. Cars DRIVE the centerline so they clear the parked file; they only cut to the box lane at their own box.")]
    public float laneChangeRate = 3f;
    [Tooltip("Braking rate (m/s^2) used to size the approach to the box, so the car arrives AT its box instead of sailing through it.")]
    public float boxApproachDecel = 5f;
    [Tooltip("Speed (mph) the car keeps creeping at over the last few metres into the box.")]
    public float boxCrawlMph = 5f;
    [Tooltip("Shortest run-in (m) over which the car may cut from the centerline to the box lane.")]
    public float minCutWindow = 12f;

    public enum State { Parked, Leaving, OnTrack, HeadingToPit, PittingIn }
    State _state = State.Parked;

    SplineDriver _spline;
    AIRacingBehaviour _ai;
    PlayerVehicleController _pvc;   // dynamic-AI motion model; null on kinematic cars
    PracticeDirector _director;

    int _lapsToRun;
    int _lapsDone;
    float _prevDist;
    bool _hasPrev;

    public State CurrentState => _state;
    public bool IsParked => _state == State.Parked;

    // Set by the director: earliest time this car may be sent out again.
    [HideInInspector] public float nextReleaseTime;

    void Awake()
    {
        _spline = GetComponent<SplineDriver>();
        _pvc = GetComponent<PlayerVehicleController>();
    }

    void Start()
    {
        // AIRacingBehaviour is added by AIDriverBinding.Apply() during spawn; grab it now and keep the
        // racing brain off while parked so it doesn't fight the pit hold.
        _ai = GetComponent<AIRacingBehaviour>();
        if (_ai != null) _ai.enabled = false;
    }

    public void Bind(PracticeDirector director) { _director = director; }

    public void Release(int laps)
    {
        if (_state != State.Parked) return;
        _lapsToRun = Mathf.Max(1, laps);
        _lapsDone = 0;
        _hasPrev = false;
        _spline.parkedHold = false;
        _spline.pitParkDistance = -1f;   // release the box stop line, else SplineDriver re-pins the car
        _spline.aiMaxSpeedMph = 200f;    // drop the approach cap so the car can run the lane out
        _spline.pitStopHold = false;
        _state = State.Leaving;
    }

    float ActualSpeedMps => _pvc != null && _pvc.enabled ? _pvc.SpeedMps : _spline.speed;

    void FixedUpdate()
    {
        if (_spline == null || _spline.TrackLength <= 0f) return;

        switch (_state)
        {
            case State.Leaving:
                // SplineDriver drives the pit lane at the pit limit and auto-merges onto the main spline.
                // Pull off the box lane onto the centerline first, so the run down the lane clears the
                // cars still parked in the boxes ahead.
                if (_spline.IsOnPit)
                {
                    _spline.lateralOffset = Mathf.MoveTowards(_spline.lateralOffset, 0f, laneChangeRate * Time.fixedDeltaTime);
                }
                else
                {
                    if (_ai == null) _ai = GetComponent<AIRacingBehaviour>();
                    if (_ai != null) _ai.enabled = true;
                    _hasPrev = false;
                    _state = State.OnTrack;
                }
                break;

            case State.OnTrack:
            {
                // Count start/finish crossings by the distance wrap.
                float d = _spline.DistanceOnTrack;
                if (_hasPrev && _prevDist - d > _spline.TrackLength * 0.5f) _lapsDone++;
                _prevDist = d;
                _hasPrev = true;
                if (_lapsDone >= _lapsToRun) _state = State.HeadingToPit;
                break;
            }

            case State.HeadingToPit:
                if (!_spline.IsOnPit)
                {
                    var tb = _spline.track;
                    if (tb != null && tb.track != null)
                    {
                        float gap = tb.track.PitEntryDistanceOnLap - _spline.DistanceOnTrack;
                        if (gap < 0f) gap += _spline.TrackLength;
                        if (gap < pitEntryWindow) _spline.usePitLane = true;
                    }
                }
                else
                {
                    if (_ai != null) _ai.enabled = false;   // don't race down the lane
                    _spline.tacticalLateralOffset = 0f;
                    _state = State.PittingIn;
                }
                break;

            case State.PittingIn:
            {
                // Brake to a stop in this car's assigned box, then pin it there. Drive the centerline
                // down the lane (clear of the parked file) and only cut across to the wall-side box
                // lane inside the last stretch before the car's own box.
                float targetFrac = 0.5f;
                float boxDist = _spline.PitLength * 0.5f;
                if (PitLane.Configured && _spline.PitLength > 0f)
                {
                    boxDist = PitLane.BoxDistance(_spline.qualifyingPosition, _spline.PitLength);
                    targetFrac = boxDist / _spline.PitLength;
                }
                float remaining = boxDist - _spline.PitProgress01 * _spline.PitLength;
                // Window sized from the lateral left to cover — the box lane sits several metres off the
                // centerline, so a fixed short run-in leaves the car stopping in the lane. Under the braking
                // ramp below the time left to the box is sqrt(2d/a), so d = (latRemain/rate)^2 * a/2 covers it.
                float latRemain = Mathf.Abs(PitLane.ParkLateral - _spline.lateralOffset);
                float latTime = latRemain / Mathf.Max(0.1f, laneChangeRate);
                float cutWindow = Mathf.Max(minCutWindow, 0.75f * Mathf.Max(0.5f, boxApproachDecel) * latTime * latTime);
                if (remaining < cutWindow)
                    _spline.lateralOffset = Mathf.MoveTowards(_spline.lateralOffset, PitLane.ParkLateral, laneChangeRate * Time.fixedDeltaTime);
                // Brake FOR the box rather than at it, and pin a stop line there, so the car doesn't roll on
                // into the next car's box.
                float approachMph = Mathf.Sqrt(2f * Mathf.Max(0.5f, boxApproachDecel) * Mathf.Max(0f, remaining - 1f)) * 2.237f;
                _spline.aiMaxSpeedMph = Mathf.Max(boxCrawlMph, approachMph);
                _spline.pitParkDistance = boxDist;
                if (!_spline.pitStopHold && (remaining <= 0.5f || _spline.PitProgress01 >= targetFrac))
                    _spline.pitStopHold = true;
                if (_spline.pitStopHold && ActualSpeedMps < 0.3f)
                {
                    _spline.parkedHold = true;
                    _state = State.Parked;
                    _director?.OnStintParked(this);
                }
                break;
            }
        }
    }
}
