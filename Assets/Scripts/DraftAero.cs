using UnityEngine;

// Aerodynamic draft field: who is towing whom, and who is being side-drafted.
//
// Two effects, both computed in TRACK space (distance along the centerline + signed lateral) so they work the
// same for AI splines and the free-driven player:
//  - SLIPSTREAM (tow): tucked in behind a car within draftingMaxGap and laterally aligned — the leader punches
//    the hole in the air. Factor 0..1 with linear falloff over gap and misalignment. Consumed by
//    PlayerVehicleController (real drag reduction: extra accel + raised top-speed ceiling) and by
//    AIRacingBehaviour (target-speed boost, so the brain actually ASKS for the extra speed the physics allows).
//  - SIDE DRAFT: a rival's nose alongside our rear quarter dumps turbulent air on the spoiler — drag and
//    top-speed loss for US, the car ahead in the overlap. The classic NASCAR tool for stalling a pass. Factor
//    0..1 summed over attackers. Consumed by PlayerVehicleController only; kinematic (non-dynamic-model) cars
//    have no drag model to penalise.
//
// Geometry constants live here so the player's physics and the AI's targeting agree on what "in the draft" means.
// Per-vehicle strength/gates (draftingMaxGap, draftingMinSpeed, bonuses) come from VehicleInfo.
public static class DraftAero
{
    public const float TowLateralHalfWidth = 2.5f; // |lateral delta| (m) within which a follower catches the tow
    public const float SideLateralMin = 1.1f;      // closer than this is door-banging contact, not aero
    public const float SideLateralMax = 3.2f;      // beyond this the wake misses the spoiler
    public const float SideOverlapLength = 6.5f;   // centre-to-centre longitudinal window (m) for a nose-on-quarter overlap
    public const float SpeedRampMph = 15f;         // effects fade in over this band below draftingMinSpeed (no hard pop)

    // Total main-spline length for a track, read from any registered AI driving it — the player's own controller
    // has no notion of lap length. 0 when no AI shares the track, and then there is nobody to draft anyway.
    public static float TrackLengthFor(TrackBuilder track)
    {
        if (track == null) return 0f;
        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var d = drivers[i];
            if (d != null && d.track == track && d.TrackLength > 0f) return d.TrackLength;
        }
        return 0f;
    }

    // Evaluate the draft field at one car's track pose. tow = strongest slipstream caught (0..1) and the car
    // providing it; sideDraft = accumulated side-draft suffered (0..1) from cars with a nose on our quarter.
    public static void Compute(TrackBuilder track, float trackLen, float myDist, float myLat, float mySpeedMph,
                               GameObject self, VehicleInfo vi,
                               out float tow, out GameObject towSource, out float sideDraft)
    {
        tow = 0f; sideDraft = 0f; towSource = null;
        if (track == null || trackLen <= 0f || vi == null) return;

        // Aero only matters at speed: ramp in just under draftingMinSpeed so the effect doesn't pop on.
        float speed01 = Mathf.Clamp01((mySpeedMph - (vi.draftingMinSpeed - SpeedRampMph)) / SpeedRampMph);
        if (speed01 <= 0f) return;

        var drivers = RaceField.Drivers;
        for (int i = 0; i < drivers.Count; i++)
        {
            var d = drivers[i];
            if (d == null || !d.isActiveAndEnabled || d.gameObject == self || d.IsOnPit) continue;
            if (d.track != track || d.TrackLength <= 0f) continue;
            Accumulate(trackLen, myDist, myLat, d.DistanceOnTrack, d.LateralOnTrack, vi, d.gameObject,
                       ref tow, ref towSource, ref sideDraft);
        }

        // Free-driven human car(s) — absent from RaceField, projected onto the centerline by their controller.
        var obstacles = RaceObstacles.All;
        for (int i = 0; i < obstacles.Count; i++)
        {
            var p = obstacles[i];
            if (p == null || p.gameObject == self || p.ObstacleTrack != track) continue;
            Accumulate(trackLen, myDist, myLat, p.TrackDistance, p.TrackLateral, vi, p.gameObject,
                       ref tow, ref towSource, ref sideDraft);
        }

        tow *= speed01;
        sideDraft = Mathf.Clamp01(sideDraft) * speed01;
    }

    static void Accumulate(float trackLen, float myDist, float myLat, float otherDist, float otherLat,
                           VehicleInfo vi, GameObject other,
                           ref float tow, ref GameObject towSource, ref float sideDraft)
    {
        float g = otherDist - myDist; // + = other car ahead of us
        if (g > trackLen * 0.5f) g -= trackLen;
        else if (g < -trackLen * 0.5f) g += trackLen;
        float latDelta = Mathf.Abs(otherLat - myLat);

        // Slipstream: behind them, aligned. Strongest bumper-to-bumper on their line.
        if (g > 0f && g <= vi.draftingMaxGap && latDelta <= TowLateralHalfWidth)
        {
            float f = (1f - g / Mathf.Max(vi.draftingMaxGap, 0.1f)) * (1f - latDelta / TowLateralHalfWidth);
            if (f > tow) { tow = f; towSource = other; }
        }

        // Side draft ON US: they sit behind us (g < 0) inside the overlap window, one lane over — their nose is
        // beside our rear quarter, stealing air off our spoiler. Peaks mid-overlap (nose at our rear wheel),
        // fades to zero both fully alongside and barely overlapped.
        if (g < 0f && g >= -SideOverlapLength && latDelta > SideLateralMin && latDelta <= SideLateralMax)
        {
            float half = SideOverlapLength * 0.5f;
            float longFrac = 1f - Mathf.Abs(-g - half) / half;
            float latFrac = 1f - (latDelta - SideLateralMin) / (SideLateralMax - SideLateralMin);
            sideDraft += Mathf.Clamp01(longFrac) * Mathf.Clamp01(latFrac);
        }
    }
}
