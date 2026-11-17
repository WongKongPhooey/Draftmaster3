using Draftmaster.Controls;
using NUnit.Framework;

// Turning Auto off leaves the AI driving until the player taps the accelerator or the brake.
public class AutoHandBackTests
{
    [Test]
    public void NothingHappensUntilAPedalIsTapped()
    {
        var h = new AutoHandBack();
        h.Arm(0f, 0f);
        Assert.IsTrue(h.Waiting);
        for (int i = 0; i < 10; i++) Assert.IsFalse(h.Update(0f, 0f), "no pedal, the AI keeps the car");
        Assert.IsTrue(h.Waiting);
    }

    [Test]
    public void TappingTheAcceleratorHandsTheCarBack()
    {
        var h = new AutoHandBack();
        h.Arm(0f, 0f);
        Assert.IsTrue(h.Update(1f, 0f));
        Assert.IsFalse(h.Waiting);
        Assert.IsFalse(h.Update(1f, 0f), "only the one hand-back");
    }

    [Test]
    public void TappingTheBrakeHandsTheCarBack()
    {
        var h = new AutoHandBack();
        h.Arm(0f, 0f);
        Assert.IsTrue(h.Update(0f, 1f));
        Assert.IsFalse(h.Waiting);
    }

    [Test]
    public void APedalHeldWhenAutoGoesOffMustBeLetGoFirst()
    {
        var h = new AutoHandBack();
        h.Arm(1f, 0f);
        Assert.IsFalse(h.Update(1f, 0f), "a thumb already on the throttle is not a tap");
        Assert.IsFalse(h.Update(0f, 0f));
        Assert.IsTrue(h.Update(1f, 0f), "lifted and pressed again: that is the tap");
    }

    [Test]
    public void TheOtherPedalStillCountsWhileOneIsHeld()
    {
        var h = new AutoHandBack();
        h.Arm(1f, 0f);
        Assert.IsTrue(h.Update(1f, 1f), "a fresh brake press hands over even with the throttle held");
    }

    [Test]
    public void ATriggerRestingJustOffZeroIsNotATap()
    {
        var h = new AutoHandBack();
        h.Arm(0f, 0f);
        Assert.IsFalse(h.Update(0.15f, 0.2f));
        Assert.IsTrue(h.Waiting);
    }

    [Test]
    public void PressesBeforeArmingOrAfterCancelDoNothing()
    {
        var h = new AutoHandBack();
        Assert.IsFalse(h.Update(1f, 0f), "not waiting: a pedal is just a pedal");
        h.Update(0f, 0f);
        h.Arm(0f, 0f);
        h.Cancel();
        Assert.IsFalse(h.Update(1f, 0f), "Auto put back on: no hand-back");
        Assert.IsFalse(h.Waiting);
    }

    [Test]
    public void APressStartedWhileNotWaitingIsNotATapOnceArmed()
    {
        var h = new AutoHandBack();
        h.Update(1f, 0f);   // pedal down before the hand-back is asked for
        h.Arm(1f, 0f);
        Assert.IsFalse(h.Update(1f, 0f));
    }
}
