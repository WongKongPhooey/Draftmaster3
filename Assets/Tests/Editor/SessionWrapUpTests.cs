using Draftmaster.Sim;
using NUnit.Framework;

// The end of a practice or qualifying session for the player's car.
//
// When the clock runs out a driver on track finishes their lap, then the AI drives the car into the box —
// or the driver pits themselves. The session only ends once they are out of the car.
public class SessionWrapUpTests
{
    const SessionWrapUp.Step Wait = SessionWrapUp.Step.Wait;
    const SessionWrapUp.Step TakeOver = SessionWrapUp.Step.TakeOver;
    const SessionWrapUp.Step Close = SessionWrapUp.Step.CloseSession;

    [Test]
    public void ADriverOnFootEndsTheSessionAtOnce()
    {
        Assert.AreEqual(Close, SessionWrapUp.Next(false, false, false, 0f, true));
        Assert.AreEqual(Close, SessionWrapUp.Next(false, true, true, 10f, true));
    }

    [Test]
    public void TheLapInProgressIsRunOut()
    {
        Assert.AreEqual(Wait, SessionWrapUp.Next(true, false, false, 0f, true));
    }

    [Test]
    public void AtTheLineTheAIBringsTheCarIn()
    {
        Assert.AreEqual(TakeOver, SessionWrapUp.Next(true, true, false, 0f, true));
    }

    [Test]
    public void WithNoBrainOrBoxTheSessionEndsAtTheLineInstead()
    {
        Assert.AreEqual(Close, SessionWrapUp.Next(true, true, false, 0f, false));
    }

    [Test]
    public void ADriverPittingThemselvesIsLeftToReachTheirBox()
    {
        // Crossing the line on pit road does not take the car off them.
        Assert.AreEqual(Wait, SessionWrapUp.Next(true, true, true, 0f, true));
        Assert.AreEqual(Wait, SessionWrapUp.Next(true, false, true, SessionWrapUp.StoppedOnPitRoadSeconds - 0.1f, true));
    }

    [Test]
    public void ACarStoppedShortOnPitRoadIsBroughtTheRestOfTheWay()
    {
        Assert.AreEqual(TakeOver, SessionWrapUp.Next(true, false, true, SessionWrapUp.StoppedOnPitRoadSeconds, true));
        Assert.AreEqual(Close, SessionWrapUp.Next(true, false, true, SessionWrapUp.StoppedOnPitRoadSeconds, false));
    }

    [Test]
    public void TheLapIsFinishedOnceTheLineIsCrossed()
    {
        Assert.IsFalse(SessionWrapUp.LapFinished(5, 5));
        Assert.IsTrue(SessionWrapUp.LapFinished(5, 6));
    }
}
