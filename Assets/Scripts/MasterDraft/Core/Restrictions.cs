using System;
using System.Collections.Generic;

namespace Draftmaster.MasterDraft
{
    // Entry requirements for a series or a challenge (Draftmaster 2's offlineMin* fields): a minimum player level,
    // a minimum car class, a minimum star rating and at most one "rule" naming what sort of car may enter.
    // Exported into Series.json (and Challenges.json) by Tools/masterdraft_export.py. Public fields for JsonUtility.
    [Serializable]
    public class Restriction
    {
        public int minLevel;     // player level, DM2 offlineMinLevel
        public int minClass;     // car class 0-6, DM2 offlineMinClass
        public int minRarity;    // stars >= this; 0 = any. Every DM2 series has 0 (its Rarity rule is exact)
        public string rule;      // "" or one of RestrictionRule.All
        public string value;     // what the rule asks for: team code, manufacturer code, car number, driver, type, stars

        public Restriction() { }

        public Restriction(int minLevel, int minClass, string rule = "", string value = "", int minRarity = 0)
        {
            this.minLevel = minLevel;
            this.minClass = minClass;
            this.rule = rule ?? "";
            this.value = value ?? "";
            this.minRarity = minRarity;
        }

        public bool HasRule => !string.IsNullOrEmpty(rule);

        public override string ToString() =>
            $"L{minLevel}+ class {minClass}+" + (minRarity > 0 ? $" {minRarity}*+" : "") + (HasRule ? $" {rule}={value}" : "");
    }

    // DM2's offlineMinType values (plus "Driver", which DM2's garage checked and its challenges use).
    public static class RestrictionRule
    {
        public const string Team = "Team";                  // value = team code, "HEN"
        public const string Manufacturer = "Manufacturer";  // value = manufacturer code, "CHV"
        public const string Car = "Car";                    // value = car number in any carset ("48"), or a car id ("cup20_48")
        public const string Driver = "Driver";              // value = DM2 driver name, "Johnson"
        public const string Type = "Type";                  // value = driver type, "Closer"
        public const string Rarity = "Rarity";              // value = stars, exactly ("4"), as DM2's garage matched it

        public static readonly string[] All = { Team, Manufacturer, Car, Driver, Type, Rarity };

        public static bool IsValid(string rule) => string.IsNullOrEmpty(rule) || Array.IndexOf(All, rule) >= 0;
    }

    // Why a car can't enter. The order is the order Check reports them in (and the UI should list them in).
    public enum RestrictionFailure
    {
        PlayerLevel = 0,   // the player's level is below minLevel
        NotOwned = 1,      // the car isn't in the garage
        CarClass = 2,      // the owned car's class is below minClass
        MinRarity = 3,     // fewer stars than minRarity
        Team = 4,
        Manufacturer = 5,
        Car = 6,
        Driver = 7,
        DriverType = 8,
        Rarity = 9,        // the Rarity rule: not exactly that many stars
        UnknownRule = 10,  // the data names a rule this engine doesn't know: fails closed
        NoCar = 11,        // no car given (or not in the catalogue)
    }

    public struct RestrictionReason
    {
        public RestrictionFailure failure;
        public string message;

        public RestrictionReason(RestrictionFailure failure, string message)
        {
            this.failure = failure;
            this.message = message;
        }

        public override string ToString() => message;
    }

    // The answer: eligible, or not and every reason why.
    public class Eligibility
    {
        readonly List<RestrictionReason> reasons = new List<RestrictionReason>();

        public bool Eligible => reasons.Count == 0;
        public IReadOnlyList<RestrictionReason> Reasons => reasons;

        public bool Has(RestrictionFailure failure)
        {
            foreach (var r in reasons) if (r.failure == failure) return true;
            return false;
        }

        // One line per reason, for a tooltip or a locked tile.
        public string Summary => Eligible ? "Eligible" : string.Join("\n", reasons.ConvertAll(r => r.message));

        internal void Add(RestrictionFailure failure, string message) => reasons.Add(new RestrictionReason(failure, message));

        public override string ToString() => Summary;
    }

    // One restriction engine for series and challenges. Ported from DM2's SeriesUI (level lock on a tile),
    // GarageUI's car list (class and unlocked checks, meetsRestrictions) and SeriesUI.getEntryReqs (the list the
    // entry-requirements popup showed). Engine-free.
    public static class Restrictions
    {
        // Pass as ownedClass when the player doesn't own the car.
        public const int NotOwned = -1;

        // Can this car enter, raced at this class by a player at this level? ownedClass = the OwnedCar's class, or
        // NotOwned. Every failing requirement is reported, level first.
        public static Eligibility Check(Restriction restriction, CarDef car, int ownedClass, int playerLevel)
        {
            var result = new Eligibility();
            var r = restriction ?? new Restriction();
            if (playerLevel < r.minLevel)
                result.Add(RestrictionFailure.PlayerLevel, $"Reach level {r.minLevel} (you're level {playerLevel})");
            if (car == null)
            {
                result.Add(RestrictionFailure.NoCar, "Choose a car");
                return result;
            }
            if (ownedClass < 0)
                result.Add(RestrictionFailure.NotOwned, $"You don't own the #{car.number} {car.driver}");
            else if (ownedClass < r.minClass)
                result.Add(RestrictionFailure.CarClass,
                    $"Class {ClassLetter(r.minClass)} or better (this car is class {ClassLetter(ownedClass)})");
            AddCarFailures(r, car, result);
            return result;
        }

        public static Eligibility Check(Restriction restriction, CarDef car, OwnedCar owned, int playerLevel) =>
            Check(restriction, car, owned == null ? NotOwned : CarProgression.ClampClass(owned.carClass), playerLevel);

        // Looks the car up in the garage by id.
        public static Eligibility CheckInGarage(Restriction restriction, CarDef car, IList<OwnedCar> garage, int playerLevel) =>
            Check(restriction, car, car == null ? null : CarProgression.Find(garage, car.id), playerLevel);

        // Just the player-level gate: DM2 covered a series tile when minLevel > level.
        public static bool LevelUnlocked(Restriction restriction, int playerLevel) =>
            restriction == null || playerLevel >= restriction.minLevel;

        // Does the car itself fit (stars and the rule), whoever owns it at whatever class? For listing the cars a
        // series takes, and for prize previews.
        public static bool CarFits(Restriction restriction, CarDef car)
        {
            if (car == null) return false;
            var result = new Eligibility();
            AddCarFailures(restriction ?? new Restriction(), car, result);
            return result.Eligible;
        }

        // The garage cars that can enter right now, in garage order.
        public static List<OwnedCar> EligibleGarage(Restriction restriction, CarCatalog catalog, IList<OwnedCar> garage, int playerLevel)
        {
            var list = new List<OwnedCar>();
            if (catalog == null || garage == null) return list;
            foreach (var owned in garage)
            {
                if (owned == null) continue;
                var car = catalog.Get(owned.id);
                if (car != null && Check(restriction, car, owned, playerLevel).Eligible) list.Add(owned);
            }
            return list;
        }

        // Every catalogue car the restriction's stars and rule allow.
        public static List<CarDef> FittingCars(Restriction restriction, CarCatalog catalog)
        {
            var list = new List<CarDef>();
            if (catalog == null) return list;
            foreach (var c in catalog.Cars) if (CarFits(restriction, c)) list.Add(c);
            return list;
        }

        // The entry-requirements list (DM2 SeriesUI.getEntryReqs): "Level: 5+", "Driver Class: D+", then the rule.
        public static List<string> Describe(Restriction restriction)
        {
            var r = restriction ?? new Restriction();
            var lines = new List<string>
            {
                $"Level: {r.minLevel}+",
                $"Driver Class: {ClassLetter(r.minClass)}+",
            };
            if (r.minRarity > 0) lines.Add($"Driver Rarity: {r.minRarity}+");
            switch (r.rule)
            {
                case RestrictionRule.Team: lines.Add($"Team: {r.value}"); break;
                case RestrictionRule.Manufacturer: lines.Add($"Manufacturer: {r.value}"); break;
                case RestrictionRule.Car: lines.Add(r.value != null && r.value.Contains("_") ? $"Exact Car: {r.value}" : $"Exact Car: #{r.value}"); break;
                case RestrictionRule.Driver: lines.Add($"Driver: {r.value}"); break;
                case RestrictionRule.Type: lines.Add($"Driver Type: {r.value}"); break;
                case RestrictionRule.Rarity: lines.Add($"Driver Rarity: {r.value}"); break;
            }
            return lines;
        }

        // DM2's class letters (SeriesData.classAbbr): 0 and 1 both read "R", then D, C, B, A, S.
        public static string ClassLetter(int carClass)
        {
            switch (carClass)
            {
                case 2: return "D";
                case 3: return "C";
                case 4: return "B";
                case 5: return "A";
                case 6: return "S";
                default: return "R";
            }
        }

        static void AddCarFailures(Restriction r, CarDef car, Eligibility result)
        {
            if (r.minRarity > 0 && car.rarity < r.minRarity)
                result.Add(RestrictionFailure.MinRarity, $"{r.minRarity}-star driver or better");
            if (!r.HasRule) return;
            string value = r.value ?? "";
            switch (r.rule)
            {
                case RestrictionRule.Team:
                    if (!Same(car.team, value)) result.Add(RestrictionFailure.Team, $"{value} cars only");
                    break;
                case RestrictionRule.Manufacturer:
                    if (!Same(car.manufacturer, value)) result.Add(RestrictionFailure.Manufacturer, $"{value} cars only");
                    break;
                case RestrictionRule.Car:
                    if (!IsCar(car, value))
                        result.Add(RestrictionFailure.Car, value.Contains("_") ? $"Only the {value}" : $"The #{value} only");
                    break;
                case RestrictionRule.Driver:
                    if (!Same(car.driver, value)) result.Add(RestrictionFailure.Driver, $"{value}'s cars only");
                    break;
                case RestrictionRule.Type:
                    if (!Same(car.type, value)) result.Add(RestrictionFailure.DriverType, $"{value} drivers only");
                    break;
                case RestrictionRule.Rarity:
                    if (!int.TryParse(value, out int stars) || car.rarity != stars)
                        result.Add(RestrictionFailure.Rarity, $"{value}-star drivers only");
                    break;
                default:
                    result.Add(RestrictionFailure.UnknownRule, $"Unknown entry rule \"{r.rule}\"");
                    break;
            }
        }

        // "48" = the #48 of any carset (DM2's offlineExactCar); "cup20_48" = that car exactly.
        static bool IsCar(CarDef car, string value)
        {
            if (value.Contains("_")) return string.Equals(car.id, value, StringComparison.OrdinalIgnoreCase);
            return int.TryParse(value, out int number) && car.number == number;
        }

        static bool Same(string a, string b) => string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);
    }
}
