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
//   3. Lays Watkins Glen's paddock down in the infield behind pit road, rotated to face it: boundary, RV, key
//      areas, motorhome/garage lot boxes and paddock tarmac, all put through one rotation + shift (read off the
//      Watkins package, not typed in), so the two paddocks are the same place.
//
// Safe to re-run: it replaces only the pieces it places (by name) and reshapes the paddock pocket, RV and
// fallback start it finds; anything else in the package is left alone. Do NOT follow it with an overwriting
// Dress Selected Package - that regenerates the paddock pocket from the pit lane and throws this layout away.
// Report writes Temp/track_package_report.txt comparing the two packages.
//
// Martinsville goes through the same steps (Author Martinsville): traced geometry (its inside walls off OSM,
// Tools/trace_from_wall.py --inner), pit road on the mapped front-stretch pit lane, Watkins Glen's paddock in the
// infield. The difference is size: Watkins' paddock is 470 x 76 m and Martinsville's infield, between pit road
// and the back stretch, is about 250 x 85. A squeezed copy of Watkins' lots holds a third of the field, so a
// short-track infield is laid out instead: the garages in a band along pit road, the motorhomes in the band
// behind, each box as long as the infield is clear of the racing surface at that depth, and the walkable
// boundary is that clear infield. Watkins' key areas and RV are carried in proportionally (same share of the
// way along pit road and back from it). Daytona has room and keeps the rigid copy.
public static class DaytonaPackageAuthoring
{
    const string PackageDir = "Assets/Resources/TrackPackages";
    const string ReportPath = "Temp/track_package_report.txt";
    const string TraceDir = "Assets/TrackTraces";
    const string BlueprintId = "WatkinsGlen";

    // What differs between the venues this authors.
    class Venue
    {
        public string id;
        public bool infieldWall;      // Martinsville has a pit wall along its inside edge; Daytona runs onto grass
        public bool apron;            // Daytona's 3.5 m paved strip below the yellow line
        public bool infieldLayout;    // too small for Watkins' paddock: lay the lots out in the clear infield
    }

    static readonly Venue Daytona = new Venue { id = "Daytona", infieldWall = false, apron = true, infieldLayout = false };
    static readonly Venue Martinsville = new Venue { id = "Martinsville", infieldWall = true, apron = false, infieldLayout = true };

    // How far the infield paddock stays from the edge of the racing surface, and its bands' depths.
    const float PaddockTrackClearance = 4f;
    const float InfieldGarageDepth = 44f;         // Watkins' garage box
    const float InfieldBandGap = 6f;              // the walkway between the garages and the motorhomes
    const float InfieldMinMotorhomeDepth = 26f;   // two lines of motorhomes and the aisle between

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
    public static void Author() => Author(Daytona);

    [MenuItem("Draftmaster/Tracks/Martinsville/Author Martinsville (Watkins Glen Blueprint)")]
    public static void AuthorMartinsville() => Author(Martinsville);

    static void Author(Venue venue)
    {
        string summary = AuthorVenue(venue);
        Debug.Log($"[PackageAuthoring] {summary}");
        File.WriteAllText($"Temp/{venue.id.ToLowerInvariant()}_author_result.txt", summary);
        Report(venue.id);
    }

    public static string AuthorDaytona() => AuthorVenue(Daytona);

    static string AuthorVenue(Venue venue)
    {
        string trackId = venue.id;
        // 1. Geometry: refilled in place, so the asset's GUID - and the package's reference to it - survive.
        var row = TrackCatalog.Row(trackId);
        if (row == null) return $"{trackId} is not in the catalogue.";
        var geometry = TrackAuthoringMenu.GenerateGeometry(row, overwrite: true);

        // 1b. The real shape over the formula's: the centreline traced from the OSM outer wall
        // (Tools/trace_from_wall.py). The import re-fits the chord pit road to it and puts the start/finish line
        // on the tri-oval where the trace begins. With no trace, or one the importer refuses, the formula stands.
        string traceNote = null;
        if (geometry != null && File.Exists($"{TraceDir}/{trackId}.json"))
        {
            traceNote = OsmTrackImporter.Import(trackId, geometry).Trim();
            EditorUtility.SetDirty(geometry);
            AssetDatabase.SaveAssets();
        }

        if (geometry == null || !geometry.hasPitLane || geometry.pitSegments == null || geometry.pitSegments.Length != 3)
            return $"{trackId} geometry did not come out with a three-piece pit road - package left alone." +
                   (traceNote != null ? $" Trace import: {traceNote}" : "");

        string moved = MoveSceneLotAreasIntoBlueprint();

        var blueprint = ReadBlueprint(out string blueprintError);
        if (blueprint == null) return blueprintError;

        string path = $"{PackageDir}/{trackId}.prefab";
        var contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var package = contents.GetComponent<TrackPackage>();
            var builder = package != null ? package.Builder : null;
            if (builder == null || builder.track != geometry) return $"{trackId} package has no TrackBuilder on its geometry.";
            var paddockRoot = package.paddockRoot != null ? package.paddockRoot : contents.transform.Find("Paddock");
            if (paddockRoot == null) return $"{trackId} package has no Paddock root.";

            var notes = new List<string>();
            if (moved != null) notes.Add(moved);
            if (traceNote != null) notes.Add($"trace: {traceNote}");
            var frame = DaytonaFrame(builder, notes);
            var map = BlueprintToDaytona(blueprint.frame, frame);
            Infield infield = null;
            if (venue.infieldLayout)
            {
                // Watkins' key areas stand in a strip at its entry end, ahead of the lot boxes. That strip keeps its
                // size; the lots stop short of it. Which end of this pit road it lands at is the rotation's call.
                float zone = CivicZone(blueprint);
                bool zoneAtEnd = Vector2.Dot(map.Point(blueprint.frame.origin) - frame.origin, frame.along) > frame.length * 0.5f;
                infield = FitInfield(builder, frame, zoneAtEnd ? 0f : zone, zoneAtEnd ? zone : 0f, notes);
                if (infield == null) return $"{trackId}: no clear infield behind pit road - package left alone.";
                map = ProportionalMap(map, blueprint.frame, infield, zone);
            }

            // 2. Walls: gaps measured against the new pit road, on whichever side it actually is.
            var envBuilder = contents.GetComponentInChildren<TrackEnvironmentBuilder>(true);
            if (envBuilder != null && envBuilder.environment != null)
            {
                var env = envBuilder.environment;
                if (venue.apron) EnsureApron(env, geometry, notes);
                // Daytona has no infield wall: the apron runs straight onto the grass, as at the real track. Left of
                // travel on an anticlockwise oval is BarrierSide.Outer.
                env.outerSideBarrier = venue.infieldWall;
                env.barrierGaps = TrackDressingFactory.PitGaps(builder, env.outerEdgeOffset);
                EnsurePaddockTarmac(env, blueprint, map, frame, infield?.outline, notes);
                EditorUtility.SetDirty(env);
                AssetDatabase.SaveAssets();
                // Loading the contents already ran the builder's OnEnable build, whose clear doesn't take during a
                // load - clear by hand or the new walls and run-off stack on the old.
                for (int i = envBuilder.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(envBuilder.transform.GetChild(i).gameObject, true);
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
            LayPaddock(contents, paddockRoot, builder, frame, blueprint, map, infield, notes);

            PrefabUtility.SaveAsPrefabAsset(contents, path);
            return $"{trackId} authored: {string.Join("; ", notes)}.";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
            if (blueprint.root != null) PrefabUtility.UnloadPrefabContents(blueprint.root);
        }
    }

    // The motorhome and garage lot boxes were drawn for Watkins Glen while it was the race scene's track, and
    // were left at the root of RaceScene - so every venue packed its lots into Watkins Glen's paddock
    // coordinates, which at Daytona is the racing surface by turn 1. They belong to Watkins Glen's package, like
    // the rest of its paddock: moved there (same world pose, so Watkins is unchanged) and out of the scene.
    static string MoveSceneLotAreasIntoBlueprint()
    {
        var found = new List<PaddockLotArea>();
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponent<TrackPackage>() == null)
                    found.AddRange(root.GetComponentsInChildren<PaddockLotArea>(true));
        }
        if (found.Count == 0) return null;

        string path = $"{PackageDir}/{BlueprintId}.prefab";
        var contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var package = contents.GetComponent<TrackPackage>();
            var paddock = package != null && package.paddockRoot != null ? package.paddockRoot : EnsureChild(contents.transform, "Paddock");
            foreach (var old in contents.GetComponentsInChildren<PaddockLotArea>(true)) Object.DestroyImmediate(old.gameObject);
            foreach (var area in found)
            {
                var copy = Object.Instantiate(area.gameObject, paddock);
                copy.name = area.name;
                copy.transform.SetPositionAndRotation(area.transform.position, area.transform.rotation);
                copy.transform.localScale = area.transform.localScale;
            }
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        var scenes = new HashSet<UnityEngine.SceneManagement.Scene>();
        foreach (var area in found)
        {
            scenes.Add(area.gameObject.scene);
            Undo.DestroyObjectImmediate(area.gameObject);
        }
        foreach (var scene in scenes)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }
        return $"moved {found.Count} lot box(es) from the race scene into the Watkins Glen package";
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
        public Transform rv;
        public PaddockBoundary boundary;
        public TrackEnvironment environment;
        public readonly List<PaddockLotArea> lotAreas = new List<PaddockLotArea>();
        public GameObject root;   // loaded prefab contents; unloaded by the caller
    }

    // Watkins Glen's paddock picked up and set down at Daytona: a rotation that turns "away from pit road" at
    // Watkins Glen onto "away from pit road" at Daytona, and a shift that puts the middle of Watkins' pit-road
    // edge on the middle of Daytona's. Rotation only, no mirror, so every rig, sprite and lot box keeps its
    // handedness and the paddock is the same place, just facing a different way. (Watkins runs clockwise and
    // Daytona anticlockwise, so the paddock's entry end lands at Daytona's exit end - the price of not mirroring.)
    //
    // squeeze < 1 shortens it along pit road (blueprint 'along') about the middle of pit road, for a venue too
    // short for it (Martinsville); 1 is the rigid copy. Depth from pit road is never squeezed.
    struct Rigid
    {
        public Vector2 from, to, along, outward;
        public float angle;
        public float squeeze;        // along pit road
        public float squeezeOut;     // away from it
        public float maxOut;         // nothing lands further back than this (0 = no limit)
        // With zone > 0 the first 'zone' metres from the blueprint's entry end keep their size and only the rest
        // is squeezed; fromHalf / toHalf are the two pit roads' half lengths.
        public float zone, fromHalf, toHalf;
        public Quaternion Turn => Quaternion.Euler(0f, 0f, angle);
        public Vector2 Point(Vector2 p)
        {
            Vector2 d = p - from;
            float a = Vector2.Dot(d, along), o = Vector2.Dot(d, outward);
            float a2 = a * squeeze, o2 = o * squeezeOut;
            if (zone > 0f)
            {
                float u = a + fromHalf;
                a2 = (u <= zone ? u : zone + (u - zone) * squeeze) - toHalf;
            }
            if (maxOut > 0f) o2 = Mathf.Min(o2, maxOut);
            d += along * (a2 - a) + outward * (o2 - o);
            return to + (Vector2)(Turn * d);
        }
        public Vector2 Dir(Vector2 d) => Turn * d;
    }

    static Rigid BlueprintToDaytona(Frame bp, Frame daytona) => new Rigid
    {
        from = bp.At(new Vector2(bp.length * 0.5f, 0f)),
        to = daytona.At(new Vector2(daytona.length * 0.5f, 0f)),
        along = bp.along,
        outward = bp.outward,
        angle = Vector2.SignedAngle(bp.outward, daytona.outward),
        squeeze = 1f,
        squeezeOut = 1f,
    };

    // ------------------------------------------------------------------ short-track infield

    // The clear infield behind pit road, in the paddock frame (x along pit road from the box lane's start, y back
    // from pit road's paddock-side edge), and the two lot boxes laid in it.
    class Infield
    {
        public Frame frame;
        public float depth;                  // how far back the infield is clear, at the middle of pit road
        public List<Vector2> outline;        // walkable boundary, world
        public Vector2 garageCentre, motorhomeCentre;   // world
        public float garageLength, garageDepth, motorhomeLength, motorhomeDepth;
    }

    // reserveLo / reserveHi: metres kept free of lot boxes at either end of pit road, for the key areas.
    static Infield FitInfield(TrackBuilder builder, Frame frame, float reserveLo, float reserveHi, List<string> notes)
    {
        var centreline = builder.SampleCenterline();
        float keepOut = builder.track.defaultWidth * 0.5f + PaddockTrackClearance;
        bool Clear(float a, float o)
        {
            Vector2 q = frame.At(new Vector2(a, o));
            foreach (var s in centreline)
                if ((s.position - q).sqrMagnitude < keepOut * keepOut) return false;
            return true;
        }

        float mid = frame.length * 0.5f;
        float depth = 0f;
        while (depth < 300f && Clear(mid, depth + 1f)) depth += 1f;

        // The run along pit road either side of the middle, held to the box lane, that is clear all the way
        // across a band of depth.
        void Span(float o1, float o2, out float lo, out float hi, float minA = 0f, float maxA = float.MaxValue)
        {
            minA = Mathf.Max(0f, minA);
            maxA = Mathf.Min(frame.length, maxA);
            bool BandClear(float a)
            {
                for (float o = o1; o < o2; o += 2f) if (!Clear(a, o)) return false;
                return Clear(a, o2);
            }
            lo = mid;
            hi = mid;
            while (lo > minA && BandClear(lo - 1f)) lo -= 1f;
            while (hi < maxA && BandClear(hi + 1f)) hi += 1f;
        }

        var f = new Infield { frame = frame, depth = depth };
        f.garageDepth = Mathf.Min(InfieldGarageDepth, depth - InfieldBandGap - InfieldMinMotorhomeDepth);
        f.motorhomeDepth = depth - f.garageDepth - InfieldBandGap;
        if (f.garageDepth < 20f) return null;

        float lotsFrom = reserveLo > 0f ? reserveLo + InfieldBandGap : 0f;
        float lotsTo = reserveHi > 0f ? frame.length - reserveHi - InfieldBandGap : frame.length;
        Span(0f, f.garageDepth, out float gLo, out float gHi, lotsFrom, lotsTo);
        float m0 = f.garageDepth + InfieldBandGap;
        Span(m0, depth, out float mLo, out float mHi, lotsFrom, lotsTo);
        f.garageLength = gHi - gLo;
        f.motorhomeLength = mHi - mLo;
        f.garageCentre = frame.At(new Vector2((gLo + gHi) * 0.5f, f.garageDepth * 0.5f));
        f.motorhomeCentre = frame.At(new Vector2((mLo + mHi) * 0.5f, m0 + f.motorhomeDepth * 0.5f));

        // The walkable pocket: the clear run at every depth, down one side and back up the other.
        var left = new List<Vector2>();
        var right = new List<Vector2>();
        for (float o = 0f; ; o = Mathf.Min(o + 4f, depth))
        {
            Span(o, o, out float lo, out float hi);
            left.Add(frame.At(new Vector2(lo, o)));
            right.Add(frame.At(new Vector2(hi, o)));
            if (o >= depth) break;
        }
        right.Reverse();
        f.outline = new List<Vector2>(left);
        f.outline.AddRange(right);

        notes.Add($"infield paddock {frame.length:0} m along pit road x {depth:0} m deep: garages {f.garageLength:0} x " +
                  $"{f.garageDepth:0} m, motorhomes {f.motorhomeLength:0} x {f.motorhomeDepth:0} m");
        return f;
    }

    // Watkins' key areas and RV: its entry-end strip carried at full size, the rest of the way along pit road
    // squeezed into what is left, depth scaled to the infield's - and never further back than the infield goes.
    static Rigid ProportionalMap(Rigid map, Frame bp, Infield infield, float zone)
    {
        map.zone = zone;
        map.fromHalf = bp.length * 0.5f;
        map.toHalf = infield.frame.length * 0.5f;
        map.squeeze = Mathf.Min(1f, (infield.frame.length - zone) / Mathf.Max(1f, bp.length - zone));
        map.squeezeOut = Mathf.Min(1f, infield.depth / PaddockDepth);
        map.maxOut = infield.depth - 3f;
        return map;
    }

    // How far Watkins' key-area strip runs from its entry end: up to where its first lot box starts.
    static float CivicZone(Blueprint bp)
    {
        float zone = float.MaxValue;
        foreach (var area in bp.lotAreas)
        {
            area.GetRect(out Vector3 c, out Quaternion r, out float w, out float d);
            Vector2 x = r * Vector3.right * (w * 0.5f), y = r * Vector3.up * (d * 0.5f), cc = c;
            foreach (var corner in new[] { cc - x - y, cc + x - y, cc + x + y, cc - x + y })
                zone = Mathf.Min(zone, bp.frame.Local(corner).x);
        }
        return zone == float.MaxValue ? 0f : Mathf.Max(0f, zone);
    }

    // Puts a copy of the blueprint transform's pose (position, z kept, rotation) through the rigid map.
    static void Place(Transform target, Transform source, Rigid map)
    {
        Vector2 p = map.Point(source.position);
        target.position = new Vector3(p.x, p.y, source.position.z);
        target.rotation = map.Turn * source.rotation;
        target.localScale = source.localScale;
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
            boundary = boundary,
        };
        foreach (var t in contents.GetComponentsInChildren<Transform>(true))
            if (System.Array.IndexOf(KeyAreas, t.name) >= 0 && !bp.pieces.ContainsKey(t.name)) bp.pieces[t.name] = t.gameObject;

        bp.rv = contents.transform.Find("RV");
        var env = contents.GetComponentInChildren<TrackEnvironmentBuilder>(true);
        bp.environment = env != null ? env.environment : null;
        bp.lotAreas.AddRange(contents.GetComponentsInChildren<PaddockLotArea>(true));

        foreach (var name in KeyAreas)
            if (!bp.pieces.ContainsKey(name))
            {
                PrefabUtility.UnloadPrefabContents(contents);
                error = $"Watkins Glen has no {name} to copy.";
                return null;
            }
        return bp;
    }

    // ------------------------------------------------------------------ the paddock

    // Watkins Glen's paddock tarmac: the run-off polygons of its environment that lie in its paddock (the big
    // one under the boundary). Put through the rigid map, then trimmed to Daytona's straight stretch of pit
    // road - Watkins' tarmac carries on along its pit wall past the paddock, and at Daytona that end would run
    // out onto the pit-road arc and the track.
    const string PaddockTarmacLabel = "Paddock (Watkins Glen)";

    // An infield layout paves its own outline in Watkins' tarmac instead.
    static void EnsurePaddockTarmac(TrackEnvironment env, Blueprint bp, Rigid map, Frame frame, List<Vector2> outline,
                                    List<string> notes)
    {
        var areas = new List<TrackEnvironment.RunoffArea>(env.runoffAreas ?? new TrackEnvironment.RunoffArea[0]);
        areas.RemoveAll(a => a.label != null && a.label.StartsWith(PaddockTarmacLabel));
        int added = 0;
        if (bp.environment != null && bp.environment.runoffAreas != null)
        {
            foreach (var src in bp.environment.runoffAreas)
            {
                if (src.surface != TrackEnvironment.SurfaceType.TarmacRunoff || src.points == null || src.points.Length < 3) continue;
                if (!InBlueprintPaddock(src.points, bp)) continue;

                var pts = new List<Vector2>();
                if (outline != null) pts.AddRange(outline);
                else foreach (var p in src.points) pts.Add(map.Point(p));
                // Keep only what lies between the two ends of the box lane.
                pts = ClipHalfPlane(pts, frame.origin, frame.along);
                pts = ClipHalfPlane(pts, frame.origin + frame.along * frame.length, -frame.along);
                if (pts.Count < 3) continue;

                areas.Add(new TrackEnvironment.RunoffArea
                {
                    label = added == 0 ? PaddockTarmacLabel : $"{PaddockTarmacLabel} {added}",
                    surface = TrackEnvironment.SurfaceType.TarmacRunoff,
                    points = pts.ToArray(),
                    // Watkins' own tarmac, so it looks the same rather than taking Daytona's run-off grey.
                    materialOverride = src.materialOverride != null ? src.materialOverride : bp.environment.tarmacRunoffMaterial,
                });
                added++;
                if (outline != null) break;
            }
        }
        env.runoffAreas = areas.ToArray();
        EditorUtility.SetDirty(env);
        notes.Add($"{added} paddock tarmac polygon(s) from Watkins Glen");
    }

    // A run-off polygon belongs to Watkins' paddock when the middle of its corners is inside the paddock
    // boundary. Not the corners themselves: the paddock tarmac was traced off the boundary, so its corners sit
    // on the boundary's edges, where an inside test is a coin toss.
    static bool InBlueprintPaddock(Vector2[] pts, Blueprint bp)
    {
        var collider = bp.boundary.GetComponent<PolygonCollider2D>();
        var poly = new Vector2[collider.points.Length];
        for (int i = 0; i < poly.Length; i++) poly[i] = bp.boundary.transform.TransformPoint(collider.points[i]);
        Vector2 mean = Vector2.zero;
        foreach (var p in pts) mean += p;
        return InPolygon(mean / pts.Length, poly);
    }

    // Even-odd ray cast. Not the collider's OverlapPoint: loaded prefab contents sit outside any physics scene
    // that answers queries.
    static bool InPolygon(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        return inside;
    }

    // Sutherland-Hodgman against one half-plane: keeps the side the normal points into.
    static List<Vector2> ClipHalfPlane(List<Vector2> pts, Vector2 point, Vector2 normal)
    {
        var result = new List<Vector2>();
        for (int i = 0; i < pts.Count; i++)
        {
            Vector2 a = pts[i], b = pts[(i + 1) % pts.Count];
            float da = Vector2.Dot(a - point, normal), db = Vector2.Dot(b - point, normal);
            if (da >= 0f) result.Add(a);
            if ((da >= 0f) != (db >= 0f)) result.Add(a + (b - a) * (da / (da - db)));
        }
        return result;
    }

    static void LayPaddock(GameObject contents, Transform paddockRoot, TrackBuilder builder, Frame frame,
                           Blueprint bp, Rigid map, Infield infield, List<string> notes)
    {
        // The pocket: Watkins Glen's boundary polygon, corner for corner. It starts at pit road's paddock-side
        // edge there too, so the boxes are inside it and the player walks straight from the paddock to their car.
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
        Vector2[] pts;
        if (infield != null)
        {
            pts = new Vector2[infield.outline.Count];
            for (int i = 0; i < pts.Length; i++) pts[i] = boundary.transform.InverseTransformPoint(infield.outline[i]);
            notes.Add($"paddock boundary: the clear infield, {pts.Length} corners");
        }
        else
        {
            var srcPoly = bp.boundary.GetComponent<PolygonCollider2D>();
            pts = new Vector2[srcPoly.points.Length];
            for (int i = 0; i < pts.Length; i++)
                pts[i] = boundary.transform.InverseTransformPoint(map.Point(bp.boundary.transform.TransformPoint(srcPoly.points[i])));
            notes.Add($"paddock boundary: Watkins Glen's {pts.Length}-corner polygon, turned {map.angle:0.0} deg");
        }
        poly.points = pts;
        EditorUtility.SetDirty(poly);

        // The RV, where the player wakes up: same place and same way round relative to pit road.
        var rv = paddockRoot.Find("RV");
        if (rv != null && bp.rv != null)
        {
            Place(rv, bp.rv, map);
            notes.Add($"RV at {(Vector2)rv.position}");
        }

        // The generated fallback start goes to the middle of the paddock rather than the old pocket.
        var fallback = paddockRoot.Find("SpawnPoint_Paddock");
        if (fallback != null)
        {
            Vector2 mid = frame.At(new Vector2(frame.length * 0.5f, (infield != null ? infield.depth : PaddockDepth) * 0.5f));
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
            Place(copy.transform, source.transform, map);
            placed++;
        }
        notes.Add($"{placed} Watkins Glen key areas placed");

        // The motorhome and garage lots: Watkins Glen's drawn boxes, turned with everything else. Without them
        // the lots grow off the RV in world directions, which only line up at Watkins Glen.
        foreach (var old in contents.GetComponentsInChildren<PaddockLotArea>(true)) Object.DestroyImmediate(old.gameObject);
        foreach (var src in bp.lotAreas)
        {
            var copy = Object.Instantiate(src.gameObject, paddockRoot);
            copy.name = src.name;
            Place(copy.transform, src.transform, map);
            // In an infield layout the box is the band the infield fit for it, whatever Watkins' was. The copy keeps
            // Watkins' turned rotation, so its own x runs along pit road one way or the other.
            var box = copy.GetComponent<BoxCollider2D>();
            if (infield != null && box != null)
            {
                bool garages = src.kind == PaddockLotKind.Garages;
                Vector2 centre = garages ? infield.garageCentre : infield.motorhomeCentre;
                float length = garages ? infield.garageLength : infield.motorhomeLength;
                float depth = garages ? infield.garageDepth : infield.motorhomeDepth;
                bool xAlong = Mathf.Abs(Vector2.Dot(copy.transform.rotation * Vector3.right, frame.along)) > 0.7f;
                Vector3 scale = copy.transform.lossyScale;
                copy.transform.position = new Vector3(centre.x, centre.y, copy.transform.position.z);
                box.offset = Vector2.zero;
                box.size = xAlong ? new Vector2(length / Mathf.Abs(scale.x), depth / Mathf.Abs(scale.y))
                                  : new Vector2(depth / Mathf.Abs(scale.x), length / Mathf.Abs(scale.y));
                EditorUtility.SetDirty(box);
            }
            var lot = copy.GetComponent<PaddockLotArea>();
            lot.GetRect(out _, out _, out float w, out float d);
            string fit = "";
            if (src.kind == PaddockLotKind.Motorhomes &&
                lot.Solve(40, PaddockLotArea.DefaultRvWidth, PaddockLotArea.DefaultRvLength, 0f, out var line, out int rows, out bool tight))
                fit = $", {rows * line.perRow} places for 40{(tight ? " (TIGHT)" : "")}";
            notes.Add($"{src.kind} lot box {w:0} x {d:0} m at {(Vector2)copy.transform.position}{fit}");
        }

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
    public static void Report() => Report(Daytona.id);

    [MenuItem("Draftmaster/Tracks/Martinsville/Report Packages (Watkins Glen vs Martinsville)")]
    public static void ReportMartinsville() => Report(Martinsville.id);

    static void Report(string trackId)
    {
        var sb = new StringBuilder();
        DescribePackage(sb, BlueprintId);
        DescribePackage(sb, trackId);
        DescribeOpenScenes(sb);
        File.WriteAllText(ReportPath, sb.ToString());
        Debug.Log($"[PackageAuthoring] wrote {ReportPath}");
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
            foreach (var a in contents.GetComponentsInChildren<PaddockLotArea>(true))
            {
                a.GetRect(out Vector3 c, out Quaternion r, out float w, out float d);
                Vector2 x = r * Vector3.right * (w * 0.5f), y = r * Vector3.up * (d * 0.5f);
                Vector2 cc = c;
                float clear = float.MaxValue;
                foreach (var corner in new[] { cc - x - y, cc + x - y, cc + x + y, cc - x + y, cc })
                    foreach (var s in builder != null ? builder.SampleCenterline() : new List<TrackBuilder.Sample>())
                        clear = Mathf.Min(clear, Vector2.Distance(corner, s.position));
                sb.AppendLine($"  lot {a.kind}: centre {cc} {w:0.0} x {d:0.0} rot {r.eulerAngles.z:0.0} corners {cc - x - y} {cc + x + y}, " +
                              $"nearest track centreline {clear:0.0} m");
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
                if (e.strips != null)
                    foreach (var s in e.strips)
                        sb.AppendLine($"    strip {s.label} {s.useSpline} {s.anchor} seg {s.startSegmentIndex}+{s.startDistance:0.0}..{s.endSegmentIndex}+{s.endDistance:0.0} " +
                                      $"lat {s.lateralOffset:0.0} w {s.width:0.0} order {s.sortingOrder} mat {(s.material != null ? s.material.name : "-")}");
                if (e.runoffAreas != null)
                    foreach (var a in e.runoffAreas)
                    {
                        sb.Append($"    runoff {a.label} {a.surface} mat {(a.materialOverride != null ? a.materialOverride.name : "-")}:");
                        if (a.points != null) foreach (var p in a.points) sb.Append($" {p}");
                        sb.AppendLine();
                    }
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // The race scene's own paddock pieces - the ones every track shares, which the package has to agree with.
    static void DescribeOpenScenes(StringBuilder sb)
    {
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            sb.AppendLine($"===== scene {scene.path}");
            foreach (var root in scene.GetRootGameObjects())
            {
                sb.AppendLine($"  root {root.name} active {root.activeSelf}");
                foreach (var rv in root.GetComponentsInChildren<RVExterior>(true))
                    sb.AppendLine($"    RVExterior {rv.name} at {(Vector2)rv.transform.position} rot {rv.transform.eulerAngles.z:0.0}");
                foreach (var sp in root.GetComponentsInChildren<PaddockSpawner>(true))
                {
                    sb.AppendLine($"    PaddockSpawner {sp.name} depth {sp.paddockDepth} gap {sp.pitGap} side {sp.side}");
                    for (int c = 0; c < sp.transform.childCount; c++)
                    {
                        var ch = sp.transform.GetChild(c);
                        var r = ch.GetComponent<Renderer>();
                        sb.AppendLine($"      child {ch.name} at {(Vector2)ch.position} bounds {(r != null ? r.bounds.ToString() : "-")}");
                    }
                }
                foreach (var lot in root.GetComponentsInChildren<DriverMotorhomeLot>(true))
                    sb.AppendLine($"    DriverMotorhomeLot {lot.name} dir {lot.lineDirection} rows {lot.rowCount} perRow {lot.maxPerRow} player {lot.playerLineIndex}");
                foreach (var b in root.GetComponentsInChildren<PaddockBoundary>(true))
                    sb.AppendLine($"    PaddockBoundary {b.name}");
                foreach (var a in root.GetComponentsInChildren<PaddockLotArea>(true))
                    sb.AppendLine($"    PaddockLotArea {a.name}");
            }
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
