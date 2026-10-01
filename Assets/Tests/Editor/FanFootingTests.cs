using NUnit.Framework;
using UnityEngine;
using Draftmaster.Fans;

// EditMode coverage for keeping pit-lane autograph fans off the track: where they stand, and that a step
// toward a player out on the tarmac never lands on it. The track here is a fake band — a pit lane along x,
// tarmac for |y| <= 5 plus a 6 m box lane on the +y side, as TrackBuilder lays them out.
public class FanFootingTests
{
    const float HalfWidth = 5f, BoxLane = 6f;
    static bool OnTrack(Vector2 p) => p.y >= -HalfWidth && p.y <= HalfWidth + BoxLane;

    [Test]
    public void StandLateral_ClearsPitAndBoxLane()
    {
        // The old 4 m default sits on a 10 m pit lane; the clamp has to push it past the box lane too.
        float lat = FanFooting.StandLateral(HalfWidth, BoxLane, wanted: 4f, clearance: 1.2f);
        Assert.AreEqual(HalfWidth + BoxLane + 1.2f, lat, 1e-4f);
        Assert.IsFalse(OnTrack(new Vector2(0f, lat)));
    }

    [Test]
    public void StandLateral_KeepsAWiderWantedFigure()
    {
        Assert.AreEqual(20f, FanFooting.StandLateral(HalfWidth, BoxLane, wanted: 20f, clearance: 1.2f), 1e-4f);
    }

    [Test]
    public void StandLateral_NoBoxLane_ClearsPitEdge()
    {
        Assert.AreEqual(HalfWidth + 1f, FanFooting.StandLateral(HalfWidth, 0f, wanted: 0f, clearance: 1f), 1e-4f);
    }

    [Test]
    public void Step_OffTrack_GoesStraight()
    {
        Vector2 next = FanFooting.Step(new Vector2(0f, 15f), Vector2.right, 1f, OnTrack);
        Assert.AreEqual(1f, next.x, 1e-4f);
        Assert.AreEqual(15f, next.y, 1e-4f);
    }

    [Test]
    public void Step_TowardTrack_NeverLandsOnIt()
    {
        // Player stood in the middle of the pit lane; fan walking straight at them from the garage side.
        Vector2 pos = new Vector2(0f, HalfWidth + BoxLane + 1.2f);
        Vector2 player = new Vector2(3f, 0f);
        for (int i = 0; i < 500; i++)
        {
            Vector2 dir = player - pos;
            if (dir.magnitude < 1.4f) break;
            pos = FanFooting.Step(pos, dir, 0.03f, OnTrack);
            Assert.IsFalse(OnTrack(pos), $"step {i} put the fan on the track at {pos}");
        }
    }

    [Test]
    public void Step_SlidesAlongTheEdgeToKeepUp()
    {
        // Player walking down the lane: the fan can't cross onto it but should track along the edge.
        Vector2 pos = new Vector2(0f, HalfWidth + BoxLane + 0.01f);
        Vector2 next = FanFooting.Step(pos, new Vector2(1f, -1f), 0.1f, OnTrack);
        Assert.IsFalse(OnTrack(next));
        Assert.Greater(next.x, pos.x);
    }

    [Test]
    public void Step_NowhereDry_StaysPut()
    {
        Vector2 pos = new Vector2(0f, 20f);
        Assert.AreEqual(pos, FanFooting.Step(pos, Vector2.down, 1f, _ => true));
    }
}
