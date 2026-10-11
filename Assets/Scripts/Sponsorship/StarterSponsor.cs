using System.Collections.Generic;
using Draftmaster.Data;
using Draftmaster.Sponsors;
using UnityEngine;

// The sponsor a new career starts with: Aces & Spades on the hood of Ace Emerson's #89. Signed the first time
// the brand catalogue loads in a career (SponsorCatalog.All), so it is on the car from the first scene that
// paints one. SponsorBook keeps the once-per-career flag; CareerReset clears it with everything else.
public static class StarterSponsor
{
    public const string BrandName = "Aces & Spades";
    const int SeasonOfRaces = 36;      // a full Cup season — it shouldn't fall off the car mid-demo

    public static void EnsureGiven(IReadOnlyList<Sponsor> catalogue)
    {
        if (catalogue == null) return;
        Sponsor brand = null;
        foreach (var s in catalogue) if (s.Name == BrandName) { brand = s; break; }
        if (brand == null) return;     // DB older than the brand — the additive seed adds it next launch

        var deal = SponsorCatalog.BuildDeal(brand, new SponsorTerms.Offer
        {
            perRace = SponsorTerms.BaseValue(brand.Wealth),
            races = SeasonOfRaces,
        });
        if (SponsorBook.GiveStarter(deal, SponsorSlot.Hood))
            Debug.Log($"[Sponsors] New career: {BrandName} signed onto the hood (${deal.perRace:N0}/race, {SeasonOfRaces} races).");
    }
}
