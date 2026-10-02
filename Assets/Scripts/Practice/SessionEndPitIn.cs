using UnityEngine;
using Draftmaster.Sim;

// The end of a practice or qualifying session, for the player's car. Added by PracticeDirector the moment
// the session clock runs out.
//
// A driver still out on track finishes the lap they are on — in qualifying it still counts — and at the line
// the AI takes the car and drives the in-lap down pit road into the player's own box. Or the driver comes in
// themselves. Either way the car stops in its box, the driver climbs out (PitLaneStart does that on its own
// once PracticeDirector.SessionOver is set), and only then does the session end — PracticeDirector.StartRace,
// the same step the pause menu's END SESSION row takes. A driver already on foot when the clock runs out has
// nothing to bring in, and the session ends at once. The rules live in SessionWrapUp.
//
// The hand-off is the kinematic one the broadcast cut (DriveModeController) uses — the car's own SplineDriver
// switched on, the controller off — steered home by a PracticeAIStint, the brain the AI field already parks
// in its boxes with, pointed at the player's box.
public class SessionEndPitIn : MonoBehaviour
{
    [Tooltip("Longest (s) the AI may take to get the car into its box before the session is ended anyway.")]
    public float aiDriveTimeout = 240f;

    PracticeDirector _director;
    PitLaneStart _start;
    PlayerVehicleController _car;
    SplineDriver _spline;
    PracticeAIStint _stint;
    bool _addedStint;

    int _lapAtFlag;
    float _lapDoneAt = -1f;
    float _stoppedSince = -1f;
    float _aiSince;
    bool _closed;

    // What the spline brain was set to before the AI borrowed it, restored once the car is parked.
    float _savedMaxMph;
    bool _savedAiRacing;
    AIRacingBehaviour _aiRacing;

    public bool AIDriving { get; private set; }

    // Where the player's car is in the wrap-up, for the session clock's line.
    public bool OnPitRoad => _car != null && _car.enabled && IsOnPitSurface();

    public static SessionEndPitIn Begin(PracticeDirector director)
    {
        var w = director.GetComponent<SessionEndPitIn>();
        if (w == null) w = director.gameObject.AddComponent<SessionEndPitIn>();
        w._director = director;
        w.Init();
        return w;
    }

    // The session has been ended some other way (the pause menu): the car is still brought home and the driver
    // put out, but the session is not ended a second time.
    public void MarkClosed()
    {
        _closed = true;
        if (!AIDriving) enabled = false;
    }

    void Init()
    {
        _start = FindFirstObjectByType<PitLaneStart>();
        _car = _start != null ? _start.car : null;
        if (_car == null)
        {
            var dm = DriveModeController.Current;
            if (dm != null && dm.PlayerCar != null) _car = dm.PlayerCar.GetComponent<PlayerVehicleController>();
        }
        _spline = _car != null ? _car.GetComponent<SplineDriver>() : null;

        var tracker = RacePositionTracker.Instance;
        _lapAtFlag = tracker != null && _car != null ? tracker.LapOf(_car.transform) : 0;

        // Watching from the broadcast cut: the AI already has the car, so it is taken straight home rather than
        // left to finish a lap the player isn't driving. The camera comes back to it on the way.
        var drive = DriveModeController.Current;
        if (drive != null && !drive.IsDriving && InCar())
        {
            drive.SetDriving(true);
            if (CanTakeOver()) TakeOver();
        }
    }

    void Update()
    {
        if (AIDriving) { StepAI(); return; }
        // Over and done with: a driver who gets back in the car keeps it — the AI does not take it off them again.
        if (_closed) { enabled = false; return; }

        bool inCar = InCar();
        bool onPit = inCar && _car.enabled && IsOnPitSurface();

        if (onPit && _car.SpeedMph < 0.5f)
        {
            if (_stoppedSince < 0f) _stoppedSince = Time.time;
        }
        else _stoppedSince = -1f;

        // The line crossing is held a moment before it counts, so the lap timer has scored the lap first.
        var tracker = RacePositionTracker.Instance;
        bool crossed = tracker == null || _car == null
                    || SessionWrapUp.LapFinished(_lapAtFlag, tracker.LapOf(_car.transform));
        if (crossed && _lapDoneAt < 0f) _lapDoneAt = Time.time;
        bool lapFinished = crossed && Time.time - _lapDoneAt >= SessionWrapUp.HandOverDelaySeconds;

        float stopped = _stoppedSince < 0f ? 0f : Time.time - _stoppedSince;
        switch (SessionWrapUp.Next(inCar, lapFinished, onPit, stopped, CanTakeOver()))
        {
            case SessionWrapUp.Step.TakeOver: TakeOver(); break;
            case SessionWrapUp.Step.CloseSession: Close(); break;
        }
    }

    // Sat in the car with the controls. Without the on-foot flow there is nobody to climb out, so the car
    // itself is the driver.
    bool InCar()
    {
        if (_car == null) return false;
        if (_start != null && GameSession.OnFootAllowed) return _start.IsDriving;
        return true;
    }

    bool IsOnPitSurface()
    {
        var track = _car.track != null ? _car.track : (_start != null ? _start.track : null);
        return track != null && track.IsOnPitSurface(_car.transform.position);
    }

    // There is a brain on the car to hand it to, and a box for it to park in.
    bool CanTakeOver() =>
        _car != null && _spline != null && PitLane.Configured && PitLane.PlayerBox >= 0
        && (_start == null || _start.BoxKnown);

    void TakeOver()
    {
        // Match the car's sprite/heading and track refs so the AI drives it correctly — as the broadcast cut does.
        if (_spline.track == null) _spline.track = _car.track;
        if (_spline.vehicleInfo == null) _spline.vehicleInfo = _car.vehicleInfo;
        _spline.spriteFacesUp = _car.spriteFacesUp;
        _spline.angleOffsetDeg = _car.angleOffsetDeg;
        _savedMaxMph = _spline.aiMaxSpeedMph;

        float mph = _car.SpeedMph;
        _spline.enabled = true;
        _spline.EngageFromCurrentPose(mph);
        _car.enabled = false;

        _aiRacing = _car.GetComponent<AIRacingBehaviour>();
        _savedAiRacing = _aiRacing != null && _aiRacing.enabled;

        _stint = _car.GetComponent<PracticeAIStint>();
        _addedStint = _stint == null;
        if (_addedStint) _stint = _car.gameObject.AddComponent<PracticeAIStint>();
        _stint.boxOverride = PitLane.PlayerBox;
        _stint.DriveIn();

        // The lap the flag fell on has been scored; what the AI drives now is an in-lap, not a lap.
        if (LapTimingManager.Instance != null) LapTimingManager.Instance.AbandonLap(_car.transform);

        AIDriving = true;
        _aiSince = Time.time;
    }

    void StepAI()
    {
        // Somebody handed the car back to the player mid in-lap (the crew chief's headset): it is theirs to
        // bring in again.
        if (_car == null || _spline == null || !_spline.enabled)
        {
            Release();
            return;
        }

        bool parked = _stint != null && _stint.IsParked;
        if (!parked && Time.time - _aiSince < aiDriveTimeout) return;

        if (_start == null || !GameSession.OnFootAllowed || !_start.ClimbOutAtSessionEnd())
        {
            // Nobody to put out: leave the car parked where the AI stopped it.
            float heading = _spline.CommandedHeadingDeg;
            _spline.enabled = false;
            _car.SeedPose(_car.transform.position, heading);
            ParkedCarPin.Hold(_car);
        }
        Release();
        Close();
    }

    // Give the spline brain back the way it was found, and take the stint brain off the car.
    void Release()
    {
        AIDriving = false;
        if (_spline != null)
        {
            _spline.parkedHold = false;
            _spline.pitStopHold = false;
            _spline.pitParkDistance = -1f;
            _spline.aiMaxSpeedMph = _savedMaxMph;
            _spline.usePitLane = false;
            _spline.lateralOffset = 0f;
            _spline.tacticalLateralOffset = 0f;
        }
        if (_aiRacing != null) _aiRacing.enabled = _savedAiRacing;
        if (_stint != null && _addedStint) Destroy(_stint);
        _stint = null;
    }

    void Close()
    {
        if (_closed) return;
        _closed = true;
        enabled = false;
        // The host ends the session for both of them; a guest's own copy of it does not.
        if (Coop.IsGuest) return;
        if (_director != null) _director.StartRace();
    }
}
