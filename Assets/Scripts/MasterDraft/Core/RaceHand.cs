using System.Collections.Generic;

namespace Draftmaster.MasterDraft
{
    // The action cards during one race. The deck is shuffled at the green; every time the player crosses the
    // start/finish line one card is drawn into the hand; a played card goes to the discard pile; when the
    // draw pile runs out the discards are shuffled back in.
    //
    // Cards are not used up: the deck is the same next race (Balatro-style). See Docs/Master-Draft.md.
    public sealed class RaceHand
    {
        public const int HandCap = 5;
        // Cards in hand at the green, before the first crossing.
        public const int OpeningHand = 1;

        readonly DraftRng _rng;
        readonly List<CardDefinition> _draw = new();
        readonly List<CardDefinition> _hand = new();
        readonly List<CardDefinition> _discard = new();

        public IReadOnlyList<CardDefinition> Hand => _hand;
        public int DrawPileCount => _draw.Count;
        public int DiscardCount => _discard.Count;
        public int CardsPlayed { get; private set; }
        public int Crossings { get; private set; }

        public RaceHand(IEnumerable<CardDefinition> deck, long seed, int openingHand = OpeningHand)
        {
            _rng = new DraftRng(seed);
            if (deck != null) foreach (var c in deck) if (c != null) _draw.Add(c);
            _rng.Shuffle(_draw);
            for (int i = 0; i < openingHand; i++) Draw();
        }

        // One crossing of the line: one card, if there's room in the hand. A full hand skips the draw and the
        // card stays where it was in the deck. Returns the card drawn, or null.
        public CardDefinition OnStartFinishCrossed()
        {
            Crossings++;
            return Draw();
        }

        CardDefinition Draw()
        {
            if (_hand.Count >= HandCap) return null;
            if (_draw.Count == 0)
            {
                if (_discard.Count == 0) return null;
                _draw.AddRange(_discard);
                _discard.Clear();
                _rng.Shuffle(_draw);
            }
            var card = _draw[_draw.Count - 1];
            _draw.RemoveAt(_draw.Count - 1);
            _hand.Add(card);
            return card;
        }

        // Plays the card at this hand index. Returns it, or null for a bad index or an action the race
        // can't apply (which stays in the hand).
        public CardDefinition Play(int index)
        {
            if (index < 0 || index >= _hand.Count) return null;
            var card = _hand[index];
            if (card.type != CardType.Action || !ActionCatalog.IsPlayable(card.effect)) return null;
            _hand.RemoveAt(index);
            _discard.Add(card);
            CardsPlayed++;
            return card;
        }
    }
}
