using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// The demo's front-to-back route is data, not code — which row runs which command, where each one goes,
// and which scene the game boots into all live in scene assets and the build settings. A row whose
// destination is missing from the build settings draws disabled instead of throwing, which is only
// visible in play mode and reads as "the button does nothing" — so the whole chain is checked here
// rather than by pressing it:
//
//   TitleScreen (boot) → RaceScene at WatkinsGlen, or → TeamGarage
//   RV laptop / factory laptop → GarageScreen → BACK to whichever of those opened it
//
// Nothing here names TitleScreenUI's type: this assembly can't reference Assembly-CSharp, so components
// are read the way the inspector reads them, through SerializedObject, and looked up by type name.
public class TitleScreenWiringTests
{
    const string TitleScenePath = "Assets/Scenes/TitleScreen.unity";
    const string RaceScenePath = "Assets/Scenes/RaceScene.unity";
    const string GarageScenePath = "Assets/Scenes/GarageScreen.unity";
    const string FactoryScenePath = "Assets/Menus/TeamGarage.unity";

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

    // The demo boots into the title screen, so it has to be the first enabled scene in the list —
    // everything downstream is reached from it.
    [Test]
    public void TheGameBootsIntoTheTitleScreen()
    {
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (!scene.enabled) continue;
            Assert.AreEqual(TitleScenePath, scene.path,
                            $"The first enabled build scene is '{scene.path}', so the game boots into that, not the title.");
            return;
        }
        Assert.Fail("No scene in the build settings is enabled.");
    }

    // The car sheet is a thing in the world now, opened from a laptop the player walks up to. A GARAGE
    // row on the title menu would put it back where the player never stands.
    [Test]
    public void TheTitleMenuDoesNotOpenTheGarageDirectly()
    {
        var menu = Menu();
        var rows = menu.FindProperty("rows");
        for (int i = 0; i < rows.arraySize; i++)
        {
            var row = rows.GetArrayElementAtIndex(i);
            if (row.FindPropertyRelative("command").enumValueIndex != LoadScene) continue;
            Assert.AreNotEqual(SceneName(GarageScenePath), row.FindPropertyRelative("sceneName").stringValue,
                               $"The '{row.FindPropertyRelative("label").stringValue}' row opens the garage screen from the menu; " +
                               "it belongs behind a laptop in the RV or the factory.");
        }
    }

    [Test]
    public void TeamFactoryRowOpensTheFactory()
    {
        var menu = Menu();
        var rows = menu.FindProperty("rows");
        for (int i = 0; i < rows.arraySize; i++)
        {
            var row = rows.GetArrayElementAtIndex(i);
            if (row.FindPropertyRelative("label").stringValue != "TEAM FACTORY") continue;

            Assert.AreEqual(LoadScene, row.FindPropertyRelative("command").enumValueIndex);
            Assert.AreEqual(SceneName(FactoryScenePath), row.FindPropertyRelative("sceneName").stringValue);
            Assert.IsTrue(InBuild(FactoryScenePath), "The factory is not in the build settings, so its row draws disabled.");
            return;
        }
        Assert.Fail("The title screen has no TEAM FACTORY row.");
    }

    // BACK now goes wherever GarageScreenLoader remembers; titleSceneName is the fallback for a cold
    // open, so it still has to name a real scene.
    [Test]
    public void GarageScreenFallsBackToTheTitleAndLeadsOutToTheRace()
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

    // The factory is the walk-in half of the garage: a laptop to open the sheet on, and a door back out,
    // because nothing else in that scene changes scene and the pause menu doesn't arm itself there.
    [Test]
    public void TheFactoryHasALaptopAndAWayOut()
    {
        var factory = EditorSceneManager.OpenScene(FactoryScenePath, OpenSceneMode.Additive);
        try
        {
            Assert.IsNotNull(FindOrNull(factory, "LaptopInteractable"),
                             "The team factory has no laptop, so the garage sheet can't be opened there.");

            var door = Find(factory, "SceneDoorInteractable");
            Assert.AreEqual(SceneName(TitleScenePath), door.FindProperty("sceneName").stringValue,
                            "The factory's exit door should lead back to the title screen.");
        }
        finally
        {
            if (factory.IsValid() && factory.isLoaded) EditorSceneManager.CloseScene(factory, true);
        }
    }

    // The RV's laptop isn't in a scene to read — RVInterior generates the room when PitLaneStart hands it
    // a spawn point. So build one the way the game does and look at what came out. Both devices are
    // checked: the satnav is the pattern the laptop copies, and losing either is the same silent failure.
    [Test]
    public void TheRvInteriorCarriesALaptopAndASatnav()
    {
        var type = System.Type.GetType("RVInterior, Assembly-CSharp");
        Assert.IsNotNull(type, "RVInterior is missing from Assembly-CSharp.");

        var host = new GameObject("RVInteriorUnderTest");
        var player = new GameObject("PlayerUnderTest");
        try
        {
            var rv = host.AddComponent(type);
            var initialise = type.GetMethod("Initialize");
            Assert.IsNotNull(initialise, "RVInterior.Initialize is gone; this test builds the room through it.");
            initialise.Invoke(rv, new object[] { Vector3.zero, player.transform, null, null });

            Assert.IsTrue(HasComponent(host, "LaptopInteractable"),
                          "The RV interior built no laptop, so there is no way into the garage sheet from a race weekend.");
            Assert.IsTrue(HasComponent(host, "SatnavInteractable"),
                          "The RV interior built no satnav.");
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(player);
        }
    }

    // ------------------------------------------------------------------ helpers

    SerializedObject Menu() => Find(_title, "TitleScreenUI");

    static SerializedObject Find(Scene scene, string typeName)
    {
        var found = FindOrNull(scene, typeName);
        if (found == null) Assert.Fail($"{scene.name} has no {typeName}.");
        return found;
    }

    static SerializedObject FindOrNull(Scene scene, string typeName)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null && behaviour.GetType().Name == typeName)
                    return new SerializedObject(behaviour);
        return null;
    }

    static bool HasComponent(GameObject root, string typeName)
    {
        foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour != null && behaviour.GetType().Name == typeName) return true;
        return false;
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
