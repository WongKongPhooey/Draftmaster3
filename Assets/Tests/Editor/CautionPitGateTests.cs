using Draftmaster.Sim;
using NUnit.Framework;

// Who may pit around a full-course caution (CautionPitGate, run by PitStopController): a damaged car comes in at the
// first pits-open pass it can make, stops once under the caution, and nobody pits from the green being signalled until
// they are over the restart line. Engine-free: also runs in Tools/HeadlessTests.
public class CautionPitGateTests
{
    const float Lap = 4000f;
    const float Open = float.MaxValue;   // PitRoadOpenMetresLeft under green

    // Drive the gate along the lap from `from` to `to` (metres, `to` may run past Lap) in 10 m steps.
    static void Run(CautionPitGate gate, float from, float to, bool caution, bool signalled)
    {
        for (float d = from; d <= to; d += 10f)
            gate.Step(caution, signalled, true, d % Lap, Lap);
    }

    [Test]
    public void UnderGreen_NothingIsHeldBack()
    {
        var gate = new CautionPitGate();
        Run(gate, 0f, 2000f, caution: false, signalled: false);
        Assert.IsTrue(gate.MayPit(false, true, 300f, Open));
    }

    [Test]
    public void UnderCaution_ADamagedCarMayComeInOnlyWithPitRoadOpen_AndOnAPassItMakesBeforeItShuts()
    {
        var gate = new CautionPitGate();
        Run(gate, 0f, 500f, caution: true, signalled: false);
        Assert.IsFalse(gate.MayPit(true, false, 300f, 0f), "pit road closed while the safety car picks up the leader");
        Assert.IsTrue(gate.MayPit(true, true, 300f, 1500f), "open, and it is at the entry well before it shuts");
        Assert.IsFalse(gate.MayPit(true, true, 300f, 200f), "it would reach the entry after pit road has shut");
    }

    [Test]
    public void ACarThatHasStoppedUnderTheCaution_DoesNotComeInAgainUntilAfterTheRestart()
    {
        var gate = new CautionPitGate();
        Run(gate, 0f, 500f, caution: true, signalled: false);
        gate.CommittedToStop(true);
        Assert.IsTrue(gate.StopMadeThisCaution);
        // Back out, round to the entry again with pit road still open: no second stop.
        Run(gate, 500f, 4500f, caution: true, signalled: false);
        Assert.IsFalse(gate.MayPit(true, true, 300f, 2000f));

        // The green is signalled, the field comes round and goes green at the line; over it, the restart has been run.
        Run(gate, 500f, 3990f, caution: true, signalled: true);
        Assert.IsFalse(gate.MayPit(false, true, 300f, Open), "green waved, not over the restart line yet");
        Run(gate, 3990f, 4100f, caution: false, signalled: false);
        Assert.IsFalse(gate.StopMadeThisCaution);
        Assert.IsTrue(gate.MayPit(false, true, 300f, Open), "racing again after the restart");

        // The next caution is a new one: it may stop under that.
        Run(gate, 100f, 600f, caution: true, signalled: false);
        Assert.IsTrue(gate.MayPit(true, true, 300f, 2000f));
    }

    [Test]
    public void AStopCommittedUnderGreen_DoesNotCountAsTheCautionStop()
    {
        var gate = new CautionPitGate();
        gate.CommittedToStop(false);
        Run(gate, 0f, 500f, caution: true, signalled: false);
        Assert.IsTrue(gate.MayPit(true, true, 300f, 2000f));
    }

    [Test]
    public void OnceTheGreenIsSignalled_NobodyPits_UntilOverTheRestartLine()
    {
        var gate = new CautionPitGate();
        Run(gate, 1000f, 2000f, caution: true, signalled: false);
        Assert.IsFalse(gate.RestartLapHold);

        // "Green next time by": held, and a car already on its way in turns it down.
        gate.Step(true, true, true, 2010f, Lap);
        Assert.IsTrue(gate.RestartLapHold);
        Assert.IsTrue(gate.AbortRunIn);
        Assert.IsFalse(gate.MayPit(true, true, 300f, Open));

        // A car at the back of the train: the green waves at the front while it is still short of the pit entry
        // (3700 m) - pit road is open under green, but not to it on this lap.
        Run(gate, 2020f, 3500f, caution: true, signalled: true);
        Run(gate, 3500f, 3800f, caution: false, signalled: false);
        Assert.IsFalse(gate.MayPit(false, true, 300f, Open), "past the pit entry after the green: still the green lap");

        // Over the line: free to stop on the next pass of the entry.
        Run(gate, 3800f, 4200f, caution: false, signalled: false);
        Assert.IsFalse(gate.RestartLapHold);
        Assert.IsTrue(gate.MayPit(false, true, 300f, Open));
    }

    [Test]
    public void TheLeader_IsFreeOnceItTakesTheGreenAtTheLine()
    {
        var gate = new CautionPitGate();
        Run(gate, 1000f, 3990f, caution: true, signalled: true);
        Assert.IsTrue(gate.RestartLapHold);
        gate.Step(false, false, true, 5f, Lap);   // the green waves as the leader reaches the line
        Assert.IsFalse(gate.RestartLapHold);
    }

    [Test]
    public void OnPitRoad_TheLapDistanceIsNotWatched()
    {
        var gate = new CautionPitGate();
        gate.Step(true, true, true, 3990f, Lap);
        gate.Step(true, true, false, 10f, Lap);   // on pit road, wherever its lap distance says
        gate.Step(true, true, true, 20f, Lap);    // back out: no crossing seen
        Assert.IsTrue(gate.RestartLapHold);
    }

    [Test]
    public void CrossedLine_OnlyForwardsOverTheLine()
    {
        Assert.IsTrue(CautionPitGate.CrossedLine(3995f, 3f, Lap));
        Assert.IsFalse(CautionPitGate.CrossedLine(100f, 110f, Lap));
        Assert.IsFalse(CautionPitGate.CrossedLine(110f, 100f, Lap), "a step back is not a lap");
        Assert.IsFalse(CautionPitGate.CrossedLine(3995f, 3f, 0f));
    }
}
