using System;
using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Sponsors
{
    // The player's signed sponsorships and where each one sits on the car. PlayerPrefs-backed like the rest
    // of the live career state (PlayerWallet, PlayerStatsLedger, FanAppeal, CareerPath) rather than the
    // half-built SQLite career tables, so it works in the demo flow today.
    //
    // Two rules the whole feature hangs off:
    //   • a deal only earns while its decal is on a panel (SponsorDeal.PayoutFor),
    //   • one panel holds one decal, so signing more sponsors than you have panels forces a choice.
    public static class SponsorBook
    {
        const string Key = "sponsors.book";

        [Serializable]
        class Book
        {
            public List<SponsorDeal> deals = new();
            public int nextId = 1;
        }

        static Book _cache;

        static Book Data
        {
            get
            {
                if (_cache != null) return _cache;
                string json = PlayerPrefs.GetString(Key, "");
                _cache = string.IsNullOrEmpty(json) ? new Book() : JsonUtility.FromJson<Book>(json);
                if (_cache == null) _cache = new Book();
                if (_cache.deals == null) _cache.deals = new List<SponsorDeal>();
                return _cache;
            }
        }

        static void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // Raised whenever a deal is signed, moved or expires — the garage board and the car's paintwork
        // both listen so a decal appears the moment it is placed.
        public static event Action Changed;

        public static IReadOnlyList<SponsorDeal> Deals => Data.deals;

        public static int Count => Data.deals.Count;

        public static SponsorDeal ById(int id)
        {
            foreach (var d in Data.deals) if (d.id == id) return d;
            return null;
        }

        public static SponsorDeal InSlot(SponsorSlot slot)
        {
            if (slot == SponsorSlot.None) return null;
            foreach (var d in Data.deals) if (d.slot == slot) return d;
            return null;
        }

        public static bool HasSponsor(int sponsorId)
        {
            foreach (var d in Data.deals) if (d.sponsorId == sponsorId) return true;
            return false;
        }

        // Free panels, for the negotiation NPC to warn about ("you've nowhere to put it").
        public static int FreeSlots()
        {
            int free = 0;
            foreach (var slot in SponsorSlots.All) if (InSlot(slot) == null) free++;
            return free;
        }

        public static SponsorDeal Sign(SponsorDeal deal)
        {
            if (deal == null) return null;
            deal.id = Data.nextId++;
            if (deal.racesRemaining <= 0) deal.racesRemaining = Mathf.Max(1, deal.racesTotal);
            deal.slot = SponsorSlot.None;      // placement is a separate, deliberate act back at the garage
            Data.deals.Add(deal);
            Save();
            return deal;
        }

        // Put a deal on a panel. Whatever was on that panel comes off (one decal per panel), and the deal
        // leaves whichever panel it was on before, so dragging one sponsor onto another's panel swaps
        // cleanly instead of duplicating.
        public static void Place(int dealId, SponsorSlot slot)
        {
            var deal = ById(dealId);
            if (deal == null) return;

            if (slot != SponsorSlot.None)
            {
                var current = InSlot(slot);
                if (current != null && current.id != dealId) current.slot = SponsorSlot.None;
            }
            deal.slot = slot;
            Save();
        }

        public static void Unplace(int dealId) => Place(dealId, SponsorSlot.None);

        public static void Remove(int dealId)
        {
            var deal = ById(dealId);
            if (deal == null) return;
            Data.deals.Remove(deal);
            Save();
        }

        // Money earned from a race finished in `position` (1 = win, 0 = DNF). Pure — the caller banks it.
        public static int PayoutForFinish(int position)
        {
            int total = 0;
            foreach (var d in Data.deals) if (d.IsActive) total += d.PayoutFor(position);
            return total;
        }

        // Per-race income if every currently-placed deal is honoured, ignoring clauses. What the garage
        // board quotes as "on the car".
        public static int PerRaceIncome()
        {
            int total = 0;
            foreach (var d in Data.deals)
                if (d.IsActive && d.IsPlaced) total += (int)Math.Round(d.perRace * SponsorSlots.PayMultiplier(d.slot));
            return total;
        }

        // Burn one race off every live deal and drop the ones that run out. Unplaced deals burn too: sitting
        // on a contract you never painted on the car wastes it, which is the pressure that makes panels scarce.
        // Returns the deals that just expired, for the results screen to report.
        public static List<SponsorDeal> TickRace()
        {
            var expired = new List<SponsorDeal>();
            foreach (var d in Data.deals)
            {
                if (!d.IsActive) continue;
                d.racesRemaining--;
                if (d.racesRemaining <= 0) expired.Add(d);
            }
            foreach (var d in expired) Data.deals.Remove(d);
            Save();
            return expired;
        }

        public static void ClearAll()
        {
            _cache = new Book();
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // Tests and save-slot switching: drop the in-memory copy so the next read comes off PlayerPrefs.
        public static void InvalidateCache() => _cache = null;
    }
}
