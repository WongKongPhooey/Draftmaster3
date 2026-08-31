using Draftmaster.Weekend;
using NUnit.Framework;
using UnityEngine;

// Waking up with nothing booked, and being told the day by a person.
//
// The rule is small and the consequences are not: get it wrong in one direction and the demo opens with an
// objective the player was never told about, get it wrong in the other and a weekend can start with no
// marker at all and no way to get one. Both failures only show up several minutes into a play-through, so
// they are pinned here instead.
public class WeekendBriefingTests
{
    const string Key = "weekend.briefed";
    int _was;

    [SetUp]
    public void Save() => _was = PlayerPrefs.GetInt(Key, -1);

    [TearDown]
    public void Restore()
    {
        if (_was < 0) PlayerPrefs.DeleteKey(Key);
        else PlayerPrefs.SetInt(Key, _was);
        PlayerPrefs.Save();
    }

    [Test]
    public void TheFirstMorningOfAWeekendWaitsToBeTold()
    {
        Assert.IsTrue(WeekendBriefing.WaitingToBeTold(briefed: false, routed: false, weekendUnderway: false,
                                                      atTheVenue: true, giverComing: true),
                      "Waking up in the paddock with somebody on their way over should book nothing.");
    }

    [Test]
    public void OnceToldTheWeekendBooksForItselfAgain()
    {
        Assert.IsFalse(WeekendBriefing.WaitingToBeTold(briefed: true, routed: false, weekendUnderway: false,
                                                       atTheVenue: true, giverComing: true),
                       "The day has been handed over; the next booking should follow on by itself.");
    }

    [Test]
    public void NobodyComingMeansNobodyWaits()
    {
        Assert.IsFalse(WeekendBriefing.WaitingToBeTold(briefed: false, routed: false, weekendUnderway: false,
                                                       atTheVenue: true, giverComing: false),
                       "A track whose cast has no liaison must not leave the player with no objective at all.");
    }

    [Test]
    public void AWeekendAlreadyUnderwayIsNotAMorning()
    {
        Assert.IsFalse(WeekendBriefing.WaitingToBeTold(briefed: false, routed: false, weekendUnderway: true,
                                                       atTheVenue: true, giverComing: true),
                       "Something has already been done today — the driver has been up for hours.");
    }

    [Test]
    public void ComingBackToDriveDoesNotWaitOnAnyone()
    {
        Assert.IsFalse(WeekendBriefing.WaitingToBeTold(briefed: false, routed: true, weekendUnderway: false,
                                                       atTheVenue: true, giverComing: true),
                       "A routed session is the player arriving for something already booked.");
    }

    [Test]
    public void AwayFromTheTrackNothingIsHandedOver()
    {
        Assert.IsFalse(WeekendBriefing.WaitingToBeTold(briefed: false, routed: false, weekendUnderway: false,
                                                       atTheVenue: false, giverComing: true),
                       "The title screen and the garage have no paddock to be told about.");
    }

    // The memory is per weekend id, not a single flag: next weekend starts in the dark again.
    [Test]
    public void BeingToldIsRememberedForThatWeekendOnly()
    {
        WeekendBriefing.Forget();
        Assert.IsFalse(WeekendBriefing.Briefed(7));

        WeekendBriefing.MarkBriefed(7);
        Assert.IsTrue(WeekendBriefing.Briefed(7), "The weekend that was handed over should stay handed over.");
        Assert.IsFalse(WeekendBriefing.Briefed(8), "The next weekend is a new morning.");
    }
}
