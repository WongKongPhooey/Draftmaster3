using System;
using Draftmaster.Sim;
using NUnit.Framework;

// The search behind Draftmaster > AI > Calibrate AI Pace: given a lap time, how strong do the AI need to be to
// run it? Driven here by a made-up lap-time curve (a real car's is the same shape: faster as grip and power go
// up, flattening off), so every case is exact and instant. The editor window drives the same class with the
// headless lap sim.
public class AIPaceSearchTests
{
    // A Watkins Glen-ish curve: 80s at today's AI (k = 1), about 10s quicker by k = 1.3.
    static float Lap(float k) => 80f / (float)Math.Pow(k, 0.45);

    static AIPaceSearch Run(float target, Func<float, float> lap, float crashAbove = float.PositiveInfinity,
                            float tolerance = 0.2f)
    {
        var s = new AIPaceSearch(target, tolerance);
        while (!s.Done)
        {
            float k = s.Next();
            s.Report(k, lap(k), clean: k <= crashAbove);
        }
        return s;
    }

    [Test]
    public void TenSecondsQuicker_IsFoundWithinTolerance_InAHandfulOfLaps()
    {
        var s = Run(70f, Lap);
        Assert.IsTrue(s.Matched, s.Outcome);
        Assert.AreEqual(70f, s.Best.lapSeconds, 0.2f);
        Assert.LessOrEqual(s.Trials.Count, 7, "each trial is a simulated lap or two; the search should not wander");
        Assert.Greater(s.Best.k, 1f, "the AI had to get stronger to go quicker");
    }

    [Test]
    public void ATargetSlowerThanTodaysAI_SearchesDownward()
    {
        var s = Run(85f, Lap);
        Assert.IsTrue(s.Matched, s.Outcome);
        Assert.Less(s.Best.k, 1f, "a slower target has to weaken the AI");
    }

    [Test]
    public void TheFirstTrialIsTodaysAI()
    {
        var s = new AIPaceSearch(70f);
        Assert.AreEqual(1f, s.Next(), 1e-6f, "start from the AI as it is, so the log shows today's gap first");
    }

    // A lap that only hits the target by leaving the road isn't an answer — the difficulty wanted is one the AI
    // can hold every lap. The search must settle on the fastest CLEAN lap and say it couldn't go further.
    [Test]
    public void IfTheAICrashesBeforeReachingTheTarget_TheAnswerIsTheFastestCleanLap()
    {
        float crashAbove = 1.15f;   // Lap(1.15) ~ 75.1s: 5s short of the 70s target
        var s = Run(70f, Lap, crashAbove);
        Assert.IsFalse(s.Matched);
        Assert.IsTrue(s.Best.clean);
        Assert.LessOrEqual(s.Best.k, crashAbove);
        Assert.AreEqual(Lap(crashAbove), s.Best.lapSeconds, 0.3f, "it should close right in on the crash limit");
        StringAssert.Contains("leaves the road", s.Outcome);
    }

    [Test]
    public void AGapTheKnobsCannotClose_StopsAtTheLimitAndSaysSo()
    {
        var s = Run(40f, Lap);   // would need k ~ 4.7, past the 2.5 ceiling
        Assert.IsTrue(s.Done);
        Assert.IsFalse(s.Matched);
        StringAssert.Contains("can't close", s.Outcome);
    }

    // A curve with a flat stretch (the car hitting top speed everywhere, say) makes plain regula falsi keep one
    // end forever. The halving fallback has to get it home anyway.
    [Test]
    public void ABentCurveStillConverges()
    {
        static float Kinked(float k) => k < 1.2f ? 80f - (k - 1f) * 40f : 72f - (k - 1.2f) * 2f;
        var s = Run(71.5f, Kinked, tolerance: 0.05f);
        Assert.IsTrue(s.Matched, s.Outcome + $" after {s.Trials.Count} trials");
    }

    [Test]
    public void ItNeverDrivesMoreThanItsBudget()
    {
        var s = new AIPaceSearch(70f, toleranceSeconds: 0.0001f, maxTrials: 5);
        while (!s.Done)
        {
            float k = s.Next();
            s.Report(k, Lap(k), true);
        }
        Assert.LessOrEqual(s.Trials.Count, 5);
    }
}
