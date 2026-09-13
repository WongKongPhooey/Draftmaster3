using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Sponsors
{
    // A brand that comes looking for YOU.
    //
    // Every other sponsorship in the game is found by walking up to a rep stood in the pit lane and
    // haggling with them (SponsorRepNPC / SponsorTerms). A poach is the other direction: somebody who has
    // watched the driver work a room decides they want them off whoever is currently on the car, walks
    // over, and hands them finished terms. There is nothing to haggle about, because the whole pitch is
    // that the number already beats what they are on — so the only two answers are yes and no.
    //
    // Pure, like SponsorTerms: the NPC who speaks the lines and the popup that shows the paper own no
    // rules of their own, and the numbers are testable without standing a paddock up.
    public static class SponsorPoach
    {
        // The reputation a brand has to see before it bothers. Deliberately LOW: a fresh save sits at
        // FanAppeal.Default (40) and the beat has to be able to fire on the demo's first weekend, or
        // nobody ever sees it. Raise it when the career's reputation curve is real.
        public const float AppealRequired = 15f;

        // What they want for the money: results, not exposure.
        public const int TargetPosition = 5;
        public const int TargetCount = 2;

        // How far clear of the money already on the car they come, so the offer is worth the paperwork.
        public const float BeatBy = 1.25f;

        public static bool Qualifies(float standing) => Qualifies(standing, AppealRequired);

        public static bool Qualifies(float standing, float required) => standing >= required;

        // The best per-race rate the driver is already on, at full slot value — the number a poacher has
        // to beat. 0 with an empty book, which is its own kind of easy sell.
        public static int BestRateOnTheBooks(IReadOnlyList<SponsorDeal> deals)
        {
            int best = 0;
            if (deals == null) return 0;
            for (int i = 0; i < deals.Count; i++)
            {
                var d = deals[i];
                if (d == null || !d.IsActive) continue;
                if (d.perRace > best) best = d.perRace;
            }
            return best;
        }

        // Their terms: their own opening number, lifted clear of whatever is already on the car.
        //
        // The per-race clause a pit-lane rep would attach is dropped, because the contract-long target is
        // this deal's clause — two bonuses on one sheet of paper reads as a menu rather than an offer.
        public static SponsorTerms.Offer Offer(int wealth, int prestige, float standing, int minPrestige, int beat)
        {
            var offer = SponsorTerms.Open(wealth, prestige, standing, minPrestige);
            int floor = Mathf.RoundToInt(Mathf.Max(0, beat) * BeatBy);
            if (offer.perRace < floor) offer.perRace = floor;
            offer.clausePosition = 0;
            offer.clauseBonus = 0;
            return offer;
        }

        // The lump on the end of it: one race's money for each finish they asked for. Paid once, so it has
        // to read as worth chasing without dwarfing the purse a race win pays (PlayerWallet, $12,000).
        public static int TargetBonus(int perRace, int count) =>
            Mathf.Max(0, perRace) * Mathf.Max(1, count);

        // Finished terms, ready to sign or turn down.
        public static SponsorDeal Deal(int sponsorId, string sponsorName, string logoKey,
                                       SponsorTerms.Offer offer) => new SponsorDeal
        {
            sponsorId = sponsorId,
            sponsorName = sponsorName,
            logoKey = logoKey,
            perRace = offer.perRace,
            racesTotal = offer.races,
            racesRemaining = offer.races,
            clausePosition = offer.clausePosition,
            clauseBonus = offer.clauseBonus,
            targetPosition = TargetPosition,
            targetCount = TargetCount,
            targetBonus = TargetBonus(offer.perRace, TargetCount),
        };
    }
}
