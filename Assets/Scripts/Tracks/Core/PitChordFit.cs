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
