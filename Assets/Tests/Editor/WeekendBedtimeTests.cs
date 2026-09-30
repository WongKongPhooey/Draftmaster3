using Draftmaster.Weekend;
using NUnit.Framework;
using UnityEngine;

// Friday and Saturday end in bed.
//
// The last obligation of a Friday or Saturday afternoon no longer rolls the sheet over to the next morning;
// the weekend waits until the player sleeps in their motorhome. These pin the rule (which evenings, and only
// once nothing is left) and that sleeping is what opens the next morning's clock.
//
// The ledger and fan appeal live in PlayerPrefs, so the real save's keys are put back after the run.
public class WeekendBedtimeTests
{
    const string LedgerKey = "weekend.ledger";
    const string AppealKey = "fan.appeal";
    const int TestWeekend = 9173;

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
        WeekendLedger.EnsureWeekend(TestWeekend, RacingSeries.Trucks);
        WeekendLedger.Timetable = WeekendTimetable.Build(RacingSeries.Trucks, TestWeekend, "Test Speedway");
    }

    // Do everything left in the current half-day, earliest first, the way the chain would book it.
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

    static void GoTo(WeekendSlot slot)
    {
        while (WeekendLedger.CurrentSlot < slot) WeekendLedger.AdvanceSlot();
        Assert.AreEqual(slot, WeekendLedger.CurrentSlot);
    }

    [Test]
    public void OnlyFridayAndSaturdayEveningsEndInBed()
    {
        Assert.IsFalse(WeekendBedtime.EndsInBed(WeekendSlot.FridayAM));
        Assert.IsTrue(WeekendBedtime.EndsInBed(WeekendSlot.FridayPM));
        Assert.IsFalse(WeekendBedtime.EndsInBed(WeekendSlot.SaturdayAM));
        Assert.IsTrue(WeekendBedtime.EndsInBed(WeekendSlot.SaturdayPM));
        Assert.IsFalse(WeekendBedtime.EndsInBed(WeekendSlot.SundayAM));
        Assert.IsFalse(WeekendBedtime.EndsInBed(WeekendSlot.SundayPM), "Sunday night is the end of the weekend");
    }

    [Test]
    public void NotBedtime_WhileTheEveningStillHasSomethingOn()
    {
        GoTo(WeekendSlot.FridayPM);
        Assume.That(WeekendSchedulePlan.NextWorthDoing(), Is.Not.Null, "the sheet has nothing on Friday afternoon");
        Assert.IsFalse(WeekendBedtime.Due());
        Assert.IsFalse(WeekendBedtime.Sleep(), "cannot sleep through obligations");
        Assert.AreEqual(WeekendSlot.FridayPM, WeekendLedger.CurrentSlot);
    }

    [Test]
    public void FridayEvening_FinishedMeansBedtime_AndSleepingOpensSaturdayMorning()
    {
        GoTo(WeekendSlot.FridayPM);
        FinishTheHalfDay();

        Assert.IsTrue(WeekendBedtime.Due(), "Friday evening done: the bed is next");
        Assert.IsTrue(WeekendBedtime.Sleep());
        Assert.AreEqual(WeekendSlot.SaturdayAM, WeekendLedger.CurrentSlot);
        Assert.AreEqual(WeekendSlots.OpensAt(WeekendSlot.SaturdayAM), WeekendLedger.ClockMinute,
                        "wake up as the morning opens");
        Assert.IsFalse(WeekendBedtime.Due(), "a fresh morning is not bedtime");
        Assert.IsFalse(WeekendBedtime.Sleep(), "sleeping twice does not skip Saturday");
    }

    [Test]
    public void SaturdayEvening_FinishedMeansBedtime_AndSleepingOpensSundayMorning()
    {
        GoTo(WeekendSlot.SaturdayPM);
        FinishTheHalfDay();

        Assert.IsTrue(WeekendBedtime.Due());
        Assert.IsTrue(WeekendBedtime.Sleep());
        Assert.AreEqual(WeekendSlot.SundayAM, WeekendLedger.CurrentSlot);
    }

    [Test]
    public void MorningsAndSundayNight_NeverWaitOnTheBed()
    {
        FinishTheHalfDay();   // Friday morning
        Assert.IsFalse(WeekendBedtime.Due(), "lunchtime rolls on by itself");

        GoTo(WeekendSlot.SundayPM);
        FinishTheHalfDay();
        Assert.IsFalse(WeekendBedtime.Due(), "Sunday night ends the weekend rather than a night's sleep");
    }

    [Test]
    public void NoSheet_NoBedtime()
    {
        GoTo(WeekendSlot.FridayPM);
        WeekendLedger.Timetable = null;
        Assert.IsFalse(WeekendBedtime.Due());
    }
}
