using UnityEngine;

namespace Draftmaster.Sim
{
    // Dirty air: the wake of a car ahead costs the car behind downforce, and so grip, as well as drag.
    //
    // From NR2003 (Docs/NR2003-Internals.md, "Dirty air"): one aero-interaction pass (FUN_005afd80) walks the cars
    // ahead, reads a wake table by gap and lateral offset, and scales the follower's drag AND its spoiler term in
    // the lift sum by the same wake. The AI car's traction circle is its normal load (weight + downforce) times
    // the tyre's friction, so the tow down the straight and the lost grip in the corner come from one wake, and
    // both grow with dynamic pressure (v²). That is what makes a 1.5-mile oval race the way it does: a quicker car
    // catches the car ahead on the straight, can't drive round the back of it in the corner, and has to find clean
    // air in another lane to pass.
    //
    // PlayerVehicleController applies it to every car's tyres (player and AI alike, as NR2003 does) and
    // SplineInputDriver's grip governor plans the AI's corner speed with it, so the AI lifts for what it loses.
    public static class DirtyAir
    {
        // Speed (m/s, ~168 mph) at which the wake costs its full share; it scales with v² below that, as
        // downforce does, so a short track's 60 mph corners lose next to nothing.
        public const float ReferenceMps = 75f;

        // Grip multiplier (<= 1) for a car in a tow of strength tow (0..1, DraftAero) at speedMps, where a full
        // tow at the reference speed costs maxLoss of the grip.
        public static float GripFactor(float tow, float speedMps, float maxLoss)
        {
            if (maxLoss <= 0f || tow <= 0f || speedMps <= 0f) return 1f;
            float q = speedMps / ReferenceMps;
            return 1f - Mathf.Clamp01(maxLoss) * Mathf.Clamp01(tow) * Mathf.Clamp01(q * q);
        }
    }
}
