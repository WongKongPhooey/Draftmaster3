using System.Collections.Generic;
using System.Linq;
using Draftmaster.MasterDraft;
using NUnit.Framework;

// The Card Collection screen's rules (Core CardCollection): rarity names, class letters in place of the design's
// levels, each card's gears line, which cars a filter lists and in what order, and the grid's row scrolling.
// Engine-free, so it also runs under `dotnet test Tools/HeadlessTests`.
public class MasterDraftCollectionTests
{
    // 20 cars over two carsets, rarities 1-4 by number (1 + i % 4).
    static CarCatalog Catalogue()
    {
        var file = new CarCatalogFile { version = 1 };
        file.carsets.Add(new CarsetDef { id = "tst01", name = "Test '01", cars = 12 });
        file.carsets.Add(new CarsetDef { id = "tst02", name = "Test '02", cars = 8 });
        for (int i = 0; i < 20; i++)
        {
            string set = i < 12 ? "tst01" : "tst02";
            file.cars.Add(new CarDef
            {
                id = CarCatalog.IdFor(set, i), carset = set, number = i, driver = "Driver" + i, team = "AAA",
                manufacturer = "CHV", rarity = 1 + i % 4, type = DriverTypes.Rookie, livery = "none",
            });
        }
        return new CarCatalog(file);
    }

    static List<string> Ids(IEnumerable<CarDef> cars) => cars.Select(c => c.id).ToList();

    [Test]
    public void RarityNames_AreTheDesignsFourInStarOrder()
    {
        Assert.AreEqual("All", CardCollection.RarityName(0));
        Assert.AreEqual("Common", CardCollection.RarityName(1));
        Assert.AreEqual("Rare", CardCollection.RarityName(2));
        Assert.AreEqual("Epic", CardCollection.RarityName(3));
        Assert.AreEqual("Legend", CardCollection.RarityName(4));
        Assert.AreEqual("Legend", CardCollection.RarityName(9), "clamped");
    }

    [Test]
    public void ClassLetters_RookieThenDToS()
    {
        // DM2's classAbbr, as the series' entry rules read it: classes 0 and 1 are both Rookie.
        string[] letters = { "R", "R", "D", "C", "B", "A", "S" };
        for (int c = CarProgression.MinClass; c <= CarProgression.MaxClass; c++)
        {
            Assert.AreEqual(letters[c], CardCollection.ClassLetter(c), $"class {c}");
            Assert.AreEqual(Restrictions.ClassLetter(c), CardCollection.ClassLetter(c), "same letter as the entry rules");
        }
        Assert.AreEqual("S", CardCollection.ClassLetter(12), "clamped to the top class");
    }

    [Test]
    public void Progress_GearsReadyUpgradeAndMax()
    {
        var short1 = CardCollection.Progress(new OwnedCar("x", 1, 9), 1_000_000);   // class 1 needs 20
        Assert.AreEqual("9/20 Gears", short1.caption);
        Assert.IsFalse(short1.ready);
        Assert.IsFalse(short1.canUpgrade);
        Assert.AreEqual(0.45f, short1.fill, 1e-4f);

        var noCoins = CardCollection.Progress(new OwnedCar("x", 1, 25), 0);
        Assert.AreEqual("25/20 Ready", noCoins.caption);
        Assert.IsTrue(noCoins.ready);
        Assert.IsFalse(noCoins.canUpgrade, "gears there, coins not: no UP tag");

        var up = CardCollection.Progress(new OwnedCar("x", 1, 20), CarProgression.UpgradeCost(1));
        Assert.IsTrue(up.ready);
        Assert.IsTrue(up.canUpgrade);

        var top = CardCollection.Progress(new OwnedCar("x", CarProgression.MaxClass, 40), 0);
        Assert.IsTrue(top.max);
        Assert.AreEqual("Max Class", top.caption);
        Assert.AreEqual(1f, top.fill);
        Assert.IsFalse(top.canUpgrade);

        Assert.AreEqual("", CardCollection.Progress(null, 0).caption);
    }

    [Test]
    public void Collected_CountsCatalogueCarsOnce()
    {
        var cars = Catalogue();
        var garage = new List<OwnedCar> { new("tst01_1"), new("tst01_1"), new("tst02_15"), new("gone_99") };
        Assert.AreEqual(2, CardCollection.Collected(cars, garage));
        Assert.AreEqual(0, CardCollection.Collected(cars, null));
        Assert.AreEqual(0, CardCollection.Collected(null, garage));
    }

    [Test]
    public void List_OwnedFirst_ThenRarityOrder()
    {
        var cars = Catalogue();
        // 3 = 4 stars class 1, 6 = 3 stars class 4, 2 = 3 stars class 4 with more gears, 0 = 1 star class 6.
        var garage = new List<OwnedCar>
        {
            new("tst01_0", 6, 0), new("tst01_6", 4, 1), new("tst01_2", 4, 30), new("tst01_3", 1, 0),
        };
        var list = CardCollection.List(cars, garage, 0, null, CollectionSort.Rarity);
        Assert.AreEqual(20, list.Count);
        CollectionAssert.AreEqual(new[] { "tst01_3", "tst01_2", "tst01_6", "tst01_0" }, Ids(list.Take(4)),
                                  "owned: rarity, then class, then fuller gears bar");
        var locked = list.Skip(4).ToList();
        Assert.IsTrue(locked.All(c => !CarProgression.Owns(garage, c.id)));
        for (int i = 1; i < locked.Count; i++)
            Assert.GreaterOrEqual(locked[i - 1].rarity, locked[i].rarity, "cars to collect: Legend first");
        Assert.AreEqual("tst01_7", locked[0].id, "same rarity: catalogue order");
    }

    [Test]
    public void List_ClassAndNumberSorts()
    {
        var cars = Catalogue();
        var garage = new List<OwnedCar> { new("tst01_3", 1, 0), new("tst01_0", 6, 0), new("tst02_13", 3, 0) };

        var byClass = CardCollection.List(cars, garage, 0, null, CollectionSort.Class);
        CollectionAssert.AreEqual(new[] { "tst01_0", "tst02_13", "tst01_3" }, Ids(byClass.Take(3)));

        var byNumber = CardCollection.List(cars, garage, 0, null, CollectionSort.Number);
        CollectionAssert.AreEqual(new[] { "tst01_0", "tst01_3", "tst02_13" }, Ids(byNumber.Take(3)), "owned still first");
        CollectionAssert.AreEqual(new[] { "tst01_1", "tst01_2", "tst01_4" }, Ids(byNumber.Skip(3).Take(3)));

        Assert.AreEqual(CollectionSort.Class, CardCollection.NextSort(CollectionSort.Rarity));
        Assert.AreEqual(CollectionSort.Number, CardCollection.NextSort(CollectionSort.Class));
        Assert.AreEqual(CollectionSort.Rarity, CardCollection.NextSort(CollectionSort.Number));
    }

    [Test]
    public void List_FiltersByRarityAndCarset()
    {
        var cars = Catalogue();
        var garage = new List<OwnedCar> { new("tst01_3") };
        var legends = CardCollection.List(cars, garage, 4, null, CollectionSort.Rarity);   // 3, 7, 11, 15, 19
        CollectionAssert.AreEqual(new[] { "tst01_3", "tst01_7", "tst01_11", "tst02_15", "tst02_19" }, Ids(legends));

        Assert.AreEqual(8, CardCollection.List(cars, garage, 0, "tst02", CollectionSort.Rarity).Count);
        Assert.AreEqual(8, CardCollection.List(cars, garage, 0, "TST02", CollectionSort.Rarity).Count, "carset ignores case");
        Assert.AreEqual(2, CardCollection.List(cars, garage, 4, "tst02", CollectionSort.Rarity).Count);
        Assert.AreEqual(0, CardCollection.List(null, garage, 0, null, CollectionSort.Rarity).Count);
    }

    [Test]
    public void Scroll_RowsClampAndScrollToShow()
    {
        Assert.AreEqual(0, CardCollection.Rows(0));
        Assert.AreEqual(1, CardCollection.Rows(5));
        Assert.AreEqual(4, CardCollection.Rows(16));

        // 16 cards = 4 rows, 2 on screen: scroll 0-2.
        Assert.AreEqual(2, CardCollection.ClampScroll(9, 16, 2));
        Assert.AreEqual(0, CardCollection.ClampScroll(-3, 16, 2));
        Assert.AreEqual(0, CardCollection.ClampScroll(1, 7, 2), "everything fits");

        Assert.AreEqual(0, CardCollection.ScrollToShow(6, 0, 16, 2), "row 1 already on screen");
        Assert.AreEqual(1, CardCollection.ScrollToShow(10, 0, 16, 2), "row 2: scroll one");
        Assert.AreEqual(2, CardCollection.ScrollToShow(15, 0, 16, 2), "last row");
        Assert.AreEqual(0, CardCollection.ScrollToShow(3, 2, 16, 2), "back up to row 0");
    }

    // ---- Card Detail stats (CarStats, placeholder numbers: these pin the shape, not the values)

    [Test]
    public void CarStats_ClassUpsOnlyAddAndStayInsideTheBar()
    {
        foreach (var car in Catalogue().Cars)
            for (int s = 0; s < CarStats.Count; s++)
            {
                int b = CarStats.Base(car, s);
                Assert.That(b, Is.InRange(1, CarStats.MaxPips), car.id);
                Assert.AreEqual(b, CarStats.Value(car, CarProgression.MinClass, s));
                for (int c = CarProgression.MinClass; c < CarProgression.MaxClass; c++)
                {
                    int now = CarStats.Value(car, c, s), next = CarStats.Value(car, c + 1, s);
                    Assert.GreaterOrEqual(next, now, $"{car.id} {CarStats.Names[s]} class {c}");
                    Assert.LessOrEqual(next, CarStats.MaxPips);
                }
            }
    }

    [Test]
    public void CarStats_EveryClassUpRaisesSomethingAndRarerCarsStartHigher()
    {
        var cars = Catalogue();
        var common = cars.Get("tst01_0");   // rarity 1
        var legend = cars.Get("tst01_3");   // rarity 4
        for (int c = CarProgression.MinClass; c < CarProgression.MaxClass; c++)
        {
            int gain = 0;
            for (int s = 0; s < CarStats.Count; s++) gain += CarStats.ClassGain(c, s);
            Assert.Greater(gain, 0, $"class {c} -> {c + 1}");
        }
        int commonTotal = 0, legendTotal = 0;
        for (int s = 0; s < CarStats.Count; s++)
        {
            commonTotal += CarStats.Base(common, s);
            legendTotal += CarStats.Base(legend, s);
        }
        Assert.Greater(legendTotal, commonTotal);
        Assert.AreEqual(CarStats.Base(common, 2), CarStats.Base(cars.Get("tst01_0"), 2), "stable per car");
    }

    [Test]
    public void CarStats_LinesShowTheNextClassGainOnlyForAnOwnedCarBelowTheTop()
    {
        var car = Catalogue().Get("tst01_1");
        var lines = CarStats.Lines(car, new OwnedCar(car.id, 2, 0));
        Assert.AreEqual(CarStats.Count, lines.Length);
        int gain = 0;
        for (int s = 0; s < lines.Length; s++)
        {
            Assert.AreEqual(CarStats.Names[s], lines[s].name);
            Assert.AreEqual(CarStats.Value(car, 2, s), lines[s].value);
            Assert.AreEqual(CarStats.Value(car, 3, s) - lines[s].value, lines[s].nextGain);
            gain += lines[s].nextGain;
        }
        Assert.Greater(gain, 0);

        foreach (var line in CarStats.Lines(car, new OwnedCar(car.id, CarProgression.MaxClass, 0)))
            Assert.AreEqual(0, line.nextGain, "top class: nothing to come");
        foreach (var line in CarStats.Lines(car, null))
            Assert.AreEqual(0, line.nextGain, "not owned: base stats, nothing to come");
    }

    [Test]
    public void CarStats_TraitFollowsTheDriverType()
    {
        foreach (var type in DriverTypes.All)
        {
            var car = new CarDef { id = "x_1", type = type, rarity = 1 };
            Assert.AreEqual(type, CarStats.TraitName(car));
            Assert.IsFalse(string.IsNullOrEmpty(CarStats.TraitText(car)), type);
        }
        Assert.AreEqual(DriverTypes.Rookie, CarStats.TraitName(new CarDef { id = "x_2", type = "Nonsense" }));
    }
}
