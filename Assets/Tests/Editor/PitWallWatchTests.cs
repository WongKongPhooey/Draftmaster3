using NUnit.Framework;
using Draftmaster.Sim;

// The crew chief's watch-a-car ring: chief, the field in running order, back to the chief.
public class PitWallWatchTests
{
    [Test]
    public void Next_FromTheChief_IsTheLeader()
    {
        Assert.AreEqual(0, PitWallWatch.Step(PitWallWatch.Self, 10, +1));
    }

    [Test]
    public void Previous_FromTheChief_IsLastPlace()
    {
        Assert.AreEqual(9, PitWallWatch.Step(PitWallWatch.Self, 10, -1));
    }

    [Test]
    public void Next_WalksDownTheOrder()
    {
        Assert.AreEqual(4, PitWallWatch.Step(3, 10, +1));
        Assert.AreEqual(2, PitWallWatch.Step(3, 10, -1));
    }

    [Test]
    public void PastEitherEnd_ComesBackToTheChief()
    {
        Assert.AreEqual(PitWallWatch.Self, PitWallWatch.Step(9, 10, +1));
        Assert.AreEqual(PitWallWatch.Self, PitWallWatch.Step(0, 10, -1));
    }

    [Test]
    public void AFullLapOfTheRing_EndsWhereItStarted()
    {
        int watch = PitWallWatch.Self;
        for (int i = 0; i < 11; i++) watch = PitWallWatch.Step(watch, 10, +1);
        Assert.AreEqual(PitWallWatch.Self, watch);
    }

    [Test]
    public void ACarThatLeftTheOrder_RestartsFromTheTop()
    {
        Assert.AreEqual(0, PitWallWatch.Step(15, 10, +1));
        Assert.AreEqual(9, PitWallWatch.Step(15, 10, -1));
    }

    [Test]
    public void AnEmptyField_StaysOnTheChief()
    {
        Assert.AreEqual(PitWallWatch.Self, PitWallWatch.Step(PitWallWatch.Self, 0, +1));
        Assert.AreEqual(PitWallWatch.Self, PitWallWatch.Step(3, 0, -1));
    }
}
