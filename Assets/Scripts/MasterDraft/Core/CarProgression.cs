using System;
using System.Collections.Generic;

namespace Draftmaster.MasterDraft
{
    // A car in the player's garage: which catalogue car (CarDef.id, e.g. "cup22_26"), its class and the gears
    // banked toward the next one. Public fields so JsonUtility can save it in the profile.
    [Serializable]
    public class OwnedCar
    {
        public string id;
        public int carClass;   // 0-6 (CarProgression.MinClass..MaxClass)
        public int gears;      // banked toward leaving carClass; carries over past the target

        public OwnedCar() { }

        public OwnedCar(string id, int carClass = 0, int gears = 0)
        {
            this.id = id;
            this.carClass = carClass;
            this.gears = gears;
        }

        public override string ToString() => $"{id} class {carClass} ({gears} gears)";
    }

    // Why a class upgrade can or can't happen. The garage screen shows the reason.
    public enum UpgradeCheck
    {
        Ok = 0,
        MaxClass = 1,     // already class 6
        NeedGears = 2,    // gears short of CarProgression.ClassGears(carClass)
        NeedCoins = 3,    // gears are there, coins short of CarProgression.UpgradeCost(carClass)
    }

    // What a car prize did to the garage.
    public enum CarAwardResult
    {
        NewCar = 0,       // the car wasn't owned: added at class 0 with the starting gears
        Duplicate = 1,    // already owned: became gears on the existing car
    }

    // Draftmaster 2's levelling, ported from GameData (classMax / unlockGears / upgradeCost), GarageUIFunctions
    // .classUp and PrizeCollection (git show 2217821d^:Assets/Scripts/PrizeCollection.cs):
    //
    //  - A car climbs classes 0-6. Gears fill the current class: ClassGears(c) of them (10, 20, 35, 50, 70, 100,
    //    150). Upgrading spends that many and keeps the rest, so gears past the target carry into the next class.
    //  - DM2 classed up for free the moment the gears were there; Master Draft also charges UpgradeCost(c) coins
    //    (DM2's upgradeCost table, which it defined but never charged). Indexed like ClassGears, by the class
    //    being left: 0 -> 1 costs nothing (DM2's class 0 was a locked car collecting parts; a new car arrives
    //    with exactly the 10 gears that unlock it), 1 -> 2 costs 10,000 ... 5 -> 6 costs 500,000. The class 6
    //    entry (1,000,000) is never charged — class 6 is the top.
    //  - Winning a car you don't own adds it at class 0 with StarterGears (PrizeCollection.StarterCar). Winning
    //    one you already own adds gears to it instead (AddPrize): duplicates become gears. A gears prize for a
    //    car you don't own still gives you the car, holding just those gears, as DM2's AddPrize did.
    //  - At class 6 gears still bank (DM2 kept adding them under "Max Class").
    //
    // Every number is in the tables below — the one place to tune levelling and the class performance step.
    public static class CarProgression
    {
        public const int MinClass = 0;
        public const int MaxClass = 6;

        // Gears a car is awarded with when it's won outright (PrizeCollection.StarterCar: "+10").
        public const int StarterGears = 10;

        // Gears a duplicate car prize turns into. DM2 gave a winning garage pull 3-25 gears; a whole car won a
        // second time is worth a starter car's gears.
        public const int DuplicateGears = StarterGears;

        // GameData.classMax: gears to fill class c (index = class).
        static readonly int[] ClassGearsTable = { 10, 20, 35, 50, 70, 100, 150 };

        // GameData.upgradeCost: coins to leave class c (index = class). Class 6's entry is never charged.
        static readonly int[] UpgradeCostTable = { 0, 10000, 50000, 100000, 250000, 500000, 1000000 };

        // Class performance step through PlayerVehicleController.cardPowerScale / cardGripScale (index = class).
        // Deliberately small: +1% power and +0.5% grip per class above 0.
        static readonly float[] PowerScaleTable = { 1.00f, 1.01f, 1.02f, 1.03f, 1.04f, 1.05f, 1.06f };
        static readonly float[] GripScaleTable = { 1.000f, 1.005f, 1.010f, 1.015f, 1.020f, 1.025f, 1.030f };

        // DM2's out-of-range answers (classMax default 999, upgradeCost default 99999999): never reachable.
        const int Unreachable = 999;
        const int UnaffordableCost = 99999999;

        public static bool IsValidClass(int carClass) => carClass >= MinClass && carClass <= MaxClass;

        public static int ClampClass(int carClass) => Math.Max(MinClass, Math.Min(MaxClass, carClass));

        // Gears needed to leave class c (GameData.classMax).
        public static int ClassGears(int carClass) => IsValidClass(carClass) ? ClassGearsTable[carClass] : Unreachable;

        // Coins to upgrade out of class c (GameData.upgradeCost).
        public static int UpgradeCost(int carClass) => IsValidClass(carClass) ? UpgradeCostTable[carClass] : UnaffordableCost;

        // Total gears a car has climbed through to arrive at class c from class 0 (GameData.unlockGears: 0, 10,
        // 30, 65, 115, 185, 285). DM2 used it as the gears a car of rarity c needed to unlock; here it's also how
        // a garage values a car (UnlockGears(class) + banked gears, as PlayFabManager's garage value did).
        public static int UnlockGears(int carClass)
        {
            if (!IsValidClass(carClass)) return Unreachable;
            int sum = 0;
            for (int c = MinClass; c < carClass; c++) sum += ClassGearsTable[c];
            return sum;
        }

        // Every gear the car has ever had: the classes it climbed plus what it's banked.
        public static int TotalGears(OwnedCar car) => car == null ? 0 : UnlockGears(ClampClass(car.carClass)) + car.gears;

        public static bool IsMaxClass(OwnedCar car) => car != null && car.carClass >= MaxClass;

        // Gears still to bank before the car can upgrade; 0 when it can (or is max class).
        public static int GearsToNextClass(OwnedCar car)
        {
            if (car == null || IsMaxClass(car)) return 0;
            return Math.Max(0, ClassGears(car.carClass) - car.gears);
        }

        // Garage progress bar fill, 0-1 (1 at max class).
        public static float ClassProgress(OwnedCar car)
        {
            if (car == null) return 0f;
            if (IsMaxClass(car)) return 1f;
            return Math.Min(1f, Math.Max(0, car.gears) / (float)ClassGears(car.carClass));
        }

        // --- Class performance -------------------------------------------------------------------------------

        public static float PowerScale(int carClass) => PowerScaleTable[ClampClass(carClass)];
        public static float GripScale(int carClass) => GripScaleTable[ClampClass(carClass)];

        // --- Gears -------------------------------------------------------------------------------------------

        // Bank gears on a car. Upgrading is a separate, paid step (TryUpgrade) — gears never class a car up on
        // their own.
        public static void AddGears(OwnedCar car, int amount)
        {
            if (car == null) throw new ArgumentNullException(nameof(car));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), amount, "gears can't be taken away by a prize");
            car.gears += amount;
        }

        public static OwnedCar Find(IList<OwnedCar> garage, string carId)
        {
            if (garage == null || string.IsNullOrEmpty(carId)) return null;
            for (int i = 0; i < garage.Count; i++)
                if (garage[i] != null && garage[i].id == carId) return garage[i];
            return null;
        }

        public static bool Owns(IList<OwnedCar> garage, string carId) => Find(garage, carId) != null;

        // A car won outright (a challenge's named-driver prize, a category draw): new car at class 0 with
        // StarterGears, or DuplicateGears onto the one already owned.
        public static CarAwardResult AwardCar(IList<OwnedCar> garage, string carId) =>
            AwardCar(garage, carId, out _);

        public static CarAwardResult AwardCar(IList<OwnedCar> garage, string carId, out OwnedCar car)
        {
            if (garage == null) throw new ArgumentNullException(nameof(garage));
            if (string.IsNullOrEmpty(carId)) throw new ArgumentException("car id required", nameof(carId));
            car = Find(garage, carId);
            if (car != null)
            {
                AddGears(car, DuplicateGears);
                return CarAwardResult.Duplicate;
            }
            car = new OwnedCar(carId, MinClass, StarterGears);
            garage.Add(car);
            return CarAwardResult.NewCar;
        }

        // Gears for a particular car (race rewards, DM2's garage pulls — PrizeCollection.AddPrize): onto the car
        // if owned, otherwise the car arrives at class 0 holding just those gears.
        public static CarAwardResult AwardGears(IList<OwnedCar> garage, string carId, int amount) =>
            AwardGears(garage, carId, amount, out _);

        public static CarAwardResult AwardGears(IList<OwnedCar> garage, string carId, int amount, out OwnedCar car)
        {
            if (garage == null) throw new ArgumentNullException(nameof(garage));
            if (string.IsNullOrEmpty(carId)) throw new ArgumentException("car id required", nameof(carId));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), amount, "gears can't be taken away by a prize");
            car = Find(garage, carId);
            if (car != null)
            {
                AddGears(car, amount);
                return CarAwardResult.Duplicate;
            }
            car = new OwnedCar(carId, MinClass, amount);
            garage.Add(car);
            return CarAwardResult.NewCar;
        }

        // --- Upgrading ---------------------------------------------------------------------------------------

        // Can this car go up a class with this many coins? Max class first, then gears, then coins — the order
        // the garage should explain them in.
        public static UpgradeCheck CanUpgrade(OwnedCar car, int coins)
        {
            if (car == null) throw new ArgumentNullException(nameof(car));
            if (IsMaxClass(car)) return UpgradeCheck.MaxClass;
            if (car.gears < ClassGears(car.carClass)) return UpgradeCheck.NeedGears;
            if (coins < UpgradeCost(car.carClass)) return UpgradeCheck.NeedCoins;
            return UpgradeCheck.Ok;
        }

        // Upgrade one class: spends ClassGears(class) gears (the rest carry over) and UpgradeCost(class) coins.
        // Leaves car and coins untouched and returns the reason when it can't.
        public static UpgradeCheck TryUpgrade(OwnedCar car, ref int coins)
        {
            var check = CanUpgrade(car, coins);
            if (check != UpgradeCheck.Ok) return check;
            int from = car.carClass;
            car.gears -= ClassGears(from);
            coins -= UpgradeCost(from);
            car.carClass = from + 1;
            return UpgradeCheck.Ok;
        }

        // Brings a car read from an old or hand-edited save back inside the rules: class 0-6, gears >= 0.
        public static void Sanitise(OwnedCar car)
        {
            if (car == null) return;
            car.carClass = ClampClass(car.carClass);
            if (car.gears < 0) car.gears = 0;
        }
    }
}
