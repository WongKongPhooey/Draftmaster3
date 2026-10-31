using System.Collections.Generic;

namespace Draftmaster.MasterDraft
{
    public enum PackKind { Starter = 0, Standard = 1 }

    // What comes out of a pack. Pure: same catalogue + same seed = same cards.
    //
    // Guarantees are built in, not rolled for: the guaranteed slots are filled first with a card of the
    // required type, so there is no "re-roll until it has a driver" loop that could spin.
    public static class PackGenerator
    {
        public const int StarterSize = 12;
        public const int StandardSize = 6;

        // The Starter's guaranteed slots: enough to field a legal team on its own — a driver, a car, a crew
        // chief and a five-card action deck (MinDeck) — plus a second driver for the bench and wildcards.
        public const int StarterActions = TeamRules.MinDeck;

        // Rarity odds for an ordinary slot, Common..Legendary.
        public static readonly int[] RarityWeights = { 60, 28, 10, 2 };
        // The last slot of a Standard pack is always Rare or better.
        public static readonly int[] FeatureSlotWeights = { 0, 75, 20, 5 };
        // Type odds for an ordinary slot, Driver / Car / CrewChief / Action.
        public static readonly int[] TypeWeights = { 30, 15, 15, 40 };

        public static int SizeOf(PackKind kind) => kind == PackKind.Starter ? StarterSize : StandardSize;

        public static string NameOf(PackKind kind) => kind == PackKind.Starter ? "Starter Pack" : "Standard Pack";

        public static List<CardDefinition> Open(PackKind kind, CardCatalog catalog, long seed)
        {
            var rng = new DraftRng(seed);
            return kind == PackKind.Starter ? Starter(catalog, rng) : Standard(catalog, rng);
        }

        static List<CardDefinition> Starter(CardCatalog catalog, DraftRng rng)
        {
            var cards = new List<CardDefinition>(StarterSize)
            {
                catalog.Pick(CardType.Driver, Rarity.Rare, rng),
                catalog.Pick(CardType.Driver, Rarity.Common, rng),
                catalog.Pick(CardType.Car, Rarity.Common, rng),
                catalog.Pick(CardType.CrewChief, Rarity.Common, rng),
            };

            // Five actions spread across the playable effects — cycling a shuffled list, so no effect appears
            // more than the deck's copy limit while there are at least three to choose from.
            var playable = new List<ActionSpec>();
            foreach (var s in ActionCatalog.All) if (s.Implemented) playable.Add(s);
            rng.Shuffle(playable);
            for (int i = 0; i < StarterActions && playable.Count > 0; i++)
                cards.Add(ActionCatalog.Definition(playable[i % playable.Count]));

            while (cards.Count < StarterSize) cards.Add(Wildcard(catalog, rng, RarityWeights));
            cards.RemoveAll(c => c == null);
            return cards;
        }

        static List<CardDefinition> Standard(CardCatalog catalog, DraftRng rng)
        {
            var cards = new List<CardDefinition>(StandardSize);
            for (int i = 0; i < StandardSize - 1; i++) cards.Add(Wildcard(catalog, rng, RarityWeights));
            cards.Add(Wildcard(catalog, rng, FeatureSlotWeights));
            cards.RemoveAll(c => c == null);
            return cards;
        }

        static CardDefinition Wildcard(CardCatalog catalog, DraftRng rng, int[] rarityWeights)
        {
            var type = (CardType)rng.Weighted(TypeWeights);
            var rarity = (Rarity)rng.Weighted(rarityWeights);
            return catalog.Pick(type, rarity, rng);
        }

        // The order a pack is revealed in: commons first, the best card last.
        public static void SortForReveal(List<CardDefinition> cards)
        {
            cards.Sort((a, b) => a.rarity != b.rarity ? a.rarity.CompareTo(b.rarity) : a.overall.CompareTo(b.overall));
        }
    }
}
