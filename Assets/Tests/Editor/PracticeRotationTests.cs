using System.Collections.Generic;
using Draftmaster.Sim;
using NUnit.Framework;

// Who the practice/qualifying director sends out next, and how many cars it lets on track.
//
// It used to send cars out in list order with an 8-car cap: the front of the list came back in, rested, and
// went straight out again, and half the field sat in the pits all session without setting a lap.
public class PracticeRotationTests
{
    class Car
    {
        public bool parked = true;
        public bool hasTime;
        public int runs;
        public float readyAt;
        public float backAt;
    }

    [Test]
    public void ACarWithNoTimeGoesBeforeOneWithATime()
    {
        Assert.IsTrue(PracticeRotation.GoesBefore(false, 3, 50f, true, 0, 0f));
        Assert.IsFalse(PracticeRotation.GoesBefore(true, 0, 0f, false, 3, 50f));
    }

    [Test]
    public void FewerRunsThenLongestWaitingGoesFirst()
    {
        Assert.IsTrue(PracticeRotation.GoesBefore(false, 0, 90f, false, 1, 10f));
        Assert.IsTrue(PracticeRotation.GoesBefore(false, 1, 10f, false, 1, 20f));
        Assert.IsFalse(PracticeRotation.GoesBefore(false, 1, 20f, false, 1, 10f));
    }

    [Test]
    public void ReleaseOrderSkipsCarsNotReadyAndPutsTheNeediestFirst()
    {
        var cars = new List<Car>
        {
            new Car { hasTime = true, runs = 2, readyAt = 0f },
            new Car { hasTime = false, runs = 0, readyAt = 30f },
            new Car { parked = false },
            new Car { hasTime = false, runs = 1, readyAt = 5f },
        };
        var order = PracticeRotation.ReleaseOrder(cars, c => c.parked, c => c.hasTime, c => c.runs, c => c.readyAt);
        CollectionAssert.AreEqual(new[] { 1, 3, 0 }, order);
    }

    [Test]
    public void TheCapStaysAtTheFloorWhenTheFieldFitsTheSession()
    {
        Assert.AreEqual(8, PracticeRotation.OnTrackCap(12, 1200f, 200f, 8, 24));
    }

    [Test]
    public void TheCapRisesForABigFieldInAShortSessionButNotPastTheCeilingOrTheField()
    {
        // 40 cars x 300 s runs into 70% of 600 s needs 29 at once.
        Assert.AreEqual(24, PracticeRotation.OnTrackCap(40, 600f, 300f, 8, 24));
        Assert.AreEqual(15, PracticeRotation.OnTrackCap(40, 1200f, 300f, 8, 24));
        Assert.AreEqual(5, PracticeRotation.OnTrackCap(5, 600f, 300f, 8, 24));
    }

    // A whole session on a coarse clock: every car in a full Cup field gets a run, in practice and qualifying,
    // at a short oval and a road course.
    [TestCase(40, 1200f, 800f, 4, 40f)]   // practice, Martinsville-ish
    [TestCase(40, 600f, 4000f, 3, 85f)]   // qualifying, Daytona
    [TestCase(40, 600f, 3900f, 3, 45f)]   // qualifying, Watkins Glen
    [TestCase(40, 1200f, 3900f, 4, 45f)]  // practice, Watkins Glen
    public void EveryCarInTheFieldGetsARun(int field, float session, float lapM, int laps, float realSpeed)
    {
        const float rest = 27.5f, overhead = 45f;
        float sizedRun = PracticeRotation.RunSeconds(lapM, laps, 45f, overhead) + rest;
        int cap = PracticeRotation.OnTrackCap(field, session, sizedRun, 8, 24);
        float realRun = PracticeRotation.RunSeconds(lapM, laps, realSpeed, overhead);

        var cars = new List<Car>();
        for (int i = 0; i < field; i++) cars.Add(new Car { readyAt = 4f + 21f * i / field });

        for (float t = 0f; t < session; t += 1f)
        {
            int onTrack = 0;
            foreach (var c in cars)
            {
                if (!c.parked && t >= c.backAt) { c.parked = true; c.hasTime = true; c.readyAt = t + rest; }
                if (!c.parked) onTrack++;
            }
            var order = PracticeRotation.ReleaseOrder(cars, c => c.parked && t >= c.readyAt, c => c.hasTime,
                                                      c => c.runs, c => c.readyAt);
            for (int k = 0; k < order.Count && onTrack < cap; k++, onTrack++)
            {
                var c = cars[order[k]];
                c.parked = false;
                c.runs++;
                c.backAt = t + realRun;
            }
        }

        int neverRan = cars.FindAll(c => c.runs == 0).Count;
        Assert.AreEqual(0, neverRan, $"cap {cap}, run {realRun:0}s: {neverRan} of {field} cars never went out");
    }
}
