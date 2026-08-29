using System.Collections.Generic;
using System.Linq;
using Draftmaster.Tracks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// The SINGLE RACE route: TitleScreen -> SingleRace -> RaceScene.
//
// This flow exists because there was no way in the game to race anything but the reference track: the
// only selector was an editor window, and the title screen's NEW SEASON row overwrote its choice. So the
// things worth pinning down are that the row exists and points somewhere real, that the scene it points
// at is in the build settings and actually carries the screen, and that the screen has something to
// offer — a track list that is not one track long.
//
// As with TitleScreenWiringTests, nothing here names a type from Assembly-CSharp: an assembly definition
// cannot reference the predefined assemblies, so scene components are read through SerializedObject and
// found by type name, the way the inspector reads them.
public class SingleRaceFlowTests
{
    const string TitleScenePath = "Assets/Scenes/TitleScreen.unity";
    const string SingleRaceScenePath = "Assets/Scenes/SingleRace.unity";
    const string RaceScenePath = "Assets/Scenes/RaceScene.unity";

    const int LoadScene = 3;   // TitleScreenUI.Command.LoadScene

    static bool InBuildSettings(string path) =>
        EditorBuildSettings.scenes.Any(s => s.path == path && s.enabled);

    static SerializedObject FindComponent(Scene scene, string typeName)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null) continue;
                if (component.GetType().Name == typeName) return new SerializedObject(component);
            }
        }
        return null;
    }

    [Test]
    public void TheSingleRaceSceneExistsAndIsInTheBuildSettings()
    {
        Assert.IsTrue(System.IO.File.Exists(SingleRaceScenePath),
                      "No SingleRace scene — run Draftmaster > UI > Build Single Race Scene.");
        Assert.IsTrue(InBuildSettings(SingleRaceScenePath),
                      "SingleRace is not in the build settings, so the title row will draw disabled.");
    }

    [Test]
    public void TheTitleScreenHasASingleRaceRowPointingAtIt()
    {
        var scene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Additive);
        try
        {
            var ui = FindComponent(scene, "TitleScreenUI");
            Assert.IsNotNull(ui, "The title scene has no TitleScreenUI.");

            var rows = ui.FindProperty("rows");
            string found = null;
            for (int i = 0; i < rows.arraySize; i++)
            {
                var row = rows.GetArrayElementAtIndex(i);
                if (row.FindPropertyRelative("label").stringValue != "SINGLE RACE") continue;

                Assert.AreEqual(LoadScene, row.FindPropertyRelative("command").enumValueIndex,
                                "SINGLE RACE should be a LoadScene row.");
                found = row.FindPropertyRelative("sceneName").stringValue;
            }

            Assert.IsNotNull(found, "No SINGLE RACE row on the title screen — rebuild it with " +
                                    "Draftmaster > Art > Rebuild Title Screen Scene (quiet).");
            Assert.AreEqual("SingleRace", found);
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [Test]
    public void TheSingleRaceSceneCarriesTheScreen()
    {
        var scene = EditorSceneManager.OpenScene(SingleRaceScenePath, OpenSceneMode.Additive);
        try
        {
            var ui = FindComponent(scene, "SingleRaceUI");
            Assert.IsNotNull(ui, "SingleRace.unity has no SingleRaceUI on it.");

            // Both ends of the flow have to be loadable or the screen is a dead end.
            string race = ui.FindProperty("raceSceneName").stringValue;
            string title = ui.FindProperty("titleSceneName").stringValue;
            Assert.IsTrue(InBuildSettings($"Assets/Scenes/{race}.unity"),
                          $"SingleRaceUI races '{race}', which is not in the build settings.");
            Assert.IsTrue(InBuildSettings($"Assets/Scenes/{title}.unity"),
                          $"SingleRaceUI backs out to '{title}', which is not in the build settings.");

            Assert.Greater(ui.FindProperty("visibleRows").intValue, 2,
                           "A list that shows fewer than three rows is not a list.");
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    // The point of the screen. If this ever collapses back to one, the track pipeline has regressed and
    // the flow is pointless.
    [Test]
    public void ThereIsMoreThanOneTrackToChooseFrom()
    {
        var playable = new List<string>();
        foreach (var dim in TrackDimensions.All)
            if (System.IO.File.Exists($"Assets/Resources/Tracks/{dim.id}.asset")) playable.Add(dim.id);

        Assert.Greater(playable.Count, 30,
                       $"Only {playable.Count} tracks are built — run Draftmaster > Tracks > " +
                       "Build All Calendar Tracks.");
    }

    // The race scene must stay track-free: a package left in it overrides the whole selection flow, which
    // is exactly the bug that made SINGLE RACE necessary. Checked on the asset, not the loaded scene, so
    // it fails on the committed file rather than on whatever happens to be open.
    [Test]
    public void TheRaceSceneStillHasNoTrackBakedIntoIt()
    {
        var bytes = System.IO.File.ReadAllBytes(RaceScenePath);
        var text = System.Text.Encoding.ASCII.GetString(bytes);

        Assert.IsFalse(text.Contains("Track_WatkinsGlen"),
                       "RaceScene.unity has a track package baked into it again. TrackSceneLoader adopts " +
                       "a track already in the scene and ignores TrackSelection, so every race would load " +
                       "that one. Clear it with Draftmaster > Tracks > Clear Package Previews From Scene " +
                       "and save the scene.");

        // A road, its meshes and its dressing are worth roughly a megabyte; the bare scene is ~110 KB.
        Assert.Less(bytes.Length, 600_000,
                    $"RaceScene.unity is {bytes.Length / 1024} KB — suspiciously large for a scene with no " +
                    "road in it. Check for a package preview left in it before saving.");
    }
}
