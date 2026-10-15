using NUnit.Framework;
using UnityEngine;
using Draftmaster.Sim;

// EditMode coverage for the car-vs-wall response: a car scraping along the wall keeps its speed, a hard hit
// still bites and bounces, and nothing compounds over the many steps a scrape lasts.
public class WallContactTests
{
    const float Restitution = 0.25f, Friction = 0.2f, Threshold = 3f, Ramp = 10f;
    static readonly Vector2 Up = Vector2.up; // wall runs along x; -y is into it

    static Vector2 Hit(Vector2 v, out float dvn) => WallContact.Resolve(v, Up, Restitution, Friction, Threshold, Ramp, out dvn);

    [Test]
    public void MovingAwayOrAlong_IsUntouched()
    {
        var away = new Vector2(60f, 2f);
        Assert.AreEqual(away, Hit(away, out float dvn));
        Assert.AreEqual(0f, dvn);

        var along = new Vector2(60f, 0f);
        Assert.AreEqual(along, Hit(along, out dvn));
        Assert.AreEqual(0f, dvn);
    }

    [Test]
    public void GentleLean_DoesNotBounce()
    {
        var v = Hit(new Vector2(60f, -1f), out float dvn);
        Assert.AreEqual(0f, v.y, 1e-5f, "below the threshold the car stays on the wall");
        Assert.AreEqual(1f, dvn, 1e-5f);
    }

    [Test]
    public void HardHit_BouncesAtFullRestitution()
    {
        var v = Hit(new Vector2(30f, -20f), out float dvn);
        Assert.AreEqual(20f * Restitution, v.y, 1e-4f);
        Assert.AreEqual(20f * (1f + Restitution), dvn, 1e-4f);
    }

    [Test]
    public void BounceRampsIn_AboveThreshold()
    {
        Assert.AreEqual(0f, WallContact.EffectiveRestitution(Threshold, Restitution, Threshold, Ramp));
        Assert.AreEqual(Restitution * 0.5f, WallContact.EffectiveRestitution(Threshold + Ramp * 0.5f, Restitution, Threshold, Ramp), 1e-5f);
        Assert.AreEqual(Restitution, WallContact.EffectiveRestitution(Threshold + Ramp * 2f, Restitution, Threshold, Ramp), 1e-5f);
    }

    [Test]
    public void ScrubFollowsHowHardTheCarMeetsTheWall()
    {
        var light = Hit(new Vector2(60f, -1f), out _);
        var steep = Hit(new Vector2(60f, -15f), out _);
        Assert.Greater(light.x, 59.7f, "a light lean costs a fraction of a m/s");
        Assert.Less(steep.x, light.x, "a steep hit scrubs more");
        Assert.Greater(steep.x, 0f);
    }

    [Test]
    public void Friction_NeverReversesTheSlide()
    {
        var v = Hit(new Vector2(1f, -30f), out _);
        Assert.AreEqual(0f, v.x, 1e-5f);
    }

    // One second of scraping at 50 Hz, the tyres pushing the car ~0.5 m/s back into the wall every step (a
    // driver holding lock into it). The old fixed 0.94 per step left ~5% of the speed.
    [Test]
    public void ScrapeForOneSecond_KeepsMostOfTheSpeed()
    {
        var v = new Vector2(80f, 0f);
        for (int i = 0; i < 50; i++)
        {
            v.y -= 0.5f;
            v = Hit(v, out _);
            Assert.GreaterOrEqual(v.y, 0f);
            Assert.Less(v.y, 1e-4f, "a scrape never pings the car off the wall");
        }
        Assert.Greater(v.x, 80f * 0.85f);
    }
}
