using Draftmaster.Tracks;
using NUnit.Framework;
using UnityEngine;

// PitChordFit: pit road across the infield of any lap shape, fitted to the road rather than to the oval formula.
public class PitChordFitTests
{
    // A counter-clockwise stadium oval: 800 m straights, 200 m radius ends, start/finish mid front stretch.
    const float Straight = 800f, Radius = 200f;
    static float Lap => 2f * Straight + 2f * Mathf.PI * Radius;

    // Distance 0 is the middle of the front stretch, which runs +x along y = 0; the infield is +y.
    static PitChordFit.Pose PoseAt(float d)
    {
        d = PitChordFit.Fold(d, Lap);
        float half = Straight * 0.5f, arc = Mathf.PI * Radius;
        if (d < half) return new PitChordFit.Pose(new Vector2(d, 0f), 0f);
        d -= half;
        if (d < arc)
        {
            float a = d / Radius;
            return new PitChordFit.Pose(new Vector2(half + Radius * Mathf.Sin(a), Radius - Radius * Mathf.Cos(a)),
                                        a * Mathf.Rad2Deg);
        }
        d -= arc;
        if (d < Straight) return new PitChordFit.Pose(new Vector2(half - d, 2f * Radius), 180f);
        d -= Straight;
        if (d < arc)
        {
            float a = d / Radius;
            return new PitChordFit.Pose(new Vector2(-half - Radius * Mathf.Sin(a), Radius + Radius * Mathf.Cos(a)),
                                        180f + a * Mathf.Rad2Deg);
        }
        d -= arc;
        return new PitChordFit.Pose(new Vector2(-half + d, 0f), 360f);
    }

    [Test]
    public void ConnectJoinsBothPosesTangentially()
    {
        var a = PoseAt(-500f);
        var b = PoseAt(500f);
        Assert.IsTrue(PitChordFit.TryConnect(a, b, 100f, 1f, out float turnA, out float straight, out float turnB));
        var road = new PitChordFit.Road { radius = 100f, entryTurnDeg = turnA, straightLength = straight, exitTurnDeg = turnB };

        Vector2 end = PitChordFit.PointOnRoad(a, road, 1f, road.Length);
        Assert.Less(Vector2.Distance(end, b.position), 0.05f, "the road ends where it rejoins");
        Assert.Less(Mathf.Abs(Mathf.DeltaAngle(a.headingDeg + turnA + turnB, b.headingDeg)), 0.05f, "and joins tangentially");
    }

    // A mapped pit lane 30 m inside the front stretch: pit road's straight lies on it and both arcs meet the
    // racing line tangentially.
    [Test]
    public void FitToLinePutsTheStraightOnTheMappedLane()
    {
        // Straight front stretch here, so the arcs have to start in the corners either side.
        Vector2 lanePoint = new Vector2(-200f, 30f), laneDir = Vector2.right;
        Assert.IsTrue(PitChordFit.TryFitToLine(PoseAt, Lap, 1f, lanePoint, laneDir, Lap - 450f, 450f, 400f,
                                               new[] { 150f, 100f }, out var road));

        var entry = PoseAt(road.entryDistance);
        Vector2 straightStart = PitChordFit.PointOnRoad(entry, road, 1f, road.EntryArc);
        Vector2 straightEnd = PitChordFit.PointOnRoad(entry, road, 1f, road.EntryArc + road.straightLength);
        Assert.AreEqual(30f, straightStart.y, 0.1f, "straight starts on the lane");
        Assert.AreEqual(30f, straightEnd.y, 0.1f, "and ends on it");
        Assert.Greater(straightEnd.x, straightStart.x, "running the way the cars do");

        Vector2 end = PitChordFit.PointOnRoad(entry, road, 1f, road.Length);
        Assert.Less(Vector2.Distance(end, PoseAt(road.exitDistance).position), 0.05f, "rejoins exactly");
    }

    [Test]
    public void FitHoldsTheStraightAtTheClearanceOnTheInfieldSide()
    {
        Assert.IsTrue(PitChordFit.TryFit(PoseAt, Lap, 0f, 1f, Radius * 0.5f, 40f, out var road));

        var entry = PoseAt(road.entryDistance);
        Vector2 end = PitChordFit.PointOnRoad(entry, road, 1f, road.Length);
        Assert.Less(Vector2.Distance(end, PoseAt(road.exitDistance).position), 0.05f, "rejoins exactly");

        Assert.AreEqual(40f, road.nearestClearance, 3f, "the straight sits the asked-for distance in");
        Vector2 mid = PitChordFit.PointOnRoad(entry, road, 1f, road.EntryArc + road.straightLength * 0.5f);
        Assert.Greater(mid.y, 0f, "on the infield side of the front stretch");
        Assert.Less(road.entryDistance, Lap, "folded into the lap");
        Assert.Greater(road.entryDistance, Lap * 0.5f, "leaves before the start/finish line");
        Assert.Less(road.exitDistance, Lap * 0.5f, "rejoins after it");
    }
}
