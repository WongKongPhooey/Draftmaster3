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
// MasterDraftUI.BuildOffline. Checks it builds without errors, the grid shows the right cards (owned first with
// class letters and gears, "?" cards for the rest, filters, scrolling) and the car detail's upgrade spends gears
// and coins. The ordering and gears rules themselves are MasterDraftCollectionTests (Core, headless).
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

    // Every card built (the rows on screen, the peeking one and the drag buffer below), in grid order.
    List<Transform> Cards() => Grid == null
        ? new List<Transform>()
        : Grid.Cast<Transform>().Where(t => t.name.StartsWith("Card_", StringComparison.Ordinal)).ToList();

    Transform Card(string id) => Grid == null ? null : Grid.Find("Card_" + id);

    static bool IsLocked(Transform card) => card.Find("Locked") != null;

    static string TextOf(Transform t) => t == null ? null : t.GetComponent<TMP_Text>()?.text;

    Transform Detail => Content.Find("CarDetail");

    Button DetailButton(string key)
    {
        var detail = Detail;
        var button = detail == null ? null : detail.Find("Button_" + key);
        return button == null ? null : button.GetComponent<Button>();
    }

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

    [Test]
    public void Collection_OwnedFirstWithClassLettersAndQuestionMarkCards()
    {
        var cars = TestCatalogue();
        // 5 = Rare class 2 (D), 17 = Rare class 0 holding the 10 gears that unlock it, 0 = Common class 6 (S).
        var profile = TestProfile(0, new OwnedCar("tst01_5", 2, 3), new OwnedCar("tst02_17", 0, 10), new OwnedCar("tst01_0", 6, 0));
        Call("BuildOffline", profile, cars);

        Assert.AreEqual(20, Get<int>("GarageFilteredCount"));
        Assert.AreEqual(3, Get<int>("GarageOwnedCount"));
        Assert.AreEqual(17, Get<int>("GarageLockedCount"));
        Assert.AreEqual(3, Get<int>("GarageCollectedCount"));
        Assert.AreEqual(20, Get<int>("GarageCatalogueCount"));
        Assert.AreEqual(4, Get<int>("GarageRowCount"));
        Assert.AreEqual(15, Get<int>("GarageCardsShown"), "two whole rows and the peeking third");

        var texts = _go.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text).ToList();
        CollectionAssert.Contains(texts, "Card Collection");
        CollectionAssert.Contains(texts, "3 / 20 collected");

        var cards = Cards();
        Assert.AreEqual(20, cards.Count, "rows 0-2 and the row below for a drag");
        CollectionAssert.AreEqual(new[] { "Card_tst01_5", "Card_tst02_17", "Card_tst01_0" }, cards.Take(3).Select(t => t.name),
                                  "owned first, by rarity then class");
        Assert.IsTrue(cards.Skip(3).All(IsLocked), "the rest are cards still to collect");
        Assert.IsFalse(cards.Take(3).Any(IsLocked));

        Assert.AreEqual("Class D", TextOf(Card("tst01_5").Find("Body/Class/Text")));
        Assert.AreEqual("Class R", TextOf(Card("tst02_17").Find("Body/Class/Text")));
        Assert.AreEqual("Class S", TextOf(Card("tst01_0").Find("Body/Class/Text")));
        Assert.AreEqual("3/35 Gears", TextOf(Card("tst01_5").Find("Body/Gears")));
        Assert.AreEqual("10/10 Ready", TextOf(Card("tst02_17").Find("Body/Gears")));
        Assert.AreEqual("Max Class", TextOf(Card("tst01_0").Find("Body/Gears")));
        Assert.IsNotNull(Card("tst02_17").Find("Up"), "class 0 to class 1 is free: ready and affordable");
        Assert.IsNull(Card("tst01_5").Find("Up"));
        Assert.IsNull(Card("tst01_0").Find("Up"));

        // Rarity tabs and the two cyclers along the top.
        foreach (var r in new[] { "All", "Common", "Rare", "Epic", "Legend" })
            Assert.IsNotNull(Content.Find("Rarity_" + r), r + " tab");
        Assert.IsNotNull(Content.Find("Cycler_Sort"));
        Assert.IsNotNull(Content.Find("Cycler_Set"));
    }

    [Test]
    public void Collection_FiltersSortAndScroll()
    {
        var cars = TestCatalogue();
        var profile = TestProfile(0, new OwnedCar("tst01_3", 0, 10));
        Call("BuildOffline", profile, cars);

        Call("SetCollectionFilter", 4, null);   // i % 4 == 3: 3, 7, 11, 15, 19
        Assert.AreEqual(5, Get<int>("GarageFilteredCount"));
        Assert.AreEqual(1, Get<int>("GarageOwnedCount"));
        Assert.AreEqual(5, Cards().Count);
        Assert.AreEqual(1, Get<int>("GarageCollectedCount"), "the header counts the whole catalogue");
        Assert.AreEqual(20, Get<int>("GarageCatalogueCount"));

        Call("SetCollectionFilter", 0, "tst02");
        Assert.AreEqual(8, Get<int>("GarageFilteredCount"));
        Assert.AreEqual(0, Get<int>("GarageOwnedCount"));
        Assert.IsTrue(Cards().All(t => t.name.StartsWith("Card_tst02_", StringComparison.Ordinal)));

        Call("SetCollectionFilter", 1, "tst02");   // 12, 16
        Assert.AreEqual(2, Get<int>("GarageFilteredCount"));
        Call("SetCollectionFilter", 2, "tst01");   // 1, 5, 9
        Assert.AreEqual(3, Get<int>("GarageFilteredCount"));
        Call("SetCollectionFilter", 4, "tst02");   // 15, 19
        Assert.AreEqual(2, Get<int>("GarageFilteredCount"));

        Call("SetCollectionFilter", 0, null);
        Assert.AreEqual(20, Get<int>("GarageFilteredCount"));

        Call("SetCollectionSort", CollectionSort.Number);
        var list = Get<IReadOnlyList<CarDef>>("GarageList");
        Assert.AreEqual("tst01_3", list[0].id, "owned first");
        Assert.AreEqual("tst01_0", list[1].id, "then catalogue order");

        // 4 rows, 2 on screen: scrolls 0-2, clamped.
        Call("ScrollCollection", 2);
        Assert.AreEqual(2, Get<int>("GarageScroll"));
        Assert.AreEqual(10, Get<int>("GarageCardsShown"), "the last two rows, nothing below to peek");
        Call("ScrollCollection", 9);
        Assert.AreEqual(2, Get<int>("GarageScroll"));
        Call("SetCollectionFilter", 4, null);
        Assert.AreEqual(0, Get<int>("GarageScroll"), "a filter change goes back to the top");
    }

    [Test]
    public void Collection_EmptyFilterShowsNoCards()
    {
        var cars = TestCatalogue();
        Call("BuildOffline", TestProfile(0), cars);
        Call("SetCollectionFilter", 1, "nosuchset");
        Assert.AreEqual(0, Get<int>("GarageFilteredCount"));
        Assert.AreEqual(0, Cards().Count);
        Assert.AreEqual(0, Get<int>("GarageCardsShown"));
    }

    [Test]
    public void CarDetail_UpgradeSpendsGearsAndCoins()
    {
        var cars = TestCatalogue();
        var car = new OwnedCar("tst01_2", 1, 25);   // class 1 needs 20 gears and 10,000 coins
        var profile = TestProfile(CarProgression.UpgradeCost(1), car);
        Call("BuildOffline", profile, cars);
        Assert.IsNotNull(Card("tst01_2").Find("Up"), "ready and affordable: the UP tag");

        Call("SelectGarageCar", "tst01_2");
        Assert.IsTrue(Get<bool>("GarageDetailOpen"));
        Assert.IsNotNull(DetailButton("g.upgrade"), "owned car shows an upgrade button");
        Assert.IsTrue(DetailButton("g.upgrade").interactable);
        Assert.IsNotNull(DetailButton("g.close"));

        Assert.AreEqual(UpgradeCheck.Ok, (UpgradeCheck)Call("UpgradeSelectedCar"));
        Assert.AreEqual(2, car.carClass);
        Assert.AreEqual(5, car.gears, "gears past the target carry over");
        Assert.AreEqual(0, profile.coins);

        // Rebuilt after the upgrade: still in the detail, now short of gears, button off.
        Assert.IsTrue(Get<bool>("GarageDetailOpen"));
        Assert.IsFalse(DetailButton("g.upgrade").interactable);
        Assert.AreEqual(UpgradeCheck.NeedGears, (UpgradeCheck)Call("UpgradeSelectedCar"));
        Assert.AreEqual(2, car.carClass);

        car.gears = 100;   // gears there, coins not
        Assert.AreEqual(UpgradeCheck.NeedCoins, (UpgradeCheck)Call("UpgradeSelectedCar"));
        Assert.AreEqual(2, car.carClass);
        Assert.AreEqual(100, car.gears);

        Call("CloseCarDetail");
        Assert.IsFalse(Get<bool>("GarageDetailOpen"));
        Assert.AreEqual("Class D", TextOf(Card("tst01_2").Find("Body/Class/Text")));
        Assert.AreEqual("100/35 Ready", TextOf(Card("tst01_2").Find("Body/Gears")));
        Assert.IsNull(Card("tst01_2").Find("Up"), "ready but the coins aren't there");

        // A locked car: no upgrade, no change to the selected car.
        Call("SelectGarageCar", "tst02_19");
        Assert.AreEqual("tst02_19", Get<string>("GarageSelectedCar"));
        Assert.IsNull(DetailButton("g.upgrade"), "a locked car has nothing to upgrade");
        Assert.IsNotNull(DetailButton("g.close"));
        Assert.AreEqual("tst01_2", profile.selectedCar);
    }

    [Test]
    public void Collection_RealCatalogue_EveryLiveryLoadsAndEveryRowBuilds()
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

        var profile = MasterDraftProfile.New("garage-test", "Tester", cars);
        Assert.AreEqual(1, profile.garage.Count);
        Call("BuildOffline", profile, null);   // null: the screen loads the catalogue itself

        Assert.AreEqual(cars.Count, Get<int>("GarageFilteredCount"));
        Assert.AreEqual(1, Get<int>("GarageOwnedCount"));
        Assert.AreEqual(cars.Count - 1, Get<int>("GarageLockedCount"));
        int rows = Get<int>("GarageRowCount");
        Assert.AreEqual((cars.Count + 4) / 5, rows);

        var owned = Card(profile.garage[0].id);
        Assert.IsNotNull(owned, "the starter car is the first card");
        Assert.IsNotNull(owned.Find("Body/Slot/Livery").GetComponent<Image>().sprite, "starter car's livery");

        var seen = new HashSet<string>();
        for (int scroll = 0; scroll <= rows - 2; scroll++)
        {
            Call("ScrollCollection", scroll);
            Assert.AreEqual(scroll, Get<int>("GarageScroll"));
            foreach (var t in Cards()) seen.Add(t.name);
        }
        Assert.AreEqual(cars.Count, seen.Count, "scrolling top to bottom shows every car");

        var set = cars.Carsets[0];
        Call("SetCollectionFilter", 0, set.id);
        Assert.AreEqual(cars.InCarset(set.id).Count, Get<int>("GarageFilteredCount"));
    }
}
