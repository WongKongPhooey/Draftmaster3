using System;
using Draftmaster.Weekend;
using NUnit.Framework;
using UnityEngine;

// Leaving the circuit before the weekend is done.
//
// A new career could walk into the RV on Friday morning, open the satnav and drive to Daytona with the whole
// Watkins Glen weekend unplayed. WeekendDeparture is the rule the travel map now asks: the road opens once the
// weekend is spent or the road has been called from the results screen, and not before.
//
// The ledger lives in PlayerPrefs, so the real save's key is put back after the run.
public class WeekendDepartureTests
{
    const string LedgerKey = "weekend.ledger";
    const int TestWeekend = 9175;

    string _ledgerBefore;
    bool _hadLedger;
    Action<int> _moneyHook;
    Action<string, int> _statHook;
    Action<string, float> _relationshipHook;

    [OneTimeSetUp]
    public void KeepTheSave()
    {
        _hadLedger = PlayerPrefs.HasKey(LedgerKey);
        _ledgerBefore = PlayerPrefs.GetString(LedgerKey, "");

        // Missed bookings would otherwise fine the real wallet through the runtime's hooks.
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
        WeekendLedger.EnsureWeekend(TestWeekend, RacingSeries.Cup);
        WeekendLedger.Timetable = WeekendTimetable.Build(RacingSeries.Cup, TestWeekend, "Test Speedway");
    }

    static void RunOutTheWeekend()
    {
        for (int guard = 0; guard < 16 && !WeekendLedger.WeekendOver; guard++) WeekendLedger.AdvanceSlot();
        Assert.IsTrue(WeekendLedger.WeekendOver);
    }

    [Test]
    public void FridayMorning_IsHeld()
    {
        Assert.IsFalse(WeekendDeparture.WeekendDone(TestWeekend, roadCalled: false));
    }

    [Test]
    public void SundayAfternoon_StillUnderway_IsHeld()
    {
        while (WeekendLedger.CurrentSlot < WeekendSlot.SundayPM) WeekendLedger.AdvanceSlot();
        Assert.IsFalse(WeekendLedger.WeekendOver);
        Assert.IsFalse(WeekendDeparture.WeekendDone(TestWeekend, roadCalled: false));
    }

    [Test]
    public void SpentWeekend_OpensTheRoad()
    {
        RunOutTheWeekend();
        Assert.IsTrue(WeekendDeparture.WeekendDone(TestWeekend, roadCalled: false));
    }

    [Test]
    public void RoadCalledFromResults_OpensTheRoad_MidWeekend()
    {
        Assert.IsTrue(WeekendDeparture.WeekendDone(TestWeekend, roadCalled: true));
    }

    [Test]
    public void LastWeekendsSpentSheet_DoesNotOpenTheRoadForThisOne()
    {
        // The ledger still holds a finished weekend, but the player has since arrived somewhere new and the
        // director has not started this weekend's sheet yet.
        RunOutTheWeekend();
        Assert.IsFalse(WeekendDeparture.WeekendDone(TestWeekend + 1, roadCalled: false));
    }
}
