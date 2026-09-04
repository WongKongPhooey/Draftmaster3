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

    // Below this a corner is a bend in a straight rather than a corner, and is taken flat.
    const float ShallowKinkDegrees = 12f;

    [System.Serializable] class Node { public double lat; public double lon; }
    [System.Serializable]
    class Trace
    {
        public string trackId;
        public long osmWayId;
        public string osmName;
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

            report.AppendLine(Import(id, v2).TrimEnd());
            done++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"Traced geometry imported into {done} track(s), {skipped} skipped.\n{report}");
    }

    public static string Import(string trackId, TrackInfoV2 v2, OsmTrackGeometry.Settings settings = null)
    {
        string path = Path.Combine(TraceFolder, trackId + ".json");
        if (!File.Exists(path)) return $"{trackId,-18} no trace at {path}";

        var trace = JsonUtility.FromJson<Trace>(File.ReadAllText(path));
        if (trace?.geometry == null || trace.geometry.Length < 10) return $"{trackId,-18} trace is empty";

        var latLon = new List<OsmTrackGeometry.LatLon>();
        foreach (var n in trace.geometry) latLon.Add(new OsmTrackGeometry.LatLon(n.lat, n.lon));

        var points = OsmTrackGeometry.Project(latLon);
        MakeCounterClockwise(points);

        var lap = OsmTrackGeometry.Segment(points, settings);
        if (lap.Count < 3) return $"{trackId,-18} the trace didn't resolve into segments";

        StartAtLongestStraight(lap);

        // A traced lap is close but never exact: a few degrees of heading and a few metres of position, all
        // of it from resampling a hand-drawn line. Tidy the heading first, then shut the loop, then bring it
        // to the published length — in that order, because scaling a closed lap keeps it closed and scaling
        // an open one does not close it.
        LapGeometry.NormaliseTurnAngles(lap);
        float gapBefore = LapGeometry.ClosureGap(lap);
        bool closed = LapGeometry.Close(lap);
        float gapAfter = LapGeometry.ClosureGap(lap);

        bool known = TrackDimensions.TryGet(trackId, out TrackDimensionRow row);
        float targetLap = known && row.lapMiles > 0.01f ? row.lapMiles * 1609.344f : LapGeometry.TotalLength(lap);
        LapGeometry.Rescale(lap, targetLap);

        v2.segments = Build(lap, v2, known, row);
        v2.RebakePitDistances();
        EditorUtility.SetDirty(v2);

        int turns = 0;
        foreach (var p in lap) if (p.isTurn) turns++;
        return $"{trackId,-18} {lap.Count} segments ({turns} corners), lap {LapGeometry.TotalLength(lap):0}m " +
               $"(traced {trace.tracedMetres:0}m, published {trace.publishedMiles:0.###}mi), " +
               (closed ? $"closed {gapBefore:0.#}m -> {gapAfter:0.##}m" : $"COULD NOT CLOSE ({gapBefore:0.#}m gap)") +
               $", from OSM way {trace.osmWayId}";
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
    static void StartAtLongestStraight(List<LapGeometry.Piece> lap)
    {
        int best = -1;
        float longest = 0f;
        for (int i = 0; i < lap.Count; i++)
            if (!lap[i].isTurn && lap[i].length > longest) { longest = lap[i].length; best = i; }

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

        var segments = new TrackInfoV2.TrackSegment[lap.Count];
        int turnNumber = 1;

        for (int i = 0; i < lap.Count; i++)
        {
            var piece = lap[i];
            var seg = new TrackInfoV2.TrackSegment
            {
                type = piece.isTurn ? TrackInfoV2.SegmentType.Turn : TrackInfoV2.SegmentType.Straight,
                length = piece.length,
                angle = piece.isTurn ? piece.angle : 0f,
                banking = piece.isTurn ? turnBank : straightBank,
                width = width,
                label = piece.isTurn ? $"Turn {turnNumber++}" : "Straight",
                // What SplineDriver brakes for. A traced lap has real radii, so the corner speed comes
                // straight out of them; a shallow kink is a straight that bends and should not be braked
                // for at all, which on a tri-oval front stretch is the difference between a race and a
                // concertina at 190mph.
                maxSpeed = !piece.isTurn || Mathf.Abs(piece.angle) < ShallowKinkDegrees
                           ? topSpeed
                           : OvalGeometry.CornerSpeedMph(piece.length, piece.angle, turnBank),
            };

            if (piece.isTurn)
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
                seg.racingLine = new TrackInfoV2.SegmentRacingLine { leftApex = -outer, rightApex = outer };
            }
            segments[i] = seg;
        }
        return segments;
    }
}
