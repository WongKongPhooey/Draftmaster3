using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Draftmaster.MasterDraft;
using NUnit.Framework;

// Master Draft's schema-2 profile (Core MasterDraftProfile + ProfileMigration: garage, level, coins, series and
// challenge progress, the starter car, converting a schema-1 card save) and Core RaceRewards (money, XP and level,
// gears to the car raced, championship and challenge prizes). Engine-free — reads Cars.json off disk — so it also
// runs under `dotnet test Tools/HeadlessTests`.
public class MasterDraftProfileTests
{
    // ------------------------------------------------------------------ fixtures

    static string projectRoot;

    static string ProjectRoot()
    {
        if (projectRoot != null) return projectRoot;
        foreach (var start in new[] { TestContext.CurrentContext.TestDirectory, Directory.GetCurrentDirectory() })
        {
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "Assets", "Resources", "MasterDraft", "Cars.json")))
                    return projectRoot = dir.FullName;
        }
        Assert.Fail("Assets/Resources/MasterDraft/Cars.json not found above " + TestContext.CurrentContext.TestDirectory);
        return null;
    }

    static CarCatalog LoadCars()
    {
        var c = CarCatalog.FromJson(File.ReadAllText(Path.Combine(ProjectRoot(), "Assets", "Resources", CarCatalog.ResourcePath + ".json")));
        Assert.IsNotNull(c, "Cars.json did not parse");
        return c;
    }

    static CarDef Car(string id, int number, int rarity = 1, string team = "HEN") =>
        new CarDef { id = id, carset = id.Split('_')[0], number = number, team = team, manufacturer = "CHV", rarity = rarity, type = "Closer", driver = "D" + number };

    // Ten cars in one carset ("tst"), rarities 1-2, plus cup24_77 so the starter resolves to the real one.
    static CarCatalog SmallCatalog()
    {
        var file = new CarCatalogFile { version = 1 };
        file.carsets.Add(new CarsetDef { id = "tst", name = "Test", cars = 10 });
        for (int n = 1; n <= 10; n++) file.cars.Add(Car("tst_" + n, n, n <= 5 ? 1 : 2));
        file.carsets.Add(new CarsetDef { id = "cup24", name = "Cup '24", cars = 1 });
        file.cars.Add(Car("cup24_77", 77));
        return new CarCatalog(file);
    }

    static SeriesDef ThreeRoundSeries(int aiLevel = 10) => new SeriesDef
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

    // What a schema-1 profile file looked like: the card collection, no garage. Written by hand so the test pins
    // the shape the old code saved, not whatever the current class writes.
    const string V1File = @"{
        ""schemaVersion"": 1,
        ""playerId"": ""local-abc123"",
        ""displayName"": ""Ace"",
        ""coins"": 1200,
        ""starterClaimed"": true,
        ""nextUid"": 6,
        ""packsOpened"": 2,
        ""racesRun"": 7,
        ""wins"": 2,
        ""cards"": [
            { ""uid"": ""c1"", ""def"": { ""id"": ""DRV-1"", ""type"": 0, ""rarity"": 3, ""name"": ""Legend Driver"" } },
            { ""uid"": ""c2"", ""def"": { ""id"": ""CAR-CHV-1"", ""type"": 1, ""rarity"": 2, ""name"": ""Epic Car"" } },
            { ""uid"": ""c3"", ""def"": { ""id"": ""CC-x"", ""type"": 2, ""rarity"": 1, ""name"": ""Rare Chief"" } },
            { ""uid"": ""c4"", ""def"": { ""id"": ""ACT-PushToPass"", ""type"": 3, ""rarity"": 0, ""name"": ""Push"" } },
            { ""uid"": ""c5"", ""def"": { ""id"": ""ACT-PushToPass"", ""type"": 3, ""rarity"": 0, ""name"": ""Push"" } }
        ],
        ""team"": { ""activeDriver"": ""c1"", ""bench"": [], ""car"": ""c2"", ""crewChief"": ""c3"", ""actionDeck"": [""c4""] },
        ""myListings"": [ { ""id"": ""L1"", ""fromName"": ""me"", ""mine"": true, ""myCardUid"": ""c5"", ""wantType"": 0, ""wantMinRarity"": 1 } ],
        ""botOffers"": [],
        ""tradeRefreshes"": 3
    }";

    static int V1CardValue =>
        Economy.LegendaryCardCoins + Economy.EpicCardCoins + Economy.RareCardCoins + 2 * Economy.CommonCardCoins;

    // ------------------------------------------------------------------ new profile

    [Test]
    public void NewProfileIsSchemaTwoWithAStarterCar()
    {
        var p = MasterDraftProfile.New("p1", "Ace", LoadCars());
        Assert.AreEqual(MasterDraftProfile.CurrentSchema, p.schemaVersion);
        Assert.AreEqual(2, MasterDraftProfile.CurrentSchema);
        Assert.AreEqual(MasterDraftProfile.FirstLevel, p.level);
        Assert.AreEqual(0, p.xp);
        Assert.AreEqual(Economy.StartingCoins, p.coins);
        Assert.AreEqual(1, p.garage.Count, "one starter car");
        Assert.AreEqual(StarterCars.Ids[0], p.garage[0].id);
        Assert.AreEqual(CarProgression.MinClass, p.garage[0].carClass);
        Assert.AreEqual(CarProgression.StarterGears, p.garage[0].gears);
        Assert.AreEqual(p.garage[0].id, p.selectedCar);
        Assert.AreSame(p.garage[0], p.SelectedCar());
        Assert.IsFalse(p.migration.Happened(), "a new profile was never converted");

        // Without a catalogue the first starter id is used; with one that lacks every starter, its lowest rarity.
        Assert.AreEqual(StarterCars.Ids[0], MasterDraftProfile.New("p2").selectedCar);
        var odd = new CarCatalogFile { version = 1 };
        odd.cars.Add(Car("zzz_1", 1, rarity: 3));
        odd.cars.Add(Car("zzz_2", 2, rarity: 1));
        Assert.AreEqual("zzz_2", StarterCars.Pick(new CarCatalog(odd)));
    }

    [Test]
    public void EveryStarterCarIsInTheCatalogue()
    {
        var cars = LoadCars();
        foreach (var id in StarterCars.Ids) Assert.IsTrue(cars.Contains(id), $"starter {id} missing from Cars.json");
    }

    // ------------------------------------------------------------------ schema 1 -> 2

    [Test]
    public void V1FileMigratesToAStarterCarPlusCoins()
    {
        var p = MasterDraftProfile.FromJson(V1File, LoadCars(), out int from);
        Assert.IsNotNull(p);
        Assert.AreEqual(1, from, "reported as converted from schema 1");
        Assert.AreEqual(MasterDraftProfile.CurrentSchema, p.schemaVersion);

        // Kept: identity, history, the old balance.
        Assert.AreEqual("local-abc123", p.playerId);
        Assert.AreEqual("Ace", p.displayName);
        Assert.AreEqual(7, p.racesRun);
        Assert.AreEqual(2, p.wins);

        // Converted: every card into coins on top of the old balance, and a starter car.
        Assert.AreEqual(1200 + V1CardValue, p.coins);
        Assert.AreEqual(1, p.garage.Count);
        Assert.AreEqual(StarterCars.Ids[0], p.garage[0].id);
        Assert.AreEqual(CarProgression.StarterGears, p.garage[0].gears);
        Assert.AreEqual(StarterCars.Ids[0], p.selectedCar);
        Assert.AreEqual(MasterDraftProfile.FirstLevel, p.level);

        // The card side is emptied (and the card mode's starter can be claimed again until it retires).
        Assert.AreEqual(0, p.cards.Count);
        Assert.IsTrue(string.IsNullOrEmpty(p.team.activeDriver));
        Assert.AreEqual(0, p.myListings.Count);
        Assert.IsFalse(p.starterClaimed);

        // Nothing silent: the conversion is on the record.
        var m = p.migration;
        Assert.IsTrue(m.Happened());
        Assert.AreEqual(1, m.fromSchema);
        Assert.AreEqual(1200, m.coinsBefore);
        Assert.AreEqual(5, m.cardsConverted);
        Assert.AreEqual(2, m.commons);
        Assert.AreEqual(1, m.rares);
        Assert.AreEqual(1, m.epics);
        Assert.AreEqual(1, m.legendaries);
        Assert.AreEqual(V1CardValue, m.coinsFromCards);
        Assert.AreEqual(4, m.teamCards, "driver, car, chief and one deck card");
        Assert.AreEqual(1, m.listingsCancelled);
        Assert.AreEqual(StarterCars.Ids[0], m.starterCar);
        Assert.IsFalse(m.acknowledged);
        Assert.IsTrue(DateTime.TryParse(m.migratedUtc, out _), "timestamp: " + m.migratedUtc);
        StringAssert.Contains("5 cards", m.Describe());
        StringAssert.Contains(StarterCars.Ids[0], m.Describe());

        // Saved and read back, it's schema 2 and doesn't convert again.
        var again = MasterDraftProfile.FromJson(p.ToJson(), LoadCars(), out int from2);
        Assert.AreEqual(0, from2, "a schema-2 file is not converted");
        Assert.AreEqual(p.ToJson(), again.ToJson());
        Assert.AreEqual(1, again.migration.fromSchema);
    }

    [Test]
    public void FileWithNoSchemaVersionIsReadAsSchemaOne()
    {
        string json = @"{ ""playerId"": ""old"", ""coins"": 300, ""cards"": [
            { ""uid"": ""c1"", ""def"": { ""id"": ""DRV-1"", ""type"": 0, ""rarity"": 1, ""name"": ""Rare"" } } ] }";
        var p = MasterDraftProfile.FromJson(json, null, out int from);
        Assert.AreEqual(1, from);
        Assert.AreEqual(300 + Economy.RareCardCoins, p.coins);
        Assert.AreEqual(StarterCars.Ids[0], p.selectedCar);
    }

    [Test]
    public void EmptyV1ProfileStillGetsAStarterCar()
    {
        var p = MasterDraftProfile.FromJson(@"{ ""schemaVersion"": 1, ""playerId"": ""x"", ""coins"": 500 }", null, out int from);
        Assert.AreEqual(1, from);
        Assert.AreEqual(500, p.coins);
        Assert.AreEqual(0, p.migration.cardsConverted);
        Assert.AreEqual(1, p.garage.Count);
        StringAssert.Contains("empty", p.migration.Describe());
    }

    [Test]
    public void ANewerSchemaIsNotDowngraded()
    {
        var p = MasterDraftProfile.FromJson(@"{ ""schemaVersion"": 3, ""playerId"": ""future"", ""coins"": 9 }", null, out int from);
        Assert.AreEqual(0, from);
        Assert.AreEqual(3, p.schemaVersion);
        Assert.AreEqual(9, p.coins);
    }

    [Test]
    public void UnreadableTextIsNull()
    {
        Assert.IsNull(MasterDraftProfile.FromJson(""));
        Assert.IsNull(MasterDraftProfile.FromJson("   "));
        Assert.IsNull(MasterDraftProfile.FromJson("{ not json"));
    }

    // ------------------------------------------------------------------ schema 2 on disk

    [Test]
    public void SchemaTwoProfileRoundTripsWithProgress()
    {
        var cars = SmallCatalog();
        var p = MasterDraftProfile.New("rt", "Ace", cars);
        CarProgression.AwardCar(p.garage, "tst_3");
        p.selectedCar = "tst_3";
        p.altPaints.Add("cup20livery48alt1");
        var series = ThreeRoundSeries();
        p.StartChampionship(series, cars.Get("tst_3"), cars);
        RaceRewards.ChampionshipRound(p, series, null, new[] { "tst_3", "tst_1" }, cars);
        p.level = 4;
        p.xp = 60;
        Challenges.GetOrAddRecord(p.challengeRecords, "ch_1").attempts = 2;

        var back = MasterDraftProfile.FromJson(p.ToJson(), cars, out int from);
        Assert.AreEqual(0, from);
        Assert.AreEqual(p.ToJson(), back.ToJson());
        Assert.AreEqual(2, back.garage.Count);
        Assert.AreEqual("tst_3", back.selectedCar);
        Assert.AreEqual(4, back.level);
        Assert.AreEqual(60, back.xp);
        var c = back.Championship(series.id);
        Assert.IsNotNull(c);
        Assert.AreEqual(1, c.round);
        Assert.AreEqual(3, c.Length);
        Assert.AreEqual(40, c.table.First(e => e.carId == "tst_3").points);
        Assert.AreEqual(2, back.challengeRecords[0].attempts);
        CollectionAssert.AreEqual(new[] { "cup20livery48alt1" }, back.altPaints);
    }

    [Test]
    public void LoadingSanitisesAHandEditedProfile()
    {
        string json = @"{ ""schemaVersion"": 2, ""playerId"": ""h"", ""coins"": -5, ""level"": 0, ""xp"": -3,
            ""selectedCar"": ""not_owned"",
            ""garage"": [ { ""id"": ""tst_1"", ""carClass"": 9, ""gears"": -4 }, { ""id"": ""tst_1"", ""carClass"": 2, ""gears"": 5 }, { ""id"": """" } ],
            ""altPaints"": [ ""a"", ""a"", """" ] }";
        var p = MasterDraftProfile.FromJson(json, SmallCatalog());
        Assert.AreEqual(1, p.garage.Count, "duplicate and blank cars dropped");
        Assert.AreEqual(CarProgression.MaxClass, p.garage[0].carClass);
        Assert.AreEqual(0, p.garage[0].gears);
        Assert.AreEqual("tst_1", p.selectedCar, "selection falls back to an owned car");
        Assert.AreEqual(MasterDraftProfile.FirstLevel, p.level);
        Assert.AreEqual(0, p.xp);
        Assert.AreEqual(0, p.coins);
        CollectionAssert.AreEqual(new[] { "a" }, p.altPaints);

        var empty = MasterDraftProfile.FromJson(@"{ ""schemaVersion"": 2, ""playerId"": ""e"", ""garage"": [] }", SmallCatalog());
        Assert.AreEqual(StarterCars.Ids[0], empty.selectedCar, "an empty garage gets the starter back");
    }

    // ------------------------------------------------------------------ reward tables

    [Test]
    public void PrizeMoneyFollowsDraftmasterTwo()
    {
        Assert.AreEqual(10000, RaceRewards.PrizeMoney(1));
        Assert.AreEqual(7500, RaceRewards.PrizeMoney(2));
        Assert.AreEqual(3400, RaceRewards.PrizeMoney(10));
        Assert.AreEqual(400, RaceRewards.PrizeMoney(40));
        Assert.AreEqual(100, RaceRewards.PrizeMoney(43));
        Assert.AreEqual(100, RaceRewards.PrizeMoney(60));
        Assert.AreEqual(100, RaceRewards.PrizeMoney(0), "unclassified pays the last entry, not the winner's");
        for (int pos = 1; pos < 43; pos++)
            Assert.Greater(RaceRewards.PrizeMoney(pos), RaceRewards.PrizeMoney(pos + 1), $"P{pos} pays more than P{pos + 1}");
    }

    [Test]
    public void XpAndLevelsFollowDraftmasterTwo()
    {
        Assert.AreEqual(110, RaceRewards.RaceXp(1, 1), "comment in RaceResultsUI: 1st = 110");
        Assert.AreEqual(100, RaceRewards.RaceXp(1, 0));
        Assert.AreEqual(28, RaceRewards.RaceXp(5, 0));
        Assert.AreEqual(19, RaceRewards.RaceXp(10, 0));
        Assert.AreEqual(13, RaceRewards.RaceXp(30, 0));
        Assert.AreEqual(200, RaceRewards.RaceXp(1, 10));
        Assert.AreEqual(12, RaceRewards.RaceXp(0, 0), "unclassified = 40th");

        Assert.AreEqual(100, RaceRewards.XpToNextLevel(1));
        Assert.AreEqual(350, RaceRewards.XpToNextLevel(10));
        Assert.AreEqual(16000, RaceRewards.XpToNextLevel(45));
        Assert.AreEqual(int.MaxValue, RaceRewards.XpToNextLevel(RaceRewards.MaxLevel));

        var p = MasterDraftProfile.New("xp");
        Assert.AreEqual(0, RaceRewards.AddXp(p, 99));
        Assert.AreEqual(1, p.level);
        Assert.AreEqual(2, RaceRewards.AddXp(p, 151), "99 + 151 = 250: 100 to level 2, 125 to level 3");
        Assert.AreEqual(3, p.level);
        Assert.AreEqual(25, p.xp, "the rest carries over");
        Assert.AreEqual(0.25f / 1.5f, RaceRewards.LevelProgress(p), 1e-4);

        p.level = RaceRewards.MaxLevel;
        p.xp = 0;
        Assert.AreEqual(0, RaceRewards.AddXp(p, 1000000));
        Assert.AreEqual(RaceRewards.MaxLevel, p.level, "the top level is the top");
        Assert.AreEqual(1f, RaceRewards.LevelProgress(p));
    }

    [Test]
    public void GearsFollowDraftmasterTwo()
    {
        Assert.AreEqual(3, RaceRewards.MaxRaceGears(0));
        Assert.AreEqual(5, RaceRewards.MaxRaceGears(10));
        Assert.AreEqual(6, RaceRewards.MaxRaceGears(14));
        Assert.AreEqual(7, RaceRewards.MaxRaceGears(15));

        // One-off race, full field: top gears for P1-P4, one less every four places, nothing once it runs out.
        Assert.AreEqual(5, RaceRewards.RaceGears(1, 10, 40));
        Assert.AreEqual(5, RaceRewards.RaceGears(4, 10, 40));
        Assert.AreEqual(4, RaceRewards.RaceGears(5, 10, 40));
        Assert.AreEqual(1, RaceRewards.RaceGears(17, 10, 40));
        Assert.AreEqual(0, RaceRewards.RaceGears(21, 10, 40));
        Assert.AreEqual(0, RaceRewards.RaceGears(40, 10, 40));
        // A small field scales down (counted as at least 24).
        Assert.AreEqual(3, RaceRewards.RaceGears(1, 10, 24));
        Assert.AreEqual(3, RaceRewards.RaceGears(1, 10, 10));

        // Championships multiply by the rounds; long seasons have a floor of half the rounds.
        Assert.AreEqual(15, RaceRewards.ChampionshipGears(1, 10, 40, 3));
        Assert.AreEqual(0, RaceRewards.ChampionshipGears(30, 10, 40, 3));
        Assert.AreEqual(7 * 33, RaceRewards.ChampionshipGears(1, 15, 40, 33));
        Assert.AreEqual(17, RaceRewards.ChampionshipGears(40, 15, 40, 33));

        Assert.AreEqual(3 * 10000, RaceRewards.ChampionshipMoney(1, 3));

        // rewardRatio: ceil(rounds/2) x ceil(ai/5) / ceil(place/2).
        Assert.AreEqual(4, RaceRewards.ChampionshipPrizeGears(1, 10, 3));
        Assert.AreEqual(2, RaceRewards.ChampionshipPrizeGears(3, 10, 3));
        Assert.AreEqual(0, RaceRewards.ChampionshipPrizeGears(20, 10, 3));
        Assert.AreEqual(1, RaceRewards.ChampionshipPrizeGears(1, 0, 3), "the champion always gets a prize car");
        Assert.AreEqual(0, RaceRewards.ChampionshipPrizeGears(2, 0, 3));
    }

    // ------------------------------------------------------------------ paying races

    [Test]
    public void ARacePaysMoneyXpAndGearsToTheCarRaced()
    {
        var p = MasterDraftProfile.New("race", "Ace", SmallCatalog());
        string car = p.selectedCar;
        int coins = p.coins, gears = p.Car(car).gears;

        var s = RaceRewards.Race(p, car, 1, 10, 40);
        Assert.AreEqual(RaceRewardKind.Race, s.kind);
        Assert.AreEqual(10000, s.money);
        Assert.AreEqual(coins + 10000, p.coins);
        Assert.AreEqual(p.coins, s.coins);
        Assert.AreEqual(200, s.xp);
        Assert.AreEqual(1, s.levelBefore);
        Assert.AreEqual(2, s.levelAfter, "200 XP clears level 1's 100");
        Assert.AreEqual(1, s.LevelsGained);
        Assert.AreEqual(5, s.gears);
        Assert.AreEqual(gears + 5, p.Car(car).gears);
        Assert.AreEqual(1, p.racesRun);
        Assert.AreEqual(1, p.wins);
        Assert.AreEqual(0, s.prizes.Count, "no prize pool given, no raffle");

        // A car the player doesn't own gets nothing and isn't handed over.
        var borrowed = RaceRewards.Race(p, "tst_9", 1, 10, 40);
        Assert.AreEqual(0, borrowed.gears);
        Assert.IsFalse(p.Owns("tst_9"));

        // Back of the field: the last purse, a little XP, no gears.
        var back = RaceRewards.Race(p, car, 0, 10, 40);
        Assert.AreEqual(RaceRewards.PrizeMoney(40), back.money);
        Assert.AreEqual(0, back.gears);
        Assert.AreEqual(3, p.racesRun);
        Assert.AreEqual(2, p.wins);
    }

    [Test]
    public void ATopThreeRaceDrawsAPrizeFromThePool()
    {
        var cars = SmallCatalog();
        var p = MasterDraftProfile.New("raffle", "Ace", cars);
        var pool = new PrizeFilter { code = "Rarity2", minRarity = 2, maxRarity = 2 };
        var s = RaceRewards.Race(p, p.selectedCar, 2, 10, 40, pool, cars);
        Assert.AreEqual(1, s.prizes.Count, "top 3 always draws");
        var o = s.prizes[0];
        Assert.AreEqual(2, cars.Get(o.carId).rarity, "drawn from the pool");
        Assert.AreEqual(1, o.gears);
        Assert.IsTrue(o.newCar);
        Assert.AreEqual(1, p.Car(o.carId).gears, "a new car arrives holding the prize gears");

        // Same profile state, same draw.
        var q = MasterDraftProfile.New("raffle", "Ace", cars);
        Assert.AreEqual(o.carId, RaceRewards.Race(q, q.selectedCar, 2, 10, 40, pool, cars).prizes[0].carId);
    }

    [Test]
    public void AChampionshipPaysAtTheEndAndIsRecorded()
    {
        var cars = SmallCatalog();
        var series = ThreeRoundSeries(aiLevel: 10);
        var p = MasterDraftProfile.New("champ", "Ace", cars);
        CarProgression.AwardCar(p.garage, "tst_1");
        int coins = p.coins;
        Assert.Throws<ArgumentException>(() => p.StartChampionship(series, cars.Get("tst_4"), cars), "not owned");
        Assert.Throws<InvalidOperationException>(() => RaceRewards.ChampionshipRound(p, series, null, new[] { "tst_1" }, cars));

        p.StartChampionship(series, cars.Get("tst_1"), cars);
        var order = Enumerable.Range(1, 10).Select(n => "tst_" + n).ToList();   // the player (tst_1) wins every round

        for (int r = 1; r <= 2; r++)
        {
            var mid = RaceRewards.ChampionshipRound(p, series, null, order, cars);
            Assert.AreEqual(RaceRewardKind.ChampionshipRound, mid.kind);
            Assert.AreEqual(r, mid.round.round);
            Assert.AreEqual(0, mid.money, "the purse waits for the end");
            Assert.AreEqual(0, mid.gears);
            Assert.AreEqual(200, mid.xp);
            Assert.IsFalse(mid.championshipOver);
        }
        Assert.AreEqual(coins, p.coins);

        int gearsBefore = p.Car("tst_1").gears;
        var end = RaceRewards.ChampionshipRound(p, series, null, order, cars);
        Assert.AreEqual(RaceRewardKind.ChampionshipFinal, end.kind);
        Assert.IsTrue(end.championshipOver);
        Assert.AreEqual(1, end.championshipPosition);
        Assert.AreEqual(3, end.rounds);
        Assert.AreEqual(RaceRewards.ChampionshipMoney(1, 3), end.money);
        Assert.AreEqual(coins + end.money, p.coins);
        Assert.AreEqual(RaceRewards.ChampionshipGears(1, 10, 10, 3), end.gears);
        Assert.AreEqual(gearsBefore + end.gears, p.Car("tst_1").gears);
        Assert.AreEqual(1, end.prizes.Count);
        Assert.AreEqual(2, cars.Get(end.prizes[0].carId).rarity, "prize car from the series' pool");
        Assert.AreEqual(RaceRewards.ChampionshipPrizeGears(1, 10, 3), end.prizes[0].gears);

        Assert.IsNull(p.Championship(series.id), "finished championships leave the profile");
        var rec = p.SeriesRecord(series.id);
        Assert.AreEqual(1, rec.championships);
        Assert.AreEqual(1, rec.titles);
        Assert.AreEqual(1, rec.bestFinish);
        Assert.AreEqual(3, p.wins);
        Assert.AreEqual(3, p.racesRun);
    }

    [Test]
    public void AChallengePaysItsPrizeOnceForAProgressionEvent()
    {
        var cars = SmallCatalog();
        var chapter = new ChallengeChapter
        {
            id = "ev_ch1", eventId = "ev", name = "Chapter 1", aiLevel = 5, track = "Daytona",
            prize = new ChallengePrize { kind = ChallengePrizeKind.Car, code = "D7", cars = new List<string> { "tst_7" } },
        };
        var ev = new ChallengeEvent
        {
            id = "ev", name = "Event", type = ChallengeEventType.Progression, weeks = new List<int> { 1 },
            chapters = new List<ChallengeChapter> { chapter },
        };
        var p = MasterDraftProfile.New("chal", "Ace", cars);
        string car = p.selectedCar;
        int gears = p.Car(car).gears;

        var lose = RaceRewards.Challenge(p, ev, chapter, car, 4, false, cars);
        Assert.AreEqual(RaceRewardKind.Challenge, lose.kind);
        Assert.AreEqual(0, lose.money);
        Assert.IsFalse(lose.challenge.completed);
        Assert.AreEqual(0, lose.prizes.Count);
        Assert.AreEqual(RaceRewards.RaceXp(4, 5), lose.xp);

        var win = RaceRewards.Challenge(p, ev, chapter, car, 1, false, cars);
        Assert.AreEqual(RaceRewards.ChallengeMoney, win.money);
        Assert.IsTrue(win.challenge.firstCompletion);
        Assert.AreEqual(1, win.prizes.Count);
        Assert.AreEqual("tst_7", win.prizes[0].carId);
        Assert.IsTrue(p.Owns("tst_7"));
        Assert.AreEqual(CarProgression.StarterGears, p.Car("tst_7").gears);
        Assert.AreEqual(gears, p.Car(car).gears, "challenges pay no gears to the car raced");

        var again = RaceRewards.Challenge(p, ev, chapter, car, 1, false, cars);
        Assert.AreEqual(RaceRewards.ChallengeMoney, again.money);
        Assert.AreEqual(0, again.prizes.Count, "a Progression chapter pays its prize once");
        var rec = Challenges.FindRecord(p.challengeRecords, chapter.id);
        Assert.AreEqual(3, rec.attempts);
        Assert.AreEqual(2, rec.completions);
        Assert.AreEqual(1, rec.prizesPaid);

        // A moment completed counts without the win.
        var p2 = MasterDraftProfile.New("chal2", "Ace", cars);
        var moment = RaceRewards.Challenge(p2, ev, chapter, p2.selectedCar, 6, true, cars);
        Assert.IsTrue(moment.challenge.completed);
        Assert.AreEqual(1, moment.prizes.Count);
        Assert.AreEqual(0, p2.wins);
    }
}
