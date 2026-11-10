using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Sim
{
    // Pit road as two driving lanes, side by side down the pit ribbon, beside the strip the boxes stand on.
    //
    // Lane 0 runs next to the boxes: it is the lane a car brakes down to turn into its own box. Lane 1 is the far
    // lane. A car coming in alongside another takes lane 1 rather than running into it, and moves across to lane 0
    // once there is room; a car leaving its box pulls out across lane 0 into lane 1 and runs that to the exit, clear
    // of the cars still braking down lane 0 for their boxes. A pit road too narrow for two cars side by side (or with
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
        }

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
    }
}
