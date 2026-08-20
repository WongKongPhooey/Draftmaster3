using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// The title menu is data, not code: which row runs which command, and where each one goes, live in
// TitleScreen.unity. A row whose destination is missing from the build settings draws disabled instead of
// throwing, which is only visible in play mode and reads as "the button does nothing" — so the wiring is
// checked here rather than by pressing it.
//
// Nothing here names TitleScreenUI's type: this assembly can't reference Assembly-CSharp, so the component
// is read the way the inspector reads it, through SerializedObject.
public class TitleScreenWiringTests
{
    const string TitleScenePath = "Assets/Scenes/TitleScreen.unity";
    const string RaceScenePath = "Assets/Scenes/RaceScene.unity";
    const string GarageScenePath = "Assets/Scenes/GarageScreen.unity";

    // TitleScreenUI.Command, in declaration order.
    const int NewSeason = 0;
    const int Continue = 1;
    const int Exhibition = 2;
    const int LoadScene = 3;

    Scene _title;

    // Additive: the editor keeps whatever scene it had open while these run.
    [SetUp]
    public void OpenTitle() => _title = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Additive);

    [TearDown]
    public void CloseTitle()
    {
        if (_title.IsValid() && _title.isLoaded) EditorSceneManager.CloseScene(_title, true);
    }

    [Test]
    public void EveryMenuDestinationIsInTheBuildSettings()
    {
        var menu = Menu();
        Assert.IsTrue(InBuild(TitleScenePath), "TitleScreen itself has to be in the build settings to be loaded back into.");
        Assert.IsTrue(InBuild(SceneAsset(menu.FindProperty("raceSceneName").stringValue)),
                      "The race scene the season rows load is not in the build settings.");

        var rows = menu.FindProperty("rows");
        for (int i = 0; i < rows.arraySize; i++)
        {
            var row = rows.GetArrayElementAtIndex(i);
            if (row.FindPropertyRelative("command").enumValueIndex != LoadScene) continue;

            string label = row.FindPropertyRelative("label").stringValue;
            string scene = row.FindPropertyRelative("sceneName").stringValue;
            Assert.IsNotEmpty(scene, $"The {label} row loads a scene but names none.");
            Assert.IsTrue(InBuild(SceneAsset(scene)),
                          $"The {label} row loads '{scene}', which is not in the build settings — it will draw disabled.");
        }
    }

    [Test]
    public void SeasonRowsHaveSomewhereToRace()
    {
        var menu = Menu();
        Assert.AreEqual(NewSeason, CommandOf(menu, "NEW SEASON"), "NEW SEASON should start a fresh weekend.");
        Assert.AreEqual(Continue, CommandOf(menu, "CONTINUE"), "CONTINUE should resume the selected track.");
        Assert.AreEqual(Exhibition, CommandOf(menu, "EXHIBITION"), "EXHIBITION should skip to the race.");

        // The race scene builds its road from the selected track, so the season's opener needs both halves
        // of a track: the spline asset and the package of scenery bound to it.
        string trackId = menu.FindProperty("newSeasonTrackId").stringValue;
        Assert.IsNotEmpty(trackId, "NEW SEASON has no opening track; it would fall back to whatever the calendar starts with.");
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Object>($"Assets/Resources/Tracks/{trackId}.asset"),
                         $"'{trackId}' has no geometry at Assets/Resources/Tracks/{trackId}.asset.");
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Resources/TrackPackages/{trackId}.prefab"),
                         $"'{trackId}' has no content package at Assets/Resources/TrackPackages/{trackId}.prefab.");
    }

    [Test]
    public void GarageRowOpensTheGarageScreen()
    {
        var menu = Menu();
        var rows = menu.FindProperty("rows");
        for (int i = 0; i < rows.arraySize; i++)
        {
            var row = rows.GetArrayElementAtIndex(i);
            if (row.FindPropertyRelative("label").stringValue != "GARAGE") continue;

            Assert.AreEqual(LoadScene, row.FindPropertyRelative("command").enumValueIndex);
            Assert.AreEqual(SceneName(GarageScenePath), row.FindPropertyRelative("sceneName").stringValue);
            return;
        }
        Assert.Fail("The title screen has no GARAGE row.");
    }

    [Test]
    public void GarageScreenLeadsBackToTheTitleAndOutToTheRace()
    {
        var garage = EditorSceneManager.OpenScene(GarageScenePath, OpenSceneMode.Additive);
        try
        {
            var ui = Find(garage, "GarageScreenUI");
            Assert.AreEqual(SceneName(TitleScenePath), ui.FindProperty("titleSceneName").stringValue);
            Assert.AreEqual(SceneName(RaceScenePath), ui.FindProperty("raceSceneName").stringValue);
        }
        finally
        {
            if (garage.IsValid() && garage.isLoaded) EditorSceneManager.CloseScene(garage, true);
        }
    }

    // ------------------------------------------------------------------ helpers

    SerializedObject Menu() => Find(_title, "TitleScreenUI");

    static SerializedObject Find(Scene scene, string typeName)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null && behaviour.GetType().Name == typeName)
                    return new SerializedObject(behaviour);

        Assert.Fail($"{scene.name} has no {typeName}.");
        return null;
    }

    static int CommandOf(SerializedObject menu, string label)
    {
        var rows = menu.FindProperty("rows");
        for (int i = 0; i < rows.arraySize; i++)
        {
            var row = rows.GetArrayElementAtIndex(i);
            if (row.FindPropertyRelative("label").stringValue == label)
                return row.FindPropertyRelative("command").enumValueIndex;
        }
        Assert.Fail($"The title screen has no {label} row.");
        return -1;
    }

    static bool InBuild(string scenePath)
    {
        foreach (var scene in EditorBuildSettings.scenes)
            if (scene.enabled && scene.path == scenePath) return true;
        return false;
    }

    static string SceneName(string path) => System.IO.Path.GetFileNameWithoutExtension(path);

    // SceneManager.LoadScene takes a bare name; the build list holds paths. Resolve one to the other so the
    // check fails on a genuinely missing scene rather than on the two spellings disagreeing.
    static string SceneAsset(string sceneName)
    {
        foreach (var scene in EditorBuildSettings.scenes)
            if (SceneName(scene.path) == sceneName) return scene.path;
        return sceneName;
    }
}
