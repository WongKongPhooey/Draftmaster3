using System;
using Draftmaster.Weekend;
using NUnit.Framework;
using UnityEngine;

// Skipping ahead off the schedule sheet.
//
// SKIP TO on the sheet gives up the rest of the half-day and sends the player back to their motorhome with a
// summary of what it cost. These pin the pure half of that (WeekendSkip): what counts as skipped, that the
// optional bookings it walks past now cost something from the meter they belong to, and that the report's
// numbers are what the ledger actually moved.
//
// The ledger and fan appeal live in PlayerPrefs, so the real save's keys are put back after the run.
public class WeekendSkipTests
{
    const string LedgerKey = "weekend.ledger";
    const string AppealKey = "fan.appeal";
    const int TestWeekend = 9174;

    string _ledgerBefore;
    bool _hadLedger, _hadAppeal;
    float _appealBefore;
    Action<int> _moneyHook;
    Action<string, int> _statHook;
    Action<string, float> _relationshipHook;

    [OneTimeSetUp]
    public void KeepTheSave()
    {
        _hadLedger = PlayerPrefs.HasKey(LedgerKey);
        _ledgerBefore = PlayerPrefs.GetString(LedgerKey, "");
        _hadAppeal = PlayerPrefs.HasKey(AppealKey);
        _appealBefore = PlayerPrefs.GetFloat(AppealKey, 0f);

        // The no-show fines would otherwise reach the real wallet through the runtime's hooks.
        _moneyHook = WeekendLedger.MoneyHook;
        _statHook = WeekendLedger.StatHook;
        _relationshipHook = WeekendLedger.RelationshipHook;
        WeekendLedger.MoneyHook = null;
        WeekendLedger.StatHook = null;
        WeekendLedger.RelationshipHook = null;
    }

    [OneTimeTearDown]
    public void PutTheSaveBack()
    {
        if (_hadLedger) PlayerPrefs.SetString(LedgerKey, _ledgerBefore); else PlayerPrefs.DeleteKey(LedgerKey);
        if (_hadAppeal) PlayerPrefs.SetFloat(AppealKey, _appealBefore); else PlayerPrefs.DeleteKey(AppealKey);
        PlayerPrefs.Save();
        WeekendLedger.Timetable = null;
        WeekendLedger.InvalidateCache();
        WeekendLedger.MoneyHook = _moneyHook;
        WeekendLedger.StatHook = _statHook;
        WeekendLedger.RelationshipHook = _relationshipHook;
    }

    [SetUp]
    public void FreshWeekend()
    {
        WeekendLedger.Timetable = null;
        WeekendLedger.ClearAll();
        WeekendLedger.EnsureWeekend(TestWeekend, RacingSeries.Trucks);
        WeekendLedger.Timetable = WeekendTimetable.Build(RacingSeries.Trucks, TestWeekend, "Test Speedway");
        Draftmaster.Fans.FanAppeal.Value = 50f;
    }

    static void FinishTheHalfDay()
    {
        var slot = WeekendLedger.CurrentSlot;
        for (int guard = 64; guard > 0; guard--)
        {
            var next = WeekendSchedulePlan.NextWorthDoing();
            if (next == null || next.slot != slot) return;
            Assert.IsTrue(WeekendLedger.Complete(next, WeekendOutcome.Nothing), $"could not complete {next.id}");
        }
        Assert.Fail("half-day never ran out of things to do");
    }

    [Test]
    public void Skip_MovesToTheNextHalfDay_AndListsWhatItWalkedPast()
    {
        var report = WeekendSkip.SkipHalfDay();

        Assert.IsNotNull(report);
        Assert.AreEqual(WeekendSlot.FridayAM, report.from);
        Assert.AreEqual(WeekendSlot.FridayPM, report.to);
        Assert.AreEqual(WeekendSlot.FridayPM, WeekendLedger.CurrentSlot);
        Assert.IsFalse(report.endedWeekend);
        Assume.That(report.SkippedAnything, "the sheet has nothing on Friday morning");

        foreach (var a in report.skipped)
        {
            Assert.AreEqual(WeekendSlot.FridayAM, a.slot, $"{a.id} is not from the half-day given up");
            Assert.IsTrue(WeekendLedger.IsMissed(a.id), $"{a.id} listed as skipped but not missed");
            Assert.AreNotEqual(ActivityKind.Rest, a.kind, "an hour off is not something skipped");
        }
    }

    [Test]
    public void Skip_CostsWhatTheSkippedBookingsAreWorth()
    {
        // Independently: the optional bookings' tolls, plus the mandatory ones' own no-show penalties.
        float team = 0f, sponsor = 0f, media = 0f;
        foreach (var a in WeekendLedger.Timetable.InSlot(WeekendSlot.FridayAM))
        {
            if (a.kind == ActivityKind.Rest) continue;
            if (!a.mandatory)
            {
                var toll = WeekendSkip.TollFor(a.kind);
                team += toll.teamMorale; sponsor += toll.sponsorMood; media += toll.mediaStanding;
            }
            else if (ActivityKinds.IsSponsorDuty(a.kind)) sponsor -= 18f;
            else if (ActivityKinds.IsMedia(a.kind)) media -= 12f;
            else if (ActivityKinds.IsTeam(a.kind)) team -= 12f;
        }
        Assume.That(team + sponsor + media < 0f, "nothing on Friday morning costs anything to skip");

        var report = WeekendSkip.SkipHalfDay();

        Assert.AreEqual(team, report.cost.teamMorale, 0.01f);
        Assert.AreEqual(sponsor, report.cost.sponsorMood, 0.01f);
        Assert.AreEqual(media, report.cost.mediaStanding, 0.01f);
        Assert.AreEqual(team, WeekendLedger.TeamMorale, 0.01f, "the report is what the ledger moved");
        Assert.AreEqual(sponsor, WeekendLedger.SponsorMood, 0.01f);
        Assert.AreEqual(media, WeekendLedger.MediaStanding, 0.01f);
    }

    [Test]
    public void Skip_AfterDoingEverything_CostsNothing()
    {
        FinishTheHalfDay();
        if (WeekendLedger.CurrentSlot != WeekendSlot.FridayAM) Assert.Ignore("Friday morning rolled on by itself");

        var report = WeekendSkip.SkipHalfDay();

        Assert.IsFalse(report.SkippedAnything);
        Assert.AreEqual(0, report.cost.money);
        Assert.AreEqual(0f, report.cost.fanAppeal, 0.001f);
        Assert.AreEqual(0f, report.cost.teamMorale, 0.001f);
        Assert.AreEqual(0f, report.cost.sponsorMood, 0.001f);
        Assert.AreEqual(0f, report.cost.mediaStanding, 0.001f);
    }

    [Test]
    public void Skip_OffTheEndOfSunday_EndsTheWeekend_AndThenDoesNothing()
    {
        while (WeekendLedger.CurrentSlot < WeekendSlot.SundayPM) WeekendLedger.AdvanceSlot();

        var report = WeekendSkip.SkipHalfDay();
        Assert.IsNotNull(report);
        Assert.IsTrue(report.endedWeekend);
        Assert.IsTrue(WeekendLedger.WeekendOver);

        Assert.IsNull(WeekendSkip.SkipHalfDay(), "nothing left to skip");
    }

    [Test]
    public void Tolls_TakeFromTheMeterTheBookingBelongsTo()
    {
        Assert.Less(WeekendSkip.TollFor(ActivityKind.Practice).teamMorale, 0f);
        Assert.Less(WeekendSkip.TollFor(ActivityKind.Qualifying).teamMorale, 0f);
        Assert.Less(WeekendSkip.TollFor(ActivityKind.Debrief).teamMorale, 0f);
        Assert.Less(WeekendSkip.TollFor(ActivityKind.PressConference).mediaStanding, 0f);
        Assert.Less(WeekendSkip.TollFor(ActivityKind.MediaHit).mediaStanding, 0f);
        Assert.Less(WeekendSkip.TollFor(ActivityKind.Autographs).fanAppeal, 0f);
        Assert.Less(WeekendSkip.TollFor(ActivityKind.SponsorDuty).sponsorMood, 0f);
        Assert.Less(WeekendSkip.TollFor(ActivityKind.PhotoShoot).sponsorMood, 0f);

        foreach (var free in new[] { ActivityKind.Rest, ActivityKind.SpectatePractice, ActivityKind.SpectateRace })
        {
            var o = WeekendSkip.TollFor(free);
            Assert.AreEqual(0f, o.teamMorale + o.sponsorMood + o.mediaStanding + o.fanAppeal, 0.001f, free.ToString());
        }
    }

    // ------------------------------------------------------------------ the phone's SKIP TO HERE

    static int Pos(WeekendActivity a) => (int)a.slot * 1440 + a.startMinute;
    static int Clock => (int)WeekendLedger.CurrentSlot * 1440 + WeekendLedger.ClockMinute;

    static WeekendActivity Mine(ActivityKind k) => WeekendLedger.Timetable.PlayerSession(k);

    [Test]
    public void SkipTo_WillNotGoPastYourOwnPractice()
    {
        var practice = Mine(ActivityKind.Practice);
        var race = Mine(ActivityKind.Race);
        Assert.IsFalse(WeekendSkip.CanSkipTo(race, out string why));
        StringAssert.Contains(practice.title, why);
        Assert.IsNull(WeekendSkip.SkipToActivity(race), "a refused skip must not move the clock");
        Assert.AreEqual(WeekendSlot.FridayAM, WeekendLedger.CurrentSlot);
    }

    [Test]
    public void SkipTo_YourOwnSession_LandsOnItsStart_AndLeavesWhatCameBefore()
    {
        var practice = Mine(ActivityKind.Practice);
        Assume.That(Pos(practice) > Clock, "practice opens the weekend, so there is nothing to skip to");

        Assert.IsTrue(WeekendSkip.CanSkipTo(practice, out string why), why);
        var report = WeekendSkip.SkipToActivity(practice);

        Assert.IsNotNull(report);
        Assert.AreEqual(practice.slot, WeekendLedger.CurrentSlot);
        Assert.AreEqual(practice.startMinute, WeekendLedger.ClockMinute);
        Assert.AreEqual(WeekendLedger.State.Available, WeekendLedger.Status(practice), "the session skipped to is still there to drive");

        foreach (var a in WeekendLedger.Timetable.Activities)
            if (Pos(a) < Pos(practice) && a.kind != ActivityKind.Rest)
                Assert.IsTrue(WeekendLedger.IsMissed(a.id), $"{a} was walked past and should be missed");
    }

    [Test]
    public void SkipTo_PastASessionYouHaveDriven_IsAllowed_AndNeverBackwards()
    {
        var practice = Mine(ActivityKind.Practice);
        var qualifying = Mine(ActivityKind.Qualifying);
        if (Pos(practice) > Clock) WeekendSkip.SkipToActivity(practice);
        Assert.IsTrue(WeekendLedger.Complete(practice, WeekendOutcome.Nothing));

        Assert.IsTrue(WeekendSkip.CanSkipTo(qualifying, out string why), why);
        Assert.IsNotNull(WeekendSkip.SkipToActivity(qualifying));

        Assert.IsFalse(WeekendSkip.CanSkipTo(practice, out _), "practice is done and behind the clock");
        foreach (var a in WeekendLedger.Timetable.Activities)
            if (Pos(a) < Pos(qualifying)) Assert.IsFalse(WeekendSkip.CanSkipTo(a, out _), $"{a} is behind the clock");
    }

    [Test]
    public void SkipTo_WhatIsOnNow_IsRefused()
    {
        WeekendActivity now = null;
        foreach (var a in WeekendLedger.Timetable.Activities)
            if (Pos(a) == Clock) { now = a; break; }
        Assume.That(now != null, "nothing starts on the opening minute");
        Assert.IsFalse(WeekendSkip.CanSkipTo(now, out string why));
        StringAssert.Contains("on now", why);
    }
}
