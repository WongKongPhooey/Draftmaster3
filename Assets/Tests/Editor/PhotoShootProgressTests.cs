using System.Linq;
using Draftmaster.Weekend;
using NUnit.Framework;
using UnityEngine;

// The rival brand's rep outside the winner's circle (SponsorPoachBeat) only comes once a sponsor photo shoot
// has been done. A career mode that takes the shoots off the sheet must read as a shoot that never happens —
// not as a weekend that never had one, which sent the rep straight to the player at the RV.
//
// The ledger lives in PlayerPrefs, so the real save's key is put back after the run.
public class PhotoShootProgressTests
{
    const string LedgerKey = "weekend.ledger";
    const int TestWeekend = 9242;
    const string Track = "Test Speedway";
    const RacingSeries Series = RacingSeries.Cup;

    string _ledgerBefore;
    bool _hadLedger;

    [OneTimeSetUp]
    public void KeepTheSave()
    {
        _hadLedger = PlayerPrefs.HasKey(LedgerKey);
        _ledgerBefore = PlayerPrefs.GetString(LedgerKey, "");
    }

    [OneTimeTearDown]
    public void PutTheSaveBack()
    {
        if (_hadLedger) PlayerPrefs.SetString(LedgerKey, _ledgerBefore); else PlayerPrefs.DeleteKey(LedgerKey);
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

    static WeekendTimetable Sheet(CareerMode mode)
    {
        var t = WeekendTimetable.Build(Series, TestWeekend, Track);
        t.ApplyCareerMode(mode);
        WeekendLedger.Timetable = t;
        return t;
    }

    [Test]
    public void FullCareer_WaitsForTheShoot()
    {
        var t = Sheet(CareerMode.Full);
        Assert.IsTrue(t.Activities.Any(a => a.kind == ActivityKind.PhotoShoot), "Full career books a shoot.");
        Assert.AreEqual(PhotoShootState.Waiting, PhotoShootProgress.Of(t, false, CareerMode.Full));
    }

    [TestCase(CareerMode.DrivingOnly)]
    [TestCase(CareerMode.Minimal)]
    public void CareerModeWithoutTheShoot_NeverSendsTheRep(CareerMode mode)
    {
        var t = Sheet(mode);
        Assert.IsFalse(t.Activities.Any(a => a.kind == ActivityKind.PhotoShoot), $"{mode} leaves the shoot off.");
        Assert.AreEqual(PhotoShootState.NeverHappening, PhotoShootProgress.Of(t, false, mode));
    }

    [Test]
    public void ShootTurnedDownAtTheMeeting_NeverSendsTheRep()
    {
        var t = Sheet(CareerMode.Full);
        t.WaiveSponsorExtras();
        Assert.AreEqual(PhotoShootState.NeverHappening, PhotoShootProgress.Of(t, true, CareerMode.Full));
    }
}
