using System.Collections.Generic;
using Draftmaster.Weekend;
using UnityEngine;

// The sponsor's pit-stop challenge, driven.
//
// One shot. The crew chief brings the car round to the top of pit road (WeekendVenueHost hands over here once
// he has had his say), the screen counts down, and the clock starts on GO with the pit limiter off. Run the
// lane flat out, stop fully inside the marked box, sit still while the crew bolt on four fresh tyres, then
// drive out — the clock stops the moment the car is clean out of the box. Overshoot the box, or run out the
// end of pit road without stopping in it, and there is no time to give.
//
// Runs between sessions, with the circuit empty (the booking is placed where no series is on track). The
// car is borrowed from its garage through PitLaneStart and handed back afterwards; the result is scored by
// PitChallenge (core) and settled through WeekendDirector like any other booking.
public class PitChallengeRun : MonoBehaviour
{
    public static PitChallengeRun Current { get; private set; }

    const float CountdownSeconds = 3f;
    const float BoxLength = 8.5f;          // m along the lane — same box the race uses (PlayerPitBoxMarker)
    const float BoxWidth = 4.6f;           // m across it
    const float StoppedMps = 0.5f;
    const float OvershootMetres = 12f;     // past the box with no stop: the run is over
    const float StartMetres = 2f;          // how far down the pit lane the car is placed
    const float ResultSeconds = 3f;
    const string BestKeyPrefix = "pitchallenge.best.";

    enum Phase { Countdown, Running, Servicing, Leaving, Result }

    WeekendActivity _activity;
    PitLaneStart _pls;
    TrackBuilder _track;
    PlayerVehicleController _car;
    List<TrackBuilder.Sample> _pit;

    Vector3 _boxCentre;
    Vector3 _boxAlong;      // unit, world, the way the lane runs
    Vector3 _boxAcross;
    float _boxPitDistance;
    float _par;
    Vector2 _carHalf = new Vector2(1.0f, 2.4f);   // x = half-width, y = half-length (VehicleCollision)

    Phase _phase;
    float _phaseTime;
    float _clock;
    float _serviceTime;
    bool _completed;
    bool _personalBest;
    float _best;
    WeekendOutcome _outcome;
    PitCrewBox _crew;
    GameObject _boxMarker;
    bool _ready;          // the car is on pit road and the run is on
    bool _handingBack;    // the closing fade has been asked for

    // ------------------------------------------------------------------ start

    // Start the run for this booking. False when the scene has nothing to run it in — no walk-up, no car,
    // no pit lane — in which case the caller settles the booking itself.
    public static bool Launch(WeekendActivity a)
    {
        if (Current != null || a == null) return false;

        var pls = FindFirstObjectByType<PitLaneStart>();
        if (pls == null || pls.car == null || pls.track == null) return false;
        var pit = pls.PitSamples;
        if (pit == null || pit.Count < 2) return false;

        var go = new GameObject("PitChallengeRun");
        var run = go.AddComponent<PitChallengeRun>();
        run._activity = a;
        run._pls = pls;
        run._track = pls.track;
        run._car = pls.car;
        run._pit = pit;
        Current = run;

        // The car is brought round off screen: a cut to black over the teleport, and the countdown starts as
        // the picture comes back.
        ScreenFade.Cut(run.PutCarOnPitRoad);
        return true;
    }

    void PutCarOnPitRoad()
    {
        // The circuit is meant to be empty. A practice that ended in this scene leaves its field parked in
        // the boxes until the player goes home to the motorhome; one who came straight here would be running
        // the lane through it. Behind the cut, so they just aren't there.
        GridSpawner.ClearFinishedSessionField();

        float pitLength = _pit[_pit.Count - 1].distance;

        // The box: the player's own when this session has fitted one, otherwise where the car is parked at
        // the scene's open — which is the spot the player's box is reserved around once a field arrives.
        _boxPitDistance = PitLane.Configured && PitLane.PlayerBox >= 0
            ? PitLane.BoxDistance(PitLane.PlayerBox, pitLength)
            : _pls.PlayerPitDistance;
        _boxPitDistance = Mathf.Clamp(_boxPitDistance, pitLength * 0.3f, pitLength - BoxLength);

        float lateral = _track.HasPitBoxLane ? _track.PitBoxLaneCenterLateral
                      : PitLane.Configured ? PitLane.ParkLateral : 0f;
        var box = _track.SamplePitAt(_boxPitDistance, _pit);
        Vector2 boxLocal = box.position + box.normal * lateral;
        _boxCentre = _track.transform.TransformPoint(new Vector3(boxLocal.x, boxLocal.y, 0f));
        _boxAlong = _track.transform.TransformDirection(new Vector3(box.tangent.x, box.tangent.y, 0f));
        _boxAlong.z = 0f;
        _boxAlong.Normalize();
        _boxAcross = new Vector3(-_boxAlong.y, _boxAlong.x, 0f);

        var start = _track.SamplePitAt(StartMetres, _pit);
        Vector3 startWorld = _track.transform.TransformPoint(new Vector3(start.position.x, start.position.y, 0f));
        Vector3 startDir = _track.transform.TransformDirection(new Vector3(start.tangent.x, start.tangent.y, 0f));
        startWorld.z = _car.transform.position.z;
        float heading = Mathf.Atan2(startDir.y, startDir.x) * Mathf.Rad2Deg;

        if (!_pls.BeginChallengeDrive(startWorld, heading)) { Abort(); return; }

        var vc = _car.GetComponent<VehicleCollision>();
        if (vc != null) _carHalf = vc.halfExtents;

        _par = PitChallenge.Par(_boxPitDistance - StartMetres, BoxLength * 0.5f + _carHalf.y);
        _best = PlayerPrefs.GetFloat(BestKey, -1f);
        BuildBoxMarker();
        SetPhase(Phase.Countdown);
        _ready = true;
    }

    string BestKey => BestKeyPrefix + (_track != null && _track.track != null ? _track.track.name : "track");

    // ------------------------------------------------------------------ the run

    void Update()
    {
        if (_car == null || _pls == null) { Abort(); return; }
        if (!_ready || _handingBack) return;   // behind the opening cut, or the closing one

        _phaseTime += Time.deltaTime;
        if (_phase == Phase.Running || _phase == Phase.Servicing || _phase == Phase.Leaving)
            _clock += Time.deltaTime;

        switch (_phase)
        {
            case Phase.Countdown:
                if (_phaseTime >= CountdownSeconds)
                {
                    _pls.SetChallengeControls(true);
                    SetPhase(Phase.Running);
                }
                break;

            case Phase.Running:
                if (FullyInsideBox() && _car.SpeedMps < StoppedMps)
                {
                    // Held still for the crew: the controls go off so a twitch on the throttle can't roll it
                    // out from under the wheel guns. They come back on with the last tyre.
                    _pls.SetChallengeControls(false);
                    _crew = PitCrewRegistry.Nearest(_boxCentre);
                    if (_crew != null && (_crew.transform.position - _boxCentre).sqrMagnitude > 12f * 12f) _crew = null;
                    _crew?.BeginService(_car.transform);
                    SetPhase(Phase.Servicing);
                }
                else if (Overshot() || _clock > PitChallenge.TimeLimitSeconds)
                {
                    Settle(false);
                }
                break;

            case Phase.Servicing:
                _serviceTime += Time.deltaTime;
                if (_serviceTime >= PitChallenge.ServiceSeconds)
                {
                    _car.PitResetTyres();
                    _crew?.EndService();
                    _crew = null;
                    _pls.SetChallengeControls(true);
                    SetPhase(Phase.Leaving);
                }
                break;

            case Phase.Leaving:
                if (FullyOutOfBox()) Settle(true);
                else if (_clock > PitChallenge.TimeLimitSeconds) Settle(false);
                break;

            case Phase.Result:
                if (_phaseTime >= ResultSeconds && !ScreenFade.Busy)
                {
                    _handingBack = true;
                    ScreenFade.Cut(HandBack);
                }
                break;
        }
    }

    void SetPhase(Phase p)
    {
        _phase = p;
        _phaseTime = 0f;
    }

    void Settle(bool completed)
    {
        _completed = completed;
        _pls.SetChallengeControls(false);
        _crew?.EndService();
        _crew = null;

        if (completed)
        {
            _personalBest = _best < 0f || _clock < _best;
            if (_personalBest)
            {
                PlayerPrefs.SetFloat(BestKey, _clock);
                PlayerPrefs.Save();
            }
        }
        _outcome = PitChallenge.Outcome(_clock, _par, completed, _personalBest && _best >= 0f);
        SetPhase(Phase.Result);
    }

    // Out of the car and back on foot at the team's garage, and the booking settled.
    void HandBack()
    {
        Vector3 standAt = _pls.BesideCarPosition();
        var garage = WeekendVenueAnchor.Find(WeekendVenue.PitBox);
        if (garage != null) standAt = garage.transform.position;

        _pls.EndChallengeDrive(standAt);
        WeekendDirector.Finish(_activity, _outcome, inWorld: true);
        Cleanup();
    }

    // Nothing to run it in, or the car went away under it: the driver is handed back if they were in it, and
    // the booking is left as it was — come back to it, or miss it like anything else.
    void Abort()
    {
        if (_pls != null && _pls.ChallengeDriving) _pls.EndChallengeDrive(_pls.BesideCarPosition());
        _crew?.EndService();
        Cleanup();
    }

    void Cleanup()
    {
        if (_boxMarker != null) Destroy(_boxMarker);
        if (Current == this) Current = null;
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (_boxMarker != null) Destroy(_boxMarker);
        if (Current == this) Current = null;
    }

    // ------------------------------------------------------------------ the box

    // The car's four corners in the box's own frame: x along the lane, y across it.
    void Corners(Vector2[] into)
    {
        float h = _car.HeadingDeg * Mathf.Deg2Rad;
        Vector3 fwd = new Vector3(Mathf.Cos(h), Mathf.Sin(h), 0f) * _carHalf.y;
        Vector3 side = new Vector3(-Mathf.Sin(h), Mathf.Cos(h), 0f) * _carHalf.x;
        Vector3 c = _car.transform.position - _boxCentre;
        c.z = 0f;
        int i = 0;
        for (int f = -1; f <= 1; f += 2)
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 p = c + fwd * f + side * s;
            into[i++] = new Vector2(Vector3.Dot(p, _boxAlong), Vector3.Dot(p, _boxAcross));
        }
    }

    readonly Vector2[] _corners = new Vector2[4];

    bool FullyInsideBox()
    {
        Corners(_corners);
        foreach (var p in _corners)
            if (Mathf.Abs(p.x) > BoxLength * 0.5f || Mathf.Abs(p.y) > BoxWidth * 0.5f) return false;
        return true;
    }

    // Clean out: every corner past the same edge of the box.
    bool FullyOutOfBox()
    {
        Corners(_corners);
        bool ahead = true, behind = true, left = true, right = true;
        foreach (var p in _corners)
        {
            ahead &= p.x > BoxLength * 0.5f;
            behind &= p.x < -BoxLength * 0.5f;
            left &= p.y > BoxWidth * 0.5f;
            right &= p.y < -BoxWidth * 0.5f;
        }
        return ahead || behind || left || right;
    }

    bool Overshot()
    {
        float d = _track.NearestPitDistance(_car.transform.position);
        float pitLength = _pit[_pit.Count - 1].distance;
        return d > _boxPitDistance + OvershootMetres || d >= pitLength - 1f;
    }

    // A glowing outline on the box, so there is no doubt where the car has to stop.
    void BuildBoxMarker()
    {
        _boxMarker = new GameObject("PitChallengeBox");
        var line = _boxMarker.AddComponent<LineRenderer>();
        line.sharedMaterial = PaddockPerson.Unlit;
        line.loop = true;
        line.useWorldSpace = true;
        line.widthMultiplier = 0.2f;
        line.positionCount = 4;
        var colour = new Color(0.35f, 1f, 0.55f);
        line.startColor = colour;
        line.endColor = colour;
        line.sortingOrder = 3;

        float z = -0.05f;
        Vector3 a = _boxAlong * (BoxLength * 0.5f), w = _boxAcross * (BoxWidth * 0.5f);
        Vector3 c = new Vector3(_boxCentre.x, _boxCentre.y, z);
        line.SetPosition(0, c - a - w);
        line.SetPosition(1, c + a - w);
        line.SetPosition(2, c + a + w);
        line.SetPosition(3, c - a + w);
    }

    // ------------------------------------------------------------------ on screen

    void OnGUI()
    {
        if (!_ready) return;

        string big, small = "";
        Color colour = PixelGUI.Gold;
        switch (_phase)
        {
            case Phase.Countdown:
                int n = Mathf.CeilToInt(CountdownSeconds - _phaseTime);
                big = n > 0 ? n.ToString() : "GO!";
                small = "PIT-STOP CHALLENGE  -  LIMITER OFF";
                break;
            case Phase.Running:
                big = _phaseTime < 0.8f ? "GO!" : $"{_clock:0.00}s";
                colour = _phaseTime < 0.8f ? PixelGUI.Confirm : PixelGUI.Gold;
                small = "STOP FULLY INSIDE THE BOX";
                break;
            case Phase.Servicing:
                big = $"{_clock:0.00}s";
                small = $"FOUR TYRES...  {Mathf.Max(0f, PitChallenge.ServiceSeconds - _serviceTime):0.0}";
                break;
            case Phase.Leaving:
                big = $"{_clock:0.00}s";
                colour = PixelGUI.Confirm;
                small = "GO GO GO - CLEAR THE BOX";
                break;
            default:
                var grade = PitChallenge.GradeFor(_clock, _par, _completed);
                big = _completed ? $"{_clock:0.00}s" : "NO TIME";
                colour = _completed ? PixelGUI.Confirm : PixelGUI.Danger;
                small = PitChallenge.Label(grade)
                        + (_completed ? $"   PAR {_par:0.0}s" : "   MISSED THE BOX")
                        + (_completed && _personalBest ? "   NEW BEST" : _best > 0f ? $"   BEST {_best:0.00}s" : "");
                break;
        }

        float w = PixelGUI.Px(300f), h = PixelGUI.Px(46f);
        float x = Mathf.Round((Screen.width - w) * 0.5f);
        float y = Mathf.Round(Screen.height * 0.16f);
        PixelGUI.Panel(new Rect(x, y, w, h));

        var style = PixelGUI.Heading;
        var prevAlign = style.alignment;
        var prevColour = style.normal.textColor;
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = colour;
        GUI.Label(new Rect(x, y, w, h * 0.6f), big, style);
        style.alignment = prevAlign;
        style.normal.textColor = prevColour;

        var sub = PixelGUI.Label;
        var subAlign = sub.alignment;
        sub.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(x, y + h * 0.55f, w, h * 0.4f), small, sub);
        sub.alignment = subAlign;
    }
}
