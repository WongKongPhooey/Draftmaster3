using UnityEngine;

// The end of a race, for the player's car. Added by RaceDirector.
//
// The AI field brings itself in: each car's PitStopController sees it take the chequer, runs the cool-down lap
// and parks in its own box. The player's car is handed to the AI the moment it crosses the line — the broadcast
// cut's hand-off (DriveModeController), so it is the same car the TV button would give away — while the camera
// stays on the line (FinishLineCamera) for the rest of the field coming home. As the car comes up on the pit
// entry at the end of its cool-down lap it is steered down pit road into the player's box by a
// PitStopController pointed at PitLane.PlayerBox, as the end of a practice session (SessionEndPitIn) does.
// A driver who is already out of the car, or finishes the race on pit road, is left to it.
public class RaceEndPitIn : MonoBehaviour
{
    static RaceEndPitIn _instance;

    // The player crossed the line to finish and the AI has had their car since. It is not theirs to take back:
    // the TV button stays off, and an upright phone turns back to landscape for the finish.
    public static bool HandedOverAtLine => _instance != null && _instance._handedOverAtLine;

    [Tooltip("Share of a lap (0..1) the car runs after the chequer before it is sent in, so a pit entry just past the line doesn't cut the cool-down lap short.")]
    [Range(0f, 1f)] public float coolDownLapFrac = 0.5f;
    [Tooltip("The car is sent for the pit lane this far (m) before the pit entry, early enough to brake and line up for it.")]
    public float handOverLeadM = 400f;

    PitLaneStart _start;
    float _flagProgress = -1f;
    bool _lineChecked;
    bool _handedOverAtLine;

    // The AI has the car and is bringing it in.
    public bool AIDriving { get; private set; }

    void Awake() => _instance = this;
    void OnDestroy() { if (_instance == this) _instance = null; }

    void Start() { _start = FindFirstObjectByType<PitLaneStart>(); }

    void Update()
    {
        var rt = RacePositionTracker.Instance;
        if (rt == null || rt.TrackLength <= 0f) return;

        RacePositionTracker.Entry e = null;
        for (int i = 0; i < rt.Order.Count; i++)
            if (rt.Order[i] != null && rt.Order[i].isPlayer && rt.Order[i].tf != null) { e = rt.Order[i]; break; }
        if (e == null || !RaceDirector.TookChequer(e.tf)) return;
        if (_flagProgress < 0f) _flagProgress = e.progress;
        if (!_lineChecked)
        {
            _lineChecked = true;
            AtTheLine(e.tf);
        }

        // Already being brought in by a pit brain of its own (a team car the AI was driving).
        var pit = e.tf.GetComponent<PitStopController>();
        if (pit != null && pit.isActiveAndEnabled) { enabled = false; return; }

        // Nowhere to park it, or nothing to drive it with. A team car the player switched into keeps its own
        // pit brain (switched off while they drive) and its own box; the player's car parks in the one reserved.
        var car = e.tf.GetComponent<PlayerVehicleController>();
        var spline = e.tf.GetComponent<SplineDriver>();
        bool hasBox = PitLane.Configured && (pit != null || PitLane.PlayerBox >= 0);
        if (!hasBox || car == null || spline == null) { enabled = false; return; }

        // Out of the car (it is parked in the box), or on pit road under the driver's own steam.
        if (_start != null && GameSession.OnFootAllowed && !_start.IsDriving) return;
        var track = car.track != null ? car.track : spline.track;
        if (track == null || track.track == null) return;
        if (track.IsOnPitSurface(car.transform.position)) return;

        // The cool-down lap, then in on the run up to the pit entry.
        if (e.progress - _flagProgress < coolDownLapFrac * rt.TrackLength) return;
        float gap = track.track.PitEntryDistanceOnLap - e.trackDistance;
        if (gap < 0f) gap += rt.TrackLength;
        if (gap > handOverLeadM) return;

        TakeOver(car, spline, pit);
        enabled = false;
    }

    // Across the line with the race won or lost: the AI takes the car, and the camera stays behind on the line.
    void AtTheLine(Transform carTf)
    {
        var car = carTf.GetComponent<PlayerVehicleController>();
        var spline = carTf.GetComponent<SplineDriver>();
        if (car == null || spline == null) return;

        // Out of the car already, or crossing the line in the pit lane: theirs to bring in.
        if (_start != null && GameSession.OnFootAllowed && !_start.IsDriving) return;
        var track = car.track != null ? car.track : spline.track;
        if (track == null || track.IsOnPitSurface(carTf.position)) return;

        // Asked before the hand-off: the camera only stays on the line if it was on this car, or on the TV cut.
        var finishCam = GetComponent<FinishLineCamera>();
        if (finishCam == null) finishCam = gameObject.AddComponent<FinishLineCamera>();
        finishCam.Hold(carTf, track);

        if (PlayerVehicleController.IsHuman(car))
        {
            // Through the broadcast switch when this is its car, so the crew chief's headset and END RACE see a
            // car the AI is driving rather than one that went quiet under them. Told to leave the camera alone
            // first, or it would pull the camera straight back onto the car.
            var drive = DriveModeController.Current;
            if (drive != null && drive.PlayerCar == carTf.gameObject)
            {
                drive.suppressBroadcastCamera = true;
                drive.SetDriving(false);
            }
            else
            {
                HandToAI(car, spline);
            }
        }
        _handedOverAtLine = spline.enabled && !car.enabled;
    }

    // The kinematic hand-off, with the car's sprite/heading and track refs matched so the AI drives it correctly.
    static void HandToAI(PlayerVehicleController car, SplineDriver spline)
    {
        if (spline.enabled) return;
        if (spline.track == null) spline.track = car.track;
        if (spline.vehicleInfo == null) spline.vehicleInfo = car.vehicleInfo;
        spline.spriteFacesUp = car.spriteFacesUp;
        spline.angleOffsetDeg = car.angleOffsetDeg;

        float mph = car.SpeedMph;
        spline.enabled = true;
        spline.EngageFromCurrentPose(mph);
        car.enabled = false;
    }

    void TakeOver(PlayerVehicleController car, SplineDriver spline, PitStopController pit)
    {
        // Normally the AI has had it since the line (or the broadcast cut did); otherwise it takes it here.
        HandToAI(car, spline);

        if (pit == null)
        {
            pit = car.gameObject.AddComponent<PitStopController>();
            pit.forcedPit = false;
            pit.boxOverride = PitLane.PlayerBox;
        }
        pit.enabled = true;
        pit.ParkNow();
        AIDriving = true;
    }
}
