using UnityEngine;

namespace Draftmaster.Sim
{
    // The sideways move a car makes between the pit lane's driving line and its box on the wall-side strip, on the
    // way in to a stop and on the way out of one: the same S-curve over distance rolled as the formation pull-out
    // (FormationLanes.PullOutLateral), so the body's path angle is the same at any speed, starts and ends at zero and
    // never jumps when the car stops or starts. Easing the lateral at a fixed rate in TIME - how pit stops did it -
    // swung the car through 50 degrees and more at a crawl out of the box and into it, and squared it up with a snap.
    //
    // The curve is as gentle as the boxes allow and no gentler: a car pulling out has to have moved `clearance` across
    // by the time its nose reaches the tail of the car parked in the next box down the lane, and a car coming in has
    // to still be that far off the strip while it is alongside the car parked in the box before its own.
    public static class PitBoxPath
    {
        // Inverse of the smoothstep 3t^2 - 2t^3 on [0, 1]: the share of the curve run when share `s` of the move is made.
        public static float InverseSmoothStep(float s)
        {
            s = Mathf.Clamp01(s);
            return 0.5f - Mathf.Sin(Mathf.Asin(1f - 2f * s) / 3f);
        }

        // Length (m) of the curve for a lateral move of `delta` m, with boxes `spacing` m apart, cars `carLength` long
        // and `clearance` m (a car's width and a margin) to have made before the neighbouring car is reached.
        // `endMargin` is how far short of the box the curve has to be finished (coming in) - 0 going out. Never
        // longer than the curve that peaks at `gentleAngleDeg` (no point being gentler than that), nor than
        // `maxLength` when that is positive (the lane left before the pit exit), and never shorter than `minLength`.
        // A ladder packed tighter than a car length has no gap to fit to, so the curve is the shortest allowed.
        public static float Length(float delta, float spacing, float carLength, float clearance, float gentleAngleDeg,
                                   float minLength, float maxLength = 0f, float endMargin = 0f)
        {
            float d = Mathf.Abs(delta);
            float floor = Mathf.Max(0.5f, minLength);
            if (d < 1e-3f) return floor;

            float len = FormationLanes.PullOutLength(d, gentleAngleDeg, 0f, 0f);
            float room = spacing - carLength - Mathf.Max(0f, endMargin);
            if (room > 0f)
            {
                float t = InverseSmoothStep(clearance / d);
                if (t > 1e-4f) len = Mathf.Min(len, room / t);
            }
            else len = floor;
            if (maxLength > 0f) len = Mathf.Min(len, maxLength);
            return Mathf.Max(len, floor);
        }

        // Lateral after rolling `travelled` m of a `length` m curve from `from` to `to`.
        public static float Lateral(float from, float to, float travelled, float length)
            => to + FormationLanes.PullOutLateral(from - to, travelled, length);

        // Steepest path angle (deg) on a curve of `length` m for a move of `delta` m - at its midpoint.
        public static float PeakAngleDeg(float delta, float length)
            => length > 0f ? Mathf.Atan(1.5f * Mathf.Abs(delta) / length) * Mathf.Rad2Deg : 90f;
    }
}
