using System.Collections.Generic;
using System.Linq;
using Draftmaster.Tracks;
using Draftmaster.Weekend;
using NUnit.Framework;

// The demo's four venues share the weekend's quests out between them — one a day — instead of the opener
// running every one of them and each round after repeating the lot (WeekendTimetable.BuildRoundQuests).
// The sessions, the team meetings and the race-day ceremony stay on every round.
public class DemoRoundQuestsTests
{
    const int Weekend = 5;

    // A quest is a media, fan or sponsor booking — everything that is not a session, a team meeting or the
    // officials' race-day ceremony.
    static bool IsQuest(WeekendActivity a) =>
        !a.IsOnTrack && !a.IsSpectate && !ActivityKinds.IsTeam(a.kind) && !ActivityKinds.IsCeremony(a.kind);

    static List<WeekendActivity> Quests(WeekendTimetable t) => t.Activities.Where(IsQuest).ToList();

    static WeekendTimetable Round(int round, RacingSeries series = RacingSeries.Cup) =>
        WeekendTimetable.Build(series, Weekend, DemoCalendar.Rounds[round], round);

    [Test]
    public void EveryDemoVenue_HasItsOwnShareOfTheQuests()
    {
        Assert.AreEqual(DemoCalendar.Rounds.Count, WeekendTimetable.DemoRounds,
                        "every demo round needs a quest list, and only demo rounds have one");
        CollectionAssert.AreEqual(new[] { "WatkinsGlen", "Daytona", "FortWorth", "Martinsville" }, DemoCalendar.Rounds);
    }

    [Test]
    public void CupRound_HasOneQuestPerDay()
    {
        for (int round = 0; round < WeekendTimetable.DemoRounds; round++)
        {
            var quests = Quests(Round(round));
            Assert.AreEqual(3, quests.Count, $"round {round}: {string.Join(", ", quests)}");
            foreach (var day in new[] { WeekendSlot.FridayAM, WeekendSlot.SaturdayAM, WeekendSlot.SundayAM })
                Assert.AreEqual(1, quests.Count(q => WeekendSlots.MorningOf(q.slot) == day),
                                $"round {round}: expected one quest on {WeekendSlots.DaySpoken(day)}");
        }
    }

    [Test]
    public void NoQuestRepeatsAcrossTheDemo()
    {
        var titles = new List<string>();
        for (int round = 0; round < WeekendTimetable.DemoRounds; round++)
            titles.AddRange(Quests(Round(round)).Select(q => q.title));
        Assert.AreEqual(titles.Count, titles.Distinct().Count(), string.Join(", ", titles));
        Assert.AreEqual(12, titles.Count);
    }

    [Test]
    public void EveryRound_HasOneSponsorDutyAndOneMediaAppearance()
    {
        for (int round = 0; round < WeekendTimetable.DemoRounds; round++)
        {
            var quests = Quests(Round(round));
            Assert.AreEqual(1, quests.Count(q => ActivityKinds.IsSponsorDuty(q.kind) || q.kind == ActivityKind.PitChallenge),
                            $"round {round}: {string.Join(", ", quests)}");
            Assert.GreaterOrEqual(quests.Count(q => ActivityKinds.IsMedia(q.kind)), 1,
                                  $"round {round}: {string.Join(", ", quests)}");
        }
    }

    [Test]
    public void SessionsMeetingsAndCeremony_StayOnEveryRound()
    {
        foreach (var series in SeriesCatalog.All)
            for (int round = 0; round < WeekendTimetable.DemoRounds; round++)
            {
                var t = Round(round, series);
                Assert.AreEqual(3, t.Activities.Count(a => a.IsOnTrack), $"{series} round {round}: own sessions");
                Assert.AreEqual(6, t.Activities.Count(a => a.IsSpectate), $"{series} round {round}: watchable sessions");
                Assert.IsNotNull(t.FirstOfKind(ActivityKind.TeamBriefing), $"{series} round {round}: briefing");
                Assert.IsNotNull(t.FirstOfKind(ActivityKind.Debrief), $"{series} round {round}: debrief");
                Assert.IsNotNull(t.FirstOfKind(ActivityKind.RacePlan), $"{series} round {round}: race plan");
                Assert.IsNotNull(t.FirstOfKind(ActivityKind.DriverIntros), $"{series} round {round}: intros");
                Assert.AreEqual(3, Quests(t).Count, $"{series} round {round}: {string.Join(", ", Quests(t))}");
            }
    }

    // The day's one quest is not a straight choice against the car for the demo's Cup driver.
    [Test]
    public void CupQuests_DoNotClashWithTheDriversOwnWeekend()
    {
        for (int round = 0; round < WeekendTimetable.DemoRounds; round++)
        {
            var t = Round(round);
            foreach (var q in Quests(t))
                foreach (var other in t.ClashesFor(q))
                    Assert.IsTrue(other.IsSpectate,
                                  $"round {round}: {q} clashes with {other}");
        }
    }

    [Test]
    public void OffCalendarTrack_KeepsTheWholeGeneratedWeekend()
    {
        var generated = WeekendTimetable.Build(RacingSeries.Cup, Weekend, "Bristol");
        foreach (int notARound in new[] { -1, WeekendTimetable.DemoRounds })
        {
            var t = WeekendTimetable.Build(RacingSeries.Cup, Weekend, "Bristol", notARound);
            Assert.AreEqual(-1, t.demoRound);
            CollectionAssert.AreEqual(generated.Activities.Select(a => a.id).ToList(),
                                      t.Activities.Select(a => a.id).ToList());
        }
        Assert.Greater(Quests(generated).Count, 3, "the generated weekend still carries every quest");
    }

    [Test]
    public void DemoRound_IsDeterministic()
    {
        for (int round = 0; round < WeekendTimetable.DemoRounds; round++)
            CollectionAssert.AreEqual(Round(round).Activities.Select(a => a.id).ToList(),
                                      Round(round).Activities.Select(a => a.id).ToList());
    }
}
