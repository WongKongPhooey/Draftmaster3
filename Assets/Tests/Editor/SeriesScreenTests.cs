using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Draftmaster.MasterDraft;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// The Master Draft Series screen (MasterDraftUI, rework row 7) built in the editor against a test profile and
// test catalogues: no session, no backend, no scene — MasterDraftUI.BuildOfflineSeries. Checks it builds without
// errors, lists the garage cars that can enter, and that starting a championship turns the panel into its rounds.
// The race launch itself (SeriesRace.Plan) and the scoring are MasterDraftSeriesRaceTests, engine-free.
//
// Not named MasterDraft*Tests: those compile into Tools/HeadlessTests, and this needs Unity. MasterDraftUI lives
// in Assembly-CSharp, which an asmdef can't reference, so it is reached by reflection (as GarageScreenTests is).
public class SeriesScreenTests
{
    static readonly Type UiType = Type.GetType("MasterDraftUI, Assembly-CSharp", throwOnError: true);

    GameObject _go;
    Component _ui;

    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("SeriesScreenTest");
        _ui = _go.AddComponent(UiType);
    }

    [TearDown]
    public void TearDown()
    {
        if (_go != null) Object.DestroyImmediate(_go);
    }

    object Call(string method, params object[] args) =>
        UiType.GetMethod(method, BindingFlags.Public | BindingFlags.Instance).Invoke(_ui, args);

    T Get<T>(string property) =>
        (T)UiType.GetProperty(property, BindingFlags.Public | BindingFlags.Instance).GetValue(_ui);

    Transform Content => _go.transform.Find("Canvas/Content");

    Button ContentButton(string key) =>
        Content.Cast<Transform>().FirstOrDefault(t => t.name == "Button_" + key)?.GetComponent<Button>();

    // Six cars, classes set by the profile.
    static CarCatalog TestCatalogue()
    {
        var file = new CarCatalogFile { version = 1 };
        file.carsets.Add(new CarsetDef { id = "tst01", name = "Test '01", cars = 6 });
        for (int i = 1; i <= 6; i++)
            file.cars.Add(new CarDef
            {
                id = CarCatalog.IdFor("tst01", i), carset = "tst01", number = i, driver = "Driver" + i,
                team = i <= 3 ? "AAA" : "BBB", manufacturer = "CHV", rarity = 1 + i % 4, type = DriverTypes.Rookie,
                livery = "nolivery_test_" + i,
            });
        return new CarCatalog(file);
    }

    // Two menus: an open one (level 1, class 1+) with two series, and one that opens at level 5 (team AAA only).
    static DraftSeriesCatalog TestSeries()
    {
        SeriesDef Series(string id, string menu, int level, int carClass, string rule = "", string value = "") => new SeriesDef
        {
            id = id, menu = menu, name = id, aiLevel = 1,
            restriction = new Restriction(level, carClass, rule, value),
            prize = new PrizeFilter(),
            rounds = new List<SeriesRound>
            {
                new SeriesRound { track = "Daytona", baseLaps = 4 },
                new SeriesRound { track = "Martinsville", baseLaps = 6 },
            },
        };
        var file = new SeriesFile { version = 1 };
        file.menus.Add(new SeriesMenu
        {
            id = "open", name = "Open",
            series = new List<SeriesDef> { Series("open_a", "open", 1, 1), Series("open_b", "open", 1, 3) },
        });
        file.menus.Add(new SeriesMenu
        {
            id = "team", name = "Team",
            series = new List<SeriesDef> { Series("team_aaa", "team", 5, 0, RestrictionRule.Team, "AAA") },
        });
        return new DraftSeriesCatalog(file);
    }

    static MasterDraftProfile TestProfile(int level, params OwnedCar[] cars) => new MasterDraftProfile
    {
        schemaVersion = MasterDraftProfile.CurrentSchema,
        playerId = "series-test",
        level = level,
        garage = cars.ToList(),
        selectedCar = cars.Length > 0 ? cars[0].id : "",
    };

    [Test]
    public void Series_ListsTheGarageCarsThatCanEnter()
    {
        // tst01_1 is class 0 (too low for open_a), tst01_2 class 1, tst01_5 class 2.
        var profile = TestProfile(1, new OwnedCar("tst01_1", 0, 0), new OwnedCar("tst01_2", 1, 0), new OwnedCar("tst01_5", 2, 0));
        Call("BuildOfflineSeries", profile, TestCatalogue(), TestSeries(), "open_a");

        Assert.AreEqual("open_a", Get<string>("SeriesPicked"));
        Assert.AreEqual(2, Get<int>("SeriesEligibleCount"));
        Assert.AreEqual("tst01_2", Get<string>("SeriesEntryCar"), "the selected car can't enter: the first one that can");
        Assert.IsTrue(Get<bool>("SeriesCanStart"));
        Assert.IsTrue(ContentButton("se.go").interactable);
        Assert.IsNotNull(ContentButton("se.car"), "a car picker when cars can enter");

        // Class 3+: nobody.
        Call("PickSeries", "open_b");
        Assert.AreEqual(0, Get<int>("SeriesEligibleCount"));
        Assert.IsFalse(Get<bool>("SeriesCanStart"));
        Assert.IsFalse(ContentButton("se.go").interactable);

        // Level 5 menu at level 1: locked, with the level on the row.
        Call("PickSeries", "team_aaa");
        Assert.AreEqual("team_aaa", Get<string>("SeriesPicked"));
        Assert.IsFalse(Get<bool>("SeriesCanStart"));
    }

    [Test]
    public void Series_StartingAChampionshipShowsItsFirstRound()
    {
        var profile = TestProfile(1, new OwnedCar("tst01_2", 1, 0));
        Call("BuildOfflineSeries", profile, TestCatalogue(), TestSeries(), "open_a");

        Assert.IsTrue((bool)Call("StartPickedChampionship"));
        var running = profile.Championship("open_a");
        Assert.IsNotNull(running);
        Assert.AreEqual("tst01_2", running.carId);
        Assert.AreEqual(6, running.table.Count, "the carset is the field");
        Assert.IsTrue(Get<bool>("SeriesInProgress"));

        var go = ContentButton("se.go");
        Assert.IsNotNull(go);
        Assert.IsTrue(go.interactable);
        StringAssert.Contains("Race Round 1", go.GetComponentInChildren<TMPro.TMP_Text>().text);
        Assert.IsNotNull(ContentButton("se.quit"));
    }
}
