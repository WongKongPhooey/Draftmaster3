using System.Collections.Generic;
using System.IO;
using Draftmaster.Data;
using Draftmaster.Tracks;
using UnityEditor;
using UnityEngine;

// Authoring tools for the track pipeline: turn a catalogue row into a drivable layout, and a layout into a
// content package the shared race scene can load.
//
// The intended loop for adding track number 3 through 35:
//
//   1. Make sure it's in the catalogue (Assets/Scripts/Database/DummyTracks.cs — most of the calendar is
//      already there with its type, length, banking and lap count).
//   2. Draftmaster > Tracks > Track Builder Window, pick it, press Generate Layout. That writes
//      Resources/Tracks/<id>.asset from OvalTrackFactory — a closed, drivable oval with a racing line and
//      a pit road. Road courses - and Pocono's triangle - come from an authored corner
//      sequence in RoadCourseLayouts instead; WatkinsGlen is hand-measured and is skipped.
//   3. Press Build Package, which writes Resources/TrackPackages/<id>.prefab: a TrackPackage with the road
//      and a ground plane, ready to have scenery dropped into it in Prefab Mode.
//   4. Open the package prefab, dress it — grandstands, environment, paddock, spawn markers.
//   5. Race it: TrackSelection.Select("<id>") and load the race scene.
public static class TrackAuthoringMenu
{
    const string GeometryDir = "Assets/Resources/Tracks";
    const string PackageDir = "Assets/Resources/TrackPackages";

    [MenuItem("Draftmaster/Tracks/Track Builder Window")]
    public static void OpenWindow() => TrackBuilderWindow.Open();

    // ---------------------------------------------------------------- geometry

    // Tracks whose geometry was authored by hand and must never be generated over. Watkins Glen was
    // measured off satellite imagery — a generated approximation would be a straight downgrade.
    public static bool IsHandAuthored(string trackId) => trackId == RoadCourseLayouts.HandAuthored;

    // An authored corner sequence always wins over the oval solver, whatever the catalogue TYPE says.
    // That is not just for road courses: Pocono is catalogued as a speedway and is a triangle, which no
    // oval formula closes, so it is authored too.
    public static bool UsesAuthoredLayout(string trackId) => RoadCourseFactory.CanBuild(trackId);

    public static bool CanGenerate(Track row)
    {
        if (row == null || IsHandAuthored(row.Name)) return false;
        if (UsesAuthoredLayout(row.Name)) return true;
        return row.Type != TrackType.RoadCourse;
    }

    // Writes (or refills) Resources/Tracks/<id>.asset for a catalogue row. Refilling keeps the asset's GUID
    // so scenes and packages already pointing at it stay wired.
    //
    // Ovals are solved from their published length and banking (OvalTrackFactory); road courses are built
    // from an authored corner sequence (RoadCourseFactory). Both land on the same TrackInfoV2, so nothing
    // downstream needs to know which path a track came down.
    public static TrackInfoV2 GenerateGeometry(Track row, bool overwrite)
    {
        if (row == null) return null;

        if (IsHandAuthored(row.Name))
        {
            Debug.Log($"Tracks: {row.Name} is hand-measured — skipped, deliberately. Its asset is the " +
                      "reference the generated circuits are aiming at.");
            return AssetDatabase.LoadAssetAtPath<TrackInfoV2>($"{GeometryDir}/{row.Name}.asset");
        }

        bool isRoad = UsesAuthoredLayout(row.Name);
        if (!isRoad && row.Type == TrackType.RoadCourse)
        {
            Debug.LogWarning($"Tracks: {row.Name} is a road course with no authored layout. Add its corner " +
                             "sequence to RoadCourseLayouts, or author the asset by hand.");
            return null;
        }

        Directory.CreateDirectory(GeometryDir);
        string path = $"{GeometryDir}/{row.Name}.asset";

        var existing = AssetDatabase.LoadAssetAtPath<TrackInfoV2>(path);
        if (existing != null && !overwrite)
        {
            Debug.Log($"Tracks: {row.Name} already has a layout at {path} — left alone.");
            return existing;
        }

        var roadSpec = isRoad ? RoadCourseLayouts.Spec(row.Name) : null;
        var ovalSpec = isRoad ? null : OvalTrackFactory.FromCatalogue(row);

        TrackInfoV2 asset;
        if (existing != null)
        {
            if (isRoad) RoadCourseFactory.Populate(existing, roadSpec);
            else OvalTrackFactory.Populate(existing, ovalSpec);
            EditorUtility.SetDirty(existing);
            asset = existing;
        }
        else
        {
            asset = isRoad ? RoadCourseFactory.Build(roadSpec) : OvalTrackFactory.Build(ovalSpec);
            AssetDatabase.CreateAsset(asset, path);
        }

        AssetDatabase.SaveAssets();
        string summary = isRoad
            ? RoadCourseGeometry.Validate(RoadCourseGeometry.Solve(roadSpec)).Summary
            : OvalTrackFactory.Validate(asset).Summary;
        Debug.Log($"Tracks: {row.DisplayName} layout written to {path} — {summary}, " +
                  $"{asset.defaultWidth:0.0} m wide", asset);
        return asset;
    }

    // ---------------------------------------------------------------- package

    // Builds Resources/TrackPackages/<id>.prefab: the per-track half of a race scene. Deliberately sparse —
    // a road, a ground plane and the component that binds them — because the rest is art the designer adds
    // in Prefab Mode.
    public static GameObject BuildPackage(string trackId, bool overwrite)
    {
        if (string.IsNullOrEmpty(trackId)) return null;

        var geometry = AssetDatabase.LoadAssetAtPath<TrackInfoV2>($"{GeometryDir}/{trackId}.asset");
        if (geometry == null)
        {
            Debug.LogError($"Tracks: no layout at {GeometryDir}/{trackId}.asset — generate one first.");
            return null;
        }

        Directory.CreateDirectory(PackageDir);
        string path = $"{PackageDir}/{trackId}.prefab";
        if (File.Exists(path) && !overwrite)
        {
            Debug.Log($"Tracks: {trackId} already has a package at {path} — left alone.");
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        var root = new GameObject($"Track_{trackId}");
        var package = root.AddComponent<TrackPackage>();
        package.trackId = trackId;

        // The road itself, matching how the reference scene is put together (TrackBuilder on its own object
        // with a MeshFilter/MeshRenderer, rebuilt from the TrackInfoV2).
        var trackGo = new GameObject("Track");
        trackGo.transform.SetParent(root.transform, false);
        trackGo.AddComponent<MeshFilter>();
        var renderer = trackGo.AddComponent<MeshRenderer>();
        var builder = trackGo.AddComponent<TrackBuilder>();
        builder.track = geometry;
        builder.surfaceMaterial = FindMaterial("Asphalt", "Track", "Road");
        if (builder.surfaceMaterial != null) renderer.sharedMaterial = builder.surfaceMaterial;
        package.trackBuilder = builder;

        // Somewhere for the scenery to go, so a designer opening the prefab has an obvious home for it.
        var environment = new GameObject("Environment");
        environment.transform.SetParent(root.transform, false);
        package.environmentRoot = environment.transform;

        var paddock = new GameObject("Paddock");
        paddock.transform.SetParent(root.transform, false);
        package.paddockRoot = paddock.transform;

        builder.Build();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();

        // A road on its own isn't a racetrack — ground, walls, stands and a paddock all follow from the
        // geometry, so generate them now rather than leaving thirty-four tracks of manual dressing to do.
        string dressed = TrackDressingFactory.Dress(trackId, overwrite: false);

        Debug.Log($"Tracks: package written to {path}. {dressed}\n" +
                  "Open it to adjust the Environment/Paddock roots by hand.", prefab);
        return prefab;
    }

    static Material FindMaterial(params string[] nameHints)
    {
        foreach (var hint in nameHints)
        {
            var guids = AssetDatabase.FindAssets($"{hint} t:Material");
            if (guids.Length == 0) continue;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[0]));
            if (mat != null) return mat;
        }
        return null;
    }

    // ---------------------------------------------------------------- one-shot menu items

    [MenuItem("Draftmaster/Tracks/Create Starter Layouts (Daytona + Martinsville)")]
    public static void CreateDemoTracks()
    {
        foreach (var id in new[] { "Daytona", "Martinsville" })
        {
            var row = TrackCatalog.Row(id);
            if (row == null) { Debug.LogError($"Tracks: '{id}' is not in the catalogue."); continue; }
            var geometry = GenerateGeometry(row, overwrite: true);
            if (geometry != null) BuildPackage(id, overwrite: true);
        }
        AssetDatabase.Refresh();
    }

    // The whole calendar in one press: layout, package and dressing for every venue the three
    // championships visit. No dialogs — this is run from a menu and from tests, and a modal here would
    // wedge the editor.
    //
    // Existing packages are left alone (the geometry inside them is refilled in place, so their GUIDs and
    // any hand-dressing survive), and WatkinsGlen is skipped outright.
    [MenuItem("Draftmaster/Tracks/Build All Calendar Tracks")]
    public static void BuildAllCalendarTracks() => BuildAll(rebuildPackages: false);

    // Same, but throws away and rebuilds each package prefab. Use after changing the dressing factory;
    // it discards hand edits made inside the generated Environment/Paddock roots.
    [MenuItem("Draftmaster/Tracks/Rebuild All Calendar Tracks (replace packages)")]
    public static void RebuildAllCalendarTracks() => BuildAll(rebuildPackages: true);

    public static string BuildAll(bool rebuildPackages)
    {
        var built = new List<string>();
        var skipped = new List<string>();
        var failed = new List<string>();

        // No AssetDatabase.StartAssetEditing() around this loop, deliberately. It pauses importing, so
        // the .asset GenerateGeometry has just written cannot be loaded back by BuildPackage on the very
        // next line — every package silently failed with "no layout, generate one first" until this went.
        foreach (var dim in TrackDimensions.All)
        {
            var row = TrackCatalog.Row(dim.id);
            if (row == null) { failed.Add($"{dim.id} (not in the catalogue)"); continue; }
            if (IsHandAuthored(dim.id)) { skipped.Add($"{dim.id} (hand-measured)"); continue; }
            if (!CanGenerate(row)) { skipped.Add($"{dim.id} (no authored layout)"); continue; }

            var geometry = GenerateGeometry(row, overwrite: true);
            if (geometry == null) { failed.Add($"{dim.id} (no geometry)"); continue; }

            bool hadPackage = File.Exists($"{PackageDir}/{dim.id}.prefab");
            var package = BuildPackage(dim.id, overwrite: rebuildPackages || !hadPackage);
            if (package == null) { failed.Add($"{dim.id} (no package)"); continue; }

            built.Add(dim.id);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string report = $"Tracks: built {built.Count} — {string.Join(", ", built)}"
                      + (skipped.Count > 0 ? $"\nSkipped {skipped.Count}: {string.Join(", ", skipped)}" : "")
                      + (failed.Count > 0 ? $"\nFAILED {failed.Count}: {string.Join(", ", failed)}" : "");
        Debug.Log(report);
        return report;
    }

    // What each venue is, how big it is and who races there — the table the layouts are generated from,
    // printed so it can be checked against the real thing without opening the source.
    [MenuItem("Draftmaster/Tracks/Report Track Dimensions")]
    public static void ReportDimensions()
    {
        var lines = new List<string>
        {
            $"{"track",-18}{"type",-15}{"length",9}{"width",9}{"bank",7}  series  source"
        };
        foreach (var dim in TrackDimensions.All)
        {
            string series = ((dim.series & SeriesVisits.Cup) != 0 ? "C" : "-")
                          + ((dim.series & SeriesVisits.National) != 0 ? "N" : "-")
                          + ((dim.series & SeriesVisits.Trucks) != 0 ? "T" : "-");
            lines.Add($"{dim.id,-18}{dim.kind,-15}{dim.lapMiles,8:0.###}mi{dim.widthMetres,8:0.0}m" +
                      $"{dim.turnBankingDeg,6:0.#}°  {series}     {dim.confidence}");
        }
        Debug.Log($"Track dimensions — {TrackDimensions.All.Count} venues:\n" + string.Join("\n", lines));
    }

    // Why the next race will build what it builds.
    //
    // "I picked a track and still ended up at Watkins Glen" has several possible causes and they are
    // invisible from the window: the selection may never have been stored, something else may have
    // overwritten it, or the title screen row you pressed may deliberately reset it. This prints the whole
    // chain so the answer is one click away.
    [MenuItem("Draftmaster/Tracks/Report Current Selection")]
    public static void ReportCurrentSelection()
    {
        const string key = "track.current";
        string saved = PlayerPrefs.GetString(key, "");
        string travel = TravelState.CurrentNodeId;
        string resolved = TrackSelection.CurrentId;

        string why;
        if (!string.IsNullOrEmpty(saved)) why = $"the saved selection ('{saved}')";
        else if (!string.IsNullOrEmpty(travel) && TrackCatalog.Row(travel) != null)
            why = $"the travel map's current location ('{travel}') - nothing has been selected";
        else why = $"the fallback default ('{TrackCatalog.DefaultTrackId}') - nothing selected, no travel location";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Track selection:");
        sb.AppendLine($"  PlayerPrefs['{key}'] = {(string.IsNullOrEmpty(saved) ? "<not set>" : saved)}");
        sb.AppendLine($"  TravelState.CurrentNodeId = {(string.IsNullOrEmpty(travel) ? "<not set>" : travel)}");
        sb.AppendLine($"  -> next race builds {TrackCatalog.DisplayName(resolved)} ({resolved}), from {why}.");
        sb.AppendLine($"  geometry present: {TrackCatalog.HasGeometry(resolved)}, " +
                      $"package present: {TrackCatalog.HasPackage(resolved)}");
        sb.AppendLine();
        sb.AppendLine("  Prefs live under company/product: " +
                      $"{Application.companyName} / {Application.productName}");
        sb.AppendLine();
        sb.AppendLine("  On the title screen: CONTINUE and EXHIBITION race the selection above.");
        sb.AppendLine("  NEW SEASON deliberately restarts the calendar at its opening round, which");
        sb.AppendLine("  OVERWRITES the selection - that is the usual reason a picked track is ignored.");
        Debug.Log(sb.ToString());
    }

    [MenuItem("Draftmaster/Tracks/Report Calendar Coverage")]
    public static void ReportCoverage()
    {
        var built = new List<string>();
        var catalogueOnly = new List<string>();
        foreach (var row in TrackCatalog.All)
        {
            bool geometry = File.Exists($"{GeometryDir}/{row.Name}.asset");
            bool package = File.Exists($"{PackageDir}/{row.Name}.prefab");
            string mark = geometry && package ? "layout + package" : geometry ? "layout only" : "catalogue only";
            (geometry ? built : catalogueOnly).Add($"  {row.Name,-18} {row.Type,-14} {row.LengthMiles:0.###} mi  — {mark}");
        }

        Debug.Log($"Track coverage — {built.Count} built, {catalogueOnly.Count} still to do:\n" +
                  string.Join("\n", built) + (built.Count > 0 && catalogueOnly.Count > 0 ? "\n" : "") +
                  string.Join("\n", catalogueOnly));
    }
}

// Pick a track from the catalogue and build it. Kept as a window rather than a pile of menu items because
// with 30-odd rounds the list is the useful part: it shows at a glance what's built and what isn't.
public class TrackBuilderWindow : EditorWindow
{
    Vector2 _scroll;
    bool _overwrite;
    bool _builtOnly;
    string _search = "";

    public static void Open()
    {
        var window = GetWindow<TrackBuilderWindow>(false, "Tracks", true);
        window.minSize = new Vector2(620f, 360f);
        window.Show();
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Every venue on the Cup / National / Truck calendars. Ovals are solved from their published " +
            "length and banking; road courses (and Pocono) come from an authored corner sequence in " +
            "RoadCourseLayouts. Watkins Glen is hand-measured and is never regenerated.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            _search = EditorGUILayout.TextField("Search", _search);
            _builtOnly = EditorGUILayout.ToggleLeft("Built only", _builtOnly, GUILayout.Width(90f));
            if (GUILayout.Button("Refresh", GUILayout.Width(70f))) TrackCatalog.Invalidate();
        }

        _overwrite = EditorGUILayout.ToggleLeft(
            "Overwrite existing layouts (keeps the asset GUID, so scene references survive)", _overwrite);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Build All Calendar Tracks")) TrackAuthoringMenu.BuildAllCalendarTracks();
            if (GUILayout.Button("Report Dimensions")) TrackAuthoringMenu.ReportDimensions();
            if (GUILayout.Button("Report Coverage")) TrackAuthoringMenu.ReportCoverage();
        }

        EditorGUILayout.Space();
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        string current = TrackSelection.CurrentId;
        TrackType? lastType = null;
        int shown = 0, built = 0, total = 0;

        foreach (var row in TrackCatalog.All)
        {
            bool hasGeometry = File.Exists($"Assets/Resources/Tracks/{row.Name}.asset");
            bool hasPackage = File.Exists($"Assets/Resources/TrackPackages/{row.Name}.prefab");

            total++;
            if (hasGeometry && hasPackage) built++;

            if (_builtOnly && !hasGeometry) continue;
            if (!string.IsNullOrEmpty(_search) &&
                row.DisplayName.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) < 0 &&
                row.Name.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) < 0) continue;

            shown++;
            if (lastType != row.Type)
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField(row.Type.ToString(), EditorStyles.boldLabel);
                lastType = row.Type;
            }

            EditorGUILayout.BeginHorizontal();

            // The track selected for the next race is the one the race scene will actually build, so it is
            // worth being able to spot it without reading the footer.
            bool isCurrent = row.Name == current;
            EditorGUILayout.LabelField(isCurrent ? $"▶ {row.DisplayName}" : $"   {row.DisplayName}",
                                       isCurrent ? EditorStyles.boldLabel : EditorStyles.label,
                                       GUILayout.Width(250f));

            // Width is the number this pipeline exists to get right, so it earns a column.
            string width = TrackDimensions.TryGet(row.Name, out var dim) ? $"{dim.widthMetres:0.0} m" : "—";
            EditorGUILayout.LabelField($"{row.LengthMiles:0.###} mi · {row.BankingDegrees}°", GUILayout.Width(90f));
            EditorGUILayout.LabelField(width, GUILayout.Width(50f));
            EditorGUILayout.LabelField(hasGeometry ? (hasPackage ? "built" : "layout") : "—", GUILayout.Width(46f));

            using (new EditorGUI.DisabledScope(!TrackAuthoringMenu.CanGenerate(row)))
            {
                if (GUILayout.Button("Generate Layout", GUILayout.Width(114f)))
                    TrackAuthoringMenu.GenerateGeometry(row, _overwrite || !hasGeometry);
            }
            using (new EditorGUI.DisabledScope(!hasGeometry))
            {
                if (GUILayout.Button("Build Package", GUILayout.Width(104f)))
                    TrackAuthoringMenu.BuildPackage(row.Name, _overwrite || !hasPackage);
            }
            using (new EditorGUI.DisabledScope(!hasPackage))
            {
                if (GUILayout.Button("Dress", GUILayout.Width(52f)))
                    Debug.Log("Tracks: " + TrackDressingFactory.Dress(row.Name, _overwrite));
            }
            using (new EditorGUI.DisabledScope(!hasGeometry))
            {
                if (GUILayout.Button("Race", GUILayout.Width(46f)))
                {
                    // Report what actually happened. This used to log success unconditionally, ignoring
                    // Select()'s return value, so a rejected selection still said "next race scene will
                    // build X" and the race then loaded somewhere else with no clue why.
                    if (TrackSelection.Select(row.Name))
                        Debug.Log($"Tracks: next race scene will build {row.DisplayName}. " +
                                  "Load RaceScene, or press Play and pick CONTINUE / EXHIBITION on the " +
                                  "title screen — NEW SEASON deliberately restarts at the season opener.");
                    else
                        Debug.LogError($"Tracks: could not select {row.DisplayName} — see the warning above.");
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        if (shown == 0)
            EditorGUILayout.LabelField("Nothing matches that search.", EditorStyles.miniLabel);

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            $"{built} of {total} catalogue rows built · showing {shown} · " +
            $"next race: {TrackSelection.CurrentDisplayName}", EditorStyles.miniLabel);
    }
}
