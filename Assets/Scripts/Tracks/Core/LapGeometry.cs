using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Tracks
{
    // A lap as an ordered list of straights and corners, and the two things you always have to do to one:
    // find out whether it joins up, and make it.
    //
    // Both sources of hand-made track geometry need this. A legacy TrackInfo was measured for a
    // one-dimensional game, so nothing ever forced its plan view to close — Phoenix's misses by 93m. A
    // centreline traced off OpenStreetMap closes in reality but not in arithmetic, because a mapper's clicks
    // and the resampling done to them leave a few metres and a few degrees of slop. Either way the segments
    // have to be nudged until the road actually meets itself, or it is drawn with a step in it.
    //
    // Only the straights are moved. The corner angles fix every heading round the lap, so each corner
    // contributes a FIXED displacement and each straight contributes its length along a FIXED direction:
    // closure is linear in the straight lengths. Two equations, one per axis, against however many straights
    // there are — and the answer taken is the one that moves the measured numbers least. The corners come
    // through untouched, which matters because they are the part that was actually measured.
    public static class LapGeometry
    {
        public struct Piece
        {
            public bool isTurn;
            public float length;      // metres along the centreline
            public float angle;       // degrees of heading change; positive = left. Zero for a straight.

            public Piece(bool isTurn, float length, float angle)
            {
                this.isTurn = isTurn; this.length = length; this.angle = angle;
            }
        }

        // Where one piece takes you, from a given heading: a chord for a straight, an arc for a corner.
        public static Vector2 Displacement(Piece piece, float headingDeg)
        {
            float h = headingDeg * Mathf.Deg2Rad;
            if (!piece.isTurn || Mathf.Abs(piece.angle) < 1e-4f)
                return new Vector2(Mathf.Cos(h), Mathf.Sin(h)) * piece.length;

            float a = piece.angle * Mathf.Deg2Rad;
            float r = piece.length / a;                     // signed radius
            return new Vector2(r * (Mathf.Sin(h + a) - Mathf.Sin(h)),
                               -r * (Mathf.Cos(h + a) - Mathf.Cos(h)));
        }

        // How far the lap misses its own start by, in metres.
        public static float ClosureGap(IList<Piece> lap)
        {
            Vector2 at = Vector2.zero;
            float heading = 0f;
            for (int i = 0; i < lap.Count; i++)
            {
                at += Displacement(lap[i], heading);
                heading += lap[i].angle;
            }
            return at.magnitude;
        }

        public static float TotalAngle(IList<Piece> lap)
        {
            float total = 0f;
            for (int i = 0; i < lap.Count; i++) total += lap[i].angle;
            return total;
        }

        public static float TotalLength(IList<Piece> lap)
        {
            float total = 0f;
            for (int i = 0; i < lap.Count; i++) total += lap[i].length;
            return total;
        }

        // Spread the corner angles so they sum to exactly one full turn, in proportion to what they already
        // are. A traced lap comes back a few degrees out purely from resampling, and those few degrees are
        // the difference between a road that closes and one that spirals.
        public static void NormaliseTurnAngles(IList<Piece> lap, float target = 360f)
        {
            float total = TotalAngle(lap);
            if (Mathf.Abs(total) < 1f) return;

            float k = target / total;
            for (int i = 0; i < lap.Count; i++)
            {
                if (!lap[i].isTurn) continue;
                var piece = lap[i];
                piece.angle *= k;
                lap[i] = piece;
            }
        }

        // Shut the loop, preferring to disturb as little as possible.
        //
        // First try it on the straights alone, because on a hand-measured lap the corners are the part that
        // was actually surveyed and the straights are the part that was eyeballed. That works for a circuit
        // whose straights point in a few different directions — a rounded triangle, a road course.
        //
        // It cannot work for a plain oval. Its two straights are ANTIPARALLEL, so between them they can only
        // move the far end of the lap along one axis; the perpendicular error has nowhere to go, and the
        // solve either fails or asks for a straight of negative length. That error belongs to the corners
        // anyway: on an oval, how far apart the two ends sit IS the corner radius. So the fallback lets every
        // segment give a little, which for a corner means its arc length and therefore its radius.
        public static bool Close(IList<Piece> lap, float minLength = 1f)
        {
            return CloseByStraights(lap, minLength) || CloseByEverything(lap, minLength);
        }

        // Move the straights, and only the straights, until the lap joins up. False when that cannot be done
        // without folding one of them away entirely, in which case nothing is changed.
        public static bool CloseByStraights(IList<Piece> lap, float minStraight = 1f)
        {
            var index = new List<int>();
            var dirs = new List<Vector2>();
            Vector2 fixedPart = Vector2.zero;
            float heading = 0f;

            for (int i = 0; i < lap.Count; i++)
            {
                if (lap[i].isTurn)
                {
                    fixedPart += Displacement(lap[i], heading);
                    heading += lap[i].angle;
                }
                else
                {
                    index.Add(i);
                    float h = heading * Mathf.Deg2Rad;
                    dirs.Add(new Vector2(Mathf.Cos(h), Mathf.Sin(h)));
                }
            }
            if (index.Count == 0) return false;

            Vector2 walked = fixedPart;
            for (int k = 0; k < index.Count; k++) walked += dirs[k] * lap[index[k]].length;
            Vector2 residual = -walked;

            // Least-norm correction: s = s0 + A^T (A A^T)^-1 residual, A being the 2xN of straight directions.
            float axx = 0f, axy = 0f, ayy = 0f;
            foreach (var d in dirs) { axx += d.x * d.x; axy += d.x * d.y; ayy += d.y * d.y; }
            float det = axx * ayy - axy * axy;
            if (Mathf.Abs(det) < 1e-6f) return false;       // every straight runs the same way

            float lx = (ayy * residual.x - axy * residual.y) / det;
            float ly = (-axy * residual.x + axx * residual.y) / det;

            var lengths = new float[index.Count];
            for (int k = 0; k < index.Count; k++)
            {
                lengths[k] = lap[index[k]].length + dirs[k].x * lx + dirs[k].y * ly;
                if (lengths[k] < minStraight) return false;
            }

            for (int k = 0; k < index.Count; k++)
            {
                var piece = lap[index[k]];
                piece.length = lengths[k];
                lap[index[k]] = piece;
            }
            return true;
        }

        // Every segment gives a little. A piece's displacement is proportional to its own length once the
        // headings are fixed, so closure is linear in all of them at once and the least-norm answer spreads
        // the correction over the whole lap rather than dumping it on one segment.
        static bool CloseByEverything(IList<Piece> lap, float minLength)
        {
            int n = lap.Count;
            if (n == 0) return false;

            var dirs = new Vector2[n];
            Vector2 walked = Vector2.zero;
            float heading = 0f;

            for (int i = 0; i < n; i++)
            {
                Vector2 d = Displacement(lap[i], heading);
                walked += d;
                dirs[i] = lap[i].length > 1e-4f ? d / lap[i].length : Vector2.zero;
                heading += lap[i].angle;
            }
            Vector2 residual = -walked;

            float axx = 0f, axy = 0f, ayy = 0f;
            foreach (var d in dirs) { axx += d.x * d.x; axy += d.x * d.y; ayy += d.y * d.y; }
            float det = axx * ayy - axy * axy;
            if (Mathf.Abs(det) < 1e-9f) return false;

            float lx = (ayy * residual.x - axy * residual.y) / det;
            float ly = (-axy * residual.x + axx * residual.y) / det;

            var lengths = new float[n];
            for (int i = 0; i < n; i++)
            {
                lengths[i] = lap[i].length + dirs[i].x * lx + dirs[i].y * ly;
                if (lengths[i] < minLength) return false;
            }

            for (int i = 0; i < n; i++)
            {
                var piece = lap[i];
                piece.length = lengths[i];
                lap[i] = piece;
            }
            return true;
        }

        // Bring a lap to a target length. Uniform, so a closed lap stays closed.
        public static void Rescale(IList<Piece> lap, float targetLength)
        {
            float total = TotalLength(lap);
            if (total < 1f || targetLength < 1f) return;

            float k = targetLength / total;
            for (int i = 0; i < lap.Count; i++)
            {
                var piece = lap[i];
                piece.length *= k;
                lap[i] = piece;
            }
        }
    }
}
