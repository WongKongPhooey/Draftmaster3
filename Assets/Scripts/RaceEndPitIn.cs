using UnityEngine;

// The end of a race, for the player's car. Added by RaceDirector.
//
// The AI field brings itself in: each car's PitStopController sees it take the chequer, runs the cool-down lap
// and parks in its own box. The player drives their own cool-down lap, and as the car comes up on the pit entry
// at the end of it the AI takes it and drives it down pit road into the player's box — the same kinematic
// hand-off the broadcast cut (DriveModeController) and the end of a practice session (SessionEndPitIn) use,
// steered home by a PitStopController pointed at PitLane.PlayerBox. A driver who turns into the pit lane
// themselves, or is already out of the car, is left to it.
public class RaceEndPitIn : MonoBehaviour
{
    [Tooltip("Share of a lap (0..1) the player must drive after the chequer before the AI may take the car in, so a pit entry just past the line doesn't cut the cool-down lap short.")]
    [Range(0f, 1f)] public float coolDownLapFrac = 0.5f;
    [Tooltip("The AI takes the car this far (m) before the pit entry, early enough to brake and line up for the lane.")]
    public float handOverLeadM = 400f;

    PitLaneStart _start;
    float _flagProgress = -1f;

    // The AI has the car and is bringing it in.
    public bool AIDriving { get; private set; }

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

        // The cool-down lap: driven, then handed over on the run up to the pit entry.
        if (e.progress - _flagProgress < coolDownLapFrac * rt.TrackLength) return;
        float gap = track.track.PitEntryDistanceOnLap - e.trackDistance;
        if (gap < 0f) gap += rt.TrackLength;
        if (gap > handOverLeadM) return;

        TakeOver(car, spline, pit);
        enabled = false;
    }

    void TakeOver(PlayerVehicleController car, SplineDriver spline, PitStopController pit)
    {
        // Watching from the broadcast cut the AI is already driving it; otherwise the kinematic hand-off, with
        // the car's sprite/heading and track refs matched so the AI drives it correctly.
        if (!spline.enabled)
        {
            if (spline.track == null) spline.track = car.track;
            if (spline.vehicleInfo == null) spline.vehicleInfo = car.vehicleInfo;
            spline.spriteFacesUp = car.spriteFacesUp;
            spline.angleOffsetDeg = car.angleOffsetDeg;

            float mph = car.SpeedMph;
            spline.enabled = true;
            spline.EngageFromCurrentPose(mph);
            car.enabled = false;
        }

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
