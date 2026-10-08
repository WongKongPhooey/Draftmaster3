using UnityEngine;

// The superspeedway setup: a restrictor plate, and turns that can take it flat out.
//
// Daytona and Talladega are flat out all the way round - the plate holds the cars to a speed the banking can
// carry, so nobody lifts for the turns and the whole race is the draft. In the game the cars' engines would
// out-run the turns (and the AI's speed plan lifted for every one), so a plated track gets two things:
//   * a plate: every car's solo top speed (PlayerVehicleController clamps to it; the tow and the push add to it);
//   * the grip the turns need for that: TrackConditions.TrackGripScale is raised until the tightest turn, in the
//     lowest lane, holds the plate speed with a full tow and a full push on top, plus a margin - using the same
//     banked-grip and usable-grip maths (BankedGrip, AIGrip) the physics and the AI's planning use. The player's
//     grip is the reference, so a human car can run flat out too; the AI's own grip bonus is extra margin.
// Both come from the track's geometry asset and the Cup car, so nothing is hand-tuned per track but the plate.
public static class RestrictorPlate
{
    // What a line of cars behind is worth to the car at its head, at PushScale 1 (DraftAero.Push).
    public const float PushTopSpeedGain = 0.03f;
    public const float PushAccel = 1.2f;   // m/s² extra under throttle

    // Corner speed asked of the turns over and above plate x (1 + tow + push).
    public const float CornerMargin = 1.04f;

    // How far inside the authored turn radius the lowest lane runs (m).
    public const float LaneInset = 5f;

    public static bool Solve(string trackId, float plateMph, out float plate, out float gripScale)
    {
        plate = plateMph;
        gripScale = 1f;
        var info = Resources.Load<TrackInfoV2>("Tracks/" + trackId);
        var vi = Resources.Load<VehicleInfo>("Vehicles/Cup24");
        if (info == null || info.segments == null || vi == null || vi.maxLateralG <= 0.01f) return false;
        plate = Mathf.Min(plateMph, vi.topSpeed);
        float flatPeak = vi.maxLateralG * TrackConditions.BaseGrip * TrackConditions.GripMultiplier * Draftmaster.Sim.BankedGrip.G;
        float draftGain = vi.draftingTopSpeedGain * TrackConditions.DraftScale + PushTopSpeedGain * TrackConditions.PushScale;
        float need = plate / 2.237f * (1f + draftGain) * CornerMargin;
        gripScale = GripScaleFor(info.segments, flatPeak, need);
        return true;
    }

    // The least multiplier on flatPeak at which every turn holds cornerMps.
    public static float GripScaleFor(TrackInfoV2.TrackSegment[] segments, float flatPeak, float cornerMps)
    {
        float scale = 1f;
        foreach (var seg in segments)
        {
            if (seg.type != TrackInfoV2.SegmentType.Turn || Mathf.Abs(seg.angle) < 0.5f) continue;
            float radius = seg.length / (Mathf.Abs(seg.angle) * Mathf.Deg2Rad) - LaneInset;
            if (radius < 20f) continue;
            if (CornerSpeed(radius, flatPeak * scale, seg.banking) >= cornerMps) continue;
            float lo = scale, hi = scale;
            while (CornerSpeed(radius, flatPeak * hi, seg.banking) < cornerMps && hi < 8f) hi *= 1.25f;
            for (int i = 0; i < 24; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (CornerSpeed(radius, flatPeak * mid, seg.banking) >= cornerMps) hi = mid; else lo = mid;
            }
            scale = hi;
        }
        return scale;
    }

    static float CornerSpeed(float radius, float flatPeak, float bankDeg)
        => AIGrip.CornerSpeed(radius, Draftmaster.Sim.BankedGrip.Capacity(flatPeak, bankDeg));
}
