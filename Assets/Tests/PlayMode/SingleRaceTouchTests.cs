using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// SINGLE RACE has to be playable without a keyboard or a pad: on a phone every tile is a button (a tap selects,
// a tap on the selected tile moves on), and NEXT / RACE and BACK are buttons of their own.
public class SingleRaceTouchTests
{
    const string Scene = "SingleRace";

    [UnityTest]
    public IEnumerator TappingATilePicksIt()
    {
        PlayModeScenes.Go(Scene);
        yield return PlayModeScenes.WaitForScene(Scene);
        yield return PlayModeScenes.WaitFor(() => GameObject.Find("Tile_0") != null,
                                            "the single race screen built no track tiles");

        Assert.AreEqual("SELECT TRACK", Text(GameObject.Find("Title")));
        foreach (string name in new[] { "Next", "Back" })
            Assert.IsNotNull(GameObject.Find(name)?.GetComponent<Button>(), $"No {name} button for touch.");

        // The tile's Button sits on its fill.
        Button Tile(int i) => GameObject.Find($"Tile_{i}").GetComponentInChildren<Button>();

        // A tile other than the one the screen opened on (the last track raced is preselected).
        int other = SelectedIndex() == 0 ? 1 : 0;
        Tile(other).onClick.Invoke();
        yield return null;
        Assert.AreEqual("SELECT TRACK", Text(GameObject.Find("Title")), "The first tap should only select.");
        Tile(other).onClick.Invoke();
        yield return null;
        Assert.AreEqual("SELECT SERIES", Text(GameObject.Find("Title")), "Tapping the selected track did not pick it.");

        GameObject.Find("Back").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.AreEqual("SELECT TRACK", Text(GameObject.Find("Title")), "BACK did not step back to the track grid.");

        GameObject.Find("Next").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.AreEqual("SELECT SERIES", Text(GameObject.Find("Title")), "NEXT did not move on.");
    }

    // SingleRaceUI is in Assembly-CSharp, which the test assembly can't reference.
    static int SelectedIndex()
    {
        var type = System.Type.GetType("SingleRaceUI, Assembly-CSharp", throwOnError: true);
        var ui = Object.FindFirstObjectByType(type);
        return (int)type.GetProperty("SelectedIndex").GetValue(ui);
    }

    // TextMeshPro text, read without referencing TMP from the test assembly.
    static string Text(GameObject go)
    {
        foreach (var c in go.GetComponents<Component>())
        {
            var prop = c.GetType().GetProperty("text");
            if (prop != null && prop.PropertyType == typeof(string)) return (string)prop.GetValue(c);
        }
        return null;
    }
}
