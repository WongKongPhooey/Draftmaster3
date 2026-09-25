#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Draftmaster > Art > Add Track Backdrop To Title Screen
//
// Puts a TitleTrackBackdrop in the open TitleScreen with Watkins Glen's materials, or re-wires the one already
// there. Edits the scene in place; nothing else in it is touched.
public static class TitleTrackBackdropSetup
{
    const string TitleScene = "Assets/Scenes/TitleScreen.unity";

    [MenuItem("Draftmaster/Art/Add Track Backdrop To Title Screen", priority = 131)]
    public static void AddMenu() => Debug.Log(Add());

    public static string Add()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return "Stop Play Mode first — the title scene can't be edited while it plays.";
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != TitleScene) return $"Open {TitleScene} first.";

        var backdrop = Object.FindFirstObjectByType<TitleTrackBackdrop>();
        if (backdrop == null)
        {
            var go = new GameObject("TitleTrackBackdrop", typeof(TitleTrackBackdrop));
            Undo.RegisterCreatedObjectUndo(go, "Add title track backdrop");
            backdrop = go.GetComponent<TitleTrackBackdrop>();
        }

        Undo.RecordObject(backdrop, "Add title track backdrop");
        // The materials Watkins Glen's package draws with: its Ground, its Track, its outer barriers
        // (TrackDressingFactory's barrier pick) and its FinishLine strip.
        backdrop.grass = Load("Assets/Materials/Grass.mat");
        backdrop.asphalt = Load("Assets/Materials/TrackSurface.mat");
        backdrop.wall = Load("Assets/Materials/White.mat");
        backdrop.finishLine = Load("Assets/Materials/FinishLine.mat");
        backdrop.finishLineMetres = 1.25f;   // two chequers deep: finish.png is clamped, deeper stretches it
        EditorUtility.SetDirty(backdrop);

        // The menu's dark scrim was a fixed 392 units wide, sized for a plain dark screen. Over grass it hid
        // the grass and ran onto the wall. Now: the left half of the screen, stopping at the wall's grass-side
        // face, and only partly dark — enough for the menu to read, not so much that the grass goes.
        var scrim = FindInTitleCanvas("Scrim");
        if (scrim != null)
        {
            var rt = (RectTransform)scrim;
            Undo.RecordObject(rt, "Add title track backdrop");
            float wallHalf = backdrop.BarrierWidthMetres * 0.5f * PxPerMetre;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(backdrop.wallAt, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(-wallHalf, 0f);

            var img = scrim.GetComponent<UnityEngine.UI.Image>();
            if (img != null)
            {
                Undo.RecordObject(img, "Add title track backdrop");
                var c = img.color;
                img.color = new Color(c.r, c.g, c.b, ScrimAlpha);
            }
        }

        backdrop.Rebuild();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Track backdrop on the title screen; scene saved.";
    }

    const float ScrimAlpha = 0.55f;
    // Canvas units per metre: the crash's car is TitleCrash.CarLengthPx units for 5 m.
    static readonly float PxPerMetre = Draftmaster.Sim.TitleCrash.CarLengthPx / 5f;

    static Transform FindInTitleCanvas(string name)
    {
        var ui = Object.FindFirstObjectByType<TitleScreenUI>();
        return ui != null ? ui.transform.Find(name) : null;
    }

    static Material Load(string path)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) Debug.LogWarning($"[TitleTrackBackdropSetup] {path} not found.");
        return m;
    }
}
#endif
