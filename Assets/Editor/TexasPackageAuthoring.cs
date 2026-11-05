using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Texas Motor Speedway (FortWorth), dressed to look like the real place rather than the generic oval dressing.
//
// Everything here was measured off the top-down aerial in Assets/RefImages/texas-aerial.png, calibrated in
// Assets/RefImages/TexasOverlay.unity (see TexasLayoutReport for the transform), and cross-checked against the
// published facts: the main grandstand on the front stretch, the Speedway Club overlooking turn 1, the ten-storey
// Lone Star Tower overlooking turn 2, and Big Hoss TV - the 66 m video board that replaced the back-stretch seats
// in 2014 - on the back stretch, which has no grandstand any more.
//
// Author Texas does, to the FortWorth package only:
//   1. Grandstands. One continuous stand from the middle of turn 4, down the whole front stretch and tri-oval, to
//      the middle of turn 1 - where the real stand ends against the Speedway Club - bent round the curve in
//      pieces like Daytona's. Deep (the real one is a 60 m+ bowl): seating, then the white suites roof behind it.
//      The generic stands on the back stretch go.
//   2. Buildings outside the track: Speedway Club (turn 1), Lone Star Tower (turn 2), Big Hoss TV and the old
//      grandstand deck on the back stretch, the row of service buildings behind them.
//   3. The infield: everything between pit road and Infield Drive is paved (garages, the infield car park on the
//      turn 1 side, the care centre and skid pad on the turn 4 side), and the infield road course ("Holler Loop")
//      runs across the grass in the south half. Grass stays between the front stretch and pit road, as at the
//      real track.
//   4. The paddock: Watkins Glen's paddock laid behind the infield pit road the way Daytona's is
//      (DaytonaPackageAuthoring.LayWatkinsPaddock) - boundary, RV, key areas, motorhome and garage lots - which
//      lands on the real garage compound. Pit boxes run along the straight of pit road only, on the infield side.
//
// What it does NOT touch: the geometry (Josh's hand-shaped lap and drawn racing lines), the walls (the trioval has
// no infield wall on purpose; the walls already sit on the aerial's), the trained racing line, the TrackEnvironment
// asset. All of the dressing is TrackDressingShape / Grandstand recipes that rebuild on load - purely visual, no
// colliders, nothing registered with the car physics.
//
// Safe to re-run: it replaces the Grandstands root and its own TexasDressing root, and the paddock step replaces
// only what it places. Scattered scenery now under a stand, building or paving is removed (locked pieces stay and
// are reported). Writes Temp/texas_author_result.txt and, through TexasLayoutReport, Temp/texas_layout.png.
public static class TexasPackageAuthoring
{
    const string TrackId = TexasLayoutReport.TrackId;
    const string PackagePath = "Assets/Resources/TrackPackages/FortWorth.prefab";
    const string DressingName = "TexasDressing";
    const string GrandstandsName = "Grandstands";   // TrackDressingFactory's root: one set of stands, whoever made it
    const string ResultPath = "Temp/texas_author_result.txt";

    // ---- measured off the aerial (world metres, package space) ----

    // Where the main grandstand's seating ends at either end: inside the end of the suites roof in turn 4, and in
    // turn 1 where the roof stops against the Speedway Club. Projected onto the lap, so the stand starts and ends
    // at whatever lap distance is beside them.
    static readonly Vector2 MainStandTurn4End = new Vector2(325.3f, -150.6f);
    static readonly Vector2 MainStandTurn1End = new Vector2(-591.8f, -146.4f);
    const float StandSetback = 5f;       // road edge to the front row: wall + SAFER barrier + catch fence
    const float StandDepth = 34f;        // seating bowl
    const float SuitesDepth = 22f;       // the white suites/concourse roof behind the seating
    const float StandPieceLength = 110f; // as TrackDressingFactory
    // Each bent piece is the arc through its ends and middle, so where the curvature changes neighbouring pieces
    // leave a thin wedge of grass between their front and back corners. Every piece runs this far past each end.
    const float StandPieceOverlap = 1.5f;

    // Back stretch: the concrete deck the old seats stood on, from its turn 2 end to its turn 3 end, and the band
    // it fills beyond the road edge.
    static readonly Vector2 DeckTurn2End = new Vector2(-439.3f, -505f);
    static readonly Vector2 DeckTurn3End = new Vector2(74.1f, -505f);
    const float DeckFrom = 3f, DeckTo = 44f;
    // Big Hoss TV: middle of the back stretch, just behind the deck. 218 ft x 95 ft; from above, a 66 m beam.
    const float BigHossCentreX = -123.8f;
    const float BigHossBeyondEdge = 49f;
    const float BigHossLength = 66f, BigHossThickness = 5f;

    // Buildings: centre, long-axis heading (degrees), length along it, width across it, shadow sweep.
    struct Building
    {
        public string name;
        public Vector2 centre;
        public float heading, length, width, border;
        public Color roof, edge;
        public Vector2 shadow;
    }

    // Declared before Buildings: static initialisers run top to bottom.
    static readonly Color GarageRoof = new Color(0.45f, 0.35f, 0.26f);
    static readonly Color GarageEdge = new Color(0.30f, 0.23f, 0.17f);

    static readonly Building[] Buildings =
    {
        // Nine storeys, outside turn 1 next to the end of the main stand.
        new Building { name = "SpeedwayClub", centre = new Vector2(-652f, -180f), heading = 73f, length = 70f, width = 46f,
                       border = 2f, roof = new Color(0.24f, 0.26f, 0.30f), edge = new Color(0.45f, 0.48f, 0.53f), shadow = new Vector2(12f, 24f) },
        // Ten storeys, offices and condos, a long block following turn 2.
        new Building { name = "LoneStarTower", centre = new Vector2(-533.6f, -518.2f), heading = 141.8f, length = 150f, width = 28f,
                       border = 3f, roof = new Color(0.80f, 0.82f, 0.86f), edge = new Color(0.22f, 0.34f, 0.62f), shadow = new Vector2(14f, 28f) },
        // The garage compound behind the paddock: the long garage buildings and two smaller blocks, cut short where
        // Watkins Glen's paddock (pit road back to about y -154, the player's RV at y -172) stands on the rest.
        Garage("InfieldGarage_West", -190f, 21f), Garage("InfieldGarage_East", -47f, 21f),
        new Building { name = "InfieldGarage_Annex", centre = new Vector2(-254f, -205f), heading = 90f, length = 40f, width = 18f,
                       border = 1f, roof = GarageRoof, edge = GarageEdge, shadow = new Vector2(3f, 6f) },
        new Building { name = "InfieldGarage_Turn4", centre = new Vector2(125.5f, -228.5f), heading = 90f, length = 38f, width = 16f,
                       border = 1f, roof = GarageRoof, edge = GarageEdge, shadow = new Vector2(3f, 6f) },
        // Service buildings behind the back-stretch deck.
        Service("BackstretchBuilding_1", -312.9f, -275.0f, false),
        Service("BackstretchBuilding_2", -243.1f, -207.7f, false),
        Service("BackstretchBuilding_3", -180.0f, -144.6f, false),
        Service("BackstretchBuilding_4", -109.3f, -73.0f, false),
        Service("BackstretchBuilding_5", -50.4f, -9.9f, true),
        Service("BackstretchBuilding_6", 23.6f, 61.6f, false),
    };

    static Building Garage(string name, float x, float width) => new Building
    {
        name = name, centre = new Vector2(x, -211f), heading = 90f, length = 62f, width = width, border = 1.2f,
        roof = GarageRoof, edge = GarageEdge, shadow = new Vector2(4f, 8f),
    };

    static Building Service(string name, float x0, float x1, bool white) => new Building
    {
        name = name, centre = new Vector2((x0 + x1) * 0.5f, -580.5f), heading = 0f, length = x1 - x0, width = 18.5f, border = 1f,
        roof = white ? new Color(0.90f, 0.90f, 0.90f) : new Color(0.18f, 0.19f, 0.22f),
        edge = white ? new Color(0.62f, 0.62f, 0.64f) : new Color(0.32f, 0.33f, 0.36f), shadow = new Vector2(3f, 6f),
    };

    // The infield is paved from pit road back to the far side of Infield Drive, and held this far inside the
    // infield edge of the racing surface (the infield wall stands 6 m in; grass between).
    const float InfieldDriveSouthY = -283.5f;
    const float InfieldPavingInset = 14f;

    // The infield road course ("Holler Loop") and its spurs, traced off the aerial. Spurs that ran to the oval's
    // apron are cut at the infield wall.
    const float RoadCourseWidth = 9f;
    static readonly Vector2[][] RoadCourse =
    {
        new[] { V(-372.9f, -309.3f), V(-246.7f, -308.8f), V(-120.5f, -309.5f), V(5.7f, -311.1f), V(131.9f, -312.7f), V(186.6f, -314.1f),
                V(207.7f, -330.0f), V(215.4f, -363.6f), V(205.9f, -397.3f), V(174.5f, -422.7f), V(111.4f, -439.8f), V(27.3f, -450.7f),
                V(-52.6f, -453.1f), V(-86.2f, -453.2f), V(-162.0f, -434.6f), V(-204.1f, -431.9f), V(-233.6f, -432.0f), V(-288.3f, -435.2f),
                V(-372.4f, -431.3f), V(-401.9f, -423.0f), V(-423.0f, -404.2f), V(-429.4f, -379.0f), V(-423.2f, -353.7f), V(-402.3f, -328.4f) },
        new[] { V(-398.0f, -341.0f), V(-330.7f, -344.9f), V(-267.6f, -355.1f), V(-221.3f, -354.9f), V(-187.7f, -340.1f), V(-154.1f, -321.0f),
                V(-120.5f, -317.9f), V(-91.0f, -327.1f), V(-82.5f, -352.3f), V(-95.0f, -377.6f), V(-128.6f, -394.5f), V(-162.2f, -407.3f),
                V(-204.2f, -418.0f), V(-233.6f, -432.0f) },
        new[] { V(-187.3f, -433.5f), V(-166.2f, -449.4f), V(-159.8f, -470.4f), V(-159.7f, -476f) },
        new[] { V(-94.5f, -476f), V(-94.6f, -465.9f), V(-77.8f, -453.2f) },
        new[] { V(-162.2f, -407.3f), V(-120.1f, -413.4f), V(-86.3f, -430.1f), V(-56.8f, -448.9f) },
        new[] { V(54.1f, -313.8f), V(41.6f, -339.1f), V(31.2f, -368.6f), V(6.0f, -385.5f), V(-36.0f, -394.1f), V(-57.0f, -406.8f),
                V(-65.3f, -427.9f), V(-52.6f, -448.9f) },
        // Holler Loop's link north to Infield Drive.
        new[] { V(-244.7f, -280f), V(-244.6f, -308.8f) },
    };

    static Vector2 V(float x, float y) => new Vector2(x, y);

    // Colours for the flat pieces.
    static readonly Color DeckColour = new Color(0.60f, 0.60f, 0.58f);
    static readonly Color SuitesRoof = new Color(0.88f, 0.89f, 0.90f);
    static readonly Color SuitesEdge = new Color(0.56f, 0.58f, 0.62f);
    static readonly Color ScreenBody = new Color(0.10f, 0.10f, 0.12f);
    static readonly Color ScreenEdge = new Color(0.34f, 0.34f, 0.38f);
    // The infield lots are pale concrete and old asphalt from the air; the road course darker asphalt. Both tint the
    // light asphalt texture (mean grey 159/255).
    static readonly Color InfieldPavingTint = new Color(0.80f, 0.80f, 0.78f);
    static readonly Color RoadCourseTint = new Color(0.58f, 0.58f, 0.60f);
    const string AsphaltPath = "Assets/Textures/Environment/asphalt-light.png";
    static Texture2D Asphalt => AssetDatabase.LoadAssetAtPath<Texture2D>(AsphaltPath);

    // Sorting: paving under the run-off (-10) and road (0); stands at 2 (Grandstand default); buildings above.
    const int PavingSorting = -12, RoadCourseSorting = -11, DeckSorting = -11, SuitesSorting = 3, BuildingSorting = 4, ScreenSorting = 5;

    [MenuItem("Draftmaster/Tracks/Texas/Author Texas (Real Layout)")]
    public static void AuthorMenu()
    {
        string summary = Author();
        summary += "\n  " + ClearOrphanedSceneryArt();
        Debug.Log($"[TexasPackageAuthoring] {summary}");
        Directory.CreateDirectory("Temp");
        File.WriteAllText(ResultPath, summary);
    }

    public static string Author()
    {
        if (!File.Exists(PackagePath)) return $"no package at {PackagePath}";
        var contents = PrefabUtility.LoadPrefabContents(PackagePath);
        try
        {
            var package = contents.GetComponent<TrackPackage>();
            var builder = package != null ? package.Builder : null;
            if (builder == null || builder.track == null) return "FortWorth package has no TrackBuilder/geometry - left alone.";
            if (!builder.track.hasPitLane) return "FortWorth has no pit road - left alone.";

            var notes = new List<string>();
            var samples = builder.SampleCenterline();
            var world = new List<Vector2>(samples.Count);
            foreach (var s in samples) world.Add(builder.transform.TransformPoint(s.position));
            Vector2 centroid = Vector2.zero;
            foreach (var p in world) centroid += p;
            centroid /= world.Count;

            var environmentRoot = EnsureChild(contents.transform, "Environment");
            var old = environmentRoot.Find(DressingName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var dressing = new GameObject(DressingName).transform;
            dressing.SetParent(environmentRoot, false);

            // 1. Grandstands.
            var standsOld = environmentRoot.Find(GrandstandsName);
            if (standsOld != null) Object.DestroyImmediate(standsOld.gameObject);
            var standsRoot = new GameObject(GrandstandsName).transform;
            standsRoot.SetParent(environmentRoot, false);
            BuildMainStand(builder, samples, centroid, standsRoot, dressing, notes);

            // 2. Outside the track.
            var buildings = EnsureChild(dressing, "Buildings");
            BuildBackstretch(builder, samples, centroid, dressing, buildings, notes);
            foreach (var b in Buildings) AddBuilding(buildings, b);
            notes.Add($"{Buildings.Length} buildings (Speedway Club, Lone Star Tower, infield garages, back-stretch row)");

            // 3. The infield.
            BuildInfield(builder, samples, centroid, dressing, notes);

            // 4. The paddock, Watkins Glen's, behind pit road (needs the stands: the grandstand gate seats you in one).
            notes.Add("paddock: " + DaytonaPackageAuthoring.LayWatkinsPaddock(contents));

            ClearSceneryUnderDressing(contents, notes);
            foreach (var piece in contents.GetComponentsInChildren<SceneryPiece>(true)) piece.ClearArt();

            notes.Add(TexasLayoutReport.Render(contents));
            PrefabUtility.SaveAsPrefabAsset(contents, PackagePath);
            return "FortWorth authored:\n  " + string.Join("\n  ", notes);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // A FortWorth preview left in an open scene keeps its scattered pieces' art children, which are not part of the
    // prefab: when a re-author deletes a piece, Unity hands its DontSave "SceneryArt" up to the instance root. They
    // are never saved with the scene, but RaceSceneSplitter would read them as objects added to the package on the
    // next RaceScene save. Removed here, from every loaded scene, so a re-author leaves no strays behind.
    [MenuItem("Draftmaster/Tracks/Texas/Clear Orphaned Scenery Art In Open Scenes")]
    public static void ClearOrphansMenu() => Debug.Log($"[TexasPackageAuthoring] {ClearOrphanedSceneryArt()}");

    public static string ClearOrphanedSceneryArt()
    {
        int removed = 0;
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (var root in scene.GetRootGameObjects())
            {
                var package = root.GetComponent<TrackPackage>();
                if (package == null || package.trackId != TrackId) continue;
                for (int c = root.transform.childCount - 1; c >= 0; c--)
                {
                    var child = root.transform.GetChild(c);
                    if (child.name != SceneryPiece.ArtName || PrefabUtility.IsPartOfPrefabInstance(child)) continue;
                    Object.DestroyImmediate(child.gameObject);
                    removed++;
                }
            }
        }
        return $"{removed} orphaned scenery art object(s) cleared from open scenes";
    }

    // ------------------------------------------------------------------ grandstands

    // Lap distance beside a world point: the nearest centreline sample.
    static float LapDistanceNear(List<TrackBuilder.Sample> samples, TrackBuilder builder, Vector2 world)
    {
        Vector2 local = builder.transform.InverseTransformPoint(world);
        float best = float.MaxValue, at = 0f;
        foreach (var s in samples)
        {
            float d = (s.position - local).sqrMagnitude;
            if (d < best) { best = d; at = s.distance; }
        }
        return at;
    }

    // A point `beyond` metres outside the road edge (away from the infield) at lap distance d, in world space.
    static Vector2 Outside(TrackBuilder builder, List<TrackBuilder.Sample> samples, Vector2 centroid, float d, float beyond)
    {
        var s = builder.SampleAt(d, samples);
        Vector2 p = builder.transform.TransformPoint(s.position);
        Vector2 n = ((Vector2)builder.transform.TransformDirection(s.normal)).normalized;
        float outward = Vector2.Dot(p - centroid, n) >= 0f ? 1f : -1f;
        return p + n * outward * (s.width * 0.5f + beyond);
    }

    static void BuildMainStand(TrackBuilder builder, List<TrackBuilder.Sample> samples, Vector2 centroid,
                               Transform standsRoot, Transform dressing, List<string> notes)
    {
        float lap = builder.track.TotalLength();
        float from = LapDistanceNear(samples, builder, MainStandTurn4End);
        float to = LapDistanceNear(samples, builder, MainStandTurn1End);
        float run = Mathf.Repeat(to - from, lap);
        float Wrap(float d) => Mathf.Repeat(d, lap);

        // The stand's middle line and the suites roof's two edges, every 2 m down the run.
        int count = Mathf.Max(2, Mathf.CeilToInt(run / 2f) + 1);
        var middle = new List<Vector2>(count);
        var along = new List<float>(count);
        var roofIn = new List<Vector2>(count);
        var roofOut = new List<Vector2>(count);
        float total = 0f;
        for (int i = 0; i < count; i++)
        {
            float d = Wrap(from + run * i / (count - 1));
            Vector2 m = Outside(builder, samples, centroid, d, StandSetback + StandDepth * 0.5f);
            if (i > 0) total += Vector2.Distance(middle[i - 1], m);
            middle.Add(m);
            along.Add(total);
            roofIn.Add(Outside(builder, samples, centroid, d, StandSetback + StandDepth - 0.5f));
            roofOut.Add(Outside(builder, samples, centroid, d, StandSetback + StandDepth + SuitesDepth));
        }

        Vector2 At(float a)
        {
            int j = along.BinarySearch(a);
            if (j >= 0) return middle[j];
            j = ~j;
            if (j <= 0) return middle[0];
            if (j >= middle.Count) return middle[middle.Count - 1];
            return Vector2.Lerp(middle[j - 1], middle[j], Mathf.InverseLerp(along[j - 1], along[j], a));
        }

        // Pieces of about StandPieceLength, each the circular arc through its ends and middle (as
        // TrackDressingFactory's joined run), so they meet exactly and follow the tri-oval and the turns.
        var crowd = FindTexture("crowd-phoenix", "lacrowd1", "crowd");
        int pieces = Mathf.Max(1, Mathf.RoundToInt(total / StandPieceLength));
        float span = total / pieces;
        // Which side the infield is, held for the whole run: the stand faces back toward it.
        var mid = builder.SampleAt(Wrap(from + run * 0.5f), samples);
        Vector2 midPos = builder.transform.TransformPoint(mid.position);
        Vector2 midNormal = builder.transform.TransformDirection(mid.normal);
        float outward = Vector2.Dot(midPos - centroid, midNormal) >= 0f ? 1f : -1f;
        var stands = new Grandstand[pieces];
        for (int k = 0; k < pieces; k++)
        {
            Vector2 a = At(k * span), b = At((k + 1) * span), m = At((k + 0.5f) * span);
            Vector2 chord = b - a;
            float c = chord.magnitude;
            if (c < 0.01f) continue;
            Vector2 x = chord / c;
            if (outward > 0f) x = -x;
            Vector2 y = new Vector2(-x.y, x.x);
            Vector2 half = (a + b) * 0.5f;
            float h = Vector2.Dot(half - m, y);
            float bend = 4f * Mathf.Atan(2f * h / c);
            float length = Mathf.Abs(bend) < 1e-4f ? c : bend * c / (2f * Mathf.Sin(bend * 0.5f));
            Vector2 pos = half - y * h;

            var go = new GameObject($"Grandstand_Main_{k}");
            go.transform.SetParent(standsRoot, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(x.y, x.x) * Mathf.Rad2Deg);
            var stand = go.AddComponent<Grandstand>();
            stand.crowdTexture = crowd;
            stand.length = length + 2f * StandPieceOverlap;
            stand.depth = StandDepth;
            stand.flipFacing = outward > 0f;
            stand.bendDegrees = bend * Mathf.Rad2Deg;
            stands[k] = stand;
        }
        // One crowd texture down the whole run, carried across every joint.
        float offset = 0f;
        for (int i = 0; i < pieces; i++)
        {
            var stand = stands[outward > 0f ? pieces - 1 - i : i];
            if (stand == null) continue;
            stand.textureOffset = offset - StandPieceOverlap;   // the overlap carries on its neighbour's crowd
            offset += stand.length - 2f * StandPieceOverlap;
            stand.Build();
        }

        // The suites roof: one band behind the seating, from end to end.
        var roof = new List<Vector2>(roofIn);
        for (int i = roofOut.Count - 1; i >= 0; i--) roof.Add(roofOut[i]);
        var buildings = EnsureChild(dressing, "Buildings");
        AddShape(buildings, "MainGrandstandSuites", roof.ToArray(), SuitesRoof, SuitesSorting, 1.5f, SuitesEdge, Vector2.zero);

        notes.Add($"main grandstand {total:0} m round the front stretch (lap {from:0} -> {to:0}), {pieces} pieces, " +
                  $"{StandDepth} m seating + {SuitesDepth} m suites, {StandSetback} m off the road");
    }

    // ------------------------------------------------------------------ back stretch

    static void BuildBackstretch(TrackBuilder builder, List<TrackBuilder.Sample> samples, Vector2 centroid,
                                 Transform dressing, Transform buildings, List<string> notes)
    {
        float lap = builder.track.TotalLength();
        float from = LapDistanceNear(samples, builder, DeckTurn2End);
        float to = LapDistanceNear(samples, builder, DeckTurn3End);
        float run = Mathf.Repeat(to - from, lap);
        int count = Mathf.Max(2, Mathf.CeilToInt(run / 4f) + 1);
        var near = new List<Vector2>();
        var far = new List<Vector2>();
        for (int i = 0; i < count; i++)
        {
            float d = Mathf.Repeat(from + run * i / (count - 1), lap);
            near.Add(Outside(builder, samples, centroid, d, DeckFrom));
            far.Add(Outside(builder, samples, centroid, d, DeckTo));
        }
        var deck = new List<Vector2>(near);
        for (int i = far.Count - 1; i >= 0; i--) deck.Add(far[i]);
        AddShape(dressing, "BackstretchDeck", deck.ToArray(), DeckColour, DeckSorting, 0f, default, Vector2.zero);

        // Big Hoss TV, square to the back stretch at its middle.
        float hossAt = LapDistanceNear(samples, builder, new Vector2(BigHossCentreX, DeckTurn2End.y));
        Vector2 centre = Outside(builder, samples, centroid, hossAt, BigHossBeyondEdge);
        var s = builder.SampleAt(hossAt, samples);
        Vector2 t = ((Vector2)builder.transform.TransformDirection(s.tangent)).normalized;
        AddBuilding(buildings, new Building
        {
            name = "BigHossTV", centre = centre, heading = Mathf.Atan2(t.y, t.x) * Mathf.Rad2Deg,
            length = BigHossLength, width = BigHossThickness, border = 0.8f, roof = ScreenBody, edge = ScreenEdge,
            shadow = new Vector2(6f, 14f),
        }, ScreenSorting);
        notes.Add($"back stretch: deck lap {from:0} -> {to:0} ({DeckFrom}-{DeckTo} m out), Big Hoss TV at {centre}");
    }

    // ------------------------------------------------------------------ infield

    static void BuildInfield(TrackBuilder builder, List<TrackBuilder.Sample> samples, Vector2 centroid,
                             Transform dressing, List<string> notes)
    {
        // The infield edge of the racing surface, pulled in by the inset: a closed loop round the infield.
        var loop = new List<Vector2>();
        for (int i = 0; i < samples.Count; i += 2)
        {
            var s = samples[i];
            Vector2 p = builder.transform.TransformPoint(s.position);
            Vector2 n = ((Vector2)builder.transform.TransformDirection(s.normal)).normalized;
            float inward = Vector2.Dot(centroid - p, n) >= 0f ? 1f : -1f;
            loop.Add(p + n * inward * (s.width * 0.5f + InfieldPavingInset));
        }

        // Behind pit road: the infield-side edge of the box lane along the straight of pit road (the chord), and
        // north of the far side of Infield Drive.
        var track = builder.track;
        var pit = builder.SamplePitCenterline();
        float entryArc = track.pitSegments != null && track.pitSegments.Length == 3 ? track.pitSegments[0].length : 0f;
        float chordLen = track.pitSegments != null && track.pitSegments.Length == 3 ? track.pitSegments[1].length : 0f;
        if (pit.Count < 2 || chordLen <= 0f)
        {
            notes.Add("infield: pit road is not entry arc + straight + exit arc - no paving");
            return;
        }
        Vector2 chordA = builder.transform.TransformPoint(builder.SamplePitAt(entryArc + 0.01f, pit).position);
        Vector2 chordB = builder.transform.TransformPoint(builder.SamplePitAt(entryArc + chordLen - 0.01f, pit).position);
        Vector2 along = (chordB - chordA).normalized;
        Vector2 outward = new Vector2(-along.y, along.x);
        if (Vector2.Dot(centroid - chordA, outward) < 0f) outward = -outward;   // toward the infield
        float behind = track.pitDefaultWidth * 0.5f + builder.pitBoxLaneWidth;
        var paving = ClipHalfPlane(loop, chordA + outward * behind, outward);
        paving = ClipHalfPlane(paving, new Vector2(0f, InfieldDriveSouthY), Vector2.up);
        if (paving.Count >= 3)
        {
            var go = AddShape(dressing, "InfieldPaving", paving.ToArray(), InfieldPavingTint, PavingSorting, 0f, default, Vector2.zero);
            go.texture = Asphalt;
            go.Build();
            notes.Add($"infield paved from pit road to Infield Drive ({paving.Count} corners, {(Asphalt != null ? Asphalt.name : "NO TEXTURE - flat")})");
        }

        var roads = EnsureChild(dressing, "InfieldRoadCourse");
        for (int i = 0; i < RoadCourse.Length; i++)
        {
            var go = new GameObject(i == 0 ? "HollerLoop" : $"HollerLoop_Link_{i}");
            go.transform.SetParent(roads, false);
            var shape = go.AddComponent<TrackDressingShape>();
            shape.kind = TrackDressingShape.Kind.Path;
            shape.points = RoadCourse[i];
            shape.closedPath = i == 0;
            shape.pathWidth = RoadCourseWidth;
            shape.texture = Asphalt;
            shape.color = RoadCourseTint;
            shape.sortingOrder = RoadCourseSorting;
            shape.Build();
        }
        notes.Add($"infield road course: {RoadCourse.Length} paths, {RoadCourseWidth} m wide");
    }

    // ------------------------------------------------------------------ pieces

    static TrackDressingShape AddShape(Transform parent, string name, Vector2[] points, Color colour, int sorting,
                                       float border, Color borderColour, Vector2 shadow)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var shape = go.AddComponent<TrackDressingShape>();
        shape.kind = TrackDressingShape.Kind.Polygon;
        shape.points = points;
        shape.color = colour;
        shape.sortingOrder = sorting;
        shape.borderWidth = border;
        shape.borderColor = borderColour;
        shape.shadowOffset = shadow;
        shape.Build();
        return shape;
    }

    static void AddBuilding(Transform parent, Building b, int sorting = BuildingSorting)
    {
        float r = b.heading * Mathf.Deg2Rad;
        Vector2 x = new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * (b.length * 0.5f);
        Vector2 y = new Vector2(-x.y, x.x).normalized * (b.width * 0.5f);
        var pts = new[] { b.centre - x - y, b.centre + x - y, b.centre + x + y, b.centre - x + y };
        AddShape(parent, b.name, pts, b.roof, sorting, b.border, b.edge, b.shadow);
    }

    // Unlocked scattered scenery under anything placed here goes; a locked piece was kept on purpose and stays.
    static void ClearSceneryUnderDressing(GameObject contents, List<string> notes)
    {
        const float Pad = 4f;
        var stands = contents.GetComponentsInChildren<Grandstand>(true);
        var shapes = contents.GetComponentsInChildren<TrackDressingShape>(true);
        int removed = 0;
        var locked = new List<string>();
        var pieces = new List<SceneryPiece>();
        foreach (var scenery in contents.GetComponentsInChildren<TrackScenery>(true))
            foreach (Transform child in scenery.transform)
                if (child.TryGetComponent(out SceneryPiece p)) pieces.Add(p);
        foreach (var piece in pieces)
        {
            Vector2 at = piece.transform.position;
            bool under = false;
            foreach (var stand in stands)
                if (stand.Covers(stand.transform.InverseTransformPoint(at), Pad)) { under = true; break; }
            if (!under)
                foreach (var shape in shapes)
                    if (shape.Covers(at, Pad)) { under = true; break; }
            if (!under) continue;
            if (piece.locked) { locked.Add(piece.name); continue; }
            Object.DestroyImmediate(piece.gameObject);
            removed++;
        }
        notes.Add($"{removed} scattered scenery pieces under the new stands/buildings/paving removed" +
                  (locked.Count > 0 ? $"; LOCKED pieces kept under them: {string.Join(", ", locked)}" : ""));
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

    static Transform EnsureChild(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t != null) return t;
        t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    // Exact name first, then the first fuzzy hit (as TrackDressingFactory finds the crowd texture).
    static Texture2D FindTexture(params string[] names)
    {
        foreach (var n in names)
        {
            Texture2D fuzzy = null;
            foreach (var guid in AssetDatabase.FindAssets($"{n} t:Texture2D"))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                if (tex == null) continue;
                if (tex.name == n) return tex;
                if (fuzzy == null) fuzzy = tex;
            }
            if (fuzzy != null) return fuzzy;
        }
        return null;
    }
}
