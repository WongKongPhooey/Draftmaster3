using System;
using System.Collections.Generic;

namespace Draftmaster.Sim
{
    // Who goes out next in a practice or qualifying session, and how many cars the track holds at once.
    //
    // The field waits parked in its boxes and the director sends cars out a few at a time. Two rules keep
    // every car getting a run:
    //   - the next car out is the one that needs it most — no time on the board first, then the fewest runs,
    //     then whoever has been waiting longest — not whoever happens to be first in the list;
    //   - the number on track is sized so the whole field can get through one run inside the session.
    //
    // Kept free of MonoBehaviour state so it can be unit-tested in EditMode.
    public static class PracticeRotation
    {
        // Share of the session the whole field should be able to get round in once, leaving the rest for
        // second runs and for the cars that come out late.
        public const float FirstRoundShare = 0.7f;

        // How many cars may be on track at once. `floor` is the director's normal cap; the cap only rises
        // above it when the field could not all get a run inside FirstRoundShare of the session at that
        // rate. Never above `ceiling` or the field size.
        public static int OnTrackCap(int fieldSize, float sessionSeconds, float runSeconds, int floor, int ceiling)
        {
            if (fieldSize <= 0) return 0;
            int cap = floor;
            float usable = sessionSeconds * FirstRoundShare;
            if (usable > 0f && runSeconds > 0f)
            {
                int needed = (int)Math.Ceiling(fieldSize * runSeconds / usable);
                if (needed > cap) cap = needed;
            }
            if (cap > ceiling) cap = ceiling;
            if (cap > fieldSize) cap = fieldSize;
            return Math.Max(1, cap);
        }

        // Rough length (s) of one run: out of the box, `laps` laps at `avgSpeedMps`, back into the box.
        public static float RunSeconds(float lapMetres, int laps, float avgSpeedMps, float pitOverheadSeconds)
        {
            if (lapMetres <= 0f || avgSpeedMps <= 0f) return pitOverheadSeconds;
            return laps * lapMetres / avgSpeedMps + pitOverheadSeconds;
        }

        // True when `a` should go out before `b`.
        public static bool GoesBefore(bool aHasTime, int aRuns, float aReadySince,
                                      bool bHasTime, int bRuns, float bReadySince)
        {
            if (aHasTime != bHasTime) return !aHasTime;
            if (aRuns != bRuns) return aRuns < bRuns;
            return aReadySince < bReadySince;
        }

        // Order the cars ready to go out, neediest first. Returns indices into `cars`.
        public static List<int> ReleaseOrder<T>(IReadOnlyList<T> cars, Func<T, bool> ready, Func<T, bool> hasTime,
                                               Func<T, int> runs, Func<T, float> readySince)
        {
            var order = new List<int>();
            for (int i = 0; i < cars.Count; i++)
                if (ready(cars[i])) order.Add(i);
            order.Sort((x, y) =>
            {
                T a = cars[x], b = cars[y];
                if (GoesBefore(hasTime(a), runs(a), readySince(a), hasTime(b), runs(b), readySince(b))) return -1;
                if (GoesBefore(hasTime(b), runs(b), readySince(b), hasTime(a), runs(a), readySince(a))) return 1;
                return x.CompareTo(y);
            });
            return order;
        }
    }
}
