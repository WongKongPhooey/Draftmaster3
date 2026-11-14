using System.Collections.Generic;
using System.IO;
using System.Linq;
using Draftmaster.MasterDraft;
using NUnit.Framework;

// Master Draft's series tree (Core SeriesTree.cs): the Series screen's overworld, built from the "tree" placements
// in Assets/Resources/MasterDraft/Series.json and Challenges.json (the hand-authored master copy). Checks the shipped
// tree, and the rules — clearing a series by points or a title opens what comes after it, challenges clear by
// winning every chapter, branches can take only their carsets and hand over a starter car. Engine-free, so it also
// runs under `dotnet test Tools/HeadlessTests`.
public class MasterDraftSeriesTreeTests
{
    static string projectRoot;

    static string ProjectRoot()
    {
        if (projectRoot != null) return projectRoot;
        foreach (var start in new[] { TestContext.CurrentContext.TestDirectory, Directory.GetCurrentDirectory() })
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "Assets", "Resources", "MasterDraft", "Series.json")))
                    return projectRoot = dir.FullName;
        Assert.Fail("Assets/Resources/MasterDraft/Series.json not found above " + TestContext.CurrentContext.TestDirectory);
        return null;
    }

    static string Resource(string path) => File.ReadAllText(Path.Combine(ProjectRoot(), "Assets", "Resources", path + ".json"));

    // ---- test data ------------------------------------------------------------------------------------------

    // Two carsets: "aaa" (cars 1-3) and "bbb" (cars 4-6).
    static CarCatalog Cars()
    {
        var file = new CarCatalogFile { version = 1 };
        file.carsets.Add(new CarsetDef { id = "aaa", name = "Aaa '01", cars = 3 });
        file.carsets.Add(new CarsetDef { id = "bbb", name = "Bbb '01", cars = 3 });
        for (int i = 1; i <= 6; i++)
        {
            string set = i <= 3 ? "aaa" : "bbb";
            file.cars.Add(new CarDef { id = CarCatalog.IdFor(set, i), carset = set, number = i, driver = "D" + i, team = "T", manufacturer = "CHV", rarity = 1, type = "Rookie" });
        }
        return new CarCatalog(file);
    }

    static SeriesDef Series(string id, int rounds, int minLevel = 0, int minClass = 0, string carset = null)
    {
        var s = new SeriesDef { id = id, menu = "m", name = id, restriction = new Restriction(minLevel, minClass) };
        if (carset != null) s.restriction.carsets.Add(carset);
        for (int i = 0; i < rounds; i++) s.rounds.Add(new SeriesRound { track = "Daytona", baseLaps = 4 });
        return s;
    }

    // start -> (left, right); left -> ev (a challenge event); right -> late (bbb only, starter car bbb_4).
    static DraftSeriesCatalog SeriesData()
    {
        var file = new SeriesFile { version = 1 };
        var menu = new SeriesMenu { id = "m", name = "M" };
        menu.series.Add(Series("start", 3, carset: "aaa"));
        menu.series.Add(Series("left", 4, carset: "aaa"));
        menu.series.Add(Series("right", 4, carset: "aaa"));
        menu.series.Add(Series("late", 5, minClass: 2, carset: "bbb"));
        file.menus.Add(menu);
        return new DraftSeriesCatalog(file);
    }

    static ChallengeCatalog ChallengeData()
    {
        ChallengeChapter Chapter(string id) => new ChallengeChapter
        {
            id = id, eventId = "ev", name = id, track = "Daytona", aiLevel = 3, baseLaps = 4, restriction = new Restriction(0, 0),
        };
        var file = new ChallengesFile { version = 1, cycleWeeks = 13 };
        file.events.Add(new ChallengeEvent
        {
            id = "ev", name = "Ev", type = ChallengeEventType.Progression, weeks = Enumerable.Range(1, 13).ToList(),
            chapters = new List<ChallengeChapter> { Chapter("ev_one"), Chapter("ev_two") },
        });
        return new ChallengeCatalog(file);
    }

    static TreeNode Node(string id, string kind, string target, int col, int row, string[] after = null, string[] carsets = null,
        string starter = "", int points = 0) => new TreeNode
    {
        id = id, kind = kind, target = target, col = col, row = row, after = (after ?? new string[0]).ToList(),
        carsets = (carsets ?? new string[0]).ToList(), starterCar = starter, unlockPoints = points,
    };

    static SeriesTree Tree() => new SeriesTree(new SeriesTreeFile
    {
        version = 1,
        nodes = new List<TreeNode>
        {
            Node("n_start", TreeNodeKind.Series, "start", 0, 0, carsets: new[] { "aaa" }),
            Node("n_left", TreeNodeKind.Series, "left", 1, -1, new[] { "n_start" }, new[] { "aaa" }, points: 100),
            Node("n_right", TreeNodeKind.Series, "right", 1, 1, new[] { "n_start" }, new[] { "aaa" }),
            Node("n_ev", TreeNodeKind.Challenge, "ev", 2, -1, new[] { "n_left" }),
            Node("n_late", TreeNodeKind.Series, "late", 2, 1, new[] { "n_right", "n_ev" }, new[] { "bbb" }, "bbb_4"),
        },
    });

    static MasterDraftProfile Profile(params OwnedCar[] cars) => new MasterDraftProfile
    {
        schemaVersion = MasterDraftProfile.CurrentSchema, playerId = "tree", level = 1, garage = cars.ToList(),
        selectedCar = cars.Length > 0 ? cars[0].id : "",
    };

    static void Record(MasterDraftProfile p, string seriesId, int points, int titles = 0) =>
        p.seriesRecords.Add(new SeriesRecord { seriesId = seriesId, championships = 1, bestPoints = points, titles = titles, bestFinish = titles > 0 ? 1 : 5 });

    static void Win(MasterDraftProfile p, string chapterId) =>
        Challenges.GetOrAddRecord(p.challengeRecords, chapterId).completions = 1;

    // ---- the shipped tree --------------------------------------------------------------------------------

    [Test]
    public void ShippedTreeIsSoundAgainstTheCatalogues()
    {
        var series = DraftSeriesCatalog.FromJson(Resource(DraftSeriesCatalog.ResourcePath));
        var challenges = ChallengeCatalog.FromJson(Resource(ChallengeCatalog.ResourcePath));
        var cars = CarCatalog.FromJson(Resource(CarCatalog.ResourcePath));
        var tree = SeriesTree.Build(series, challenges);
        Assert.Greater(tree.Nodes.Count, 0);
        CollectionAssert.IsEmpty(tree.Validate(series, challenges, cars));

        // One way in: Rookie Midgets, which a new profile's starter car can enter.
        var roots = tree.Nodes.Where(n => n.after.Count == 0).ToList();
        Assert.AreEqual(1, roots.Count);
        Assert.AreEqual("rookie_midgets", roots[0].target);
        var profile = MasterDraftProfile.New("tree-real", "Ace", cars);
        var root = series.Get(roots[0].target);
        Assert.AreEqual(1, Restrictions.EligibleGarage(root.restriction, cars, profile.garage, profile.level).Count,
            $"the starter {profile.selectedCar} can't enter {root.id}");
        Assert.AreEqual(TreeNodeState.Open, tree.State(roots[0], profile, series, challenges));

        // Every branch's starter car can enter its series once collected.
        foreach (var n in tree.Nodes.Where(x => !string.IsNullOrEmpty(x.starterCar)))
        {
            var p = Profile();
            foreach (var parent in tree.Parents(n)) Record(p, parent.target, 0, titles: 1);
            Assert.IsTrue(tree.CanCollectStarter(n, p, series, challenges, cars), n.id);
            var car = tree.CollectStarter(n, p, series, challenges, cars);
            Assert.IsNotNull(car, n.id);
            Assert.IsTrue(Restrictions.Check(series.Get(n.target).restriction, cars.Get(car.id), car, p.level).Eligible, n.id);
        }
    }

    // The ladder as Josh laid it out (2026-11-13): Rookie Midgets -> Legend Cars / Late Models; Legend Cars -> ARCA
    // Regionals / Super Late Models; Late Models -> Super Late Models / Sprint Cars (Dirt), and the Snowball Derby.
    [Test]
    public void ShippedLadderOpensInOrder()
    {
        var series = DraftSeriesCatalog.FromJson(Resource(DraftSeriesCatalog.ResourcePath));
        var challenges = ChallengeCatalog.FromJson(Resource(ChallengeCatalog.ResourcePath));
        var tree = SeriesTree.Build(series, challenges);
        string[] Opens(string id) => tree.Children(tree.Get(id)).Select(n => n.id).OrderBy(x => x).ToArray();

        Assert.AreEqual("Rookie Midgets", series.Get("rookie_midgets").name);
        CollectionAssert.AreEqual(new[] { "late_models", "legend_cars" }, Opens("rookie_midgets"));
        CollectionAssert.AreEqual(new[] { "arca_regionals", "super_late_models" }, Opens("legend_cars"));
        CollectionAssert.AreEqual(new[] { "snowball_derby", "sprint_cars_dirt", "super_late_models" }, Opens("late_models"));

        // The Snowball Derby: one challenge race, far longer than a Late Models round at the same track.
        var derby = tree.Get("snowball_derby");
        Assert.IsTrue(derby.IsChallenge);
        var chapters = SeriesTree.Chapters(derby, challenges);
        Assert.AreEqual(1, chapters.Count);
        var lateModels = series.Get("late_models");
        int standard = lateModels.rounds.Where(r => r.track == chapters[0].track)
            .Select(r => SeriesRules.RaceLaps(r.baseLaps, lateModels.aiLevel)).DefaultIfEmpty(0).Max();
        Assert.Greater(standard, 0, "Late Models races the Derby's track");
        Assert.GreaterOrEqual(Challenges.RaceLaps(chapters[0]), standard * 3);
    }

    [Test]
    public void BuildPlacesOnlyWhatCarriesATreePlacement()
    {
        var series = SeriesData();
        var challenges = ChallengeData();
        series.Get("start").tree = new TreePlacement { col = 0, row = 0, branch = "B", icon = "x" };
        series.Get("left").tree = new TreePlacement { col = 1, row = -1, after = new List<string> { "start" }, unlockPoints = 100 };
        series.Get("right").tree = new TreePlacement();   // col -1: off the tree
        challenges.Get("ev").tree = new TreePlacement { col = 2, row = 0, after = new List<string> { "left" } };

        var tree = SeriesTree.Build(series, challenges);
        CollectionAssert.AreEquivalent(new[] { "start", "left", "ev" }, tree.Nodes.Select(n => n.id).ToArray());
        Assert.AreEqual("B", tree.Get("start").branch);
        Assert.AreEqual("x", SeriesTree.IconFor(tree.Get("start"), series));
        CollectionAssert.AreEqual(new[] { "aaa" }, tree.Get("start").carsets, "carsets come from the series' entry rule");
        Assert.AreEqual(100, SeriesTree.UnlockPoints(tree.Get("left"), series));
        Assert.IsTrue(tree.Get("ev").IsChallenge);
        Assert.AreSame(tree.Get("ev"), tree.ForChapter("ev_two", "ev"));
        Assert.IsNull(tree.ForSeries("right"));
        Assert.IsNull(tree.ForSeries("late"), "no placement at all");
    }

    // ---- the rules -----------------------------------------------------------------------------------------

    [Test]
    public void OnlyTheStartIsOpenOnANewProfile()
    {
        var tree = Tree();
        var p = Profile();
        Assert.AreEqual(TreeNodeState.Open, tree.State(tree.Get("n_start"), p, SeriesData(), ChallengeData()));
        foreach (var id in new[] { "n_left", "n_right", "n_ev", "n_late" })
            Assert.AreEqual(TreeNodeState.Locked, tree.State(tree.Get(id), p, SeriesData(), ChallengeData()), id);
    }

    [Test]
    public void EnoughPointsClearASeriesAndOpenWhatComesAfter()
    {
        var tree = Tree();
        var series = SeriesData();
        var start = tree.Get("n_start");
        Assert.AreEqual(90, SeriesTree.UnlockPoints(start, series), "3 rounds x 30");
        Assert.AreEqual(100, SeriesTree.UnlockPoints(tree.Get("n_left"), series), "the node's own figure wins");

        var p = Profile();
        Record(p, "start", 89);
        Assert.IsFalse(SeriesTree.IsCleared(start, p, series, ChallengeData()));
        Assert.AreEqual(TreeNodeState.Locked, tree.State(tree.Get("n_left"), p, series, ChallengeData()));

        p.seriesRecords[0].bestPoints = 90;
        Assert.AreEqual(TreeNodeState.Cleared, tree.State(start, p, series, ChallengeData()));
        Assert.AreEqual(TreeNodeState.Open, tree.State(tree.Get("n_left"), p, series, ChallengeData()));
        Assert.AreEqual(TreeNodeState.Open, tree.State(tree.Get("n_right"), p, series, ChallengeData()));
        Assert.AreEqual(TreeNodeState.Locked, tree.State(tree.Get("n_late"), p, series, ChallengeData()));
    }

    [Test]
    public void ATitleClearsASeriesWhateverThePoints()
    {
        var tree = Tree();
        var p = Profile();
        Record(p, "start", 10, titles: 1);
        Assert.IsTrue(SeriesTree.IsCleared(tree.Get("n_start"), p, SeriesData(), ChallengeData()));
    }

    [Test]
    public void AnyClearedParentOpensANode()
    {
        var tree = Tree();
        var p = Profile();
        Record(p, "right", 120);   // late is after right OR the event
        Assert.IsTrue(tree.IsOpen(tree.Get("n_late"), p, SeriesData(), ChallengeData()));
    }

    [Test]
    public void AChallengeEventClearsWhenEveryChapterIsWon()
    {
        var tree = Tree();
        var ev = tree.Get("n_ev");
        var p = Profile();
        Record(p, "start", 90);
        Record(p, "left", 100);
        Assert.AreEqual(TreeNodeState.Open, tree.State(ev, p, SeriesData(), ChallengeData()));
        Win(p, "ev_one");
        Assert.AreEqual(TreeNodeState.Running, tree.State(ev, p, SeriesData(), ChallengeData()));
        Assert.AreEqual((1, 2), SeriesTree.ChapterProgress(ev, p, ChallengeData()));
        Win(p, "ev_two");
        Assert.AreEqual(TreeNodeState.Cleared, tree.State(ev, p, SeriesData(), ChallengeData()));
        Assert.IsTrue(tree.IsOpen(tree.Get("n_late"), p, SeriesData(), ChallengeData()), "the event opens what's after it");
    }

    [Test]
    public void ARunningChampionshipShowsAsRunning()
    {
        var tree = Tree();
        var series = SeriesData();
        var cars = Cars();
        var p = Profile(new OwnedCar("aaa_1", 0, 0));
        p.StartChampionship(series.Get("start"), cars.Get("aaa_1"), cars);
        Assert.AreEqual(TreeNodeState.Running, tree.State(tree.Get("n_start"), p, series, ChallengeData()));
    }

    [Test]
    public void ABranchsCarsetsGateEntry()
    {
        var series = SeriesData();
        var cars = Cars();
        var check = Restrictions.Check(series.Get("late").restriction, cars.Get("aaa_1"), new OwnedCar("aaa_1", 3, 0), 1);
        Assert.IsTrue(check.Has(RestrictionFailure.Carset));
        Assert.IsTrue(Restrictions.Check(series.Get("late").restriction, cars.Get("bbb_5"), new OwnedCar("bbb_5", 3, 0), 1).Eligible);
        CollectionAssert.DoesNotContain(Restrictions.Describe(series.Get("late").restriction, cars), "Level: 0+");
        CollectionAssert.Contains(Restrictions.Describe(series.Get("late").restriction, cars), "Cars: Bbb '01");
    }

    [Test]
    public void ABranchHandsOverItsStarterCarAtTheClassItNeeds()
    {
        var tree = Tree();
        var series = SeriesData();
        var challenges = ChallengeData();
        var cars = Cars();
        var late = tree.Get("n_late");
        var p = Profile(new OwnedCar("aaa_1", 3, 0));
        Assert.IsFalse(tree.CanCollectStarter(late, p, series, challenges, cars), "locked");

        Record(p, "right", 999);
        Assert.IsTrue(tree.CanCollectStarter(late, p, series, challenges, cars));
        var car = tree.CollectStarter(late, p, series, challenges, cars);
        Assert.AreEqual("bbb_4", car.id);
        Assert.AreEqual(2, car.carClass, "arrives at the series' minimum class");
        Assert.IsTrue(p.Owns("bbb_4"));
        Assert.IsFalse(tree.CanCollectStarter(late, p, series, challenges, cars), "once only: now owned, and it can enter");

        // Someone who already has a car that can enter isn't handed another.
        var q = Profile(new OwnedCar("bbb_5", 2, 0));
        Record(q, "right", 999);
        Assert.IsFalse(tree.CanCollectStarter(late, q, series, challenges, cars));
    }

    [Test]
    public void AFinishedChampionshipRecordsItsBestPoints()
    {
        var series = SeriesData();
        var cars = Cars();
        var p = Profile(new OwnedCar("aaa_1", 0, 0));
        var c = p.StartChampionship(series.Get("start"), cars.Get("aaa_1"), cars);
        c.round = c.Length;
        c.table.First(e => e.carId == "aaa_1").points = 77;
        var rec = SeriesRules.RecordChampionship(p.seriesRecords, c);
        Assert.AreEqual(77, rec.bestPoints);

        var again = p.StartChampionship(series.Get("start"), cars.Get("aaa_1"), cars);
        again.round = again.Length;
        again.table.First(e => e.carId == "aaa_1").points = 50;
        Assert.AreEqual(77, SeriesRules.RecordChampionship(p.seriesRecords, again).bestPoints, "best, not last");
    }

    [Test]
    public void TilesShowTheirSeriesMenuIconUnlessTheyNameOne()
    {
        var tree = Tree();
        var series = SeriesData();
        Assert.AreEqual("m", SeriesTree.IconFor(tree.Get("n_start"), series), "the series' DM2 menu");
        Assert.AreEqual(SeriesTree.ChallengeIcon, SeriesTree.IconFor(tree.Get("n_ev"), series));
        tree.Get("n_late").icon = "big_one";
        Assert.AreEqual("big_one", SeriesTree.IconFor(tree.Get("n_late"), series), "the node's own icon wins");

        Assert.AreEqual("SS", SeriesTree.IconPlaceholder("superspeedways"));
        Assert.AreEqual("BO", SeriesTree.IconPlaceholder("big_one"));
        Assert.AreEqual("CH", SeriesTree.IconPlaceholder(SeriesTree.ChallengeIcon));
    }

    [Test]
    public void ValidateCatchesBrokenNodes()
    {
        var tree = new SeriesTree(new SeriesTreeFile
        {
            nodes = new List<TreeNode>
            {
                Node("a", TreeNodeKind.Series, "start", 0, 0),
                Node("b", TreeNodeKind.Series, "nope", 1, 0, new[] { "a" }),
                Node("c", TreeNodeKind.Series, "left", 1, 0, new[] { "zz" }),
                Node("d", TreeNodeKind.Series, "right", 0, 1, new[] { "a" }, new[] { "qqq" }, "aaa_9"),
            },
        });
        var problems = tree.Validate(SeriesData(), ChallengeData(), Cars());
        Assert.IsTrue(problems.Any(x => x.Contains("no series nope")));
        Assert.IsTrue(problems.Any(x => x.Contains("shares cell 1,0")));
        Assert.IsTrue(problems.Any(x => x.Contains("after unknown node zz")));
        Assert.IsTrue(problems.Any(x => x.Contains("isn't left of it")));
        Assert.IsTrue(problems.Any(x => x.Contains("unknown carset qqq")));
        Assert.IsTrue(problems.Any(x => x.Contains("unknown starter car aaa_9")));

        // The data says what it means: no level gate and no weekly rotation on the tree.
        var series = SeriesData();
        series.Get("start").restriction.minLevel = 5;
        var challenges = ChallengeData();
        challenges.Get("ev").weeks = new List<int> { 4 };
        var gated = new SeriesTree(new SeriesTreeFile
        {
            nodes = new List<TreeNode> { Node("start", TreeNodeKind.Series, "start", 0, 0), Node("ev", TreeNodeKind.Challenge, "ev", 1, 0, new[] { "start" }) },
        }).Validate(series, challenges, Cars());
        Assert.IsTrue(gated.Any(x => x.Contains("has a level gate")));
        Assert.IsTrue(gated.Any(x => x.Contains("doesn't run every week")));
    }
}
