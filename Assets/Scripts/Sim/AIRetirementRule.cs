using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Sim
{
    // When an AI car's race is over, and where it is parked once it is — lifted out of AIRetirement so it can be
    // pinned in EditMode.
    //
    // Two ways out. A car bent past `breakdownDamage` stops working: the engine is gone, it rolls to a stop, and
    // after a short wait (`brokenWait`) it is retired. A car that is not broken but has sat stopped on the circuit
    // for `stuckWait` — beached, wedged against a wall, pointing the wrong way with nobody able to get it going —
    // is retired too. Either way it is then lifted off the circuit to the far side of the infield wall, out of the
    // race, and stops raising yellows.
    public static class AIRetirementRule
    {
        // Whether bodywork this damaged (VehicleDamage.DamageLevel, 0..1) has stopped the car working.
        // A threshold of 0 or less disables breakdowns.
        public static bool BrokenDown(float damage, float breakdownDamage) =>
            breakdownDamage > 0f && damage >= breakdownDamage;

        // Whether a car stopped for `stoppedFor` seconds (negative = moving) is retired now. A broken-down car goes
        // after `brokenWait`; one that could still drive only after the longer `stuckWait`.
        public static bool Retire(float stoppedFor, bool brokenDown, float brokenWait, float stuckWait)
        {
            if (stoppedFor < 0f) return false;
            return stoppedFor >= (brokenDown ? brokenWait : stuckWait);
        }

        // The order spots along the track are tried in, as metres from where the car stopped: right beside it
        // first, then alternately further ahead and behind in `step` metre steps out to `max`.
        public static IEnumerable<float> AlongOffsets(float step, float max)
        {
            yield return 0f;
            if (step <= 0f) yield break;
            for (float d = step; d <= max + 1e-3f; d += step)
            {
                yield return d;
                yield return -d;
            }
        }

        // Whether `point` keeps at least `margin` metres from the edge of the road everywhere — measured against
        // every centreline sample, not only the nearest stretch, so a spot in a road course's infield is not
        // sat on another part of the same circuit. `centres` and `halfWidths` are parallel.
        public static bool ClearOfRoad(Vector2 point, IReadOnlyList<Vector2> centres, IReadOnlyList<float> halfWidths,
                                       float margin)
        {
            if (centres == null) return true;
            for (int i = 0; i < centres.Count; i++)
            {
                float half = halfWidths != null && i < halfWidths.Count ? halfWidths[i] : 0f;
                float clear = half + margin;
                if ((point - centres[i]).sqrMagnitude < clear * clear) return false;
            }
            return true;
        }

        // Whether `point` is at least `minGap` metres from every spot already taken by another retired car.
        public static bool ClearOfOthers(Vector2 point, IReadOnlyList<Vector2> taken, float minGap)
        {
            if (taken == null) return true;
            for (int i = 0; i < taken.Count; i++)
                if ((point - taken[i]).sqrMagnitude < minGap * minGap) return false;
            return true;
        }

        // Whether a circle of `radius` metres at `point` is wholly outside a camera frame centred on `viewCentre`,
        // `halfHeight` metres from centre to top edge and `aspect` (width over height) wide, with `margin` metres to
        // spare. Tested against the circle through the frame's corners, so it holds whichever way the view is rolled
        // (the swing camera turns the picture with the car).
        public static bool OutOfShot(Vector2 point, float radius, Vector2 viewCentre, float halfHeight, float aspect,
                                     float margin)
        {
            float h = Mathf.Max(0f, halfHeight);
            float w = h * Mathf.Max(0.01f, aspect);
            float reach = Mathf.Sqrt(h * h + w * w) + Mathf.Max(0f, radius) + Mathf.Max(0f, margin);
            return (point - viewCentre).sqrMagnitude > reach * reach;
        }

        // Whether a car waiting out of sight to be moved to its parking spot should be moved now: as soon as
        // nobody can see it go or arrive, or — so a car stopped in a fixed view (a grandstand seat, a camera
        // following it) cannot hold a caution out for ever — once it has waited `forceAfter` seconds
        // (0 or less = wait for ever).
        public static bool MoveOutOfSight(bool carInShot, bool spotInShot, float waited, float forceAfter)
        {
            if (!carInShot && !spotInShot) return true;
            return forceAfter > 0f && waited >= forceAfter;
        }

        // Index of the centreline sample nearest `point`; -1 for an empty list.
        public static int NearestIndex(Vector2 point, IReadOnlyList<Vector2> centres)
        {
            int best = -1;
            float bestSq = float.MaxValue;
            if (centres == null) return best;
            for (int i = 0; i < centres.Count; i++)
            {
                float sq = (point - centres[i]).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = i; }
            }
            return best;
        }
    }
}
