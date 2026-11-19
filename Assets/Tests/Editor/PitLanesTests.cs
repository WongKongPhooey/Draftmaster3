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

    // Leaving the box (ExitLane): the far lane when it is free, the box-side lane when only the far one has a car in
    // it, and wait in the box while both do.
    const float ExitSecs = 2f, ExitMinM = 14f, ExitAhead = 12f;
    static int Exit(float myD, params PitLanes.Car[] cars)
        => PitLanes.ExitLane(myD, new List<PitLanes.Car>(cars), Width, Park, ExitSecs, ExitMinM, ExitAhead);

    [Test]
    public void LeavingTheBox_TheFarLane_WhenNobodyIsInEither()
    {
        Assert.AreEqual(PitLanes.FarSide, Exit(200f));
        Assert.AreEqual(PitLanes.FarSide, Exit(200f, At(190f, Park, -1, 0f), At(210f, Park, -1, 0f)),
                        "cars in the boxes either side are not in a lane");
        Assert.AreEqual(PitLanes.FarSide, Exit(200f, At(260f, -2.5f, PitLanes.FarSide, 50f)), "a car well down the lane, going away");
    }

    [Test]
    public void LeavingTheBox_TheBoxSideLane_WhenACarIsInTheFarLane()
    {
        Assert.AreEqual(PitLanes.BoxSide, Exit(200f, At(201f, -2.5f, PitLanes.FarSide, 40f)), "alongside");
        Assert.AreEqual(PitLanes.BoxSide, Exit(200f, At(208f, -2.5f, PitLanes.FarSide, 0f)), "stopped just ahead, in the way of the pull-out");
        Assert.AreEqual(PitLanes.BoxSide, Exit(200f, At(170f, -2.5f, PitLanes.FarSide, 50f)), "coming down the far lane at pit speed, 30 m back");
        Assert.AreEqual(PitLanes.BoxSide, Exit(200f, At(195f, -2.0f, -1, 30f)), "judged by where it is across the road");
        Assert.AreEqual(PitLanes.BoxSide, Exit(200f, At(209f, Park - 1f, PitLanes.FarSide, 5f)),
                        "the car in the next box is already pulling out into the far lane");
    }

    [Test]
    public void LeavingTheBox_WaitForAGap_WhenBothLanesHaveACar()
    {
        Assert.AreEqual(-1, Exit(200f, At(201f, -2.5f, PitLanes.FarSide, 40f), At(199f, 2.5f, PitLanes.BoxSide, 40f)));
        Assert.AreEqual(-1, Exit(200f, At(175f, -2.5f, PitLanes.FarSide, 50f), At(180f, 2.5f, PitLanes.BoxSide, 50f)),
                        "both lanes coming down behind");
        // The far lane is across the box-side lane: a car there coming past is waited for too.
        Assert.AreEqual(-1, Exit(200f, At(180f, 2.5f, PitLanes.BoxSide, 50f)));
    }

    [Test]
    public void LeavingTheBox_ACarBehindThatWillNotReachMe_IsNoReasonToWait()
    {
        // Braking down the box-side lane for a box short of mine: it stops before it gets here.
        Assert.AreEqual(PitLanes.FarSide, Exit(200f, new PitLanes.Car { distance = 185f, lateral = 2.5f, lane = PitLanes.BoxSide, mph = 25f, stopLine = 190f }));
        // ...but one braking for a box beyond mine comes past.
        Assert.AreEqual(-1, Exit(200f, new PitLanes.Car { distance = 185f, lateral = 2.5f, lane = PitLanes.BoxSide, mph = 25f, stopLine = 230f }));
        // Stopped or queuing behind: it waits for me.
        Assert.AreEqual(PitLanes.FarSide, Exit(200f, At(190f, -2.5f, PitLanes.FarSide, 0f)));
        // A slow car far enough back to be more than the headway away.
        Assert.AreEqual(PitLanes.FarSide, Exit(200f, At(180f, -2.5f, PitLanes.FarSide, 10f)), "20 m back at 10 mph is ~4.5 s away");
        Assert.AreEqual(PitLanes.BoxSide, Exit(200f, At(190f, -2.5f, PitLanes.FarSide, 10f)), "inside the 14 m floor");
    }

    [Test]
    public void LeavingTheBox_OnAOneLaneRoad_IsTheMiddle()
        => Assert.AreEqual(PitLanes.BoxSide,
                           PitLanes.ExitLane(200f, new List<PitLanes.Car> { At(201f, 0f, -1, 40f) }, 6f, Park, ExitSecs, ExitMinM, ExitAhead));

    [Test]
    public void LaneClear_LooksBehindAndAhead()
    {
        var cars = new List<PitLanes.Car> { At(91f, 2.5f, PitLanes.BoxSide) };
        Assert.IsFalse(PitLanes.LaneClear(PitLanes.BoxSide, 100f, cars, Width, Park, 10f, 12f), "9 m behind");
        Assert.IsTrue(PitLanes.LaneClear(PitLanes.BoxSide, 100f, cars, Width, Park, 8f, 12f));
        Assert.IsTrue(PitLanes.LaneClear(PitLanes.FarSide, 100f, cars, Width, Park, 10f, 12f), "the other lane");
    }
}
