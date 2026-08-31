using System.Collections.Generic;
using System.Reflection;
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
    const int NotWired = 4;
    const int RestartDemo = 5;

    // TitleScreenUI.Build, in declaration order.
    const int Both = 0;
    const int DemoOnly = 1;
    const int FullOnly = 2;

    // DemoMode.OverrideKey. Spelled out rather than referenced: this assembly can't see Assembly-CSharp.
    const string DemoOverrideKey = "game.demo";

    Scene _title;
    int _demoOverrideWas;

    // Additive: the editor keeps whatever scene it had open while these run.
    [SetUp]
    public void OpenTitle()
    {
        _title = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Additive);

        // The menu is two menus now, and which one it builds depends on a flag that a developer may have
        // left flipped (Draftmaster > Demo > Preview Demo Menu). Pin it, so these tests read the full
        // release unless one of them says otherwise, and put it back afterwards.
        _demoOverrideWas = PlayerPrefs.GetInt(DemoOverrideKey, -1);
        ForceBuild(demo: false);
    }

    [TearDown]
    public void CloseTitle()
    {
        if (_demoOverrideWas < 0) PlayerPrefs.DeleteKey(DemoOverrideKey);
        else PlayerPrefs.SetInt(DemoOverrideKey, _demoOverrideWas);

        if (_title.IsValid() && _title.isLoaded) EditorSceneManager.CloseScene(_title, true);
    }

    static void ForceBuild(bool demo) => PlayerPrefs.SetInt(DemoOverrideKey, demo ? 1 : 0);

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

    // The menu is drawn by the layout in the scene and driven by the list on the component, and the two
    // are edited separately — a row dragged up the column doesn't move in the list. When they disagree
    // the cursor jumps around the menu instead of walking down it, which is only visible by pressing the
    // arrow keys. So the walk order the binder computes is checked against the column itself.
    [Test]
    public void ArrowKeysWalkTheMenuInTheOrderItIsDrawn()
    {
        var menu = Menu();
        var drawn = DrawnRowLabels(menu, demo: false);

        CollectionAssert.AreEqual(drawn, WalkOrder(menu),
                                  "Pressing DOWN does not move down the menu: the walk order and the column disagree.");

        // With the list and the column currently in the same order, that alone would also pass if the
        // binder just read the list. So turn the list upside down: the menu should still walk top to
        // bottom, because that is what the player sees.
        Reverse(menu);
        try
        {
            CollectionAssert.AreEqual(drawn, WalkOrder(menu),
                                      "The arrow keys follow the rows list rather than the column the player reads.");
        }
        finally
        {
            Reverse(menu);
        }
    }

    // The demo ships a shorter menu than the full game: the same column with the full-release rows
    // switched off and RESTART DEMO switched on. Which rows those are is a per-row flag in the scene, so
    // it is checked the same way the walk order is — by building the menu both ways and reading it back.
    [Test]
    public void TheDemoBuildDrawsTheDemoMenu()
    {
        var menu = Menu();

        ForceBuild(demo: true);
        var demo = WalkOrder(menu);
        ForceBuild(demo: false);
        var full = WalkOrder(menu);

        CollectionAssert.AreEqual(DrawnRowLabels(menu, demo: true), demo,
                                  "The demo build's menu is not the demo rows in column order.");
        CollectionAssert.Contains(demo, "RESTART DEMO", "The demo menu has no RESTART DEMO row.");
        CollectionAssert.DoesNotContain(full, "RESTART DEMO",
                                        "RESTART DEMO is drawn in the full release, which has no demo to restart.");
        CollectionAssert.IsNotSubsetOf(full, demo,
                                       "The demo menu offers everything the full game does; nothing is held back.");

        Assert.AreEqual(RestartDemo, CommandOf(menu, "RESTART DEMO"),
                        "The RESTART DEMO row does not run the restart command.");
    }

    // A row that is drawn but missing from the list can never be selected — the cursor steps over a
    // visible line and Enter on it does nothing.
    [Test]
    public void EveryDrawnMenuRowIsWiredIntoTheList()
    {
        var rows = Menu().FindProperty("rows");
        var wired = new List<string>();
        for (int i = 0; i < rows.arraySize; i++)
        {
            var rect = rows.GetArrayElementAtIndex(i).FindPropertyRelative("rect").objectReferenceValue;
            Assert.IsNotNull(rect, $"Row '{rows.GetArrayElementAtIndex(i).FindPropertyRelative("label").stringValue}' has no rect.");
            wired.Add(RowLabel(rect.name));
        }

        foreach (string drawn in DrawnRowLabels())
            Assert.Contains(drawn, wired, $"The menu draws a '{drawn}' row that is not in TitleScreenUI.rows.");
        Assert.AreEqual(DrawnRowLabels().Count, wired.Count, "TitleScreenUI.rows holds rows the menu does not draw.");
    }

    // ------------------------------------------------------------------ helpers

    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    // The row labels in the order the arrow keys would step through them.
    static List<string> WalkOrder(SerializedObject menu)
    {
        var binder = menu.targetObject;

        var rebuild = binder.GetType().GetMethod("RebuildOrder", Flags);
        Assert.IsNotNull(rebuild, "TitleScreenUI.RebuildOrder is gone; the menu no longer sorts its rows by where they are drawn.");
        rebuild.Invoke(binder, null);

        var order = (int[])binder.GetType().GetField("_order", Flags).GetValue(binder);
        Assert.IsNotNull(order, "TitleScreenUI built no walk order.");

        menu.Update();
        var rows = menu.FindProperty("rows");
        var walked = new List<string>();
        foreach (int i in order)
            walked.Add(rows.GetArrayElementAtIndex(i).FindPropertyRelative("label").stringValue);
        return walked;
    }

    // Flips the rows list in place. Applied to the live component so the binder sees it; run twice it
    // puts the list back exactly as it was, which is why the caller does.
    static void Reverse(SerializedObject menu)
    {
        var rows = menu.FindProperty("rows");
        for (int i = 0; i < rows.arraySize - 1; i++) rows.MoveArrayElement(rows.arraySize - 1, i);
        menu.ApplyModifiedPropertiesWithoutUndo();
    }

    // The labels the player reads down the column in a given build: the layout, minus the rows that build
    // switches off. TitleScreenUI closes the column up over the gaps at runtime, so the order is the same
    // either way — it is the membership that changes.
    List<string> DrawnRowLabels(SerializedObject menu, bool demo)
    {
        var appearsIn = new Dictionary<string, int>();
        var rows = menu.FindProperty("rows");
        for (int i = 0; i < rows.arraySize; i++)
        {
            var row = rows.GetArrayElementAtIndex(i);
            var rect = row.FindPropertyRelative("rect").objectReferenceValue;
            if (rect != null) appearsIn[RowLabel(rect.name)] = row.FindPropertyRelative("appearsIn").enumValueIndex;
        }

        var shown = new List<string>();
        foreach (string label in DrawnRowLabels())
        {
            int where = appearsIn.TryGetValue(label, out int v) ? v : Both;
            if (where == Both || (where == DemoOnly) == demo) shown.Add(label);
        }
        return shown;
    }

    // Every row the column draws, top first, whatever build it belongs to.
    List<string> DrawnRowLabels()
    {
        var menu = MenuColumn();
        var found = new List<RectTransform>();
        foreach (RectTransform child in menu)
            if (child.name.StartsWith("Row_")) found.Add(child);
        Assert.IsNotEmpty(found, "The title menu draws no rows.");

        found.Sort((a, b) => b.anchoredPosition.y.CompareTo(a.anchoredPosition.y));

        var labels = new List<string>();
        foreach (var rect in found) labels.Add(RowLabel(rect.name));
        return labels;
    }

    RectTransform MenuColumn()
    {
        foreach (var root in _title.GetRootGameObjects())
        {
            var menu = root.transform.Find("Column/Menu") as RectTransform;
            if (menu != null) return menu;
        }
        Assert.Fail("The title screen has no Column/Menu to draw rows in.");
        return null;
    }

    static string RowLabel(string objectName) =>
        objectName.StartsWith("Row_") ? objectName.Substring("Row_".Length).Replace('_', ' ') : objectName;

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
