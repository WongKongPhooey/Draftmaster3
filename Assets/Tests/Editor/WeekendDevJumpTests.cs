using System;
using System.Linq;
using Draftmaster.Weekend;
using NUnit.Framework;
using UnityEngine;

// The debug panel's JUMP TO (WeekendLedger.DevJumpTo): moving the weekend straight to any booking, forwards or
// back, as though everything before it had been attended — and charging nothing for it.
//
// The ledger and fan appeal live in PlayerPrefs, so the real save's keys are put back after the run.
public class WeekendDevJumpTests
{
    const string LedgerKey = "weekend.ledger";
    const string AppealKey = "fan.appeal";
    const int TestWeekend = 9175;

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

    static int Position(WeekendActivity a) => (int)a.slot * 24 * 60 + a.startMinute;

    // The player's own race: the last thing worth jumping to, with almost the whole weekend in front of it.
    static WeekendActivity TheRace() =>
        WeekendLedger.Timetable.Activities.First(a => a.kind == ActivityKind.Race);

    [Test]
    public void JumpForward_AttendsEverythingBefore_AndChargesNothing()
    {
        var race = TheRace();
        float sponsor = WeekendLedger.SponsorMood, team = WeekendLedger.TeamMorale, media = WeekendLedger.MediaStanding;

        WeekendLedger.DevJumpTo(race);

        Assert.AreEqual(race.slot, WeekendLedger.CurrentSlot);
        Assert.AreEqual(race.startMinute, WeekendLedger.ClockMinute);
        Assert.IsTrue(WeekendLedger.CanDo(race, out string why), why);

        foreach (var a in WeekendLedger.Timetable.Activities)
        {
            if (Position(a) < Position(race)) Assert.IsTrue(WeekendLedger.IsDone(a.id), $"{a.id} not attended");
            else Assert.IsFalse(WeekendLedger.IsDone(a.id) || WeekendLedger.IsMissed(a.id), $"{a.id} not open");
        }

        Assert.AreEqual(0, WeekendLedger.MissedCount);
        Assert.AreEqual(0, WeekendLedger.Fines);
        Assert.AreEqual(sponsor, WeekendLedger.SponsorMood);
        Assert.AreEqual(team, WeekendLedger.TeamMorale);
        Assert.AreEqual(media, WeekendLedger.MediaStanding);
        Assert.AreEqual(50f, Draftmaster.Fans.FanAppeal.Value, 0.001f);
    }

    [Test]
    public void JumpBack_ReopensEverythingFromTheTarget()
    {
        WeekendLedger.DevJumpTo(TheRace());

        var first = WeekendLedger.Timetable.Activities.OrderBy(Position).First();
        WeekendLedger.DevJumpTo(first);

        Assert.AreEqual(first.slot, WeekendLedger.CurrentSlot);
        Assert.AreEqual(first.startMinute, WeekendLedger.ClockMinute);
        Assert.AreEqual(0, WeekendLedger.MissedCount);
        foreach (var a in WeekendLedger.Timetable.Activities)
            if (Position(a) >= Position(first)) Assert.IsFalse(WeekendLedger.IsDone(a.id), $"{a.id} still done");
        Assert.IsTrue(WeekendLedger.CanDo(first, out string why), why);
    }

    [Test]
    public void JumpToNull_EndsTheWeekendWithEverythingAttended()
    {
        WeekendLedger.DevJumpTo(null);

        Assert.IsTrue(WeekendLedger.WeekendOver);
        Assert.AreEqual(0, WeekendLedger.MissedCount);
        Assert.AreEqual(WeekendLedger.Timetable.Activities.Count, WeekendLedger.DoneCount);
    }
}
