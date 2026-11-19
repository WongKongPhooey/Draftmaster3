using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Sim
{
    // Pit road as two driving lanes, side by side down the pit ribbon, beside the strip the boxes stand on.
    //
    // Lane 0 runs next to the boxes: it is the lane a car brakes down to turn into its own box. Lane 1 is the far
    // lane. A car coming in alongside another takes lane 1 rather than running into it, and moves across to lane 0
    // once there is room; a car leaving its box pulls out across lane 0 into lane 1 and runs that to the exit, clear
    // of the cars still braking down lane 0 for their boxes - or into lane 0 when lane 1 already has a car in it, and
    // waits in its box for a gap when both do (ExitLane). A pit road too narrow for two cars side by side (or with
    // no box strip, so parked cars sit in the road) is one lane, down the middle.
    public static class PitLanes
    {
        public const int BoxSide = 0;
        public const int FarSide = 1;
        // Narrowest pit road (m, kerb to kerb, not counting the box strip) that holds two lanes: two cars 2 m wide
        // with room between them and to either edge.
        public const float MinTwoLaneWidth = 7f;

        public struct Car
        {
            public float distance;   // along pit road (m)
            public float lateral;    // across it (m), the same frame as the box strip's parkLateral
            public float mph;
            public int lane;         // the lane it is in or moving to; -1 = unknown (judge by lateral)
            public float stopLine;   // where down pit road (m) it is braking to stop in its box; <= 0 = not stopping
        }

        // A car within this far (m) of me, either way, is alongside: in the way of a pull-out whatever its speed.
        public const float AlongsideM = 5f;
        // Slower than this (mph) a car behind is stopped or queuing - it waits for me, it does not run into me.
        public const float MovingMph = 2f;
        const float MphToMps = 1f / 2.237f;

        public static bool TwoLanes(float pitWidth, float parkLateral)
            => pitWidth >= MinTwoLaneWidth && Mathf.Abs(parkLateral) > 0.01f;

        // Lateral (m) of `lane` on pit road `pitWidth` wide, its boxes on the side `parkLateral` points to. Each lane
        // is centred a quarter of the width off the middle. One lane = the middle (0).
        public static float Lateral(int lane, float pitWidth, float parkLateral)
        {
            if (!TwoLanes(pitWidth, parkLateral)) return 0f;
            float side = parkLateral > 0f ? 1f : -1f;
            float q = pitWidth * 0.25f;
            return lane == FarSide ? -side * q : side * q;
        }

        // Which lane `car` is in: the lane it says, or the one nearest its lateral. -1 = neither (out on the box
        // strip, parked or pulling in).
        public static int LaneOf(in Car car, float pitWidth, float parkLateral)
        {
            if (car.lane >= 0) return car.lane;
            if (!TwoLanes(pitWidth, parkLateral)) return BoxSide;
            float l0 = Lateral(BoxSide, pitWidth, parkLateral), l1 = Lateral(FarSide, pitWidth, parkLateral);
            float half = Mathf.Abs(l0 - l1) * 0.5f;
            if (Mathf.Abs(car.lateral - l0) <= half) return BoxSide;
            if (Mathf.Abs(car.lateral - l1) <= half) return FarSide;
            return -1;
        }

        // Is `lane` free around a car at `myDistance`: nobody in it from `behindM` behind to `aheadM` ahead. A car out
        // on the box strip (in its box, or pulling in or out) is in neither lane.
        public static bool LaneClear(int lane, float myDistance, IReadOnlyList<Car> cars, float pitWidth, float parkLateral,
                                     float behindM, float aheadM)
        {
            if (cars == null) return true;
            for (int i = 0; i < cars.Count; i++)
            {
                var c = cars[i];
                float g = c.distance - myDistance;
                if (g < -behindM || g > aheadM) continue;
                if (LaneOf(c, pitWidth, parkLateral) == lane) return false;
            }
            return true;
        }

        // The lane a car coming onto pit road at `myLateral` takes: the one it is nearer, unless a car there is
        // alongside or close enough ahead or behind to be run into - then the other, if that is any better. Two cars
        // coming in side by side each keep their own side rather than crossing each other's noses. (A car on its own
        // on the far side moves over to the box side once on pit road - BoxSideOpen.)
        public static int EntryLane(float myDistance, float myLateral, IReadOnlyList<Car> cars, float pitWidth, float parkLateral,
                                    float behindM, float aheadM)
        {
            if (!TwoLanes(pitWidth, parkLateral)) return BoxSide;
            float l0 = Lateral(BoxSide, pitWidth, parkLateral), l1 = Lateral(FarSide, pitWidth, parkLateral);
            int near = Mathf.Abs(myLateral - l1) < Mathf.Abs(myLateral - l0) ? FarSide : BoxSide;
            int other = 1 - near;
            if (LaneClear(near, myDistance, cars, pitWidth, parkLateral, behindM, aheadM)) return near;
            return LaneClear(other, myDistance, cars, pitWidth, parkLateral, behindM, aheadM) ? other : near;
        }

        // Whether a car in the far lane at `myDistance` down pit road, `travelled` m since it came onto it, may move
        // across to the box side now: far enough in (`settleM`) that a car that came in alongside is on pit road too
        // and can be seen, and the lane clear.
        public static bool BoxSideOpen(float myDistance, float travelled, IReadOnlyList<Car> cars, float pitWidth, float parkLateral,
                                       float behindM, float aheadM, float settleM)
            => travelled >= settleM && LaneClear(BoxSide, myDistance, cars, pitWidth, parkLateral, behindM, aheadM);

        // The lane a car pulling out of its box at `myDistance` takes, or -1 to stay in the box and wait for a gap.
        // The far lane first - clear of the cars braking down the box side for their boxes - if nobody is in it; the
        // box-side lane if the far one is taken; and if both are taken, neither. Reaching the far lane means crossing
        // the box-side lane, so that has to be clear too: a car in the box-side lane always means wait.
        // One lane (too narrow, or no box strip): the middle - the caller keeps its own gate there.
        public static int ExitLane(float myDistance, IReadOnlyList<Car> cars, float pitWidth, float parkLateral,
                                   float behindSeconds, float behindMinM, float aheadM)
        {
            if (!TwoLanes(pitWidth, parkLateral)) return BoxSide;
            if (ExitBlocked(BoxSide, myDistance, cars, pitWidth, parkLateral, behindSeconds, behindMinM, aheadM)) return -1;
            return ExitBlocked(FarSide, myDistance, cars, pitWidth, parkLateral, behindSeconds, behindMinM, aheadM)
                ? BoxSide : FarSide;
        }

        // Is there a car in `lane` that a car pulling out of its box at `myDistance` would pull into or across the nose
        // of? Alongside or ahead up to `aheadM` (the length of the pull-out), at any speed - a car stopped there is
        // as much in the way as a moving one. Behind, a moving car close enough to arrive within `behindSeconds`
        // (never less than `behindMinM`): pit-road speed covers a short fixed gap in well under the time a car takes
        // to get rolling out of its box. Not a car behind that is braking to stop in a box short of mine - it never
        // gets here - nor one stopped or queuing behind, which waits for me.
        public static bool ExitBlocked(int lane, float myDistance, IReadOnlyList<Car> cars, float pitWidth, float parkLateral,
                                       float behindSeconds, float behindMinM, float aheadM)
        {
            if (cars == null) return false;
            for (int i = 0; i < cars.Count; i++)
            {
                var c = cars[i];
                if (LaneOf(c, pitWidth, parkLateral) != lane) continue;
                float g = c.distance - myDistance;
                if (g > aheadM) continue;
                if (g >= -AlongsideM) return true;
                if (c.mph < MovingMph) continue;
                if (c.stopLine > 0f && c.stopLine < myDistance - AlongsideM) continue;
                float reach = Mathf.Max(behindMinM, c.mph * MphToMps * Mathf.Max(0f, behindSeconds));
                if (-g <= reach) return true;
            }
            return false;
        }
    }
}
