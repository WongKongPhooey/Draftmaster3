using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Tracks
{
    // Turning a centreline traced off OpenStreetMap into the straights and corners the spline system wants.
    //
    // Why bother, when every venue publishes its dimensions? Because a published figure does not pin down a
    // shape. "A 1,551 ft back stretch on a 1.022 mile lap" is satisfied by a long thin oval and by a rounded
    // triangle alike, and Phoenix — which is the second — was generated as the first for exactly that reason:
    // its corners came out 34m tighter than the real ones and its straights 92m too long. A trace does not
    // have that problem. It says where the road actually goes.
    //
    // The method is curvature. Walk the traced line at even spacing, measure how fast the heading turns per
    // metre, and cut the lap where that crosses a threshold: the flat parts are straights, the rest are
    // corners. Two details matter and both are about a mapper's clicks rather than the circuit:
    //
    //   RESAMPLING. Nodes are placed by hand, densely round corners and sparsely down straights, so raw
    //   spacing measures the mapper and not the road. Everything is resampled to an even step first.
    //
    //   SMOOTHING. One node a metre out of line reads as a sharp corner. The curvature is averaged over a
    //   short window so a wobble does not saw a straight into three.
    //
    // What comes out is close but never exact — a traced lap misses its own start by a few metres and its
    // heading by a few degrees. LapGeometry finishes the job.
    public static class OsmTrackGeometry
    {
        public struct LatLon
        {
            public double lat, lon;
            public LatLon(double lat, double lon) { this.lat = lat; this.lon = lon; }
        }

        [System.Serializable]
        public class Settings
        {
            [Tooltip("Even spacing (m) the trace is resampled to before curvature is measured.")]
            public float resampleMetres = 4f;
            [Tooltip("Degrees of heading change per metre above which the road counts as cornering. Lower " +
                     "finds gentler bends (a tri-oval kink); higher keeps only the real corners.")]
            public float turnThresholdDegPerMetre = 0.09f;
            [Tooltip("How many samples the curvature is averaged over. Bigger tolerates a sloppier trace.")]
            public int smoothWindow = 7;
            [Tooltip("Passes of gentle smoothing over the traced points themselves before any heading is " +
                     "measured. This is what absorbs a mapper's hand: a node a metre off the true line is a " +
                     "30 degree kink over a 4m step, and no amount of averaging the curvature afterwards " +
                     "recovers a straight from that.")]
            public int pointSmoothPasses = 2;
            [Tooltip("Pieces shorter than this (m) are folded into their neighbour rather than kept as a " +
                     "segment of their own.")]
            public float minPieceMetres = 25f;
        }

        // Lat/lon onto a local metric plane, centred on the track. Equirectangular: at the size of a
        // motor circuit the error against a proper projection is millimetres.
        public static List<Vector2> Project(IList<LatLon> geometry)
        {
            var pts = new List<Vector2>();
            if (geometry == null || geometry.Count == 0) return pts;

            double lat0 = 0, lon0 = 0;
            foreach (var g in geometry) { lat0 += g.lat; lon0 += g.lon; }
            lat0 /= geometry.Count; lon0 /= geometry.Count;

            const double R = 6378137.0;
            double k = System.Math.Cos(lat0 * Mathf.Deg2Rad);
            foreach (var g in geometry)
                pts.Add(new Vector2((float)((g.lon - lon0) * Mathf.Deg2Rad * R * k),
                                    (float)((g.lat - lat0) * Mathf.Deg2Rad * R)));
            return pts;
        }

        // Even spacing, so curvature measures the road rather than how densely somebody clicked.
        public static List<Vector2> Resample(IList<Vector2> pts, float step)
        {
            var outPts = new List<Vector2>();
            if (pts == null || pts.Count < 2 || step <= 0.01f) return outPts;

            outPts.Add(pts[0]);
            float carry = 0f;
            for (int i = 1; i < pts.Count; i++)
            {
                Vector2 a = pts[i - 1], b = pts[i];
                float seg = Vector2.Distance(a, b);
                if (seg < 1e-6f) continue;

                float t = step - carry;
                while (t <= seg)
                {
                    outPts.Add(Vector2.Lerp(a, b, t / seg));
                    t += step;
                }
                carry = seg - (t - step);
            }
            return outPts;
        }

        // The lap as straights and corners. `points` should be the closed traced ring, in metres.
        public static List<LapGeometry.Piece> Segment(IList<Vector2> points, Settings settings = null)
        {
            settings ??= new Settings();
            var lap = new List<LapGeometry.Piece>();

            var pts = Resample(points, settings.resampleMetres);
            if (pts.Count < 8) return lap;
            SmoothRing(pts, settings.pointSmoothPasses);

            // Headings and step lengths along the resampled ring.
            int n = pts.Count - 1;
            var step = new float[n];
            var heading = new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 d = pts[i + 1] - pts[i];
                step[i] = d.magnitude;
                heading[i] = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            }
            Unwrap(heading);

            // Degrees turned per metre, then smoothed so one stray node is not a corner.
            var curvature = new float[n - 1];
            for (int i = 0; i < n - 1; i++)
                curvature[i] = (heading[i + 1] - heading[i]) / Mathf.Max(step[i + 1], 1e-4f);
            Smooth(curvature, Mathf.Max(1, settings.smoothWindow));

            // Cut into runs of "turning" and "not turning".
            for (int i = 0; i < curvature.Length; i++)
            {
                bool turning = Mathf.Abs(curvature[i]) > settings.turnThresholdDegPerMetre;
                float len = step[i + 1];
                float ang = heading[i + 1] - heading[i];

                if (lap.Count > 0 && lap[lap.Count - 1].isTurn == turning)
                {
                    var last = lap[lap.Count - 1];
                    last.length += len;
                    last.angle += ang;
                    lap[lap.Count - 1] = last;
                }
                else
                {
                    lap.Add(new LapGeometry.Piece(turning, len, ang));
                }
            }

            MergeSlivers(lap, settings.minPieceMetres);
            JoinTheSeam(lap);
            for (int i = 0; i < lap.Count; i++)
            {
                var piece = lap[i];
                if (!piece.isTurn) piece.angle = 0f;    // a straight's leftover wobble belongs to nobody
                lap[i] = piece;
            }
            return lap;
        }

        // A trace has short bursts of noise in it — a couple of metres reading as a corner in the middle of a
        // straight. Anything too short to be a real piece is absorbed by whatever it sits next to, angle and
        // all, so no heading change is thrown away.
        static void MergeSlivers(List<LapGeometry.Piece> lap, float minMetres)
        {
            bool merged = true;
            while (merged && lap.Count > 2)
            {
                merged = false;
                for (int i = 0; i < lap.Count; i++)
                {
                    if (lap[i].length >= minMetres) continue;

                    int into = i > 0 ? i - 1 : (lap.Count > 1 ? 1 : -1);
                    if (into < 0) break;

                    var host = lap[into];
                    host.length += lap[i].length;
                    host.angle += lap[i].angle;
                    lap[into] = host;
                    lap.RemoveAt(i);
                    merged = true;
                    break;
                }
            }

            // Neighbours of the same kind can now be adjacent; fold them together.
            for (int i = lap.Count - 1; i > 0; i--)
            {
                if (lap[i].isTurn != lap[i - 1].isTurn) continue;
                var host = lap[i - 1];
                host.length += lap[i].length;
                host.angle += lap[i].angle;
                lap[i - 1] = host;
                lap.RemoveAt(i);
            }
        }

        // A lap has no start, but a traced way does, and it lands wherever the mapper began. A straight the
        // seam falls in the middle of would otherwise come back as two — one at each end of the list — which
        // is a corner-count nobody can check and a segment nobody authored.
        static void JoinTheSeam(List<LapGeometry.Piece> lap)
        {
            if (lap.Count < 3) return;
            if (lap[0].isTurn != lap[lap.Count - 1].isTurn) return;

            var first = lap[0];
            var last = lap[lap.Count - 1];
            first.length += last.length;
            first.angle += last.angle;
            lap[0] = first;
            lap.RemoveAt(lap.Count - 1);
        }

        // Gentle moving average over the points, wrapping at the seam because the trace is a ring. Corners
        // are pulled in by a few centimetres, which costs nothing; per-node jitter, which would otherwise
        // read as cornering, is gone.
        static void SmoothRing(List<Vector2> pts, int passes)
        {
            int n = pts.Count;
            if (n < 5 || passes <= 0) return;

            for (int pass = 0; pass < passes; pass++)
            {
                var src = pts.ToArray();
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = src[(i - 1 + n) % n], b = src[i], c = src[(i + 1) % n];
                    pts[i] = a * 0.25f + b * 0.5f + c * 0.25f;
                }
            }
        }

        static void Unwrap(float[] degrees)
        {
            for (int i = 1; i < degrees.Length; i++)
            {
                float d = Mathf.DeltaAngle(degrees[i - 1], degrees[i]);
                degrees[i] = degrees[i - 1] + d;
            }
        }

        static void Smooth(float[] values, int window)
        {
            if (window <= 1 || values.Length == 0) return;
            var src = (float[])values.Clone();
            int half = window / 2;
            for (int i = 0; i < values.Length; i++)
            {
                float sum = 0f;
                int count = 0;
                for (int k = i - half; k <= i + half; k++)
                {
                    if (k < 0 || k >= src.Length) continue;
                    sum += src[k]; count++;
                }
                values[i] = count > 0 ? sum / count : src[i];
            }
        }
    }
}
