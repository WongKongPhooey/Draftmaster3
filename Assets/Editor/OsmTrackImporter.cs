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

        var latLon = new List<OsmTrackGeometry.LatLon>();
        foreach (var n in trace.geometry) latLon.Add(new OsmTrackGeometry.LatLon(n.lat, n.lon));

        var points = OsmTrackGeometry.Project(latLon);
        MakeCounterClockwise(points);

        var readings = new StringBuilder();
        var lap = ReadShape(points, settings, out _, readings);
        if (lap.Count < 3) return $"{trackId,-18} the trace didn't resolve into segments";

        StartAtLongestStraight(lap);

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

        // The pit lane is attached to the main line BY SEGMENT INDEX, and the new line has different
        // segments in a different order. Held as a fraction of the lap it survives the swap: pit road still
        // leaves and rejoins where it did on the road, rather than wherever segment 3 happens to be now.
        float oldLap = v2.TotalLength();
        float entryFraction = oldLap > 1f ? v2.pitEntryDistance / oldLap : 0f;
        float exitFraction = oldLap > 1f ? v2.pitExitDistance / oldLap : 0f;

        v2.segments = Build(lap, v2, known, row);
        if (v2.hasPitLane && oldLap > 1f)
        {
            PinPitTo(v2, entryFraction * LapGeometry.TotalLength(lap), true);
            PinPitTo(v2, exitFraction * LapGeometry.TotalLength(lap), false);
        }
        v2.RebakePitDistances();
        EditorUtility.SetDirty(v2);

        int turns = 0;
        var shape = new StringBuilder();
        foreach (var p in lap)
        {
            if (IsCorner(p, LapGeometry.TotalLength(lap))) turns++;
            if (shape.Length > 0) shape.Append(" + ");
            shape.Append(p.isTurn ? $"T{p.angle:0}deg/{p.length:0}m" : $"S{p.length:0}m");
        }

        // The shape is the whole point of importing a trace, so print it: a lap that comes back as four
        // pieces when the circuit is a rounded triangle has gone wrong in a way no length check catches.
        return $"{trackId,-18} {lap.Count} segments ({turns} corners), lap {LapGeometry.TotalLength(lap):0}m " +
               $"(traced {trace.tracedMetres:0}m, published {trace.publishedMiles:0.###}mi, " +
               $"read at {readings}), " +
               (closed ? $"closed {gapBefore:0.#}m -> {gapAfter:0.##}m" : $"COULD NOT CLOSE ({gapBefore:0.#}m gap)") +
               "\n" + $"{"",-18} {shape}" +
               "\n" + $"{"",-18} readings:{readings}";
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
        };
    }

    // The cars run the way the circuit does, and a traced ring runs whichever way the mapper drew it. Signed
    // area says which: positive is counter-clockwise, which is a left-hand lap and a positive turn angle.
    static void MakeCounterClockwise(List<Vector2> points)
    {
        double twiceArea = 0;
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 a = points[i], b = points[(i + 1) % points.Count];
            twiceArea += (double)a.x * b.y - (double)b.x * a.y;
        }
        if (twiceArea < 0) points.Reverse();
    }

    // A traced way starts wherever somebody began clicking. Rotating the lap to start at the longest straight
    // puts segment zero on the front stretch, which is where a start/finish line lives and where every other
    // track asset in the project begins.
    //
    // "Straight" here means anything that is not a corner, shallow bends included — a D-shaped oval can
    // come back with no dead-straight piece in it at all, and its front stretch is still a front stretch.
    static void StartAtLongestStraight(List<LapGeometry.Piece> lap)
    {
        int best = -1;
        float longest = 0f;
        float lapMetres = LapGeometry.TotalLength(lap);
        for (int i = 0; i < lap.Count; i++)
        {
            if (IsCorner(lap[i], lapMetres)) continue;
            if (lap[i].length > longest) { longest = lap[i].length; best = i; }
        }

        if (best <= 0) return;
        var rotated = new List<LapGeometry.Piece>(lap.Count);
        for (int i = 0; i < lap.Count; i++) rotated.Add(lap[(best + i) % lap.Count]);
        lap.Clear();
        lap.AddRange(rotated);
    }

    // Banking, width and speeds are not in a trace and never will be, so they come from the published table.
    static TrackInfoV2.TrackSegment[] Build(List<LapGeometry.Piece> lap, TrackInfoV2 v2,
                                            bool known, TrackDimensionRow row)
    {
        float width = known && row.widthMetres > 1f ? row.widthMetres : v2.defaultWidth;
        float outer = Mathf.Max(1f, width * 0.5f - 1.6f);
        float turnBank = known ? row.turnBankingDeg : 0f;
        float straightBank = known ? row.straightBankingDeg : 0f;
        int topSpeed = v2.topSpeed > 20 ? v2.topSpeed : 180;
        float lapMetres = LapGeometry.TotalLength(lap);

        var segments = new TrackInfoV2.TrackSegment[lap.Count];
        int turnNumber = 1;

        for (int i = 0; i < lap.Count; i++)
        {
            var piece = lap[i];
            bool cornering = IsCorner(piece, lapMetres);
            var seg = new TrackInfoV2.TrackSegment
            {
                type = piece.isTurn ? TrackInfoV2.SegmentType.Turn : TrackInfoV2.SegmentType.Straight,
                length = piece.length,
                angle = piece.isTurn ? piece.angle : 0f,
                banking = cornering ? turnBank : straightBank,
                width = width,
                label = cornering ? $"Turn {turnNumber++}" : piece.isTurn ? "Bend" : "Straight",
                // What SplineDriver brakes for. A traced lap has real radii, so the corner speed comes
                // straight out of them; a shallow kink is a straight that bends and should not be braked
                // for at all, which on a tri-oval front stretch is the difference between a race and a
                // concertina at 190mph.
                maxSpeed = cornering ? OvalGeometry.CornerSpeedMph(piece.length, piece.angle, turnBank)
                                     : topSpeed,
            };

            if (cornering)
            {
                // Out-in-out, the same shape every generated corner uses: wide in, tight at the apex, wide
                // out again, with the AI's two extremes pinned at the edges of the usable road.
                seg.leadIn = seg.leadOut = Mathf.Clamp(piece.length * 0.35f, 10f, 90f);
                seg.racingLine = new TrackInfoV2.SegmentRacingLine
                {
                    idealEntry = outer * 0.80f, idealApex = -outer * 0.65f, idealExit = outer * 0.60f,
                    leftEntry = -outer, leftApex = -outer, leftExit = -outer,
                    rightEntry = outer, rightApex = outer, rightExit = outer,
                };
            }
            else
            {
                // A straight, or a bend shallow enough to be one. Giving a 700m six-degree bow the
                // out-in-out line of a corner would have the field weaving down it for no reason.
                seg.racingLine = new TrackInfoV2.SegmentRacingLine { leftApex = -outer, rightApex = outer };
            }
            segments[i] = seg;
        }
        return segments;
    }
}
