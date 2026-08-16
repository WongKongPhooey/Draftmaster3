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

        // "Top 10 finish" / "just show up".
        public string ClauseText =>
            clausePosition <= 0 ? "no performance clause"
            : clausePosition == 1 ? $"win the race (+${clauseBonus:N0})"
            : $"finish top {clausePosition} (+${clauseBonus:N0})";
    }
}
