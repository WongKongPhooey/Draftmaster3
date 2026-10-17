using System.Collections.Generic;
using Draftmaster.Weekend;
using NUnit.Framework;
using UnityEngine;

// A race watched from the grandstand is a real one: the field starts from a grid behind the line, the
// chequered flag comes out about five minutes after the green, the leader takes it the next time they
// cross the line and everybody else on their next crossing after that. GrandstandRace is the scoring half,
// pure so it can be driven here a metre at a time.
public class GrandstandRaceTests
{
    const float Lap = 1000f;
    const float Tolerance = 0.001f;

    static GrandstandRace Grid(int cars, float raceSeconds = GrandstandRace.RaceSeconds)
    {
        var starts = new List<float>();
        for (int i = 0; i < cars; i++) starts.Add(GrandstandRace.GridProgress(i, 6f, 6f));
        return new GrandstandRace(Lap, starts, raceSeconds);
    }

    [Test]
    public void TheRaceIsAboutFiveMinutes()
    {
        Assert.AreEqual(300f, GrandstandRace.RaceSeconds, Tolerance);
    }

    [Test]
    public void TheGrid_IsBehindTheLine_TwoByTwo()
    {
        Assert.AreEqual(-6f, GrandstandRace.GridProgress(0, 6f, 6f), Tolerance);
        Assert.AreEqual(-12f, GrandstandRace.GridProgress(1, 6f, 6f), Tolerance);
        // A negative first-row distance (GridSpawner's gridStartDistance convention) means the same thing.
        Assert.AreEqual(-66f, GrandstandRace.GridProgress(10, -6f, 6f), Tolerance);
    }

    [Test]
    public void CrossingTheLineAtTheStart_IsNotALap()
    {
        var race = Grid(2);
        race.Advance(0, 10f);   // pole sitter takes the green and crosses the line
        Assert.AreEqual(0, race.Laps(0));
        Assert.AreEqual(1, race.CurrentLap);

        race.Advance(0, Lap);   // ... and comes round again
        Assert.AreEqual(1, race.Laps(0));
        Assert.AreEqual(2, race.CurrentLap);
    }

    [Test]
    public void TheFlag_ComesOutWhenTheClockRunsOut_NotBefore()
    {
        var race = Grid(2);
        race.Tick(GrandstandRace.RaceSeconds - 0.1f);
        Assert.IsFalse(race.FlagOut);
        race.Tick(0.2f);
        Assert.IsTrue(race.FlagOut);
        Assert.IsFalse(race.WinnerIn, "the flag is shown, not taken");
    }

    [Test]
    public void TheLeaderTakesTheFlag_ThenEveryoneOnTheirNextCrossing()
    {
        var race = Grid(3, raceSeconds: 10f);
        race.Advance(0, 2506f);         // leader: 2500 — two laps done, half way round the third
        race.Advance(1, 2312f);         // P2:     2300
        race.Advance(2, 1418f);         // P3:     1400 — more than a lap behind the leader

        race.Tick(11f);
        Assert.IsTrue(race.FlagOut);

        race.Advance(0, 600f);          // leader crosses: wins
        Assert.IsTrue(race.WinnerIn);
        Assert.IsTrue(race.Finished(0));
        Assert.IsFalse(race.Over);

        race.Tick(1f);
        race.Advance(2, 600f);          // the lapped car crosses next — that crossing is its last
        Assert.IsTrue(race.Finished(2));
        Assert.AreEqual(2, race.Laps(2));
        race.Tick(1f);
        race.Advance(1, 700f);          // and then P2
        Assert.IsTrue(race.Finished(1));
        Assert.IsTrue(race.Over);

        var order = new List<int>();
        race.Classify(order);
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, order,
            "a lapped car that took the flag before P2 is still classified a lap down");
    }

    [Test]
    public void TheFlagIsForTheLeader_ALappedCarCrossingFirstHasNotFinished()
    {
        var race = Grid(2, raceSeconds: 1f);
        race.Advance(0, 1506f);         // leader 1500, one lap done
        race.Advance(1, 912f);          // lapped car 900
        race.Tick(2f);

        race.Advance(1, 200f);          // crosses the line first, on its first lap
        Assert.IsFalse(race.Finished(1));
        Assert.IsFalse(race.WinnerIn);
    }

    [Test]
    public void ACarThatNeverComesRound_DoesNotHoldTheResultOpenForever()
    {
        var race = Grid(2, raceSeconds: 1f);
        race.Advance(0, 0.5f * Lap);
        race.Tick(2f);
        race.Advance(0, Lap);           // leader wins
        Assert.IsTrue(race.WinnerIn);
        Assert.IsFalse(race.Over);

        race.Tick(GrandstandRace.FinishGraceSeconds + 0.1f);
        Assert.IsTrue(race.Over);
    }

    [Test]
    public void TheRunningOrder_IsLapsThenRoadPosition()
    {
        var race = Grid(3);
        race.Advance(0, 300f);
        race.Advance(1, 800f);
        race.Advance(2, 1500f);         // a lap completed: leads

        var order = new List<int>();
        race.Classify(order);
        CollectionAssert.AreEqual(new[] { 2, 1, 0 }, order);
    }

    [Test]
    public void TheGap_IsSecondsAtTheLine_OrLapsDown()
    {
        var race = new GrandstandRace(Lap, new List<float> { 0f, 0f, 0f });
        race.Advance(0, Lap);           // leader completes lap 1 at t = 0
        race.Tick(1.5f);
        race.Advance(1, Lap);           // P2 completes lap 1 1.5 s later
        race.Advance(0, 900f);          // leader most of the way round lap 2
        race.Advance(2, 500f);          // more than a lap behind

        Assert.AreEqual("+1.500", race.GapText(0, 1));
        Assert.AreEqual("+1 LAP", race.GapText(0, 2));
        Assert.AreEqual("LAP 2", race.GapText(0, 0));
    }

    [Test]
    public void BeforeAnyoneHasCompletedALap_ThereIsNoGapToShow()
    {
        var race = Grid(2);
        race.Advance(0, 100f);
        race.Advance(1, 90f);
        Assert.AreEqual("-", race.GapText(0, 1));
    }

    // ---- Where a watched race starts: off the last corner before the line ----

    // A closed centreline sampled every 2 m from (straight length, turn radius, turn degrees) pieces; radius 0 = a
    // straight. The line's distance along it is what MetresFromLastCorner measures against.
    static void Loop(out List<Vector2> points, out List<float> distances, params (float length, float radius)[] pieces)
    {
        points = new List<Vector2>();
        distances = new List<float>();
        Vector2 p = Vector2.zero;
        float heading = 0f, d = 0f;
        points.Add(p); distances.Add(0f);
        foreach (var (length, radius) in pieces)
        {
            int steps = Mathf.Max(1, Mathf.RoundToInt(length / 2f));
            float step = length / steps;
            for (int k = 0; k < steps; k++)
            {
                if (radius != 0f) heading += step / radius;
                p += new Vector2(Mathf.Cos(heading), Mathf.Sin(heading)) * step;
                d += step;
                points.Add(p); distances.Add(d);
            }
        }
    }

    const float HalfTurn = Mathf.PI * 100f;   // a 180-degree turn of radius 100 m

    [Test]
    public void TheField_StartsOffTheLastCorner_BeforeTheLine()
    {
        // Front straight, turns 1-2, back straight, turns 3-4: the line is 150 m down the front straight.
        Loop(out var pts, out var dist, (300f, 0f), (HalfTurn, 100f), (300f, 0f), (HalfTurn, 100f));
        Assert.AreEqual(150f, GrandstandRace.MetresFromLastCorner(pts, dist, 150f), 6f);
        // On the back straight instead, the last corner is turns 1-2.
        Assert.AreEqual(100f, GrandstandRace.MetresFromLastCorner(pts, dist, 300f + HalfTurn + 100f), 6f);
    }

    [Test]
    public void TheLine_RightAtTheCornerExit_IsNoDistanceAtAll()
    {
        // A lap that starts at the line, the last piece of it the turn onto the front straight.
        Loop(out var pts, out var dist, (300f, 0f), (HalfTurn, 100f), (300f, 0f), (HalfTurn, 100f));
        Assert.AreEqual(0f, GrandstandRace.MetresFromLastCorner(pts, dist, 0f), 6f);
    }

    [Test]
    public void ADogleg_OrAWiggle_IsNotTheLastCorner()
    {
        // Turn 4, then a sweeping 1 km-radius dogleg and a tight little 10-degree wiggle before the line: the
        // field still starts off turn 4.
        float dogleg = 1000f * 20f * Mathf.Deg2Rad, wiggle = 30f * 10f * Mathf.Deg2Rad;
        Loop(out var pts, out var dist,
             (300f, 0f), (HalfTurn, 100f), (300f, 0f), (HalfTurn, 100f),
             (100f, 0f), (dogleg, 1000f), (50f, 0f), (wiggle, 30f), (50f, 0f), (200f, 0f));
        float line = dist[dist.Count - 1] - 200f;   // clear of where this (unclosed) test loop meets itself
        Assert.AreEqual(100f + dogleg + 50f + wiggle + 50f, GrandstandRace.MetresFromLastCorner(pts, dist, line), 8f);
    }

    [Test]
    public void ALapWithNoCorner_HasNowhereToStart()
    {
        // A huge circle never gets tighter than the corner radius.
        Loop(out var pts, out var dist, (2f * Mathf.PI * 800f, 800f));
        Assert.AreEqual(-1f, GrandstandRace.MetresFromLastCorner(pts, dist, 10f), Tolerance);
    }
}
