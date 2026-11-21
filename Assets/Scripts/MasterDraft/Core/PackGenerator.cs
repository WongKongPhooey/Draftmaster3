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

        // The Starter's guaranteed slots: enough to field a legal team on its own — a driver, a car and a crew
        // chief — plus a second driver for the bench; the rest are wildcards.

        // Rarity odds for an ordinary slot, Common..Legendary.
        public static readonly int[] RarityWeights = { 60, 28, 10, 2 };
        // The last slot of a Standard pack is always Rare or better.
        public static readonly int[] FeatureSlotWeights = { 0, 75, 20, 5 };
        // Type odds for an ordinary slot, Driver / Car / CrewChief.
        public static readonly int[] TypeWeights = { 50, 25, 25 };

        public static int SizeOf(PackKind kind) => kind == PackKind.Starter ? StarterSize : StandardSize;

        public static string NameOf(PackKind kind) => kind == PackKind.Starter ? "Starter Pack" : "Standard Pack";

        // One car card in this many comes out wearing an alternate paint — a shiny card. A Standard pack holds a
        // car about one time in four a slot, so roughly one Standard pack in four has a shiny in it.
        public const int AltPaintOneIn = 6;

        // The alt paints roll on their own stream, so a seed's cards are the same with or without a paint catalogue.
        const long AltPaintSalt = 0x416C745061696E74L;   // "AltPaint"

        // paints (optional): the paint catalogue the alt paints are drawn from. Null = no alt paints.
        public static List<CardDefinition> Open(PackKind kind, CardCatalog catalog, long seed, PaintCatalog paints = null)
        {
            var rng = new DraftRng(seed);
            var cards = kind == PackKind.Starter ? Starter(catalog, rng) : Standard(catalog, rng);
            PaintAlts(cards, paints, new DraftRng(seed ^ AltPaintSalt));
            return cards;
        }

        // The alternate paints in a catalogue (alt > 0), catalogue order.
        public static List<PaintDef> AltPaints(PaintCatalog paints)
        {
            var alts = new List<PaintDef>();
            if (paints == null) return alts;
            foreach (var p in paints.Paints)
                if (p.alt > 0) alts.Add(p);
            return alts;
        }

        // Each car card rolls once: one in AltPaintOneIn wears an alt paint, picked evenly. The cards are the
        // catalogue's clones, so painting one never touches the catalogue.
        static void PaintAlts(List<CardDefinition> cards, PaintCatalog paints, DraftRng rng)
        {
            var alts = AltPaints(paints);
            if (alts.Count == 0) return;
            foreach (var c in cards)
            {
                if (c.type != CardType.Car || rng.Next(AltPaintOneIn) != 0) continue;
                var p = rng.Pick(alts);
                c.paint = p.id;
                c.paintLivery = p.livery;
                c.paintName = p.DisplayName;
            }
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
