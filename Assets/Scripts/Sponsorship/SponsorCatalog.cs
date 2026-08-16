using System.Collections.Generic;
using Draftmaster.Data;
using Draftmaster.Fans;
using Draftmaster.Sponsors;
using UnityEngine;

// Bridge between the SQLite Sponsors table (the brands that exist in the world) and the live game: who
// turns up at this weekend's track, what a deal with them looks like, and where their decal art lives.
//
// The Sponsors table is already seeded (DummySponsors, 12 brands with Wealth / Prestige / MinPrestige).
// Nothing here writes to it — a signed deal lands in SponsorBook, which is PlayerPrefs-backed like the
// rest of the live career.
public static class SponsorCatalog
{
    // Decal art naming lives in SponsorKeys, so the editor tools and the tests (which can't reference
    // Assembly-CSharp) resolve the same paths this does.
    public const string CarArtFolder = SponsorKeys.CarArtFolder;

    public static string LogoKey(string sponsorName) => SponsorKeys.LogoKey(sponsorName);

    static List<Sponsor> _all;

    public static IReadOnlyList<Sponsor> All()
    {
        if (_all != null) return _all;
        _all = new List<Sponsor>();
        var db = DatabaseManager.Instance;
        if (db == null || !db.IsReady || db.Connection == null) return _all;   // no DB yet — caller retries
        try { _all = new List<Sponsor>(db.Connection.Table<Sponsor>()); }
        catch (System.Exception e) { Debug.LogWarning($"SponsorCatalog: could not read the Sponsors table — {e.Message}"); }
        return _all;
    }

    public static void Invalidate() => _all = null;

    public static Sponsor ById(int id)
    {
        foreach (var s in All()) if (s.Id == id) return s;
        return null;
    }

    // The player's standing, judged against Sponsor.MinPrestige. FanAppeal is the live 0-100 reputation the
    // running game maintains (it moves on autographs and results), so it stands in for team prestige until
    // the career tables are wired up.
    public static float PlayerStanding => FanAppeal.Value;

    // Which brands have sent a rep to this weekend's track. Deterministic per (track, weekend) so the same
    // faces are there all weekend, and different rounds put different sponsors in the pit lane. Brands the
    // player has already signed are skipped; ones above their standing are NOT — the rep still turns up and
    // tells them what they'd need, which is how the player learns what to aim at.
    public static List<Sponsor> RepsForWeekend(string trackId, int weekendId, int count)
    {
        var pool = new List<Sponsor>();
        foreach (var s in All())
            if (!SponsorBook.HasSponsor(s.Id)) pool.Add(s);
        if (pool.Count == 0 || count <= 0) return new List<Sponsor>();

        // Stable shuffle from the weekend seed: same track + same weekend = same reps, every reload.
        int seed = (trackId != null ? trackId.GetHashCode() : 0) * 397 ^ (weekendId * 7919 + 13);
        var rng = new System.Random(seed);
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        // Weight the pick toward brands the player could plausibly land: sort the shuffled pool so anything
        // within reach comes first, then take the top N. A stretch target still slips in when the reachable
        // list is short, which keeps the aspiration visible.
        float standing = PlayerStanding;
        pool.Sort((a, b) =>
        {
            bool aOk = SponsorTerms.CanApproach(standing, a.MinPrestige);
            bool bOk = SponsorTerms.CanApproach(standing, b.MinPrestige);
            if (aOk != bOk) return aOk ? -1 : 1;
            return 0;   // List.Sort is unstable, but the pre-shuffle already randomised within each group
        });

        if (count > pool.Count) count = pool.Count;
        return pool.GetRange(0, count);
    }

    // The terms a brand opens with for the player as they stand today.
    public static SponsorTerms.Offer OpeningOffer(Sponsor sponsor) =>
        SponsorTerms.Open(sponsor.Wealth, sponsor.Prestige, PlayerStanding, sponsor.MinPrestige);

    public static int Ceiling(Sponsor sponsor) =>
        SponsorTerms.CeilingValue(sponsor.Wealth, PlayerStanding, sponsor.MinPrestige);

    // Turn agreed terms into the deal that gets written to the book.
    public static SponsorDeal BuildDeal(Sponsor sponsor, SponsorTerms.Offer offer) => new SponsorDeal
    {
        sponsorId = sponsor.Id,
        sponsorName = sponsor.Name,
        logoKey = LogoKey(sponsor.Name),
        perRace = offer.perRace,
        racesTotal = offer.races,
        racesRemaining = offer.races,
        clausePosition = offer.clausePosition,
        clauseBonus = offer.clauseBonus,
    };
}
