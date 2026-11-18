using System.Collections.Generic;
using System.Linq;
using Draftmaster.Sim;
using NUnit.Framework;

// The restart order under a full-course caution (CautionOrder, run by CautionLineup): the running order at the flag;
// a car that stops is dropped from it and joins the back once it is back up to 30 mph; a car that pits loses its place
// and takes whatever place it is in on the road when it leaves pit road.
public class CautionOrderTests
{
    const float Racing = 60f;      // m/s - well above the rejoin speed
    const float Crawl = 8f;        // m/s (~18 mph) - moving, but not yet back up to 30 mph
    const float Stopped = 0f;

    static List<int> Restart(CautionOrder order, IEnumerable<int> cars) => cars.OrderBy(order.Key).ToList();

    [Test]
    public void TheOrderAtTheFlagIsTheRestartOrder()
    {
        var o = new CautionOrder();
        o.Freeze(new[] { 5, 3, 9, 1 });
        CollectionAssert.AreEqual(new[] { 5, 3, 9, 1 }, Restart(o, new[] { 1, 9, 3, 5 }));
    }

    [Test]
    public void TheThresholdsAreThirtyMphToRejoin()
    {
        var o = new CautionOrder();
        Assert.AreEqual(30f / 2.237f, o.RejoinMps, 0.01f);
        Assert.Less(o.StopMps, 2f, "stopped means stopped, not slow");
    }

    [Test]
    public void ACarThatStopsIsDroppedFromTheOrder_AndHoldsNoPlaceWhileItCrawls()
    {
        var o = new CautionOrder();
        o.Freeze(new[] { 1, 2, 3, 4 });
        o.Observe(2, false, Stopped, 900);
        Assert.IsFalse(o.IsQueued(2));
        Assert.IsTrue(o.IsDropped(2));
        Assert.IsTrue(o.IsCaught(2));
        CollectionAssert.AreEqual(new[] { 1, 3, 4, 2 }, Restart(o, new[] { 1, 2, 3, 4 }));

        o.Observe(2, false, Crawl, 905);   // going again, but not yet up to speed
        Assert.IsTrue(o.IsDropped(2), "still out of the order below 30 mph");
        CollectionAssert.AreEqual(new[] { 1, 3, 4 }, o.Queue);
    }

    [Test]
    public void ASlowCarThatNeverStopsKeepsItsPlace()
    {
        var o = new CautionOrder();
        o.Freeze(new[] { 1, 2, 3 });
        o.Observe(1, false, Crawl, 1000);
        o.Observe(2, false, Crawl, 990);
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, o.Queue);
        Assert.IsFalse(o.IsCaught(1));
    }

    [Test]
    public void BackUpToThirtyMph_AStoppedCarJoinsTheBackOfTheQueue_InTheOrderTheyGetGoing()
    {
        var o = new CautionOrder();
        o.Freeze(new[] { 1, 2, 3, 4, 5 });
        o.Observe(1, false, Stopped, 1000);
        o.Observe(3, false, Stopped, 980);
        o.Observe(3, false, Racing, 990);   // 3 gets going first...
        o.Observe(1, false, Racing, 1010);  // ...then the old leader
        CollectionAssert.AreEqual(new[] { 2, 4, 5, 3, 1 }, o.Queue);
        Assert.IsTrue(o.IsCaught(1), "a car back from a stop is due behind - it lets the field by");
        Assert.IsTrue(o.Behind(1, 5));
    }

    [Test]
    public void ACarThatStopsAgainAfterRejoiningIsDroppedAgain()
    {
        var o = new CautionOrder();
        o.Freeze(new[] { 1, 2, 3 });
        o.Observe(1, false, Stopped, 1000);
        o.Observe(1, false, Racing, 1001);
        o.Observe(1, false, Stopped, 1002);
        CollectionAssert.AreEqual(new[] { 2, 3 }, o.Queue);
        o.Observe(1, false, Racing, 1003);
        CollectionAssert.AreEqual(new[] { 2, 3, 1 }, o.Queue);
    }

    [Test]
    public void ACarOnPitRoadLosesItsPlace_AndAStopInThePitBoxIsNotAStop()
    {
        var o = new CautionOrder();
        o.Freeze(new[] { 1, 2, 3 });
        o.Observe(1, true, 20f, 1000);
        o.Observe(1, true, Stopped, 1050);   // in the box
        Assert.IsFalse(o.IsQueued(1));
        Assert.IsFalse(o.IsDropped(1));
        Assert.IsFalse(o.IsCaught(1));
        Assert.IsTrue(o.HasPitted(1));
        CollectionAssert.AreEqual(new[] { 2, 3, 1 }, Restart(o, new[] { 1, 2, 3 }));
    }

    [Test]
    public void OffPitRoad_ACarTakesWhateverPlaceItIsInOnTheRoad()
    {
        var o = new CautionOrder();
        o.Freeze(new[] { 1, 2, 3, 4, 5 });
        // The leader pits; by the time it leaves pit road 2 and 3 have gone by the pit exit, 4 and 5 have not.
        o.Observe(1, true, 20f, 1000);
        o.Observe(2, false, Racing, 1300);
        o.Observe(3, false, Racing, 1250);
        o.Observe(4, false, Racing, 1150);
        o.Observe(5, false, Racing, 1100);
        o.Observe(1, false, 25f, 1200);
        CollectionAssert.AreEqual(new[] { 2, 3, 1, 4, 5 }, o.Queue);
        Assert.IsFalse(o.IsCaught(1));
    }

    [Test]
    public void OffPitRoadBehindEverybody_ACarGoesToTheBack()
    {
        var o = new CautionOrder();
        o.Freeze(new[] { 1, 2, 3 });
        o.Observe(2, true, 20f, 1000);
        o.Observe(1, false, Racing, 1400);
        o.Observe(3, false, Racing, 1300);
        o.Observe(2, false, 25f, 1100);
        CollectionAssert.AreEqual(new[] { 1, 3, 2 }, o.Queue);
    }

    [Test]
    public void OffPitRoad_RaceDistanceCountsTheLaps_ALappedCarIsNotAhead()
    {
        var o = new CautionOrder();
        o.Freeze(new[] { 1, 2, 3 });   // 3 is a lap down
        o.Observe(1, true, 20f, 5000);
        o.Observe(2, false, Racing, 5400);
        o.Observe(3, false, Racing, 5400 - 2500 + 100);   // physically just ahead of the pit exit, a lap down
        o.Observe(1, false, 25f, 5100);
        CollectionAssert.AreEqual(new[] { 2, 1, 3 }, o.Queue);
    }

    [Test]
    public void AStoppedCarThatGetsToPitRoadIsPlacedWhereItComesOut_NotAtTheBack()
    {
        var o = new CautionOrder();
        o.Freeze(new[] { 1, 2, 3, 4 });
        o.Observe(1, false, Stopped, 1000);
        o.Observe(1, false, Crawl, 1010);   // limps on, never back to 30 mph
        o.Observe(1, true, Crawl, 1020);    // onto pit road
        o.Observe(2, false, Racing, 1300);
        o.Observe(3, false, Racing, 1100);
        o.Observe(4, false, Racing, 1050);
        o.Observe(1, false, 25f, 1200);
        CollectionAssert.AreEqual(new[] { 2, 1, 3, 4 }, o.Queue);
        Assert.IsFalse(o.IsCaught(1), "placed by where it came out: nobody is due to pass it");
    }

    [Test]
    public void ACarRejoinedAtTheBackThatPitsIsPlacedWhereItComesOut()
    {
        var o = new CautionOrder();
        o.Freeze(new[] { 1, 2, 3 });
        o.Observe(1, false, Stopped, 1000);
        o.Observe(1, false, Racing, 1005);
        CollectionAssert.AreEqual(new[] { 2, 3, 1 }, o.Queue);
        o.Observe(1, true, 20f, 1010);
        o.Observe(2, false, Racing, 1300);
        o.Observe(3, false, Racing, 1100);
        o.Observe(1, false, 25f, 1200);
        CollectionAssert.AreEqual(new[] { 2, 1, 3 }, o.Queue);
    }

    [Test]
    public void CarsOutOfTheQueueSortAfterIt_PitRoadThenUnseenThenStopped()
    {
        var o = new CautionOrder();
        o.Freeze(new[] { 1, 2, 3 });
        o.Observe(1, false, Stopped, 1000);
        o.Observe(2, true, 20f, 990);
        CollectionAssert.AreEqual(new[] { 3, 2, 7, 1 }, Restart(o, new[] { 7, 1, 2, 3 }));
    }

    [Test]
    public void ACarTheFreezeNeverSawJoinsTheBackOnceItIsUpToSpeed()
    {
        var o = new CautionOrder();
        o.Freeze(new[] { 1, 2 });
        o.Observe(7, false, Racing, 3000);
        CollectionAssert.AreEqual(new[] { 1, 2, 7 }, o.Queue);
    }

    [Test]
    public void DroppedFromTheStart_ACarStoppedAtTheFlagHoldsNoPlace()
    {
        var o = new CautionOrder();
        o.Freeze(new[] { 1, 2 });
        o.Drop(1);
        Assert.IsTrue(o.IsCaught(1));
        CollectionAssert.AreEqual(new[] { 2, 1 }, Restart(o, new[] { 1, 2 }));
    }

    [Test]
    public void TheNextFreezeStartsClean()
    {
        var o = new CautionOrder();
        o.Freeze(new[] { 1, 2 });
        o.Drop(1);
        o.Observe(2, true, 20f, 1000);
        o.Freeze(new[] { 2, 1 });
        Assert.IsFalse(o.IsCaught(1));
        Assert.IsFalse(o.IsDropped(1));
        Assert.IsFalse(o.HasPitted(2));
        CollectionAssert.AreEqual(new[] { 2, 1 }, o.Queue);
    }

    // ---- Two-wide for the restart (RestartSlots) ----

    [Test]
    public void NoSlotsUntilTheLineUpIsSet()
    {
        var slots = new RestartSlots();
        slots.Update(new[] { 1, 2, 3 });
        Assert.IsFalse(slots.Latched);
        Assert.AreEqual(-1, slots.SlotOf(1));
    }

    [Test]
    public void TheLineUpIsTheRestartQueue_LeaderOnSlotZero()
    {
        var o = new CautionOrder();
        o.Freeze(new[] { 4, 9, 2, 7, 5 });
        var slots = new RestartSlots();
        slots.Latch(o.Queue);
        Assert.IsTrue(slots.Latched);
        Assert.AreEqual(0, slots.SlotOf(4));
        Assert.AreEqual(1, slots.SlotOf(9));   // row 1, the other column
        Assert.AreEqual(2, slots.SlotOf(2));
        Assert.AreEqual(3, slots.SlotOf(7));
        Assert.AreEqual(4, slots.SlotOf(5));
        Assert.AreEqual(-1, slots.SlotOf(99));
    }

    [Test]
    public void DoublesUpOncePitRoadHasShutAndEmptied_OrAtTheCloseUpWhatever()
    {
        Assert.IsFalse(RestartSlots.ShouldLatch(false, 0, false), "pit road still open (or the safety car still picking up)");
        Assert.IsFalse(RestartSlots.ShouldLatch(true, 1, false), "a car still on pit road takes a place when it comes off it");
        Assert.IsTrue(RestartSlots.ShouldLatch(true, 0, false));
        Assert.IsTrue(RestartSlots.ShouldLatch(false, 2, true), "closing up for the green: two-wide whatever");
    }

    [Test]
    public void ACarStillOnPitRoadHoldsUpTheLineUp()
    {
        var o = new CautionOrder();
        o.Freeze(new[] { 1, 2, 3 });
        o.Observe(2, true, 20f, 1000);
        Assert.AreEqual(1, o.OnPitCount);
        Assert.IsFalse(RestartSlots.ShouldLatch(true, o.OnPitCount, false));
        o.Observe(2, false, 25f, 900);   // out behind the others
        Assert.AreEqual(0, o.OnPitCount);
        Assert.IsTrue(RestartSlots.ShouldLatch(true, o.OnPitCount, false));
    }

    [Test]
    public void ACarLeavingTheQueueMovesNobodyElse_ACarJoiningGoesToTheTail()
    {
        var o = new CautionOrder();
        o.Freeze(new[] { 1, 2, 3, 4, 5 });
        var slots = new RestartSlots();
        slots.Latch(o.Queue);

        // Car 2 stops: it gives up its slot, and nobody behind it changes column.
        o.Observe(2, false, Stopped, 1000);
        slots.Update(o.Queue);
        Assert.AreEqual(-1, slots.SlotOf(2));
        Assert.AreEqual(0, slots.SlotOf(1));
        Assert.AreEqual(2, slots.SlotOf(3));
        Assert.AreEqual(3, slots.SlotOf(4));
        Assert.AreEqual(4, slots.SlotOf(5));

        // Back up to speed: the back of the queue, and the next slot at the tail.
        o.Observe(2, false, Racing, 1000);
        slots.Update(o.Queue);
        Assert.AreEqual(5, slots.SlotOf(2));
    }

    [Test]
    public void TheNextCautionStartsSingleFileAgain()
    {
        var slots = new RestartSlots();
        slots.Latch(new[] { 1, 2 });
        slots.Clear();
        Assert.IsFalse(slots.Latched);
        Assert.AreEqual(-1, slots.SlotOf(1));
    }
}
