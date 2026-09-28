using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// The sponsor's guests round the winner's circle look in at the middle of it.
//
// They were spawned unrotated, so all twenty faced down the screen whatever side of the chequers they were
// on. The paper-doll art faces local -Y, so a guest is looking at the centre when their rotated -Y points
// back at the origin of the circle. Checked against the real layout (GuestSpot, fixed seed) rather than a
// made-up ring, so a change to where they stand is covered too. Each guest is then turned a few degrees off
// dead-centre so the crowd does not read as drilled — vaguely towards the middle, never away from it.
//
// Assembly-CSharp can't be referenced by an asmdef, so the methods are reached by reflection the same way
// WeekendVenueClearanceTests reaches its own.
public class SponsorGuestFacingTests
{
    static readonly System.Type SitesType = System.Type.GetType("WeekendVenueSites, Assembly-CSharp");

    static MethodInfo Method(string name)
    {
        Assert.IsNotNull(SitesType, "WeekendVenueSites is missing from Assembly-CSharp.");
        var m = SitesType.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(m, $"WeekendVenueSites.{name} is gone — the winner's circle has been rewritten.");
        return m;
    }

    [Test]
    public void EveryGuestFacesTheMiddleOfTheCircle()
    {
        int count = (int)SitesType.GetField("GuestCount", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                                  .GetRawConstantValue();
        var spotOf = Method("GuestSpot");
        var facingOf = Method("GuestFacing");
        var rng = new System.Random(4821);

        for (int i = 0; i < count; i++)
        {
            var spot = (Vector2)spotOf.Invoke(null, new object[] { i, rng });
            var rot = (Quaternion)facingOf.Invoke(null, new object[] { spot, 0f });

            Vector2 looking = rot * Vector3.down;
            Vector2 toCentre = (-spot).normalized;
            Assert.Greater(Vector2.Dot(looking, toCentre), 0.999f,
                           $"Guest {i} at {spot} looks {looking}, not at the middle ({toCentre}).");
        }
    }

    [Test]
    public void AGuestBelowTheMiddleIsLeftFacingUpTheScreen()
    {
        var rot = (Quaternion)Method("GuestFacing").Invoke(null, new object[] { new Vector2(0f, -3f), 0f });
        Vector2 looking = rot * Vector3.down;
        Assert.Greater(Vector2.Dot(looking, Vector2.up), 0.999f);
    }

    [Test]
    public void JitterTurnsAGuestByThatManyDegreesOffCentre()
    {
        var spot = new Vector2(0f, -3f);
        var rot = (Quaternion)Method("GuestFacing").Invoke(null, new object[] { spot, 15f });
        Vector2 looking = rot * Vector3.down;
        Assert.AreEqual(15f, Vector2.Angle(looking, (-spot).normalized), 0.01f);
    }

    [Test]
    public void TheJitterIsSlightButNotZero()
    {
        float limit = (float)SitesType.GetField("GuestFacingJitter", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                                      .GetRawConstantValue();
        Assert.Greater(limit, 0f, "Guests should not all stare at exactly the same point.");
        Assert.LessOrEqual(limit, 30f, "More than 30 degrees off and a guest is no longer watching the shoot.");
    }
}
