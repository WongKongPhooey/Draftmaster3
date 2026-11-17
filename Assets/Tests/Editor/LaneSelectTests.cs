using Draftmaster.Controls;
using NUnit.Framework;

// Draftmaster 2's lane controls: left / right move the car one lane per press, three lanes, stopping at the edges.
public class LaneSelectTests
{
    [Test]
    public void APressMovesOneLane_HoldingDoesNotKeepMoving()
    {
        var s = new LaneSelect();
        s.Reset(LaneSelect.Middle, 0f);
        Assert.IsTrue(s.Update(-1f));
        Assert.AreEqual(LaneSelect.Left, s.Lane);
        for (int i = 0; i < 10; i++) Assert.IsFalse(s.Update(-1f), "held, not pressed again");
        Assert.AreEqual(LaneSelect.Left, s.Lane);
    }

    [Test]
    public void LettingGoAndPressingAgainMovesAgain()
    {
        var s = new LaneSelect();
        s.Reset(LaneSelect.Left, 0f);
        Assert.IsTrue(s.Update(1f));
        Assert.IsFalse(s.Update(0f));
        Assert.IsTrue(s.Update(1f));
        Assert.AreEqual(LaneSelect.Right, s.Lane);
    }

    [Test]
    public void StopsAtTheEdges()
    {
        var s = new LaneSelect();
        s.Reset(LaneSelect.Right, 0f);
        Assert.IsFalse(s.Update(1f), "already in the right-hand lane");
        Assert.AreEqual(LaneSelect.Right, s.Lane);
        s.Reset(LaneSelect.Left, 0f);
        Assert.IsFalse(s.Update(-1f));
        Assert.AreEqual(LaneSelect.Left, s.Lane);
    }

    [Test]
    public void SteerHeldWhenTheLanesTakeOverHasToBeLetGoFirst()
    {
        var s = new LaneSelect();
        s.Reset(LaneSelect.Middle, 1f);
        Assert.IsFalse(s.Update(1f), "the turn the player was already making is not a press");
        Assert.AreEqual(LaneSelect.Middle, s.Lane);
        s.Update(0f);
        Assert.IsTrue(s.Update(1f));
        Assert.AreEqual(LaneSelect.Right, s.Lane);
    }

    [Test]
    public void AStickHoveringAroundThePressPointIsOnePress()
    {
        var s = new LaneSelect();
        s.Reset(LaneSelect.Middle, 0f);
        Assert.IsTrue(s.Update(0.6f));
        // Wobbling between the release and press points never lets go.
        Assert.IsFalse(s.Update(0.4f));
        Assert.IsFalse(s.Update(0.6f));
        Assert.AreEqual(LaneSelect.Right, s.Lane);
        // A small nudge is not a press.
        s.Reset(LaneSelect.Middle, 0f);
        Assert.IsFalse(s.Update(0.3f));
        Assert.AreEqual(LaneSelect.Middle, s.Lane);
    }

    [Test]
    public void ResetClampsAnOutOfRangeLane()
    {
        var s = new LaneSelect();
        s.Reset(5, 0f);
        Assert.AreEqual(LaneSelect.Right, s.Lane);
        s.Reset(-5, 0f);
        Assert.AreEqual(LaneSelect.Left, s.Lane);
    }

    [Test]
    public void LaneNames_InsideAndOutsideOnAnOval_LeftAndRightOnARoadCourse()
    {
        Assert.AreEqual("Inside", LaneSelect.Name(LaneSelect.Left, oval: true));
        Assert.AreEqual("Middle", LaneSelect.Name(LaneSelect.Middle, oval: true));
        Assert.AreEqual("Outside", LaneSelect.Name(LaneSelect.Right, oval: true));
        Assert.AreEqual("Outside", LaneSelect.Name(LaneSelect.Left, oval: true, insideIsLeft: false));
        Assert.AreEqual("Left", LaneSelect.Name(LaneSelect.Left, oval: false));
        Assert.AreEqual("Right", LaneSelect.Name(LaneSelect.Right, oval: false));
    }
}
