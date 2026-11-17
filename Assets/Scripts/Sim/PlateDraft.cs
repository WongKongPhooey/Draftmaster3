using UnityEngine;

namespace Draftmaster.Sim
{
    // How much of the tow a car gets at its speed on a restrictor-plate track, from NR2003's draft shape.
    //
    // NR2003 (Docs/NR2003-Internals.md, "Drafting", FUN_00530ca0) scales draft by (v / vTop)^10: at half of top
    // speed it is 0.1% of full, at 80% 11%, at 95% 60%. The hole a car punches in the air is only worth anything
    // near top speed, so a field getting going off a rolling start at 50 mph has no draft at all and every row
    // accelerates the same - the rows stay apart. Draftmaster ran the full tow (and push) from 70 mph on plate
    // tracks, so every follower out-accelerated the front row by 3-4 m/s^2 off the green and the field ran into
    // itself on Daytona's back straight within 2-3 s of the flag (2026-11-16, ai_incidents #1-#5).
    //
    // vTop is the plate speed: at or above it (where a pack actually races) the share is 1, so pack racing keeps
    // the draft it was tuned on.
    public static class PlateDraft
    {
        public const float Exponent = 10f;

        public static float SpeedShare(float speedMps, float plateMps)
        {
            if (plateMps <= 0f) return 1f;
            if (speedMps <= 0f) return 0f;
            float r = speedMps / plateMps;
            if (r >= 1f) return 1f;
            return Mathf.Pow(r, Exponent);
        }
    }
}
