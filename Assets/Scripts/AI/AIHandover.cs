using UnityEngine;

// Handing a car between the human and the AI without a teleport, on the same dynamic model either way: the
// spline brain re-engages at the car's current pose and SplineInputDriver drives the PlayerVehicleController the
// human was just using, with AIRacingBehaviour's racecraft on top — following distance, passing, the start, the
// cars alongside — exactly as every car GridSpawner builds. Used by the TEAM panel's car swap and by Auto
// (DriveModeController).
public static class AIHandover
{
    // The racing AI takes the car. False when the car has no dynamic model or no brain to hand it to.
    public static bool ToAI(GameObject car, float steerGain, float speedGain)
    {
        if (car == null) return false;
        var pvc = car.GetComponent<PlayerVehicleController>();
        var spline = car.GetComponent<SplineDriver>();
        if (pvc == null || spline == null) return false;

        float mph = pvc.SpeedMph;

        pvc.enabled = false;   // drops the RaceObstacles registration while we rewire
        if (spline.track == null) spline.track = pvc.track;
        if (spline.vehicleInfo == null) spline.vehicleInfo = pvc.vehicleInfo;
        spline.spriteFacesUp = pvc.spriteFacesUp;
        spline.angleOffsetDeg = pvc.angleOffsetDeg;
        spline.enabled = true;                 // back into RaceField
        spline.EngageFromCurrentPose(mph);

        if (car.GetComponent<TireState>() == null) car.AddComponent<TireState>();
        var racing = car.GetComponent<AIRacingBehaviour>();
        if (racing == null) racing = car.AddComponent<AIRacingBehaviour>();
        racing.enabled = true;

        var input = car.GetComponent<SplineInputDriver>();
        if (input == null) input = car.AddComponent<SplineInputDriver>();
        input.steerGain = steerGain;
        input.speedGain = speedGain;
        input.enabled = true;                  // OnEnable: externalMotionController=true, externalInput=true, reseeds

        var pit = car.GetComponent<PitStopController>();
        if (pit != null) pit.enabled = true;   // wear-based strategy resumes for the AI

        pvc.externalInput = true;
        pvc.enableWheelspin = false;           // AI parity with GridSpawner-built cars
        pvc.damageImpairsHandling = false;
        pvc.enabled = true;                    // OnEnable sees an enabled SplineDriver → not an obstacle
        return true;
    }

    // The human takes the car: brains off, inputs live, obstacle registration on so the rest of the field brakes
    // for it like they do for any human. Picks up from wherever the AI left it — the dynamic model's pose, or the
    // spline's when the car was being driven kinematically (behind the safety car).
    public static void ToHuman(GameObject car)
    {
        if (car == null) return;
        var pvc = car.GetComponent<PlayerVehicleController>();
        if (pvc == null) return;
        var spline = car.GetComponent<SplineDriver>();

        var input = car.GetComponent<SplineInputDriver>();
        if (input != null) input.enabled = false;
        var racing = car.GetComponent<AIRacingBehaviour>();
        if (racing != null) racing.enabled = false;
        var pit = car.GetComponent<PitStopController>();
        if (pit != null) pit.enabled = false;  // the human decides when to pit

        float mph = pvc.SpeedMph;
        float heading = pvc.HeadingDeg;
        Vector3 pos = car.transform.position;
        if (spline != null && spline.enabled)
        {
            if (!spline.externalMotionController) // kinematic: the spline owned the transform
            {
                mph = spline.CurrentMph;
                heading = spline.CommandedHeadingDeg;
            }
            spline.enabled = false;            // out of RaceField — this car is the human now
        }

        pvc.externalInput = false;
        pvc.enableWheelspin = true;
        pvc.damageImpairsHandling = true;
        pvc.enabled = false;
        pvc.enabled = true;                    // OnEnable sees the spline disabled → registers as an obstacle
        pvc.SeedPose(pos, heading, mph / 2.237f);
    }
}
