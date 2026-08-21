using NUnit.Framework;
using UnityEngine;
using Draftmaster.Sim;

// EditMode coverage for the driving camera's lean and shake maths. The camera itself can only be judged by
// driving it, so these pin the numbers: which way the lean goes, that it stays inside its limits, and that
// trauma actually decays instead of leaving the view shaking for the rest of the race.
public class CameraFeelTests
{
    const float RefG = 1.1f;
    const float LongLean = 1.6f;
    const float LatLean = 1.2f;

    [Test]
    public void LeanPushesAheadUnderPowerAndDropsBackUnderBraking()
    {
        float power = CameraFeel.LeanOffset(8f, 0f, RefG, LongLean, LatLean).x;
        float braking = CameraFeel.LeanOffset(-8f, 0f, RefG, LongLean, LatLean).x;
        float coasting = CameraFeel.LeanOffset(0f, 0f, RefG, LongLean, LatLean).x;

        Assert.Greater(power, 0f, "accelerating should push the camera ahead of the nose");
        Assert.Less(braking, 0f, "braking should drop the camera back behind the nose");
        Assert.AreEqual(0f, coasting, 1e-5f, "no longitudinal load should mean no longitudinal lean");
    }

    [Test]
    public void LeanSlidesTowardTheInsideOfTheCorner()
    {
        // Lateral accel follows the vehicle body convention: + is to the left.
        float leftHander = CameraFeel.LeanOffset(0f, 9f, RefG, LongLean, LatLean).y;
        float rightHander = CameraFeel.LeanOffset(0f, -9f, RefG, LongLean, LatLean).y;

        Assert.Greater(leftHander, 0f, "a left-hander should slide the camera to the car's left");
        Assert.Less(rightHander, 0f, "a right-hander should slide the camera to the car's right");
        Assert.AreEqual(leftHander, -rightHander, 1e-5f, "the lean should be symmetric");
    }

    [Test]
    public void LeanNeverExceedsItsConfiguredMetres()
    {
        foreach (float accel in new[] { -200f, -30f, -1f, 0f, 1f, 30f, 200f })
        {
            var lean = CameraFeel.LeanOffset(accel, accel, RefG, LongLean, LatLean);
            Assert.LessOrEqual(Mathf.Abs(lean.x), LongLean + 1e-4f, $"longitudinal lean ran away at {accel} m/s²");
            Assert.LessOrEqual(Mathf.Abs(lean.y), LatLean + 1e-4f, $"lateral lean ran away at {accel} m/s²");
        }
    }

    [Test]
    public void RollFollowsLateralLoadAndStaysInsideItsLimit()
    {
        Assert.AreEqual(0f, CameraFeel.RollDegrees(0f, RefG, 1.8f), 1e-5f, "straight running should sit square");
        Assert.AreNotEqual(0f, CameraFeel.RollDegrees(9f, RefG, 1.8f), "a loaded corner should roll the view");
        Assert.AreEqual(-CameraFeel.RollDegrees(9f, RefG, 1.8f), CameraFeel.RollDegrees(-9f, RefG, 1.8f), 1e-5f,
                        "roll should mirror between left and right handers");
        Assert.LessOrEqual(Mathf.Abs(CameraFeel.RollDegrees(500f, RefG, 1.8f)), 1.8f + 1e-4f, "roll blew past its cap");
    }

    [Test]
    public void LeanFadesOutAtACrawl()
    {
        Assert.AreEqual(0f, CameraFeel.LeanFade(0f, 12f), 1e-5f, "a stopped car should not lean");
        Assert.AreEqual(0.5f, CameraFeel.LeanFade(6f, 12f), 1e-5f, "the fade should ramp linearly");
        Assert.AreEqual(1f, CameraFeel.LeanFade(180f, 12f), 1e-5f, "racing speed should get the full lean");
    }

    [Test]
    public void ApproachConvergesAndIsFrameRateIndependent()
    {
        // Same wall-clock second, different frame rates: the result should land in the same place.
        float slow = 0f, fast = 0f;
        for (int i = 0; i < 30; i++) slow = CameraFeel.Approach(slow, 1f, 5f, 1f / 30f);
        for (int i = 0; i < 240; i++) fast = CameraFeel.Approach(fast, 1f, 5f, 1f / 240f);

        Assert.AreEqual(slow, fast, 0.01f, "the lean should settle the same way at 30 and 240 fps");
        Assert.Greater(slow, 0.98f, "one second at 5 Hz response should be all but settled");
        Assert.AreEqual(0.5f, CameraFeel.Approach(0.5f, 1f, 5f, 0f), 1e-5f, "a zero-length step should change nothing");
    }

    [Test]
    public void TraumaBuildsClampsAndDecaysAwayCompletely()
    {
        float trauma = CameraFeel.AddTrauma(0f, 0.4f);
        Assert.AreEqual(0.4f, trauma, 1e-5f);

        Assert.AreEqual(1f, CameraFeel.AddTrauma(0.8f, 5f), 1e-5f, "trauma should saturate at 1");
        Assert.AreEqual(0f, CameraFeel.DecayTrauma(0.1f, 1.7f, 1f), 1e-5f, "trauma should never go negative");

        // A full-strength hit should be gone within a couple of seconds, not linger.
        trauma = 1f;
        for (int i = 0; i < 120; i++) trauma = CameraFeel.DecayTrauma(trauma, 1.7f, 1f / 60f);
        Assert.AreEqual(0f, trauma, 1e-5f, "trauma should be spent two seconds after a maximum hit");
    }

    [Test]
    public void ShakeIsQuadraticSoLightContactsStayQuiet()
    {
        Assert.AreEqual(0f, CameraFeel.ShakeAmount(0f), 1e-5f);
        Assert.AreEqual(1f, CameraFeel.ShakeAmount(1f), 1e-5f);
        Assert.AreEqual(0.25f, CameraFeel.ShakeAmount(0.5f), 1e-5f, "half trauma should shake at a quarter strength");
        Assert.AreEqual(1f, CameraFeel.ShakeAmount(3f), 1e-5f, "out-of-range trauma should clamp");
    }

    [Test]
    public void ShakeStaysInsideItsAmplitudeAndOnlyRunsWhileTraumatised()
    {
        Assert.AreEqual(Vector2.zero, CameraFeel.ShakeOffset(0f, 0.45f, 12.3f, 26f, 7),
                        "no trauma should mean a perfectly still camera");
        Assert.AreEqual(0f, CameraFeel.ShakeRoll(0f, 1.4f, 12.3f, 26f, 7), 1e-5f);

        for (float t = 0f; t < 2f; t += 1f / 60f)
        {
            var offset = CameraFeel.ShakeOffset(1f, 0.45f, t, 26f, 7);
            Assert.LessOrEqual(Mathf.Abs(offset.x), 0.45f + 1e-4f, $"shake overshot on x at t={t}");
            Assert.LessOrEqual(Mathf.Abs(offset.y), 0.45f + 1e-4f, $"shake overshot on y at t={t}");
            Assert.LessOrEqual(Mathf.Abs(CameraFeel.ShakeRoll(1f, 1.4f, t, 26f, 7)), 1.4f + 1e-4f,
                               $"roll shake overshot at t={t}");
        }
    }

    [Test]
    public void ShakeActuallyMovesAndTheTwoAxesAreNotTheSameNoiseRow()
    {
        // Perlin noise is flat along integer lattice lines and mirrored about them — a badly chosen channel
        // row would leave the shake either dead still or moving both axes in lockstep (a diagonal twitch).
        float minX = float.MaxValue, maxX = float.MinValue, biggestSplit = 0f;
        for (float t = 0f; t < 1f; t += 1f / 120f)
        {
            var o = CameraFeel.ShakeOffset(1f, 0.45f, t, 26f, 7);
            minX = Mathf.Min(minX, o.x);
            maxX = Mathf.Max(maxX, o.x);
            biggestSplit = Mathf.Max(biggestSplit, Mathf.Abs(o.x - o.y));
        }

        Assert.Greater(maxX - minX, 0.1f, "the shake barely moved — check the noise sampling");
        Assert.Greater(biggestSplit, 0.05f, "both axes are riding the same noise row");
    }
}
