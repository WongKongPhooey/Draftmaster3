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
        const string StarterKey = "sponsors.starterGiven";

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
            foreach (var d in Data.deals) if (Overlaps(d.slot, slot)) return d;
            return null;
        }

        // Two slots share bodywork: Quarters covers both single quarter panels (deals placed before the
        // Paint Scheme Designer sold the pair as one slot still sit on just one of them).
        static bool Overlaps(SponsorSlot a, SponsorSlot b)
        {
            if (a == SponsorSlot.None || b == SponsorSlot.None) return false;
            if (a == b) return true;
            foreach (var pa in SponsorSlots.Panels(a))
                foreach (var pb in SponsorSlots.Panels(b))
                    if (pa == pb) return true;
            return false;
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
                foreach (var other in Data.deals)
                    if (other.id != dealId && Overlaps(other.slot, slot)) other.slot = SponsorSlot.None;
            }
            deal.slot = slot;
            Save();
        }

        public static void Unplace(int dealId) => Place(dealId, SponsorSlot.None);

        // Lay the whole car out at once (the Paint Scheme Designer's SAVE): every deal comes off, then each
        // slot gets the deal named for it (0 = leave it empty). One save, one Changed, so the car repaints once.
        public static void Arrange(IDictionary<SponsorSlot, int> dealForSlot)
        {
            foreach (var d in Data.deals) d.slot = SponsorSlot.None;
            if (dealForSlot != null)
                foreach (var kv in dealForSlot)
                {
                    var deal = kv.Value != 0 ? ById(kv.Value) : null;
                    if (deal != null && kv.Key != SponsorSlot.None) deal.slot = kv.Key;
                }
            Save();
        }

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

        // Bank the player's finish against every live deal's contract-long target, and return the money
        // that just came due. Almost every race returns 0 — a target pays once, on the race it is met.
        //
        // Call this BEFORE TickRace, while the race just run still counts as one of the deal's races.
        public static int SettleTargets(int position)
        {
            int paid = 0;
            bool changed = false;
            foreach (var d in Data.deals)
            {
                if (d == null) continue;
                int before = d.targetProgress;
                paid += d.RecordFinish(position);
                if (d.targetProgress != before) changed = true;
            }
            if (changed) Save();
            return paid;
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

        // The deal a new career starts with, already on the car. Given once per career: the flag lives in
        // PlayerPrefs, so CareerReset (DeleteAll) and ClearAll both make the next career get it again.
        // Placed on `slot` only if that panel is free, so an old save that never had it doesn't lose a deal.
        public static bool GiveStarter(SponsorDeal deal, SponsorSlot slot)
        {
            if (deal == null || PlayerPrefs.GetInt(StarterKey, 0) == 1) return false;
            PlayerPrefs.SetInt(StarterKey, 1);

            if (deal.sponsorId == 0 || !HasSponsor(deal.sponsorId))
            {
                deal.id = Data.nextId++;
                if (deal.racesRemaining <= 0) deal.racesRemaining = Mathf.Max(1, deal.racesTotal);
                deal.slot = slot != SponsorSlot.None && InSlot(slot) == null ? slot : SponsorSlot.None;
                Data.deals.Add(deal);
            }
            Save();
            return true;
        }

        public static void ClearAll()
        {
            _cache = new Book();
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.DeleteKey(StarterKey);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // Tests and save-slot switching: drop the in-memory copy so the next read comes off PlayerPrefs.
        public static void InvalidateCache() => _cache = null;
    }
}
