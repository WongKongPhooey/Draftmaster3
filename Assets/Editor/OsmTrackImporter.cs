using System.Collections.Generic;
using System.IO;
using System.Text;
using Draftmaster.Tracks;
using UnityEditor;
using UnityEngine;

// Building a track's main line from a centreline traced off OpenStreetMap.
//
// The spline system has always had to invent a shape. OvalGeometry solves one from the published lap length
// and a guessed share of it spent cornering, which is enough for a plain oval and wrong for everything else:
// Phoenix came out with corners 34m tighter than the real ones and straights 92m too long, because a
// published "1,551 ft back stretch" is equally true of a long thin oval and of the rounded triangle Phoenix
// actually is. A trace does not have to be guessed at. It says where the road goes.
//
// What this does NOT take from the trace is anything a trace cannot know: banking, road width and pit speed
// still come from TrackDimensions, and the pit lane, materials and start position already on the asset are
// left alone. The import replaces the main line and nothing else.
//
// Traces live in Assets/TrackTraces as plain JSON, fetched once and committed, so an import is reproducible
// and needs no network. They carry their own attribution: the data is (c) OpenStreetMap contributors under
// the ODbL, which is worth settling deliberately before any of this ships.
public static class OsmTrackImporter
{
    const string TraceFolder = "Assets/TrackTraces";

    // The most of its own lap a reading may be out by and still be believed.
    const float MaxClosureShareOfLap = 0.03f;

    // A superspeedway's ideal lane, as a share of the half-width the AI may use, toward the inside: low-middle,
    // leaving the bottom lane below it and the middle and top above.
    const float SuperspeedwayLaneShare = 0.4f;

    // Below this a piece is a bend in a straight rather than a corner: it is banked like a straight, taken
    // flat, and given a straight's racing line. Two tests, because either alone gets a venue wrong — a
    // 6 degree kink is a bend however tight it is, and Michigan's front stretch bends 48 degrees over
    // nearly a kilometre, which is a bend too.
    const float ShallowKinkDegrees = 12f;

    // ...and the second test: a corner turns several times faster than the lap does on average. 360/lap is
    // that average for this circuit whatever its size, the same measure the segmenter cuts on.
    static bool IsCorner(LapGeometry.Piece piece, float lapMetres)
    {
        if (!piece.isTurn || Mathf.Abs(piece.angle) < ShallowKinkDegrees) return false;
        float rate = Mathf.Abs(piece.angle) / Mathf.Max(1f, piece.length);
        return rate >= 0.5f * 360f / Mathf.Max(1f, lapMetres);
    }

    // Which pieces are cornering, judged a corner at a time. A trace read as constant-curvature runs cuts a corner
    // into several pieces, most of them well under ShallowKinkDegrees each, so asking each piece "are you a
    // corner" calls the whole of Daytona's Turn 1 a string of bends. Instead: a run of consecutive pieces each
    // turning the same way at a cornering rate is one corner if, together, it turns more than a kink does. A lap
    // read one arc per corner gives exactly what IsCorner did, piece by piece.
    static bool[] CornerMask(IList<LapGeometry.Piece> lap)
    {
        int n = lap.Count;
        var mask = new bool[n];
        if (n == 0) return mask;
        float lapMetres = LapGeometry.TotalLength(lap);
        float rate = 0.5f * 360f / Mathf.Max(1f, lapMetres);
        bool Turning(int k) => lap[k].isTurn && Mathf.Abs(lap[k].angle) / Mathf.Max(1f, lap[k].length) >= rate;

        // The lap is a ring: a corner the start/finish line falls in (Daytona's tri-oval) is one corner, not two
        // halves at opposite ends of the list. Walk from a piece no run continues into.
        int from = RunBoundary(n, k => Turning(k) && Turning((k - 1 + n) % n)
                                       && Mathf.Sign(lap[k].angle) == Mathf.Sign(lap[(k - 1 + n) % n].angle));
        int walked = 0;
        while (walked < n)
        {
            int i = (from + walked) % n;
            if (!Turning(i)) { walked++; continue; }
            var run = new List<int>();
            float angle = 0f;
            while (walked < n)
            {
                int k = (from + walked) % n;
                if (!Turning(k) || Mathf.Sign(lap[k].angle) != Mathf.Sign(lap[i].angle)) break;
                run.Add(k);
                angle += lap[k].angle;
                walked++;
            }
            bool corner = Mathf.Abs(angle) >= ShallowKinkDegrees;
            foreach (int k in run) mask[k] = corner;
        }
        return mask;
    }

    // The first index that does not continue the run before it (0 if every piece does, e.g. a circle).
    static int RunBoundary(int n, System.Func<int, bool> continuesPrevious)
    {
        for (int k = 0; k < n; k++) if (!continuesPrevious(k)) return k;
        return 0;
    }

    [System.Serializable] class Node { public double lat; public double lon; }
    [System.Serializable]
    class Trace
    {
        public string trackId;
        public string osmName;          // how the circuit was identified, for a human reading the file
        public string foundBy;          // which query found it: a venue with a hand-fixed trace says so
        public float publishedMiles;
        public float tracedMetres;
        public string attribution;
        public string startFinish;      // "firstNode": the trace starts on the start/finish line (trace_from_wall.py)
        public string segmentation;     // "curvature": a precise trace, read as runs of constant curvature
        public Node[] pitLane;          // the mapped pit lane, in the direction cars use it (trace_from_wall.py --pit)
        public Node[] geometry;
    }

    [MenuItem("Draftmaster/Tracks/Import Traced Geometry (selected asset)", priority = 420)]
    public static void ImportSelected()
    {
        var v2 = Selection.activeObject as TrackInfoV2;
        if (v2 == null) { Debug.LogError("Select a TrackInfoV2 in Resources/Tracks first."); return; }
        Debug.Log(Import(v2.name, v2), v2);
    }

    [MenuItem("Draftmaster/Tracks/Import Traced Geometry For Every Trace", priority = 421)]
    public static void ImportAll()
    {
        if (!Directory.Exists(TraceFolder)) { Debug.LogError($"No {TraceFolder} to import from."); return; }

        var report = new StringBuilder();
        int done = 0, skipped = 0;
        foreach (string file in Directory.GetFiles(TraceFolder, "*.json"))
        {
            string id = Path.GetFileNameWithoutExtension(file);
            var v2 = AssetDatabase.LoadAssetAtPath<TrackInfoV2>($"Assets/Resources/Tracks/{id}.asset");
            if (v2 == null) { report.AppendLine($"{id,-18} no track asset to import into"); skipped++; continue; }

            string line = Import(id, v2).TrimEnd();
            report.AppendLine(line);
            if (line.Contains("refused") || line.Contains("hand-measured")) skipped++; else done++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"Traced geometry imported into {done} track(s), {skipped} skipped.\n{report}");
    }

    public static string Import(string trackId, TrackInfoV2 v2, OsmTrackGeometry.Settings settings = null)
    {
        string path = Path.Combine(TraceFolder, trackId + ".json");
        if (!File.Exists(path)) return $"{trackId,-18} no trace at {path}";

        if (trackId == RoadCourseLayouts.HandAuthored)
            return $"{trackId,-18} hand-measured off satellite imagery; a trace does not improve on it";

        var trace = JsonUtility.FromJson<Trace>(File.ReadAllText(path));
        if (trace?.geometry == null || trace.geometry.Length < 10) return $"{trackId,-18} trace is empty";

        // The lap and its mapped pit lane are projected together, so they share one plane.
        var latLon = new List<OsmTrackGeometry.LatLon>();
        foreach (var n in trace.geometry) latLon.Add(new OsmTrackGeometry.LatLon(n.lat, n.lon));
        int mainCount = latLon.Count;
        if (trace.pitLane != null) foreach (var n in trace.pitLane) latLon.Add(new OsmTrackGeometry.LatLon(n.lat, n.lon));

        var projected = OsmTrackGeometry.Project(latLon);
        var points = projected.GetRange(0, mainCount);
        var pitPoints = projected.GetRange(mainCount, projected.Count - mainCount);
        bool reversed = MakeCounterClockwise(points);

        // A trace that starts on the start/finish line keeps that start: the lap is not rotated, and the piece
        // the line falls in stays cut in two so segment 0 begins exactly there.
        bool startsOnLine = trace.startFinish == "firstNode";
        if (startsOnLine)
        {
            settings = settings == null ? new OsmTrackGeometry.Settings() : Clone(settings);
            settings.keepSeam = true;
        }

        // A precise trace (a wall offset, not a mapper's raceway line) is followed closely: pieces of constant
        // curvature, as many as the road needs, rather than one arc per corner.
        var readings = new StringBuilder();
        List<LapGeometry.Piece> lap;
        if (trace.segmentation == "curvature")
        {
            lap = OsmTrackGeometry.SegmentByCurvature(points);
            LapGeometry.NormaliseTurnAngles(lap);
            readings.Append("constant-curvature runs");
        }
        else lap = ReadShape(points, settings, out _, readings);
        if (lap.Count < 3) return $"{trackId,-18} the trace didn't resolve into segments";

        if (!startsOnLine) StartAtLongestStraight(lap);

        // A traced lap is close but never exact: a few degrees of heading and a few metres of position, all
        // of it from resampling a hand-drawn line. Tidy the heading first, then shut the loop, then bring it
        // to the published length — in that order, because scaling a closed lap keeps it closed and scaling
        // an open one does not close it.
        float gapBefore = LapGeometry.ClosureGap(lap);
        bool closed = LapGeometry.Close(lap);
        float gapAfter = LapGeometry.ClosureGap(lap);

        // How much of the lap the closure solve had to move is the honest measure of whether this reading
        // describes the circuit at all. A good one is a few metres out; a bad one is hundreds, and the
        // solve then buys closure by moving corner radii and straight lengths that were measured off the
        // real thing. Past a few per cent the traced shape is worse than the generated or hand-authored
        // one it would replace, so it is refused and the asset left alone.
        float lapBefore = LapGeometry.TotalLength(lap);
        if (!closed || gapBefore > lapBefore * MaxClosureShareOfLap)
            return $"{trackId,-18} refused: the trace reads as a lap that misses itself by {gapBefore:0}m " +
                   $"({100f * gapBefore / Mathf.Max(1f, lapBefore):0.#}% of the lap). Keeping what was there.";

        bool known = TrackDimensions.TryGet(trackId, out TrackDimensionRow row);
        float targetLap = known && row.lapMiles > 0.01f ? row.lapMiles * 1609.344f : LapGeometry.TotalLength(lap);
        LapGeometry.Rescale(lap, targetLap);

        // A trace that starts on the start/finish line still has its lap seam moved to the longest straight, and
        // the line kept where it is with startFinishDistance. The seam is where the sampled road closes on itself,
        // and closed mid-corner (Daytona's line is at the tri-oval apex) it read to the AI as a hairpin: the grip
        // governor braked from 165 mph to a crawl there every lap. On a straight it is harmless.
        float startFinishAt = startsOnLine ? StartAtLongestStraight(lap) : 0f;

        // The pit lane is attached to the main line BY SEGMENT INDEX, and the new line has different
        // segments in a different order. Held as a fraction of the lap it survives the swap: pit road still
        // leaves and rejoins where it did on the road, rather than wherever segment 3 happens to be now.
        float oldLap = v2.TotalLength();
        float entryFraction = oldLap > 1f ? v2.pitEntryDistance / oldLap : 0f;
        float exitFraction = oldLap > 1f ? v2.pitExitDistance / oldLap : 0f;

        v2.segments = Build(lap, v2, known, row, startFinishAt);
        if (known && row.widthMetres > 1f) v2.defaultWidth = row.widthMetres;
        if (startsOnLine) v2.startFinishDistance = startFinishAt;

        // A track that cuts pit road across its infield (Daytona) has it re-fitted to the traced shape: the
        // formula's chord was built from the formula's own pieces and no longer meets this road. Everything
        // else keeps its pit lane where it was, as a share of the lap.
        string pitNote = "";
        // A mapped pit lane wins: pit road is laid along it. Without one, a track that cuts pit road across its
        // infield gets a chord fitted to the lap.
        if (v2.hasPitLane && known && startsOnLine && !reversed && pitPoints.Count >= 2)
            pitNote = FitMappedPit(v2, row, points, pitPoints);
        else if (v2.hasPitLane && known && row.pitChordInsetMetres > 0f)
            pitNote = FitChordPit(v2, row);
        else if (v2.hasPitLane && oldLap > 1f)
        {
            PinPitTo(v2, entryFraction * LapGeometry.TotalLength(lap), true);
            PinPitTo(v2, exitFraction * LapGeometry.TotalLength(lap), false);
        }
        v2.RebakePitDistances();
        EditorUtility.SetDirty(v2);

        int turns = 0;
        var shape = new StringBuilder();
        var cornerMask = CornerMask(lap);
        for (int i = 0; i < lap.Count; i++)
        {
            var p = lap[i];
            // A corner read as several pieces is still one corner, including one the start line falls in.
            int prev = (i - 1 + lap.Count) % lap.Count;
            bool continues = cornerMask[prev] && Mathf.Sign(lap[prev].angle) == Mathf.Sign(p.angle);
            if (cornerMask[i] && !continues) turns++;
            if (shape.Length > 0) shape.Append(" + ");
            shape.Append(p.isTurn ? $"T{p.angle:0}deg/{p.length:0}m" : $"S{p.length:0}m");
        }

        // The shape is the whole point of importing a trace, so print it: a lap that comes back as four
        // pieces when the circuit is a rounded triangle has gone wrong in a way no length check catches.
        return $"{trackId,-18} {lap.Count} segments ({turns} corners), lap {LapGeometry.TotalLength(lap):0}m " +
               $"(traced {trace.tracedMetres:0}m, published {trace.publishedMiles:0.###}mi, " +
               $"read at {readings}), " +
               (closed ? $"closed {gapBefore:0.#}m -> {gapAfter:0.##}m" : $"COULD NOT CLOSE ({gapBefore:0.#}m gap)") +
               pitNote +
               "\n" + $"{"",-18} {shape}" +
               "\n" + $"{"",-18} readings:{readings}";
    }

    // Pit road straight across the infield, fitted to the traced lap (PitChordFit): it leaves the racing line
    // before the start/finish line, crosses the inside with its straight held pitChordInsetMetres in from the
    // racing line, and rejoins after. Its arcs are half the corners' radius, as the formula's chord was.
    static string FitChordPit(TrackInfoV2 v2, TrackDimensionRow row)
    {
        float lap = v2.TotalLength();
        // The corners' mean radius, weighted by length: total cornering distance over total cornering angle, so a
        // corner read as several pieces counts once, not once per piece.
        var pieces = new List<LapGeometry.Piece>();
        foreach (var seg in v2.segments)
            pieces.Add(new LapGeometry.Piece(seg.type == TrackInfoV2.SegmentType.Turn, seg.length, seg.angle));
        var corner = CornerMask(pieces);
        float cornerLength = 0f, cornerAngle = 0f;
        for (int i = 0; i < pieces.Count; i++)
        {
            if (!corner[i]) continue;
            cornerLength += pieces[i].length;
            cornerAngle += Mathf.Abs(pieces[i].angle) * Mathf.Deg2Rad;
        }
        if (cornerAngle < 0.1f) return "; no corners to size a chord pit road from - pit lane left alone";
        float radius = cornerLength / cornerAngle * OvalGeometry.ChordArcRadiusShare;

        System.Func<float, PitChordFit.Pose> poseAt = d =>
        {
            v2.SampleAuthoredSpline(d, out Vector2 p, out float h);
            return new PitChordFit.Pose(p, h);
        };
        // Counter-clockwise (MakeCounterClockwise), so the infield is on the left.
        if (!PitChordFit.TryFit(poseAt, lap, v2.startFinishDistance, 1f, radius, row.pitChordInsetMetres, out var road))
            return "; NO chord pit road fits this lap - pit lane left alone";

        ApplyPitRoad(v2, row, road);
        return $"; chord pit road {road.Length:0}m (arcs r{radius:0}m, {road.entryTurnDeg:0.#}/{road.exitTurnDeg:0.#}deg, " +
               $"straight {road.straightLength:0}m, {road.nearestClearance:0}m from the racing line)";
    }

    // Pit road along the mapped pit lane. The trace and the built lap are not the same plane - the lap is rebuilt
    // from its pieces, closed and rescaled - so the two are first laid over each other along the front stretch
    // (the trace starts on the start/finish line, the lap has it at startFinishDistance; matching points
    // 800 m either side give the rotation and offset), then the mapped lane is carried across and pit road is
    // fitted to it: its straight on the lane's line, an arc onto it from the racing line and one back off.
    static string FitMappedPit(TrackInfoV2 v2, TrackDimensionRow row, List<Vector2> trace, List<Vector2> pitLane)
    {
        int n = trace.Count;
        var along = new float[n];
        for (int i = 1; i < n; i++) along[i] = along[i - 1] + Vector2.Distance(trace[i - 1], trace[i]);
        float traceLap = along[n - 1] + Vector2.Distance(trace[n - 1], trace[0]);
        float lap = v2.TotalLength();
        float scale = lap / Mathf.Max(1f, traceLap);

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

        // Matched pairs round the start/finish line, then the best rotation + offset between them.
        var from = new List<Vector2>();
        var to = new List<Vector2>();
        for (float s = -800f; s <= 800f; s += 10f)
        {
            from.Add(TraceAt(s) * scale);
            v2.SampleAuthoredSpline(Mathf.Repeat(v2.startFinishDistance + s * scale, lap), out Vector2 p, out _);
            to.Add(p);
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
        float angle = Mathf.Atan2(sxy, sxx);
        float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
        Vector2 Map(Vector2 p)
        {
            Vector2 a = p * scale - meanFrom;
            return new Vector2(a.x * cos - a.y * sin, a.x * sin + a.y * cos) + meanTo;
        }
        float misfit = 0f;
        for (int i = 0; i < from.Count; i++) misfit = Mathf.Max(misfit, Vector2.Distance(Map(from[i] / scale), to[i]));

        // The lane's line: its ends, carried across.
        Vector2 first = Map(pitLane[0]), last = Map(pitLane[pitLane.Count - 1]);
        System.Func<float, PitChordFit.Pose> poseAt = d =>
        {
            v2.SampleAuthoredSpline(d, out Vector2 p, out float h);
            return new PitChordFit.Pose(p, h);
        };
        float Nearest(Vector2 q)
        {
            float best = 0f, bestSq = float.MaxValue;
            for (float d = 0f; d < lap; d += 2f)
            {
                float sq = (poseAt(d).position - q).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = d; }
            }
            return best;
        }

        var radii = new[] { 250f, 200f, 160f, 120f, 90f, 60f, 40f };
        if (!PitChordFit.TryFitToLine(poseAt, lap, 1f, first, last - first, Nearest(first), Nearest(last), 400f,
                                      radii, out var road))
            return $"; mapped pit lane would not fit (trace laid over the lap to {misfit:0.#} m) - pit lane left alone";

        ApplyPitRoad(v2, row, road);
        return $"; pit road on the mapped pit lane {road.Length:0}m (arcs r{road.radius:0}m, " +
               $"{road.entryTurnDeg:0.#}/{road.exitTurnDeg:0.#}deg, straight {road.straightLength:0}m; trace laid over " +
               $"the lap to {misfit:0.#} m)";
    }

    static void ApplyPitRoad(TrackInfoV2 v2, TrackDimensionRow row, PitChordFit.Road road)
    {
        float width = v2.pitDefaultWidth > 0f ? v2.pitDefaultWidth : row.pitWidthMetres;
        int limit = v2.pitSpeedLimit > 0 ? v2.pitSpeedLimit : row.pitSpeedLimitMph;
        TrackInfoV2.TrackSegment Piece(string label, float length, float angle) => new TrackInfoV2.TrackSegment
        {
            label = label,
            type = Mathf.Approximately(angle, 0f) ? TrackInfoV2.SegmentType.Straight : TrackInfoV2.SegmentType.Turn,
            length = length,
            angle = angle,
            maxSpeed = limit,
            width = width,
        };
        v2.pitSegments = new[]
        {
            Piece("Pit Entry", road.EntryArc, road.entryTurnDeg),
            Piece("Pit Road", road.straightLength, 0f),
            Piece("Pit Exit", road.ExitArc, road.exitTurnDeg),
        };
        v2.pitStartHeadingOffset = 0f;
        PinPitTo(v2, road.entryDistance, true);
        PinPitTo(v2, road.exitDistance, false);
        // The limit ends where the straight does: the exit arc is the run back up onto the banking.
        v2.pitExitLineDistance = road.EntryArc + road.straightLength;
    }

    // Point the pit entry (or exit) at whatever segment now holds that distance around the lap, with the
    // leftover carried in the offset so it lands on the same piece of road as before.
    static void PinPitTo(TrackInfoV2 v2, float distance, bool entry)
    {
        float at = 0f;
        for (int i = 0; i < v2.segments.Length; i++)
        {
            float end = at + v2.segments[i].length;
            if (distance <= end || i == v2.segments.Length - 1)
            {
                if (entry) { v2.pitEntrySegmentIndex = i; v2.pitEntryOffset = distance - end; }
                else       { v2.pitExitSegmentIndex = i;  v2.pitExitOffset = distance - end; }
                return;
            }
            at = end;
        }
    }

    // Read the trace at several sensitivities and keep whichever reading joins up best.
    //
    // How gentle a bend counts as cornering is not knowable in advance. Michigan is the case in point: its
    // back straight bows, and read as dead straight the lap misses its own start by 269m — an error the
    // closure solve then has to spread over every segment, moving the corner radii by 8%. Read a little
    // more sensitively, the bow comes back as the shallow turn it is and the lap nearly closes on its own.
    //
    // The gap before closing is the honest measure of how well a reading describes the trace, so try a few
    // and keep the best. Fewer pieces breaks a tie, because an extra segment for a metre of closure is a
    // worse description of a circuit, not a better one.
    static List<LapGeometry.Piece> ReadShape(List<Vector2> points, OsmTrackGeometry.Settings settings,
                                             out float chosenScale, StringBuilder log = null)
    {
        var scales = new[] { 1f, 0.7f, 0.5f, 1.5f };
        var arcs = new[] { 0f, 90f, 60f, 45f };
        List<LapGeometry.Piece> best = null;
        float bestGap = float.MaxValue;
        chosenScale = 1f;
        string chosen = "1x";

        foreach (float scale in scales)
        foreach (float arc in arcs)
        {
            var attempt = settings == null ? new OsmTrackGeometry.Settings() : Clone(settings);
            attempt.thresholdScale = scale;
            attempt.maxTurnDegrees = arc;

            var lap = OsmTrackGeometry.Segment(points, attempt);
            if (lap.Count < 3) continue;

            LapGeometry.NormaliseTurnAngles(lap);
            float gap = LapGeometry.ClosureGap(lap);

            // A metre of closure is not worth an extra segment: a circuit described in more pieces than it
            // has corners is a worse description, however neatly the arithmetic lands.
            if (gap < bestGap - 1f || (gap < bestGap + 1f && best != null && lap.Count < best.Count))
            {
                best = lap;
                bestGap = gap;
                chosenScale = scale;
                chosen = arc > 1f ? $"{scale:0.##}x in {arc:0}deg arcs" : $"{scale:0.##}x";
            }
        }
        log?.Append(chosen);
        return best ?? new List<LapGeometry.Piece>();
    }

    static OsmTrackGeometry.Settings Clone(OsmTrackGeometry.Settings from)
    {
        return new OsmTrackGeometry.Settings
        {
            resampleMetres = from.resampleMetres,
            turnThresholdDegPerMetre = from.turnThresholdDegPerMetre,
            smoothWindow = from.smoothWindow,
            pointSmoothPasses = from.pointSmoothPasses,
            minPieceMetres = from.minPieceMetres,
            adaptiveThreshold = from.adaptiveThreshold,
            thresholdScale = from.thresholdScale,
            maxTurnDegrees = from.maxTurnDegrees,
            keepSeam = from.keepSeam,
        };
    }

    // The cars run the way the circuit does, and a traced ring runs whichever way the mapper drew it. Signed
    // area says which: positive is counter-clockwise, which is a left-hand lap and a positive turn angle.
    static bool MakeCounterClockwise(List<Vector2> points)
    {
        double twiceArea = 0;
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 a = points[i], b = points[(i + 1) % points.Count];
            twiceArea += (double)a.x * b.y - (double)b.x * a.y;
        }
        if (twiceArea < 0) points.Reverse();
        return twiceArea < 0;
    }

    // A traced way starts wherever somebody began clicking. Rotating the lap to start at the longest straight
    // puts segment zero on the front stretch, which is where a start/finish line lives and where every other
    // track asset in the project begins.
    //
    // "Straight" here means anything that is not a corner, shallow bends included — a D-shaped oval can
    // come back with no dead-straight piece in it at all, and its front stretch is still a front stretch.
    // Returns how far round the old start the new one is, so a start/finish line that was at the old start
    // can be found again (TrackInfoV2.startFinishDistance).
    static float StartAtLongestStraight(List<LapGeometry.Piece> lap)
    {
        int best = -1;
        float longest = 0f;
        float lapMetres = LapGeometry.TotalLength(lap);
        for (int i = 0; i < lap.Count; i++)
        {
            if (IsCorner(lap[i], lapMetres)) continue;
            if (lap[i].length > longest) { longest = lap[i].length; best = i; }
        }

        if (best <= 0) return 0f;
        float oldStartAt = 0f;
        for (int i = best; i < lap.Count; i++) oldStartAt += lap[i].length;
        var rotated = new List<LapGeometry.Piece>(lap.Count);
        for (int i = 0; i < lap.Count; i++) rotated.Add(lap[(best + i) % lap.Count]);
        lap.Clear();
        lap.AddRange(rotated);
        return oldStartAt;
    }

    // Banking, width and speeds are not in a trace and never will be, so they come from the published table.
    static TrackInfoV2.TrackSegment[] Build(List<LapGeometry.Piece> lap, TrackInfoV2 v2,
                                            bool known, TrackDimensionRow row, float startFinish = 0f)
    {
        float width = known && row.widthMetres > 1f ? row.widthMetres : v2.defaultWidth;
        float outer = Mathf.Max(1f, width * 0.5f - 1.6f);
        float turnBank = known ? row.turnBankingDeg : 0f;
        float straightBank = known ? row.straightBankingDeg : 0f;
        int topSpeed = v2.topSpeed > 20 ? v2.topSpeed : 180;
        float lapMetres = LapGeometry.TotalLength(lap);

        var segments = new TrackInfoV2.TrackSegment[lap.Count];

        // A corner is a run of consecutive cornering pieces turning the same way. A trace read as constant-
        // curvature runs (SegmentByCurvature) has several per corner, and each one taking its own out-in-out line
        // would weave the field through every turn; so the line is laid over the whole corner instead, and each
        // piece takes its share of it. A one-piece corner gets exactly the line it always did.
        int n = lap.Count;
        var cornering = CornerMask(lap);
        bool Joins(int k) => cornering[k] && cornering[(k - 1 + n) % n] && Mathf.Sign(lap[k].angle) == Mathf.Sign(lap[(k - 1 + n) % n].angle);

        // Each corner as its pieces in order, wrapping the start/finish line (so the tri-oval is one corner).
        var groupOf = new List<int>[n];
        int from = RunBoundary(n, Joins);
        for (int w = 0; w < n; w++)
        {
            int k = (from + w) % n;
            if (!cornering[k]) continue;
            groupOf[k] = Joins(k) && w > 0 ? groupOf[(k - 1 + n) % n] : new List<int>();
            groupOf[k].Add(k);
        }

        // Out-in-out across a corner, f = 0 at its start and 1 at its end: wide in, tight at the apex, wide out.
        //
        // Not on a superspeedway. There the turns are flat out and the field races in lanes - bottom, middle,
        // top - that it holds all the way round, so the ideal line is a lane: one lateral, low-middle, the whole
        // lap. The out-in-out swing had every car at Daytona drift to the wall 90 m before Turn 3, snap back at
        // turn-in and, with a car alongside or a tactical offset on top, slide off at the entry - every off in
        // the 40-car pack sim was there.
        bool holdLane = known && row.kind == TrackKind.Superspeedway;
        float lane = -outer * SuperspeedwayLaneShare;
        float Line(float f) => holdLane ? lane
                             : f < 0.5f ? Mathf.Lerp(outer * 0.80f, -outer * 0.65f, f / 0.5f)
                                        : Mathf.Lerp(-outer * 0.65f, outer * 0.60f, (f - 0.5f) / 0.5f);

        // Corners numbered round the lap from the start/finish line; one the line falls in is the last.
        var pieceStart = new float[n];
        for (int i = 1; i < n; i++) pieceStart[i] = pieceStart[i - 1] + lap[i - 1].length;
        var groups = new List<List<int>>();
        for (int i = 0; i < n; i++)
            if (cornering[i] && groupOf[i][0] == i) groups.Add(groupOf[i]);
        groups.Sort((a, b) => Mathf.Repeat(pieceStart[a[0]] - startFinish, lapMetres)
                              .CompareTo(Mathf.Repeat(pieceStart[b[0]] - startFinish, lapMetres)));
        var turnNumbers = new Dictionary<List<int>, int>();
        foreach (var g in groups) turnNumbers[g] = turnNumbers.Count + 1;

        for (int i = 0; i < lap.Count; i++)
        {
            var piece = lap[i];
            bool corner = cornering[i];
            int turnNumber = corner ? turnNumbers[groupOf[i]] : 0;
            var seg = new TrackInfoV2.TrackSegment
            {
                type = piece.isTurn ? TrackInfoV2.SegmentType.Turn : TrackInfoV2.SegmentType.Straight,
                length = piece.length,
                angle = piece.isTurn ? piece.angle : 0f,
                banking = corner ? turnBank : straightBank,
                width = width,
                label = corner ? $"Turn {turnNumber}" : piece.isTurn ? "Bend" : "Straight",
                // What SplineDriver brakes for. A traced lap has real radii, so the corner speed comes
                // straight out of them; a shallow kink is a straight that bends and should not be braked
                // for at all, which on a tri-oval front stretch is the difference between a race and a
                // concertina at 190mph.
                maxSpeed = corner ? OvalGeometry.CornerSpeedMph(piece.length, piece.angle, turnBank)
                                  : topSpeed,
            };

            if (corner)
            {
                // Where this piece sits in its corner, as shares of the corner's length.
                var group = groupOf[i];
                float groupLength = 0f, before = 0f;
                bool reached = false;
                foreach (int k in group)
                {
                    if (k == i) reached = true;
                    if (!reached) before += lap[k].length;
                    groupLength += lap[k].length;
                }
                float f0 = before / groupLength, f1 = (before + piece.length) / groupLength;
                bool first = group[0] == i, last = group[group.Count - 1] == i;
                float lead = Mathf.Clamp(groupLength * 0.35f, 10f, 90f);
                seg.leadIn = first ? lead : 0f;
                seg.leadOut = last ? lead : 0f;
                seg.racingLine = new TrackInfoV2.SegmentRacingLine
                {
                    idealEntry = Line(f0), idealApex = Line((f0 + f1) * 0.5f), idealExit = Line(f1),
                    leftEntry = -outer, leftApex = -outer, leftExit = -outer,
                    rightEntry = outer, rightApex = outer, rightExit = outer,
                    // The corner's entry and exit are anchored once, at its ends; the pieces between carry
                    // only their apex point of the shared line.
                    skipEntry = !first,
                    skipExit = !last,
                };
            }
            else
            {
                // A straight, or a bend shallow enough to be one. Giving a 700m six-degree bow the
                // out-in-out line of a corner would have the field weaving down it for no reason.
                // Only the apex point: a bend is stored as a Turn, and a Turn's entry and exit points would
                // otherwise be added with zero-width bounds, pinching the road shut at both ends of every bend.
                seg.racingLine = new TrackInfoV2.SegmentRacingLine
                {
                    idealApex = holdLane ? lane : 0f,
                    leftApex = -outer, rightApex = outer, skipEntry = true, skipExit = true,
                };
            }
            segments[i] = seg;
        }
        return segments;
    }
}
