using Draftmaster.Sim;
using NUnit.Framework;
using UnityEngine;

// The formation pull-out off the pit-box strip (FormationLanes.PullOutLateral / PullOutLength). A kinematic car
// points down its own path, heading = atan(lateral rate / forward speed) (SplineDriver.Place), so these check the
// path angle a car filing out of its box would show, at a crawl, at pit speed and stopping and starting in the queue.
public class PitPullOutTests
{
    const float Dt = 0.02f;
    const float BoxLateral = 2.6f; // GridSpawner.pitBoxParkLateral

    // Heading off the lane (deg) for one step, the way SplineDriver.Place works it out.
    static float PathAngle(float lateralStep, float speedMps)
        => Mathf.Abs(Mathf.Atan2(lateralStep / Dt, Mathf.Max(speedMps, 0.5f)) * Mathf.Rad2Deg);

    [Test]
    public void StartsOnTheBoxAndEndsOnTheLine()
    {
        float len = FormationLanes.PullOutLength(BoxLateral, 8f, 0f, 8f);
        Assert.AreEqual(BoxLateral, FormationLanes.PullOutLateral(BoxLateral, 0f, len), 1e-5f);
        Assert.AreEqual(0f, FormationLanes.PullOutLateral(BoxLateral, len, len), 1e-5f);
        Assert.AreEqual(0f, FormationLanes.PullOutLateral(BoxLateral, len * 3f, len), 1e-5f);
        Assert.AreEqual(-BoxLateral * 0.5f, FormationLanes.PullOutLateral(-BoxLateral, len * 0.5f, len), 1e-4f);
    }

    [TestCase(1.3f)]   // 3 mph, where the old fixed-rate ease switched on at ~48 degrees
    [TestCase(4.5f)]   // 10 mph
    [TestCase(20f)]    // 45 mph pit limit
    public void PathAngleStaysUnderTheLimitAtAnySpeed(float speedMps)
    {
        const float maxAngle = 8f;
        float len = FormationLanes.PullOutLength(BoxLateral, maxAngle, 0f, 8f);
        float s = 0f, prevLat = BoxLateral, peak = 0f, firstStep = -1f, lastStep = 0f;
        while (s < len + 1f)
        {
            s += speedMps * Dt;
            float lat = FormationLanes.PullOutLateral(BoxLateral, s, len);
            float a = PathAngle(lat - prevLat, speedMps);
            if (firstStep < 0f) firstStep = a;
            if (lat != prevLat) lastStep = a;
            peak = Mathf.Max(peak, a);
            prevLat = lat;
        }
        Assert.LessOrEqual(peak, maxAngle + 0.05f, $"peak path angle {peak:0.00} deg at {speedMps} m/s");
        // Eases in and out: no step in heading as the car starts across or as it arrives on the line.
        Assert.Less(firstStep, 1f, "turns in from straight");
        Assert.Less(lastStep, 1f, "straightens up before the line");
    }

    [Test]
    public void StoppingInTheQueueHoldsTheCarWhereItIs()
    {
        float len = FormationLanes.PullOutLength(BoxLateral, 8f, 0f, 8f);
        float half = FormationLanes.PullOutLateral(BoxLateral, len * 0.5f, len);
        // Stopped part-way: distance rolled doesn't change, so neither does the lateral - no sideways creep at rest.
        Assert.AreEqual(half, FormationLanes.PullOutLateral(BoxLateral, len * 0.5f, len));
    }

    [Test]
    public void ShortensToFitTheLaneLeftButNotBelowTheFloor()
    {
        float ideal = FormationLanes.PullOutLength(BoxLateral, 8f, 0f, 8f);
        Assert.AreEqual(1.5f * BoxLateral / Mathf.Tan(8f * Mathf.Deg2Rad), ideal, 1e-3f);
        Assert.AreEqual(15f, FormationLanes.PullOutLength(BoxLateral, 8f, 15f, 8f), 1e-4f);
        Assert.AreEqual(8f, FormationLanes.PullOutLength(BoxLateral, 8f, 3f, 8f), 1e-4f);
        Assert.AreEqual(ideal, FormationLanes.PullOutLength(BoxLateral, 8f, 500f, 8f), 1e-4f);
    }
}
