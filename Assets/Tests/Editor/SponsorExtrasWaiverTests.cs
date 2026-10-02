using System.Linq;
using Draftmaster.Weekend;
using NUnit.Framework;
using UnityEngine;

// "All of it. I'm here to drive the car."
//
// Telling the team manager at the strategy briefing that you want none of the sponsor's weekend cancels the
// extras — the photo shoots and the suite meet-and-greet come off the sheet — and costs far more sponsor mood
// than the other two answers. These pin which bookings go, that they stay gone across a rebuild of the sheet,
// that nothing else goes with them, and that the next weekend starts with them back.
//
// The ledger and fan appeal live in PlayerPrefs, so the real save's keys are put back after the run.
public class SponsorExtrasWaiverTests
{
    const string LedgerKey = "weekend.ledger";
    const string AppealKey = "fan.appeal";
    const int TestWeekend = 9241;
    const string Track = "Test Speedway";
    const RacingSeries Series = RacingSeries.Cup;

    string _ledgerBefore;
    bool _hadLedger, _hadAppeal;
    float _appealBefore;

    [OneTimeSetUp]
    public void KeepTheSave()
    {
        _hadLedger = PlayerPrefs.HasKey(LedgerKey);
        _ledgerBefore = PlayerPrefs.GetString(LedgerKey, "");
        _hadAppeal = PlayerPrefs.HasKey(AppealKey);
        _appealBefore = PlayerPrefs.GetFloat(AppealKey, 0f);
    }

    [OneTimeTearDown]
    public void PutTheSaveBack()
    {
        if (_hadLedger) PlayerPrefs.SetString(LedgerKey, _ledgerBefore); else PlayerPrefs.DeleteKey(LedgerKey);
        if (_hadAppeal) PlayerPrefs.SetFloat(AppealKey, _appealBefore); else PlayerPrefs.DeleteKey(AppealKey);
        PlayerPrefs.Save();
        WeekendLedger.Timetable = null;
        WeekendLedger.InvalidateCache();
    }

    [SetUp]
    public void FreshWeekend()
    {
        WeekendLedger.Timetable = null;
        WeekendLedger.ClearAll();
        WeekendLedger.EnsureWeekend(TestWeekend, Series);
        WeekendLedger.Timetable = WeekendTimetable.Build(Series, TestWeekend, Track);
    }

    // The sponsor beat of the strategy briefing, and the answer to it that starts with `text`.
    static WeekendChoice SponsorAnswer(string text)
    {
        var briefing = WeekendLedger.Timetable.FirstOfKind(ActivityKind.TeamBriefing);
        var talk = TeamMeetingContent.Build(briefing);
        var beat = talk.beats.Single(b => b.speaker == "TEAM MANAGER");
        return beat.choices.Single(c => c.text.StartsWith(text));
    }

    // Sit the briefing and give `text` as the answer to the sponsor question.
    static void SitBriefing(string text)
    {
        var briefing = WeekendLedger.Timetable.FirstOfKind(ActivityKind.TeamBriefing);
        var talk = TeamMeetingContent.Build(briefing);
        var running = new WeekendOutcome();
        WeekendConversation.Accumulate(ref running, talk.beats[0].choices[0]);
        WeekendConversation.Accumulate(ref running, SponsorAnswer(text));
        Assert.IsTrue(WeekendLedger.Complete(briefing, talk.Settle(running, talk.beats.Count)),
            "the briefing should be doable first thing on Friday");
    }

    static bool OnSheet(string title) => WeekendLedger.Timetable.Activities.Any(a => a.title == title);

    [Test]
    public void TheExtras_AreThePhotoShootsAndTheSuite()
    {
        var extras = WeekendLedger.Timetable.Activities.Where(a => a.sponsorExtra).ToList();
        Assert.IsTrue(extras.Any(a => a.title == "SPONSOR PHOTO SHOOT"));
        Assert.IsTrue(extras.Any(a => a.title == "SUITE MEET & GREET"));
        Assert.IsTrue(extras.All(a => ActivityKinds.IsSponsorDuty(a.kind)), "only sponsor bookings are extras");
        Assert.IsFalse(extras.Any(a => a.title == "PIT-STOP CHALLENGE"), "contracted appearances are not extras");
    }

    [Test]
    public void AvoidingTheSponsor_TakesTheExtrasOffTheSheet()
    {
        Assert.IsTrue(OnSheet("SPONSOR PHOTO SHOOT"));
        Assert.IsTrue(OnSheet("SUITE MEET & GREET"));
        int before = WeekendLedger.Timetable.Activities.Count;
        int extras = WeekendLedger.Timetable.Activities.Count(a => a.sponsorExtra);

        SitBriefing("All of it.");

        Assert.IsTrue(WeekendLedger.SponsorExtrasWaived);
        Assert.IsFalse(OnSheet("SPONSOR PHOTO SHOOT"));
        Assert.IsFalse(OnSheet("DEALER GROUP PHOTOS"));
        Assert.IsFalse(OnSheet("SUITE MEET & GREET"));
        Assert.IsTrue(OnSheet("PIT-STOP CHALLENGE"), "the contracted appearances still stand");
        Assert.IsTrue(OnSheet("HOSPITALITY Q&A"));
        Assert.AreEqual(before - extras, WeekendLedger.Timetable.Activities.Count, "nothing else came off");
    }

    [Test]
    public void AvoidingTheSponsor_IsABigHitToSponsorMood()
    {
        var avoid = SponsorAnswer("All of it.");
        Assert.IsTrue(avoid.waivesSponsorExtras);
        Assert.LessOrEqual(avoid.sponsor, -30f, "cancelling the brand's weekend is a big hit, not a shrug");

        SitBriefing("All of it.");
        Assert.LessOrEqual(WeekendLedger.SponsorMood, -25f);
    }

    [TestCase("None.")]
    [TestCase("Keep the ones")]
    public void TheOtherAnswers_LeaveTheSheetAlone(string text)
    {
        Assert.IsFalse(SponsorAnswer(text).waivesSponsorExtras);
        int before = WeekendLedger.Timetable.Activities.Count;

        SitBriefing(text);

        Assert.IsFalse(WeekendLedger.SponsorExtrasWaived);
        Assert.AreEqual(before, WeekendLedger.Timetable.Activities.Count);
        Assert.IsTrue(OnSheet("SUITE MEET & GREET"));
    }

    [Test]
    public void TheWaiver_SurvivesARebuildOfTheSheet_AndNotTheNextWeekend()
    {
        SitBriefing("All of it.");

        // The scene reloads between sessions and the sheet is built afresh; the runtime re-applies the waiver.
        var rebuilt = WeekendTimetable.Build(Series, TestWeekend, Track);
        Assert.IsTrue(rebuilt.Activities.Any(a => a.title == "SUITE MEET & GREET"), "the build itself is pure");
        rebuilt.WaiveSponsorExtras();
        Assert.IsFalse(rebuilt.Activities.Any(a => a.sponsorExtra));

        WeekendLedger.EnsureWeekend(TestWeekend + 1, Series);
        Assert.IsFalse(WeekendLedger.SponsorExtrasWaived, "a new weekend books the sponsor's extras again");
    }

    [Test]
    public void AnExtraAlreadyAttended_StaysOnTheSheet()
    {
        var shoot = WeekendLedger.Timetable.Activities.First(a => a.title == "SPONSOR PHOTO SHOOT");
        WeekendLedger.SkipTo(shoot.startMinute);
        Assert.IsTrue(WeekendLedger.Complete(shoot, WeekendOutcome.Nothing));

        WeekendLedger.Timetable.WaiveSponsorExtras();

        Assert.IsTrue(WeekendLedger.Timetable.Activities.Contains(shoot),
            "a photo shoot that already happened is not cancelled after the fact");
        Assert.IsFalse(OnSheet("DEALER GROUP PHOTOS"), "the ones still to come do go");
    }

    [Test]
    public void AnAuthoredPlan_FlagsItsPhotoShootsToo()
    {
        var authored = WeekendTimetable.Build(RacingSeries.Cup, 3, "WatkinsGlen");
        Assume.That(authored.authored, "needs the Watkins Glen Cup plan file");
        Assert.IsTrue(authored.Activities.Where(a => a.kind == ActivityKind.PhotoShoot).All(a => a.sponsorExtra));
    }
}
