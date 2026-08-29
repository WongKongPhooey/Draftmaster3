using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Builds Assets/Scenes/SingleRace.unity — the SINGLE RACE flow's scene.
//
// Deliberately almost empty: a camera and one GameObject carrying SingleRaceUI, which draws the whole
// screen at runtime. There is nothing here to hand-place and, more to the point, nothing serialised that
// a later save could drop — generated button listeners do not survive a save in this project, so a screen
// that wires itself in Start is the durable choice for a list this dynamic.
//
// Re-running is safe: it overwrites the scene file, and the scene has no hand-authored content by design.
// No confirmation dialog, so this is callable from a menu, from tests and over MCP without wedging the
// editor waiting on a modal.
public static class SingleRaceSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/SingleRace.unity";

    [MenuItem("Draftmaster/UI/Build Single Race Scene")]
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

        var ui = new GameObject("SingleRaceUI", typeof(SingleRaceUI));
        ui.transform.SetParent(null);

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);

        string added = EnsureInBuildSettings(ScenePath);
        AssetDatabase.Refresh();

        return $"Single Race: wrote {ScenePath}. {added}\n" +
               "The screen draws itself at runtime (SingleRaceUI) — there is nothing to dress here.";
    }

    // A menu scene that isn't in the build settings loads as a black screen at runtime, and the title
    // screen draws a row disabled when its destination is missing — so registering it is part of building it.
    public static string EnsureInBuildSettings(string path)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        int at = scenes.FindIndex(s => s.path == path);
        if (at >= 0)
        {
            if (scenes[at].enabled) return $"Already in the build settings at index {at}.";
            scenes[at] = new EditorBuildSettingsScene(path, true);
            EditorBuildSettings.scenes = scenes.ToArray();
            return $"Re-enabled in the build settings at index {at}.";
        }

        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        return $"Added to the build settings at index {scenes.Count - 1}.";
    }
}
