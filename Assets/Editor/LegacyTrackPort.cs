using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Reading the old hand-measured tracks so their geometry can be carried into the spline system.
//
// The legacy TrackInfo is a different description of a circuit from TrackInfoV2: it is a lap laid out as
// POSITIONS along a tape measure — turnPositions/turnLengths/straightLengths in metres from the start line,
// with per-turn angles, banking and steering — where TrackInfoV2 is an ordered list of segments each of
// which knows only its own length and angle. The two hold the same shape in different words, which is why a
// port is a translation rather than a copy.
//
// Phoenix is the one that matters. Every oval in the calendar is solved from its published lap length by
// OvalGeometry, which can only ever produce a symmetrical oval; Phoenix is not one — it has the dogleg on
// the back straight, and that was measured turn by turn years ago and has been sitting in git ever since.
//
// This dumps a legacy asset to JSON so the two can be compared honestly before anything is converted.
public static class LegacyTrackPort
{
    const string DumpFolder = "Logs";

    [MenuItem("Draftmaster/Tracks/Legacy/Dump Legacy Track To JSON", priority = 400)]
    public static void DumpSelected()
    {
        var info = Selection.activeObject as TrackInfo;
        if (info == null)
        {
            Debug.LogError("Select a legacy TrackInfo asset first (Assets/LegacyTrackData/...).");
            return;
        }
        Dump(info);
    }

    // Fixed entry point so the dump can be driven without a selection.
    [MenuItem("Draftmaster/Tracks/Legacy/Dump Phoenix", priority = 401)]
    public static void DumpPhoenix()
    {
        var info = AssetDatabase.LoadAssetAtPath<TrackInfo>("Assets/LegacyTrackData/PhoenixLegacy.asset");
        if (info == null)
        {
            Debug.LogError("No Assets/LegacyTrackData/PhoenixLegacy.asset to read.");
            return;
        }
        Dump(info);
    }

    // The other half of the comparison: what the spline system currently has for the same circuit, in its
    // own words, so the port can match its conventions (units, sign of a left-hand turn, where the racing
    // line offsets are measured from) rather than guess at them.
    [MenuItem("Draftmaster/Tracks/Legacy/Dump Current Track V2", priority = 402)]
    public static void DumpCurrentV2()
    {
        var v2 = Selection.activeObject as TrackInfoV2
                 ?? AssetDatabase.LoadAssetAtPath<TrackInfoV2>("Assets/Resources/Tracks/Phoenix.asset");
        if (v2 == null) { Debug.LogError("Select a TrackInfoV2, or have Resources/Tracks/Phoenix.asset."); return; }

        var sb = new StringBuilder();
        sb.AppendLine($"{v2.trackName}  lap {v2.TotalLength():0.#}m  defaultWidth {v2.defaultWidth}  " +
                      $"startHeading {v2.startHeading}  closedLoop {v2.closedLoop}  " +
                      $"startFinish {v2.startFinishDistance}  segments {(v2.segments?.Length ?? 0)}");

        float at = 0f;
        for (int i = 0; v2.segments != null && i < v2.segments.Length; i++)
        {
            var s = v2.segments[i];
            var r = s.racingLine;
            sb.AppendLine($"[{i}] {s.type,-8} '{s.label}' at {at,7:0.#} len {s.length,7:0.#} angle {s.angle,7:0.#} " +
                          $"bank {s.banking,5:0.#} lead {s.leadIn:0.#}/{s.leadOut:0.#} " +
                          $"maxSpeed {s.maxSpeed,4} width {s.width:0.#}");
            sb.AppendLine($"        ideal {r.idealEntry,6:0.##}/{r.idealApex,6:0.##}/{r.idealExit,6:0.##}  " +
                          $"left {r.leftEntry,6:0.##}/{r.leftApex,6:0.##}/{r.leftExit,6:0.##}  " +
                          $"right {r.rightEntry,6:0.##}/{r.rightApex,6:0.##}/{r.rightExit,6:0.##}");
            at += s.length;
        }

        Directory.CreateDirectory(DumpFolder);
        string path = Path.Combine(DumpFolder, $"current-{v2.name}.txt");
        File.WriteAllText(path, sb.ToString());
        Debug.Log($"Current V2 track '{v2.name}' dumped to {path}");
    }

    // ---------------------------------------------------------------- the port

    [MenuItem("Draftmaster/Tracks/Legacy/Port Phoenix Into The Spline System", priority = 410)]
    public static void PortPhoenix()
    {
        var legacy = AssetDatabase.LoadAssetAtPath<TrackInfo>("Assets/LegacyTrackData/PhoenixLegacy.asset");
        var v2 = AssetDatabase.LoadAssetAtPath<TrackInfoV2>("Assets/Resources/Tracks/Phoenix.asset");
        if (legacy == null) { Debug.LogError("No Assets/LegacyTrackData/PhoenixLegacy.asset to port from."); return; }
        if (v2 == null) { Debug.LogError("No Assets/Resources/Tracks/Phoenix.asset to port into."); return; }

        // Left as measured. The published stretch lengths imply the corners take 49% of the lap, and forcing
        // that produced a worse track, not a better one: the OSM trace of the real circuit puts the corners
        // at 58-60% and its corner angles at 41/166/144 degrees against the hand-measured 50/170/140. The
        // measurement was right and the published figures describe something else — most likely stretch
        // lengths taken along the outside wall rather than the racing surface.
        const float PhoenixTurnShare = 0f;
        const float PhoenixStraightBanking = 3f;

        string report = Port(legacy, v2, PhoenixTurnShare, PhoenixStraightBanking);
        EditorUtility.SetDirty(v2);
        AssetDatabase.SaveAssets();
        Debug.Log(report, v2);
    }

    // Rebuild a TrackInfoV2's main line from a hand-measured legacy track.
    //
    // Only the main line is replaced. Everything else the spline system generated for this circuit — the pit
    // lane, the materials, the start position, the edge lines — is left exactly as it was, because none of it
    // exists in the legacy description and regenerating it is not what a port is for. The pit entry and exit
    // are re-anchored afterwards so they stay at the same DISTANCE round the lap, since the segment they used
    // to be indexed against no longer exists.
    //
    // The legacy lap is a tape measure: turns and straights at absolute positions, each turn carrying its own
    // arc length and heading change. Walking those positions in order gives the alternating straight/turn
    // sequence TrackInfoV2 wants. Lengths are scaled to the lap length the spline system already uses (the
    // real published distance), so the hand-measured PROPORTIONS survive without moving the finish line or
    // invalidating the pit distances.
    // `turnShare` is how much of the lap should be spent cornering, 0 to leave it as measured. It is worth
    // insisting on, because it is the one thing the legacy data gets badly wrong and the one thing the
    // published dimensions get right.
    //
    // The measured CORNERS are excellent. Scale their arc lengths to the published turn total and they come
    // out at radii of 127/125/136m against the ~129m the published stretch lengths imply — near enough exact,
    // for three corners nobody ever drew in plan view. What the legacy lap gets wrong is only the SPLIT: it
    // spends 55% of the lap turning where the real circuit spends 49%, which is what pushed the ported radii
    // out to 141-154m.
    //
    // (The generated track has the opposite problem, and worse. TrackDimensions gives Phoenix a
    // turnShareOfLap of 0.36, so its corners come out at 95m radius — 34m tighter than the real ones — and
    // its straights 92m too long, which is what stretches it into a superspeedway shape rather than the
    // rounded triangle Phoenix actually is.)
    public static string Port(TrackInfo legacy, TrackInfoV2 v2, float turnShare = 0f, float straightBanking = 0f)
    {
        float legacyLap = Mathf.Max(1f, legacy.trackLength);
        float targetLap = v2.TotalLength() > 1f ? v2.TotalLength() : legacyLap;

        // How far off the centreline the outer racing line sits, in metres. Taken off the asset being ported
        // into rather than recomputed, so the ported lines sit exactly where every other track's do.
        float outer = OuterLineOffset(v2);

        var built = new System.Collections.Generic.List<TrackInfoV2.TrackSegment>();
        var order = WalkLap(legacy);

        foreach (var (isTurn, index, length) in order)
        {
            if (isTurn) built.Add(Turn(legacy, index, length, outer));
            else built.Add(Straight(length, outer, legacy.topSpeed));
        }

        // The measured lap does not close, and was never going to. The legacy system was one-dimensional —
        // a position along a tape measure — so nothing ever required the plan view to join up, and Phoenix's
        // comes back 88m short of where it started. Drawn as-is that is a road with a step in it.
        //
        // Only the straights are moved to fix it. The corner angles fix every heading round the lap, so each
        // turn contributes a FIXED displacement and each straight contributes its length along a FIXED
        // direction: closure is linear in the straight lengths, two equations against three unknowns, and the
        // answer taken is the one that moves the measured numbers least. The corners come through untouched,
        // which matters because they are the part that was actually measured and the part that makes this
        // circuit itself.
        // Put the corners on the published share of the lap before closing, so the straights are solved
        // around corners that are already the right size rather than dragged to fit the wrong ones.
        if (turnShare > 0.01f) SetTurnShare(built, turnShare);
        if (straightBanking != 0f) BankStraights(built, straightBanking);

        float gapBefore = ClosureGap(built);
        bool closed = CloseByStraights(built);
        float gapAfter = ClosureGap(built);

        // Scaling a closed loop keeps it closed, so the lap is only brought to length once it joins up.
        Rescale(built, targetLap);

        // Keep the pit lane where it physically is. Its indices pointed at the old segment list.
        float entryWas = v2.pitEntryDistance, exitWas = v2.pitExitDistance;
        v2.segments = built.ToArray();
        AnchorAtDistance(v2, entryWas, ref v2.pitEntrySegmentIndex, ref v2.pitEntryOffset);
        AnchorAtDistance(v2, exitWas, ref v2.pitExitSegmentIndex, ref v2.pitExitOffset);
        v2.RebakePitDistances();

        var sb = new StringBuilder();
        sb.AppendLine($"Ported {legacy.trackName} into the spline system: {built.Count} segments, " +
                      $"lap {v2.TotalLength():0.#}m (legacy {legacyLap:0}m), turns sum {TotalAngle(v2):0.#}deg.");
        sb.AppendLine(closed
            ? $"Lap closed by adjusting the straights: gap {gapBefore:0.#}m -> {gapAfter:0.##}m."
            : $"COULD NOT CLOSE the lap ({gapBefore:0.#}m gap): the straights cannot absorb it without one " +
              "of them going negative. The road will have a step in it.");
        sb.AppendLine($"Pit entry {entryWas:0.#}m -> {v2.pitEntryDistance:0.#}m, " +
                      $"exit {exitWas:0.#}m -> {v2.pitExitDistance:0.#}m.");
        return sb.ToString();
    }

    // The lap in order, as (isTurn, legacyIndex, length) — read off the absolute positions rather than
    // assuming straights and turns alternate, because a legacy track is free to not.
    static System.Collections.Generic.List<(bool, int, float)> WalkLap(TrackInfo legacy)
    {
        var pieces = new System.Collections.Generic.List<(float pos, bool isTurn, int index, float length)>();

        for (int i = 0; legacy.straightPositions != null && i < legacy.straightPositions.Length; i++)
            pieces.Add((legacy.straightPositions[i], false, i, Len(legacy.straightLengths, i)));
        for (int i = 0; legacy.turnPositions != null && i < legacy.turnPositions.Length; i++)
            pieces.Add((legacy.turnPositions[i], true, i, Len(legacy.turnLengths, i)));

        pieces.Sort((a, b) => a.pos.CompareTo(b.pos));

        var order = new System.Collections.Generic.List<(bool, int, float)>();
        foreach (var piece in pieces)
            if (piece.length > 0.01f) order.Add((piece.isTurn, piece.index, piece.length));
        return order;
    }

    static float Len(int[] a, int i) => a != null && i < a.Length ? a[i] : 0f;
    static float At(float[] a, int i) => a != null && i < a.Length ? a[i] : 0f;

    static TrackInfoV2.TrackSegment Turn(TrackInfo legacy, int i, float length, float outer)
    {
        // Legacy angles are magnitudes; the ovals all run counter-clockwise, and TrackInfoV2 reads positive
        // as a left turn, so a measured corner ports across as a positive angle.
        return new TrackInfoV2.TrackSegment
        {
            label = $"Turn {i + 1}",
            type = TrackInfoV2.SegmentType.Turn,
            length = length,
            angle = Mathf.Abs(Len(legacy.turnAngles, i)),
            banking = Len(legacy.bankingAngles, i),
            leadIn = Len(legacy.turnLeadIn, i),
            leadOut = Len(legacy.turnLeadOut, i),
            maxSpeed = (int)Len(legacy.turnMaxSpeeds, i),
            racingLine = Lines(legacy, i, outer),
        };
    }

    static TrackInfoV2.TrackSegment Straight(float length, float outer, int topSpeed)
    {
        // A legacy track holds no line data for its straights — the old model only steered through corners —
        // so they take the same shape every generated straight has: ideal down the middle, and the AI's two
        // extremes out at the edges.
        return new TrackInfoV2.TrackSegment
        {
            label = "Straight",
            type = TrackInfoV2.SegmentType.Straight,
            length = length,
            maxSpeed = topSpeed,
            racingLine = new TrackInfoV2.SegmentRacingLine { leftApex = -outer, rightApex = outer },
        };
    }

    // Legacy lines are normalised across the track, negative toward the infield, which is the same sense
    // TrackInfoV2 uses (negative = left of travel = inside on a left-hand oval). So they only need scaling
    // into metres. "Lowest" is the inside line and "highest" the outside one.
    static TrackInfoV2.SegmentRacingLine Lines(TrackInfo legacy, int i, float outer)
    {
        return new TrackInfoV2.SegmentRacingLine
        {
            idealEntry = At(legacy.idealEntry, i) * outer,
            idealApex  = At(legacy.idealMidpoint, i) * outer,
            idealExit  = At(legacy.idealExit, i) * outer,

            leftEntry  = At(legacy.lowestEntry, i) * outer,
            leftApex   = At(legacy.lowestMidpoint, i) * outer,
            leftExit   = At(legacy.lowestExit, i) * outer,

            rightEntry = At(legacy.highestEntry, i) * outer,
            rightApex  = At(legacy.highestMidpoint, i) * outer,
            rightExit  = At(legacy.highestExit, i) * outer,
        };
    }

    // Where the AI's outermost line sits on this asset already, so a ported track matches its neighbours.
    // Read off a turn rather than computed from the width, because the margin that produced it is the
    // generator's business and not restated here.
    static float OuterLineOffset(TrackInfoV2 v2)
    {
        if (v2.segments != null)
            foreach (var seg in v2.segments)
                if (seg.type == TrackInfoV2.SegmentType.Turn && seg.racingLine.rightApex > 0.01f)
                    return seg.racingLine.rightApex;

        return Mathf.Max(1f, v2.defaultWidth * 0.5f - 1.6f);
    }

    // Point a segment index + offset at an absolute distance round the lap. The index names the segment the
    // node sits at the END of, so the offset carries whatever is left over.
    static void AnchorAtDistance(TrackInfoV2 v2, float distance, ref int index, ref float offset)
    {
        if (v2.segments == null || v2.segments.Length == 0) return;

        float lap = v2.TotalLength();
        if (lap > 1f) distance = ((distance % lap) + lap) % lap;

        float at = 0f;
        for (int i = 0; i < v2.segments.Length; i++)
        {
            float end = at + v2.segments[i].length;
            if (distance <= end || i == v2.segments.Length - 1)
            {
                index = i;
                offset = distance - end;
                return;
            }
            at = end;
        }
    }

    // How far the lap misses its own start by, in metres, walking the segments exactly.
    static float ClosureGap(System.Collections.Generic.List<TrackInfoV2.TrackSegment> segs)
    {
        Vector2 at = Vector2.zero;
        float heading = 0f;
        foreach (var seg in segs)
        {
            at += Displacement(seg, heading);
            if (seg.type == TrackInfoV2.SegmentType.Turn) heading += seg.angle;
        }
        return at.magnitude;
    }

    // Exact displacement across one segment: a chord for a straight, an arc for a turn.
    static Vector2 Displacement(TrackInfoV2.TrackSegment seg, float headingDeg)
    {
        float h = headingDeg * Mathf.Deg2Rad;
        if (seg.type == TrackInfoV2.SegmentType.Straight || Mathf.Abs(seg.angle) < 1e-4f)
            return new Vector2(Mathf.Cos(h) * seg.length, Mathf.Sin(h) * seg.length);

        float a = seg.angle * Mathf.Deg2Rad;
        float r = seg.length / a;                       // signed radius
        return new Vector2(r * (Mathf.Sin(h + a) - Mathf.Sin(h)),
                           -r * (Mathf.Cos(h + a) - Mathf.Cos(h)));
    }

    // Adjust the straights, and only the straights, until the lap joins up. Returns false when it cannot be
    // done without driving a straight to nothing, in which case the segments are left as measured.
    static bool CloseByStraights(System.Collections.Generic.List<TrackInfoV2.TrackSegment> segs)
    {
        var index = new System.Collections.Generic.List<int>();
        var dirs = new System.Collections.Generic.List<Vector2>();
        Vector2 fixedPart = Vector2.zero;
        float heading = 0f;

        for (int i = 0; i < segs.Count; i++)
        {
            if (segs[i].type == TrackInfoV2.SegmentType.Turn)
            {
                fixedPart += Displacement(segs[i], heading);
                heading += segs[i].angle;
            }
            else
            {
                index.Add(i);
                float h = heading * Mathf.Deg2Rad;
                dirs.Add(new Vector2(Mathf.Cos(h), Mathf.Sin(h)));
            }
        }
        if (index.Count == 0) return false;

        // Residual the straights have to make up, then the least-norm correction that does it:
        // s = s0 + A^T (A A^T)^-1 (target - A s0), with A the 2xN matrix of straight directions.
        Vector2 walked = fixedPart;
        for (int k = 0; k < index.Count; k++) walked += dirs[k] * segs[index[k]].length;
        Vector2 residual = -walked;

        float axx = 0f, axy = 0f, ayy = 0f;
        foreach (var d in dirs) { axx += d.x * d.x; axy += d.x * d.y; ayy += d.y * d.y; }
        float det = axx * ayy - axy * axy;
        if (Mathf.Abs(det) < 1e-6f) return false;       // every straight points the same way

        float lx = (ayy * residual.x - axy * residual.y) / det;
        float ly = (-axy * residual.x + axx * residual.y) / det;

        var lengths = new float[index.Count];
        for (int k = 0; k < index.Count; k++)
        {
            lengths[k] = segs[index[k]].length + dirs[k].x * lx + dirs[k].y * ly;
            if (lengths[k] < 1f) return false;          // would fold a straight away entirely
        }

        for (int k = 0; k < index.Count; k++)
        {
            var seg = segs[index[k]];
            seg.length = lengths[k];
            segs[index[k]] = seg;
        }
        return true;
    }

    // Scale the turns so they take `share` of the lap, keeping their relative sizes — the measured corner
    // proportions are the good part of the legacy data and are preserved exactly. The straights take the
    // rest, provisionally; CloseByStraights then redistributes them to shut the loop.
    static void SetTurnShare(System.Collections.Generic.List<TrackInfoV2.TrackSegment> segs, float share)
    {
        float turns = 0f, straights = 0f;
        foreach (var seg in segs)
            if (seg.type == TrackInfoV2.SegmentType.Turn) turns += seg.length; else straights += seg.length;

        float lap = turns + straights;
        if (lap < 1f || turns < 1f || straights < 1f) return;

        float wantTurns = lap * Mathf.Clamp(share, 0.1f, 0.85f);
        float turnScale = wantTurns / turns;
        float straightScale = (lap - wantTurns) / straights;

        for (int i = 0; i < segs.Count; i++)
        {
            var seg = segs[i];
            bool isTurn = seg.type == TrackInfoV2.SegmentType.Turn;
            seg.length *= isTurn ? turnScale : straightScale;
            if (isTurn) { seg.leadIn *= turnScale; seg.leadOut *= turnScale; }
            segs[i] = seg;
        }
    }

    // A legacy track holds no banking for its straights — the old model only banked corners — but a real
    // circuit does, and Phoenix's front stretch is a few degrees.
    static void BankStraights(System.Collections.Generic.List<TrackInfoV2.TrackSegment> segs, float banking)
    {
        for (int i = 0; i < segs.Count; i++)
        {
            if (segs[i].type != TrackInfoV2.SegmentType.Straight) continue;
            var seg = segs[i];
            seg.banking = banking;
            segs[i] = seg;
        }
    }

    // Bring a closed lap to a target length. Uniform, so it stays closed.
    static void Rescale(System.Collections.Generic.List<TrackInfoV2.TrackSegment> segs, float targetLap)
    {
        float total = 0f;
        foreach (var seg in segs) total += seg.length;
        if (total < 1f || targetLap < 1f) return;

        float k = targetLap / total;
        for (int i = 0; i < segs.Count; i++)
        {
            var seg = segs[i];
            seg.length *= k;
            seg.leadIn *= k;
            seg.leadOut *= k;
            segs[i] = seg;
        }
    }

    static float TotalAngle(TrackInfoV2 v2)
    {
        float total = 0f;
        if (v2.segments != null) foreach (var seg in v2.segments) total += seg.angle;
        return total;
    }

    static void Dump(TrackInfo info)
    {
        var sb = new StringBuilder();
        sb.AppendLine(EditorJsonUtility.ToJson(info, true));

        // AnimationCurves survive ToJson as key lists, but the per-turn decel curves are arrays OF curves and
        // read far more clearly sampled than as control points.
        Sample(sb, "pitLane", info.pitLane);
        Sample(sb, "pitSpeed", info.pitSpeed);
        for (int i = 0; info.lowTurnDecel != null && i < info.lowTurnDecel.Length; i++)
            Sample(sb, $"lowTurnDecel[{i}]", info.lowTurnDecel[i]);
        for (int i = 0; info.highTurnDecel != null && i < info.highTurnDecel.Length; i++)
            Sample(sb, $"highTurnDecel[{i}]", info.highTurnDecel[i]);

        Directory.CreateDirectory(DumpFolder);
        string path = Path.Combine(DumpFolder, $"legacy-{info.name}.json");
        File.WriteAllText(path, sb.ToString());
        Debug.Log($"Legacy track '{info.name}' dumped to {path}");
    }

    static void Sample(StringBuilder sb, string label, AnimationCurve curve)
    {
        if (curve == null || curve.length == 0) { sb.AppendLine($"// {label}: empty"); return; }

        sb.Append($"// {label}: {curve.length} keys, t {curve.keys[0].time:0.###}..{curve.keys[curve.length - 1].time:0.###} -> ");
        for (int i = 0; i < curve.length; i++)
            sb.Append($"({curve.keys[i].time:0.###},{curve.keys[i].value:0.###}) ");
        sb.AppendLine();
    }
}
