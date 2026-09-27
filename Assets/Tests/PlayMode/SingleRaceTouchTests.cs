using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// SINGLE RACE has to be playable without a keyboard or a pad: on a phone the rows are the buttons, and
// paging and BACK are buttons of their own.
public class SingleRaceTouchTests
{
    const string Scene = "SingleRace";

    [UnityTest]
    public IEnumerator TappingARowPicksIt()
    {
        PlayModeScenes.Go(Scene);
        yield return PlayModeScenes.WaitForScene(Scene);
        yield return PlayModeScenes.WaitFor(() => GameObject.Find("Hit_0") != null,
                                            "the single race screen built no tappable rows");

        var title = GameObject.Find("Title");
        Assert.AreEqual("SELECT TRACK", Text(title));

        foreach (string name in new[] { "Up", "Down", "Back" })
            Assert.IsNotNull(GameObject.Find(name)?.GetComponent<Button>(), $"No {name} button for touch.");

        GameObject.Find("Hit_0").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.AreEqual("SELECT SERIES", Text(title), "Tapping a track did not pick it.");

        GameObject.Find("Back").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.AreEqual("SELECT TRACK", Text(title), "BACK did not step back to the track list.");
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
