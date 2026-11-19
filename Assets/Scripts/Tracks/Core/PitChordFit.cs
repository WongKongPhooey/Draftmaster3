using System;
using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Tracks
{
    // Pit road cut straight across the infield, fitted to ANY lap shape.
    //
    // OvalGeometry.TryBuildChordPitLane builds Daytona's chord out of the oval formula's own pieces - it needs a
    // front stretch that starts and ends on the x axis with a labelled turn either side - so it cannot survive a
    // traced lap, whose pieces are whatever the trace says. This works from the lap as a road instead: given
    // where the main line is and which way it points at any distance, it finds an arc - straight - arc that leaves
    // the main line tangentially before the start/finish line, runs straight across the inside, and rejoins it
    // tangentially after, with the straight held a set distance in from the racing line.
    //
    // Both arcs turn the same way (towards the infield) with one radius, so each candidate pair of entry and exit
    // points has exactly one answer: the two arcs' centres fix the straight, which runs parallel to the line
    // joining them. The search is over where to leave and where to rejoin.
    public static class PitChordFit
    {
        public struct Pose
        {
            public Vector2 position;
            public float headingDeg;   // 0 = +x, counter-clockwise positive, the TrackInfoV2 convention
            public Pose(Vector2 position, float headingDeg) { this.position = position; this.headingDeg = headingDeg; }
        }

        public struct Road
        {
            public float entryDistance, exitDistance;   // along the main line, folded into [0, lap)
            public float radius;
            public float entryTurnDeg, straightLength, exitTurnDeg;   // signed like a TrackSegment angle
            public float nearestClearance;               // closest the straight comes to the main centreline (m)
            public float EntryArc => radius * Mathf.Abs(entryTurnDeg) * Mathf.Deg2Rad;
            public float ExitArc => radius * Mathf.Abs(exitTurnDeg) * Mathf.Deg2Rad;
            public float Length => EntryArc + straightLength + ExitArc;
        }

        // Arc - straight - arc from pose a to pose b, both arcs of `radius` turning to `side` (+1 left, -1 right).
        // turnA / turnB come back signed (positive = left) and between 0 and 360 degrees in the turning direction.
        public static bool TryConnect(Pose a, Pose b, float radius, float side,
                                      out float turnA, out float straight, out float turnB)
        {
            turnA = turnB = straight = 0f;
            if (radius <= 0f) return false;
            Vector2 centreA = a.position + Left(a.headingDeg) * side * radius;
            Vector2 centreB = b.position + Left(b.headingDeg) * side * radius;
            Vector2 d = centreB - centreA;
            straight = d.magnitude;
            if (straight < 1e-3f) return false;

            float theta = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            turnA = side * Wrap360(side * (theta - a.headingDeg));
            turnB = side * Wrap360(side * (b.headingDeg - theta));
            return true;
        }

        // The point `distance` along a connected road (for walking it in tests and clearance checks).
        public static Vector2 PointOnRoad(Pose a, Road road, float side, float distance)
        {
            Vector2 pos = a.position;
            float heading = a.headingDeg;
            float left = distance;
            Arc(ref pos, ref heading, road.radius, road.entryTurnDeg, Mathf.Min(left, road.EntryArc));
            left -= road.EntryArc;
            if (left <= 0f) return pos;
            float run = Mathf.Min(left, road.straightLength);
            pos += Forward(heading) * run;
            left -= run;
            if (left <= 0f) return pos;
            Arc(ref pos, ref heading, road.radius, road.exitTurnDeg, Mathf.Min(left, road.ExitArc));
            return pos;
        }

        // Find the chord. `poseAt` samples the main centreline at any distance (wrapping is handled here).
        // `side` is the infield side (+1 for a counter-clockwise, left-hand lap). The straight is held
        // `clearance` metres (centre to centre) in from the racing line at its nearest, at both ends.
        public static bool TryFit(Func<float, Pose> poseAt, float lapLength, float startFinish, float side,
                                  float radius, float clearance, out Road road,
                                  float minReach = 80f, float maxReachShare = 0.3f, float maxTurnDeg = 75f)
        {
            road = default;
            if (poseAt == null || lapLength <= 0f || radius <= 0f) return false;
            float maxReach = lapLength * maxReachShare;

            // The stretch of main line the chord can come near, sampled once.
            var main = new List<Vector2>();
            for (float d = -maxReach - 50f; d <= maxReach + 50f; d += 4f)
                main.Add(poseAt(Fold(startFinish + d, lapLength)).position);

            bool found = false;
            float bestCost = float.MaxValue;
            float bestE = 0f, bestX = 0f;
            Vector2 line = poseAt(Fold(startFinish, lapLength)).position;

            // Many chords come within `clearance` of the racing line at both ends - one cut deep across the
            // infield between two corners does too, touching each corner as it passes. The one wanted runs along
            // the front stretch, so among chords that hold the clearance (to within a few metres) the winner is
            // the one whose straight passes closest to the start/finish line. Clearance error is still the cost
            // when nothing holds it.
            const float ClearanceSlack = 3f;
            void Try(float reachIn, float reachOut)
            {
                if (reachIn < minReach || reachOut < minReach || reachIn > maxReach || reachOut > maxReach) return;
                if (!Evaluate(poseAt, main, lapLength, startFinish - reachIn, startFinish + reachOut, side, radius,
                              maxTurnDeg, out var candidate))
                    return;
                float error = Cost(poseAt, main, lapLength, startFinish - reachIn, side, candidate, clearance);
                var entryPose = poseAt(Fold(startFinish - reachIn, lapLength));
                Vector2 mid = PointOnRoad(entryPose, candidate, side, candidate.EntryArc + candidate.straightLength * 0.5f);
                float depth = Vector2.Distance(mid, line);
                float cost = error <= 2f * ClearanceSlack * ClearanceSlack ? depth : 1e6f + error;
                if (cost < bestCost)
                {
                    bestCost = cost; bestE = reachIn; bestX = reachOut; found = true;
                }
            }

            for (float e = minReach; e <= maxReach; e += 20f)
                for (float x = minReach; x <= maxReach; x += 20f)
                    Try(e, x);
            if (!found) return false;

            float coarseE = bestE, coarseX = bestX;
            for (float e = coarseE - 20f; e <= coarseE + 20f; e += 2f)
                for (float x = coarseX - 20f; x <= coarseX + 20f; x += 2f)
                    Try(e, x);

            Evaluate(poseAt, main, lapLength, startFinish - bestE, startFinish + bestX, side, radius, maxTurnDeg,
                     out road);
            road.nearestClearance = NearestClearance(poseAt, main, lapLength, startFinish - bestE, side, road,
                                                     0f, 1f);
            return true;
        }

        // Pit road along a known line - a mapped pit lane. The straight lies ON the line (`linePoint`, `lineDir` in
        // the direction cars travel it), and each arc is the one of `radius` that leaves the main line tangentially
        // and meets the pit line tangentially, so nothing is guessed but where the arcs start. Entry is searched
        // within `window` metres of `entryNear` (the main-line distance nearest the mapped lane's first point), exit
        // likewise round `exitNear`. Radii are tried largest first; the first that fits on both ends wins.
        public static bool TryFitToLine(Func<float, Pose> poseAt, float lapLength, float side,
                                        Vector2 linePoint, Vector2 lineDir, float entryNear, float exitNear,
                                        float window, float[] radii, out Road road, float maxTurnDeg = 75f)
        {
            road = default;
            if (poseAt == null || lapLength <= 0f || radii == null || lineDir.sqrMagnitude < 1e-6f) return false;
            lineDir.Normalize();
            Vector2 lineLeft = new Vector2(-lineDir.y, lineDir.x);

            foreach (float radius in radii)
            {
                // Signed distance of the arc's centre from the pit line, less the radius: zero where the arc is
                // tangent to it. The arc turns to `side`, so its centre is `side` of both lines.
                float Gap(float d)
                {
                    var p = poseAt(Fold(d, lapLength));
                    Vector2 centre = p.position + Left(p.headingDeg) * side * radius;
                    return Vector2.Dot(centre - linePoint, lineLeft) * side - radius;
                }

                if (!Root(Gap, entryNear, window, out float entry)) continue;
                if (!Root(Gap, exitNear, window, out float exit)) continue;

                var a = poseAt(Fold(entry, lapLength));
                var b = poseAt(Fold(exit, lapLength));
                if (!TryConnect(a, b, radius, side, out float turnA, out float straight, out float turnB)) continue;
                if (Mathf.Abs(turnA) > maxTurnDeg || Mathf.Abs(turnB) > maxTurnDeg) continue;

                // The straight must run along the line the way cars do, not back up it.
                Vector2 dir = Forward(a.headingDeg + turnA);
                if (Vector2.Dot(dir, lineDir) < 0.995f) continue;

                road = new Road
                {
                    entryDistance = Fold(entry, lapLength),
                    exitDistance = Fold(exit, lapLength),
                    radius = radius,
                    entryTurnDeg = turnA,
                    straightLength = straight,
                    exitTurnDeg = turnB,
                };
                return true;
            }
            return false;
        }

        // Pit road beside a STRAIGHT front stretch - a road course's pit lane (Indianapolis). TryFitToLine's arcs both
        // turn towards the infield, which can only meet a lane the main line curves across, as at an oval's tri-oval;
        // a lane running parallel to a straight needs an S at each end: towards the lane and straighten (entry), off
        // it towards the main line and straighten (exit). Each S is two arcs of `radius`. The second arc of the entry
        // and the first of the exit are tangent to the lane, so their centres sit `radius` from it on the main line's
        // side, `2 * radius` from the arc they meet - a circle-line intersection, nothing to search but where along
        // the main line to leave and rejoin. Those are chosen so the straight starts at `startAlong` and ends at
        // `endAlong` (metres along `lineDir` from `linePoint`, the mapped lane's ends) as nearly as the radius allows.
        public struct SideRoad
        {
            public float entryDistance, exitDistance, radius;
            public float entryOutDeg, entryInDeg;   // signed like a TrackSegment angle: towards the lane, then straighten
            public float straightLength;
            public float exitOutDeg, exitInDeg;     // off the lane towards the main line, then onto it
            public float startMiss, endMiss;        // how far the straight's ends are from the mapped ends (m)
            float ArcLength(float deg) => radius * Mathf.Abs(deg) * Mathf.Deg2Rad;
            public float EntryLength => ArcLength(entryOutDeg) + ArcLength(entryInDeg);
            public float ExitLength => ArcLength(exitOutDeg) + ArcLength(exitInDeg);
            public float Length => EntryLength + straightLength + ExitLength;
        }

        const float GoodMissMetres = 25f;

        public static bool TryFitAlongside(Func<float, Pose> poseAt, float lapLength, float side,
                                           Vector2 linePoint, Vector2 lineDir, float startAlong, float endAlong,
                                           float entryNear, float exitNear, float window, float[] radii,
                                           out SideRoad road, float maxTurnDeg = 60f)
        {
            road = default;
            if (poseAt == null || lapLength <= 0f || radii == null || lineDir.sqrMagnitude < 1e-6f) return false;
            lineDir.Normalize();
            Vector2 lineLeft = new Vector2(-lineDir.y, lineDir.x);
            float lineHeading = Mathf.Atan2(lineDir.y, lineDir.x) * Mathf.Rad2Deg;

            bool found = false;
            float bestMiss = float.MaxValue;
            foreach (float radius in radii)
            {
                // The lane's arcs' centres: on the main line's side of the lane, `radius` off it.
                Vector2 q = linePoint - lineLeft * side * radius;

                float bestE = 0f, missE = float.MaxValue, eOut = 0f, eIn = 0f, eFoot = 0f;
                float bestX = 0f, missX = float.MaxValue, xOut = 0f, xIn = 0f, xFoot = 0f;
                for (float d = entryNear - window; d <= entryNear + window; d += 2f)
                {
                    if (!SolveS(poseAt(Fold(d, lapLength)), true, side, radius, q, lineDir, lineLeft, lineHeading, linePoint,
                                maxTurnDeg, out float o, out float i, out float f)) continue;
                    if (Mathf.Abs(f - startAlong) < missE) { missE = Mathf.Abs(f - startAlong); bestE = d; eOut = o; eIn = i; eFoot = f; }
                }
                for (float d = exitNear - window; d <= exitNear + window; d += 2f)
                {
                    if (!SolveS(poseAt(Fold(d, lapLength)), false, side, radius, q, lineDir, lineLeft, lineHeading, linePoint,
                                maxTurnDeg, out float o, out float i, out float f)) continue;
                    if (Mathf.Abs(f - endAlong) < missX) { missX = Mathf.Abs(f - endAlong); bestX = d; xOut = o; xIn = i; xFoot = f; }
                }
                if (missE == float.MaxValue || missX == float.MaxValue || xFoot - eFoot < 1f) continue;
                // Radii are tried largest first, and the first whose straight lands within GoodMissMetres of both
                // mapped ends wins: a pit lane is a gentle S, not the tightest one that hits the mark exactly.
                bool good = Mathf.Max(missE, missX) <= GoodMissMetres;
                if (!good && missE + missX >= bestMiss) continue;

                bestMiss = good ? 0f : missE + missX;
                found = true;
                road = new SideRoad
                {
                    entryDistance = Fold(bestE, lapLength),
                    exitDistance = Fold(bestX, lapLength),
                    radius = radius,
                    entryOutDeg = eOut, entryInDeg = eIn,
                    straightLength = xFoot - eFoot,
                    exitOutDeg = xOut, exitInDeg = xIn,
                    startMiss = missE, endMiss = missX,
                };
                if (good) break;
            }
            return found;
        }

        // One S of TryFitAlongside from the main line pose `p`: the arc centred `radius` to `side` of it, the lane-side
        // centre 2r away (ahead for the entry, behind for the exit), the heading where the two arcs meet, and where
        // the S meets the lane (its foot, metres along the lane line).
        static bool SolveS(Pose p, bool entry, float side, float radius, Vector2 q, Vector2 lineDir, Vector2 lineLeft,
                           float lineHeading, Vector2 linePoint, float maxTurnDeg,
                           out float outDeg, out float inDeg, out float foot)
        {
            outDeg = inDeg = foot = 0f;
            Vector2 c1 = p.position + Left(p.headingDeg) * side * radius;
            Vector2 w = q - c1;
            float b = Vector2.Dot(w, lineDir);
            float disc = b * b - (w.sqrMagnitude - 4f * radius * radius);
            if (disc < 0f) return false;
            float t = entry ? -b + Mathf.Sqrt(disc) : -b - Mathf.Sqrt(disc);
            Vector2 c2 = q + lineDir * t;
            Vector2 radial = (c1 + c2) * 0.5f - c1;
            Vector2 travel = side * new Vector2(-radial.y, radial.x);
            float meet = Mathf.Atan2(travel.y, travel.x) * Mathf.Rad2Deg;
            // Entry: main heading -> meet turning to `side`, meet -> lane turning away from it.
            // Exit: lane -> meet turning away from `side`, meet -> main heading turning to it.
            float toSide = entry ? side * Mathf.DeltaAngle(p.headingDeg, meet) : side * Mathf.DeltaAngle(meet, p.headingDeg);
            float away = entry ? side * Mathf.DeltaAngle(lineHeading, meet) : side * Mathf.DeltaAngle(meet, lineHeading);
            if (toSide < 0.5f || toSide > maxTurnDeg || away < -0.01f || away > maxTurnDeg) return false;
            outDeg = entry ? side * toSide : -side * Mathf.Max(0f, away);
            inDeg = entry ? -side * Mathf.Max(0f, away) : side * toSide;
            foot = Vector2.Dot(c2 + lineLeft * side * radius - linePoint, lineDir);
            return true;
        }

        // A mapped pit lane (already in the lap's plane, in the direction cars use it) as pit road beside a straight
        // front stretch: the lane's straight runs parallel to the main line at the lane's middle, at the mapped lane's
        // mean distance from the main line (never closer than `minOffset`, which keeps pit road off the racing
        // surface), from where the mapped lane starts to where it ends. See TryFitAlongside.
        public static bool TryFitMappedLaneAlongside(Func<float, Pose> poseAt, float lapLength, float side,
                                                     IList<Vector2> lane, float minOffset, out SideRoad road,
                                                     out float offset)
        {
            road = default;
            offset = 0f;
            if (poseAt == null || lane == null || lane.Count < 2 || lapLength <= 0f) return false;

            float Nearest(Vector2 q)
            {
                float best = 0f, bestSq = float.MaxValue;
                for (float d = 0f; d < lapLength; d += 2f)
                {
                    float sq = (poseAt(d).position - q).sqrMagnitude;
                    if (sq < bestSq) { bestSq = sq; best = d; }
                }
                return best;
            }

            // Length-weighted mean distance of the lane from the main line, and its middle.
            float total = 0f, weighted = 0f;
            for (int i = 1; i < lane.Count; i++) total += Vector2.Distance(lane[i - 1], lane[i]);
            if (total < 1f) return false;
            Vector2 middle = lane[0];
            float walked = 0f;
            for (int i = 1; i < lane.Count; i++)
            {
                float len = Vector2.Distance(lane[i - 1], lane[i]);
                for (float s = 2.5f; s < len; s += 5f)
                {
                    Vector2 q = Vector2.Lerp(lane[i - 1], lane[i], s / len);
                    var p = poseAt(Nearest(q));
                    weighted += Vector2.Dot(q - p.position, Left(p.headingDeg)) * side * Mathf.Min(5f, len - s + 2.5f);
                }
                if (walked < total * 0.5f && walked + len >= total * 0.5f)
                    middle = Vector2.Lerp(lane[i - 1], lane[i], (total * 0.5f - walked) / Mathf.Max(len, 1e-4f));
                walked += len;
            }
            offset = Mathf.Max(minOffset, weighted / total);

            var mid = poseAt(Nearest(middle));
            Vector2 dir = Forward(mid.headingDeg);
            Vector2 linePoint = mid.position + Left(mid.headingDeg) * side * offset;
            float startAlong = Vector2.Dot(lane[0] - linePoint, dir);
            float endAlong = Vector2.Dot(lane[lane.Count - 1] - linePoint, dir);

            // Where the main line curves in towards the lane's line (Indianapolis's last corner, onto the front
            // stretch), the mapped lane's end would put pit road on the racing surface: start and end the straight
            // where it is clear of it.
            bool Clear(float along)
            {
                Vector2 q = linePoint + dir * along;
                var p = poseAt(Nearest(q));
                return Vector2.Dot(q - p.position, Left(p.headingDeg)) * side >= minOffset - 0.5f;
            }
            while (startAlong < endAlong && !Clear(startAlong)) startAlong += 5f;
            while (endAlong > startAlong && !Clear(endAlong)) endAlong -= 5f;
            if (endAlong - startAlong < 50f) return false;
            var radii = new[] { 120f, 90f, 60f, 40f, 30f };
            return TryFitAlongside(poseAt, lapLength, side, linePoint, dir, startAlong, endAlong,
                                   Nearest(lane[0]), Nearest(lane[lane.Count - 1]), 400f, radii, out road);
        }

        // The point `distance` along a side road from its entry pose (for walking it in tests).
        public static Vector2 PointOnSideRoad(Pose a, SideRoad road, float distance)
        {
            Vector2 pos = a.position;
            float heading = a.headingDeg, left = distance;
            var turns = new[] { road.entryOutDeg, road.entryInDeg, 0f, road.exitOutDeg, road.exitInDeg };
            for (int k = 0; k < turns.Length && left > 0f; k++)
            {
                if (k == 2)
                {
                    float run = Mathf.Min(left, road.straightLength);
                    pos += Forward(heading) * run;
                    left -= run;
                    continue;
                }
                float arc = road.radius * Mathf.Abs(turns[k]) * Mathf.Deg2Rad;
                Arc(ref pos, ref heading, road.radius, turns[k], Mathf.Min(left, arc));
                left -= arc;
            }
            return pos;
        }

        // The zero of f nearest `near` within ±window, by a 2 m scan then bisection.
        static bool Root(Func<float, float> f, float near, float window, out float root)
        {
            root = near;
            bool found = false;
            float bestOff = float.MaxValue;
            float prevD = near - window, prev = f(prevD);
            for (float d = near - window + 2f; d <= near + window; d += 2f)
            {
                float v = f(d);
                if (Mathf.Sign(v) != Mathf.Sign(prev))
                {
                    float lo = prevD, hi = d, flo = prev;
                    for (int k = 0; k < 30; k++)
                    {
                        float mid = 0.5f * (lo + hi), fm = f(mid);
                        if (Mathf.Sign(fm) == Mathf.Sign(flo)) { lo = mid; flo = fm; } else hi = mid;
                    }
                    float r = 0.5f * (lo + hi);
                    if (Mathf.Abs(r - near) < bestOff) { bestOff = Mathf.Abs(r - near); root = r; found = true; }
                }
                prevD = d; prev = v;
            }
            return found;
        }

        static bool Evaluate(Func<float, Pose> poseAt, List<Vector2> main, float lap, float entry, float exit,
                             float side, float radius, float maxTurnDeg, out Road road)
        {
            road = default;
            var a = poseAt(Fold(entry, lap));
            var b = poseAt(Fold(exit, lap));
            if (!TryConnect(a, b, radius, side, out float turnA, out float straight, out float turnB)) return false;
            if (Mathf.Abs(turnA) < 0.5f || Mathf.Abs(turnB) < 0.5f) return false;
            if (Mathf.Abs(turnA) > maxTurnDeg || Mathf.Abs(turnB) > maxTurnDeg) return false;

            road = new Road
            {
                entryDistance = Fold(entry, lap),
                exitDistance = Fold(exit, lap),
                radius = radius,
                entryTurnDeg = turnA,
                straightLength = straight,
                exitTurnDeg = turnB,
            };

            // Every point of the straight must be on the infield side of the main line - a chord that crosses
            // the racing surface is not a pit road.
            for (float s = 0f; s <= straight; s += 10f)
            {
                Vector2 p = PointOnRoad(a, road, side, road.EntryArc + s);
                int i = Nearest(main, p);
                Vector2 tangent = main[Mathf.Min(i + 1, main.Count - 1)] - main[Mathf.Max(i - 1, 0)];
                Vector2 leftN = new Vector2(-tangent.y, tangent.x);
                if (Vector2.Dot(p - main[i], leftN) * side <= 0f) return false;
            }
            return true;
        }

        // Both halves of the straight should come to `clearance` of the racing line, so neither end of pit road
        // crowds the track and neither runs pointlessly deep into the infield.
        static float Cost(Func<float, Pose> poseAt, List<Vector2> main, float lap, float entry, float side,
                          Road road, float clearance)
        {
            float first = NearestClearance(poseAt, main, lap, entry, side, road, 0f, 0.5f);
            float second = NearestClearance(poseAt, main, lap, entry, side, road, 0.5f, 1f);
            return (first - clearance) * (first - clearance) + (second - clearance) * (second - clearance);
        }

        static float NearestClearance(Func<float, Pose> poseAt, List<Vector2> main, float lap, float entry,
                                      float side, Road road, float from01, float to01)
        {
            var a = poseAt(Fold(entry, lap));
            float nearest = float.MaxValue;
            for (float t = from01; t <= to01 + 1e-4f; t += 0.05f)
            {
                Vector2 p = PointOnRoad(a, road, side, road.EntryArc + road.straightLength * t);
                nearest = Mathf.Min(nearest, Vector2.Distance(p, main[Nearest(main, p)]));
            }
            return nearest;
        }

        static int Nearest(List<Vector2> pts, Vector2 p)
        {
            int best = 0;
            float bestSq = float.MaxValue;
            for (int i = 0; i < pts.Count; i++)
            {
                float sq = (pts[i] - p).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = i; }
            }
            return best;
        }

        // Same arc walk as TrackInfoV2.AdvanceAlongSegment, so a fitted road lands where the builder draws it.
        static void Arc(ref Vector2 pos, ref float headingDeg, float radius, float turnDeg, float dist)
        {
            if (dist <= 0f || Mathf.Abs(turnDeg) < 1e-4f) return;
            float arc = radius * Mathf.Abs(turnDeg) * Mathf.Deg2Rad;
            float fraction = Mathf.Clamp01(dist / Mathf.Max(arc, 1e-4f));
            Vector2 centre = pos + (turnDeg >= 0f ? Left(headingDeg) : -Left(headingDeg)) * radius;
            Vector2 radial = pos - centre;
            float start = Mathf.Atan2(radial.y, radial.x);
            float end = start + turnDeg * Mathf.Deg2Rad * fraction;
            pos = centre + new Vector2(Mathf.Cos(end), Mathf.Sin(end)) * radius;
            headingDeg += turnDeg * fraction;
        }

        static Vector2 Forward(float headingDeg)
            => new Vector2(Mathf.Cos(headingDeg * Mathf.Deg2Rad), Mathf.Sin(headingDeg * Mathf.Deg2Rad));

        static Vector2 Left(float headingDeg)
        {
            Vector2 f = Forward(headingDeg);
            return new Vector2(-f.y, f.x);
        }

        static float Wrap360(float deg) => ((deg % 360f) + 360f) % 360f;

        public static float Fold(float distance, float lap) => lap > 0f ? ((distance % lap) + lap) % lap : distance;
    }
}
