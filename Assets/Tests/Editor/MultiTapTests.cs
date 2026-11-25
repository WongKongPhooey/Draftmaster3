using Draftmaster.Controls;
using NUnit.Framework;

// Triple-tapping a steering key takes the car off Auto (DriveModeController). The run fires as the third press
// STARTS, so the held third press is already steering the car the player has just taken back.
public class MultiTapTests
{
    // Press at `at`, let go `hold` seconds later. Returns whether the press fired.
    static bool Tap(MultiTap t, float at, float hold = 0.1f)
    {
        bool fired = t.Update(1f, at);
        t.Update(0f, at + hold);
        return fired;
    }

    [Test]
    public void ThreeQuickTapsFireOnTheThirdPressStarting()
    {
        var t = new MultiTap(3);
        Assert.IsFalse(Tap(t, 0f));
        Assert.IsFalse(Tap(t, 0.3f));
        Assert.IsTrue(t.Update(1f, 0.6f), "the third press fires as it goes down");
        Assert.IsFalse(t.Update(1f, 1.5f), "holding it does not fire again");
    }

    [Test]
    public void TwoTapsDoNot()
    {
        var t = new MultiTap(3);
        Assert.IsFalse(Tap(t, 0f));
        Assert.IsFalse(t.Update(1f, 0.3f));
        Assert.IsFalse(t.Update(1f, 2f));
    }

    [Test]
    public void APauseTooLongStartsTheCountAgain()
    {
        var t = new MultiTap(3);
        Tap(t, 0f);
        Tap(t, 0.3f);
        Assert.IsFalse(Tap(t, 0.4f + MultiTap.GapSeconds + 0.1f), "after the pause this is the first tap of a new run");
        Assert.IsFalse(Tap(t, 1.1f));
        Assert.IsTrue(t.Update(1f, 1.4f));
    }

    [Test]
    public void AHeldPressBreaksTheRun()
    {
        var t = new MultiTap(3);
        Tap(t, 0f);
        Tap(t, 0.3f, hold: MultiTap.TapSeconds + 0.2f);   // a steer, not a tap
        Assert.IsFalse(t.Update(1f, 0.85f), "the held press was not a tap");
    }

    [Test]
    public void AFourthTapDoesNotStartAnotherRunHalfway()
    {
        var t = new MultiTap(3);
        Tap(t, 0f);
        Tap(t, 0.3f);
        Assert.IsTrue(Tap(t, 0.6f));
        Assert.IsFalse(Tap(t, 0.9f));
        Assert.IsFalse(Tap(t, 1.2f));
        Assert.IsTrue(t.Update(1f, 1.5f), "three more taps after the run fire again");
    }

    [Test]
    public void APressHeldThroughAnInterruptDoesNotCount()
    {
        var t = new MultiTap(3);
        t.Update(1f, 0f);
        t.Interrupt();
        t.Update(1f, 0.1f);
        t.Update(0f, 0.15f);            // let go of the held press: not a tap
        Assert.IsFalse(Tap(t, 0.3f));
        Assert.IsFalse(Tap(t, 0.6f));
        Assert.IsTrue(t.Update(1f, 0.9f));
    }

    [Test]
    public void ADoubleTapCounterFiresOnTheSecond()
    {
        var t = new MultiTap(2);
        Assert.IsFalse(Tap(t, 0f));
        Assert.IsTrue(t.Update(1f, 0.3f));
    }
}
