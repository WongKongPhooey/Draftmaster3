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
[InitializeOnLoad]
public static class RaceSceneSplitter
{
    const string RaceScenePath = "Assets/Scenes/RaceScene.unity";

    // ---------------------------------------------------------------- the scene stays track-free

    // A package instance saved into the race scene pins it to that one track (TrackSceneLoader now swaps a
    // mismatched one out at runtime, but the scene should still never carry one). The rule is one line:
    //
    //   SAVING THE RACE SCENE PUTS YOUR TRACK EDITS IN THE PACKAGE AND TAKES THE TRACK OUT OF THE SCENE.
    //
    // Whatever you changed on a track in the race scene — dragged a marker, added a component, dropped in a new
    // object — is applied to Resources/TrackPackages/<id>.prefab, then the instance is removed. Closing an
    // Edit In Context stage does the same. There is no "apply first, then clear" to remember, and nothing is
    // ever kept back in the scene: keeping an instance with unapplied edits was the old safety net, and it is
    // what pinned the race scene to Watkins Glen and sent a Daytona weekend there (0.4.1).
    //
    // What is NOT carried over is what the track builders regenerate on every enable anyway (meshes, and the
    // objects they spawn under a TrackBuilder / TrackGround / grandstand / scenery piece) — see IsGeneratedObject.
    //
    // Clear Package Previews From Scene does the same apply-then-remove, as a broom for a scene already in a mess.
    static RaceSceneSplitter()
    {
        EditorSceneManager.sceneSaving -= StripPackagesBeforeSave;
        EditorSceneManager.sceneSaving += StripPackagesBeforeSave;
    }

    static void StripPackagesBeforeSave(UnityEngine.SceneManagement.Scene scene, string path)
    {
        // Only the shared race scene. A hand-built track scene is entitled to a road of its own.
        if (scene.path != RaceScenePath && path != RaceScenePath) return;

        // A stage open in context is USING its instance as the backdrop right now — pulling it out from
        // under the stage would break the thing being edited. It is removed when the stage closes instead.
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) return;

        int removed = 0, kept = 0;
        var report = new System.Text.StringBuilder();
        foreach (var package in Object.FindObjectsByType<TrackPackage>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (package == null) continue;
            if (FoldIntoPackageAndRemove(package.gameObject, report, undoable: false)) removed++;
            else kept++;
        }

        if (removed > 0 || kept > 0)
            WriteReport($"Save: took {removed} track(s) out of the race scene" +
                        (kept > 0 ? $", could NOT take {kept} out (see the console)" : "") + ".\n" + report);
    }

    // Apply whatever was authored on this track instance to its package, then remove the instance. False (and
    // the instance left where it is) only when an apply failed — then the edits exist nowhere else and
    // removing it would lose them; the console says which.
    static bool FoldIntoPackageAndRemove(GameObject instance, System.Text.StringBuilder report, bool undoable)
    {
        var package = instance.GetComponent<TrackPackage>();
        string id = package != null ? package.trackId : instance.name;

        if (!PrefabUtility.IsPartOfPrefabInstance(instance))
        {
            Debug.LogError($"RaceScene: '{instance.name}' is a track that is not a package instance, so there is no " +
                           "package to put it in. Left in the scene — make it a package (Draftmaster > Tracks > " +
                           "Build Package For Track) or delete it by hand.", instance);
            report.AppendLine($"  {id}: left in — not a package instance");
            return false;
        }

        var applied = new System.Collections.Generic.List<string>();
        var failed = new System.Collections.Generic.List<string>();
        ApplyAuthoredEdits(instance, applied, failed);

        if (failed.Count > 0)
        {
            Debug.LogError($"RaceScene: could not put {failed.Count} edit(s) on '{id}' into its package, so the " +
                           $"track was left in the scene rather than lose them:\n  {string.Join("\n  ", failed)}", instance);
            report.AppendLine($"  {id}: left in — {failed.Count} edit(s) would not apply");
            return false;
        }

        if (undoable) Undo.DestroyObjectImmediate(instance);
        else Object.DestroyImmediate(instance);

        string what = applied.Count == 0
            ? "no edits on it"
            : $"{applied.Count} edit(s) saved into the package:\n    " + string.Join("\n    ", applied);
        Debug.Log($"RaceScene: took the '{id}' track out of the scene — {what}");
        report.AppendLine($"  {id}: removed, {what}");
        return true;
    }

    // Push the authored overrides on a package instance into the package asset. Same classification as
    // UnappliedEdits: the root's own transform and anything a builder regenerates are left behind.
    static void ApplyAuthoredEdits(GameObject instance, System.Collections.Generic.List<string> applied,
                                   System.Collections.Generic.List<string> failed)
    {
        string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance);
        const InteractionMode mode = InteractionMode.AutomatedAction;

        foreach (var added in PrefabUtility.GetAddedGameObjects(instance))
        {
            if (added?.instanceGameObject == null || IsGeneratedObject(added.instanceGameObject)) continue;
            string where = PathInside(instance, added.instanceGameObject.transform);
            try { added.Apply(assetPath, mode); applied.Add("added object " + where); }
            catch (System.Exception e) { failed.Add($"added object {where}: {e.Message}"); }
        }

        foreach (var added in PrefabUtility.GetAddedComponents(instance))
        {
            if (added?.instanceComponent == null) continue;
            if (!PrefabUtility.IsPartOfPrefabInstance(added.instanceComponent.gameObject)) continue;
            if (IsGenerated(added.instanceComponent)) continue;
            string where = $"{added.instanceComponent.GetType().Name} on {PathInside(instance, added.instanceComponent.transform)}";
            try { added.Apply(assetPath, mode); applied.Add("added " + where); }
            catch (System.Exception e) { failed.Add($"added {where}: {e.Message}"); }
        }

        foreach (var gone in PrefabUtility.GetRemovedComponents(instance))
        {
            if (gone?.assetComponent == null) continue;
            string where = gone.assetComponent.GetType().Name;
            try { gone.Apply(assetPath, mode); applied.Add("removed " + where); }
            catch (System.Exception e) { failed.Add($"removed {where}: {e.Message}"); }
        }

        var rootTransform = instance.transform;
        foreach (var over in PrefabUtility.GetObjectOverrides(instance, includeDefaultOverrides: false))
        {
            if (over?.instanceObject == null) continue;
            if (over.instanceObject == rootTransform || over.instanceObject == instance) continue;
            if (IsGenerated(over.instanceObject)) continue;
            string where = PathInside(instance, over.instanceObject);
            string label = over.instanceObject is Transform ? "moved " + where
                                                            : $"changed {over.instanceObject.GetType().Name} on {where}";
            try { over.Apply(assetPath, mode); applied.Add(label); }
            catch (System.Exception e) { failed.Add($"{label}: {e.Message}"); }
        }
    }

    // An object a track builder spawned and will spawn again on its next enable: it sits somewhere under one
    // of the [ExecuteAlways] builders. Applying those would bake a second copy of the road into the package.
    static bool IsGeneratedObject(GameObject go)
    {
        for (var t = go.transform.parent; t != null; t = t.parent)
            if (t.GetComponent<TrackBuilder>() != null || t.GetComponent<TrackGround>() != null ||
                t.GetComponent<TrackOverpass>() != null || t.GetComponent<Grandstand>() != null ||
                t.GetComponent<TrackEnvironmentBuilder>() != null || t.GetComponent<ExtraTrackSpline>() != null ||
                t.GetComponent<SceneryPiece>() != null)
                return true;
        return false;
    }

    // The instance Edit In Context put in the race scene, so the stage can take it away again on close.
    // Null after a domain reload, which is why the close handler falls back to finding it by id.
    static GameObject _contextInstance;
    static string _contextTrackId;

    static void OnStageClosing(PrefabStage stage)
    {
        PrefabStage.prefabStageClosing -= OnStageClosing;

        var instance = _contextInstance;
        if (instance == null && !string.IsNullOrEmpty(_contextTrackId)) instance = FindPreviewInstance(_contextTrackId);

        _contextInstance = null;
        _contextTrackId = null;
        if (instance == null) return;

        // Same rule as the save: whatever was changed on the instance in the scene (rather than inside the
        // stage) goes into the package, and then the instance goes.
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var report = new System.Text.StringBuilder();
        bool removed = FoldIntoPackageAndRemove(instance, report, undoable: true);
        if (removed) EditorSceneManager.MarkSceneDirty(scene);
        WriteReport((removed ? "Edit: closed the stage and took the track out of the race scene.\n"
                             : "Edit: closed the stage but could NOT take the track out (see the console).\n") + report);
    }

    // ---------------------------------------------------------------- selection + preview

    // Pick the track the next race scene builds, from everything that is actually built.
    //
    // This used to be three hard-coded menu items - Daytona, Martinsville, WatkinsGlen - from when those
    // were the only three tracks that existed. There are now 38, and a [MenuItem] is a compile-time
    // attribute, so the list cannot be one item per track without writing all 38 out by hand and
    // re-writing them every time the calendar changes. The picker is built when it opens, so it always
    // shows exactly what is on disk.
    //
    // Grouped by track type, with a tick against the current selection. A track with no geometry is shown
    // greyed out rather than hidden, so "why is X not in the list" has a visible answer.
    //
    // A window, not a GenericMenu: ShowAsContext needs a GUI event in flight, and a main-menu item has none,
    // so on Windows the dropdown silently never appeared.
    [MenuItem("Draftmaster/Tracks/Select Track For Next Race...")]
    public static void SelectTrackForNextRace() => TrackPickerWindow.Open();

    internal static void SelectTrack(string id)
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

        // The instance exists only to be the backdrop for this stage, so it leaves when the stage does.
        // Nothing to remember to do afterwards, and nothing left to be saved into the scene by accident.
        _contextInstance = instance;
        _contextTrackId = id;
        PrefabStage.prefabStageClosing -= OnStageClosing;
        PrefabStage.prefabStageClosing += OnStageClosing;

        UnityEditor.SceneManagement.PrefabStageUtility.OpenPrefab(path, instance);
        WriteReport($"Edit: opened {id} in context in RaceScene. Edits land in the package; the context " +
               "instance is removed from the scene when you close the stage.");
    }

    // A package instance left saved in the race scene would be adopted by TrackSceneLoader and quietly pin
    // the scene to that one track, which is the exact thing this structure exists to avoid. So: a broom.
    [MenuItem("Draftmaster/Tracks/Clear Package Previews From Scene")]
    public static void ClearPreviews()
    {
        int removed = 0, kept = 0;
        var report = new System.Text.StringBuilder();
        foreach (var package in Object.FindObjectsByType<TrackPackage>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (package == null) continue;
            if (FoldIntoPackageAndRemove(package.gameObject, report, undoable: true)) removed++;
            else kept++;
        }

        if (removed > 0) EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        WriteReport(removed == 0 && kept == 0
            ? "Clear: no tracks in the scene."
            : $"Clear: took {removed} track(s) out (edits saved into their packages)" +
              (kept > 0 ? $", could NOT take {kept} out (see the console)" : "") +
              ". Scene is dirty — save it.\n" + report);
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
    // What this instance is carrying that the package on disk does not have, in words — or null when it is
    // carrying nothing and is therefore safe to throw away.
    //
    // Two whole classes of "override" are deliberately ignored, because on a track package neither of them
    // is authoring:
    //
    //   THE ROOT'S OWN TRANSFORM. The splitter positions the instance itself when it drops one into the
    //   scene, so the root reads as overridden on every preview. Guarding on that would mean never stripping
    //   anything, which is the failure this whole file exists to prevent.
    //
    //   ADDED GAMEOBJECTS. TrackBuilder is [ExecuteAlways] and rebuilds the road from the spline in
    //   OnEnable — edge lines, brake markers, pit lane, runoff, barriers, decorations — so a package that
    //   has only just been instantiated and touched by nobody already reports eight added objects. They are
    //   generated, they are regenerated on every enable, and treating them as work would jam the guard
    //   permanently shut. (It is also why applying a track package wholesale bloats the asset: those
    //   generated children get baked in, and the next enable generates another set beside them.)
    //
    //   THE MESH COMPONENTS. Same reason, one level down. The builders write the meshes they generate
    //   straight into the instance's MeshFilters and MeshRenderers, so every road, kerb, runoff and
    //   grandstand in a previewed package reads as an override the moment it is drawn. Nobody assigns those
    //   by hand — they are output, not authoring — and they are the single biggest thing bloating the race
    //   scene, because a scene that saves with a package in it saves every generated mesh with it.
    //
    // What is left is the signal: a property changed on something that IS in the package — a marker dragged
    // to a new position, a renamed object, a component pulled off. Which is exactly the case that was lost.
    public static string UnappliedEdits(GameObject instance, int limit = 10)
    {
        if (instance == null) return null;

        // Not a prefab instance at all. Nothing can be applied anywhere, so there is no safe way to remove
        // it — whatever it is, it only exists here.
        if (!PrefabUtility.IsPartOfPrefabInstance(instance))
            return "  the whole object — it is not a prefab instance, so nothing in it exists anywhere else";

        var lines = new System.Collections.Generic.List<string>();
        var rootTransform = instance.transform;

        foreach (var over in PrefabUtility.GetObjectOverrides(instance, includeDefaultOverrides: false))
        {
            if (over?.instanceObject == null) continue;
            if (over.instanceObject == rootTransform || over.instanceObject == instance) continue;
            if (IsGenerated(over.instanceObject)) continue;

            string where = PathInside(instance, over.instanceObject);
            if (over.instanceObject is Transform t)
                lines.Add($"  moved: {where} is at {t.localPosition}");
            else
                lines.Add($"  changed: {over.instanceObject.GetType().Name} on {where}");
        }

        // A component added by hand to an object that IS in the package still counts — that is somebody
        // wiring something up, not the road rebuilding itself.
        foreach (var added in PrefabUtility.GetAddedComponents(instance))
        {
            if (added?.instanceComponent == null) continue;
            if (!PrefabUtility.IsPartOfPrefabInstance(added.instanceComponent.gameObject)) continue;
            lines.Add($"  added: {added.instanceComponent.GetType().Name} on " +
                      $"{PathInside(instance, added.instanceComponent.transform)}");
        }

        foreach (var gone in PrefabUtility.GetRemovedComponents(instance))
            if (gone?.assetComponent != null)
                lines.Add($"  removed: {gone.assetComponent.GetType().Name}");

        if (lines.Count == 0) return null;

        int shown = Mathf.Min(lines.Count, limit);
        string text = string.Join("\n", lines.GetRange(0, shown));
        if (lines.Count > shown) text += $"\n  ...and {lines.Count - shown} more";
        return text;
    }

    // Output of an [ExecuteAlways] builder rather than something a person put there. Meshes and the
    // renderers that draw them are regenerated from the spline on every enable, so they are never authoring
    // and counting them would mean nothing could ever be stripped.
    static bool IsGenerated(Object o) => o is MeshFilter || o is MeshRenderer;

    // Where inside the instance something is, for a message somebody has to act on.
    static string PathInside(GameObject instance, Object member)
    {
        var t = member as Transform ?? (member as Component)?.transform;
        if (t == null) return member != null ? member.name : "?";

        string path = t.name;
        for (var p = t.parent; p != null && p != instance.transform.parent; p = p.parent)
            path = p.name + "/" + path;
        return path;
    }

    // Consoles and log readers routinely show only the first line of a multi-line message, and here the list
    // IS the message — so the first line has to carry the count.
    static int CountLines(string text) => string.IsNullOrEmpty(text) ? 0 : text.Split('\n').Length;

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

// The list behind `Select Track For Next Race...`: every catalogue track grouped by type, the current one ticked,
// unbuilt ones greyed out. Picking one sets TrackSelection and closes the window.
public class TrackPickerWindow : EditorWindow
{
    Vector2 _scroll;

    public static void Open()
    {
        var w = GetWindow<TrackPickerWindow>(true, "Select Track For Next Race", true);
        w.minSize = new Vector2(320f, 420f);
    }

    void OnGUI()
    {
        string current = TrackSelection.CurrentId;
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        foreach (TrackType type in new[] { TrackType.Superspeedway, TrackType.Speedway,
                                           TrackType.ShortTrack, TrackType.RoadCourse, TrackType.DirtCourse })
        {
            var rows = TrackCatalog.All.Where(r => r.Type == type).OrderBy(r => r.DisplayName).ToList();
            if (rows.Count == 0) continue;

            EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(type.ToString()), EditorStyles.boldLabel);
            foreach (var row in rows)
            {
                bool built = TrackCatalog.HasGeometry(row.Name);
                string label = $"{(current == row.Name ? "\u2713 " : "   ")}{row.DisplayName}  ({row.LengthMiles:0.###} mi)"
                               + (built ? "" : " - no layout");
                using (new EditorGUI.DisabledScope(!built))
                {
                    if (GUILayout.Button(label, EditorStyles.miniButton))
                    {
                        RaceSceneSplitter.SelectTrack(row.Name);
                        Close();
                        GUIUtility.ExitGUI();
                    }
                }
            }
            EditorGUILayout.Space(4f);
        }

        EditorGUILayout.EndScrollView();
        if (GUILayout.Button("Open the Track Builder Window"))
            TrackAuthoringMenu.OpenWindow();
    }
}
