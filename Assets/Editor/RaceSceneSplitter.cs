using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Turns the authored-in-place reference scene into the shared race scene the package pipeline expects.
//
// WatkinsGlen holds two things at once: the ~20 manager objects every race needs (player car, GridSpawner,
// PitLaneStart, directors, HUDs, camera, database) and the Watkins Glen track itself (road, environment,
// ground, grandstands, paddock boundary, spawn markers, RV, the extra splines). While the road lives in the
// scene, TrackSceneLoader adopts it and no package can ever load — and every manager's `TrackBuilder` field
// is serialised to that road, so binding does nothing either (BindSceneReferences only fills nulls).
//
// So: copy the scene to RaceScene.unity, lift the track half of it into Resources/TrackPackages/
// WatkinsGlen.prefab, and delete it from the copy. What's left is a scene with no road and null TrackBuilder
// fields — which is exactly what TrackSceneLoader wants. Watkins then loads the same way Daytona does.
//
// WatkinsGlen.unity itself is never written to; it stays as the authored reference.
public static class RaceSceneSplitter
{
    const string SourceScene = "Assets/Scenes/WatkinsGlen.unity";
    const string RaceScenePath = "Assets/Scenes/RaceScene.unity";
    const string PackageDir = "Assets/Resources/TrackPackages";
    const string PackageTrackId = "WatkinsGlen";

    // Roots that belong to Watkins Glen alone, identified by a component only a track carries.
    static readonly System.Type[] TrackComponents =
    {
        typeof(TrackBuilder),
        typeof(TrackEnvironmentBuilder),
        typeof(ExtraTrackSpline),
        typeof(Grandstand),
        typeof(PaddockBoundary),
        typeof(RVExterior),
    };

    // ...plus the ones that carry nothing but a transform or a renderer, so they have to go by name.
    static readonly string[] TrackNames = { "Ground", "PlayerSpawnPoints", "TrackReferenceImage" };

    [MenuItem("Draftmaster/Tracks/Split Shared Race Scene (WatkinsGlen → package)")]
    public static void Split()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var log = new List<string>();
        var scene = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);

        // Save As first: from here on every edit lands in RaceScene.unity, never in the reference scene.
        if (!EditorSceneManager.SaveScene(scene, RaceScenePath))
        {
            Debug.LogError($"RaceSceneSplitter: could not save {RaceScenePath}.");
            return;
        }
        log.Add($"scene copied: {SourceScene} -> {RaceScenePath}");

        var roots = scene.GetRootGameObjects();
        var trackRoots = roots.Where(IsTrackRoot).ToList();
        var kept = roots.Where(r => !IsTrackRoot(r)).Select(r => r.name).ToList();
        if (trackRoots.Count == 0)
        {
            Debug.LogError("RaceSceneSplitter: no track objects found in the scene — nothing to lift.");
            return;
        }

        Directory.CreateDirectory(PackageDir);
        string packagePath = $"{PackageDir}/{PackageTrackId}.prefab";

        var packageRoot = new GameObject($"Track_{PackageTrackId}");
        var package = packageRoot.AddComponent<TrackPackage>();
        package.trackId = PackageTrackId;

        // worldPositionStays: the package is authored at the origin, so the track keeps the coordinates the
        // whole scene (spawn points, paddock polygon, grandstands) was laid out against.
        foreach (var root in trackRoots)
        {
            root.transform.SetParent(packageRoot.transform, true);
            log.Add($"  moved into package: {root.name}");
        }

        package.trackBuilder = packageRoot.GetComponentInChildren<TrackBuilder>(true);
        var environment = packageRoot.GetComponentInChildren<TrackEnvironmentBuilder>(true);
        if (environment != null) package.environmentRoot = environment.transform;
        var paddock = packageRoot.GetComponentInChildren<PaddockBoundary>(true);
        if (paddock != null) package.paddockRoot = paddock.transform;

        var prefab = PrefabUtility.SaveAsPrefabAsset(packageRoot, packagePath, out bool saved);
        if (!saved || prefab == null)
        {
            Debug.LogError($"RaceSceneSplitter: failed to write {packagePath}. Scene left untouched on disk.");
            return;
        }
        log.Add($"package written: {packagePath} ({trackRoots.Count} roots)");

        // Out of the scene it goes. The managers' TrackBuilder fields go null with it, which is the point:
        // TrackSceneLoader fills them from whichever package loads.
        Object.DestroyImmediate(packageRoot);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.Add($"race scene kept {kept.Count} roots: {string.Join(", ", kept)}");

        AddToBuildSettings(RaceScenePath, log);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string summary = "RaceSceneSplitter:\n" + string.Join("\n", log);
        Debug.Log(summary);
        WriteReport(summary);
    }

    static bool IsTrackRoot(GameObject root)
    {
        if (TrackNames.Contains(root.name)) return true;
        foreach (var type in TrackComponents)
            if (root.GetComponentInChildren(type, true) != null) return true;
        return false;
    }

    static void AddToBuildSettings(string path, List<string> log)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == path)) { log.Add("build settings: already listed"); return; }
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        log.Add($"build settings: added at index {scenes.Count - 1}");
    }

    // ---------------------------------------------------------------- selection + preview

    [MenuItem("Draftmaster/Tracks/Select Track For Next Race/Daytona")]
    public static void SelectDaytona() => SelectTrack("Daytona");

    [MenuItem("Draftmaster/Tracks/Select Track For Next Race/Martinsville")]
    public static void SelectMartinsville() => SelectTrack("Martinsville");

    [MenuItem("Draftmaster/Tracks/Select Track For Next Race/WatkinsGlen")]
    public static void SelectWatkinsGlen() => SelectTrack("WatkinsGlen");

    static void SelectTrack(string id)
    {
        bool ok = TrackSelection.Select(id);
        string summary = ok
            ? $"TrackSelection: next race scene builds {TrackCatalog.DisplayName(id)} ({id})."
            : $"TrackSelection: '{id}' rejected — no geometry at Resources/Tracks/{id}.asset.";
        Debug.Log(summary);
        WriteReport(summary);
    }

    // Open the selected track's package on its own Prefab Mode stage: the road, its dressing and nothing
    // else. This is where placement work belongs — every edit is saved into the package, so it travels with
    // the track instead of into whichever scene happened to be open.
    [MenuItem("Draftmaster/Tracks/Edit Selected Package (Prefab Mode)")]
    public static void EditSelectedPackage()
    {
        string id = TrackSelection.CurrentId;
        string path = $"Assets/Resources/{TrackCatalog.PackageFolder}/{id}.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            WriteReport($"Edit: no package at {path}.");
            Debug.LogError($"Edit: no package at {path}.");
            return;
        }

        UnityEditor.SceneManagement.PrefabStageUtility.OpenPrefab(path);
        WriteReport($"Edit: opened {id} in Prefab Mode ({path}).");
    }

    // The same stage, but opened THROUGH an instance in the race scene, so the package is drawn in context:
    // the player car, the HUDs and the rest of the scene stay visible and greyed out around it while the
    // edits still land in the package. This is the one to use for "does this grandstand sit right".
    [MenuItem("Draftmaster/Tracks/Edit Selected Package In Context (Race Scene)")]
    public static void EditSelectedPackageInContext()
    {
        string id = TrackSelection.CurrentId;
        string path = $"Assets/Resources/{TrackCatalog.PackageFolder}/{id}.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            WriteReport($"Edit: no package at {path}.");
            Debug.LogError($"Edit: no package at {path}.");
            return;
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != RaceScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(RaceScenePath, OpenSceneMode.Single);
        }

        var instance = FindPreviewInstance(id);
        if (instance == null)
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = $"Track_{id}";
            Undo.RegisterCreatedObjectUndo(instance, "Preview Track Package");
        }

        UnityEditor.SceneManagement.PrefabStageUtility.OpenPrefab(path, instance);
        WriteReport($"Edit: opened {id} in context in RaceScene. The instance is unsaved — " +
               "'Clear Package Previews' when done, or the scene pins itself to this track.");
    }

    // A package instance left saved in the race scene would be adopted by TrackSceneLoader and quietly pin
    // the scene to that one track, which is the exact thing this structure exists to avoid. So: a broom.
    [MenuItem("Draftmaster/Tracks/Clear Package Previews From Scene")]
    public static void ClearPreviews()
    {
        int removed = 0;
        foreach (var package in Object.FindObjectsByType<TrackPackage>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (package == null) continue;
            Undo.DestroyObjectImmediate(package.gameObject);
            removed++;
        }

        if (removed > 0) EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        WriteReport(removed == 0
            ? "Clear: no package instances in the scene."
            : $"Clear: removed {removed} package instance(s). Scene is dirty — save it to keep it track-free.");
    }

    static GameObject FindPreviewInstance(string id)
    {
        foreach (var package in Object.FindObjectsByType<TrackPackage>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (package != null && package.trackId == id) return package.gameObject;
        return null;
    }

    // Drop the selected track into the open scene at edit time, to look at it or dress the scene around it.
    // Deliberately does NOT bind scene references — that happens at play time, and doing it in the editor
    // would serialise a preview road into the shared scene.
    [MenuItem("Draftmaster/Tracks/Preview Selected Package In Scene")]
    public static void PreviewSelected()
    {
        string id = TrackSelection.CurrentId;
        var prefab = TrackCatalog.Package(id);
        if (prefab == null)
        {
            string miss = $"Preview: no package at Resources/{TrackCatalog.PackageFolder}/{id}.";
            Debug.LogError(miss);
            WriteReport(miss);
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = $"Track_{id}";
        Undo.RegisterCreatedObjectUndo(instance, "Preview Track Package");
        Selection.activeGameObject = instance;
        SceneView.FrameLastActiveSceneView();

        string summary = $"Preview: instantiated {id} package in " +
                         $"{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} " +
                         "(unsaved; references not bound — press Play for the real load).";
        Debug.Log(summary);
        WriteReport(summary);
    }

    // The console isn't readable over MCP in this project, so every run also drops its result here.
    static void WriteReport(string text)
    {
        try
        {
            string dir = Path.Combine(Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "track-tools.txt"), text);
        }
        catch (IOException) { /* reporting is a convenience, never a failure */ }
    }
}
