using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Draftmaster.MasterDraft;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// The Master Draft Garage screen (MasterDraftUI, rework row 6) built in the editor against a test profile:
// no session, no backend, no scene — MasterDraftUI.BuildOffline. Checks it builds without errors, the grid
// shows the right cars (owned vs locked, filters, pages) and the upgrade spends gears and coins.
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

    List<Transform> Tiles() =>
        Content.Cast<Transform>().Where(t => t.name.StartsWith("Car_", StringComparison.Ordinal)).ToList();

    static int LockedTiles(IEnumerable<Transform> tiles) => tiles.Count(t => t.Find("Locked") != null);

    Button UpgradeButton() =>
        Content.Cast<Transform>().FirstOrDefault(t => t.name == "Button_g.upgrade")?.GetComponent<Button>();

    // 20 cars over two carsets, three teams, two makes, rarities 1-4. Livery names don't exist: the tile
    // falls back to the number.
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
    public void Garage_BuildsWithOwnedFirstAndLockedGreyed()
    {
        var cars = TestCatalogue();
        // tst01_5 and tst02_17 are owned; tst01_0 too.
        var profile = TestProfile(0, new OwnedCar("tst01_5", 2, 3), new OwnedCar("tst02_17", 0, 10), new OwnedCar("tst01_0", 6, 0));
        Call("BuildOffline", profile, cars);

        Assert.AreEqual(20, Get<int>("GarageFilteredCount"));
        Assert.AreEqual(3, Get<int>("GarageOwnedCount"));
        Assert.AreEqual(17, Get<int>("GarageLockedCount"));
        Assert.AreEqual(2, Get<int>("GaragePageCount"));
        Assert.AreEqual(15, Get<int>("GarageTilesShown"));

        var tiles = Tiles();
        Assert.AreEqual(15, tiles.Count, "one tile per car on the page");
        Assert.AreEqual(12, LockedTiles(tiles), "15 tiles, the 3 owned first, the rest locked");
        CollectionAssert.AreEquivalent(new[] { "Car_tst01_0", "Car_tst01_5", "Car_tst02_17" }, tiles.Take(3).Select(t => t.name));

        // Owned tiles carry six class cells, the class's worth filled.
        var classRow = tiles.First(t => t.name == "Car_tst01_5").Find("Class");
        Assert.IsNotNull(classRow, "owned tile has class stars");
        Assert.AreEqual(CarProgression.MaxClass, classRow.childCount);

        Assert.AreEqual("tst01_5", Get<string>("GarageSelectedCar"), "panel opens on the profile's selected car");

        Call("SetGaragePage", 1);
        Assert.AreEqual(5, Get<int>("GarageTilesShown"));
        Assert.AreEqual(5, Tiles().Count);
        Assert.AreEqual(5, LockedTiles(Tiles()));

    }

    [Test]
    public void Garage_FiltersByCarsetRarityTeamAndMake()
    {
        var cars = TestCatalogue();
        var profile = TestProfile(0, new OwnedCar("tst01_3", 0, 10));
        Call("BuildOffline", profile, cars);

        Call("SetGarageFilter", "tst02", 0, null, null);
        Assert.AreEqual(8, Get<int>("GarageFilteredCount"));
        Assert.AreEqual(0, Get<int>("GarageOwnedCount"));
        Assert.AreEqual(8, Tiles().Count);
        Assert.IsTrue(Tiles().All(t => t.name.StartsWith("Car_tst02_", StringComparison.Ordinal)));

        Call("SetGarageFilter", null, 4, null, null);   // i % 4 == 3: 3, 7, 11, 15, 19
        Assert.AreEqual(5, Get<int>("GarageFilteredCount"));
        Assert.AreEqual(1, Get<int>("GarageOwnedCount"));

        Call("SetGarageFilter", null, 0, "BBB", null);  // i % 3 == 1: 1, 4, 7, 10, 13, 16, 19
        Assert.AreEqual(7, Get<int>("GarageFilteredCount"));

        Call("SetGarageFilter", null, 0, null, "FRD");  // odd numbers
        Assert.AreEqual(10, Get<int>("GarageFilteredCount"));
        Assert.AreEqual(1, Get<int>("GarageOwnedCount"));

        Call("SetGarageFilter", "tst01", 0, "AAA", "CHV");   // i < 12, i % 3 == 0, even: 0, 6
        Assert.AreEqual(2, Get<int>("GarageFilteredCount"));
        Assert.AreEqual(2, Tiles().Count);

        Call("SetGarageFilter", "tst02", 1, "AAA", "FRD");   // nothing
        Assert.AreEqual(0, Get<int>("GarageFilteredCount"));
        Assert.AreEqual(0, Tiles().Count);
        Assert.AreEqual(1, Get<int>("GaragePageCount"));

        Call("SetGarageFilter", null, 0, null, null);
        Assert.AreEqual(20, Get<int>("GarageFilteredCount"));

    }

    [Test]
    public void Garage_UpgradeSpendsGearsAndCoins()
    {
        var cars = TestCatalogue();
        var car = new OwnedCar("tst01_2", 1, 25);   // class 1 needs 20 gears and 10,000 coins
        var profile = TestProfile(CarProgression.UpgradeCost(1), car);
        Call("BuildOffline", profile, cars);

        Assert.IsNotNull(UpgradeButton(), "owned car shows an upgrade button");
        Assert.IsTrue(UpgradeButton().interactable);

        Assert.AreEqual(UpgradeCheck.Ok, (UpgradeCheck)Call("UpgradeSelectedCar"));
        Assert.AreEqual(2, car.carClass);
        Assert.AreEqual(5, car.gears, "gears past the target carry over");
        Assert.AreEqual(0, profile.coins);

        // Rebuilt after the upgrade: now short of gears, button off.
        Assert.IsFalse(UpgradeButton().interactable);
        Assert.AreEqual(UpgradeCheck.NeedGears, (UpgradeCheck)Call("UpgradeSelectedCar"));
        Assert.AreEqual(2, car.carClass);

        car.gears = 100;   // gears there, coins not
        Assert.AreEqual(UpgradeCheck.NeedCoins, (UpgradeCheck)Call("UpgradeSelectedCar"));
        Assert.AreEqual(2, car.carClass);
        Assert.AreEqual(100, car.gears);

        // A locked car: no button, no change to the selected car.
        Call("SelectGarageCar", "tst02_19");
        Assert.AreEqual("tst02_19", Get<string>("GarageSelectedCar"));
        Assert.IsNull(UpgradeButton(), "a locked car has nothing to upgrade");
        Assert.AreEqual("tst01_2", profile.selectedCar);

    }

    [Test]
    public void Garage_RealCatalogue_EveryLiveryLoadsAndEveryPageBuilds()
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
        int pages = Get<int>("GaragePageCount");
        Assert.AreEqual((cars.Count + 14) / 15, pages);

        int seen = 0;
        for (int page = 0; page < pages; page++)
        {
            Call("SetGaragePage", page);
            var tiles = Tiles();
            seen += tiles.Count;
            foreach (var t in tiles)
                Assert.IsNotNull(t.Find("Livery").GetComponent<Image>().sprite, t.name + " has no livery sprite");
        }
        Assert.AreEqual(cars.Count, seen, "every car appears on exactly one page");

        // One carset.
        var set = cars.Carsets[0];
        Call("SetGarageFilter", set.id, 0, null, null);
        Assert.AreEqual(cars.InCarset(set.id).Count, Get<int>("GarageFilteredCount"));

    }
}
