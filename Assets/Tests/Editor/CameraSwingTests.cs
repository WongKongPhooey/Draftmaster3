using NUnit.Framework;
using UnityEngine;
using Draftmaster.Sim;

// EditMode coverage for the swing camera's hinge. The camera itself can only be judged by driving it, so
// these pin the things that make it either a chase camera or a turntable: that it points the right way at
// all, that it arrives late and swings past the mark, that it always takes the short way round, and that
// nothing — a spin, a dropped frame, a five-second hitch — can send it spinning off.
public class CameraSwingTests
{
    // DrivingCameraFeel's shipped defaults, so these say something about the camera actually driven.
    const float ResponseHz = 1f;
    const float Damping = 0.6f;
    const float MaxDegPerSecond = 220f;
    const float Frame = 1f / 60f;

    static void Run(ref float angle, ref float velocity, float target, float seconds,
                    float damping = Damping, float maxDegPerSecond = MaxDegPerSecond)
    {
        int frames = Mathf.RoundToInt(seconds / Frame);
        for (int i = 0; i < frames; i++)
            CameraSwing.Step(ref angle, ref velocity, target, ResponseHz, damping, maxDegPerSecond, Frame);
    }

    // The whole point of the target angle: roll the view by it and "up the screen" IS the way the car is
    // pointing. Checked as vectors rather than as numbers so the convention is asserted, not the arithmetic.
    [Test]
    public void TargetAnglePutsTheCarsNoseUpTheScreen()
    {
        foreach (float heading in new[] { 0f, 37f, 90f, 175f, -120f, 359f })
        {
            float theta = CameraSwing.TargetAngle(heading) * Mathf.Deg2Rad;
            var cameraUp = new Vector2(-Mathf.Sin(theta), Mathf.Cos(theta));
            var carForward = new Vector2(Mathf.Cos(heading * Mathf.Deg2Rad), Mathf.Sin(heading * Mathf.Deg2Rad));
            Assert.AreEqual(1f, Vector2.Dot(cameraUp, carForward), 1e-3f,
                            $"heading {heading} should end up straight up the screen");
        }
    }

    [Test]
    public void NormaliseFoldsIntoHalfTurns()
    {
        Assert.AreEqual(10f, CameraSwing.Normalise(370f), 1e-3f);
        Assert.AreEqual(-10f, CameraSwing.Normalise(350f), 1e-3f);
        Assert.AreEqual(0f, CameraSwing.Normalise(720f), 1e-3f);
    }

    [Test]
    public void SwingSettlesOnTheHeading()
    {
        float angle = 0f, velocity = 0f;
        Run(ref angle, ref velocity, 80f, 4f);

        Assert.AreEqual(0f, Mathf.DeltaAngle(angle, 80f), 0.5f, "the swing should arrive at the heading");
        Assert.AreEqual(0f, velocity, 1f, "and stop when it gets there");
    }

    // The momentum, which is what stops it feeling rigid: the view carries past the heading before settling.
    [Test]
    public void UnderDampedSwingCarriesPastTheHeading()
    {
        float angle = 0f, velocity = 0f, peak = 0f;
        for (int i = 0; i < 240; i++)
        {
            CameraSwing.Step(ref angle, ref velocity, 60f, ResponseHz, Damping, MaxDegPerSecond, Frame);
            peak = Mathf.Max(peak, angle);
        }

        Assert.Greater(peak, 60f, "an under-damped swing should overshoot the heading");
        Assert.Less(peak, 75f, "but only by a little — this is a camera, not a pendulum");
    }

    [Test]
    public void CriticalDampingDoesNotOvershoot()
    {
        float angle = 0f, velocity = 0f, peak = 0f;
        for (int i = 0; i < 240; i++)
        {
            CameraSwing.Step(ref angle, ref velocity, 60f, ResponseHz, 1f, MaxDegPerSecond, Frame);
            peak = Mathf.Max(peak, angle);
        }

        Assert.LessOrEqual(peak, 60.2f, "a critically damped swing should ease in without swinging past");
    }

    // Through a corner the heading is still moving, and the camera is supposed to trail it rather than be
    // welded to it — but not so far that the car is driving sideways across the screen.
    [Test]
    public void SwingTrailsATurningCarWithoutFallingBehindIt()
    {
        float angle = 0f, velocity = 0f, heading = 90f;
        const float YawRate = 45f;   // degrees per second, a brisk corner

        for (int i = 0; i < 180; i++)   // three seconds of steady turning
        {
            heading += YawRate * Frame;
            CameraSwing.Step(ref angle, ref velocity, CameraSwing.TargetAngle(heading),
                             ResponseHz, Damping, MaxDegPerSecond, Frame);
        }

        float lag = Mathf.DeltaAngle(angle, CameraSwing.TargetAngle(heading));
        Assert.Greater(lag, 1f, "the swing should trail the heading, not track it exactly");
        Assert.Less(lag, 20f, "but it must not be left pointing the wrong way down the road");
    }

    // At the wrap the short way round is two degrees forwards, not 358 degrees backwards.
    [Test]
    public void SwingTakesTheShortWayRoundTheWrap()
    {
        float angle = 170f, velocity = 0f;
        Run(ref angle, ref velocity, -170f, 0.1f);

        Assert.Greater(Mathf.DeltaAngle(170f, angle), 0f, "it should carry on through 180, not unwind");
        Assert.Less(Mathf.Abs(Mathf.DeltaAngle(angle, -170f)), 20f, "and it should be closing on the target");
    }

    // A spin turns the car far faster than anyone wants to watch the world turn.
    [Test]
    public void SwingRateIsCapped()
    {
        float angle = 0f, velocity = 0f, travelled = 0f;
        const float Cap = 60f;
        for (int i = 0; i < 60; i++)   // one second chasing a heading half a turn away
        {
            float before = angle;
            CameraSwing.Step(ref angle, ref velocity, 179f, ResponseHz, Damping, Cap, Frame);
            travelled += Mathf.Abs(Mathf.DeltaAngle(before, angle));
        }

        Assert.LessOrEqual(travelled, Cap * 1.05f, "the view must not turn faster than its cap");
        Assert.LessOrEqual(Mathf.Abs(velocity), Cap + 0.01f);
    }

    // One enormous frame — a scene load, an editor left unfocused — must not hand the spring free energy.
    [Test]
    public void LongFrameStaysStable()
    {
        float angle = 0f, velocity = 0f;
        CameraSwing.Step(ref angle, ref velocity, 120f, ResponseHz, Damping, MaxDegPerSecond, 5f);

        Assert.IsFalse(float.IsNaN(angle) || float.IsInfinity(angle), "the angle must stay a real number");
        Assert.LessOrEqual(Mathf.Abs(angle), 180f, "and stay inside a half turn");
        Assert.LessOrEqual(Mathf.Abs(velocity), MaxDegPerSecond + 0.01f);

        // And it must still settle afterwards rather than ring forever.
        Run(ref angle, ref velocity, 120f, 6f);
        Assert.AreEqual(0f, Mathf.DeltaAngle(angle, 120f), 0.5f);
    }

    [Test]
    public void ZeroOrNegativeDeltaTimeChangesNothing()
    {
        float angle = 12f, velocity = 3f;
        CameraSwing.Step(ref angle, ref velocity, 90f, ResponseHz, Damping, MaxDegPerSecond, 0f);
        CameraSwing.Step(ref angle, ref velocity, 90f, ResponseHz, Damping, MaxDegPerSecond, -1f);

        Assert.AreEqual(12f, angle, 1e-4f);
        Assert.AreEqual(3f, velocity, 1e-4f);
    }

    // Switching the swing off mid-race unwinds to square instead of snapping there.
    [Test]
    public void SwingUnwindsToSquareWhenSwitchedOff()
    {
        float angle = 90f, velocity = 0f;
        Run(ref angle, ref velocity, 0f, 0.25f);
        Assert.Less(Mathf.Abs(angle), 90f, "it should be on its way back");
        Assert.Greater(Mathf.Abs(angle), 1f, "but not have jumped there in a quarter of a second");

        Run(ref angle, ref velocity, 0f, 4f);
        Assert.IsTrue(CameraSwing.Settled(angle, velocity, 0f), $"should have settled square, sat at {angle}");
    }

    [Test]
    public void SettledOnlyWhenStoppedOnTheTarget()
    {
        Assert.IsTrue(CameraSwing.Settled(0f, 0f, 0f));
        Assert.IsFalse(CameraSwing.Settled(20f, 0f, 0f), "still pointing the wrong way");
        Assert.IsFalse(CameraSwing.Settled(0f, 40f, 0f), "on target but still turning");
    }
}
