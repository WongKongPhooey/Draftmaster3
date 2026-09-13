using System;

namespace Draftmaster.Sponsors
{
    // One signed sponsorship, denormalised so the deal survives without the sponsor database being open
    // (the catalogue lives in SQLite; a save has to reload on a cold start regardless).
    //
    // Serialized through JsonUtility into PlayerPrefs by SponsorBook, matching how the rest of the live
    // career persists (PlayerWallet, PlayerStatsLedger, FanAppeal).
    [Serializable]
    public class SponsorDeal
    {
        public int id;                 // unique within the book, assigned on signing
        public int sponsorId;          // Sponsors table row this came from (0 if unknown)
        public string sponsorName;     // "Voltage Energy"
        public string logoKey;         // Resources/Sponsors/Car/<logoKey> decal art

        public int perRace;            // money per race at a full-value slot (the hood)
        public int racesTotal;         // length of the deal in races
        public int racesRemaining;     // ticks down every race entered, placed or not

        public int clausePosition;     // finish this position or better to earn the bonus (0 = no clause)
        public int clauseBonus;        // money added on a race that meets the clause

        // A target that runs the LENGTH of the contract instead of paying race by race: "finish top 5 at
        // least twice before this runs out". Paid once, on the race the count is reached.
        //
        // A brand that comes looking for you (SponsorPoach) is buying results rather than exposure, and
        // this is how the paper says so — the per-race clause above is what a brand you walked up to in
        // the pit lane offers instead.
        public int targetPosition;     // finish this position or better...
        public int targetCount;        // ...this many times over the contract (0 = no target at all)
        public int targetBonus;        // the lump, paid once
        public int targetProgress;     // qualifying finishes banked so far
        public bool targetPaid;        // already collected; it can never pay twice

        public SponsorSlot slot = SponsorSlot.None;   // where it currently sits on the car

        public bool IsActive => racesRemaining > 0;
        public bool IsPlaced => slot != SponsorSlot.None;

        // What this deal pays for a race finished in `position` (1 = win, 0 = DNF/unclassified).
        // Nothing at all unless the decal is actually on the car — signing is only half the job.
        public int PayoutFor(int position)
        {
            if (!IsPlaced || perRace <= 0) return 0;
            float mult = SponsorSlots.PayMultiplier(slot);
            int pay = (int)System.Math.Round(perRace * mult);
            if (clausePosition > 0 && position > 0 && position <= clausePosition) pay += clauseBonus;
            return pay;
        }

        public bool HasTarget => targetCount > 0 && targetPosition > 0 && targetBonus > 0;

        // Bank one race against the contract target and return the money that just came due (0 on almost
        // every race). Only counts while the decal is actually on the car — the one rule the whole feature
        // hangs off applies to the target as much as to the per-race money.
        //
        // Call this BEFORE the deal's race counter is burned, while the race just run still belongs to it.
        public int RecordFinish(int position)
        {
            if (!HasTarget || targetPaid || !IsActive || !IsPlaced) return 0;
            if (position <= 0 || position > targetPosition) return 0;

            targetProgress++;
            if (targetProgress < targetCount) return 0;
            targetPaid = true;
            return targetBonus;
        }

        // "finish top 5 at least twice during the contract (+$8,572)".
        public string TargetText => !HasTarget
            ? "no contract target"
            : $"finish top {targetPosition} at least {Times(targetCount)} during the contract (+${targetBonus:N0})";

        static string Times(int n) => n <= 1 ? "once" : n == 2 ? "twice" : $"{n} times";

        // "Top 10 finish" / "just show up".
        public string ClauseText =>
            clausePosition <= 0 ? "no performance clause"
            : clausePosition == 1 ? $"win the race (+${clauseBonus:N0})"
            : $"finish top {clausePosition} (+${clauseBonus:N0})";
    }
}
