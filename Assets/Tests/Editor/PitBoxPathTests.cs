using Draftmaster.Sim;
using NUnit.Framework;
using UnityEngine;

// The move between the pit lane's driving line and a car's box on the wall-side strip during a pit stop
// (PitBoxPath, driven by PitStopController): an S-curve over distance rolled like the formation pull-out, as gentle
// as the cars parked in the neighbouring boxes allow.
public class PitBoxPathTests
{
    const float Dt = 0.02f;
    const float BoxLateral = 8f;    // box strip centre off the pit centerline (pit half-width + half the strip)
    const float Spacing = 10f;      // PitLane.MaxSpacing
    const float CarLength = 4.8f;   // VehicleCollision.halfExtents.y * 2
    const float Clearance = 2.5f;   // car width 2 + PitStopController.boxPathClearance
    const float Gentle = 12f;       // PitStopController.boxPathAngleDeg
    const float MinLen = 8f;        // PitStopController.boxPathMinLength
    const float EndMargin = 1f;     // PitStopController.BoxPathEndMargin

    static float PathAngle(float lateralStep, float speedMps)
        => Mathf.Abs(Mathf.Atan2(lateralStep / Dt, Mathf.Max(speedMps, 0.5f)) * Mathf.Rad2Deg);

    [Test]
    public void InverseSmoothStepUndoesTheSmoothStep()
    {
        for (float t = 0f; t <= 1f; t += 0.05f)
        {
            float s = t * t * (3f - 2f * t);
            Assert.AreEqual(t, PitBoxPath.InverseSmoothStep(s), 1e-4f, $"t = {t}");
        }
    }

    [Test]
    public void PullingOutClearsTheCarInTheNextBox()
    {
        float len = PitBoxPath.Length(BoxLateral, Spacing, CarLength, Clearance, Gentle, MinLen);
        // The nose reaches the tail of the car parked in the next box down after rolling spacing - car length.
        float moved = BoxLateral - PitBoxPath.Lateral(BoxLateral, 0f, Spacing - CarLength, len);
        Assert.GreaterOrEqual(moved, Clearance - 1e-3f);
        Assert.AreEqual(0f, PitBoxPath.Lateral(BoxLateral, 0f, len, len), 1e-5f, "on the driving line at the end");
        // Steep only as far as it has to be: well under the 50+ degrees a fixed lateral rate swung at a crawl.
        Assert.Less(PitBoxPath.PeakAngleDeg(BoxLateral, len), 45f);
    }

    [Test]
    public void CuttingInStaysClearOfTheCarInTheBoxBefore()
    {
        float len = PitBoxPath.Length(BoxLateral, Spacing, CarLength, Clearance, Gentle, MinLen, 0f, EndMargin);
        // Coming in, the car is alongside the box before its own until its centre is spacing - car length from its
        // own box's stop line; the curve ends EndMargin short of that line.
        float travelledThen = len - (Spacing - CarLength - EndMargin);
        float lat = PitBoxPath.Lateral(0f, BoxLateral, travelledThen, len);
        Assert.GreaterOrEqual(BoxLateral - lat, Clearance - 1e-3f);
        Assert.AreEqual(BoxLateral, PitBoxPath.Lateral(0f, BoxLateral, len, len), 1e-5f, "on the strip at the end");
    }

    [TestCase(0.8f)]   // crawling out of the box
    [TestCase(4.5f)]   // 10 mph
    [TestCase(20f)]    // pit limit
    public void PathAngleIsTheSameAtAnySpeedAndSquaresUp(float speedMps)
    {
        float len = PitBoxPath.Length(BoxLateral, Spacing, CarLength, Clearance, Gentle, MinLen);
        float peakAllowed = PitBoxPath.PeakAngleDeg(BoxLateral, len);
        float s = 0f, prev = BoxLateral, peak = 0f, first = -1f, last = 0f;
        while (s < len + 1f)
        {
            s += speedMps * Dt;
            float lat = PitBoxPath.Lateral(BoxLateral, 0f, s, len);
            float a = PathAngle(lat - prev, speedMps);
            if (first < 0f) first = a;
            if (lat != prev) last = a;
            peak = Mathf.Max(peak, a);
            prev = lat;
        }
        Assert.LessOrEqual(peak, peakAllowed + 0.1f, $"peak {peak:0.0} deg at {speedMps} m/s");
        Assert.Less(first, 3f, "turns out of the box from straight");
        Assert.Less(last, 3f, "straight on the driving line, no snap");
    }

    [Test]
    public void StoppedPartWayHoldsTheCarWhereItIs()
    {
        float len = PitBoxPath.Length(BoxLateral, Spacing, CarLength, Clearance, Gentle, MinLen);
        float a = PitBoxPath.Lateral(BoxLateral, 0f, len * 0.4f, len);
        Assert.AreEqual(a, PitBoxPath.Lateral(BoxLateral, 0f, len * 0.4f, len));
    }

    [Test]
    public void LengthLimits()
    {
        // Nothing to fit round (boxes far apart, a small move): no steeper than the gentle angle.
        float gentle = 1.5f * 2f / Mathf.Tan(Gentle * Mathf.Deg2Rad);
        Assert.AreEqual(gentle, PitBoxPath.Length(2f, 100f, CarLength, 0.5f, Gentle, MinLen), 1e-3f);
        // A ladder packed tighter than a car length has no gap: the shortest allowed.
        Assert.AreEqual(MinLen, PitBoxPath.Length(BoxLateral, 4.5f, CarLength, Clearance, Gentle, MinLen), 1e-4f);
        // Never shorter than the floor, and capped by the lane left to the exit.
        Assert.GreaterOrEqual(PitBoxPath.Length(BoxLateral, 6f, CarLength, Clearance, Gentle, MinLen), MinLen);
        Assert.AreEqual(MinLen, PitBoxPath.Length(BoxLateral, 100f, CarLength, Clearance, Gentle, MinLen, 3f), 1e-4f);
        Assert.AreEqual(20f, PitBoxPath.Length(BoxLateral, 100f, CarLength, Clearance, Gentle, MinLen, 20f), 1e-4f);
        // Nothing to move: the floor.
        Assert.AreEqual(MinLen, PitBoxPath.Length(0f, Spacing, CarLength, Clearance, Gentle, MinLen), 1e-4f);
    }
}
