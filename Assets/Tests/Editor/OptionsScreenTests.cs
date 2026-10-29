using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// The OPTIONS route: TitleScreen -> Options -> back to TitleScreen.
//
// OPTIONS was a NotWired placeholder for as long as the title screen has existed, which meant pressing it
// printed "Not wired up yet." on the status line. What is pinned here is that it now goes somewhere real,
// that the somewhere is in the build settings (a missing destination draws the row disabled, which is only
// visible in play mode), that the screen it lands on can get back, and that the name it edits round-trips
// through the save the way every reader of that save expects.
//
// As with TitleScreenWiringTests, nothing here names a type from Assembly-CSharp: this assembly cannot
// reference the predefined assemblies, so scene components are read through SerializedObject and found by
// type name, and PlayerDriver's pure name helpers are called by reflection.
public class OptionsScreenTests
{
    const string TitleScenePath = "Assets/Scenes/TitleScreen.unity";
    const string OptionsScenePath = "Assets/Scenes/Options.unity";

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

    static Type PlayerDriver()
    {
        var type = AppDomain.CurrentDomain.GetAssemblies()
                            .Select(a => a.GetType("PlayerDriver", false))
                            .FirstOrDefault(t => t != null);
        Assert.IsNotNull(type, "No PlayerDriver type — the career identity helpers have moved.");
        return type;
    }

    [Test]
    public void TheOptionsSceneExistsAndIsInTheBuildSettings()
    {
        Assert.IsTrue(System.IO.File.Exists(OptionsScenePath),
                      "No Options scene — run Draftmaster > UI > Build Options Scene.");
        Assert.IsTrue(InBuildSettings(OptionsScenePath),
                      "Options is not in the build settings, so the title row will draw disabled.");
    }

    [Test]
    public void TheTitleScreenHasAnOptionsRowPointingAtIt()
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
                if (row.FindPropertyRelative("label").stringValue != "OPTIONS") continue;

                Assert.AreEqual(LoadScene, row.FindPropertyRelative("command").enumValueIndex,
                                "OPTIONS should be a LoadScene row, not the NotWired placeholder it started as.");
                found = row.FindPropertyRelative("sceneName").stringValue;
            }

            Assert.IsNotNull(found, "No OPTIONS row on the title screen — wire it with " +
                                    "Draftmaster > UI > Wire OPTIONS Row To Options Screen.");
            Assert.AreEqual("Options", found);
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [Test]
    public void TheOptionsSceneCarriesTheScreenAndCanGetBack()
    {
        var scene = EditorSceneManager.OpenScene(OptionsScenePath, OpenSceneMode.Additive);
        try
        {
            var ui = FindComponent(scene, "OptionsUI");
            Assert.IsNotNull(ui, "Options.unity has no OptionsUI on it.");

            string title = ui.FindProperty("titleSceneName").stringValue;
            Assert.IsTrue(InBuildSettings($"Assets/Scenes/{title}.unity"),
                          $"OptionsUI backs out to '{title}', which is not in the build settings — " +
                          "the screen would be a dead end.");
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    // The two boxes on the screen and the one name the rest of the game reads have to agree. Pure string
    // work — nothing here touches PlayerPrefs, so running the tests cannot rename whoever is mid-career.
    [Test]
    public void AFullNameSplitsIntoTheTwoBoxesTheScreenDraws()
    {
        var split = PlayerDriver().GetMethod("SplitFullName", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(split, "PlayerDriver.SplitFullName has gone.");

        Assert.AreEqual(("Kyle", "Larson"), Split(split, "Kyle Larson"));
        // Everything after the first word is the surname, so a suffix stays attached to it.
        Assert.AreEqual(("Ricky", "Stenhouse Jr"), Split(split, "Ricky Stenhouse Jr"));
        Assert.AreEqual(("Josh", ""), Split(split, "Josh"));
        Assert.AreEqual(("", ""), Split(split, ""));
        Assert.AreEqual(("", ""), Split(split, "   "));
        Assert.AreEqual(("Kyle", "Larson"), Split(split, "  Kyle   Larson  "),
                        "Padding around a saved name should not become part of it.");
    }

    static (string, string) Split(MethodInfo method, string full)
    {
        var args = new object[] { full, null, null };
        method.Invoke(null, args);
        return ((string)args[1], (string)args[2]);
    }

    [Test]
    public void ATypedNameIsTrimmedRatherThanRejected()
    {
        var type = PlayerDriver();
        var clean = type.GetMethod("CleanNameHalf", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(clean, "PlayerDriver.CleanNameHalf has gone.");

        int limit = (int)type.GetField("MaxNameHalfLength", BindingFlags.Public | BindingFlags.Static)
                             .GetValue(null);
        Assert.Greater(limit, 2, "A name limit under three characters is not a name field.");

        Assert.AreEqual("Josh", clean.Invoke(null, new object[] { "  Josh  " }));
        Assert.AreEqual("Van Der Berg", clean.Invoke(null, new object[] { "Van   Der  Berg" }),
                        "Runs of spaces inside a surname should collapse, not survive.");
        Assert.AreEqual("", clean.Invoke(null, new object[] { "   " }));
        Assert.AreEqual("", clean.Invoke(null, new object[] { null }));

        string overlong = (string)clean.Invoke(null, new object[] { new string('W', limit + 8) });
        Assert.AreEqual(limit, overlong.Length,
                        $"A name longer than {limit} should be cut to fit the columns it is drawn in.");
    }
}
