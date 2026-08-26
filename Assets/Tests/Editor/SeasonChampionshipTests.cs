using System.Collections.Generic;
using Draftmaster.Weekend;
using NUnit.Framework;
using UnityEngine;

// The championships the player is not driving in.
//
// Three series share every venue and the player is entered in one; the other two run their weekend anyway.
// These tests cover the scoring scale, the simulated round, the season table folded back out of a list of
// round numbers, and the one rule that makes it feel like a paddock rather than a spreadsheet: you do not
// know Sunday's result on Friday morning.
//
// The book and the weekend ledger both live in PlayerPrefs, so the real save's two keys are put back after
// the run.
public class SeasonChampionshipTests
{
    const string BookKey = "season.championships";
    const string LedgerKey = "weekend.ledger";

    string _bookBefore, _ledgerBefore;
    bool _hadBook, _hadLedger;

    [OneTimeSetUp]
    public void KeepTheSave()
    {
        _hadBook = PlayerPrefs.HasKey(BookKey);
        _bookBefore = PlayerPrefs.GetString(BookKey, "");
        _hadLedger = PlayerPrefs.HasKey(LedgerKey);
        _ledgerBefore = PlayerPrefs.GetString(LedgerKey, "");
    }

    [OneTimeTearDown]
    public void PutTheSaveBack()
    {
        if (_hadBook) PlayerPrefs.SetString(BookKey, _bookBefore); else PlayerPrefs.DeleteKey(BookKey);
        if (_hadLedger) PlayerPrefs.SetString(LedgerKey, _ledgerBefore); else PlayerPrefs.DeleteKey(LedgerKey);
        PlayerPrefs.Save();
        SeasonChampionships.InvalidateCache();
        WeekendLedger.InvalidateCache();
    }

    [SetUp]
    public void Clear()
    {
        SeasonChampionships.ClearAll();
        WeekendLedger.Timetable = null;   // no sheet to sweep, so advancing the clock costs nothing
        WeekendLedger.ClearAll();
    }

    // Park the weekend clock somewhere the season can be reasoned about.
    static void LiveWeekend(int id, RacingSeries series = RacingSeries.Trucks) =>
        WeekendLedger.EnsureWeekend(id, series);

    // ------------------------------------------------------------------ the points scale

    [Test]
    public void PointScale_IsTheStockCarLadder()
    {
        Assert.AreEqual(40, ChampionshipPoints.ForFinish(1), "a win is 40");
        Assert.AreEqual(35, ChampionshipPoints.ForFinish(2), "second is 35, five off the win");
        Assert.AreEqual(34, ChampionshipPoints.ForFinish(3));
        Assert.AreEqual(1, ChampionshipPoints.ForFinish(36), "36th scores the minimum");
        Assert.AreEqual(1, ChampionshipPoints.ForFinish(44), "past the scale, still one point for turning up");
        Assert.AreEqual(0, ChampionshipPoints.ForFinish(0), "a car that never started scores nothing");
    }

    [Test]
    public void PointsNeverGoUp_FurtherDownTheField()
    {
        for (int p = 2; p < 45; p++)
            Assert.LessOrEqual(ChampionshipPoints.ForFinish(p), ChampionshipPoints.ForFinish(p - 1),
                               $"P{p} scores more than P{p - 1}");
    }

    [Test]
    public void Pole_IsWorthAPoint_ButNotToANonStarter()
    {
        Assert.AreEqual(ChampionshipPoints.ForFinish(5) + ChampionshipPoints.Pole, ChampionshipPoints.ForRound(5, true));
        Assert.AreEqual(ChampionshipPoints.ForFinish(5), ChampionshipPoints.ForRound(5, false));
        Assert.AreEqual(0, ChampionshipPoints.ForRound(0, true));
    }

    // ------------------------------------------------------------------ one simulated round

    [Test]
    public void SimulatedRound_ClassifiesEveryCarExactlyOnce()
    {
        var round = SeriesWeekendResult.Simulate(RacingSeries.Cup, 4, "Darlington");
        var seen = new HashSet<int>();

        Assert.Greater(round.Classification.Count, 10);
        foreach (var c in round.Classification)
        {
            Assert.IsTrue(seen.Add(c.finishPosition), $"two cars classified P{c.finishPosition}");
            Assert.Greater(c.points, 0, $"{c.driverName} scored nothing for finishing P{c.finishPosition}");
        }
        Assert.AreEqual(1, round.Winner.finishPosition);
        Assert.AreEqual(ChampionshipPoints.Win, round.Winner.points - (round.Winner.pole ? ChampionshipPoints.Pole : 0));
    }

    [Test]
    public void SimulatedRound_IsDeterministic()
    {
        var a = SeriesWeekendResult.Simulate(RacingSeries.National, 7, "Kansas");
        var b = SeriesWeekendResult.Simulate(RacingSeries.National, 7, "Kansas");

        Assert.AreEqual(a.Classification.Count, b.Classification.Count);
        for (int i = 0; i < a.Classification.Count; i++)
        {
            Assert.AreEqual(a.Classification[i].driverName, b.Classification[i].driverName, $"P{i + 1} differs");
            Assert.AreEqual(a.Classification[i].points, b.Classification[i].points);
        }
    }

    [Test]
    public void DifferentRounds_ProduceDifferentRaces()
    {
        var winners = new HashSet<string>();
        for (int round = 0; round < 8; round++)
            winners.Add(SeriesWeekendResult.Simulate(RacingSeries.Cup, round, "Track").Winner.driverName);

        Assert.Greater(winners.Count, 1, "the same driver won all eight rounds - the seed is not moving");
    }

    // ------------------------------------------------------------------ cutting the player in

    [Test]
    public void PlayerFinish_PushesEverybodyTheyBeatDownAPlace()
    {
        var clean = SeriesWeekendResult.Simulate(RacingSeries.Trucks, 3, "Bristol");
        string thirdBefore = clean.Classification[2].driverName;
        int fieldBefore = clean.Classification.Count;

        var withMe = SeriesWeekendResult.Simulate(RacingSeries.Trucks, 3, "Bristol").WithPlayer("Rookie", 3);

        Assert.AreEqual(fieldBefore + 1, withMe.Classification.Count, "the player did not join the field");
        Assert.IsTrue(withMe.Classification[2].isPlayer, "the player is not third");
        Assert.AreEqual(3, withMe.Classification[2].finishPosition);
        Assert.AreEqual(thirdBefore, withMe.Classification[3].driverName, "the car the player beat did not drop a place");
        Assert.AreEqual(ChampionshipPoints.ForFinish(3), withMe.Classification[2].points);
    }

    [Test]
    public void PlayerWinning_TakesTheWinOffTheSimulatedWinner()
    {
        var clean = SeriesWeekendResult.Simulate(RacingSeries.Cup, 12, "Michigan");
        string simWinner = clean.Winner.driverName;

        var withMe = SeriesWeekendResult.Simulate(RacingSeries.Cup, 12, "Michigan").WithPlayer("Rookie", 1);

        Assert.IsTrue(withMe.Winner.isPlayer);
        Assert.AreEqual(ChampionshipPoints.Win, withMe.Winner.points);
        Assert.AreEqual(simWinner, withMe.Classification[1].driverName, "the simulated winner is not second");
    }

    [Test]
    public void NotDrivingTheRace_LeavesTheFieldAlone()
    {
        var round = SeriesWeekendResult.Simulate(RacingSeries.Trucks, 5, "Martinsville").WithPlayer("Rookie", 0);
        foreach (var c in round.Classification) Assert.IsFalse(c.isPlayer, "a player who did not start is in the result");
    }

    // ------------------------------------------------------------------ the season table

    [Test]
    public void EnteringTheSameRoundTwice_ScoresItOnce()
    {
        LiveWeekend(50);
        SeasonChampionships.EnterRound(2, "Bristol", "Bristol");
        SeasonChampionships.EnterRound(2, "Bristol", "Bristol");

        Assert.AreEqual(1, SeasonChampionships.RoundCount);
        Assert.AreEqual(ChampionshipPoints.Win, SeasonChampionships.Leader(RacingSeries.Cup).points - PoleBonus(RacingSeries.Cup, 2));
    }

    static int PoleBonus(RacingSeries s, int round)
    {
        var winner = SeasonChampionships.Result(s, round).Winner;
        return winner != null && winner.pole ? ChampionshipPoints.Pole : 0;
    }

    [Test]
    public void EveryChampionshipScoresEveryRound()
    {
        LiveWeekend(50);
        SeasonChampionships.EnterRound(1, "a", "Track A");
        SeasonChampionships.EnterRound(2, "b", "Track B");
        SeasonChampionships.EnterRound(3, "c", "Track C");

        foreach (var s in SeriesCatalog.All)
        {
            var table = SeasonChampionships.Standings(s);
            Assert.Greater(table.Count, 10, SeriesCatalog.Name(s) + " has no field");
            foreach (var row in table)
                Assert.AreEqual(3, row.starts, $"{row.driverName} started {row.starts} of 3 rounds");
        }
    }

    [Test]
    public void TableIsOrderedByPoints_AndTheLeaderIsFirst()
    {
        LiveWeekend(50);
        for (int r = 1; r <= 4; r++) SeasonChampionships.EnterRound(r, "t" + r, "Track " + r);

        var table = SeasonChampionships.Standings(RacingSeries.National);
        for (int i = 1; i < table.Count; i++)
            Assert.LessOrEqual(table[i].points, table[i - 1].points, "the table is out of order");

        Assert.AreEqual(1, table[0].position);
        Assert.AreEqual(table[0].driverName, SeasonChampionships.Leader(RacingSeries.National).driverName);
    }

    [Test]
    public void WinsAndPolesAddUpAcrossTheSeason()
    {
        LiveWeekend(50);
        for (int r = 1; r <= 5; r++) SeasonChampionships.EnterRound(r, "t" + r, "Track " + r);

        int wins = 0, poles = 0;
        foreach (var row in SeasonChampionships.Standings(RacingSeries.Cup)) { wins += row.wins; poles += row.poles; }

        Assert.AreEqual(5, wins, "five rounds should have produced five winners' worth of wins");
        Assert.AreEqual(5, poles, "five rounds should have produced five poles");
    }

    [Test]
    public void TheTableSurvivesAReload()
    {
        LiveWeekend(50);
        SeasonChampionships.EnterRound(1, "a", "Track A");
        SeasonChampionships.EnterRound(2, "b", "Track B");
        var before = new List<(string name, int points)>();
        foreach (var row in SeasonChampionships.Standings(RacingSeries.Cup)) before.Add((row.driverName, row.points));

        // What a scene load does: everything in memory goes, the JSON in PlayerPrefs does not.
        SeasonChampionships.InvalidateCache();

        var after = SeasonChampionships.Standings(RacingSeries.Cup);
        Assert.AreEqual(before.Count, after.Count);
        for (int i = 0; i < before.Count; i++)
        {
            Assert.AreEqual(before[i].name, after[i].driverName);
            Assert.AreEqual(before[i].points, after[i].points);
        }
    }

    // ------------------------------------------------------------------ the player's own championship

    [Test]
    public void PlayerResult_ScoresInTheirOwnChampionshipOnly()
    {
        LiveWeekend(50, RacingSeries.National);
        SeasonChampionships.EnterRound(6, "d", "Dover");
        SeasonChampionships.RecordPlayerRace(6, RacingSeries.National, "Josh Wheeler", 1, 1, 24);

        var me = SeasonChampionships.PlayerRow(RacingSeries.National);
        Assert.IsNotNull(me, "the player is not in their own championship");
        Assert.AreEqual(1, me.position, "a win did not put the player on top of a one-round season");
        Assert.AreEqual(1, me.wins);
        Assert.AreEqual(ChampionshipPoints.Win + ChampionshipPoints.Pole, me.points, "the pole point was not paid");
        Assert.AreEqual(0, SeasonChampionships.PlayerDeficit(RacingSeries.National));

        Assert.IsNull(SeasonChampionships.PlayerRow(RacingSeries.Cup), "the player scored in a series they are not in");
        Assert.IsNull(SeasonChampionships.PlayerRow(RacingSeries.Trucks));
    }

    [Test]
    public void SkippingYourOwnRace_LeavesYouOffTheTable()
    {
        LiveWeekend(50, RacingSeries.Trucks);
        SeasonChampionships.EnterRound(6, "d", "Dover");

        Assert.IsNull(SeasonChampionships.PlayerRow(RacingSeries.Trucks));
        Assert.Greater(SeasonChampionships.Standings(RacingSeries.Trucks).Count, 10,
                       "the truck race did not run without the player");
    }

    // ------------------------------------------------------------------ you do not know Sunday on Friday

    [Test]
    public void NoRaceHasRun_OnFridayMorning()
    {
        LiveWeekend(3);
        SeasonChampionships.EnterRound(3, "wg", "Watkins Glen");

        foreach (var s in SeriesCatalog.All)
        {
            Assert.IsFalse(SeasonChampionships.HasRun(s, 3), SeriesCatalog.Name(s) + " had a result before it ran");
            Assert.AreEqual(0, SeasonChampionships.Standings(s).Count, SeriesCatalog.Name(s) + " scored an unrun race");
        }
    }

    [Test]
    public void ResultsArriveInTheOrderTheRacesAreRun()
    {
        LiveWeekend(3);
        SeasonChampionships.EnterRound(3, "wg", "Watkins Glen");

        // Friday morning -> Friday afternoon -> Saturday morning. The trucks raced on Friday night.
        WeekendLedger.AdvanceSlot();
        WeekendLedger.AdvanceSlot();

        Assert.IsTrue(SeasonChampionships.HasRun(RacingSeries.Trucks, 3), "Friday night's truck race has not landed");
        Assert.IsFalse(SeasonChampionships.HasRun(RacingSeries.National, 3), "Saturday's race ran early");
        Assert.IsFalse(SeasonChampionships.HasRun(RacingSeries.Cup, 3), "Sunday's race ran early");

        Assert.Greater(SeasonChampionships.Standings(RacingSeries.Trucks).Count, 10);
        Assert.AreEqual(0, SeasonChampionships.Standings(RacingSeries.Cup).Count);
    }

    [Test]
    public void EveryRaceHasRun_OnceTheWeekendIsOver()
    {
        LiveWeekend(3);
        SeasonChampionships.EnterRound(3, "wg", "Watkins Glen");
        for (int i = 0; i < WeekendSlots.Count; i++) WeekendLedger.AdvanceSlot();

        Assert.IsTrue(WeekendLedger.WeekendOver);
        foreach (var s in SeriesCatalog.All)
            Assert.IsTrue(SeasonChampionships.HasRun(s, 3), SeriesCatalog.Name(s) + " never ran");
    }

    [Test]
    public void PastWeekendsAreAlwaysRun_AndFutureOnesNever()
    {
        LiveWeekend(5);
        Assert.IsTrue(SeasonChampionships.HasRun(RacingSeries.Cup, 4), "last weekend's race has un-run itself");
        Assert.IsFalse(SeasonChampionships.HasRun(RacingSeries.Cup, 6), "next weekend's race already has a result");
    }

    // ------------------------------------------------------------------ the feed and the badge

    [Test]
    public void Feed_ReadsNewestFirst_AndOnlyWhatHasRun()
    {
        LiveWeekend(50);
        SeasonChampionships.EnterRound(1, "a", "Track A");
        SeasonChampionships.EnterRound(2, "b", "Track B");

        var feed = SeasonChampionships.Feed(12);
        Assert.AreEqual(6, feed.Count, "two rounds of three championships is six results");
        Assert.AreEqual(2, feed[0].round, "the newest round is not at the top");
        Assert.AreEqual(RacingSeries.Cup, feed[0].series, "Sunday's race is not the top line of the weekend");
        foreach (var line in feed) Assert.IsNotEmpty(line.text);
    }

    [Test]
    public void Badge_CountsSomebodyElsesResults_AndClearsWhenRead()
    {
        LiveWeekend(50, RacingSeries.Trucks);
        SeasonChampionships.EnterRound(1, "a", "Track A");
        SeasonChampionships.RecordPlayerRace(1, RacingSeries.Trucks, "Josh Wheeler", 4);

        Assert.AreEqual(2, SeasonChampionships.Unread, "your own race is not news; the other two are");

        SeasonChampionships.MarkRead();
        Assert.AreEqual(0, SeasonChampionships.Unread);

        SeasonChampionships.EnterRound(2, "b", "Track B");
        Assert.AreEqual(3, SeasonChampionships.Unread, "a new weekend's three races did not come in");
    }

    // ------------------------------------------------------------------ rolling over

    [Test]
    public void NewSeason_StartsFromNothing()
    {
        LiveWeekend(50);
        SeasonChampionships.EnterRound(1, "a", "Track A");
        int season = SeasonChampionships.Season;

        SeasonChampionships.StartNewSeason();

        Assert.AreEqual(season + 1, SeasonChampionships.Season);
        Assert.AreEqual(0, SeasonChampionships.RoundCount);
        Assert.AreEqual(0, SeasonChampionships.Standings(RacingSeries.Cup).Count);
        Assert.AreEqual(0, SeasonChampionships.Unread);
    }
}
