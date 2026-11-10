using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Draftmaster.MasterDraft;
using NUnit.Framework;

// A championship round raced in RaceScene (rework row 7, Core SeriesRace.cs): the launch plan the race scene is
// handed (track, laps, AI level -> field strength, the car's livery and class step) and the scoring of what comes
// back (race car numbers -> championship cars -> RaceRewards). Engine-free, so it also runs under
// `dotnet test Tools/HeadlessTests`.
public class MasterDraftSeriesRaceTests
{
    // ---- fixtures

    static CarDef Car(string id, int number, int rarity = 1, string carset = "tst") => new CarDef
    {
        id = id, carset = carset, number = number, driver = "Driver" + number, team = "HEN", manufacturer = "CHV",
        rarity = rarity, type = "Closer", livery = carset + "livery" + number,
    };

    // Ten cars numbered 1..10 in one carset (5 one-star, 5 two-star), plus the starter-id car.
    static CarCatalog SmallCatalog()
    {
        var file = new CarCatalogFile { version = 1 };
        file.carsets.Add(new CarsetDef { id = "tst", name = "Test", cars = 10 });
        for (int n = 1; n <= 10; n++) file.cars.Add(Car("tst_" + n, n, n <= 5 ? 1 : 2));
        file.carsets.Add(new CarsetDef { id = "cup24", name = "Cup '24", cars = 1 });
        file.cars.Add(Car("cup24_77", 77, carset: "cup24"));
        return new CarCatalog(file);
    }

    static SeriesDef ThreeRoundSeries(int aiLevel = 0) => new SeriesDef
    {
        id = "test_series",
        menu = "test",
        name = "Test Series",
        aiLevel = aiLevel,
        prize = new PrizeFilter { code = "Rarity2", minRarity = 2, maxRarity = 2 },
        rounds = new List<SeriesRound>
        {
            new SeriesRound { track = "Daytona", baseLaps = 4 },
            new SeriesRound { track = "Martinsville", baseLaps = 6 },
            new SeriesRound { track = "WatkinsGlen", baseLaps = 3 },
        },
    };

    // A profile owning tst_3 at the given class, in a championship of ThreeRoundSeries in it.
    static MasterDraftProfile InChampionship(CarCatalog cars, SeriesDef series, int carClass = 0, int aiLevel = -1, int laps = 0)
    {
        var p = MasterDraftProfile.New("race7", "Ace", cars);
        CarProgression.AwardCar(p.garage, "tst_3", out var owned);
        owned.carClass = carClass;
        p.StartChampionship(series, cars.Get("tst_3"), cars, aiLevel, laps);
        return p;
    }

    static string projectRoot;

    static string Resource(string path)
    {
        if (projectRoot == null)
            foreach (var start in new[] { TestContext.CurrentContext.TestDirectory, Directory.GetCurrentDirectory() })
            {
                for (var dir = new DirectoryInfo(start); dir != null && projectRoot == null; dir = dir.Parent)
                    if (File.Exists(Path.Combine(dir.FullName, "Assets", "Resources", "MasterDraft", "Series.json")))
                        projectRoot = dir.FullName;
                if (projectRoot != null) break;
            }
        Assert.IsNotNull(projectRoot, "Assets/Resources/MasterDraft/Series.json not found");
        return File.ReadAllText(Path.Combine(projectRoot, "Assets", "Resources", path + ".json"));
    }

    // ---- AI level -> field strength

    [Test]
    public void AiStrengthClimbsWithTheLevelAndIsClamped()
    {
        Assert.AreEqual(0.90f, SeriesRace.AiStrength(0), 1e-5f, "Rookies' level 0 is the softest field");
        Assert.AreEqual(1.125f, SeriesRace.AiStrength(SeriesRules.MaxAILevel), 1e-5f);
        for (int level = 1; level <= SeriesRules.MaxAILevel; level++)
            Assert.Greater(SeriesRace.AiStrength(level), SeriesRace.AiStrength(level - 1), $"level {level}");
        Assert.AreEqual(SeriesRace.AiStrength(0), SeriesRace.AiStrength(-3));
        Assert.AreEqual(SeriesRace.AiStrength(SeriesRules.MaxAILevel), SeriesRace.AiStrength(99));
        Assert.AreEqual(1f, SeriesRace.AiStrength(7), 0.01f, "the middle of the range is the AI as calibrated");
    }

    // ---- launch plan

    [Test]
    public void PlanCarriesTheFirstRoundsTrackLapsLiveryAndStrength()
    {
        var cars = SmallCatalog();
        var series = ThreeRoundSeries(aiLevel: 0);
        var p = InChampionship(cars, series);

        var plan = SeriesRace.Plan(p, series.id, cars, out string error, series);
        Assert.IsNull(error);
        Assert.IsNotNull(plan);
        Assert.AreEqual(series.id, plan.seriesId);
        Assert.AreEqual("Test Series", plan.seriesName);
        Assert.AreEqual("Daytona", plan.trackId);
        Assert.AreEqual(1, plan.round);
        Assert.AreEqual(3, plan.rounds);
        Assert.AreEqual(4, plan.laps, "base laps x the level-0 multiplier (1)");
        Assert.AreEqual(0, plan.aiLevel);
        Assert.AreEqual(SeriesRace.AiStrength(0), plan.aiStrength);
        Assert.AreEqual("tst_3", plan.carId);
        Assert.AreEqual(3, plan.number);
        Assert.AreEqual("Driver3", plan.driver);
        Assert.AreEqual("tstlivery3", plan.livery);
        Assert.AreEqual(1f, plan.powerScale);
        Assert.AreEqual(1f, plan.gripScale);
    }

    [Test]
    public void PlanAppliesTheCarsClassStep()
    {
        var cars = SmallCatalog();
        var series = ThreeRoundSeries();
        var p = InChampionship(cars, series, carClass: 3);

        var plan = SeriesRace.Plan(p, series.id, cars, out _);
        Assert.AreEqual(3, plan.carClass);
        Assert.AreEqual(CarProgression.PowerScale(3), plan.powerScale);
        Assert.AreEqual(CarProgression.GripScale(3), plan.gripScale);
        Assert.AreEqual(1.03f, plan.powerScale, 1e-5f, "+1% power per class");
        Assert.AreEqual(1.015f, plan.gripScale, 1e-5f, "+0.5% grip per class");

        // A class bought mid-championship counts from the next round.
        p.Car("tst_3").carClass = 4;
        Assert.AreEqual(CarProgression.PowerScale(4), SeriesRace.Plan(p, series.id, cars, out _).powerScale);
    }

    [Test]
    public void PlanUsesTheChosenDifficultyAndLapMultiplier()
    {
        var cars = SmallCatalog();
        var series = ThreeRoundSeries(aiLevel: 2);
        var p = InChampionship(cars, series, aiLevel: 9, laps: 3);

        var plan = SeriesRace.Plan(p, series.id, cars, out _);
        Assert.AreEqual(9, plan.aiLevel);
        Assert.AreEqual(SeriesRace.AiStrength(9), plan.aiStrength);
        Assert.AreEqual(12, plan.laps, "4 base laps x the player's multiplier of 3");

        // Asking for less than the series' level gets the series' level.
        var q = InChampionship(cars, series, aiLevel: 0);
        Assert.AreEqual(2, SeriesRace.Plan(q, series.id, cars, out _).aiLevel);
        Assert.AreEqual(4, SeriesRace.Plan(q, series.id, cars, out _).laps, "level 2 -> multiplier 1");
    }

    [Test]
    public void PlanMovesToTheNextRoundAfterARace()
    {
        var cars = SmallCatalog();
        var series = ThreeRoundSeries(aiLevel: 4);
        var p = InChampionship(cars, series);
        SeriesRace.RecordResult(p, series, null, new[] { 3, 1, 2 }, 1, cars);

        var plan = SeriesRace.Plan(p, series.id, cars, out _);
        Assert.AreEqual(2, plan.round);
        Assert.AreEqual("Martinsville", plan.trackId);
        Assert.AreEqual(12, plan.laps, "6 base laps x (4 / 4 + 1)");
    }

    [Test]
    public void PlanRefusesWhatCantRun()
    {
        var cars = SmallCatalog();
        var series = ThreeRoundSeries();

        Assert.IsNull(SeriesRace.Plan(null, series.id, cars, out string e0));
        Assert.IsNotEmpty(e0);

        var fresh = MasterDraftProfile.New("none", "Ace", cars);
        Assert.IsNull(SeriesRace.Plan(fresh, series.id, cars, out string e1));
        StringAssert.Contains("Start the championship", e1);

        var sold = InChampionship(cars, series);
        sold.garage.RemoveAll(c => c.id == "tst_3");
        Assert.IsNull(SeriesRace.Plan(sold, series.id, cars, out string e2));
        StringAssert.Contains("no longer own", e2);

        var missing = InChampionship(cars, series);
        Assert.IsNull(SeriesRace.Plan(missing, series.id, new CarCatalog(new CarCatalogFile()), out string e3));
        StringAssert.Contains("catalogue", e3);

        var over = InChampionship(cars, series);
        over.Championship(series.id).round = 3;
        Assert.IsNull(SeriesRace.Plan(over, series.id, cars, out string e4));
        StringAssert.Contains("over", e4);
    }

    [Test]
    public void EveryShippedSeriesPlansARealTrack()
    {
        var cars = CarCatalog.FromJson(Resource(CarCatalog.ResourcePath));
        var data = DraftSeriesCatalog.FromJson(Resource(DraftSeriesCatalog.ResourcePath));
        Assert.IsNotNull(cars);
        Assert.IsNotNull(data);
        var known = new HashSet<string>(Draftmaster.Tracks.TrackDimensions.All.Select(d => d.id), StringComparer.Ordinal);

        foreach (var s in data.All)
        {
            var car = Restrictions.FittingCars(s.restriction, cars).FirstOrDefault();
            if (car == null) continue;   // nothing in the catalogue fits (a team the catalogue lacks)
            var p = MasterDraftProfile.New("all", "Ace", cars);
            CarProgression.AwardCar(p.garage, car.id, out var owned);
            owned.carClass = CarProgression.MaxClass;
            p.StartChampionship(s, car, cars);
            for (int r = 0; r < s.RoundCount; r++)
            {
                var plan = SeriesRace.Plan(p, s.id, cars, out string error, s);
                Assert.IsNotNull(plan, $"{s.id} round {r + 1}: {error}");
                Assert.IsTrue(known.Contains(plan.trackId), $"{s.id} round {r + 1}: {plan.trackId} isn't a calendar venue");
                Assert.GreaterOrEqual(plan.laps, 1, s.id);
                Assert.AreEqual(car.livery, plan.livery, s.id);
                SeriesRace.RecordResult(p, s, null, new[] { car.number }, 1, cars);
            }
        }
    }

    // ---- results -> championship cars

    [Test]
    public void ThePlayerKeepsTheirRacePlace()
    {
        var cars = SmallCatalog();
        var series = ThreeRoundSeries();
        var p = InChampionship(cars, series);
        var progress = p.Championship(series.id);

        // Race field numbered like the carset; the player (#3 in the championship) finished 4th in whatever paint.
        var order = SeriesRace.FinishingOrder(progress, new[] { 7, 1, 9, 99, 2 }, 4);
        CollectionAssert.AreEqual(new[] { "tst_7", "tst_1", "tst_9", "tst_3", "tst_2" }, order);
    }

    [Test]
    public void CarsTheCarsetLacksTakeTheFreeCarsInTableOrder()
    {
        var cars = SmallCatalog();
        var series = ThreeRoundSeries();
        var p = InChampionship(cars, series);
        var progress = p.Championship(series.id);

        // #42 and #88 aren't in the carset; the second #1 is a duplicate; #3 is the player's championship number
        // but the AI car wearing it isn't the player, so it is placed like any car the carset can't match.
        var order = SeriesRace.FinishingOrder(progress, new[] { 42, 1, 0, 1, 88, 3 }, 3);
        CollectionAssert.AreEqual(new[] { "tst_2", "tst_1", "tst_3", "tst_4", "tst_5", "tst_6" }, order);
        Assert.AreEqual(order.Count, order.Distinct().Count(), "every championship car at most once");
    }

    [Test]
    public void AnUnclassifiedPlayerIsLeftOutAndTheTableCapsTheField()
    {
        var cars = SmallCatalog();
        var series = ThreeRoundSeries();
        var p = InChampionship(cars, series);
        var progress = p.Championship(series.id);

        var dnf = SeriesRace.FinishingOrder(progress, new[] { 5, 6 }, 0);
        CollectionAssert.AreEqual(new[] { "tst_5", "tst_6" }, dnf);

        // 14 race cars, 10 in the championship: the first ten places are filled, the rest dropped.
        var big = SeriesRace.FinishingOrder(progress, Enumerable.Range(20, 14).ToList(), 2);
        Assert.AreEqual(10, big.Count);
        Assert.AreEqual("tst_3", big[1]);

        // The player's place past the end of the numbers still holds.
        var shortList = SeriesRace.FinishingOrder(progress, new[] { 1 }, 3);
        CollectionAssert.AreEqual(new[] { "tst_1", "tst_2", "tst_3" }, shortList);

        Assert.IsEmpty(SeriesRace.FinishingOrder(null, new[] { 1 }, 1));
    }

    // ---- results -> rewards

    [Test]
    public void ARoundScoresTheTableAndPaysXp()
    {
        var cars = SmallCatalog();
        var series = ThreeRoundSeries(aiLevel: 0);
        var p = InChampionship(cars, series);
        int coins = p.coins, races = p.racesRun;

        var r = SeriesRace.RecordResult(p, series, null, new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, 2, cars);
        Assert.AreEqual(RaceRewardKind.ChampionshipRound, r.kind);
        Assert.AreEqual(1, r.round.round);
        Assert.AreEqual(2, r.position);
        Assert.AreEqual(2, r.round.playerFinish);
        Assert.AreEqual(SeriesRules.PointsFor(2), r.round.playerPoints);
        Assert.AreEqual(RaceRewards.RaceXp(2, 0), r.xp);
        Assert.AreEqual(0, r.money, "the purse waits for the end of the championship");
        Assert.AreEqual(coins, p.coins);
        Assert.AreEqual(races + 1, p.racesRun);

        var progress = p.Championship(series.id);
        Assert.AreEqual(1, progress.round);
        CollectionAssert.AreEqual(new[] { 2 }, progress.playerFinishes);
        var table = SeriesRules.Standings(progress);
        Assert.AreEqual("tst_1", table[0].carId, "the race winner, #1, leads");
        Assert.AreEqual(2, SeriesRules.Position(progress));
    }

    [Test]
    public void TheLastRoundPaysTheChampionshipAndRecordsIt()
    {
        var cars = SmallCatalog();
        var series = ThreeRoundSeries(aiLevel: 10);
        var p = InChampionship(cars, series);
        int coins = p.coins;
        var field = Enumerable.Range(1, 10).ToList();

        RaceRewardSummary last = null;
        for (int round = 1; round <= 3; round++) last = SeriesRace.RecordResult(p, series, null, field, 1, cars);

        Assert.AreEqual(RaceRewardKind.ChampionshipFinal, last.kind);
        Assert.IsTrue(last.championshipOver);
        Assert.AreEqual(1, last.championshipPosition);
        Assert.AreEqual(RaceRewards.ChampionshipMoney(1, 3), last.money);
        Assert.AreEqual(coins + last.money, p.coins);
        Assert.AreEqual(RaceRewards.ChampionshipGears(1, 10, 10, 3), last.gears, "gears scaled to the race's 10-car field");
        Assert.AreEqual(1, last.prizes.Count, "a champion always gets a prize car");
        Assert.IsNull(p.Championship(series.id));
        Assert.AreEqual(1, p.SeriesRecord(series.id).titles);
        Assert.IsNull(SeriesRace.Plan(p, series.id, cars, out _), "nothing left to race");
    }

    [Test]
    public void ScoringWithoutAChampionshipThrows()
    {
        var cars = SmallCatalog();
        var p = MasterDraftProfile.New("none", "Ace", cars);
        Assert.Throws<InvalidOperationException>(() => SeriesRace.RecordResult(p, ThreeRoundSeries(), null, new[] { 1 }, 1, cars));
        Assert.Throws<ArgumentNullException>(() => SeriesRace.RecordResult(null, ThreeRoundSeries(), null, new[] { 1 }, 1, cars));
    }

    [Test]
    public void TheEntryPickerStartsOnTheSelectedCarWhenItCanEnter()
    {
        var cars = SmallCatalog();
        var series = ThreeRoundSeries();
        series.restriction = new Restriction(1, 1);   // class 1+
        var p = MasterDraftProfile.New("entry", "Ace", cars);
        CarProgression.AwardCar(p.garage, "tst_2", out var two);
        CarProgression.AwardCar(p.garage, "tst_6", out var six);
        two.carClass = 1;
        six.carClass = 2;

        p.selectedCar = "tst_6";
        Assert.AreEqual("tst_6", SeriesRace.DefaultEntry(series, p, cars).id);
        p.selectedCar = "cup24_77";   // class 0: can't enter
        Assert.AreEqual("tst_2", SeriesRace.DefaultEntry(series, p, cars).id, "the first garage car that can");
        series.restriction = new Restriction(1, 5);
        Assert.IsNull(SeriesRace.DefaultEntry(series, p, cars));
    }
}
