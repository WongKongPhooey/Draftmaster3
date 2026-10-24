using Draftmaster.Sim;
using NUnit.Framework;

// Dirty air: a car's wake costs the car behind grip, more the closer it is and the faster they go (v², downforce).
public class DirtyAirTests
{
    [Test]
    public void CleanAirOrNoLossChangesNothing()
    {
        Assert.AreEqual(1f, DirtyAir.GripFactor(0f, 80f, 0.06f), 1e-6f);
        Assert.AreEqual(1f, DirtyAir.GripFactor(1f, 80f, 0f), 1e-6f);
        Assert.AreEqual(1f, DirtyAir.GripFactor(1f, 0f, 0.06f), 1e-6f);
    }

    [Test]
    public void FullTowAtSpeedCostsTheWholeLoss()
    {
        Assert.AreEqual(0.94f, DirtyAir.GripFactor(1f, DirtyAir.ReferenceMps, 0.06f), 1e-5f);
        // Faster than the reference costs no more: the loss is the most the wake takes.
        Assert.AreEqual(0.94f, DirtyAir.GripFactor(1f, 95f, 0.06f), 1e-5f);
    }

    [Test]
    public void LossGrowsWithTowAndWithSpeedSquared()
    {
        float close = DirtyAir.GripFactor(0.8f, 70f, 0.06f);
        float back = DirtyAir.GripFactor(0.3f, 70f, 0.06f);
        Assert.Less(close, back);
        Assert.Less(back, 1f);

        // Half the speed, a quarter of the loss: Martinsville's corners hardly notice it.
        float fast = 1f - DirtyAir.GripFactor(1f, 60f, 0.06f);
        float slow = 1f - DirtyAir.GripFactor(1f, 30f, 0.06f);
        Assert.AreEqual(fast / 4f, slow, 1e-5f);
    }

    [Test]
    public void OutOfRangeInputsAreClamped()
    {
        Assert.AreEqual(0.94f, DirtyAir.GripFactor(3f, 200f, 0.06f), 1e-5f);
        Assert.AreEqual(0f, DirtyAir.GripFactor(1f, 200f, 5f), 1e-5f);
    }
}
