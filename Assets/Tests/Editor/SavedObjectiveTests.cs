using Draftmaster.Weekend;
using NUnit.Framework;
using UnityEngine;

// Save Anytime: the objective marker that was up at the save is the one CONTINUE brings back.
//
// Pinned here: when the saved booking is put back (only into an empty slot, only in the weekend it was saved
// in, only while the sheet can still do it), and that the record round-trips through PlayerPrefs
// (SavedObjective, pure).
public class SavedObjectiveTests
{
    string _id;
    int _weekend;
    bool _hadId, _hadWeekend;

    [SetUp]
    public void Stash()
    {
        _hadId = PlayerPrefs.HasKey("save.objective.id");
        _hadWeekend = PlayerPrefs.HasKey("save.objective.weekend");
        _id = SavedObjective.Id;
        _weekend = SavedObjective.WeekendId;
    }

    [TearDown]
    public void PutBack()
    {
        SavedObjective.Forget();
        if (_hadId || _hadWeekend) SavedObjective.Write(_weekend, _id);
    }

    [Test]
    public void ASavedBooking_IsRestored_IntoAnEmptySlotInTheSameWeekend()
    {
        Assert.IsTrue(SavedObjective.ShouldRestore(7, "cup.practice1", 7, "", true));
    }

    [Test]
    public void ALiveBooking_IsNewerThanTheSave_AndStands()
    {
        Assert.IsFalse(SavedObjective.ShouldRestore(7, "cup.practice1", 7, "cup.qualifying", true),
                       "A booking already on the map was overwritten by an older save.");
    }

    [Test]
    public void ASaveFromAnotherWeekend_IsNeverPulledForward()
    {
        Assert.IsFalse(SavedObjective.ShouldRestore(6, "cup.practice1", 7, "", true));
    }

    [Test]
    public void ABookingTheSheetCanNoLongerDo_IsNotRestored()
    {
        Assert.IsFalse(SavedObjective.ShouldRestore(7, "cup.practice1", 7, "", false),
                       "A done, missed or past booking would point the player at the past.");
    }

    [Test]
    public void ASaveWithNothingBooked_RestoresNothing()
    {
        Assert.IsFalse(SavedObjective.ShouldRestore(7, "", 7, "", true));
        Assert.IsFalse(SavedObjective.ShouldRestore(7, null, 7, "", true));
    }

    [Test]
    public void TheRecord_RoundTrips_AndForgets()
    {
        SavedObjective.Write(12, "media.press");
        Assert.AreEqual("media.press", SavedObjective.Id);
        Assert.AreEqual(12, SavedObjective.WeekendId);

        SavedObjective.Write(12, null);
        Assert.AreEqual("", SavedObjective.Id, "Saving with nothing booked should record nothing, not null.");

        SavedObjective.Forget();
        Assert.AreEqual("", SavedObjective.Id);
        Assert.AreEqual(-1, SavedObjective.WeekendId);
    }
}
