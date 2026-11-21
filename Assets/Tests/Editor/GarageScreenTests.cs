using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Draftmaster.MasterDraft;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// The Master Draft Garage tab, the Card Collection screen (MasterDraftUI.Collection, to the Draftmaster3_UI_Unity
// design's Collection_PC), built in the editor against a test profile: no session, no backend, no scene —
// MasterDraftUI.BuildOffline. Checks it builds without errors, the Builds tab shows the one build (the highest rated
// driver, car and crew chief card) and the car detail's upgrade spends gears and coins. The build and gears rules
// themselves are MasterDraftRulesTests / MasterDraftCollectionTests (Core, headless).
//
// Not named MasterDraft*Tests on purpose: those files are compiled engine-free into Tools/HeadlessTests, and
// this one needs Unity. MasterDraftUI lives in Assembly-CSharp, which an asmdef can't reference, so it is
// reached by reflection (as ButtonStandardTests does). An error or exception logged while building fails the
// test on its own (the test runner treats unexpected error logs as failures); plain Debug.Log lines from the
// UI kit (EditorPlayView's one-off play-view note) are allowed.
public class GarageScreenTests
{
    static readonly Type UiType = Type.GetType("MasterDraftUI, Assembly-CSharp", throwOnError: true);

    GameObject _go;
    Component _ui;

    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("GarageScreenTest");
        _ui = _go.AddComponent(UiType);
    }

    [TearDown]
    public void TearDown()
    {
        if (_go != null) Object.DestroyImmediate(_go);
    }

    // ---- helpers

    object Call(string method, params object[] args) =>
        UiType.GetMethod(method, BindingFlags.Public | BindingFlags.Instance).Invoke(_ui, args);

    T Get<T>(string property) =>
        (T)UiType.GetProperty(property, BindingFlags.Public | BindingFlags.Instance).GetValue(_ui);

    Transform Content => _go.transform.Find("Canvas/Content");
    Transform Grid => _go.transform.Find("Canvas/CollectionViewport/Grid");

    static string TextOf(Transform t) => t == null ? null : t.GetComponent<TMP_Text>()?.text;

    Transform Detail => _go.transform.Find("Canvas/CardDetail");

    Transform DetailFind(string name) =>
        Detail == null ? null : Detail.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

    Button DetailButton(string key) => DetailFind("Button_" + key)?.GetComponent<Button>();

    static string CaptionOf(Button button) => button == null ? null : button.GetComponentInChildren<TMP_Text>(true)?.text;

    // 20 cars over two carsets, three teams, two makes, rarities 1-4. Livery names don't exist: the card
    // falls back to "[ Car ]".
    static CarCatalog TestCatalogue()
    {
        var file = new CarCatalogFile { version = 1 };
        file.carsets.Add(new CarsetDef { id = "tst01", name = "Test '01", cars = 12 });
        file.carsets.Add(new CarsetDef { id = "tst02", name = "Test '02", cars = 8 });
        string[] teams = { "AAA", "BBB", "CCC" };
        string[] makes = { "CHV", "FRD" };
        for (int i = 0; i < 20; i++)
        {
            string set = i < 12 ? "tst01" : "tst02";
            file.cars.Add(new CarDef
            {
                id = CarCatalog.IdFor(set, i), carset = set, number = i, driver = "Driver" + i,
                team = teams[i % 3], manufacturer = makes[i % 2], rarity = 1 + i % 4, type = DriverTypes.Rookie,
                livery = "nolivery_test_" + i,
            });
        }
        return new CarCatalog(file);
    }

    static MasterDraftProfile TestProfile(int coins, params OwnedCar[] cars) => new MasterDraftProfile
    {
        schemaVersion = MasterDraftProfile.CurrentSchema,
        playerId = "garage-test",
        coins = coins,
        garage = cars.ToList(),
        selectedCar = cars.Length > 0 ? cars[0].id : "",
    };

    // ---- tests

    static CardInstance AddCard(MasterDraftProfile p, CardType type, int overall, string name) =>
        p.Add(new CardDefinition { id = name, type = type, name = name, overall = overall });

    Transform BuildSlot(CardType type) => Content.Find("Build/Slot_" + type);

    string SlotCardName(CardType type) => TextOf(BuildSlot(type)?.Find("Card/Inner/Name"));

    [Test]
    public void Builds_ShowsOneBuildOfTheHighestRatedCards()
    {
        var profile = TestProfile(0);
        AddCard(profile, CardType.Driver, 71, "Slow Driver");
        AddCard(profile, CardType.Driver, 88, "Fast Driver");
        AddCard(profile, CardType.Car, 80, "Good Car");
        AddCard(profile, CardType.Car, 64, "Bad Car");
        AddCard(profile, CardType.CrewChief, 59, "Chief B");
        AddCard(profile, CardType.CrewChief, 77, "Chief A");
        Call("BuildOffline", profile, TestCatalogue());

        Assert.AreEqual(1, Content.Cast<Transform>().Count(t => t.name == "Build"), "exactly one build");
        Assert.AreEqual("Fast Driver", SlotCardName(CardType.Driver));
        Assert.AreEqual("Good Car", SlotCardName(CardType.Car));
        Assert.AreEqual("Chief A", SlotCardName(CardType.CrewChief));

        var build = Get<CardBuild>("GarageBuild");
        Assert.IsTrue(build.Complete);
        Assert.AreEqual(TeamRules.Rating(build.driver.def, build.car.def, build.crewChief.def), build.Rating);
        Assert.AreEqual($"Best Build   OVR {build.Rating}", TextOf(Content.Find("Build/Heading")));

        // The tabs stay; Builds has nothing to sort or filter.
        foreach (var t in new[] { "Builds", "Cars", "Drivers", "Crew Chiefs", "Numbers" })
            Assert.IsNotNull(Content.Find("GarageTab_" + t), t + " tab");
        Assert.IsNull(Content.Find("GarageTab_Teams"), "Crew Chiefs replaced the Teams tab");
        Assert.IsNull(Content.Find("Cycler_Sort"));
        Assert.IsNull(Content.Find("Cycler_Rarity"));
        Assert.IsNull(Grid, "no card grid on Builds");
    }

    [Test]
    public void Builds_EmptySlotsShowACardBack()
    {
        var profile = TestProfile(0);
        AddCard(profile, CardType.Driver, 70, "Only Driver");
        Call("BuildOffline", profile, TestCatalogue());

        Assert.AreEqual("Only Driver", SlotCardName(CardType.Driver));
        Assert.IsNull(SlotCardName(CardType.Car), "no car card: a card back, no name");
        Assert.AreEqual("No Car card yet", TextOf(BuildSlot(CardType.Car).Find("Label")));
        Assert.AreEqual("No Crew Chief card yet", TextOf(BuildSlot(CardType.CrewChief).Find("Label")));
        Assert.IsFalse(Get<CardBuild>("GarageBuild").Complete);
        Assert.AreEqual("Best Build", TextOf(Content.Find("Build/Heading")));
    }

    [Test]
    public void Drivers_ShowsEveryDriverCardInTheCollection()
    {
        var profile = TestProfile(0);
        var drivers = new List<CardInstance>();
        for (int i = 0; i < 11; i++) drivers.Add(AddCard(profile, CardType.Driver, 60 + i * 3, "Driver " + i));
        drivers.Add(AddCard(profile, CardType.Driver, 60 + 10 * 3, "Driver 10"));   // a second copy of the best one
        AddCard(profile, CardType.Car, 99, "Not A Driver");
        AddCard(profile, CardType.CrewChief, 99, "Not A Driver Either");
        Call("BuildOffline", profile, TestCatalogue());
        Call("SetGarageTab", GarageTab.Drivers);

        // Every driver card, each copy, best OVR first; nothing else.
        var expected = drivers.OrderByDescending(c => c.def.overall).ThenBy(c => c.def.name, StringComparer.Ordinal)
                              .ThenBy(c => c.uid, StringComparer.Ordinal).Select(c => c.uid).ToList();
        CollectionAssert.AreEqual(expected, Get<IReadOnlyList<string>>("GarageCardIds").ToList());
        Assert.AreEqual(12, Get<int>("GarageFilteredCount"));
        Assert.AreEqual(12, Get<int>("GarageCollectedCount"));
        Assert.AreEqual(3, Get<int>("GarageRowCount"));
        Assert.AreEqual(12, Get<int>("GarageCardsShown"), "two whole rows and the peeking third");

        Assert.IsNotNull(Grid, "driver cards sit on the collection grid");
        foreach (var c in drivers)
            Assert.AreEqual(c.def.name, TextOf(Grid.Find("Card_" + c.uid + "/Inner/Name")), c.uid);
        Assert.IsNull(Content.Find("Cycler_Sort"), "Drivers shows every card: nothing to sort");
        Assert.IsNull(Content.Find("Cycler_Rarity"), "nor filter");
    }

    [Test]
    public void Drivers_NoDriverCardsSaysSo()
    {
        var profile = TestProfile(0);
        AddCard(profile, CardType.Car, 70, "Only Car");
        Call("BuildOffline", profile, TestCatalogue());
        Call("SetGarageTab", GarageTab.Drivers);

        Assert.AreEqual(0, Get<IReadOnlyList<string>>("GarageCardIds").Count);
        Assert.IsNull(Grid);
        Assert.IsTrue(Content.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "No driver cards yet."));
    }

    [Test]
    public void CrewChiefs_ShowsEveryCrewChiefCardInTheCollection()
    {
        var profile = TestProfile(0);
        var chiefs = new List<CardInstance>();
        for (int i = 0; i < 6; i++) chiefs.Add(AddCard(profile, CardType.CrewChief, 55 + i * 4, "Chief " + i));
        chiefs.Add(AddCard(profile, CardType.CrewChief, 55, "Chief 0"));   // a second copy of the worst one
        AddCard(profile, CardType.Driver, 99, "Not A Chief");
        AddCard(profile, CardType.Car, 99, "Not A Chief Either");
        Call("BuildOffline", profile, TestCatalogue());
        Call("SetGarageTab", GarageTab.CrewChiefs);

        // Every crew chief card, each copy, best OVR first; nothing else.
        var expected = chiefs.OrderByDescending(c => c.def.overall).ThenBy(c => c.def.name, StringComparer.Ordinal)
                             .ThenBy(c => c.uid, StringComparer.Ordinal).Select(c => c.uid).ToList();
        CollectionAssert.AreEqual(expected, Get<IReadOnlyList<string>>("GarageCardIds").ToList());
        Assert.AreEqual(7, Get<int>("GarageFilteredCount"));
        Assert.AreEqual(7, Get<int>("GarageCollectedCount"));

        Assert.IsNotNull(Grid, "crew chief cards sit on the collection grid");
        foreach (var c in chiefs)
            Assert.AreEqual(c.def.name, TextOf(Grid.Find("Card_" + c.uid + "/Inner/Name")), c.uid);
        Assert.IsNull(Content.Find("Cycler_Sort"), "Crew Chiefs shows every card: nothing to sort");
        Assert.IsNull(Content.Find("Cycler_Rarity"), "nor filter");
    }

    [Test]
    public void CrewChiefs_NoCrewChiefCardsSaysSo()
    {
        var profile = TestProfile(0);
        AddCard(profile, CardType.Driver, 70, "Only Driver");
        Call("BuildOffline", profile, TestCatalogue());
        Call("SetGarageTab", GarageTab.CrewChiefs);

        Assert.AreEqual(0, Get<IReadOnlyList<string>>("GarageCardIds").Count);
        Assert.IsNull(Grid);
        Assert.IsTrue(Content.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "No crew chief cards yet."));
    }

    [Test]
    public void Cars_TabRowClearsTheCyclers()
    {
        // "Crew Chiefs" is the longest tab caption: the row must still end left of the Rarity cycler.
        var profile = TestProfile(0);
        Call("BuildOffline", profile, TestCatalogue());
        Call("SetGarageTab", GarageTab.Cars);

        var last = (RectTransform)Content.Find("GarageTab_Numbers");
        var rarity = (RectTransform)Content.Find("Cycler_Rarity");
        Assert.IsNotNull(last);
        Assert.IsNotNull(rarity);
        Assert.Less(last.anchoredPosition.x + last.sizeDelta.x, rarity.anchoredPosition.x);
    }

    [Test]
    public void CarDetail_UpgradeSpendsGearsAndCoins()
    {
        var cars = TestCatalogue();
        var car = new OwnedCar("tst01_2", 1, 25);   // class 1 needs 20 gears and 10,000 coins
        var profile = TestProfile(CarProgression.UpgradeCost(1), car);
        Call("BuildOffline", profile, cars);

        Call("SelectGarageCar", "tst01_2");
        Assert.IsTrue(Get<bool>("GarageDetailOpen"));
        Assert.IsNotNull(Detail, "the Card Detail layer is up");
        Assert.AreEqual("Class R", Get<string>("GarageDetailClass"));
        Assert.IsNotNull(DetailButton("g.upgrade"), "owned car shows an upgrade button");
        Assert.IsTrue(DetailButton("g.upgrade").interactable);
        Assert.AreEqual("Upgrade to Class D", Get<string>("GarageDetailUpgradeCaption"));
        Assert.IsNotNull(DetailButton("g.close"));

        Assert.AreEqual(UpgradeCheck.Ok, (UpgradeCheck)Call("UpgradeSelectedCar"));
        Assert.AreEqual(2, car.carClass);
        Assert.AreEqual(5, car.gears, "gears past the target carry over");
        Assert.AreEqual(0, profile.coins);

        // Rebuilt after the upgrade: still in the detail, now short of gears, button off and saying so.
        Assert.IsTrue(Get<bool>("GarageDetailOpen"));
        Assert.AreEqual("Class D", Get<string>("GarageDetailClass"));
        Assert.IsFalse(DetailButton("g.upgrade").interactable);
        Assert.AreEqual("Need 30 more gears", Get<string>("GarageDetailUpgradeCaption"));
        Assert.AreEqual("Need 30 more gears", CaptionOf(DetailButton("g.upgrade")));
        Assert.AreEqual(UpgradeCheck.NeedGears, (UpgradeCheck)Call("UpgradeSelectedCar"));
        Assert.AreEqual(2, car.carClass);

        car.gears = 100;   // gears there, coins not
        Assert.AreEqual(UpgradeCheck.NeedCoins, (UpgradeCheck)Call("UpgradeSelectedCar"));
        Assert.AreEqual(2, car.carClass);
        Assert.AreEqual(100, car.gears);

        Call("CloseCarDetail");
        Assert.IsFalse(Get<bool>("GarageDetailOpen"));
        Assert.IsNull(Detail, "closing takes the layer down");

        // A locked car: no upgrade, no change to the selected car.
        Call("SelectGarageCar", "tst02_19");
        Assert.AreEqual("tst02_19", Get<string>("GarageSelectedCar"));
        Assert.IsNull(DetailButton("g.upgrade"), "a locked car has nothing to upgrade");
        Assert.IsNull(DetailButton("g.race"), "nor can it be the race car");
        Assert.IsNotNull(DetailButton("g.close"));
        Assert.AreEqual("tst01_2", profile.selectedCar);
    }

    [Test]
    public void CardDetail_StatsShowNextClassGainsAndSetAsRaceCarSelects()
    {
        var cars = TestCatalogue();
        var first = new OwnedCar("tst01_1", 0, 0);
        var car = new OwnedCar("tst01_3", 2, 5);   // Class D, short of gears
        var profile = TestProfile(0, first, car);
        Call("BuildOffline", profile, cars);

        Call("SelectGarageCar", "tst01_3");
        Assert.AreEqual("tst01_1", profile.selectedCar, "opening a card doesn't make it the race car");
        Assert.AreEqual("Class D", Get<string>("GarageDetailClass"));

        // Four stat rows, each with ten pips: gold to the value, green for the next class's gain.
        var def = cars.Get("tst01_3");
        var stats = Get<int[]>("GarageDetailStats");
        var gains = Get<int[]>("GarageDetailNextGains");
        Assert.AreEqual(CarStats.Count, stats.Length);
        for (int s = 0; s < CarStats.Count; s++)
        {
            Assert.AreEqual(CarStats.Value(def, 2, s), stats[s], CarStats.Names[s]);
            Assert.AreEqual(CarStats.Value(def, 3, s) - CarStats.Value(def, 2, s), gains[s], CarStats.Names[s]);
            var row = DetailFind("Stat_" + CarStats.Names[s]);
            Assert.IsNotNull(row, CarStats.Names[s]);
            var pips = row.Find("Pips").Cast<Transform>().ToList();
            Assert.AreEqual(CarStats.MaxPips, pips.Count);
            Assert.AreEqual(gains[s] > 0, row.Find("Gain") != null, "the green +N only where the next class adds");
        }
        Assert.Greater(gains.Sum(), 0, "a class up adds to the stats");
        Assert.IsNotNull(DetailFind("Legend"), "the Next Class legend shows when something will go up");

        // Set As Race Car.
        Assert.IsTrue(DetailButton("g.race").interactable);
        Call("SetGarageRaceCar");
        Assert.AreEqual("tst01_3", profile.selectedCar);
        Assert.IsTrue(Get<bool>("GarageDetailOpen"), "still on the card");
        Assert.IsFalse(DetailButton("g.race").interactable, "already the race car");
        Assert.AreEqual("Your race car", CaptionOf(DetailButton("g.race")));

        // Top class: no green, no Next Class legend.
        car.carClass = CarProgression.MaxClass;
        Call("SelectGarageCar", "tst01_3");
        Assert.AreEqual("Class S", Get<string>("GarageDetailClass"));
        Assert.AreEqual(0, Get<int[]>("GarageDetailNextGains").Sum());
        Assert.IsNull(DetailFind("Legend"));
        Assert.AreEqual("Top class", Get<string>("GarageDetailUpgradeCaption"));
        Assert.IsFalse(DetailButton("g.upgrade").interactable);
    }

    [Test]
    public void RealCatalogue_EveryLiveryLoads()
    {
        Type.GetType("MasterDraftJsonInstaller, Assembly-CSharp", throwOnError: true)
            .GetMethod("Install", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        var text = Resources.Load<TextAsset>(CarCatalog.ResourcePath);
        Assert.IsNotNull(text, $"Resources/{CarCatalog.ResourcePath}.json");
        var cars = CarCatalog.FromJson(text.text);
        Assert.IsNotNull(cars);
        Assert.Greater(cars.Count, 0);

        var missing = cars.Cars.Where(c => Resources.Load<Sprite>(c.livery) == null).Select(c => c.id).ToList();
        Assert.IsEmpty(missing, "catalogue cars with no livery sprite: " + string.Join(", ", missing.Take(20)));
    }
}
