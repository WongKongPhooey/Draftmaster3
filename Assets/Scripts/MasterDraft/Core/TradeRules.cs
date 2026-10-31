using System;
using System.Collections.Generic;

namespace Draftmaster.MasterDraft
{
    // One trade on the board: somebody gives `giving` and wants any card of `wantType` at `wantMinRarity` or
    // better. Bot offers are the offline stand-in for other players; `mine` marks the player's own listings.
    [Serializable]
    public class TradeOffer
    {
        public string id = "";
        public string fromName = "";
        public bool mine;
        public string myCardUid = "";        // the card the player listed (own listings only)
        public CardDefinition giving;        // what the other side hands over (bot offers only)
        public CardType wantType;
        public Rarity wantMinRarity;

        public string WantText => $"any {CardText.RarityName(wantMinRarity)}+ {CardText.TypeName(wantType)}";
    }

    public static class TradeRules
    {
        public const int BotOfferCount = 6;

        static readonly string[] BotNames =
        {
            "PitRoadPete", "DraftKing88", "LooseWheel", "TurnFourTina", "BumpAndRun", "GreenFlagGus",
            "PlateRacer", "ShortTrackSal", "WallRider", "FuelMileageMo",
        };

        // Fills the board with bot offers when it's empty. Deterministic per profile and refresh count.
        public static void EnsureBotOffers(MasterDraftProfile profile, CardCatalog catalog)
        {
            if (profile.botOffers.Count > 0) return;
            var rng = new DraftRng(DraftRng.Hash(profile.playerId) ^ (long)(profile.tradeRefreshes + 1) * 0x2545F491L);
            profile.tradeRefreshes++;
            for (int i = 0; i < BotOfferCount; i++)
            {
                var giveType = (CardType)rng.Weighted(PackGenerator.TypeWeights);
                var giveRarity = (Rarity)rng.Weighted(PackGenerator.RarityWeights);
                var giving = catalog.Pick(giveType, giveRarity, rng);
                if (giving == null) continue;
                // Bots ask for the same rarity they give, of any type, at most one step down.
                var wantType = (CardType)rng.Next(4);
                var wantRarity = (Rarity)Math.Max(0, (int)giving.rarity - rng.Next(2));
                profile.botOffers.Add(new TradeOffer
                {
                    id = $"bot-{profile.tradeRefreshes}-{i}",
                    fromName = rng.Pick(BotNames),
                    giving = giving,
                    wantType = wantType,
                    wantMinRarity = wantRarity,
                });
            }
        }

        // Whether this card would satisfy this offer. Null = yes, otherwise the reason not.
        public static string CanFulfil(MasterDraftProfile profile, TradeOffer offer, string myUid)
        {
            var card = profile.Find(myUid);
            if (card == null) return "That card isn't in your collection.";
            if (offer == null) return "That offer is gone.";
            if (card.def.type != offer.wantType) return $"They want {offer.WantText}.";
            if (card.def.rarity < offer.wantMinRarity) return $"They want {offer.WantText}.";
            if (profile.team.Uses(myUid)) return $"{card.def.name} is in your team — take it out first.";
            if (IsListed(profile, myUid)) return $"{card.def.name} is listed for trade — cancel the listing first.";
            return null;
        }

        // Swaps the player's card for the offer's. The offer leaves the board.
        public static DraftResult Accept(MasterDraftProfile profile, string offerId, string myUid)
        {
            var offer = profile.botOffers.Find(o => o.id == offerId);
            string why = CanFulfil(profile, offer, myUid);
            if (why != null) return DraftResult.Fail(why);

            profile.Remove(myUid);
            var got = profile.Add(offer.giving);
            profile.botOffers.Remove(offer);
            var result = new DraftResult { ok = true, coins = profile.coins };
            result.cards.Add(got);
            return result;
        }

        // Lists one of the player's cards. Offline, nobody answers — the listing sits until cancelled.
        public static DraftResult Propose(MasterDraftProfile profile, string myUid, CardType wantType, Rarity wantMinRarity)
        {
            var card = profile.Find(myUid);
            if (card == null) return DraftResult.Fail("That card isn't in your collection.");
            if (profile.team.Uses(myUid)) return DraftResult.Fail($"{card.def.name} is in your team — take it out first.");
            if (IsListed(profile, myUid)) return DraftResult.Fail($"{card.def.name} is already listed.");
            profile.myListings.Add(new TradeOffer
            {
                id = "mine-" + myUid,
                fromName = profile.displayName,
                mine = true,
                myCardUid = myUid,
                wantType = wantType,
                wantMinRarity = wantMinRarity,
            });
            return new DraftResult { ok = true, coins = profile.coins };
        }

        public static bool Cancel(MasterDraftProfile profile, string listingId) =>
            profile.myListings.RemoveAll(o => o.id == listingId) > 0;

        public static bool IsListed(MasterDraftProfile profile, string uid) =>
            profile.myListings.Exists(o => o.myCardUid == uid);

        // Cards the player could put up: everything not in the team and not already listed.
        public static List<CardInstance> Tradeable(MasterDraftProfile profile)
        {
            var list = new List<CardInstance>();
            foreach (var c in profile.cards)
                if (!profile.team.Uses(c.uid) && !IsListed(profile, c.uid)) list.Add(c);
            return list;
        }
    }
}
