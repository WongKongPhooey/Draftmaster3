using System.Linq;
using Draftmaster.Weekend;
using NUnit.Framework;
using UnityEngine;

// OPTIONS > CAREER MODE: Full plays the whole weekend, Minimal keeps the player's sessions and the team
// meetings that shape the car, Driving Only keeps the sessions alone. These pin what each mode leaves on the
// sheet, that what has already happened stays, and that nothing left behind waits on a removed booking.
//
// The ledger and the mode live in PlayerPrefs, so the real save's keys are put back after the run.
public class CareerModeTests
{
    const string LedgerKey = "weekend.ledger";
    const int TestWeekend = 9377;
    const string Track = "Test Speedway";
    const RacingSeries Series = RacingSeries.Cup;

    string _ledgerBefore;
    bool _hadLedger, _hadMode;
    int _modeBefore;

    [OneTimeSetUp]
    public void KeepTheSave()
    {
        _hadLedger = PlayerPrefs.HasKey(LedgerKey);
        _ledgerBefore = PlayerPrefs.GetString(LedgerKey, "");
        _hadMode = PlayerPrefs.HasKey(CareerModes.PrefKey);
        _modeBefore = PlayerPrefs.GetInt(CareerModes.PrefKey, 0);
    }

    [OneTimeTearDown]
    public void PutTheSaveBack()
    {
        if (_hadLedger) PlayerPrefs.SetString(LedgerKey, _ledgerBefore); else PlayerPrefs.DeleteKey(LedgerKey);
        if (_hadMode) PlayerPrefs.SetInt(CareerModes.PrefKey, _modeBefore); else PlayerPrefs.DeleteKey(CareerModes.PrefKey);
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
    }

    static WeekendTimetable Sheet() => WeekendTimetable.Build(Series, TestWeekend, Track);

    [Test]
    public void FullLeavesTheSheetAlone()
    {
        var t = Sheet();
        int before = t.Activities.Count;
        Assert.AreEqual(0, t.ApplyCareerMode(CareerMode.Full));
        Assert.AreEqual(before, t.Activities.Count);
    }

    [Test]
    public void DrivingOnlyKeepsJustThePlayersThreeSessions()
    {
        var t = Sheet();
        t.ApplyCareerMode(CareerMode.DrivingOnly);

        Assert.IsTrue(t.Activities.All(a => a.IsOnTrack),
                      "Left on the sheet: " + string.Join(", ", t.Activities.Where(a => !a.IsOnTrack)));
        Assert.IsNotNull(t.PlayerSession(ActivityKind.Practice));
        Assert.IsNotNull(t.PlayerSession(ActivityKind.Qualifying));
        Assert.IsNotNull(t.PlayerSession(ActivityKind.Race));
    }

    [Test]
    public void MinimalKeepsTheSessionsAndTheStrategyMeetingAndNothingElse()
    {
        var t = Sheet();
        Assert.IsNotNull(t.FirstOfKind(ActivityKind.TeamBriefing), "the generated weekend should have a strategy briefing");
        t.ApplyCareerMode(CareerMode.Minimal);

        Assert.IsNotNull(t.FirstOfKind(ActivityKind.TeamBriefing), "Minimal must keep the strategy meeting");
        Assert.IsNotNull(t.PlayerSession(ActivityKind.Race));
        foreach (var a in t.Activities)
            Assert.IsTrue(a.IsOnTrack || ActivityKinds.IsTeam(a.kind), $"Minimal left {a} on the sheet");
    }

    [Test]
    public void WhatHasAlreadyHappenedStays()
    {
        var t = Sheet();
        WeekendLedger.Timetable = t;
        var duty = t.Activities.First(a => !CareerModes.Keeps(CareerMode.DrivingOnly, a.kind));
        WeekendLedger.Complete(duty, WeekendOutcome.Nothing);

        t.ApplyCareerMode(CareerMode.DrivingOnly);
        Assert.IsNotNull(t.ById(duty.id), "an attended booking is part of the weekend that happened");
    }

    [Test]
    public void NothingLeftWaitsOnARemovedBooking()
    {
        var t = Sheet();
        var meeting = t.FirstOfKind(ActivityKind.TeamBriefing);
        var race = t.PlayerSession(ActivityKind.Race);
        race.requiresId = meeting.id;

        t.ApplyCareerMode(CareerMode.DrivingOnly);
        Assert.IsNull(t.ById(meeting.id));
        Assert.AreEqual("", race.requiresId);
    }

    [Test]
    public void TheSettingRoundTripsAndCycles()
    {
        CareerModes.Current = CareerMode.Minimal;
        Assert.AreEqual(CareerMode.Minimal, CareerModes.Current);
        CareerModes.Current = CareerMode.DrivingOnly;
        Assert.AreEqual(CareerMode.DrivingOnly, CareerModes.Current);

        Assert.AreEqual(CareerMode.Full, CareerModes.Next(CareerMode.DrivingOnly));
        Assert.AreEqual(CareerMode.DrivingOnly, CareerModes.Previous(CareerMode.Full));

        PlayerPrefs.SetInt(CareerModes.PrefKey, 42);
        Assert.AreEqual(CareerMode.Full, CareerModes.Current, "a value from nowhere reads as Full");
    }
}
