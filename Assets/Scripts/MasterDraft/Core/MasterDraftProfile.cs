using System;
using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.MasterDraft
{
    // Everything one Master Draft player owns. Saved as JSON (masterdraft.json locally, Cloud Save later) —
    // separate from the career's sqlite database, which Master Draft never touches.
    [Serializable]
    public class MasterDraftProfile
    {
        public const int CurrentSchema = 1;

        public int schemaVersion = CurrentSchema;
        public string playerId = "";
        public string displayName = "Player";
        public int coins = Economy.StartingCoins;
        public bool starterClaimed;
        public int nextUid = 1;
        public int packsOpened;
        public int racesRun;
        public int wins;
        public List<CardInstance> cards = new();
        public MasterDraftTeam team = new();
        public List<TradeOffer> myListings = new();
        public List<TradeOffer> botOffers = new();
        public int tradeRefreshes;

        public static MasterDraftProfile New(string playerId, string displayName = "Player")
        {
            return new MasterDraftProfile
            {
                playerId = string.IsNullOrEmpty(playerId) ? "local" : playerId,
                displayName = string.IsNullOrEmpty(displayName) ? "Player" : displayName,
            };
        }

        public CardInstance Find(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return null;
            foreach (var c in cards) if (c.uid == uid) return c;
            return null;
        }

        public CardInstance Add(CardDefinition def)
        {
            var card = new CardInstance("c" + nextUid++, def.Clone());
            cards.Add(card);
            return card;
        }

        public bool Remove(string uid)
        {
            int i = cards.FindIndex(c => c.uid == uid);
            if (i < 0) return false;
            cards.RemoveAt(i);
            team.Remove(uid);
            return true;
        }

        public int CountOf(CardType type)
        {
            int n = 0;
            foreach (var c in cards) if (c.def.type == type) n++;
            return n;
        }

        // The seed for the next pack this profile opens: stable per profile and per pack, so a pack can be
        // re-derived (and audited) from the profile id and the count.
        public long NextPackSeed() => DraftRng.Hash(playerId) ^ (long)(packsOpened + 1) * 0x5DEECE66DL;

        public string ToJson(bool pretty = false) => JsonUtility.ToJson(this, pretty);

        // Null for empty or unreadable text. Lists JsonUtility left null are put back, and older schemas are
        // brought up to date here.
        public static MasterDraftProfile FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            MasterDraftProfile p;
            try { p = JsonUtility.FromJson<MasterDraftProfile>(json); }
            catch (Exception) { return null; }
            if (p == null) return null;

            p.cards ??= new List<CardInstance>();
            p.cards.RemoveAll(c => c == null || c.def == null || string.IsNullOrEmpty(c.uid));
            p.team ??= new MasterDraftTeam();
            p.team.bench ??= new List<string>();
            p.team.actionDeck ??= new List<string>();
            p.myListings ??= new List<TradeOffer>();
            p.botOffers ??= new List<TradeOffer>();
            if (p.schemaVersion < CurrentSchema) p.schemaVersion = CurrentSchema;   // no migrations yet
            return p;
        }
    }

    // Coins in and out. All the economy's numbers live here.
    public static class Economy
    {
        public const int StartingCoins = 500;
        public const int StandardPackPrice = 500;

        public const int WinnerCoins = 500;
        public const int CoinsLostPerPlace = 25;
        public const int MinimumFinishCoins = 50;
        public const int CoinsPerCardPlayed = 10;
        public const int MaxCardsPaid = 10;

        // position is 1-based; 0 or less = did not finish (paid the minimum). Card bonus capped.
        public static int ForFinish(int position, int cardsPlayed)
        {
            int purse = position <= 0
                ? MinimumFinishCoins
                : Math.Max(MinimumFinishCoins, WinnerCoins - (position - 1) * CoinsLostPerPlace);
            int cards = Math.Max(0, Math.Min(cardsPlayed, MaxCardsPaid));
            return purse + cards * CoinsPerCardPlayed;
        }

        public static int PriceOf(PackKind kind) => kind == PackKind.Starter ? 0 : StandardPackPrice;
    }

    // The result of a backend operation: ok, or a readable reason it wasn't.
    [Serializable]
    public class DraftResult
    {
        public bool ok;
        public string error = "";
        public List<CardInstance> cards = new();
        public int coins;      // balance after the operation
        public int awarded;    // coins this operation paid out (race rewards)

        public static DraftResult Fail(string why) => new DraftResult { ok = false, error = why };
    }

    // The transactions themselves, pure, on a profile in memory. LocalMasterDraftBackend runs these and
    // saves; before online launch the same rules move into Cloud Code so a client can't award itself cards.
    public static class ProfileOps
    {
        public static DraftResult ClaimStarter(MasterDraftProfile profile, CardCatalog catalog)
        {
            if (profile.starterClaimed) return DraftResult.Fail("The Starter Pack has already been opened.");
            var result = Open(profile, catalog, PackKind.Starter);
            profile.starterClaimed = true;

            // A brand-new player gets a team built for them, so GO RACE works straight away.
            if (string.IsNullOrEmpty(profile.team.activeDriver)) profile.team = TeamRules.AutoBuild(profile);
            return result;
        }

        public static DraftResult BuyStandardPack(MasterDraftProfile profile, CardCatalog catalog)
        {
            int price = Economy.PriceOf(PackKind.Standard);
            if (profile.coins < price)
                return DraftResult.Fail($"A Standard Pack costs {price} coins — you have {profile.coins}.");
            profile.coins -= price;
            return Open(profile, catalog, PackKind.Standard);
        }

        static DraftResult Open(MasterDraftProfile profile, CardCatalog catalog, PackKind kind)
        {
            var defs = PackGenerator.Open(kind, catalog, profile.NextPackSeed());
            PackGenerator.SortForReveal(defs);
            profile.packsOpened++;
            var result = new DraftResult { ok = true };
            foreach (var d in defs) result.cards.Add(profile.Add(d));
            result.coins = profile.coins;
            return result;
        }

        public static DraftResult AwardRace(MasterDraftProfile profile, int position, int cardsPlayed)
        {
            int coins = Economy.ForFinish(position, cardsPlayed);
            profile.coins += coins;
            profile.racesRun++;
            if (position == 1) profile.wins++;
            return new DraftResult { ok = true, awarded = coins, coins = profile.coins };
        }
    }
}
