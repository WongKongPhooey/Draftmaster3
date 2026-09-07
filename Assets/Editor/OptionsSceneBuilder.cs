using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds Assets/Scenes/Options.unity — the screen behind the title menu's OPTIONS row.
//
// Deliberately almost empty, exactly like SingleRaceSceneBuilder: a camera and one GameObject carrying
// OptionsUI, which draws the whole screen at runtime. Nothing here is hand-placed and, more to the point,
// nothing is serialised that a later save could drop.
//
// Re-running is safe: it overwrites a scene that has no hand-authored content by design. No confirmation
// dialog, so this is callable from a menu, from tests and over MCP without wedging the editor on a modal.
public static class OptionsSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Options.unity";

    [MenuItem("Draftmaster/UI/Build Options Scene")]
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

        new GameObject("OptionsUI", typeof(OptionsUI));

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);

        // A menu scene that isn't in the build settings loads as a black screen, and the title row that
        // points at it draws disabled — so registering it is part of building it.
        string added = SingleRaceSceneBuilder.EnsureInBuildSettings(ScenePath);
        AssetDatabase.Refresh();

        return $"Options: wrote {ScenePath}. {added}\n" +
               "The screen draws itself at runtime (OptionsUI) — there is nothing to dress here.";
    }

    // Point the title menu's OPTIONS row at the scene above.
    //
    // OPTIONS was drawn on the title screen as a NotWired placeholder — the house style for "the design
    // has this and the game hasn't yet" — so this WIRES the row that is already there rather than adding a
    // second one, and the row keeps its place at the foot of the menu. TitleScreenRowEditor.AddRow does
    // the surgery; never rebuild the title screen for a menu change, it destroys every hand edit in the
    // scene. Idempotent.
    [MenuItem("Draftmaster/UI/Wire OPTIONS Row To Options Screen")]
    public static void WireTitleRow()
    {
        Debug.Log(TitleScreenRowEditor.AddRow("OPTIONS", TitleScreenUI.Command.LoadScene, "Options",
                                              after: TitleScreenUI.Command.LoadScene));
    }
}
