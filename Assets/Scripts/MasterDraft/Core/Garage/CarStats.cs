using System;

namespace Draftmaster.MasterDraft
{
    // One stat row on the Card Detail screen: its name, the pips filled now and the pips the next class adds
    // (the design's green squares and "+1").
    public struct CarStatLine
    {
        public string name;
        public int value;       // 0-MaxPips at the car's class
        public int nextGain;    // pips the next class adds; 0 at the top class or for a car not owned
    }

    // PLACEHOLDER car stats for the Card Detail screen (Designs/Draftmaster3_UI_Unity, Layout/CardDetail_PC.json:
    // "Car Stats", four rows of ten pips). The design's fields and numbers are still to be settled, so none of this
    // feeds the race yet — the real class step is CarProgression.PowerScale / GripScale. What it does fix is the
    // shape: a base value per stat from the car, and each class up adding pips, which the screen draws in green
    // before the upgrade is bought.
    //
    // Everything to tune is in the tables below: the stat names, the base (rarity plus a fixed per-car spread) and
    // which stats each class step raises. Engine-free, so MasterDraftCollectionTests run it headless.
    public static class CarStats
    {
        public const int MaxPips = 10;

        public static readonly string[] Names = { "Top Speed", "Accel", "Grip", "Durability" };

        public static int Count => Names.Length;

        // Pips a class step adds, indexed by the class being left (0 -> 1 is row 0 ... 5 -> 6 is row 5), one column
        // per stat in Names order. Every stat gains 3 over the climb from class 0 to class 6.
        static readonly int[][] ClassGainTable =
        {
            new[] { 1, 0, 1, 0 },
            new[] { 0, 1, 0, 1 },
            new[] { 1, 0, 1, 0 },
            new[] { 0, 1, 0, 1 },
            new[] { 1, 0, 1, 0 },
            new[] { 0, 1, 0, 1 },
        };

        // Base pips: 1 + rarity (2-5), plus 0-2 picked from the car's id so cars of one rarity differ. Stable
        // across runs and platforms (FNV-1a, not string.GetHashCode, which .NET randomises per process).
        const int SpreadPips = 3;

        public static int Base(CarDef car, int stat)
        {
            if (car == null || stat < 0 || stat >= Count) return 0;
            int rarity = Math.Max(CarCatalog.MinRarity, Math.Min(CarCatalog.MaxRarity, car.rarity));
            int spread = (int)(Fnv(car.id ?? "", stat) % SpreadPips);
            return Clamp(1 + rarity + spread);
        }

        // Pips stepping out of `fromClass` adds to `stat` (0 past the top class).
        public static int ClassGain(int fromClass, int stat)
        {
            if (stat < 0 || stat >= Count || fromClass < CarProgression.MinClass || fromClass >= CarProgression.MaxClass) return 0;
            return ClassGainTable[fromClass][stat];
        }

        // The stat at a class: the base plus every class step climbed, capped at MaxPips.
        public static int Value(CarDef car, int carClass, int stat)
        {
            if (car == null || stat < 0 || stat >= Count) return 0;
            int v = Base(car, stat);
            int to = CarProgression.ClampClass(carClass);
            for (int c = CarProgression.MinClass; c < to; c++) v += ClassGain(c, stat);
            return Clamp(v);
        }

        // Every row as the screen draws it. `owned` null = a car still to collect: class 0 values, no next class.
        public static CarStatLine[] Lines(CarDef car, OwnedCar owned)
        {
            var lines = new CarStatLine[Count];
            int carClass = owned == null ? CarProgression.MinClass : CarProgression.ClampClass(owned.carClass);
            bool next = owned != null && !CarProgression.IsMaxClass(owned);
            for (int s = 0; s < Count; s++)
            {
                int now = Value(car, carClass, s);
                lines[s] = new CarStatLine
                {
                    name = Names[s],
                    value = now,
                    nextGain = next ? Value(car, carClass + 1, s) - now : 0,
                };
            }
            return lines;
        }

        // PLACEHOLDER trait per DM2 driver type (CarDef.type): the design's "Trait — Bulldozer" box. Words only;
        // no trait changes how the car races yet.
        public static string TraitName(CarDef car) =>
            car != null && DriverTypes.IsValid(car.type) ? car.type : DriverTypes.Rookie;

        public static string TraitText(CarDef car) => TraitName(car) switch
        {
            DriverTypes.Strategist => "Saves fuel and tyres on a long run.",
            DriverTypes.Closer => "Finds speed in the closing laps.",
            DriverTypes.Intimidator => "Cars ahead give way sooner.",
            DriverTypes.Blocker => "Hard to pass on the inside.",
            DriverTypes.Pusher => "Pushes harder in the draft.",
            DriverTypes.Dominator => "Pulls away when it leads.",
            DriverTypes.Legend => "Bump contact costs you no speed.",
            _ => "Fresh off the trucks. Room to grow.",
        };

        static int Clamp(int pips) => Math.Max(0, Math.Min(MaxPips, pips));

        static uint Fnv(string text, int salt)
        {
            uint h = 2166136261;
            foreach (char ch in text) { h ^= ch; h *= 16777619; }
            h ^= (uint)salt + 1u;
            h *= 16777619;
            return h;
        }
    }
}
