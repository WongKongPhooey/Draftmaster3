using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Sim
{
    // A car stopped behind a wreck that fills the road: is it stuck, and which way round is there - across the
    // pile-up, past every stopped car on that side, to a lateral clear of the lot that is still inside the limits it
    // may drive between (the road, or the road and the grass beside it). Pure maths in one car's track frame, as
    // PackAvoidance: gaps are metres between car CENTRES along the track (+ ahead), laterals metres from the
    // centreline (+ right of travel), speeds mph.
    //
    // FormationController uses it under a caution, where the pack planner otherwise only ever swerves round the one
    // car ahead within the road, and stops behind a pile-up for good when neither side of that car is open.
    public static class WreckEscape
    {
        public struct Car
        {
            public float Gap;
            public float Lateral;
            public float HalfWidth;
            public float HalfLength;
            public float SpeedMph;
        }

        public struct Self
        {
            public float Lateral;
            public float HalfWidth;
            public float HalfLength;
        }

        public struct Settings
        {
            public float StoppedMph;   // below this a car is part of the wreck, not traffic
            public float ScanM;        // a stopped car this far ahead (m, bumper to bumper) in my lane has me stuck
            public float ClearM;       // daylight (m) kept between me and each stopped car I go round
            public float LookPastM;    // stopped cars this far (m) beyond the first are part of the same pile-up
            public float MovingWindowM;// a moving car within this much (m, bumper to bumper) of the spot shuts that side

            public static Settings Default => new Settings
            {
                StoppedMph = 3f,
                ScanM = 25f,
                ClearM = 1.2f,
                LookPastM = 30f,
                MovingWindowM = 6f,
            };
        }

        static bool Overlaps(float aLo, float aHi, float bLo, float bHi) => aLo < bHi && bLo < aHi;

        // The nearest stopped car ahead in my lane, within ScanM of my nose: what has me stuck. -1 for none.
        public static int StoppedBlocker(in Self me, IReadOnlyList<Car> cars, in Settings s)
        {
            int best = -1;
            float bestClear = float.MaxValue;
            if (cars == null) return best;
            for (int i = 0; i < cars.Count; i++)
            {
                var c = cars[i];
                if (c.SpeedMph >= s.StoppedMph || c.Gap <= 0f) continue;
                float clear = c.Gap - me.HalfLength - c.HalfLength;
                if (clear > s.ScanM) continue;
                if (!Overlaps(me.Lateral - me.HalfWidth, me.Lateral + me.HalfWidth,
                              c.Lateral - c.HalfWidth, c.Lateral + c.HalfWidth)) continue;
                if (clear < bestClear) { bestClear = clear; best = i; }
            }
            return best;
        }

        // Which way round the pile-up blocker is part of: +1 right, -1 left, 0 neither. `target` is the lateral to
        // drive at, `pastGap` how far (m) I have to go along the track to be clear past the last stopped car gone round.
        // On each side the spot steps out past every stopped car it would touch, beside me or up to LookPastM beyond
        // the blocker, until a car fits; the side is no good if that spot is outside [lo, hi], if a stopped car sits
        // beside me between here and there, or if a moving car is there. The shorter move wins.
        public static int FindWayRound(in Self me, IReadOnlyList<Car> cars, int blocker, float lo, float hi, in Settings s,
                                       out float target, out float pastGap)
        {
            target = me.Lateral;
            pastGap = 0f;
            if (cars == null || blocker < 0 || blocker >= cars.Count) return 0;
            var b = cars[blocker];
            float bestCost = float.MaxValue;
            int pick = 0;
            for (int k = 0; k < 2; k++)
            {
                int dir = k == 0 ? 1 : -1;
                float edge = b.Lateral + dir * b.HalfWidth;
                float past = b.Gap + b.HalfLength + me.HalfLength + s.ClearM;
                float spot = edge + dir * (me.HalfWidth + s.ClearM);
                for (int pass = 0; pass < 32; pass++)
                {
                    bool moved = false;
                    float spotLo = spot - me.HalfWidth - s.ClearM, spotHi = spot + me.HalfWidth + s.ClearM;
                    for (int i = 0; i < cars.Count; i++)
                    {
                        var c = cars[i];
                        if (c.SpeedMph >= s.StoppedMph) continue;
                        if (c.Gap < -(me.HalfLength + c.HalfLength) || c.Gap > b.Gap + s.LookPastM) continue;
                        float cLo = c.Lateral - c.HalfWidth, cHi = c.Lateral + c.HalfWidth;
                        if (!Overlaps(spotLo, spotHi, cLo, cHi)) continue;
                        float outer = dir > 0 ? cHi : cLo;
                        if ((outer - edge) * dir <= 0f) continue;
                        edge = outer;
                        past = Mathf.Max(past, c.Gap + c.HalfLength + me.HalfLength + s.ClearM);
                        moved = true;
                    }
                    spot = edge + dir * (me.HalfWidth + s.ClearM);
                    if (!moved) break;
                }
                if (spot < lo || spot > hi) continue;
                if (SideShut(me, cars, spot, s)) continue;
                float cost = Mathf.Abs(spot - me.Lateral);
                if (cost < bestCost) { bestCost = cost; pick = dir; target = spot; pastGap = past; }
            }
            return pick;
        }

        // A stopped car alongside me in the strip between where I am and the spot (I cannot get across it), or a moving
        // car at the spot or crossing to it near me.
        static bool SideShut(in Self me, IReadOnlyList<Car> cars, float spot, in Settings s)
        {
            float stripLo = Mathf.Min(me.Lateral, spot) - me.HalfWidth, stripHi = Mathf.Max(me.Lateral, spot) + me.HalfWidth;
            float spotLo = spot - me.HalfWidth - 0.3f, spotHi = spot + me.HalfWidth + 0.3f;
            for (int i = 0; i < cars.Count; i++)
            {
                var c = cars[i];
                float lengths = me.HalfLength + c.HalfLength;
                float cLo = c.Lateral - c.HalfWidth, cHi = c.Lateral + c.HalfWidth;
                if (c.SpeedMph < s.StoppedMph)
                {
                    // Beside me, not ahead: no driving through it. Most of its length level with mine - one whose tail
                    // just reaches my nose is ahead, part of what is being walked across.
                    if (Mathf.Abs(c.Gap) < lengths * 0.6f && Overlaps(stripLo, stripHi, cLo, cHi)
                        && !Overlaps(me.Lateral - me.HalfWidth, me.Lateral + me.HalfWidth, cLo, cHi))
                        return true;
                    continue;
                }
                if (Mathf.Abs(c.Gap) > lengths + s.MovingWindowM) continue;
                if (Overlaps(spotLo, spotHi, cLo, cHi)) return true;
            }
            return false;
        }
    }
}
