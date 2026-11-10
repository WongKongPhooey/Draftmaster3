using System.Collections.Generic;
using Draftmaster.Sim;
using NUnit.Framework;

// Pit road as two lanes (PitLanes): lane 0 beside the boxes, lane 1 the far side. A car coming in alongside another
// takes the far lane; a car leaving its box pulls out into the far lane.
public class PitLanesTests
{
    const float Width = 10f;      // Watkins Glen's pit road, kerb to kerb
    const float Park = 8f;        // its box strip, centred 8 m to the right of the middle

    static PitLanes.Car At(float d, float lat, int lane = -1, float mph = 50f)
        => new PitLanes.Car { distance = d, lateral = lat, lane = lane, mph = mph };

    [Test]
    public void Lanes_SitAQuarterOfTheWidthEitherSide_TheBoxSideLaneNextToTheBoxes()
    {
        Assert.AreEqual(2.5f, PitLanes.Lateral(PitLanes.BoxSide, Width, Park), 1e-4f);
        Assert.AreEqual(-2.5f, PitLanes.Lateral(PitLanes.FarSide, Width, Park), 1e-4f);
        // Boxes on the left: the lanes swap sides.
        Assert.AreEqual(-2.5f, PitLanes.Lateral(PitLanes.BoxSide, Width, -Park), 1e-4f);
        Assert.AreEqual(2.5f, PitLanes.Lateral(PitLanes.FarSide, Width, -Park), 1e-4f);
    }

    [Test]
    public void ANarrowRoad_OrOneWithNoBoxStrip_IsOneLaneDownTheMiddle()
    {
        Assert.IsFalse(PitLanes.TwoLanes(6f, Park));
        Assert.AreEqual(0f, PitLanes.Lateral(PitLanes.FarSide, 6f, Park), 1e-4f);
        Assert.IsFalse(PitLanes.TwoLanes(Width, 0f), "no box strip: parked cars sit in the road");
        Assert.AreEqual(0f, PitLanes.Lateral(PitLanes.BoxSide, Width, 0f), 1e-4f);
        Assert.AreEqual(PitLanes.BoxSide, PitLanes.EntryLane(100f, 0f, new List<PitLanes.Car> { At(100f, 0f) }, 6f, Park, 10f, 12f));
    }

    [Test]
    public void ACarIsInTheLaneItSays_OrTheOneItIsIn_AndInNeitherOnTheBoxStrip()
    {
        Assert.AreEqual(PitLanes.FarSide, PitLanes.LaneOf(At(0f, 2.5f, PitLanes.FarSide), Width, Park), "its own word first");
        Assert.AreEqual(PitLanes.BoxSide, PitLanes.LaneOf(At(0f, 2.0f), Width, Park));
        Assert.AreEqual(PitLanes.FarSide, PitLanes.LaneOf(At(0f, -1.0f), Width, Park));
        Assert.AreEqual(-1, PitLanes.LaneOf(At(0f, Park), Width, Park), "parked in its box");
    }

    [Test]
    public void ComingIn_TheBoxSideLane_UnlessACarThereIsAlongside()
    {
        var empty = new List<PitLanes.Car>();
        Assert.AreEqual(PitLanes.BoxSide, PitLanes.EntryLane(100f, 0f, empty, Width, Park, 10f, 12f));

        var alongside = new List<PitLanes.Car> { At(102f, 2.5f, PitLanes.BoxSide) };
        Assert.AreEqual(PitLanes.FarSide, PitLanes.EntryLane(100f, 0f, alongside, Width, Park, 10f, 12f));

        var wellAhead = new List<PitLanes.Car> { At(160f, 2.5f, PitLanes.BoxSide) };
        Assert.AreEqual(PitLanes.BoxSide, PitLanes.EntryLane(100f, 0f, wellAhead, Width, Park, 10f, 12f), "room behind it");

        var parked = new List<PitLanes.Car> { At(101f, Park, -1, 0f) };
        Assert.AreEqual(PitLanes.BoxSide, PitLanes.EntryLane(100f, 0f, parked, Width, Park, 10f, 12f), "a car in its box is not in the lane");

        var both = new List<PitLanes.Car> { At(102f, 2.5f, PitLanes.BoxSide), At(98f, -2.5f, PitLanes.FarSide) };
        Assert.AreEqual(PitLanes.BoxSide, PitLanes.EntryLane(100f, 0f, both, Width, Park, 10f, 12f), "both taken: no better off across");
    }

    [Test]
    public void TwoCarsComingInSideBySide_EachKeepsItsOwnSide()
    {
        // Me on the far side (the boxes are to the right, +8), a car already in the box-side lane beside me.
        var beside = new List<PitLanes.Car> { At(101f, 2.5f, PitLanes.BoxSide) };
        Assert.AreEqual(PitLanes.FarSide, PitLanes.EntryLane(100f, -1.6f, beside, Width, Park, 10f, 12f));
        // Me on the box side, the other car came in on the far side.
        var farBeside = new List<PitLanes.Car> { At(101f, -2.5f, PitLanes.FarSide) };
        Assert.AreEqual(PitLanes.BoxSide, PitLanes.EntryLane(100f, 1.6f, farBeside, Width, Park, 10f, 12f));
        // On my own on the far side: the far lane for now (BoxSideOpen moves me over once in).
        Assert.AreEqual(PitLanes.FarSide, PitLanes.EntryLane(100f, -2f, new List<PitLanes.Car>(), Width, Park, 10f, 12f));
    }

    [Test]
    public void FromTheFarLane_OverToTheBoxSideOnlyOnceSettledIn_AndTheLaneIsClear()
    {
        var empty = new List<PitLanes.Car>();
        Assert.IsFalse(PitLanes.BoxSideOpen(110f, 10f, empty, Width, Park, 10f, 12f, 30f), "just come in");
        Assert.IsTrue(PitLanes.BoxSideOpen(135f, 35f, empty, Width, Park, 10f, 12f, 30f));
        var beside = new List<PitLanes.Car> { At(136f, 2.5f, PitLanes.BoxSide) };
        Assert.IsFalse(PitLanes.BoxSideOpen(135f, 35f, beside, Width, Park, 10f, 12f, 30f), "a car there");
    }

    [Test]
    public void LaneClear_LooksBehindAndAhead()
    {
        var cars = new List<PitLanes.Car> { At(91f, 2.5f, PitLanes.BoxSide) };
        Assert.IsFalse(PitLanes.LaneClear(PitLanes.BoxSide, 100f, cars, Width, Park, 10f, 12f), "9 m behind");
        Assert.IsTrue(PitLanes.LaneClear(PitLanes.BoxSide, 100f, cars, Width, Park, 8f, 12f));
        Assert.IsTrue(PitLanes.LaneClear(PitLanes.FarSide, 100f, cars, Width, Park, 10f, 12f), "the other lane");
    }
}
