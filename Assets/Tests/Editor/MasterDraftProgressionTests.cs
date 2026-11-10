using System;
using System.Collections.Generic;
using Draftmaster.MasterDraft;
using NUnit.Framework;

// Master Draft's levelling rules (Core CarProgression): Draftmaster 2's classes and gears, duplicate prizes as
// gears, paid class upgrades and the class performance step. Engine-free, so it also runs under
// `dotnet test Tools/HeadlessTests`.
public class MasterDraftProgressionTests
{
    // --- DM2 numbers (GameData) ---------------------------------------------------------------------------------

    [Test]
    public void ClassGears_MatchesDm2ClassMax()
    {
        int[] dm2 = { 10, 20, 35, 50, 70, 100, 150 };
        for (int c = 0; c <= 6; c++) Assert.AreEqual(dm2[c], CarProgression.ClassGears(c), $"class {c}");
        Assert.AreEqual(999, CarProgression.ClassGears(7));
        Assert.AreEqual(999, CarProgression.ClassGears(-1));
    }

    [Test]
    public void UpgradeCost_MatchesDm2UpgradeCost()
    {
        int[] dm2 = { 0, 10000, 50000, 100000, 250000, 500000, 1000000 };
        for (int c = 0; c <= 6; c++) Assert.AreEqual(dm2[c], CarProgression.UpgradeCost(c), $"class {c}");
        Assert.AreEqual(99999999, CarProgression.UpgradeCost(7));
    }

    [Test]
    public void UnlockGears_MatchesDm2AndIsTheSumOfTheClassesBelow()
    {
        int[] dm2 = { 0, 10, 30, 65, 115, 185, 285 };
        int sum = 0;
        for (int c = 0; c <= 6; c++)
        {
            Assert.AreEqual(dm2[c], CarProgression.UnlockGears(c), $"class {c}");
            Assert.AreEqual(sum, CarProgression.UnlockGears(c), $"class {c} cumulative");
            sum += CarProgression.ClassGears(c);
        }
        Assert.AreEqual(999, CarProgression.UnlockGears(7));
    }

    // --- Awards -------------------------------------------------------------------------------------------------

    [Test]
    public void AwardCar_NewCarArrivesAtClassZeroWithStarterGears()
    {
        var garage = new List<OwnedCar>();
        var result = CarProgression.AwardCar(garage, "cup22_26", out var car);

        Assert.AreEqual(CarAwardResult.NewCar, result);
        Assert.AreEqual(1, garage.Count);
        Assert.AreSame(car, garage[0]);
        Assert.AreEqual("cup22_26", car.id);
        Assert.AreEqual(0, car.carClass);
        Assert.AreEqual(10, car.gears);
        Assert.AreEqual(CarProgression.StarterGears, car.gears);
    }

    [Test]
    public void AwardCar_DuplicateBecomesGearsOnTheOwnedCar()
    {
        var owned = new OwnedCar("cup22_26", 3, 7);
        var garage = new List<OwnedCar> { owned };

        var result = CarProgression.AwardCar(garage, "cup22_26", out var car);

        Assert.AreEqual(CarAwardResult.Duplicate, result);
        Assert.AreEqual(1, garage.Count, "a duplicate never adds a second copy");
        Assert.AreSame(owned, car);
        Assert.AreEqual(3, owned.carClass, "gears alone never class a car up");
        Assert.AreEqual(7 + CarProgression.DuplicateGears, owned.gears);
    }

    [Test]
    public void AwardGears_OntoOwnedCarOrGivesTheCarHoldingThoseGears()
    {
        var garage = new List<OwnedCar> { new OwnedCar("cup20_48", 2, 5) };

        Assert.AreEqual(CarAwardResult.Duplicate, CarProgression.AwardGears(garage, "cup20_48", 12));
        Assert.AreEqual(17, CarProgression.Find(garage, "cup20_48").gears);

        Assert.AreEqual(CarAwardResult.NewCar, CarProgression.AwardGears(garage, "cup20_9", 3, out var fresh));
        Assert.AreEqual(2, garage.Count);
        Assert.AreEqual(0, fresh.carClass);
        Assert.AreEqual(3, fresh.gears);
    }

    [Test]
    public void AddGears_RejectsNegativeAndAllowsZero()
    {
        var car = new OwnedCar("x", 1, 4);
        CarProgression.AddGears(car, 0);
        Assert.AreEqual(4, car.gears);
        Assert.Throws<ArgumentOutOfRangeException>(() => CarProgression.AddGears(car, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CarProgression.AwardGears(new List<OwnedCar>(), "x", -5));
        Assert.Throws<ArgumentException>(() => CarProgression.AwardCar(new List<OwnedCar>(), ""));
    }

    [Test]
    public void GearsKeepBankingAtMaxClass()
    {
        var garage = new List<OwnedCar> { new OwnedCar("x", 6, 40) };
        CarProgression.AwardCar(garage, "x");
        Assert.AreEqual(6, garage[0].carClass);
        Assert.AreEqual(40 + CarProgression.DuplicateGears, garage[0].gears);
    }

    [Test]
    public void FindAndOwns()
    {
        var garage = new List<OwnedCar> { new OwnedCar("a"), null, new OwnedCar("b") };
        Assert.AreEqual("b", CarProgression.Find(garage, "b").id);
        Assert.IsNull(CarProgression.Find(garage, "c"));
        Assert.IsNull(CarProgression.Find(null, "a"));
        Assert.IsTrue(CarProgression.Owns(garage, "a"));
        Assert.IsFalse(CarProgression.Owns(garage, null));
    }

    // --- Upgrading ----------------------------------------------------------------------------------------------

    [Test]
    public void StarterCar_UpgradesToClassOneForFree()
    {
        var garage = new List<OwnedCar>();
        CarProgression.AwardCar(garage, "cup22_26", out var car);
        int coins = 0;

        Assert.AreEqual(UpgradeCheck.Ok, CarProgression.CanUpgrade(car, coins));
        Assert.AreEqual(UpgradeCheck.Ok, CarProgression.TryUpgrade(car, ref coins));
        Assert.AreEqual(1, car.carClass);
        Assert.AreEqual(0, car.gears);
        Assert.AreEqual(0, coins);
    }

    [Test]
    public void TryUpgrade_SpendsGearsAndCoinsAndCarriesSpareGears()
    {
        var car = new OwnedCar("x", 1, 27);   // class 1 needs 20 gears, costs 10,000
        int coins = 12500;

        Assert.AreEqual(UpgradeCheck.Ok, CarProgression.TryUpgrade(car, ref coins));
        Assert.AreEqual(2, car.carClass);
        Assert.AreEqual(7, car.gears, "gears past the target carry into the next class");
        Assert.AreEqual(2500, coins);
    }

    [Test]
    public void CanUpgrade_ExplainsWhyNot_InOrder()
    {
        int rich = int.MaxValue;
        Assert.AreEqual(UpgradeCheck.MaxClass, CarProgression.CanUpgrade(new OwnedCar("x", 6, 9999), rich));
        Assert.AreEqual(UpgradeCheck.NeedGears, CarProgression.CanUpgrade(new OwnedCar("x", 2, 34), rich));
        Assert.AreEqual(UpgradeCheck.NeedGears, CarProgression.CanUpgrade(new OwnedCar("x", 2, 34), 0),
            "gears are reported before coins");
        Assert.AreEqual(UpgradeCheck.NeedCoins, CarProgression.CanUpgrade(new OwnedCar("x", 2, 35), 49999));
        Assert.AreEqual(UpgradeCheck.Ok, CarProgression.CanUpgrade(new OwnedCar("x", 2, 35), 50000));
    }

    [Test]
    public void TryUpgrade_FailureLeavesCarAndCoinsUntouched()
    {
        var car = new OwnedCar("x", 3, 50);
        int coins = 99999;   // class 3 costs 100,000

        Assert.AreEqual(UpgradeCheck.NeedCoins, CarProgression.TryUpgrade(car, ref coins));
        Assert.AreEqual(3, car.carClass);
        Assert.AreEqual(50, car.gears);
        Assert.AreEqual(99999, coins);
    }

    [Test]
    public void ClimbingFromZeroToMax_CostsEveryClassBelowSix()
    {
        var car = new OwnedCar("x", 0, CarProgression.UnlockGears(6));
        int coins = 10000 + 50000 + 100000 + 250000 + 500000;

        while (CarProgression.TryUpgrade(car, ref coins) == UpgradeCheck.Ok) { }

        Assert.AreEqual(6, car.carClass);
        Assert.IsTrue(CarProgression.IsMaxClass(car));
        Assert.AreEqual(0, car.gears);
        Assert.AreEqual(0, coins, "class 6's 1,000,000 is never charged");
        Assert.AreEqual(UpgradeCheck.MaxClass, CarProgression.TryUpgrade(car, ref coins));
    }

    [Test]
    public void ProgressHelpers()
    {
        var car = new OwnedCar("x", 2, 14);   // class 2 needs 35
        Assert.AreEqual(21, CarProgression.GearsToNextClass(car));
        Assert.AreEqual(0.4f, CarProgression.ClassProgress(car), 1e-5f);
        Assert.AreEqual(30 + 14, CarProgression.TotalGears(car));

        car.gears = 50;
        Assert.AreEqual(0, CarProgression.GearsToNextClass(car));
        Assert.AreEqual(1f, CarProgression.ClassProgress(car));

        var max = new OwnedCar("y", 6, 3);
        Assert.AreEqual(0, CarProgression.GearsToNextClass(max));
        Assert.AreEqual(1f, CarProgression.ClassProgress(max));
        Assert.AreEqual(285 + 3, CarProgression.TotalGears(max));
    }

    [Test]
    public void Sanitise_ClampsClassAndGears()
    {
        var car = new OwnedCar("x", 9, -4);
        CarProgression.Sanitise(car);
        Assert.AreEqual(6, car.carClass);
        Assert.AreEqual(0, car.gears);

        car.carClass = -2;
        CarProgression.Sanitise(car);
        Assert.AreEqual(0, car.carClass);
    }

    // --- Class performance --------------------------------------------------------------------------------------

    [Test]
    public void ClassPerformance_StartsAtOnePercentPowerAndHalfPercentGripPerClass()
    {
        for (int c = 0; c <= 6; c++)
        {
            Assert.AreEqual(1f + 0.01f * c, CarProgression.PowerScale(c), 1e-5f, $"power class {c}");
            Assert.AreEqual(1f + 0.005f * c, CarProgression.GripScale(c), 1e-5f, $"grip class {c}");
        }
    }

    [Test]
    public void ClassPerformance_IsMonotonicAndClampedOutOfRange()
    {
        for (int c = 1; c <= 6; c++)
        {
            Assert.Greater(CarProgression.PowerScale(c), CarProgression.PowerScale(c - 1));
            Assert.Greater(CarProgression.GripScale(c), CarProgression.GripScale(c - 1));
        }
        Assert.AreEqual(1f, CarProgression.PowerScale(-3));
        Assert.AreEqual(CarProgression.PowerScale(6), CarProgression.PowerScale(10));
        Assert.AreEqual(CarProgression.GripScale(6), CarProgression.GripScale(10));
    }
}
