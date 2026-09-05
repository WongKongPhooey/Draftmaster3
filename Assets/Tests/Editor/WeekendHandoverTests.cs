using Draftmaster.Weekend;
using NUnit.Framework;

// Handing the circuit over behind a blackout.
//
// The rule is not "always fade": a wipe is only worth anything when somebody is stood in the paddock
// watching the handover happen. Every other case — driving, sat in a grandstand, in a lobby, or already
// mid-wipe for some other reason — has to fall straight through to what the spawner always did, because a
// blackout nobody asked for is a worse bug than the pop-in it was hiding.
//
// The runtime half (WeekendTrackChangeover) is coroutines and scene lookups and cannot be tested; this is
// the half with the decisions in it.
public class WeekendHandoverTests
{
    // The case the whole thing exists for: on foot in the paddock, single player, nothing else on screen.
    [Test]
    public void OnFootInThePaddock_HandsOverBehindAWipe()
    {
        Assert.IsTrue(WeekendHandover.ShouldWipe(onFootInThePaddock: true, multiplayer: false,
                                                 watchingFromAStand: false, screenAlreadyWiping: false));
    }

    [Test]
    public void NobodyOnFootToSeeIt_LetsTheHandoverThrough()
    {
        Assert.IsFalse(WeekendHandover.ShouldWipe(onFootInThePaddock: false, multiplayer: false,
                                                  watchingFromAStand: false, screenAlreadyWiping: false));
    }

    // A lobby's field is the host's to spawn and replicate; blacking every client's screen out on a clock
    // they do not own is not a handover, it is a fault.
    [Test]
    public void Multiplayer_NeverWipes()
    {
        Assert.IsFalse(WeekendHandover.ShouldWipe(onFootInThePaddock: true, multiplayer: true,
                                                  watchingFromAStand: false, screenAlreadyWiping: false));
    }

    // The stand owns the camera and runs its own wipes in and out of the seat. A session ending under a
    // spectator is the thing they came to watch, not something to hide from them.
    [Test]
    public void WatchingFromAStand_NeverWipes()
    {
        Assert.IsFalse(WeekendHandover.ShouldWipe(onFootInThePaddock: true, multiplayer: false,
                                                  watchingFromAStand: true, screenAlreadyWiping: false));
    }

    // Two fades over the same frames read as a flicker. Whoever got there first is already covering it.
    [Test]
    public void AlreadyWiping_LeavesTheScreenAlone()
    {
        Assert.IsFalse(WeekendHandover.ShouldWipe(onFootInThePaddock: true, multiplayer: false,
                                                  watchingFromAStand: false, screenAlreadyWiping: true));
    }

    // ------------------------------------------------------------------ when the lights may go out

    [Test]
    public void NothingOnScreen_IsReadyToWipe()
    {
        Assert.IsTrue(WeekendHandover.ReadyToWipe(cardUp: false, panelUp: false, conversationUp: false));
    }

    // The obligation that moved the clock puts a card up saying what it earned, the sheet may be open over
    // it, and the crew chief who ran it is still stood there. All three hold the wipe.
    [Test]
    public void AnythingWithThePlayersAttentionInIt_HoldsTheWipe()
    {
        Assert.IsFalse(WeekendHandover.ReadyToWipe(cardUp: true, panelUp: false, conversationUp: false));
        Assert.IsFalse(WeekendHandover.ReadyToWipe(cardUp: false, panelUp: true, conversationUp: false));
        Assert.IsFalse(WeekendHandover.ReadyToWipe(cardUp: false, panelUp: false, conversationUp: true));
    }

    // ------------------------------------------------------------------ the two ways out

    [Test]
    public void WaitingForever_GivesUpAndLetsItThrough()
    {
        Assert.IsFalse(WeekendHandover.GaveUpWaiting(0f));
        Assert.IsFalse(WeekendHandover.GaveUpWaiting(WeekendHandover.WaitLimitSeconds - 0.1f));
        Assert.IsTrue(WeekendHandover.GaveUpWaiting(WeekendHandover.WaitLimitSeconds));
    }

    // A spawner destroyed mid-handover never reports back. The screen still comes up.
    [Test]
    public void SatInTheDarkTooLong_BringsTheScreenBack()
    {
        Assert.IsFalse(WeekendHandover.StagedLongEnough(0f));
        Assert.IsFalse(WeekendHandover.StagedLongEnough(WeekendHandover.StageLimitSeconds - 0.1f));
        Assert.IsTrue(WeekendHandover.StagedLongEnough(WeekendHandover.StageLimitSeconds));
    }

    // The whole wipe is punctuation, not a cutscene: down, a beat, back up, comfortably inside a second.
    [Test]
    public void TheWipeIsShort()
    {
        float whole = WeekendHandover.FadeOutSeconds + WeekendHandover.HoldSeconds + WeekendHandover.FadeInSeconds;
        Assert.Greater(whole, 0.2f);
        Assert.Less(whole, 1f);

        // And the give-up limits are the right way round: never sit black longer than we would wait to
        // start, or a handover held up by a conversation would time out in the dark instead of in the open.
        Assert.Less(WeekendHandover.StageLimitSeconds, WeekendHandover.WaitLimitSeconds);
    }
}
