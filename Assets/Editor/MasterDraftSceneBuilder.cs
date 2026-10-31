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
