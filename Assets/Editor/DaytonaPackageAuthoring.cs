using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Daytona, authored the way Watkins Glen was rather than left as generated dressing.
//
// Watkins Glen is the reference paddock: a walkable pocket behind pit road with the player's RV, the weekend's
// places pinned by hand (the gate to the grandstands, the winner's circle, the fan fence, the drivers' room,
// the intro stage) and a couple of named start points. Daytona was a generated oval with a pocket guessed off a
// pit lane that ran along the OUTSIDE of the tri-oval, so its paddock sat on the racing surface.
//
// Author Daytona does three things, and only to Daytona:
//   1. Regenerates its geometry, which now carries the real pit road: a straight chord across the tri-oval
//      infield (TrackDimensions.pitChordInsetMetres -> OvalGeometry.TryBuildChordPitLane), so pit road and the
//      racing surface are NOT parallel - close at either end, far apart at the start/finish line. The main lap
//      is unchanged, so the trained racing line still fits.
//   2. Re-cuts the walls to the new pit road. Pit road is in the infield now, so the outside wall - and the
//      catch fence on it - runs unbroken all the way round; only the infield wall opens where pit road crosses it.
//   3. Lays the paddock out in the infield behind pit road with Watkins Glen's key areas in it, each at the same
//      distance along and back from pit road as it is at Watkins Glen (read off the Watkins package, not typed
//      in), so the two paddocks walk the same way.
//
// Safe to re-run: it replaces only the pieces it places (by name) and reshapes the paddock pocket, RV and
// fallback start it finds; anything else in the package is left alone. Do NOT follow it with an overwriting
// Dress Selected Package - that regenerates the paddock pocket from the pit lane and throws this layout away.
// Report writes Temp/track_package_report.txt comparing the two packages.
public static class DaytonaPackageAuthoring
{
    const string PackageDir = "Assets/Resources/TrackPackages";
    const string ReportPath = "Temp/track_package_report.txt";
    const string TrackId = "Daytona";
    const string TraceDir = "Assets/TrackTraces";
    const string BlueprintId = "WatkinsGlen";

    // Watkins Glen's paddock frame. Its pit road runs toward -x and its paddock lies behind it toward +y, so a
    // place is measured along -x from the paddock boundary's entry-end edge and out along +y from its pit-road
    // edge (both edges read off the boundary itself).
    static readonly Vector2 BlueprintAlong = Vector2.left;
    static readonly Vector2 BlueprintOut = Vector2.up;

    // How deep the Watkins paddock is behind pit road, kept for Daytona.
    const float PaddockDepth = 76f;
    // Box lane starts/stops this far inside the ends of the front stretch, so no box sits on a pit-road arc.
    const float BoxLaneMargin = 20f;

    // The pieces copied from the blueprint, by name. Each lands at the same along/out offset at Daytona.
    static readonly string[] KeyAreas =
    {
        "Grandstand_Marker", "SponsorSuite_Marker", "SigningFence_Marker", "MeetingRoom_Marker",
        "IntroStage_Marker", "SpawnPoint_VictoryLane", "SpawnPoint_PitLaneCenter",
    };

    [MenuItem("Draftmaster/Tracks/Daytona/Author Daytona (Watkins Glen Blueprint)")]
    public static void Author()
    {
        string summary = AuthorDaytona();
        Debug.Log($"[DaytonaAuthoring] {summary}");
        File.WriteAllText("Temp/daytona_author_result.txt", summary);
        Report();
    }

    public static string AuthorDaytona()
    {
        // 1. Geometry: refilled in place, so the asset's GUID - and the package's reference to it - survive.
        var row = TrackCatalog.Row(TrackId);
        if (row == null) return "Daytona is not in the catalogue.";
        var geometry = TrackAuthoringMenu.GenerateGeometry(row, overwrite: true);

        // 1b. The real shape over the formula's: the centreline traced from the OSM outer wall
        // (Tools/trace_from_wall.py). The import re-fits the chord pit road to it and puts the start/finish line
        // on the tri-oval where the trace begins. With no trace, or one the importer refuses, the formula stands.
        string traceNote = null;
        if (geometry != null && File.Exists($"{TraceDir}/{TrackId}.json"))
        {
            traceNote = OsmTrackImporter.Import(TrackId, geometry).Trim();
            EditorUtility.SetDirty(geometry);
            AssetDatabase.SaveAssets();
        }

        if (geometry == null || !geometry.hasPitLane || geometry.pitSegments == null || geometry.pitSegments.Length != 3)
            return "Daytona geometry did not come out with a chord pit road - package left alone." +
                   (traceNote != null ? $" Trace import: {traceNote}" : "");

        var blueprint = ReadBlueprint(out string blueprintError);
        if (blueprint == null) return blueprintError;

        string path = $"{PackageDir}/{TrackId}.prefab";
        var contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var package = contents.GetComponent<TrackPackage>();
            var builder = package != null ? package.Builder : null;
            if (builder == null || builder.track != geometry) return "Daytona package has no TrackBuilder on its geometry.";
            var paddockRoot = package.paddockRoot != null ? package.paddockRoot : contents.transform.Find("Paddock");
            if (paddockRoot == null) return "Daytona package has no Paddock root.";

            var notes = new List<string>();
            if (traceNote != null) notes.Add($"trace: {traceNote}");
            var frame = DaytonaFrame(builder, notes);

            // 2. Walls: gaps measured against the new pit road, on whichever side it actually is.
            var envBuilder = contents.GetComponentInChildren<TrackEnvironmentBuilder>(true);
            if (envBuilder != null && envBuilder.environment != null)
            {
                var env = envBuilder.environment;
                EnsureApron(env, geometry, notes);
                env.barrierGaps = TrackDressingFactory.PitGaps(builder, env.outerEdgeOffset);
                EditorUtility.SetDirty(env);
                AssetDatabase.SaveAssets();
                envBuilder.Build();
                int outside = 0;
                foreach (var g in env.barrierGaps) if (g.side == TrackEnvironment.BarrierSide.Inner) outside++;
                notes.Add($"{env.barrierGaps.Length} wall gaps, {outside} in the outside wall");
            }
            var ground = contents.GetComponentInChildren<TrackGround>(true);
            if (ground != null) ground.Build();

            // Grandstands: laid along the road as it is now, so the shape changing (a traced import) doesn't leave
            // them standing where the old front stretch was. Before the paddock, whose seat is picked from them.
            notes.Add($"{TrackDressingFactory.RebuildGrandstands(contents, builder)} grandstands");

            // 3. The paddock.
            LayPaddock(contents, paddockRoot, builder, frame, blueprint, notes);

            PrefabUtility.SaveAsPrefabAsset(contents, path);
            return $"Daytona authored: {string.Join("; ", notes)}.";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
            if (blueprint.root != null) PrefabUtility.UnloadPrefabContents(blueprint.root);
        }
    }

    // The apron: 3.5 m of paving below the yellow line, all the way round on the infield side. A strip anchored
    // to the left (infield) edge, so it follows the road wherever the trace takes it; TrackEnvironmentBuilder
    // registers it as tarmac run-off, so a car on it is off the racing surface but not on the grass.
    const float ApronMetres = 3.5f;
    const string ApronLabel = "Apron";
    const string ApronFallbackMaterial = "Assets/Materials/TarmacLight.mat";

    static void EnsureApron(TrackEnvironment env, TrackInfoV2 geometry, List<string> notes)
    {
        if (geometry.segments == null || geometry.segments.Length == 0) return;
        var strips = new List<TrackEnvironment.Strip>(env.strips ?? new TrackEnvironment.Strip[0]);
        strips.RemoveAll(s => s.label == ApronLabel);
        int last = geometry.segments.Length - 1;
        strips.Add(new TrackEnvironment.Strip
        {
            label = ApronLabel,
            useSpline = TrackEnvironment.SplineRef.Main,
            anchor = TrackEnvironment.LateralAnchor.LeftEdge,
            startSegmentIndex = 0,
            startDistance = 0f,
            endSegmentIndex = last,
            endDistance = geometry.segments[last].length,
            lateralOffset = -ApronMetres * 0.5f,   // centred half its width outboard of the left edge
            width = ApronMetres,
            sortingOrder = env.runoffSortingOrder + 1,
            // The environment's run-off tarmac if it has one, else a lighter tarmac than the racing surface - an
            // apron reads paler than the groove from the air.
            material = env.tarmacRunoffMaterial != null ? env.tarmacRunoffMaterial
                     : AssetDatabase.LoadAssetAtPath<Material>(ApronFallbackMaterial),
            uvLengthScale = 1f,
        });
        env.strips = strips.ToArray();
        EditorUtility.SetDirty(env);
        notes.Add($"{ApronMetres} m apron in {(strips[strips.Count - 1].material != null ? strips[strips.Count - 1].material.name : "NO MATERIAL")}");
    }

    // ------------------------------------------------------------------ frames

    // A paddock frame: where pit road's paddock-side edge starts, which way pit road runs and which way is away
    // from it.
    struct Frame
    {
        public Vector2 origin, along, outward;
        public float length;      // the paddock's extent along pit road
        public Vector2 At(Vector2 local) => origin + along * local.x + outward * local.y;
        public Vector2 Local(Vector2 world) =>
            new Vector2(Vector2.Dot(world - origin, along), Vector2.Dot(world - origin, outward));
    }

    // Daytona's frame is the straight chord of pit road, measured off the built pit lane. Also sets the box lane
    // to run only along the chord, between the ends of the front stretch.
    static Frame DaytonaFrame(TrackBuilder builder, List<string> notes)
    {
        var track = builder.track;
        float entryArc = track.pitSegments[0].length;
        float chord = track.pitSegments[1].length;
        float exitArc = track.pitSegments[2].length;
        float pitLength = entryArc + chord + exitArc;
        float pitHalf = track.pitDefaultWidth * 0.5f;

        float frontLength = 0f;
        foreach (var seg in track.segments)
        {
            string label = seg.label ?? "";
            if (!label.StartsWith("Front Stretch") && label != "Tri-Oval") break;
            frontLength += seg.length;
        }

        var chordStart = builder.SamplePitAt(entryArc + 0.01f);
        // The start/finish line, on the front stretch: on a traced lap the lap itself starts on the back stretch.
        var lapStart = builder.SampleAt(track.startFinishDistance);
        Vector2 along = chordStart.tangent.normalized;
        // Away from the racing surface: the side of pit road the lap start is NOT on.
        Vector2 outward = Vector2.Dot(lapStart.position - chordStart.position, chordStart.normal) > 0f
            ? -chordStart.normal : chordStart.normal;
        // The boxes go on the paddock side of pit road, as at Watkins Glen: pit road, then the boxes, then the
        // paddock behind them. On this anticlockwise lap that is the left of pit-lane travel.
        builder.pitBoxLaneOnLeft = Vector2.Dot(outward, chordStart.normal) < 0f;

        // Ends of the front stretch, projected onto the chord. A traced lap has no "Front Stretch" pieces - it
        // starts on the start/finish line, mid tri-oval - so there the boxes run the length of the chord itself.
        float boxFrom, boxTo;
        if (frontLength > 0f)
        {
            float startAlong = Vector2.Dot(lapStart.position - chordStart.position, along);
            float endAlong = Vector2.Dot(builder.SampleAt(frontLength).position - chordStart.position, along);
            boxFrom = entryArc + startAlong + BoxLaneMargin;
            boxTo = entryArc + endAlong - BoxLaneMargin;
        }
        else
        {
            boxFrom = entryArc + BoxLaneMargin;
            boxTo = entryArc + chord - BoxLaneMargin;
        }
        builder.pitBoxLaneStartOffset = boxFrom;
        builder.pitBoxLaneEndOffset = pitLength - boxTo;
        builder.Build();
        EditorUtility.SetDirty(builder);
        notes.Add($"pit road {pitLength:0} m ({entryArc:0} arc + {chord:0} chord + {exitArc:0} arc), " +
                  $"boxes {boxFrom:0}..{boxTo:0} m along it");

        var boxStart = builder.SamplePitAt(boxFrom);
        return new Frame
        {
            origin = boxStart.position + outward * pitHalf,
            along = along,
            outward = outward,
            length = boxTo - boxFrom,
        };
    }

    class Blueprint
    {
        public Frame frame;
        public readonly Dictionary<string, GameObject> pieces = new Dictionary<string, GameObject>();
        public Vector2 rvLocal;
        public float rvTurn;      // RV's up axis relative to the frame's outward axis, degrees
        public GameObject root;   // loaded prefab contents; unloaded by the caller
    }

    // Loads Watkins Glen and measures it. The contents stay loaded (pieces are copied from them) until
    // AuthorDaytona unloads them.
    static Blueprint ReadBlueprint(out string error)
    {
        error = null;
        var contents = PrefabUtility.LoadPrefabContents($"{PackageDir}/{BlueprintId}.prefab");
        var boundary = contents.GetComponentInChildren<PaddockBoundary>(true);
        var poly = boundary != null ? boundary.GetComponent<PolygonCollider2D>() : null;
        if (poly == null)
        {
            PrefabUtility.UnloadPrefabContents(contents);
            error = "Watkins Glen has no paddock boundary to measure from.";
            return null;
        }

        float maxX = float.MinValue, minX = float.MaxValue, minY = float.MaxValue;
        foreach (var p in poly.points)
        {
            Vector2 w = boundary.transform.TransformPoint(p);
            maxX = Mathf.Max(maxX, w.x);
            minX = Mathf.Min(minX, w.x);
            minY = Mathf.Min(minY, w.y);
        }

        var bp = new Blueprint
        {
            root = contents,
            // Along runs -x, so the entry-end edge is the boundary's largest x.
            frame = new Frame { origin = new Vector2(maxX, minY), along = BlueprintAlong, outward = BlueprintOut, length = maxX - minX },
        };
        foreach (var t in contents.GetComponentsInChildren<Transform>(true))
            if (System.Array.IndexOf(KeyAreas, t.name) >= 0 && !bp.pieces.ContainsKey(t.name)) bp.pieces[t.name] = t.gameObject;

        var rv = contents.transform.Find("RV");
        if (rv != null)
        {
            bp.rvLocal = bp.frame.Local(rv.position);
            bp.rvTurn = Vector2.SignedAngle(bp.frame.outward, rv.up);
        }

        foreach (var name in KeyAreas)
            if (!bp.pieces.ContainsKey(name))
            {
                PrefabUtility.UnloadPrefabContents(contents);
                error = $"Watkins Glen has no {name} to copy.";
                return null;
            }
        return bp;
    }

    // Map a direction from one paddock frame to the other. Watkins runs clockwise and Daytona anticlockwise, so
    // this is a reflection as well as a rotation - which is what is wanted: "toward pit road" stays toward pit road.
    static Vector2 MapDirection(Vector2 dir, Frame from, Frame to) =>
        to.along * Vector2.Dot(dir, from.along) + to.outward * Vector2.Dot(dir, from.outward);

    static float Heading(Vector2 dir) => Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

    // ------------------------------------------------------------------ the paddock

    static void LayPaddock(GameObject contents, Transform paddockRoot, TrackBuilder builder, Frame frame,
                           Blueprint bp, List<string> notes)
    {
        // The pocket: the length of the box lane, from pit road's paddock-side edge back to Watkins Glen's depth -
        // Watkins Glen's own boundary starts at the same edge, so the boxes behind pit road are inside it and the
        // player walks straight from the paddock to their car.
        var boundary = paddockRoot.GetComponentInChildren<PaddockBoundary>(true);
        if (boundary == null)
        {
            var go = new GameObject("PaddockBoundary");
            go.transform.SetParent(paddockRoot, false);
            go.AddComponent<PolygonCollider2D>();
            boundary = go.AddComponent<PaddockBoundary>();
        }
        boundary.transform.localPosition = Vector3.zero;
        boundary.transform.localRotation = Quaternion.identity;
        var poly = boundary.GetComponent<PolygonCollider2D>();
        poly.isTrigger = true;
        const float near = 0f;
        poly.points = new[]
        {
            frame.At(new Vector2(0f, near)), frame.At(new Vector2(frame.length, near)),
            frame.At(new Vector2(frame.length, PaddockDepth)), frame.At(new Vector2(0f, PaddockDepth)),
        };
        EditorUtility.SetDirty(poly);
        notes.Add($"paddock {frame.length:0} x {PaddockDepth - near:0} m in the infield");

        // The RV, where the player wakes up: same offset from pit road, same way round, as at Watkins Glen.
        var rv = paddockRoot.Find("RV");
        if (rv != null)
        {
            Vector2 rvPos = frame.At(bp.rvLocal);
            Vector2 rvUp = (Vector2)(Quaternion.Euler(0f, 0f, bp.rvTurn) * frame.outward);
            rv.localPosition = new Vector3(rvPos.x, rvPos.y, rv.localPosition.z);
            rv.localRotation = Quaternion.Euler(0f, 0f, Heading(rvUp) - 90f);
            notes.Add($"RV at {rvPos}");
        }

        // The generated fallback start goes to the middle of the paddock rather than the old pocket.
        var fallback = paddockRoot.Find("SpawnPoint_Paddock");
        if (fallback != null)
        {
            Vector2 mid = frame.At(new Vector2(frame.length * 0.5f, PaddockDepth * 0.5f));
            fallback.localPosition = new Vector3(mid.x, mid.y, 0f);
        }

        // The key areas.
        int placed = 0;
        foreach (var name in KeyAreas)
        {
            var source = bp.pieces[name];
            foreach (var old in FindAll(contents.transform, name)) Object.DestroyImmediate(old.gameObject);

            var parent = name.StartsWith("SpawnPoint_") ? EnsureChild(paddockRoot, "PlayerSpawnPoints") : paddockRoot;
            var copy = Object.Instantiate(source, parent);
            copy.name = name;

            Vector2 pos = frame.At(bp.frame.Local(source.transform.position));
            Vector2 right = MapDirection(source.transform.right, bp.frame, frame);
            copy.transform.position = new Vector3(pos.x, pos.y, source.transform.position.z);
            copy.transform.rotation = Quaternion.Euler(0f, 0f, Heading(right));
            copy.transform.localScale = source.transform.localScale;
            placed++;
        }
        notes.Add($"{placed} Watkins Glen key areas placed");

        // The grandstand gate's seat is across the track: in the front-stretch stand nearest the start/finish.
        var gate = paddockRoot.Find("Grandstand_Marker");
        var marker = gate != null ? gate.GetComponent<WeekendMarker>() : null;
        if (marker != null)
        {
            var seat = gate.Find("Seat");
            if (seat == null)
            {
                seat = new GameObject("Seat").transform;
                seat.SetParent(gate, false);
            }
            if (TrySeatInStand(contents, builder, out Vector2 seatPos))
            {
                seat.position = new Vector3(seatPos.x, seatPos.y, 0f);
                seat.rotation = Quaternion.identity;
                notes.Add($"grandstand seat at {seatPos}");
            }
            marker.teleportTo = seat;
            marker.cameraView = null;
            EditorUtility.SetDirty(marker);
        }
    }

    // A seat in the stand closest to the start/finish line (the middle of the tri-oval), a quarter of its depth
    // back from the middle toward the track.
    static bool TrySeatInStand(GameObject contents, TrackBuilder builder, out Vector2 seat)
    {
        seat = default;
        // The start/finish line: the middle of the formula's "Tri-Oval" piece, or startFinishDistance on a traced
        // lap, which has no such piece and puts the line there itself.
        var segs = builder.track.segments;
        float triOval = 0f;
        bool labelled = false;
        for (int i = 0; i < segs.Length; i++)
        {
            if (segs[i].label == "Tri-Oval") { triOval += segs[i].length * 0.5f; labelled = true; break; }
            triOval += segs[i].length;
        }
        var line = builder.SampleAt(labelled ? triOval : builder.track.startFinishDistance);

        Grandstand best = null;
        float bestDist = float.MaxValue;
        foreach (var stand in contents.GetComponentsInChildren<Grandstand>(true))
        {
            float d = Vector2.Distance(stand.transform.position, line.position);
            if (d < bestDist) { bestDist = d; best = stand; }
        }
        if (best == null) return false;

        Vector2 centre = best.transform.position;
        Vector2 nearest = line.position;
        float nd = float.MaxValue;
        foreach (var s in builder.SampleCenterline())
        {
            float d = (s.position - centre).sqrMagnitude;
            if (d < nd) { nd = d; nearest = s.position; }
        }
        seat = centre + (nearest - centre).normalized * best.depth * 0.25f;
        return true;
    }

    static List<Transform> FindAll(Transform root, string name)
    {
        var found = new List<Transform>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t != root && t.name == name) found.Add(t);
        return found;
    }

    static Transform EnsureChild(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t != null) return t;
        t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    // ------------------------------------------------------------------ report

    [MenuItem("Draftmaster/Tracks/Daytona/Report Packages (Watkins Glen vs Daytona)")]
    public static void Report()
    {
        var sb = new StringBuilder();
        DescribePackage(sb, BlueprintId);
        DescribePackage(sb, TrackId);
        File.WriteAllText(ReportPath, sb.ToString());
        Debug.Log($"[DaytonaAuthoring] wrote {ReportPath}");
    }

    static void DescribePackage(StringBuilder sb, string id)
    {
        string path = $"{PackageDir}/{id}.prefab";
        var contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            sb.AppendLine($"===== {id}");
            Walk(sb, contents.transform, 0);

            var builder = contents.GetComponentInChildren<TrackBuilder>(true);
            if (builder != null && builder.track != null) DescribeGeometry(sb, builder);

            foreach (var b in contents.GetComponentsInChildren<PaddockBoundary>(true))
            {
                var poly = b.GetComponent<PolygonCollider2D>();
                if (poly == null) continue;
                sb.Append($"  boundary {b.name}: ");
                foreach (var p in poly.points) sb.Append($"{(Vector2)b.transform.TransformPoint(p)} ");
                sb.AppendLine();
            }
            foreach (var m in contents.GetComponentsInChildren<WeekendMarker>(true))
            {
                var box = m.GetComponent<BoxCollider2D>();
                sb.AppendLine($"  marker {m.name}: venue {m.venue} label '{m.label}' teleport " +
                              $"{(m.teleportTo != null ? m.teleportTo.position.ToString() : "-")} zoom {m.cameraZoom} " +
                              $"box {(box != null ? box.size + " off " + box.offset + " trig " + box.isTrigger : "-")}");
            }
            foreach (var sp in contents.GetComponentsInChildren<PlayerSpawnPoint>(true))
                sb.AppendLine($"  spawn {sp.name}: weight {sp.weight} label '{sp.label}' at {(Vector2)sp.transform.position}");
            var env = contents.GetComponentInChildren<TrackEnvironmentBuilder>(true);
            if (env != null && env.environment != null)
            {
                var e = env.environment;
                sb.AppendLine($"  environment {AssetDatabase.GetAssetPath(e)} inner {e.innerEdgeOffset} outer {e.outerEdgeOffset} safer {e.saferBarrier}");
                if (e.barrierGaps != null)
                    foreach (var g in e.barrierGaps)
                        sb.AppendLine($"    gap {g.label} {g.side} seg {g.segmentIndex} {g.startDistance:0.0}..{g.endDistance:0.0}");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    static void Walk(StringBuilder sb, Transform t, int depth)
    {
        if (depth > 3) return;
        var comps = new List<string>();
        foreach (var c in t.GetComponents<Component>())
            if (c != null && !(c is Transform)) comps.Add(c.GetType().Name);
        sb.AppendLine($"{new string(' ', depth * 2)}{t.name}  pos {t.position.x:0.0},{t.position.y:0.0},{t.position.z:0.00} " +
                      $"rot {t.eulerAngles.z:0.0} scale {t.localScale.x:0.##},{t.localScale.y:0.##}  [{string.Join(",", comps)}]");
        // Builder output is regenerated on load; listing it is noise.
        if (t.GetComponent<TrackBuilder>() || t.GetComponent<TrackEnvironmentBuilder>() || t.GetComponent<Grandstand>()
            || t.name == "Grandstands") return;
        for (int i = 0; i < t.childCount; i++) Walk(sb, t.GetChild(i), depth + 1);
    }

    static void DescribeGeometry(StringBuilder sb, TrackBuilder builder)
    {
        var track = builder.track;
        sb.AppendLine($"  geometry {track.name}: lap {track.TotalLength():0.0} width {track.defaultWidth:0.00} " +
                      $"pitWidth {track.pitDefaultWidth:0.00} entry seg {track.pitEntrySegmentIndex}+{track.pitEntryOffset:0.0} " +
                      $"= {track.pitEntryDistance:0.0}, exit seg {track.pitExitSegmentIndex}+{track.pitExitOffset:0.0} = {track.pitExitDistance:0.0}, " +
                      $"exitLine {track.pitExitLineDistance:0.0}, boxLane {builder.pitBoxLaneWidth} from {builder.pitBoxLaneStartOffset:0.0} to -{builder.pitBoxLaneEndOffset:0.0}");
        float cum = 0f;
        for (int i = 0; i < track.segments.Length; i++)
        {
            var s = track.segments[i];
            var at = builder.SampleAt(cum);
            sb.AppendLine($"    seg {i} {s.label} {s.type} len {s.length:0.0} ang {s.angle:0.0} bank {s.banking:0.0} start {at.position} hdg {Mathf.Atan2(at.tangent.y, at.tangent.x) * Mathf.Rad2Deg:0.0}");
            cum += s.length;
        }
        if (track.pitSegments != null)
            for (int i = 0; i < track.pitSegments.Length; i++)
                sb.AppendLine($"    pit {i} {track.pitSegments[i].label} len {track.pitSegments[i].length:0.0} ang {track.pitSegments[i].angle:0.0}");
        var pit = builder.SamplePitCenterline();
        if (pit.Count >= 2)
        {
            var exitOnTrack = builder.SampleAt(track.pitExitDistance);
            sb.AppendLine($"    pit lane start {pit[0].position} end {pit[pit.Count - 1].position} (track at exit {exitOnTrack.position}, " +
                          $"miss {Vector2.Distance(pit[pit.Count - 1].position, exitOnTrack.position):0.00} m), mid {pit[pit.Count / 2].position}");
            // Pit road to racing surface, along the lane: the proof it is not parallel.
            var main = builder.SampleCenterline();
            var line = new StringBuilder("    pit-to-track separation every 100 m:");
            for (float d = 0f; d <= pit[pit.Count - 1].distance; d += 100f)
            {
                var p = builder.SamplePitAt(d, pit).position;
                float best = float.MaxValue;
                foreach (var m in main) best = Mathf.Min(best, Vector2.Distance(p, m.position));
                line.Append($" {best:0}");
            }
            sb.AppendLine(line.ToString());
        }
    }
}
