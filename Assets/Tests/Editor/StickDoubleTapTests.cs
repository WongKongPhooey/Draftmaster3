using Draftmaster.Controls;
using NUnit.Framework;

// Double-tapping the pad's walking stick flips walk/run. The flip lands on the SECOND push's start, so the
// player can tap, push and hold, and be walking in the new gait from the first step.
public class StickDoubleTapTests
{
    // Flick the stick out at `at` and back `hold` seconds later.
    static void Tap(StickDoubleTap d, float at, float hold = 0.1f)
    {
        d.Update(1f, at);
        d.Update(0f, at + hold);
    }

    [Test]
    public void TapThenPushFlipsToRunOnThePushNotItsRelease()
    {
        var d = new StickDoubleTap();
        Tap(d, 0f);
        Assert.IsTrue(d.Update(1f, 0.25f), "the second push flips as it starts");
        Assert.IsTrue(d.Running);
        d.Update(1f, 2f);
        Assert.IsTrue(d.Running, "holding the second push keeps running");
    }

    [Test]
    public void AnotherDoubleTapFlipsBackToWalk()
    {
        var d = new StickDoubleTap();
        Tap(d, 0f);
        d.Update(1f, 0.2f);
        d.Update(0f, 3f);         // long walk, let go
        Tap(d, 5f);
        d.Update(1f, 5.2f);
        Assert.IsFalse(d.Running);
    }

    [Test]
    public void OrdinaryWalkingNeverFlips()
    {
        var d = new StickDoubleTap();
        d.Update(1f, 0f); d.Update(0f, 1.5f);      // walk, stop
        d.Update(1f, 1.7f); d.Update(0f, 4f);      // walk again soon after
        Assert.IsFalse(d.Running, "a long push is not a tap");
    }

    [Test]
    public void SlowSecondPushDoesNotFlip()
    {
        var d = new StickDoubleTap();
        Tap(d, 0f);
        Assert.IsFalse(d.Update(1f, 0.1f + StickDoubleTap.GapSeconds + 0.05f));
        Assert.IsFalse(d.Running);
    }

    [Test]
    public void TripleTapFlipsOnceNotTwice()
    {
        var d = new StickDoubleTap();
        Tap(d, 0f);
        Tap(d, 0.2f);             // flips to run
        d.Update(1f, 0.4f);       // third push: no fresh tap before it
        Assert.IsTrue(d.Running);
    }

    [Test]
    public void WobbleInsideTheHysteresisIsNotATap()
    {
        var d = new StickDoubleTap();
        d.Update(1f, 0f);
        d.Update(0.4f, 0.05f);    // eased off but not let go
        d.Update(1f, 0.1f);       // still the same push
        Assert.IsFalse(d.Running);
    }

    [Test]
    public void InterruptDropsAHalfFinishedTapAndKeepsTheGait()
    {
        var d = new StickDoubleTap();
        Tap(d, 0f); d.Update(1f, 0.2f); d.Update(0f, 1f);   // running
        Tap(d, 2f);
        d.Interrupt();            // stick went to a menu
        d.Update(0f, 2.15f);
        Assert.IsFalse(d.Update(1f, 2.2f), "the tap before the menu no longer counts");
        Assert.IsTrue(d.Running);
    }

    [Test]
    public void StickHeldThroughAnInterruptMustBeLetGoFirst()
    {
        var d = new StickDoubleTap();
        Tap(d, 0f);
        d.Interrupt();
        d.Update(1f, 0.2f);       // already pushed when the stick came back: not a fresh push
        d.Update(0f, 0.25f);
        Assert.IsFalse(d.Update(1f, 0.3f));
        Assert.IsFalse(d.Running);
    }
}
