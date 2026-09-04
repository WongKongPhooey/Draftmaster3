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
        // Below this a run that reads as straight really is straight, and its leftover heading is a
        // mapper's hand rather than the road.
        const float StraightWobbleDegrees = 2f;

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
            [Tooltip("Pick the cornering threshold from the trace itself rather than using the fixed one. " +
                     "A bullring and a superspeedway do not corner at the same rate, and a single number " +
                     "that suits one eats the other's straights.")]
            public bool adaptiveThreshold = true;
            [Tooltip("Multiplies whichever threshold is in force. Below 1 finds gentler bends — the bow in " +
                     "a D-shaped oval's back straight — at the cost of cutting the lap into more pieces.")]
            public float thresholdScale = 1f;
            [Tooltip("Longest corner, in degrees, before it is cut into several arcs. 0 keeps every corner " +
                     "as one arc of constant radius. A real corner opens and tightens down its length, and " +
                     "one arc cannot say so — Michigan's ends, read as single arcs, leave the lap 353m " +
                     "open. The importer tries several values and keeps whichever describes the trace best.")]
            public float maxTurnDegrees = 0f;
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
            float lapMetres = 0f;
            for (int i = 0; i < n; i++) lapMetres += step[i];
            float threshold = (settings.adaptiveThreshold
                               ? ChooseThreshold(lapMetres, settings.turnThresholdDegPerMetre)
                               : settings.turnThresholdDegPerMetre)
                            * Mathf.Max(0.05f, settings.thresholdScale);

            int runStart = 0;
            bool runTurning = Mathf.Abs(curvature[0]) > threshold;
            for (int i = 1; i <= curvature.Length; i++)
            {
                bool turning = i < curvature.Length && Mathf.Abs(curvature[i]) > threshold;
                if (i < curvature.Length && turning == runTurning) continue;

                AddRun(lap, runStart, i - 1, runTurning, step, heading, settings);
                runStart = i;
                runTurning = turning;
            }

            MergeSlivers(lap, settings.minPieceMetres);
            JoinTheSeam(lap);
            // What to do with the heading a "straight" accumulated.
            //
            // A metre or two of it is a mapper's hand and belongs to nobody, so it is dropped. Several
            // degrees is not noise — it is a straight that BOWS, which is most of what makes a D-shaped
            // oval a D. Michigan's back stretch bends about five degrees, and throwing that away leaves the
            // lap 353m open, an error the closure solve then has to take out of the corner radii. So a run
            // that bends that much is handed back as the shallow turn it is; the importer gives a corner of
            // a few degrees the top speed of a straight anyway.
            for (int i = 0; i < lap.Count; i++)
            {
                var piece = lap[i];
                if (!piece.isTurn)
                {
                    if (Mathf.Abs(piece.angle) >= StraightWobbleDegrees) piece.isTurn = true;
                    else piece.angle = 0f;
                }
                lap[i] = piece;
            }
            return lap;
        }

        // One run of samples, turning or not, becomes one piece — or, for a long corner, several.
        //
        // A real corner is rarely one radius. Michigan's ends open out, and described as single arcs the
        // lap misses its own start by 353m: the model cannot say what the road does, and the closure solve
        // then spreads that error over every segment. Cutting a long corner into shorter arcs lets each one
        // fit the radius it actually has — which is also how a circuit is numbered, since a NASCAR oval has
        // four turns rather than two.
        //
        // Off by default, because a corner that IS one radius should stay one piece. The importer decides,
        // by reading the trace several ways and keeping whichever joins up.
        static void AddRun(List<LapGeometry.Piece> lap, int from, int to, bool turning,
                           float[] step, float[] heading, Settings settings)
        {
            if (to < from) return;

            float length = 0f;
            for (int i = from; i <= to; i++) length += step[i + 1];
            float angle = heading[to + 1] - heading[from];

            int chunks = 1;
            if (turning && settings.maxTurnDegrees > 1f)
                chunks = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(angle) / settings.maxTurnDegrees), 1, 12);

            if (chunks == 1 || length < settings.minPieceMetres * chunks)
            {
                lap.Add(new LapGeometry.Piece(turning, length, angle));
                return;
            }

            // Cut by ANGLE rather than by distance, so each arc covers the same amount of turning and a
            // tightening corner gives a short arc where it is tight and a long one where it is open.
            float per = angle / chunks;
            int cutFrom = from;
            for (int c = 1; c <= chunks; c++)
            {
                float wanted = per * c;
                int cutTo = to;
                for (int i = cutFrom; i <= to; i++)
                {
                    if (Mathf.Abs(heading[i + 1] - heading[from]) >= Mathf.Abs(wanted)) { cutTo = i; break; }
                }
                if (c == chunks) cutTo = to;

                float chunkLength = 0f;
                for (int i = cutFrom; i <= cutTo; i++) chunkLength += step[i + 1];
                lap.Add(new LapGeometry.Piece(true, chunkLength, heading[cutTo + 1] - heading[cutFrom]));

                cutFrom = cutTo + 1;
                if (cutFrom > to) break;
            }
        }

        // Where cornering starts, taken from the circuit rather than assumed.
        //
        // One fixed figure cannot serve every venue, because cornering rate scales with the size of the
        // place. Bristol's ends turn at 0.7 degrees per metre and Michigan's D at 0.3, so a threshold low
        // enough to catch Michigan reads most of Bristol's lap as corner: its 61m straights came back as
        // 14m of themselves.
        //
        // What every closed lap has in common is that it turns through 360 degrees, so 360/lap is the rate
        // an average metre of THIS circuit turns at, whatever its size. Half of that separates the two
        // populations well: a corner turns several times faster than the lap average and a straight far
        // slower. It stays inside a band around the authored figure so that a trace which is all noise, or
        // a circuit with no straights in it, cannot produce a meaningless threshold.
        static float ChooseThreshold(float lapMetres, float fallback)
        {
            if (lapMetres < 50f) return fallback;
            return Mathf.Clamp(0.5f * 360f / lapMetres, fallback * 0.5f, fallback * 6f);
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
