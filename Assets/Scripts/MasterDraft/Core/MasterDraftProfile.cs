using System;
using System.Collections.Generic;

namespace Draftmaster.MasterDraft
{
    // Everything one Master Draft player owns. Saved as JSON (masterdraft.json locally, Cloud Save later) —
    // separate from the career's sqlite database, which Master Draft never touches.
    //
    // Schema 2 (the Draftmaster 2 model, Docs/MasterDraft-Rework.md): a garage of owned cars, the player's level
    // and XP, coins (the money races pay and class upgrades cost), championships in progress, series records and
    // challenge records. Schema 1 was the card collection; FromJson converts one (SchemaMigration) into a
    // starter car plus coins, and LocalMasterDraftBackend keeps the old file as masterdraft.v1.json.
    //
    // The card fields (cards, team, packs, trades) stay until the card mode retires (rework row 9) so its code
    // keeps compiling and running; a migrated profile starts them empty.
    [Serializable]
    public class MasterDraftProfile
    {
        public const int CurrentSchema = 2;
        public const int CardSchema = 1;      // the card-collection profile, before the garage
        public const int FirstLevel = 1;

        // 0 = not stamped (a file with no schemaVersion is read as schema 1). New() stamps CurrentSchema.
        public int schemaVersion;
        public string playerId = "";
        public string displayName = "Player";
        public int coins = Economy.StartingCoins;   // the one currency: race money in, class upgrades and packs out
        public int racesRun;
        public int wins;

        // --- Schema 2: the garage, level and progress ---
        public List<OwnedCar> garage = new();
        public string selectedCar = "";      // OwnedCar.id the garage last picked; an owned car once sanitised
        public int level = FirstLevel;       // player level, DM2's "Level" (gates series and challenges)
        public int xp;                       // progress toward the next level (RaceRewards.XpToNextLevel)
        public List<ChampionshipProgress> championships = new();   // in progress, at most one per series
        public List<SeriesRecord> seriesRecords = new();
        public List<ChallengeRecord> challengeRecords = new();
        public List<string> altPaints = new();                     // alternate livery sprites unlocked
        public ProfileMigration migration = new();                 // what converting an older save did; empty if none

        // --- Schema 1: the card mode (retires in rework row 9) ---
        public bool starterClaimed;
        public int nextUid = 1;
        public int packsOpened;
        public List<CardInstance> cards = new();
        public MasterDraftTeam team = new();
        public List<TradeOffer> myListings = new();
        public List<TradeOffer> botOffers = new();
        public int tradeRefreshes;

        // A brand-new profile: schema 2, level 1, the starting coins and a starter car (StarterCars.Pick — the
        // catalogue is optional; without it the first starter id is used).
        public static MasterDraftProfile New(string playerId, string displayName = "Player", CarCatalog cars = null)
        {
            var p = new MasterDraftProfile
            {
                schemaVersion = CurrentSchema,
                playerId = string.IsNullOrEmpty(playerId) ? "local" : playerId,
                displayName = string.IsNullOrEmpty(displayName) ? "Player" : displayName,
            };
            StarterCars.Ensure(p, cars);
            return p;
        }

        // Stable per profile: seeds prize draws (Challenges.PrizeSeed, RaceRewards). A method, not a property, so
        // no serializer ever writes it.
        public long Seed() => DraftRng.Hash(playerId);

        // --- Garage ------------------------------------------------------------------------------------------

        public OwnedCar Car(string carId) => CarProgression.Find(garage, carId);
        public bool Owns(string carId) => CarProgression.Owns(garage, carId);

        // The car the garage has picked (the first owned car if the pick was lost); null with an empty garage.
        public OwnedCar SelectedCar() => Car(selectedCar) ?? (garage != null && garage.Count > 0 ? garage[0] : null);

        // --- Championships -----------------------------------------------------------------------------------

        public ChampionshipProgress Championship(string seriesId)
        {
            if (championships == null || string.IsNullOrEmpty(seriesId)) return null;
            foreach (var c in championships) if (c != null && c.seriesId == seriesId) return c;
            return null;
        }

        public SeriesRecord SeriesRecord(string seriesId)
        {
            if (seriesRecords == null || string.IsNullOrEmpty(seriesId)) return null;
            foreach (var r in seriesRecords) if (r != null && r.seriesId == seriesId) return r;
            return null;
        }

        // Start (or restart — DM2 kept one per series) a championship in an owned car. Entry rules are the
        // caller's to check first (SeriesRules.CanEnter); this only refuses a car the player doesn't own.
        public ChampionshipProgress StartChampionship(SeriesDef series, CarDef car, CarCatalog cars, int aiLevel = -1,
            int lapsMultiplier = 0)
        {
            if (series == null) throw new ArgumentNullException(nameof(series));
            if (car == null) throw new ArgumentNullException(nameof(car));
            if (!Owns(car.id)) throw new ArgumentException($"{car.id} isn't in the garage", nameof(car));
            var progress = SeriesRules.Start(series, car, cars, aiLevel, lapsMultiplier);
            championships.RemoveAll(c => c == null || c.seriesId == series.id);
            championships.Add(progress);
            return progress;
        }

        public bool AbandonChampionship(string seriesId) =>
            championships != null && championships.RemoveAll(c => c != null && c.seriesId == seriesId) > 0;

        // --- Card mode (schema 1; retires in rework row 9) ----------------------------------------------------

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

        // --- JSON --------------------------------------------------------------------------------------------

        public string ToJson(bool pretty = false) => ProfileJson.Writer(this, pretty);

        // Null for empty or unreadable text. Lists JsonUtility left null are put back, older schemas are brought
        // up to date (SchemaMigration) and the result is sanitised. cars (optional) picks the starter car.
        public static MasterDraftProfile FromJson(string json, CarCatalog cars = null) => FromJson(json, cars, out _);

        // migratedFrom = the schema the text was written in when this read converted it (the caller should keep
        // the original text — LocalMasterDraftBackend writes masterdraft.v1.json); 0 when no conversion ran.
        public static MasterDraftProfile FromJson(string json, CarCatalog cars, out int migratedFrom)
        {
            migratedFrom = 0;
            if (string.IsNullOrWhiteSpace(json)) return null;
            // Fetched outside the try: a missing serializer must throw, not read as "no profile" (which
            // would start a fresh one over the saved file).
            var read = ProfileJson.Reader;
            MasterDraftProfile p;
            try { p = read(json, typeof(MasterDraftProfile)) as MasterDraftProfile; }
            catch (Exception) { return null; }
            if (p == null) return null;

            p.RestoreLists();
            if (p.schemaVersion < CurrentSchema)
            {
                migratedFrom = p.schemaVersion <= 0 ? CardSchema : p.schemaVersion;
                SchemaMigration.FromCards(p, cars, migratedFrom);
            }
            p.Sanitise(cars);
            return p;
        }

        // JsonUtility (and System.Text.Json reading "null") can leave lists and objects null; put them back.
        void RestoreLists()
        {
            cards ??= new List<CardInstance>();
            cards.RemoveAll(c => c == null || c.def == null || string.IsNullOrEmpty(c.uid));
            team ??= new MasterDraftTeam();
            team.bench ??= new List<string>();
            team.actionDeck ??= new List<string>();
            myListings ??= new List<TradeOffer>();
            botOffers ??= new List<TradeOffer>();
            garage ??= new List<OwnedCar>();
            championships ??= new List<ChampionshipProgress>();
            seriesRecords ??= new List<SeriesRecord>();
            challengeRecords ??= new List<ChallengeRecord>();
            altPaints ??= new List<string>();
            migration ??= new ProfileMigration();
        }

        // Brings a profile read from disk (or hand-edited) back inside the rules: one entry per car, series and
        // chapter; classes, gears, level and XP in range; a starter car if the garage is empty; the selected car
        // one the player owns.
        void Sanitise(CarCatalog cars)
        {
            RestoreLists();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            garage.RemoveAll(c => c == null || string.IsNullOrEmpty(c.id) || !seen.Add(c.id));
            foreach (var c in garage) CarProgression.Sanitise(c);

            seen.Clear();
            championships.RemoveAll(c => c == null || string.IsNullOrEmpty(c.seriesId) || !seen.Add(c.seriesId));
            foreach (var c in championships) SeriesRules.Sanitise(c);
            seen.Clear();
            seriesRecords.RemoveAll(r => r == null || string.IsNullOrEmpty(r.seriesId) || !seen.Add(r.seriesId));
            Challenges.Sanitise(challengeRecords);
            seen.Clear();
            altPaints.RemoveAll(a => string.IsNullOrEmpty(a) || !seen.Add(a));

            if (level < FirstLevel) level = FirstLevel;
            if (level > RaceRewards.MaxLevel) level = RaceRewards.MaxLevel;
            if (xp < 0) xp = 0;
            if (coins < 0) coins = 0;
            if (racesRun < 0) racesRun = 0;
            if (wins < 0) wins = 0;

            StarterCars.Ensure(this, cars);
            if (!Owns(selectedCar)) selectedCar = garage.Count > 0 ? garage[0].id : "";
        }
    }

    // Coins in and out. All the economy's numbers live here (race money is RaceRewards').
    public static class Economy
    {
        public const int StartingCoins = 500;
        public const int StandardPackPrice = 500;

        // Card mode's race purse (retires in rework row 9; the garage game pays RaceRewards).
        public const int WinnerCoins = 500;
        public const int CoinsLostPerPlace = 25;
        public const int MinimumFinishCoins = 50;
        public const int CoinsPerCardPlayed = 10;
        public const int MaxCardsPaid = 10;

        // What a card is worth when a schema-1 collection converts to schema 2 (SchemaMigration), in the
        // schema-2 money scale: a DM2 race win pays 10,000 and class 1 -> 2 costs 10,000.
        public const int CommonCardCoins = 250;
        public const int RareCardCoins = 1000;
        public const int EpicCardCoins = 2500;
        public const int LegendaryCardCoins = 5000;

        public static int CardTradeIn(Rarity rarity) => rarity switch
        {
            Rarity.Legendary => LegendaryCardCoins,
            Rarity.Epic => EpicCardCoins,
            Rarity.Rare => RareCardCoins,
            _ => CommonCardCoins,
        };

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

    // The one engine service Core needs. Core is compiled without UnityEngine (noEngineReferences) so the rules
    // build and test with plain `dotnet test` (Tools/HeadlessTests); the game installs JsonUtility here
    // (Runtime/MasterDraftJsonInstaller) and the headless tests install System.Text.Json. JsonUtility's rules
    // apply either way: [Serializable] classes, public fields only.
    public static class ProfileJson
    {
        public static Func<object, bool, string> Write;
        public static Func<string, Type, object> Read;

        internal static Func<object, bool, string> Writer =>
            Write ?? throw new InvalidOperationException("No Master Draft JSON serializer installed (ProfileJson.Write).");
        internal static Func<string, Type, object> Reader =>
            Read ?? throw new InvalidOperationException("No Master Draft JSON serializer installed (ProfileJson.Read).");
    }
}
