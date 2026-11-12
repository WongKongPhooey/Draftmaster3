using System;
using System.Collections.Generic;

namespace Draftmaster.MasterDraft
{
    // The order the Card Collection grid lists cars in. Owned cars always come before ones still to collect.
    public enum CollectionSort
    {
        Rarity = 0,   // Legend first, then the highest class, then the fullest gears bar (the design's default)
        Class = 1,    // highest class first, then rarity
        Number = 2,   // catalogue order: carset, then car number
    }

    // What one card's gears bar says (the design's "9/50 PARTS" / "30/30 READY" line, in DM2's gears).
    public struct CardProgress
    {
        public int gears;          // banked toward leaving the current class
        public int target;         // gears to leave it (CarProgression.ClassGears); 0 at max class
        public float fill;         // bar fill 0-1; 1 at max class
        public bool ready;         // gears are there for the next class (coins may not be)
        public bool canUpgrade;    // gears and coins both there: the card shows "UP"
        public bool max;           // top class
        public string caption;     // "9/50 Gears", "30/30 Ready", "Max Class"
    }

    // The Card Collection screen's rules (Master Draft's garage, Docs/Master-Draft.md): which cars a filter shows and
    // in what order, how many are collected, and what each card's badge, rarity line and gears bar say. Engine-free,
    // so MasterDraftCollectionTests run it headless.
    //
    // The design handoff (Draftmaster3_UI_Unity, Collection_PC) draws a level badge (LV1-LV6) and four rarities
    // (Common / Rare / Epic / Legend). Master Draft uses DM2's classes for the badge instead — R (Rookie), D, C, B,
    // A, S, S the top — read through Restrictions.ClassLetter so the garage and the series' entry rules can never
    // disagree about a car's letter. DM2's 1-4 star rarities are the design's four rarities in order.
    public static class CardCollection
    {
        public const int CardsPerRow = 5;

        static readonly string[] RarityNames = { "All", "Common", "Rare", "Epic", "Legend" };

        // "Common" (1 star) ... "Legend" (4 stars); 0 (no filter) reads "All".
        public static string RarityName(int rarity) =>
            RarityNames[Math.Max(0, Math.Min(CarCatalog.MaxRarity, rarity))];

        // The card's class badge: R, D, C, B, A or S.
        public static string ClassLetter(int carClass) => Restrictions.ClassLetter(CarProgression.ClampClass(carClass));

        public static string SortName(CollectionSort sort) => sort switch
        {
            CollectionSort.Class => "Class",
            CollectionSort.Number => "Number",
            _ => "Rarity",
        };

        public static CollectionSort NextSort(CollectionSort sort) =>
            sort >= CollectionSort.Number ? CollectionSort.Rarity : sort + 1;

        public static CardProgress Progress(OwnedCar car, int coins)
        {
            var p = new CardProgress();
            if (car == null) { p.caption = ""; return p; }
            p.gears = Math.Max(0, car.gears);
            p.max = CarProgression.IsMaxClass(car);
            if (p.max)
            {
                p.fill = 1f;
                p.caption = "Max Class";
                return p;
            }
            p.target = CarProgression.ClassGears(car.carClass);
            p.fill = CarProgression.ClassProgress(car);
            p.ready = p.gears >= p.target;
            p.canUpgrade = CarProgression.CanUpgrade(car, coins) == UpgradeCheck.Ok;
            p.caption = $"{p.gears}/{p.target} " + (p.ready ? "Ready" : "Gears");
            return p;
        }

        // Cars in the catalogue that the garage holds (a garage entry for a car the catalogue has dropped
        // doesn't count): the "10 / 48 collected" line.
        public static int Collected(CarCatalog cars, IList<OwnedCar> garage)
        {
            if (cars == null || garage == null) return 0;
            int n = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var car in garage)
                if (car != null && cars.Contains(car.id) && seen.Add(car.id)) n++;
            return n;
        }

        // Every catalogue car passing the filters, owned first, each half in the sort's order.
        // rarity 0 = every rarity; carset null/empty = every carset.
        public static List<CarDef> List(CarCatalog cars, IList<OwnedCar> garage, int rarity, string carset, CollectionSort sort)
        {
            var owned = new List<CarDef>();
            var locked = new List<CarDef>();
            if (cars == null) return owned;
            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < cars.Cars.Count; i++) index[cars.Cars[i].id] = i;

            foreach (var c in cars.Cars)
            {
                if (rarity > 0 && c.rarity != rarity) continue;
                if (!string.IsNullOrEmpty(carset) && !string.Equals(c.carset, carset, StringComparison.OrdinalIgnoreCase)) continue;
                (CarProgression.Owns(garage, c.id) ? owned : locked).Add(c);
            }

            int Catalogue(CarDef a, CarDef b) => index[a.id].CompareTo(index[b.id]);

            Comparison<CarDef> ownedOrder = sort switch
            {
                CollectionSort.Number => Catalogue,
                CollectionSort.Class => (a, b) => FirstNonZero(
                    -ClassOf(garage, a).CompareTo(ClassOf(garage, b)),
                    b.rarity.CompareTo(a.rarity),
                    -FillOf(garage, a).CompareTo(FillOf(garage, b)),
                    Catalogue(a, b)),
                _ => (a, b) => FirstNonZero(
                    b.rarity.CompareTo(a.rarity),
                    -ClassOf(garage, a).CompareTo(ClassOf(garage, b)),
                    -FillOf(garage, a).CompareTo(FillOf(garage, b)),
                    Catalogue(a, b)),
            };
            // Cars still to collect have no class: rarity order (or catalogue order) is all there is.
            Comparison<CarDef> lockedOrder = sort == CollectionSort.Number
                ? Catalogue
                : (a, b) => FirstNonZero(b.rarity.CompareTo(a.rarity), Catalogue(a, b));

            owned.Sort(ownedOrder);
            locked.Sort(lockedOrder);
            owned.AddRange(locked);
            return owned;
        }

        // Rows the grid needs for this many cards.
        public static int Rows(int cards) => (Math.Max(0, cards) + CardsPerRow - 1) / CardsPerRow;

        // Keeps a scroll (the first row on screen) inside the list, with `visibleRows` rows showing.
        public static int ClampScroll(int scroll, int cards, int visibleRows) =>
            Math.Max(0, Math.Min(scroll, Rows(cards) - Math.Max(1, visibleRows)));

        // The scroll that brings `index`'s row on screen, moving as little as possible from `scroll`.
        public static int ScrollToShow(int index, int scroll, int cards, int visibleRows)
        {
            int row = Math.Max(0, index) / CardsPerRow;
            int rows = Math.Max(1, visibleRows);
            if (row < scroll) scroll = row;
            else if (row >= scroll + rows) scroll = row - rows + 1;
            return ClampScroll(scroll, cards, rows);
        }

        static int ClassOf(IList<OwnedCar> garage, CarDef c) => CarProgression.Find(garage, c.id)?.carClass ?? -1;
        static float FillOf(IList<OwnedCar> garage, CarDef c) => CarProgression.ClassProgress(CarProgression.Find(garage, c.id));

        static int FirstNonZero(params int[] results)
        {
            foreach (int r in results) if (r != 0) return r;
            return 0;
        }
    }
}
