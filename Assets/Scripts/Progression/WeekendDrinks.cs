using Draftmaster.Weekend;
using UnityEngine;

namespace Draftmaster.Progression
{
    // What is in the vending machine beside the paddock grandstand, and what a can of it does to you.
    //
    // The machine is restocked every race weekend: four cans off a longer list, each one paired with a
    // random pair of career attributes and a random swing between +1 and +3. A can always GIVES and TAKES
    // the same amount — it is a trade, not a reward, so the one that buys a tenth in qualifying costs
    // something you would rather not have spent. The swing lasts the weekend and no longer.
    //
    // Deterministic from the weekend id (WeekendRandom), for the same reason the timetable is: the race
    // scene reloads between practice, qualifying and the race, and the machine has to have the same four
    // cans in it on Sunday morning that it had on Friday. Nothing about it is stored except WHICH can was
    // taken.
    //
    // The swing is deliberately NOT written into the stats ledger (PlayerStatsLedger and the "stat." keys
    // CareerPath pays its starting grants into). A ledger counter is permanent career progress; this is a
    // can of pop. It is held as an overlay instead — read it with EffectiveStat — so there is no revert
    // step that can be missed and leave a career permanently dented by a Friday afternoon drink.
    //
    // Pure rules plus its own two PlayerPrefs keys, in the Progression assembly so it is EditMode-testable
    // (see WeekendDrinksTests). The machine itself is VendingMachine / VendingMachineSpawner.
    public static class WeekendDrinks
    {
        // How many cans are in the machine on any given weekend.
        public const int SelectionSize = 4;
        // The swing on a can, either way. Big enough to be felt against a starting spread of 17 points
        // across five attributes, small enough that no single can decides a career.
        public const int MinSwing = 1;
        public const int MaxSwing = 3;

        // Keeps this stream clear of the timetable's and the press bank's, which are keyed on the same id.
        const int Stream = 0xD21;

        const string WeekendKey = "vending.weekend";   // which weekend the pick below belongs to
        const string PickKey = "vending.pick";         // index into that weekend's Selection()

        // One can: what it is called, what it tastes of, and the trade it makes.
        public struct Drink
        {
            public string name;
            public string flavour;     // spoken when it is opened
            public string boostKey;    // career attribute raised, a CareerPath.StatKeys entry
            public string drainKey;    // career attribute lowered by the same amount
            public int amount;

            public bool IsValid => !string.IsNullOrEmpty(name) && amount > 0;
        }

        // The rack. Names and flavour only — which attributes a can moves is rolled per weekend, so the
        // machine never becomes a lookup table the player memorises.
        static readonly string[,] Catalogue =
        {
            { "HOT LAP COLA",        "Warm from the top of the stack, and somehow better for it." },
            { "DRAFT ENERGY",        "Tastes like a fire extinguisher. You feel it behind your eyes." },
            { "PIT ROAD ICED TEA",   "Sweet enough to stand a spanner up in." },
            { "VICTORY LANE SODA",   "Confetti on the label. Nobody has won anything yet." },
            { "GASOLINE ALLEY ROOT", "Root beer, allegedly. It foams for a worryingly long time." },
            { "TORQUE WRENCH COLD",  "Black coffee out of a can. It does not pretend to be nice." },
            { "BACKMARKER BLUE",     "Turns your tongue blue for the rest of the day. Worth it." },
            { "CHEQUERS CHERRY",     "Cherry, mostly. The bubbles go straight up your nose." },
            { "GRID WALK GINGER",    "Hot and sharp. Settles a stomach that has been doing laps." },
            { "TWO-STOP TONIC",      "Bitter. The crew swear by it and none of them will say why." },
            { "PADDOCK SPRING",      "Just water. The machine hands it over slightly grudgingly." },
            { "LOOSE LUGNUT LIME",   "So sour your jaw aches. You are suddenly wide awake." },
        };

        public static int CatalogueSize => Catalogue.GetLength(0);

        // The four cans in the machine this weekend. Same answer every time for the same weekend.
        public static Drink[] Selection(int weekendId)
        {
            var keys = CareerPath.StatKeys;
            var rng = WeekendRandom.For(weekendId, Stream);

            var order = new int[CatalogueSize];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            rng.Shuffle(order);

            int count = Mathf.Min(SelectionSize, CatalogueSize);
            var drinks = new Drink[count];

            for (int i = 0; i < count; i++)
            {
                int boost = 0, drain = 0, amount = MinSwing;

                // Two cans offering the identical trade reads as a bug rather than a coincidence, so a
                // repeat is re-rolled a few times. Bounded, and the roll stays deterministic either way.
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    boost = rng.Range(0, keys.Length);
                    drain = rng.Range(0, keys.Length - 1);
                    if (drain >= boost) drain++;               // never drains what it boosts
                    amount = rng.Range(MinSwing, MaxSwing + 1);
                    if (!AlreadyOffered(drinks, i, keys[boost], keys[drain], amount)) break;
                }

                drinks[i] = new Drink
                {
                    name = Catalogue[order[i], 0],
                    flavour = Catalogue[order[i], 1],
                    boostKey = keys[boost],
                    drainKey = keys[drain],
                    amount = amount,
                };
            }
            return drinks;
        }

        static bool AlreadyOffered(Drink[] drinks, int upTo, string boost, string drain, int amount)
        {
            for (int i = 0; i < upTo; i++)
                if (drinks[i].boostKey == boost && drinks[i].drainKey == drain && drinks[i].amount == amount)
                    return true;
            return false;
        }

        // Which can was taken this weekend, or -1 for none. A pick stamped with another weekend's id has
        // expired — that is the whole expiry mechanism, and it needs nobody to remember to run it.
        public static int TakenIndex(int weekendId)
        {
            if (PlayerPrefs.GetInt(WeekendKey, -1) != weekendId) return -1;
            int pick = PlayerPrefs.GetInt(PickKey, -1);
            return pick >= 0 && pick < SelectionSize ? pick : -1;
        }

        public static bool HasTaken(int weekendId) => TakenIndex(weekendId) >= 0;

        public static bool TryGetTaken(int weekendId, out Drink drink)
        {
            drink = default;
            int index = TakenIndex(weekendId);
            if (index < 0) return false;

            var selection = Selection(weekendId);
            if (index >= selection.Length) return false;
            drink = selection[index];
            return true;
        }

        // Take a can. One per weekend: the machine is a decision, not a dial to be wound up.
        public static bool Take(int weekendId, int index)
        {
            if (index < 0 || index >= Selection(weekendId).Length) return false;
            if (HasTaken(weekendId)) return false;

            PlayerPrefs.SetInt(WeekendKey, weekendId);
            PlayerPrefs.SetInt(PickKey, index);
            PlayerPrefs.Save();
            return true;
        }

        // What this weekend's can is doing to one career attribute right now: + for the one it raised,
        // - for the one it cost, 0 for everything else and for a weekend with no can taken.
        public static int Bonus(string statKey, int weekendId)
        {
            if (string.IsNullOrEmpty(statKey)) return 0;
            if (!TryGetTaken(weekendId, out var drink)) return 0;
            if (drink.boostKey == statKey) return drink.amount;
            if (drink.drainKey == statKey) return -drink.amount;
            return 0;
        }

        // What the attribute is worth today: the career value plus whatever is in the player's hand.
        public static int EffectiveStat(string statKey, int weekendId) =>
            CareerPath.Stat(statKey) + Bonus(statKey, weekendId);

        // "+2 Driving / -2 Business"
        public static string Effect(Drink drink) =>
            !drink.IsValid ? "no effect"
                           : $"+{drink.amount} {StatLabel(drink.boostKey)} / -{drink.amount} {StatLabel(drink.drainKey)}";

        // The row the machine offers: what it is called and what it costs you.
        public static string Label(Drink drink) => $"{drink.name}   -   {Effect(drink)}";

        // "career.pitcraft" -> "Pit Craft". Title case, because these are read off a list rather than out
        // of the middle of a sentence (CareerPathNPC has the lower-case prose form for that).
        public static string StatLabel(string statKey)
        {
            if (string.IsNullOrEmpty(statKey)) return "";
            if (statKey == CareerPath.StatPitCraft) return "Pit Craft";

            int dot = statKey.LastIndexOf('.');
            string bare = dot >= 0 && dot < statKey.Length - 1 ? statKey.Substring(dot + 1) : statKey;
            return bare.Length == 0 ? statKey : char.ToUpperInvariant(bare[0]) + bare.Substring(1);
        }

        // Debug/testing: put the can back. The next weekend does this by itself.
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(WeekendKey);
            PlayerPrefs.DeleteKey(PickKey);
            PlayerPrefs.Save();
        }
    }
}
