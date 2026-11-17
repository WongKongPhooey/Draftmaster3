using NUnit.Framework;

// EditMode coverage for FormationSkip.Offered — when the "skip the pace lap" prompt is up (and, since a skip in
// progress stops the moment it isn't, when fast-forwarding ends).
//
// FormationSkip and RaceStart live in Assembly-CSharp, which an asmdef can't reference, so they're reached by
// reflection.
public class FormationSkipTests
{
    static System.Type T(string name) => System.Type.GetType(name + ", Assembly-CSharp");

    static object Phase(string name) => System.Enum.Parse(T("RaceStart+Phase") ?? T("RaceStart").GetNestedType("Phase"), name);

    static bool Offered(bool multiplayer = false, string phase = "Formation", bool closingUp = false, bool auto = true,
                        bool onFoot = false) =>
        (bool)T("FormationSkip").GetMethod("Offered")
            .Invoke(null, new object[] { multiplayer, Phase(phase), closingUp, auto, onFoot });

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
    public void OnlyOnTheFormationLap()
    {
        Assert.IsFalse(Offered(phase: "PreGrid"), "offered before the pace lap had begun");
        Assert.IsFalse(Offered(phase: "Green"), "offered in the race");
        Assert.IsFalse(Offered(phase: "Caution"), "offered behind a caution's safety car");
    }

    [Test]
    public void NotInMultiplayerOrOnFoot()
    {
        Assert.IsFalse(Offered(multiplayer: true), "a co-op guest can't be fast-forwarded by the host's clock");
        Assert.IsFalse(Offered(onFoot: true), "offered with the player out of the car");
    }
}
