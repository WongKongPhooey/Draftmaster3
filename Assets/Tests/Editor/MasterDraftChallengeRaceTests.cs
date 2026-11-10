using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Draftmaster.MasterDraft;
using NUnit.Framework;

// A challenge chapter raced in RaceScene (rework row 8, Core ChallengeRace.cs): the calendar week the Challenges
// screen shows, the launch plan the race scene is handed (live / unlocked / eligible, track, laps, the garage car or
// the handed-over car, AI strength, class step), how a moment is approximated, and the scoring that pays the prize.
// Engine-free, so it also runs under `dotnet test Tools/HeadlessTests`.
public class MasterDraftChallengeRaceTests
{
    // ---- fixtures

    static CarDef Car(string id, int number, string team = "HEN", string carset = "tst") => new CarDef
    {
        id = id, carset = carset, number = number, driver = "Driver" + number, team = team, manufacturer = "CHV",
        rarity = 1, type = "Closer", livery = carset + "livery" + number,
    };

    // tst_1..tst_4 (HEN), tst_5..tst_6 (IND), the starter-id car (STR), and the prize car prz_48.
    static CarCatalog SmallCatalog()
    {
        var file = new CarCatalogFile { version = 1 };
        file.carsets.Add(new CarsetDef { id = "tst", name = "Test", cars = 6 });
        for (int n = 1; n <= 6; n++) file.cars.Add(Car("tst_" + n, n, n <= 4 ? "HEN" : "IND"));
        file.carsets.Add(new CarsetDef { id = "cup24", name = "Cup '24", cars = 1 });
        file.cars.Add(Car("cup24_77", 77, team: "STR", carset: "cup24"));
        file.carsets.Add(new CarsetDef { id = "prz", name = "Prize", cars = 1 });
        file.cars.Add(Car("prz_48", 48, carset: "prz"));
        return new CarCatalog(file);
    }

    // One Progression event live in weeks 2 and 6 (two HEN chapters, the prize car prz_48), and one Replay event
    // live every week with a moment chapter that hands over its own car.
    static ChallengeCatalog TestChallenges()
    {
        ChallengeChapter Chapter(string id, string ev, string track, int ai, Restriction r, ChallengePrize prize) => new ChallengeChapter
        {
            id = id, eventId = ev, name = id, track = track, aiLevel = ai, baseLaps = 4, restriction = r,
            moment = new ChallengeMoment(), prize = prize,
        };
        ChallengePrize CarPrize(int gears) => new ChallengePrize { kind = ChallengePrizeKind.Car, code = "Prz", cars = new List<string> { "prz_48" }, gears = gears };

        var file = new ChallengesFile { version = 1, cycleWeeks = 13 };
        file.events.Add(new ChallengeEvent
        {
            id = "seven", name = "Seven", type = ChallengeEventType.Progression, weeks = new List<int> { 2, 6 },
            chapters = new List<ChallengeChapter>
            {
                Chapter("seven_one", "seven", "Daytona", 0, new Restriction(1, 0, RestrictionRule.Team, "HEN"), CarPrize(10)),
                Chapter("seven_two", "seven", "Martinsville", 9, new Restriction(1, 2, RestrictionRule.Team, "HEN"), CarPrize(20)),
            },
        });
        var moment = Chapter("moments_late", "moments", "Atlanta", 12, new Restriction(3, 1),
            new ChallengePrize { kind = ChallengePrizeKind.Category, code = "1Star", filter = new PrizeFilter { code = "1Star", minRarity = 1, maxRarity = 1 } });
        moment.raceLaps = 326;
        moment.moment = new ChallengeMoment
        {
            startingLap = 324, playerLivery = "cup01livery29",
            fieldOrder = new List<string> { "29", "player", "x", "3" },
        };
        file.events.Add(new ChallengeEvent
        {
            id = "moments", name = "Moments", type = ChallengeEventType.Replay,
            weeks = Enumerable.Range(1, 13).ToList(),
            chapters = new List<ChallengeChapter> { moment },
        });
        return new ChallengeCatalog(file);
    }

    static MasterDraftProfile Owning(CarCatalog cars, string carId, int carClass, int level = 5)
    {
        var p = MasterDraftProfile.New("chal8", "Ace", cars);
        CarProgression.AwardCar(p.garage, carId, out var owned);
        owned.carClass = carClass;
        p.level = level;
        return p;
    }

    static string projectRoot;

    static string Resource(string path)
    {
        if (projectRoot == null)
            foreach (var start in new[] { TestContext.CurrentContext.TestDirectory, Directory.GetCurrentDirectory() })
            {
                for (var dir = new DirectoryInfo(start); dir != null && projectRoot == null; dir = dir.Parent)
                    if (File.Exists(Path.Combine(dir.FullName, "Assets", "Resources", "MasterDraft", "Challenges.json")))
                        projectRoot = dir.FullName;
                if (projectRoot != null) break;
            }
        Assert.IsNotNull(projectRoot, "Assets/Resources/MasterDraft/Challenges.json not found");
        return File.ReadAllText(Path.Combine(projectRoot, "Assets", "Resources", path + ".json"));
    }

    // ---- the week

    [Test]
    public void TheWeekTurnsEveryMondayAndWrapsAfterThirteen()
    {
        var epoch = ChallengeRace.CycleEpoch;
        Assert.AreEqual(DayOfWeek.Monday, epoch.DayOfWeek);
        Assert.AreEqual(1, ChallengeRace.WeekAt(epoch));
        Assert.AreEqual(1, ChallengeRace.WeekAt(epoch.AddDays(6).AddHours(23)), "Sunday night is still week 1");
        Assert.AreEqual(2, ChallengeRace.WeekAt(epoch.AddDays(7)));
        Assert.AreEqual(13, ChallengeRace.WeekAt(epoch.AddDays(7 * 12)));
        Assert.AreEqual(1, ChallengeRace.WeekAt(epoch.AddDays(7 * 13)), "the cycle wraps");
        Assert.AreEqual(13, ChallengeRace.WeekAt(epoch.AddSeconds(-1)), "the week before the epoch is the cycle's last");
        Assert.AreEqual(6, ChallengeRace.WeekAt(epoch.AddDays(7 * 30), 25), "another cycle length: 30 weeks on is week 6 of 25");
        Assert.AreEqual(epoch.AddDays(14), ChallengeRace.NextWeekStarts(epoch.AddDays(10)));
        Assert.AreEqual(epoch.AddDays(7), ChallengeRace.NextWeekStarts(epoch));
        Assert.AreEqual(epoch, ChallengeRace.NextWeekStarts(epoch.AddHours(-1)));
    }

    // ---- launch plan

    [Test]
    public void PlanCarriesTheChaptersTrackLapsCarAndStrength()
    {
        var cars = SmallCatalog();
        var data = TestChallenges();
        var p = Owning(cars, "tst_2", carClass: 3);

        var plan = ChallengeRace.Plan(p, data, "seven_one", "tst_2", cars, week: 2, out string error);
        Assert.IsNull(error);
        Assert.IsNotNull(plan);
        Assert.AreEqual("seven", plan.eventId);
        Assert.AreEqual("Seven", plan.eventName);
        Assert.AreEqual("seven_one", plan.chapterId);
        Assert.AreEqual(2, plan.week);
        Assert.AreEqual("Daytona", plan.trackId);
        Assert.AreEqual(Challenges.RaceLaps(data.GetChapter("seven_one")), plan.laps);
        Assert.AreEqual(1, plan.startingLap);
        Assert.AreEqual(SeriesRace.AiStrength(0), plan.aiStrength);
        Assert.AreEqual("tst_2", plan.carId);
        Assert.IsFalse(plan.providedCar);
        Assert.AreEqual(2, plan.number);
        Assert.AreEqual("Driver2", plan.driver);
        Assert.AreEqual("tstlivery2", plan.livery);
        Assert.AreEqual(3, plan.carClass);
        Assert.AreEqual(CarProgression.PowerScale(3), plan.powerScale);
        Assert.AreEqual(CarProgression.GripScale(3), plan.gripScale);
        Assert.IsFalse(plan.momentStartSkipped);
        Assert.IsFalse(plan.momentGridSkipped);
        Assert.AreEqual("", ChallengeRace.MomentNote(data.GetChapter("seven_one")));
    }

    [Test]
    public void PlanRefusesWithTheReason()
    {
        var cars = SmallCatalog();
        var data = TestChallenges();
        var p = Owning(cars, "tst_2", carClass: 3);
        CarProgression.AwardCar(p.garage, "tst_5", out var ind);
        ind.carClass = 3;

        Assert.IsNull(ChallengeRace.Plan(p, data, "seven_one", "tst_2", cars, week: 3, out string notLive));
        StringAssert.Contains("this week", notLive);

        Assert.IsNull(ChallengeRace.Plan(p, data, "seven_two", "tst_2", cars, week: 2, out string locked));
        StringAssert.Contains("previous chapter", locked);

        Assert.IsNull(ChallengeRace.Plan(p, data, "seven_one", "tst_5", cars, week: 2, out string team));
        StringAssert.Contains("HEN", team);

        Assert.IsNull(ChallengeRace.Plan(p, data, "seven_one", "tst_3", cars, week: 2, out string notOwned));
        StringAssert.Contains("garage", notOwned);

        Assert.IsNull(ChallengeRace.Plan(p, data, "nope", "tst_2", cars, week: 2, out string missing));
        Assert.IsNotNull(missing);

        Assert.IsNull(ChallengeRace.Plan(null, data, "seven_one", "tst_2", cars, 2, out string noProfile));
        Assert.IsNotNull(noProfile);
    }

    [Test]
    public void AHandedOverCarSkipsTheGarageButNotTheLevel()
    {
        var cars = SmallCatalog();
        var data = TestChallenges();
        var p = Owning(cars, "tst_2", carClass: 0, level: 3);

        // Whatever car is passed, the chapter's own car races — even one outside the catalogue (cup01).
        var plan = ChallengeRace.Plan(p, data, "moments_late", "tst_2", cars, week: 9, out string error);
        Assert.IsNotNull(plan, error);
        Assert.IsTrue(plan.providedCar);
        Assert.AreEqual("cup01_29", plan.carId);
        Assert.AreEqual(29, plan.number);
        Assert.AreEqual("cup01livery29", plan.livery);
        Assert.AreEqual("", plan.driver, "not in the catalogue: the player keeps their own name");
        Assert.AreEqual(0, plan.carClass, "not owned: no class step");
        Assert.AreEqual(1f, plan.powerScale);

        Assert.IsNotNull(ChallengeRace.Plan(p, data, "moments_late", null, cars, week: 9, out _), "no garage car needed");

        p.level = 2;
        Assert.IsNull(ChallengeRace.Plan(p, data, "moments_late", null, cars, week: 9, out string level));
        StringAssert.Contains("level 3", level);
    }

    // RaceScene can't join a race on lap N or set a one-off grid: a moment runs over the laps it has left, from the
    // green, on the usual grid, and says so.
    [Test]
    public void AMomentRunsAsTheLapsItHasLeftFromTheGreen()
    {
        var cars = SmallCatalog();
        var data = TestChallenges();
        var p = Owning(cars, "tst_2", carClass: 0, level: 3);
        var chapter = data.GetChapter("moments_late");

        var plan = ChallengeRace.Plan(p, data, chapter.id, null, cars, week: 1, out string error);
        Assert.IsNotNull(plan, error);
        Assert.AreEqual(326, plan.raceLaps);
        Assert.AreEqual(324, plan.startingLap);
        Assert.AreEqual(3, plan.laps, "laps 324, 325 and 326");
        Assert.IsTrue(plan.momentStartSkipped);
        Assert.IsTrue(plan.momentGridSkipped);
        Assert.AreEqual(SeriesRace.AiStrength(12), plan.aiStrength);

        string note = ChallengeRace.MomentNote(chapter);
        StringAssert.Contains("lap 324 of 326", note);
        StringAssert.Contains("3-lap race", note);
        StringAssert.Contains("usual grid", note);
    }

    // ---- scoring

    [Test]
    public void WinningPaysThePrizeOnceAndUnlocksTheNextChapter()
    {
        var cars = SmallCatalog();
        var data = TestChallenges();
        var p = Owning(cars, "tst_2", carClass: 3);
        int coins = p.coins;
        int gears = p.Car("tst_2").gears;

        var plan = ChallengeRace.Plan(p, data, "seven_one", "tst_2", cars, 2, out _);
        var lost = ChallengeRace.RecordResult(p, data, plan, 4, cars);
        Assert.IsFalse(lost.challenge.completed);
        Assert.AreEqual(0, lost.prizes.Count);
        Assert.AreEqual(0, lost.money);
        Assert.IsFalse(p.Owns("prz_48"));
        Assert.IsNull(ChallengeRace.Plan(p, data, "seven_two", "tst_2", cars, 2, out _), "still locked");

        var won = ChallengeRace.RecordResult(p, data, plan, 1, cars);
        Assert.IsTrue(won.challenge.completed);
        Assert.IsTrue(won.challenge.firstCompletion);
        Assert.AreEqual(RaceRewards.ChallengeMoney, won.money);
        Assert.AreEqual(1, won.prizes.Count);
        Assert.AreEqual("prz_48", won.prizes[0].carId);
        Assert.IsTrue(won.prizes[0].newCar);
        Assert.AreEqual(10, won.prizes[0].gears);
        Assert.IsTrue(p.Owns("prz_48"), "the prize car is in the garage");
        Assert.AreEqual(coins + RaceRewards.ChallengeMoney, p.coins);
        Assert.AreEqual(2, Challenges.FindRecord(p.challengeRecords, "seven_one").attempts);
        Assert.AreEqual(gears, p.Car("tst_2").gears, "a challenge pays no race gears");

        var again = ChallengeRace.RecordResult(p, data, plan, 1, cars);
        Assert.IsTrue(again.challenge.completed);
        Assert.AreEqual(0, again.prizes.Count, "a Progression chapter pays its prize once");

        Assert.IsNotNull(ChallengeRace.Plan(p, data, "seven_two", "tst_2", cars, 2, out string error), error);
    }

    [Test]
    public void AReplayChapterPaysEveryWinAndTheHandedOverCarIsNotKept()
    {
        var cars = SmallCatalog();
        var data = TestChallenges();
        var p = Owning(cars, "tst_2", carClass: 0, level: 3);
        var plan = ChallengeRace.Plan(p, data, "moments_late", null, cars, 1, out _);

        var first = ChallengeRace.RecordResult(p, data, plan, 1, cars);
        var second = ChallengeRace.RecordResult(p, data, plan, 1, cars);
        Assert.AreEqual(1, first.prizes.Count);
        Assert.AreEqual(1, second.prizes.Count);
        Assert.IsTrue(cars.Get(first.prizes[0].carId).rarity == 1, "a one-star car");
        Assert.IsFalse(p.Owns("cup01_29"), "the moment's car was only borrowed");
    }

    [Test]
    public void RecordResultNeedsAKnownChapter()
    {
        var cars = SmallCatalog();
        var p = Owning(cars, "tst_2", 0);
        Assert.Throws<InvalidOperationException>(() =>
            ChallengeRace.RecordResult(p, TestChallenges(), new ChallengeRacePlan { chapterId = "gone" }, 1, cars));
    }

    [Test]
    public void DefaultEntryPrefersTheSelectedCarThatCanEnter()
    {
        var cars = SmallCatalog();
        var data = TestChallenges();
        var p = Owning(cars, "tst_2", carClass: 3);
        CarProgression.AwardCar(p.garage, "tst_4", out var other);
        other.carClass = 3;
        CarProgression.AwardCar(p.garage, "tst_5", out var ind);
        ind.carClass = 3;
        var chapter = data.GetChapter("seven_one");

        p.selectedCar = "tst_4";
        Assert.AreEqual("tst_4", ChallengeRace.DefaultEntry(chapter, p, cars).id);
        p.selectedCar = "tst_5";   // IND can't enter a HEN chapter
        Assert.AreEqual("tst_2", ChallengeRace.DefaultEntry(chapter, p, cars).id);
        Assert.IsNull(ChallengeRace.DefaultEntry(data.GetChapter("moments_late"), p, cars), "a handed-over car needs no entry");
    }

    // ---- the shipped data

    [Test]
    public void EveryShippedChapterPlansARealTrackForAnEligibleCar()
    {
        var cars = CarCatalog.FromJson(Resource(CarCatalog.ResourcePath));
        var data = ChallengeCatalog.FromJson(Resource(ChallengeCatalog.ResourcePath));
        Assert.IsNotNull(cars);
        Assert.IsNotNull(data);
        var known = new HashSet<string>(Draftmaster.Tracks.TrackDimensions.All.Select(d => d.id), StringComparer.Ordinal);

        int planned = 0;
        foreach (var ev in data.Events)
        {
            int week = ev.weeks.Count > 0 ? ev.weeks[0] : 1;
            var p = MasterDraftProfile.New("all", "Ace", cars);
            p.level = 99;
            foreach (var c in ev.chapters)
            {
                string carId = null;
                if (!c.ProvidesCar)
                {
                    var fit = Restrictions.FittingCars(c.restriction, cars).FirstOrDefault();
                    if (fit == null)
                    {
                        // Nothing in the catalogue fits: count it won so a Progression event's next chapter opens.
                        var skipped = Challenges.GetOrAddRecord(p.challengeRecords, c.id);
                        skipped.attempts = skipped.completions = 1;
                        continue;
                    }
                    CarProgression.AwardCar(p.garage, fit.id, out var owned);
                    owned.carClass = CarProgression.MaxClass;
                    carId = fit.id;
                }
                var plan = ChallengeRace.Plan(p, data, c.id, carId, cars, week, out string error);
                Assert.IsNotNull(plan, $"{c.id}: {error}");
                Assert.IsTrue(known.Contains(plan.trackId), $"{c.id}: {plan.trackId} isn't a calendar venue");
                Assert.GreaterOrEqual(plan.laps, 1, c.id);
                Assert.LessOrEqual(plan.laps, plan.raceLaps, c.id);
                Assert.IsFalse(string.IsNullOrEmpty(plan.livery), c.id);
                planned++;

                // Win it, so a Progression event's next chapter opens.
                ChallengeRace.RecordResult(p, data, plan, 1, cars);
            }
        }
        Assert.Greater(planned, 20, "most shipped chapters plan");
    }
}
