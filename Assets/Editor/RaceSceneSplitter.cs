using System.IO;
using System.Linq;
using Draftmaster.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Picking, previewing and editing the track a race is run at.
//
// Named for the split it used to perform: WatkinsGlen was authored in place, one scene holding both the ~20
// manager objects every race needs (player car, GridSpawner, PitLaneStart, directors, HUDs, camera,
// database) AND the Watkins Glen track itself. While a road lives in the scene, TrackSceneLoader adopts it
// and no package can ever load, so the track half was lifted into Resources/TrackPackages/WatkinsGlen.prefab
// and the manager half became Assets/Scenes/RaceScene.unity. That is done; the reference scene has been
// deleted and the one-shot that did it with it.
//
// What is left here is the day-to-day: choose the track, drop it into the race scene to look at, and open
// it for editing. `Edit Selected Package In Context` is the one to reach for — it opens the package on a
// Prefab Mode stage THROUGH an instance in the race scene, so the road is drawn with the managers and HUDs
// around it while every edit still lands in the package and travels with the track.
public static class RaceSceneSplitter
{
    const string RaceScenePath = "Assets/Scenes/RaceScene.unity";

    // ---------------------------------------------------------------- selection + preview

    // Pick the track the next race scene builds, from everything that is actually built.
    //
    // This used to be three hard-coded menu items - Daytona, Martinsville, WatkinsGlen - from when those
    // were the only three tracks that existed. There are now 38, and a [MenuItem] is a compile-time
    // attribute, so the list cannot be one item per track without writing all 38 out by hand and
    // re-writing them every time the calendar changes. A GenericMenu is built at the moment it opens, so
    // it always shows exactly what is on disk.
    //
    // Grouped by track type, with a tick against the current selection. A track with no geometry is shown
    // greyed out rather than hidden, so "why is X not in the list" has a visible answer.
    [MenuItem("Draftmaster/Tracks/Select Track For Next Race...")]
    public static void SelectTrackForNextRace()
    {
        var menu = new GenericMenu();
        string current = TrackSelection.CurrentId;
        int playable = 0;

        foreach (TrackType type in new[] { TrackType.Superspeedway, TrackType.Speedway,
                                           TrackType.ShortTrack, TrackType.RoadCourse, TrackType.DirtCourse })
        {
            var rows = TrackCatalog.All.Where(r => r.Type == type)
                                       .OrderBy(r => r.DisplayName)
                                       .ToList();
            if (rows.Count == 0) continue;

            string group = ObjectNames.NicifyVariableName(type.ToString());
            foreach (var row in rows)
            {
                string label = $"{group}/{row.DisplayName}  ({row.LengthMiles:0.###} mi)";
                if (TrackCatalog.HasGeometry(row.Name))
                {
                    playable++;
                    string id = row.Name;   // captured per iteration, not by reference to the loop
                    menu.AddItem(new GUIContent(label), current == id, () => SelectTrack(id));
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent(label + " - no layout"));
                }
            }
        }

        menu.AddSeparator("");
        menu.AddItem(new GUIContent($"Open the Track Builder Window ({playable} built)"), false,
                     TrackAuthoringMenu.OpenWindow);
        menu.ShowAsContext();
    }

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
