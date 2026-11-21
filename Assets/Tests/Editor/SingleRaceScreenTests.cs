using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// SINGLE RACE's three screens (SingleRaceUI, built to Designs/draftmaster3_race_flow_unity_handoff) opened in the
// editor with no scene: the track grid with its filters, the series grid, the car grid, the cursor, taps and BACK.
// SingleRaceUI lives in Assembly-CSharp, which an asmdef can't reference, so it is reached by reflection.
public class SingleRaceScreenTests
{
    static readonly Type UiType = Type.GetType("SingleRaceUI, Assembly-CSharp", throwOnError: true);

    GameObject _go;
    Component _ui;

    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("SingleRaceScreenTest");
        _ui = _go.AddComponent(UiType);
        Call("Open");
    }

    [TearDown]
    public void TearDown()
    {
        if (_go != null) Object.DestroyImmediate(_go);
    }

    object Call(string method, params object[] args)
    {
        foreach (var m in UiType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            if (m.Name == method && m.GetParameters().Length == args.Length) return m.Invoke(_ui, args);
        throw new MissingMethodException(UiType.Name, method);
    }

    T Get<T>(string property) =>
        (T)UiType.GetProperty(property, BindingFlags.Public | BindingFlags.Instance).GetValue(_ui);

    string Step => Get<string>("StepName");
    int Count => Get<int>("OptionCount");
    int Index => Get<int>("SelectedIndex");

    Transform Find(string name)
    {
        foreach (var t in _go.GetComponentsInChildren<Transform>(false))
            if (t.name == name) return t;
        return null;
    }

    static string Text(Transform t)
    {
        foreach (var c in t.GetComponents<Component>())
        {
            var prop = c.GetType().GetProperty("text");
            if (prop != null && prop.PropertyType == typeof(string)) return (string)prop.GetValue(c);
        }
        return null;
    }

    [Test]
    public void TheTrackScreenListsEveryBuiltTrackAsTiles()
    {
        Assert.AreEqual("Track", Step);
        Assert.AreEqual("SELECT TRACK", Get<string>("TitleText"));
        Assert.Greater(Count, 30, "Fewer than 31 tracks on the track screen.");
        Assert.AreEqual(Count, Get<int>("TileCount"), "Every track should be a tile.");
        Assert.IsNotNull(Find("Tile_0"));
        Assert.IsNotNull(Find("Back")?.GetComponent<Button>(), "No BACK button.");
        Assert.IsNotNull(Find("Next")?.GetComponent<Button>(), "No NEXT button.");
        foreach (var f in new[] { "ALL", "ROAD", "OVAL", "DIRT" })
            Assert.IsNotNull(Find("Filter_" + f)?.GetComponent<Button>(), $"No {f} filter chip.");
        StringAssert.EndsWith($"/ {Count}", Text(Find("Position")));
    }

    [Test]
    public void TrackTilesCarryTheirLayoutTagLengthAndTown()
    {
        var tile = Find("Tile_0");
        var layout = tile.Find("Art/Layout")?.GetComponent<RawImage>();
        Assert.IsNotNull(layout, "No layout on the first track tile.");
        Assert.IsNotNull(layout.texture, "The first track's layout wasn't drawn from its spline.");
        Assert.AreEqual(FilterMode.Point, layout.texture.filterMode);
        StringAssert.EndsWith(" MI", Text(tile.Find("Art/Length/LengthText")));
        CollectionAssert.Contains(new[] { "ROAD", "STREET", "OVAL", "DIRT" }, Text(tile.Find("Art/Type")));
        StringAssert.Contains(", ", Text(tile.Find("Place")));
    }

    [Test]
    public void FiltersNarrowTheTrackList()
    {
        int all = Count;
        Call("StepFilter", 1);   // ROAD
        Assert.AreEqual("ROAD", Get<string>("FilterName"));
        int road = Count;
        Call("StepFilter", 1);   // OVAL
        int oval = Count;
        Assert.Greater(road, 0);
        Assert.Greater(oval, road, "More ovals than road courses on the calendar.");
        Assert.LessOrEqual(road + oval, all);
        StringAssert.StartsWith($"{oval} TRACKS", Text(Find("Context")));
    }

    [Test]
    public void TheCursorMovesAndATapSelectsThenConfirms()
    {
        int cols = Get<int>("Columns");
        Assert.GreaterOrEqual(cols, 1);
        Call("TapTile", 0);
        Assert.AreEqual(0, Index);
        Call("MoveCursor", 1, 0);
        Assert.AreEqual(1, Index);
        Call("MoveCursor", 0, 1);
        Assert.AreEqual(1 + cols, Index);

        Call("TapTile", 2);
        Assert.AreEqual(2, Index, "A tap on another tile selects it.");
        Assert.AreEqual("Track", Step, "The first tap only selects.");
        Call("TapTile", 2);
        Assert.AreEqual("Series", Step, "Tapping the selected tile moves on.");
    }

    [Test]
    public void AnUnbuiltTrackIsGreyedAndCannotBePicked()
    {
        int unbuilt = -1;
        for (int i = 0; i < Get<int>("TileCount"); i++)
            if (Text(Find("Tile_" + i).Find("Place")) == Draftmaster.Tracks.RaceFlow.NotBuiltNote) { unbuilt = i; break; }
        Assert.GreaterOrEqual(unbuilt, 3, "The three built venues come first, then the greyed-out rest.");
        Call("TapTile", unbuilt);
        Call("TapTile", unbuilt);
        Assert.AreEqual("Track", Step, "An unbuilt track moved on to the series.");
    }

    [Test]
    public void SeriesThenCarThenBack()
    {
        Call("ConfirmSelection");
        Assert.AreEqual("Series", Step);
        Assert.AreEqual("SELECT SERIES", Get<string>("TitleText"));
        Assert.AreEqual(7, Count, "The seven championships of the world.");
        Assert.AreEqual("CUP", Text(Find("Tile_" + Index).Find("Art/Code")),
                        "Series Select should open on the Cup, the field that's seeded.");

        Call("ConfirmSelection");
        Assert.AreEqual("Car", Step);
        Assert.AreEqual("SELECT CAR", Get<string>("TitleText"));
        Assert.Greater(Count, 20, "The Cup field should be on the car screen.");
        Assert.IsNotNull(Find("Race")?.GetComponent<Button>(), "No RACE button on the last screen.");
        StringAssert.Contains(" > ", Text(Find("Context")), "No Track > Series breadcrumb.");
        Assert.IsNotNull(Find("You"), "The player's car has no YOU chip.");
        Assert.IsNotNull(Find("Tile_0").Find("Art/Livery")?.GetComponent<Image>()?.sprite,
                         "The first car has no livery.");

        Find("Back").GetComponent<Button>().onClick.Invoke();
        Assert.AreEqual("Series", Step, "BACK from cars goes to series.");
        Call("GoBack");
        Assert.AreEqual("Track", Step, "BACK from series goes to tracks.");
    }

    // The handoff's phone layout: bigger tiles, no keycaps, a "TAP A ..." hint, a taller footer button.
    [Test]
    public void ThePhoneLayoutUsesBiggerTilesAndNoKeycaps()
    {
        UiType.GetProperty("ForceHandheld").SetValue(_ui, true);
        Assert.AreEqual(150f, ((RectTransform)Find("Tile_0")).sizeDelta.x);
        Assert.IsNull(Find("Key_ENTER"), "A phone shows no ENTER keycap.");
        Assert.IsNull(Find("Key_ESC"), "A phone shows no ESC keycap.");
        Assert.IsNotNull(Find("Hint_TAP A TRACK · SWIPE FOR MORE"));
        Assert.AreEqual(26f, ((RectTransform)Find("Next")).sizeDelta.y);

        UiType.GetProperty("ForceHandheld").SetValue(_ui, false);
        Assert.AreEqual(140f, ((RectTransform)Find("Tile_0")).sizeDelta.x);
        Assert.IsNotNull(Find("Key_ENTER"), "PC shows the ENTER keycap on NEXT.");
    }

    [Test]
    public void AnEmptySeriesStillOffersThePlayersOwnCar()
    {
        Call("ConfirmSelection");
        // Find a series with no field ("NO FIELD" chip).
        int empty = -1;
        for (int i = 0; i < Count; i++)
            if (Text(Find("Tile_" + i).Find("Art/Field/FieldText")) == "NO FIELD") { empty = i; break; }
        Assume.That(empty, Is.GreaterThanOrEqualTo(0), "Every series has a field.");

        Call("TapTile", empty);
        Call("ConfirmSelection");
        Assert.AreEqual("Car", Step);
        Assert.AreEqual(1, Count);
        Assert.IsNotNull(Find("You"));
    }
}
