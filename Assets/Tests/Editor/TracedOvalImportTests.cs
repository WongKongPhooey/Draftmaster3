using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Draftmaster.Tracks;
using NUnit.Framework;
using UnityEngine;

// A committed wall/raceway trace (Assets/TrackTraces/<id>.json, segmentation "curvature", starting on the
// start/finish line) read the way Draftmaster > Tracks > Import Traced Geometry reads it, without Unity:
// project, cut into constant-curvature runs, close, rescale to the published lap. Then two questions the
// import itself only answers in the Editor:
//
// - does the built lap still lie on the trace? (fitted by rotation, translation and scale, as
//   Tools/track_reference.py fits the exported centreline: <= 4 m RMS is its "matches" verdict), and
// - does the mapped pit lane still fit as pit road (arc - straight - arc, PitChordFit.TryFitToLine)?
//
// So a trace can be written and checked in a lane that has no Editor, and the import that follows is known to
// land before anybody opens Unity. Daytona is here as the known-good case: imported, exported and measured at
// 1.5 m RMS with a 731 m pit road; this reading gives 1.9 m and the same pit road. Charlotte (traced
// 2026-11-18 from OSM's raceway ring, track_reference.py candidate 0) reads 1.0 m with an 820 m pit road.
//
// Engine-free (Draftmaster.Tracks Core only, no JsonUtility), so it also runs under Tools/HeadlessTests.
public class TracedOvalImportTests
{
    const float MaxClosureShareOfLap = 0.03f;   // OsmTrackImporter.MaxClosureShareOfLap
    const float MatchesRmsMetres = 4f;          // track_reference.py: "matches"

    struct Reading
    {
        public List<Vector2> trace;      // projected, counter-clockwise, node 0 on the start/finish line
        public List<Vector2> pitLane;    // same plane
        public bool reversed;
        public List<LapGeometry.Piece> lap;
        public float gapBefore, lapBefore;
        public bool closed;
    }

    [TestCase("Charlotte")]
    [TestCase("Daytona")]
    public void TraceImportsAsAClosedLapOfThePublishedLength(string id)
    {
        var r = Read(id);
        Assert.IsTrue(r.closed, $"{id}: the closure solve failed");
        Assert.Less(r.gapBefore, r.lapBefore * MaxClosureShareOfLap,
            $"{id}: the trace reads as a lap that misses itself by {r.gapBefore:0} m - the importer would refuse it");
        Assert.IsTrue(TrackDimensions.TryGet(id, out var row));
        Assert.AreEqual(row.LapMetres, LapGeometry.TotalLength(r.lap), 1f);
        Assert.AreEqual(360f, LapGeometry.TotalAngle(r.lap), 1f, "a counter-clockwise lap turns once, left");
    }

    [TestCase("Charlotte")]
    [TestCase("Daytona")]
    public void BuiltLapLiesOnTheTrace(string id)
    {
        var r = Read(id);
        var built = Walk(r.lap, 2f);
        float rms = FittedRms(built, r.trace, out float scale);
        TestContext.WriteLine($"{id}: built lap vs trace {rms:0.00} m RMS (scale x{scale:0.0000})");
        Assert.LessOrEqual(rms, MatchesRmsMetres, $"{id}: built lap is {rms:0.0} m RMS off its own trace");
    }

    [TestCase("Charlotte")]
    [TestCase("Daytona")]
    public void MappedPitLaneFitsAsPitRoad(string id)
    {
        var r = Read(id);
        Assume.That(r.pitLane.Count >= 2, $"{id}: no mapped pit lane in the trace");
        Assert.IsFalse(r.reversed, $"{id}: a reversed trace skips the mapped-pit fit in the importer");

        // OsmTrackImporter.FitMappedPit, on the lap as read (it starts on the start/finish line, so the line is
        // at 0 rather than at startFinishDistance after the seam moves to the longest straight - same road).
        var lap = r.lap;
        float lapLength = LapGeometry.TotalLength(lap);
        PitChordFit.Pose PoseAt(float d) => Pose(lap, Mathf.Repeat(d, lapLength));

        var trace = r.trace;
        int n = trace.Count;
        var along = new float[n];
        for (int i = 1; i < n; i++) along[i] = along[i - 1] + Vector2.Distance(trace[i - 1], trace[i]);
        float traceLap = along[n - 1] + Vector2.Distance(trace[n - 1], trace[0]);
        float scale = lapLength / traceLap;
        Vector2 TraceAt(float s)
        {
            s = Mathf.Repeat(s, traceLap);
            int i = System.Array.BinarySearch(along, s);
            if (i < 0) i = ~i - 1;
            i = Mathf.Clamp(i, 0, n - 1);
            int j = (i + 1) % n;
            float len = (j == 0 ? traceLap : along[j]) - along[i];
            return Vector2.Lerp(trace[i], trace[j], len > 1e-4f ? (s - along[i]) / len : 0f);
        }

        var from = new List<Vector2>();
        var to = new List<Vector2>();
        for (float s = -800f; s <= 800f; s += 10f)
        {
            from.Add(TraceAt(s) * scale);
            to.Add(PoseAt(s * scale).position);
        }
        Vector2 meanFrom = Vector2.zero, meanTo = Vector2.zero;
        for (int i = 0; i < from.Count; i++) { meanFrom += from[i]; meanTo += to[i]; }
        meanFrom /= from.Count;
        meanTo /= to.Count;
        float sxx = 0f, sxy = 0f;
        for (int i = 0; i < from.Count; i++)
        {
            Vector2 a = from[i] - meanFrom, b = to[i] - meanTo;
            sxx += a.x * b.x + a.y * b.y;
            sxy += a.x * b.y - a.y * b.x;
        }
        float angle = Mathf.Atan2(sxy, sxx), cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
        Vector2 Map(Vector2 p)
        {
            Vector2 a = p * scale - meanFrom;
            return new Vector2(a.x * cos - a.y * sin, a.x * sin + a.y * cos) + meanTo;
        }
        float misfit = 0f;
        for (int i = 0; i < from.Count; i++) misfit = Mathf.Max(misfit, Vector2.Distance(Map(from[i] / scale), to[i]));
        Assert.Less(misfit, 5f, $"{id}: the trace only lays over the built front stretch to {misfit:0.0} m");

        Vector2 first = Map(r.pitLane[0]), last = Map(r.pitLane[r.pitLane.Count - 1]);
        float Nearest(Vector2 q)
        {
            float best = 0f, bestSq = float.MaxValue;
            for (float d = 0f; d < lapLength; d += 2f)
            {
                float sq = (PoseAt(d).position - q).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = d; }
            }
            return best;
        }
        var radii = new[] { 250f, 200f, 160f, 120f, 90f, 60f, 40f, 30f, 25f };
        Assert.IsTrue(PitChordFit.TryFitToLine(PoseAt, lapLength, 1f, first, last - first, Nearest(first), Nearest(last),
                                               400f, radii, out var road),
                      $"{id}: the mapped pit lane would not fit - the importer would leave the old pit lane");
        TestContext.WriteLine($"{id}: pit road {road.Length:0} m (arcs r{road.radius:0}, straight {road.straightLength:0} m), " +
                              $"trace laid over the lap to {misfit:0.0} m");
        Assert.Greater(road.straightLength, 0.7f * Vector2.Distance(first, last),
                       $"{id}: pit road's straight is much shorter than the mapped lane");
    }

    // ------------------------------------------------------------------------------------------ the reading

    static Reading Read(string id)
    {
        string text = File.ReadAllText(FindTrace(id));
        StringAssert.Contains("\"curvature\"", text, $"{id}: not a curvature trace");
        StringAssert.Contains("\"firstNode\"", text, $"{id}: trace does not start on the start/finish line");

        var main = Nodes(text, "geometry");
        var pit = Nodes(text, "pitLane");
        var all = new List<OsmTrackGeometry.LatLon>(main);
        all.AddRange(pit);
        var projected = OsmTrackGeometry.Project(all);
        var r = new Reading
        {
            trace = projected.GetRange(0, main.Count),
            pitLane = projected.GetRange(main.Count, pit.Count),
        };

        double twiceArea = 0;
        for (int i = 0; i < r.trace.Count; i++)
        {
            Vector2 a = r.trace[i], b = r.trace[(i + 1) % r.trace.Count];
            twiceArea += (double)a.x * b.y - (double)b.x * a.y;
        }
        r.reversed = twiceArea < 0;
        if (r.reversed) r.trace.Reverse();
        // The trace repeats its first node to close; the importer's resample drops it, and so does the fit here.
        if (Vector2.Distance(r.trace[0], r.trace[r.trace.Count - 1]) < 0.5f) r.trace.RemoveAt(r.trace.Count - 1);

        float traced = 0f;
        for (int i = 0; i < r.trace.Count; i++) traced += Vector2.Distance(r.trace[i], r.trace[(i + 1) % r.trace.Count]);
        r.lap = OsmTrackGeometry.SegmentByCurvature(r.trace, windowMetres: Mathf.Min(120f, traced * 0.03f));
        LapGeometry.NormaliseTurnAngles(r.lap);
        r.gapBefore = LapGeometry.ClosureGap(r.lap);
        r.lapBefore = LapGeometry.TotalLength(r.lap);
        r.closed = LapGeometry.Close(r.lap);
        Assert.IsTrue(TrackDimensions.TryGet(id, out var row), $"{id}: not in TrackDimensions");
        LapGeometry.Rescale(r.lap, row.LapMetres);
        return r;
    }

    static string FindTrace(string id)
    {
        string rel = Path.Combine("Assets", "TrackTraces", id + ".json");
        foreach (string start in new[] { Directory.GetCurrentDirectory(), TestContext.CurrentContext.TestDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            {
                string path = Path.Combine(dir.FullName, rel);
                if (File.Exists(path)) return path;
            }
        }
        Assert.Fail($"no {rel} above {Directory.GetCurrentDirectory()}");
        return null;
    }

    // The nodes of one array in a trace file: objects of lat/lon, no nested arrays, so the first ']' closes it.
    static List<OsmTrackGeometry.LatLon> Nodes(string json, string key)
    {
        var list = new List<OsmTrackGeometry.LatLon>();
        int at = json.IndexOf("\"" + key + "\"", System.StringComparison.Ordinal);
        if (at < 0) return list;
        int open = json.IndexOf('[', at), close = json.IndexOf(']', open);
        var rx = new Regex("\"lat\"\\s*:\\s*(-?[0-9.eE+-]+)\\s*,\\s*\"lon\"\\s*:\\s*(-?[0-9.eE+-]+)");
        foreach (Match m in rx.Matches(json.Substring(open, close - open)))
            list.Add(new OsmTrackGeometry.LatLon(double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                                                 double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)));
        return list;
    }

    // ------------------------------------------------------------------------------------------ geometry

    // Where the lap is `d` metres in, and which way it points (TrackInfoV2.SampleAuthoredSpline's walk).
    static PitChordFit.Pose Pose(List<LapGeometry.Piece> lap, float d)
    {
        Vector2 pos = Vector2.zero;
        float heading = 0f, left = d;
        for (int i = 0; i < lap.Count; i++)
        {
            var p = lap[i];
            float run = Mathf.Min(left, p.length);
            var part = p.isTurn && p.length > 1e-4f ? new LapGeometry.Piece(true, run, p.angle * run / p.length)
                                                    : new LapGeometry.Piece(false, run, 0f);
            pos += LapGeometry.Displacement(part, heading);
            heading += part.angle;
            left -= run;
            if (left <= 0f) break;
        }
        return new PitChordFit.Pose(pos, heading);
    }

    static List<Vector2> Walk(List<LapGeometry.Piece> lap, float step)
    {
        var pts = new List<Vector2>();
        Vector2 at = Vector2.zero;
        float heading = 0f;
        foreach (var piece in lap)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(piece.length / step));
            var part = new LapGeometry.Piece(piece.isTurn, piece.length / steps, piece.isTurn ? piece.angle / steps : 0f);
            for (int i = 0; i < steps; i++)
            {
                pts.Add(at);
                at += LapGeometry.Displacement(part, heading);
                heading += part.angle;
            }
        }
        return pts;
    }

    static List<Vector2> ResampleClosed(List<Vector2> pts, int count)
    {
        var closed = new List<Vector2>(pts) { pts[0] };
        var res = OsmTrackGeometry.Resample(closed, 1f);
        float total = 0f;
        for (int i = 1; i < res.Count; i++) total += Vector2.Distance(res[i - 1], res[i]);
        var outPts = new List<Vector2>(count);
        int k = 0;
        float walked = 0f;
        for (int c = 0; c < count; c++)
        {
            float target = total * c / count;
            while (k < res.Count - 2 && walked + Vector2.Distance(res[k], res[k + 1]) < target)
            {
                walked += Vector2.Distance(res[k], res[k + 1]);
                k++;
            }
            float seg = Vector2.Distance(res[k], res[k + 1]);
            outPts.Add(Vector2.Lerp(res[k], res[k + 1], seg > 1e-4f ? (target - walked) / seg : 0f));
        }
        return outPts;
    }

    // Both laps start on the start/finish line and run the same way, so they pair by share of the lap for a first
    // similarity fit (Umeyama); then, as track_reference.py does, iterative closest point: re-pair each built point
    // with its nearest trace point and fit again, so a corner a few metres along from where the trace has it is
    // not counted twice. The answer is each built point's distance to the trace.
    static float FittedRms(List<Vector2> built, List<Vector2> trace, out float scale)
    {
        const int N = 720;
        var a = ResampleClosed(built, N);
        var pairs = ResampleClosed(trace, N);
        var dense = ResampleClosed(trace, Mathf.Max(400, Mathf.CeilToInt(Length(trace))));
        Fit(a, pairs, out scale, out float angle, out Vector2 ma, out Vector2 mb);
        float rms = 0f;
        for (int pass = 0; pass < 12; pass++)
        {
            double sum = 0;
            for (int i = 0; i < N; i++)
            {
                Vector2 moved = Apply(a[i], scale, angle, ma, mb);
                float best = float.MaxValue;
                foreach (var q in dense)
                {
                    float d = (q - moved).sqrMagnitude;
                    if (d < best) { best = d; pairs[i] = q; }
                }
                sum += best;
            }
            rms = Mathf.Sqrt((float)(sum / N));
            Fit(a, pairs, out scale, out angle, out ma, out mb);
        }
        return rms;
    }

    static void Fit(List<Vector2> a, List<Vector2> b, out float scale, out float angle, out Vector2 ma, out Vector2 mb)
    {
        int n = a.Count;
        ma = Vector2.zero;
        mb = Vector2.zero;
        for (int i = 0; i < n; i++) { ma += a[i]; mb += b[i]; }
        ma /= n;
        mb /= n;
        double sxx = 0, sxy = 0, varA = 0;
        for (int i = 0; i < n; i++)
        {
            Vector2 p = a[i] - ma, q = b[i] - mb;
            sxx += p.x * q.x + p.y * q.y;
            sxy += p.x * q.y - p.y * q.x;
            varA += p.sqrMagnitude;
        }
        angle = (float)System.Math.Atan2(sxy, sxx);
        scale = (float)(System.Math.Sqrt(sxx * sxx + sxy * sxy) / varA);
    }

    static Vector2 Apply(Vector2 p, float scale, float angle, Vector2 ma, Vector2 mb)
    {
        Vector2 c = p - ma;
        float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
        return new Vector2(c.x * cos - c.y * sin, c.x * sin + c.y * cos) * scale + mb;
    }

    static float Length(List<Vector2> pts)
    {
        float total = 0f;
        for (int i = 0; i < pts.Count; i++) total += Vector2.Distance(pts[i], pts[(i + 1) % pts.Count]);
        return total;
    }
}
