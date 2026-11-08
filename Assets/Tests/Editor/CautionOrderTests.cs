using System.Collections.Generic;
using System.Linq;
using Draftmaster.Sim;
using NUnit.Framework;

// The restart order under a full-course caution (CautionOrder, run by CautionLineup): frozen at the flag, cars that pit
// behind the cars that stayed out in the order they leave pit road, cars caught up in the crash at the tail.
public class CautionOrderTests
{
    static List<int> Restart(CautionOrder order, IEnumerable<int> cars) => cars.OrderBy(order.Key).ToList();

    [Test]
    public void TheOrderAtTheFlagIsTheRestartOrder()
    {
        var o = new CautionOrder();
        o.Freeze(new[] { 5, 3, 9, 1 });
        CollectionAssert.AreEqual(new[] { 5, 3, 9, 1 }, Restart(o, new[] { 1, 9, 3, 5 }));
    }

    [Test]
    public void ACarCaughtUpInTheCrashGoesToTheTail()
    {
        var o = new CautionOrder();
        o.Freeze(new[] { 5, 3, 9, 1 });
        o.MarkCaught(5);
        o.MarkCaught(9);
        CollectionAssert.AreEqual(new[] { 3, 1, 5, 9 }, Restart(o, new[] { 1, 9, 3, 5 }));
        Assert.IsTrue(o.IsCaught(5));
        Assert.IsFalse(o.IsCaught(3));
        Assert.IsTrue(o.Behind(5, 1), "the caught leader restarts behind the car that ran last");
    }

    [Test]
    public void CarsThatPitComeOutBehindTheCarsThatStayedOut_InPitExitOrder()
    {
        var o = new CautionOrder();
        o.Freeze(new[] { 1, 2, 3, 4 });
        o.Observe(1, true);
        o.Observe(2, true);
        o.Observe(2, false);   // 2 off pit road first
        o.Observe(1, false);
        CollectionAssert.AreEqual(new[] { 3, 4, 2, 1 }, Restart(o, new[] { 1, 2, 3, 4 }));
        Assert.IsTrue(o.HasPitted(1));
        Assert.IsFalse(o.HasPitted(3));
    }

    [Test]
    public void ACarStillOnPitRoadIsBehindTheCarsAlreadyBackOut()
    {
        var o = new CautionOrder();
        o.Freeze(new[] { 1, 2, 3 });
        o.Observe(1, true);
        o.Observe(2, true);
        o.Observe(2, false);
        CollectionAssert.AreEqual(new[] { 3, 2, 1 }, Restart(o, new[] { 1, 2, 3 }));
    }

    [Test]
    public void ACaughtCarThatPitsForRepairsIsStillAtTheTail()
    {
        var o = new CautionOrder();
        o.Freeze(new[] { 1, 2, 3 });
        o.MarkCaught(1);
        o.Observe(1, true);
        o.Observe(1, false);
        o.Observe(3, true);
        o.Observe(3, false);
        CollectionAssert.AreEqual(new[] { 2, 3, 1 }, Restart(o, new[] { 1, 2, 3 }));
    }

    [Test]
    public void BeingOnPitRoadOutsideACautionIsNotAStop_TheNextFreezeStartsClean()
    {
        var o = new CautionOrder();
        o.Freeze(new[] { 1, 2 });
        o.MarkCaught(1);
        o.Observe(2, true);
        o.Freeze(new[] { 2, 1 });
        Assert.IsFalse(o.IsCaught(1));
        Assert.IsFalse(o.HasPitted(2));
        CollectionAssert.AreEqual(new[] { 2, 1 }, Restart(o, new[] { 1, 2 }));
    }

    [Test]
    public void ACarTheFreezeNeverSawRestartsBehindTheCarsThatStayedOut_AheadOfTheCaught()
    {
        var o = new CautionOrder();
        o.Freeze(new[] { 1, 2 });
        o.MarkCaught(1);
        CollectionAssert.AreEqual(new[] { 2, 7, 1 }, Restart(o, new[] { 7, 1, 2 }));
    }
}
