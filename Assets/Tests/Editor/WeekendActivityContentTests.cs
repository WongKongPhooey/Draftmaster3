using System.Collections.Generic;
using Draftmaster.Weekend;
using NUnit.Framework;

// The simulated support races and the press question bank.
public class WeekendActivityContentTests
{
    // ------------------------------------------------------------------ the other two championships

    [Test]
    public void SimulatedRace_ProducesAValidClassification()
    {
        var race = SeriesSimulator.Race(RacingSeries.Cup, 3, "Darlington");

        Assert.Greater(race.entries.Count, 10);

        var positions = new HashSet<int>();
        var grid = new HashSet<int>();
        foreach (var e in race.entries)
        {
            Assert.IsTrue(positions.Add(e.finishPosition), $"two cars classified P{e.finishPosition}");
            Assert.IsTrue(grid.Add(e.gridPosition), $"two cars started P{e.gridPosition}");
            Assert.GreaterOrEqual(e.finishPosition, 1);
            Assert.LessOrEqual(e.finishPosition, race.entries.Count);
        }

        Assert.IsNotNull(race.Winner);
        Assert.AreEqual(1, race.Winner.finishPosition);
    }

    [Test]
    public void SimulatedSessions_AreDeterministic()
    {
        var a = SeriesSimulator.Race(RacingSeries.National, 11, "Kansas");
        var b = SeriesSimulator.Race(RacingSeries.National, 11, "Kansas");

        Assert.AreEqual(a.Winner.driverName, b.Winner.driverName);
        Assert.AreEqual(a.cautions, b.cautions);
        for (int i = 0; i < a.entries.Count; i++)
            Assert.AreEqual(a.entries[i].finishPosition, b.entries[i].finishPosition);
    }

    [Test]
    public void Qualifying_RanksByLapTime()
    {
        var quali = SeriesSimulator.Qualifying(RacingSeries.Trucks, 2, "Bristol");
        for (int i = 1; i < quali.entries.Count; i++)
            Assert.LessOrEqual(quali.entries[i - 1].lapTime, quali.entries[i].lapTime, "grid is not in lap-time order");

        Assert.AreEqual(1, quali.PoleSitter().gridPosition);
    }

    [Test]
    public void RunningOrder_StartsOnTheGrid_AndEndsOnTheResult()
    {
        var race = SeriesSimulator.Race(RacingSeries.Cup, 5, "Michigan");
        var order = new List<SeriesSimulator.Entry>();

        race.OrderAt(0f, order);
        Assert.AreEqual(race.PoleSitter().carNumber, order[0].carNumber, "the pole sitter is not leading at the green");

        race.OrderAt(1f, order);
        Assert.AreEqual(race.Winner.carNumber, order[0].carNumber, "the winner is not leading at the flag");
    }

    [Test]
    public void CarNumbers_DoNotCollideBetweenChampionships()
    {
        var seen = new Dictionary<int, RacingSeries>();
        foreach (var s in SeriesCatalog.All)
        {
            var session = SeriesSimulator.Qualifying(s, 1, "Pocono");
            foreach (var e in session.entries)
            {
                Assert.IsFalse(seen.ContainsKey(e.carNumber),
                    $"#{e.carNumber} is entered in two championships at the same weekend ({s})");
                seen[e.carNumber] = s;
            }
        }
    }

    [Test]
    public void BroadcastTimeline_IsInOrder_AndCallsTheWinner()
    {
        var race = SeriesSimulator.Race(RacingSeries.National, 9, "Iowa");
        for (int i = 1; i < race.moments.Count; i++)
            Assert.LessOrEqual(race.moments[i - 1].at01, race.moments[i].at01);

        Assert.IsTrue(race.moments[race.moments.Count - 1].text.Contains(race.Winner.driverName),
                      "the last thing the broadcast says is not who won");
    }

    // ------------------------------------------------------------------ the press

    [Test]
    public void PressConference_AsksDistinctQuestionsWithRealChoices()
    {
        var ctx = new PressContext
        {
            series = RacingSeries.Trucks,
            trackName = "Martinsville",
            rivalName = "Rowdy Hearn",
            sponsorName = "Voltage Energy",
            weekendId = 4,
            lastFinish = 22,
        };

        var questions = PressConferenceContent.Build(ctx, "3.780.30", 3);
        Assert.AreEqual(3, questions.Count);

        var asked = new HashSet<string>();
        foreach (var q in questions)
        {
            Assert.IsTrue(asked.Add(q.text), "the same question was asked twice in one availability");
            Assert.GreaterOrEqual(q.answers.Count, 2, "a question with one answer is not a choice");
            Assert.IsNotEmpty(q.reporter);
            Assert.IsNotEmpty(q.outlet);
        }
    }

    [Test]
    public void EveryTone_TradesOneMeterForAnother()
    {
        var ctx = new PressContext { series = RacingSeries.Cup, rivalName = "Wade Corliss" };

        // Backing the crew buys morale and prints nothing worth much.
        var team = PressConferenceContent.Score(new PressAnswer("", PressTone.TeamFirst), ctx);
        Assert.Greater(team.teamMorale, 0f);

        // A fight sells, and the people paying for the hood are the ones who pay for it.
        var fiery = PressConferenceContent.Score(new PressAnswer("", PressTone.Fiery), ctx);
        Assert.Greater(fiery.mediaStanding, 0f);
        Assert.Greater(fiery.fanAppeal, 0f);
        Assert.Less(fiery.sponsorMood, 0f);

        // Talking points please exactly one audience.
        var corporate = PressConferenceContent.Score(new PressAnswer("", PressTone.Corporate), ctx);
        Assert.Greater(corporate.sponsorMood, 0f);
        Assert.Less(corporate.mediaStanding, 0f);

        // Honesty about a bad car is respected outside the shop and resented inside it.
        var candid = PressConferenceContent.Score(new PressAnswer("", PressTone.Candid), ctx);
        Assert.Greater(candid.mediaStanding, 0f);
        Assert.Less(candid.teamMorale, 0f);
    }

    [Test]
    public void AimingAtTheRival_MovesThatRelationship()
    {
        var ctx = new PressContext { series = RacingSeries.Cup, rivalName = "Wade Corliss" };
        var jab = PressConferenceContent.Score(new PressAnswer("", PressTone.Fiery, null, aimedAtRival: true), ctx);

        Assert.AreEqual("Wade Corliss", jab.rivalName);
        Assert.Less(jab.rivalDelta, 0f);
    }

    [Test]
    public void NoRival_MeansNoRelationshipHit()
    {
        var ctx = new PressContext { series = RacingSeries.Cup, rivalName = "" };
        var jab = PressConferenceContent.Score(new PressAnswer("", PressTone.Fiery, null, aimedAtRival: true), ctx);

        Assert.IsTrue(string.IsNullOrEmpty(jab.rivalName));
    }

    // ------------------------------------------------------------------ the seeded RNG

    [Test]
    public void WeekendRandom_IsStableAndSeedSeparated()
    {
        var a = WeekendRandom.For(3, 1, 2);
        var b = WeekendRandom.For(3, 1, 2);
        var c = WeekendRandom.For(3, 1, 3);

        for (int i = 0; i < 8; i++) Assert.AreEqual(a.NextUInt(), b.NextUInt());

        var different = false;
        for (int i = 0; i < 8 && !different; i++) different = a.NextUInt() != c.NextUInt();
        Assert.IsTrue(different, "two different streams produced the same numbers");
    }

    [Test]
    public void WeekendRandom_RangeStaysInBounds()
    {
        var rng = WeekendRandom.For(1);
        for (int i = 0; i < 500; i++)
        {
            int v = rng.Range(3, 9);
            Assert.GreaterOrEqual(v, 3);
            Assert.Less(v, 9);

            float f = rng.Value;
            Assert.GreaterOrEqual(f, 0f);
            Assert.Less(f, 1f);
        }
    }
}
