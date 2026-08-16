using System.Collections.Generic;
using System.IO;
using Draftmaster.Data;
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
//      a pit road. (Road courses are hand-authored; the generator refuses them.)
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

    // Writes (or refills) Resources/Tracks/<id>.asset for a catalogue row. Refilling keeps the asset's GUID
    // so scenes and packages already pointing at it stay wired.
    public static TrackInfoV2 GenerateGeometry(Track row, bool overwrite)
    {
        if (row == null) return null;
        if (row.Type == TrackType.RoadCourse)
        {
            Debug.LogWarning($"Tracks: {row.Name} is a road course — there's no formula for one. " +
                             "Author it by hand (duplicate WatkinsGlen.asset as a starting point).");
            return null;
        }

        Directory.CreateDirectory(GeometryDir);
        string path = $"{GeometryDir}/{row.Name}.asset";
        var spec = OvalTrackFactory.FromCatalogue(row);

        var existing = AssetDatabase.LoadAssetAtPath<TrackInfoV2>(path);
        if (existing != null && !overwrite)
        {
            Debug.Log($"Tracks: {row.Name} already has a layout at {path} — left alone.");
            return existing;
        }

        TrackInfoV2 asset;
        if (existing != null)
        {
            OvalTrackFactory.Populate(existing, spec);
            EditorUtility.SetDirty(existing);
            asset = existing;
        }
        else
        {
            asset = OvalTrackFactory.Build(spec);
            AssetDatabase.CreateAsset(asset, path);
        }

        AssetDatabase.SaveAssets();
        var check = OvalTrackFactory.Validate(asset);
        Debug.Log($"Tracks: {row.DisplayName} layout written to {path} — {check.Summary}", asset);
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

    public static void Open()
    {
        var window = GetWindow<TrackBuilderWindow>(false, "Tracks", true);
        window.minSize = new Vector2(520f, 320f);
        window.Show();
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Generate a starting layout for any oval on the calendar, then a package prefab the shared race " +
            "scene can load. Road courses are hand-authored — the generator skips them.",
            MessageType.Info);

        _overwrite = EditorGUILayout.ToggleLeft(
            "Overwrite existing layouts (keeps the asset GUID, so scene references survive)", _overwrite);

        EditorGUILayout.Space();
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        TrackType? lastType = null;
        foreach (var row in TrackCatalog.All)
        {
            if (lastType != row.Type)
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField(row.Type.ToString(), EditorStyles.boldLabel);
                lastType = row.Type;
            }

            bool hasGeometry = File.Exists($"Assets/Resources/Tracks/{row.Name}.asset");
            bool hasPackage = File.Exists($"Assets/Resources/TrackPackages/{row.Name}.prefab");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{row.DisplayName}", GUILayout.Width(240f));
            EditorGUILayout.LabelField($"{row.LengthMiles:0.###} mi · {row.BankingDegrees}°", GUILayout.Width(90f));
            EditorGUILayout.LabelField(hasGeometry ? (hasPackage ? "built" : "layout") : "—", GUILayout.Width(50f));

            using (new EditorGUI.DisabledScope(row.Type == TrackType.RoadCourse))
            {
                if (GUILayout.Button("Generate Layout", GUILayout.Width(120f)))
                    TrackAuthoringMenu.GenerateGeometry(row, _overwrite || !hasGeometry);
            }
            using (new EditorGUI.DisabledScope(!hasGeometry))
            {
                if (GUILayout.Button("Build Package", GUILayout.Width(110f)))
                    TrackAuthoringMenu.BuildPackage(row.Name, _overwrite || !hasPackage);
            }
            using (new EditorGUI.DisabledScope(!hasPackage))
            {
                if (GUILayout.Button("Dress", GUILayout.Width(60f)))
                    Debug.Log("Tracks: " + TrackDressingFactory.Dress(row.Name, _overwrite));
            }
            using (new EditorGUI.DisabledScope(!hasGeometry))
            {
                if (GUILayout.Button("Race", GUILayout.Width(50f)))
                {
                    TrackSelection.Select(row.Name);
                    Debug.Log($"Tracks: next race scene will build {row.DisplayName}.");
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Selected for the next race: {TrackSelection.CurrentDisplayName}", EditorStyles.miniLabel);
    }
}
