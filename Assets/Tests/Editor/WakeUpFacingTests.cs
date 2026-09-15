using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// EditMode coverage for which way the driver is pointing when the career's first morning hands control
// over — the turn at the end of WakeUpSequence, after the alarm and the fade.
//
// The beat itself is a coroutine on a spawned player and can only be watched in play mode, so what is
// pinned here is the maths under it: the rotation that stands the body up facing a given world direction,
// in the same sprite convention OnFootController turns everybody else by. The reason it matters is the
// first test below — a body left in the pose it was instantiated with faces south, into the back of the
// motorhome, rather than north toward the way out.
//
// Assembly-CSharp can't be referenced by an asmdef, so the type is reached by reflection the same way
// PaddockLotAreaTests reaches the lot.
public class WakeUpFacingTests
{
    static readonly System.Type SequenceType = System.Type.GetType("WakeUpSequence, Assembly-CSharp");
    static readonly System.Type SettingsType = System.Type.GetType("WakeUpSequence+Settings, Assembly-CSharp");

    // The on-foot rigs are drawn facing -Y, which is what OnFootController's +90 offset corrects for.
    const float SpriteFacingOffsetDeg = 90f;

    static Quaternion UprightFacing(Vector2 facing, Quaternion spawned)
        => (Quaternion)SequenceType
            .GetMethod("UprightFacing", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { facing, SpriteFacingOffsetDeg, spawned });

    // Where the sprite's drawn face ends up once that rotation is on the body.
    static Vector2 DrawnFacing(Quaternion rotation) => rotation * Vector3.down;

    static void AssertFacing(Vector2 actual, Vector2 expected, string what)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f), what + " (x)");
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f), what + " (y)");
    }

    [Test]
    public void SpawnedUnturnedTheDriverFacesSouth()
    {
        // Why the turn exists at all: PitLaneStart instantiates the player with no rotation, and the rigs
        // are drawn facing down the screen, so waking up without being turned is waking up facing away
        // from the door.
        AssertFacing(DrawnFacing(Quaternion.identity), Vector2.down, "an unrotated on-foot rig faces south");
    }

    [Test]
    public void GettingUpFacesNorth()
    {
        AssertFacing(DrawnFacing(UprightFacing(Vector2.up, Quaternion.identity)), Vector2.up,
                     "standing up asked to face north should face north");
    }

    [Test]
    public void TheTurnIsIndependentOfTheSpawnPose()
    {
        // However the body was lying or was spawned, north is north: the pose is replaced, not added to.
        AssertFacing(DrawnFacing(UprightFacing(Vector2.up, Quaternion.Euler(0f, 0f, 143f))), Vector2.up,
                     "a body spawned at an angle should still stand up facing north");
    }

    [Test]
    public void AnyDirectionIsHonoured()
    {
        // The door is not north at every venue — a rig parked facing another way retunes the direction,
        // and the same maths has to serve it.
        AssertFacing(DrawnFacing(UprightFacing(Vector2.right, Quaternion.identity)), Vector2.right,
                     "east");
        AssertFacing(DrawnFacing(UprightFacing(new Vector2(0f, -4f), Quaternion.identity)), Vector2.down,
                     "south, from an unnormalised direction");
    }

    [Test]
    public void NoDirectionKeepsTheSpawnPose()
    {
        var spawned = Quaternion.Euler(0f, 0f, 37f);
        Assert.That(Quaternion.Angle(UprightFacing(Vector2.zero, spawned), spawned), Is.LessThan(0.001f),
                    "asked to face nothing, the body should keep the pose it was spawned with");
    }

    [Test]
    public void TheOpeningFacesNorthByDefault()
    {
        object defaults = SettingsType
            .GetProperty("Default", BindingFlags.Public | BindingFlags.Static)
            .GetValue(null);
        var facing = (Vector2)SettingsType.GetField("facing").GetValue(defaults);

        AssertFacing(facing, Vector2.up, "the wake-up's shipped facing");
    }
}
