using System;
using System.Collections.Generic;

namespace Draftmaster.MasterDraft
{
    // What converting an older save did, kept in the profile so the garage can tell the player once
    // (acknowledged) and so nothing about the conversion is silent. fromSchema 0 = never converted.
    [Serializable]
    public class ProfileMigration
    {
        public const string V1BackupFile = "masterdraft.v1.json";   // LocalMasterDraftBackend keeps the old file here

        public int fromSchema;          // the schema converted from; 0 = this profile was never converted
        public string migratedUtc = ""; // ISO 8601, when it happened
        public int coinsBefore;         // the schema-1 balance, carried over 1:1
        public int cardsConverted;      // every card in the collection
        public int commons, rares, epics, legendaries;
        public int coinsFromCards;      // Economy.CardTradeIn summed over the collection
        public int teamCards;           // how many of them were in the team (driver, car, chief, bench, deck)
        public int listingsCancelled;   // trade listings the player had up
        public string starterCar = "";  // the car the conversion gave (empty if the garage already had cars)
        public string backupFile = "";  // where the old file was kept; set by the backend that wrote it
        public bool acknowledged;       // the player has seen the notice

        public bool Happened() => fromSchema > 0;

        // One paragraph for the garage's notice.
        public string Describe()
        {
            if (!Happened()) return "";
            var parts = new List<string>();
            if (cardsConverted > 0)
                parts.Add($"your {cardsConverted} card{(cardsConverted == 1 ? "" : "s")} ({commons} common, {rares} rare, " +
                          $"{epics} epic, {legendaries} legendary) became {coinsFromCards:N0} coins");
            else
                parts.Add("your card collection was empty");
            if (!string.IsNullOrEmpty(starterCar)) parts.Add($"you have a starter car ({starterCar})");
            string text = "Master Draft is now about cars, not cards: " + string.Join(", and ", parts) + ".";
            if (!string.IsNullOrEmpty(backupFile)) text += $" Your old save is kept as {backupFile}.";
            return text;
        }
    }

    // Schema 1 -> 2: the card collection becomes coins and a starter car. Cards are valued by rarity
    // (Economy.CardTradeIn) on top of the coins already held; the cards, team and trades are then emptied (the card
    // mode still compiles and runs until rework row 9, and a converted player can claim its starter pack again).
    // Races run, wins and the player id carry over. Everything the conversion did is written into profile.migration.
    public static class SchemaMigration
    {
        public static void FromCards(MasterDraftProfile p, CarCatalog cars, int fromSchema = MasterDraftProfile.CardSchema)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var m = new ProfileMigration
            {
                fromSchema = Math.Max(MasterDraftProfile.CardSchema, fromSchema),
                migratedUtc = DateTime.UtcNow.ToString("o"),
                coinsBefore = p.coins,
            };

            var inTeam = TeamUids(p.team);
            foreach (var c in p.cards)
            {
                if (c?.def == null) continue;
                m.cardsConverted++;
                switch (c.def.rarity)
                {
                    case Rarity.Legendary: m.legendaries++; break;
                    case Rarity.Epic: m.epics++; break;
                    case Rarity.Rare: m.rares++; break;
                    default: m.commons++; break;
                }
                m.coinsFromCards += Economy.CardTradeIn(c.def.rarity);
                if (inTeam.Contains(c.uid)) m.teamCards++;
            }
            m.listingsCancelled = p.myListings?.Count ?? 0;

            p.coins = Math.Max(0, p.coins) + m.coinsFromCards;
            p.cards = new List<CardInstance>();
            p.team = new MasterDraftTeam();
            p.myListings = new List<TradeOffer>();
            p.botOffers = new List<TradeOffer>();
            p.starterClaimed = false;
            if (p.level < MasterDraftProfile.FirstLevel) p.level = MasterDraftProfile.FirstLevel;

            var starter = StarterCars.Ensure(p, cars);
            m.starterCar = starter ?? "";
            p.migration = m;
            p.schemaVersion = MasterDraftProfile.CurrentSchema;
        }

        static HashSet<string> TeamUids(MasterDraftTeam team)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (team == null) return set;
            foreach (var uid in new[] { team.activeDriver, team.car, team.crewChief })
                if (!string.IsNullOrEmpty(uid)) set.Add(uid);
            if (team.bench != null) foreach (var uid in team.bench) if (!string.IsNullOrEmpty(uid)) set.Add(uid);
            if (team.actionDeck != null) foreach (var uid in team.actionDeck) if (!string.IsNullOrEmpty(uid)) set.Add(uid);
            return set;
        }
    }

    // The car every player starts with. Draftmaster 2 handed new garages a set of free starters
    // (GarageUI.starterCars, git show 2217821d^:Assets/Scripts/UI/GarageUI.cs): Hocevar and Van Gisbergen ('24),
    // Gragson and Pastrana ('23), McLeod and Raikkonen ('22), McLeod ('20), Robb (IRL '23), Simpson (IRL '24) and the
    // DM1 '15 trio. Master Draft gives one — the first of these the catalogue holds — at class 0 with
    // CarProgression.StarterGears, like any car won outright.
    public static class StarterCars
    {
        public static readonly string[] Ids =
        {
            "cup24_77", "cup24_97", "cup23_42", "cup23_67", "cup22_78", "cup22_91", "cup20_52",
            "irl23_51", "irl24_4", "dmc15_0", "dmc15_1", "dmc15_2",
        };

        // The starter id: the first of Ids the catalogue holds; with no catalogue, Ids[0]; with a catalogue that
        // holds none of them, its lowest-rarity car (first in catalogue order). Null only for an empty catalogue.
        public static string Pick(CarCatalog cars)
        {
            if (cars == null) return Ids[0];
            foreach (var id in Ids) if (cars.Contains(id)) return id;
            CarDef best = null;
            foreach (var c in cars.Cars) if (best == null || c.rarity < best.rarity) best = c;
            return best?.id;
        }

        // Gives the starter if the garage is empty, and selects it. Returns the car given, or null when the garage
        // already had a car (or there is nothing to give).
        public static string Ensure(MasterDraftProfile p, CarCatalog cars)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            p.garage ??= new List<OwnedCar>();
            if (p.garage.Count > 0) return null;
            string id = Pick(cars);
            if (string.IsNullOrEmpty(id)) return null;
            CarProgression.AwardCar(p.garage, id);
            p.selectedCar = id;
            return id;
        }
    }
}
