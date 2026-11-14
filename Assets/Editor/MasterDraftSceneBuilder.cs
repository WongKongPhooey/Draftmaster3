using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds Assets/Scenes/MasterDraft.unity — the MASTER DRAFT mode's menu scene.
//
// Like SingleRace.unity it is deliberately almost empty: a camera and one GameObject carrying MasterDraftUI,
// which draws every screen at runtime. Nothing is hand-placed in it, so re-running this is safe — it
// overwrites the scene file and loses nothing. (It is the only builder allowed to rewrite this scene, and
// it must not grow hand-authored content, or that stops being true.)
//
// No confirmation dialog, so it can run from a menu, a test or over MCP without wedging the editor.
public static class MasterDraftSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/MasterDraft.unity";

    [MenuItem("Draftmaster/Master Draft/Build Master Draft Scene")]
    public static void Build()
    {
        Debug.Log(BuildScene());
    }

    public static string BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraGo = new GameObject("Main Camera", typeof(Camera));
        cameraGo.tag = "MainCamera";
        var camera = cameraGo.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;

        new GameObject("MasterDraftUI", typeof(MasterDraftUI));

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);

        string added = SingleRaceSceneBuilder.EnsureInBuildSettings(ScenePath);
        AssetDatabase.Refresh();

        return $"Master Draft: wrote {ScenePath}. {added}\n" +
               "The screens draw themselves at runtime (MasterDraftUI) — there is nothing to dress here.";
    }

    // Shows where the offline profile lives, for a reset by hand.
    [MenuItem("Draftmaster/Master Draft/Reveal Offline Profile File")]
    public static void RevealProfile()
    {
        string path = Path.Combine(Application.persistentDataPath, LocalMasterDraftBackend.FileName);
        if (File.Exists(path)) EditorUtility.RevealInFinder(path);
        else Debug.Log($"Master Draft: no offline profile yet at {path}.");
    }

    // Puts every paint in Paints.json into the offline profile's Cars collection, so the Garage's Cars tab shows
    // them all (there's no way to earn one yet). Dev only; not while playing (the running session would save over it).
    [MenuItem("Draftmaster/Master Draft/Give Every Paint (Offline Profile)")]
    public static void GiveEveryPaint()
    {
        if (Application.isPlaying) { Debug.LogWarning("Master Draft: stop Play Mode first — the running session would save over the change."); return; }
        string path = Path.Combine(Application.persistentDataPath, LocalMasterDraftBackend.FileName);
        if (!File.Exists(path)) { Debug.Log($"Master Draft: no offline profile yet at {path}. Open Master Draft once first."); return; }
        MasterDraftJsonInstaller.Install();
        var paintsText = Resources.Load<TextAsset>(Draftmaster.MasterDraft.PaintCatalog.ResourcePath);
        var paints = paintsText == null ? null : Draftmaster.MasterDraft.PaintCatalog.FromJson(paintsText.text);
        var carsText = Resources.Load<TextAsset>(Draftmaster.MasterDraft.CarCatalog.ResourcePath);
        var cars = carsText == null ? null : Draftmaster.MasterDraft.CarCatalog.FromJson(carsText.text);
        var profile = Draftmaster.MasterDraft.MasterDraftProfile.FromJson(File.ReadAllText(path), cars);
        if (paints == null || profile == null) { Debug.LogWarning("Master Draft: couldn't read Paints.json or the profile."); return; }
        int added = 0;
        foreach (var p in paints.Paints)
            if (Draftmaster.MasterDraft.PaintCollection.Award(profile.paints, paints, p.id)) added++;
        File.WriteAllText(path, profile.ToJson(pretty: true));
        Debug.Log($"Master Draft: gave {added} paints ({profile.paints.Count} of {paints.Count} held) in {path}.");
    }

    // Wipes the offline profile, so the next visit starts from a fresh Starter Pack. Dev only.
    [MenuItem("Draftmaster/Master Draft/Delete Offline Profile")]
    public static void DeleteProfile()
    {
        string path = Path.Combine(Application.persistentDataPath, LocalMasterDraftBackend.FileName);
        if (File.Exists(path)) { File.Delete(path); Debug.Log($"Master Draft: deleted {path}."); }
        else Debug.Log($"Master Draft: nothing to delete at {path}.");
        if (MasterDraftSession.Instance != null) Object.DestroyImmediate(MasterDraftSession.Instance.gameObject);
    }
}
