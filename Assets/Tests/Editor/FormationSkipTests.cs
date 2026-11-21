using NUnit.Framework;

// EditMode coverage for FormationSkip.Offered — when the "skip the pace lap" / "skip the caution" prompt is up
// (and, since a skip in progress stops the moment it isn't, when fast-forwarding ends).
//
// FormationSkip and RaceStart live in Assembly-CSharp, which an asmdef can't reference, so they're reached by
// reflection.
public class FormationSkipTests
{
    static System.Type T(string name) => System.Type.GetType(name + ", Assembly-CSharp");

    static object Phase(string name) => System.Enum.Parse(T("RaceStart+Phase") ?? T("RaceStart").GetNestedType("Phase"), name);

    static bool Offered(bool multiplayer = false, string phase = "Formation", bool closingUp = false, bool auto = true,
                        bool onFoot = false, bool leaderCaught = false, bool crossedLine = false,
                        bool finished = false) =>
        (bool)T("FormationSkip").GetMethod("Offered")
            .Invoke(null, new object[] { multiplayer, Phase(phase), closingUp, auto, onFoot, leaderCaught, crossedLine,
                                         finished });

    [Test]
    public void OfferedOnThePaceLapWithTheCarOnAuto()
    {
        Assert.IsTrue(Offered());
    }

    [Test]
    public void NotOfferedWhileThePlayerIsDriving()
    {
        Assert.IsFalse(Offered(auto: false), "the skip is an Auto-only option");
    }

    [Test]
    public void EndsOnceTheFieldClosesUp()
    {
        Assert.IsFalse(Offered(closingUp: true), "skipping ran on past the close-up into the run to the green");
    }

    [Test]
    public void NeverUnderGreenOrBeforeThePaceLap()
    {
        Assert.IsFalse(Offered(phase: "PreGrid"), "offered before the pace lap had begun");
        Assert.IsFalse(Offered(phase: "Green"), "offered in the race");
        Assert.IsFalse(Offered(phase: "Green", leaderCaught: true, crossedLine: true), "offered in the race");
    }

    [Test]
    public void NotInMultiplayerOrOnFoot()
    {
        Assert.IsFalse(Offered(multiplayer: true), "a co-op guest can't be fast-forwarded by the host's clock");
        Assert.IsFalse(Offered(onFoot: true), "offered with the player out of the car");
    }

    // ---- caution laps behind the safety car

    [Test]
    public void OfferedUnderACautionOnceTheLeaderIsCaughtAndThePlayerHasCrossedTheLine()
    {
        Assert.IsTrue(Offered(phase: "Caution", leaderCaught: true, crossedLine: true));
    }

    [Test]
    public void NotUnderACautionWhileTheSafetyCarIsStillPickingUpTheLeader()
    {
        Assert.IsFalse(Offered(phase: "Caution", leaderCaught: false, crossedLine: true),
            "offered before the leader had caught the safety car");
    }

    [Test]
    public void NotUnderACautionUntilThePlayerHasCrossedTheLine()
    {
        Assert.IsFalse(Offered(phase: "Caution", leaderCaught: true, crossedLine: false),
            "offered before the player had crossed the line under the caution");
        Assert.IsFalse(Offered(phase: "Caution"), "offered as the caution came out");
    }

    [Test]
    public void CautionSkipEndsAtTheRestartCloseUpOrWhenThePlayerDrives()
    {
        Assert.IsFalse(Offered(phase: "Caution", leaderCaught: true, crossedLine: true, closingUp: true),
            "skipping ran on past the restart close-up");
        Assert.IsFalse(Offered(phase: "Caution", leaderCaught: true, crossedLine: true, auto: false),
            "the caution skip is an Auto-only option");
        Assert.IsFalse(Offered(phase: "Caution", leaderCaught: true, crossedLine: true, multiplayer: true),
            "a co-op guest can't be fast-forwarded by the host's clock");
        Assert.IsFalse(Offered(phase: "Caution", leaderCaught: true, crossedLine: true, onFoot: true),
            "offered with the player out of the car");
    }

    [Test]
    public void CautionSkipEndsWhenThePlayerTakesTheChequer()
    {
        Assert.IsFalse(Offered(phase: "Caution", leaderCaught: true, crossedLine: true, finished: true),
            "a race that ended under caution kept fast-forwarding past the player's finish");
    }
}
