using System.Collections.Generic;
using Draftmaster.Weekend;
using NUnit.Framework;

// Fast travel is locked until the career's first team meeting is over, then taught once with a prompt.
//
// Pinned here: which bookings unlock it, which career modes lock it at all, that a meeting gone by unattended
// unlocks it as well as one attended, and when the prompt is allowed up (FastTravelUnlock, pure).
public class FastTravelUnlockTests
{
    // ------------------------------------------------------------------ what unlocks it

    [Test]
    public void TheTeamMeetings_Unlock_AndNothingElseDoes()
    {
        Assert.IsTrue(FastTravelUnlock.Unlocks(ActivityKind.TeamBriefing), "The strategy briefing should unlock fast travel.");
        Assert.IsTrue(FastTravelUnlock.Unlocks(ActivityKind.Debrief), "The practice debrief is a team meeting too.");
        Assert.IsFalse(FastTravelUnlock.Unlocks(ActivityKind.Orientation), "The orientation is not a team meeting.");
        Assert.IsFalse(FastTravelUnlock.Unlocks(ActivityKind.Practice), "Driving is not a team meeting.");
        Assert.IsFalse(FastTravelUnlock.Unlocks(ActivityKind.PressConference), "Media is not a team meeting.");
    }

    // ------------------------------------------------------------------ when it is locked

    [Test]
    public void ANewCareer_IsLocked_UntilAMeetingIsDone()
    {
        Assert.IsTrue(FastTravelUnlock.Locked(false, CareerMode.Full, false), "Fast travel was open before the first meeting.");
        Assert.IsFalse(FastTravelUnlock.Locked(true, CareerMode.Full, false), "Fast travel stayed locked after the meeting.");
    }

    [Test]
    public void MinimalMode_HasTheMeetings_SoItLocksToo()
    {
        Assert.IsTrue(FastTravelUnlock.Locked(false, CareerMode.Minimal, false));
    }

    [Test]
    public void DrivingOnly_HasNoMeetings_SoItIsNeverLocked()
    {
        Assert.IsFalse(FastTravelUnlock.Locked(false, CareerMode.DrivingOnly, false),
            "A mode with no team meetings on the sheet would never unlock fast travel.");
    }

    [Test]
    public void ACoopGuest_IsNeverLocked()
    {
        Assert.IsFalse(FastTravelUnlock.Locked(false, CareerMode.Full, true));
    }

    // ------------------------------------------------------------------ settled on the sheet

    static List<WeekendActivity> Sheet() => new List<WeekendActivity>
    {
        new WeekendActivity { id = "briefing", kind = ActivityKind.TeamBriefing },
        new WeekendActivity { id = "orientation", kind = ActivityKind.Orientation },
        new WeekendActivity { id = "practice", kind = ActivityKind.Practice },
        new WeekendActivity { id = "debrief", kind = ActivityKind.Debrief },
    };

    static bool Settled(HashSet<string> done, HashSet<string> missed) =>
        FastTravelUnlock.MeetingSettled(Sheet(), done.Contains, missed.Contains);

    [Test]
    public void NothingSettled_IsNotSettled()
    {
        Assert.IsFalse(Settled(new HashSet<string>(), new HashSet<string>()));
        Assert.IsFalse(FastTravelUnlock.MeetingSettled(null, _ => true, _ => true), "No sheet, nothing settled.");
    }

    [Test]
    public void OtherBookingsDone_DoNotSettleIt()
    {
        Assert.IsFalse(Settled(new HashSet<string> { "orientation", "practice" }, new HashSet<string>()));
    }

    [Test]
    public void TheBriefingDone_SettlesIt()
    {
        Assert.IsTrue(Settled(new HashSet<string> { "briefing" }, new HashSet<string>()));
    }

    [Test]
    public void TheBriefingMissed_SettlesIt_SoNobodyIsStranded()
    {
        Assert.IsTrue(Settled(new HashSet<string>(), new HashSet<string> { "briefing" }));
    }

    [Test]
    public void TheDebriefDone_SettlesIt()
    {
        Assert.IsTrue(Settled(new HashSet<string> { "debrief" }, new HashSet<string>()));
    }

    // ------------------------------------------------------------------ the prompt

    [Test]
    public void ThePrompt_GoesUpOnceUnlocked_WithSomewhereToGo_AndThePlayerFree()
    {
        Assert.IsTrue(FastTravelUnlock.ShouldPrompt(locked: false, taught: false, canTravel: true, busy: false));
    }

    [Test]
    public void ThePrompt_WaitsForEveryCondition()
    {
        Assert.IsFalse(FastTravelUnlock.ShouldPrompt(true, false, true, false), "Prompted while still locked.");
        Assert.IsFalse(FastTravelUnlock.ShouldPrompt(false, true, true, false), "Prompted a second time.");
        Assert.IsFalse(FastTravelUnlock.ShouldPrompt(false, false, false, false), "Prompted with no button to point at.");
        Assert.IsFalse(FastTravelUnlock.ShouldPrompt(false, false, true, true), "Prompted over a conversation or card.");
    }
}
