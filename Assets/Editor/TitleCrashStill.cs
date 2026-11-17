using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// The title's crash, standing still in an open scene outside Play Mode.
//
// TitleCrashScene builds its cars in Update, so a title scene opened in the editor has an empty track, and the
// store mockups (Assets/Scenes/StoreMockups) are stills captured from edit mode. This runs a copy of the
// scene's own TitleCrashScene to the freeze and leaves what it built in the scene as DontSave objects: they
// show in the Game view and in captures, and nothing is written into the scene file. The scene's own
// component is not touched.
//
// The tableau is laid out for the Game view's shape at the moment it is staged. Stage it again after
// changing the Game view size, the camera zoom or the pan.
public static class TitleCrashStill
{
    const string RootName = "TitleCrash (staged still)";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Draftmaster/Art/Stage Title Crash Still (open scene)", priority = 132)]
    public static void StageMenu() => Debug.Log(Stage(0));

    [MenuItem("Draftmaster/Art/Clear Title Crash Still", priority = 133)]
    public static void ClearMenu() => Debug.Log(Clear() ? "Staged title crash cleared." : "No staged title crash in the open scenes.");

    // `seed` 0 takes the scene component's own seed, and when that is 0 too draws a fresh accident. The seed
    // used is in the returned line, so a shot worth keeping can be staged again.
    public static string Stage(int seed)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return "Leave Play Mode first: the crash is already running there.";

        var source = Object.FindFirstObjectByType<TitleCrashScene>();
        if (source == null) return "No TitleCrashScene in the open scene.";

        Clear();

        // EnsureListener puts an AudioListener on the camera when the scene has none; that one would be saved.
        var listenersBefore = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        var root = new GameObject(RootName) { hideFlags = HideFlags.DontSave };
        SceneManager_Move(root, source.gameObject);
        var copy = root.AddComponent<TitleCrashScene>();
        EditorUtility.CopySerialized(source, copy);
        if (seed != 0) copy.seed = seed;
        // The field going past is over before the freeze; only the accident is wanted.
        copy.leadInSeconds = 0f;
        copy.startDelaySeconds = 0f;

        var type = typeof(TitleCrashScene);
        var update = type.GetMethod("Update", Private);
        var elapsed = type.GetField("_elapsed", Private);

        // A ScreenSpace-Camera canvas only re-fits its camera when that camera renders, so after a zoom change it
        // still has the old world size and every car would come out scaled by the old zoom. Render once first.
        var canvas = copy.layoutCanvas != null ? copy.layoutCanvas.rootCanvas : null;
        if (canvas != null && canvas.worldCamera != null) canvas.worldCamera.Render();
        Canvas.ForceUpdateCanvases();
        // The component waits for the canvas's first layout pass; a handful of tries covers it.
        var built = type.GetField("_built", Private);
        for (int i = 0; i < 12 && !(bool)built.GetValue(copy); i++) update.Invoke(copy, null);
        if (!(bool)built.GetValue(copy))
        {
            Object.DestroyImmediate(root);
            return "The crash did not build: no canvas or camera to lay it out against, or no liveries.";
        }

        // Silent: the engines and crowd would otherwise play in the editor.
        foreach (var a in root.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(a);
        foreach (var f in root.GetComponentsInChildren<AudioLowPassFilter>(true)) Object.DestroyImmediate(f);
        foreach (var l in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (System.Array.IndexOf(listenersBefore, l) < 0) Object.DestroyImmediate(l);

        // Run the sequence on its own clock, and the particle systems with it: outside Play Mode nothing
        // simulates them, so each is stepped by the frame's time at the rate the component has just set.
        var systems = root.GetComponentsInChildren<ParticleSystem>(true);
        float total = Mathf.Max(0.1f, copy.slamSeconds + copy.crawlSeconds);
        const int Steps = 360;
        float dt = total / Steps;
        for (int i = 1; i <= Steps + 1; i++)
        {
            elapsed.SetValue(copy, i <= Steps ? dt * i : total * 2f);
            update.Invoke(copy, null);
            if (i > Steps) break;
            foreach (var ps in systems)
                ps.Simulate(dt * ps.main.simulationSpeed, false, false, false);
        }
        foreach (var ps in systems) ps.Pause(false);

        int usedSeed = (int)type.GetField("_seed", Private).GetValue(copy);

        // Stop it stepping itself (and shaking the camera) if the editor ever ticks it, and keep all of it out
        // of the scene file.
        copy.enabled = false;
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            if (mf.sharedMesh != null) mf.sharedMesh.hideFlags = HideFlags.DontSave;
        // VehicleDamage makes its bodywork material at runtime with no flags; outside Play Mode the editor's
        // unused-asset sweep (a scene save runs one) destroys it and the cars draw magenta.
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.sharedMaterials)
                if (m != null && !EditorUtility.IsPersistent(m)) m.hideFlags = HideFlags.DontSave;

        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
        return $"Title crash staged, seed {usedSeed}. Not saved with the scene; stage again after resizing the Game view.";
    }

    public static bool Clear()
    {
        bool any = false;
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t == null || t.name != RootName || EditorUtility.IsPersistent(t)) continue;
            Object.DestroyImmediate(t.gameObject);
            any = true;
        }
        return any;
    }

    static void SceneManager_Move(GameObject go, GameObject beside)
    {
        if (beside.scene.IsValid() && go.scene != beside.scene)
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, beside.scene);
    }
}
