using System.Linq;
using Draftmaster.Weekend;
using NUnit.Framework;
using UnityEngine;

// Scouting: racing your own series uncovers a rival's stats fast enough that the field is fully known by the
// fifth weekend; watching another series' sessions uncovers them more slowly. And the weekend sheet: the
// hauler parade is gone, and somebody else's practice and qualifying are optional — never routed to, never
// a no-show.
public class DriverScoutingTests
{
    // DriverAttributeSheet.All's labels, in its order. Copied rather than read so the rules are tested on
    // their own (the sheet lives in the game assembly).
    static readonly string[] Labels =
    {
        "ABILITY", "POTENTIAL",
        "SHORT TRACKS", "SPEEDWAYS", "SUPERSPEEDWAY", "ROAD COURSES", "DIRT COURSES", "OPEN WHEEL",
        "SPONSOR APPEAL", "FAN SUPPORT", "PRESTIGE",
        "QUALIFYING", "CONSISTENCY", "AGGRESSION", "AWARENESS", "ADAPTABILITY", "TYRE MGMT", "FUEL MGMT",
    };

    static int OwnWeekend(int mask, int seed)
    {
        mask = DriverScouting.Reveal(mask, Labels, ActivityKind.Practice, "ROAD COURSES",
                                     DriverScouting.RevealsFor(ActivityKind.Practice), seed);
        mask = DriverScouting.Reveal(mask, Labels, ActivityKind.Qualifying, "ROAD COURSES",
                                     DriverScouting.RevealsFor(ActivityKind.Qualifying), seed);
        return DriverScouting.Reveal(mask, Labels, ActivityKind.Race, "ROAD COURSES",
                                     DriverScouting.RevealsFor(ActivityKind.Race), seed);
    }

    [Test]
    public void YourOwnFieldIsFullyKnownOnTheFifthWeekendAndNotBefore()
    {
        int seed = DriverScouting.Seed("Kyle Larson");
        int mask = 0;
        for (int w = 1; w <= 4; w++) mask = OwnWeekend(mask, seed);
        Assert.IsFalse(DriverScouting.AllKnown(mask, Labels.Length), "four weekends should leave something unknown");
        mask = OwnWeekend(mask, seed);
        Assert.IsTrue(DriverScouting.AllKnown(mask, Labels.Length), "the fifth weekend should finish the sheet");
    }

    [Test]
    public void WatchingIsSlowerThanRacing()
    {
        int seed = DriverScouting.Seed("Jimmy Karras");
        int watched = 0;
        watched = DriverScouting.Reveal(watched, Labels, ActivityKind.SpectatePractice, null,
                                        DriverScouting.RevealsFor(ActivityKind.SpectatePractice), seed);
        watched = DriverScouting.Reveal(watched, Labels, ActivityKind.SpectateQualifying, null,
                                        DriverScouting.RevealsFor(ActivityKind.SpectateQualifying), seed);
        int raced = OwnWeekend(0, seed);
        Assert.Less(DriverScouting.KnownCount(watched, Labels.Length), DriverScouting.KnownCount(raced, Labels.Length));
        Assert.Greater(DriverScouting.KnownCount(watched, Labels.Length), 0);
    }

    [Test]
    public void ASessionShowsWhatItWouldShowFirst()
    {
        int q = DriverScouting.Reveal(0, Labels, ActivityKind.Qualifying, "SPEEDWAYS", 1, 7);
        Assert.IsTrue(DriverScouting.Known(q, System.Array.IndexOf(Labels, "QUALIFYING")));

        int p = DriverScouting.Reveal(0, Labels, ActivityKind.SpectatePractice, "SHORT TRACKS", 1, 7);
        Assert.IsTrue(DriverScouting.Known(p, System.Array.IndexOf(Labels, "SHORT TRACKS")));
    }

    [Test]
    public void NothingIsRevealedTwiceAndTheOrderIsStable()
    {
        int seed = DriverScouting.Seed("Junior Kemp");
        int a = 0, b = 0;
        for (int i = 0; i < 6; i++)
        {
            a = DriverScouting.Reveal(a, Labels, ActivityKind.SpectateRace, null, 1, seed);
            b = DriverScouting.Reveal(b, Labels, ActivityKind.SpectateRace, null, 1, seed);
        }
        Assert.AreEqual(a, b);
        Assert.AreEqual(6, DriverScouting.KnownCount(a, Labels.Length));
        Assert.AreEqual(DriverScouting.Seed("junior kemp"), DriverScouting.Seed("JUNIOR KEMP"));
    }

    [Test]
    public void ObligationsUncoverNothing()
    {
        Assert.AreEqual(0, DriverScouting.RevealsFor(ActivityKind.PressConference));
        Assert.AreEqual(0, DriverScouting.RevealsFor(ActivityKind.TeamBriefing));
    }

    // ---------------------------------------------------------------- the sheet

    [Test]
    public void TheHaulerParadeIsNeverScheduled()
    {
        foreach (RacingSeries s in SeriesCatalog.All)
            for (int w = 1; w <= 12; w++)
            {
                var t = WeekendTimetable.Build(s, 5000 + w, "Test Speedway");
                Assert.IsFalse(t.Activities.Any(a => a.kind == ActivityKind.HaulerParade), $"{s} weekend {w}");
            }
    }

    [Test]
    public void OtherSeriesPracticeAndQualifyingAreOptional()
    {
        var t = WeekendTimetable.Build(RacingSeries.Cup, 5100, "Test Speedway");
        var optional = t.Activities.Where(a => ActivityKinds.IsOptional(a.kind)).ToList();
        Assert.IsNotEmpty(optional, "the other two series' practice and qualifying should still be on the sheet");
        foreach (var a in optional)
        {
            Assert.IsFalse(a.mandatory, a.ToString());
            Assert.AreEqual(0, a.skipMoneyPenalty, a.ToString());
            Assert.AreEqual(0f, a.skipAppealPenalty, a.ToString());
        }
        Assert.IsFalse(ActivityKinds.IsOptional(ActivityKind.SpectateRace));
        Assert.IsFalse(ActivityKinds.IsOptional(ActivityKind.Practice));
    }

    [Test]
    public void TheWeekendNeverRoutesYouToOptionalWatching()
    {
        string before = PlayerPrefs.GetString("weekend.ledger", "");
        bool had = PlayerPrefs.HasKey("weekend.ledger");
        try
        {
            WeekendLedger.ClearAll();
            WeekendLedger.EnsureWeekend(5200, RacingSeries.Cup);
            WeekendLedger.Timetable = WeekendTimetable.Build(RacingSeries.Cup, 5200, "Test Speedway");

            // Walk the whole weekend by always doing whatever the router hands over.
            for (int guard = 0; guard < 200 && !WeekendLedger.WeekendOver; guard++)
            {
                var next = WeekendSchedulePlan.NextWorthDoing();
                if (next == null) { WeekendLedger.AdvanceSlot(); continue; }
                Assert.IsFalse(ActivityKinds.IsOptional(next.kind), "routed to " + next);
                WeekendLedger.Complete(next, WeekendOutcome.Nothing);
            }
        }
        finally
        {
            WeekendLedger.Timetable = null;
            if (had) PlayerPrefs.SetString("weekend.ledger", before); else PlayerPrefs.DeleteKey("weekend.ledger");
            PlayerPrefs.Save();
            WeekendLedger.InvalidateCache();
        }
    }
}
