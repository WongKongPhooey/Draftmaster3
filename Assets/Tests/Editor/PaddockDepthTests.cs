using System.Reflection;
using NUnit.Framework;

// Who covers whom in the paddock.
//
// This scene draws through the 3D URP renderer, where the DEPTH BUFFER decides what is in front of what. A
// sorting order on a mesh does not order it against a sprite, so the z these constants hold IS the answer,
// and the order they sit in is the whole rule:
//
//     player  in front of  everybody else  in front of  props  in front of  the tarmac
//
// Break that order and things disappear. The hospitality tent laid a canopy over the ground the player had
// to stand on and the player, spawned at tarmac depth, went under it — and the chequered floor that
// replaced the tent did it again, for the same reason, because the floor was not the problem. Every other
// body in the paddock is placed at PaddockPerson.GroundZ and was always in front, which is exactly why the
// player was the only one who vanished.
//
// Assembly-CSharp cannot be referenced from an asmdef, so the constants are read by reflection.
public class PaddockDepthTests
{
    static float Const(string type, string name)
    {
        var t = System.Type.GetType(type + ", Assembly-CSharp");
        Assert.IsNotNull(t, type + " is missing.");
        var field = t.GetField(name, BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(field, type + "." + name + " is missing.");
        return (float)field.GetRawConstantValue();
    }

    [Test]
    public void EverybodyOnFootIsDrawnInFrontOfTheGroundTheyWalkOn()
    {
        float floor = Const("PaddockProps", "FloorZ");
        float prop = Const("PaddockProps", "PropZ");
        float person = Const("PaddockPerson", "GroundZ");
        float player = Const("PaddockPerson", "PlayerZ");

        // Nearer the camera is more negative.
        Assert.Less(floor, 0f, "A prop laid on the ground has to be in front of the ground.");
        Assert.Less(prop, floor, "Things standing on the floor go in front of the floor.");
        Assert.Less(person, prop, "A person is in front of the props they walk between.");
        Assert.Less(player, person, "The player is in front of everybody: they are the one you must never lose.");
    }

    [Test]
    public void ThePlayerIsSpawnedAtThatDepthRatherThanOnTheTarmac()
    {
        // The rule is only worth having if the spawn obeys it. PitLaneStart puts the player wherever the
        // pit sample or the RV marker says, and both of those carry the track's own z — which is the
        // tarmac, behind every prop in the paddock.
        string source = System.IO.File.ReadAllText("Assets/Scripts/OnFoot/PitLaneStart.cs");
        StringAssert.Contains("PaddockPerson.PlayerZ", source,
                              "PitLaneStart must pull the spawned player forward to the on-foot depth.");
    }
}
