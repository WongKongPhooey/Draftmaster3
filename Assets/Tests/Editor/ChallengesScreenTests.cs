using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Draftmaster.MasterDraft;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// The Master Draft Challenges screen (MasterDraftUI, rework row 8) built in the editor against a test profile and
// test catalogues: no session, no backend, no scene — MasterDraftUI.BuildOfflineChallenges. Checks this week's
// events, a chapter's eligibility and every reason it can't start, and the prize reveal after a win. The launch
// plan and the scoring themselves are MasterDraftChallengeRaceTests, engine-free.
//
// Not named MasterDraft*Tests: those compile into Tools/HeadlessTests, and this needs Unity. MasterDraftUI lives
// in Assembly-CSharp, which an asmdef can't reference, so it is reached by reflection (as SeriesScreenTests is).
public class ChallengesScreenTests
{
    static readonly Type UiType = Type.GetType("MasterDraftUI, Assembly-CSharp", throwOnError: true);
    static readonly Type OutcomeType = Type.GetType("MasterDraftSession+ChallengeOutcome, Assembly-CSharp", throwOnError: true);

    GameObject _go;
    Component _ui;

    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("ChallengesScreenTest");
        _ui = _go.AddComponent(UiType);
    }

    [TearDown]
    public void TearDown()
    {
        if (_go != null) Object.DestroyImmediate(_go);
    }

    object Call(string method, params object[] args) =>
        UiType.GetMethod(method, BindingFlags.Public | BindingFlags.Instance).Invoke(_ui, args);

    T Get<T>(string property) =>
        (T)UiType.GetProperty(property, BindingFlags.Public | BindingFlags.Instance).GetValue(_ui);

    Transform Content => _go.transform.Find("Canvas/Content");

    // Anywhere under the content (the result window nests its buttons).
    Button ContentButton(string key) =>
        Content.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "Button_" + key || b.transform.parent?.name == "Button_" + key)
        ?? Content.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Button_" + key)?.GetComponentInChildren<Button>();

    static CarCatalog TestCatalogue()
    {
        var file = new CarCatalogFile { version = 1 };
        file.carsets.Add(new CarsetDef { id = "tst01", name = "Test '01", cars = 6 });
        for (int i = 1; i <= 6; i++)
            file.cars.Add(new CarDef
            {
                id = CarCatalog.IdFor("tst01", i), carset = "tst01", number = i, driver = "Driver" + i,
                team = i <= 3 ? "AAA" : "BBB", manufacturer = "CHV", rarity = 1, type = DriverTypes.Rookie,
                livery = "nolivery_test_" + i,
            });
        return new CarCatalog(file);
    }

    // "seven": Progression, weeks 2 and 6, two AAA chapters (class 1+, then class 2+), prize tst01_6.
    // "always": Replay, every week, one chapter open to anyone at level 1.
    static ChallengeCatalog TestChallenges()
    {
        ChallengeChapter Chapter(string id, string ev, Restriction r) => new ChallengeChapter
        {
            id = id, eventId = ev, name = id, track = "Daytona", aiLevel = 3, baseLaps = 4, restriction = r,
            description = "A test chapter.",
            prize = new ChallengePrize { kind = ChallengePrizeKind.Car, code = "Six", cars = new List<string> { "tst01_6" }, gears = 15 },
        };
        var file = new ChallengesFile { version = 1, cycleWeeks = 13 };
        file.events.Add(new ChallengeEvent
        {
            id = "seven", name = "Seven", type = ChallengeEventType.Progression, weeks = new List<int> { 2, 6 },
            chapters = new List<ChallengeChapter>
            {
                Chapter("seven_one", "seven", new Restriction(1, 1, RestrictionRule.Team, "AAA")),
                Chapter("seven_two", "seven", new Restriction(1, 2, RestrictionRule.Team, "AAA")),
            },
        });
        file.events.Add(new ChallengeEvent
        {
            id = "always", name = "Always", type = ChallengeEventType.Replay, weeks = Enumerable.Range(1, 13).ToList(),
            chapters = new List<ChallengeChapter> { Chapter("always_one", "always", new Restriction(1, 0)) },
        });
        return new ChallengeCatalog(file);
    }

    static MasterDraftProfile TestProfile(int level, params OwnedCar[] cars) => new MasterDraftProfile
    {
        schemaVersion = MasterDraftProfile.CurrentSchema,
        playerId = "challenge-test",
        level = level,
        garage = cars.ToList(),
        selectedCar = cars.Length > 0 ? cars[0].id : "",
    };

    [Test]
    public void Challenges_ThisWeeksEventOpensOnItsNextChapterAndCanStart()
    {
        // tst01_4 is BBB (can't enter "seven"), tst01_2 is AAA class 1.
        var profile = TestProfile(1, new OwnedCar("tst01_4", 3, 0), new OwnedCar("tst01_2", 1, 0));
        Call("BuildOfflineChallenges", profile, TestCatalogue(), TestChallenges(), 2, null);

        Assert.AreEqual(2, Get<int>("ChallengeShownWeek"));
        Assert.AreEqual(2, Get<int>("ChallengeLiveCount"));
        Assert.AreEqual("seven", Get<string>("ChallengePickedEvent"), "this week's events come first");
        Assert.AreEqual("seven_one", Get<string>("ChallengePickedChapter"));
        Assert.AreEqual("tst01_2", Get<string>("ChallengeEntryCar"), "the selected car can't enter: the first one that can");
        Assert.IsTrue(Get<bool>("ChallengeCanStart"), Get<string>("ChallengeReasons"));
        Assert.AreEqual("", Get<string>("ChallengeReasons"));
        Assert.IsTrue(ContentButton("ch.go").interactable);
        Assert.IsNotNull(ContentButton("ch.car"));

        var plan = (ChallengeRacePlan)UiType.GetMethod("PlanPickedChallenge").Invoke(_ui, new object[] { null });
        Assert.IsNotNull(plan);
        Assert.AreEqual("tst01_2", plan.carId);
        Assert.AreEqual("Daytona", plan.trackId);
    }

    [Test]
    public void Challenges_SaysEveryReasonAChapterCantStart()
    {
        var profile = TestProfile(1, new OwnedCar("tst01_2", 1, 0));

        // Week 3: "seven" isn't running (back in week 6).
        Call("BuildOfflineChallenges", profile, TestCatalogue(), TestChallenges(), 3, "seven_one");
        Assert.AreEqual(1, Get<int>("ChallengeLiveCount"));
        Assert.IsFalse(Get<bool>("ChallengeCanStart"));
        StringAssert.Contains("Not running this week. Back in 3 weeks", Get<string>("ChallengeReasons"));
        Assert.IsFalse(ContentButton("ch.go").interactable);

        // Week 2, chapter two: locked behind chapter one, and the car's class is too low.
        Call("BuildOfflineChallenges", profile, TestCatalogue(), TestChallenges(), 2, "seven_two");
        Assert.AreEqual("seven_two", Get<string>("ChallengePickedChapter"));
        Assert.IsFalse(Get<bool>("ChallengeCanStart"));
        string reasons = Get<string>("ChallengeReasons");
        StringAssert.Contains("Win the previous chapter first", reasons);
        StringAssert.Contains("No car in your garage can enter", Content.GetComponentsInChildren<TMPro.TMP_Text>(true)
            .Select(t => t.text).FirstOrDefault(t => t.StartsWith("No car")) ?? "");
        Assert.IsNull(UiType.GetMethod("PlanPickedChallenge").Invoke(_ui, new object[] { null }));
    }

    [Test]
    public void Challenges_AWinRevealsThePrizeCar()
    {
        var cars = TestCatalogue();
        var data = TestChallenges();
        var profile = TestProfile(1, new OwnedCar("tst01_2", 1, 0));
        var plan = ChallengeRace.Plan(profile, data, "seven_one", "tst01_2", cars, 2, out string error);
        Assert.IsNotNull(plan, error);
        var rewards = ChallengeRace.RecordResult(profile, data, plan, 1, cars);
        Assert.AreEqual(1, rewards.prizes.Count);

        var outcome = Activator.CreateInstance(OutcomeType);
        OutcomeType.GetField("plan").SetValue(outcome, plan);
        OutcomeType.GetField("rewards").SetValue(outcome, rewards);
        Call("BuildOfflineChallengeResult", profile, cars, data, outcome);

        var reveal = ContentButton("res.reveal");
        Assert.IsNotNull(reveal, "the prize waits behind a Reveal button");
        reveal.onClick.Invoke();

        StringAssert.StartsWith("New car!", Get<string>("ChallengePrizeText"));
        StringAssert.Contains("#6 Driver6", Get<string>("ChallengePrizeText"));
        StringAssert.Contains("15 gears", Get<string>("ChallengePrizeText"));
        Assert.AreEqual("nolivery_test_6", Get<string>("ChallengePrizeSprite"));
        Assert.IsTrue(profile.Owns("tst01_6"));

        // Continue goes back to the screen, the next chapter now open.
        ContentButton("res.ok").onClick.Invoke();
        Assert.AreEqual("seven_one", Get<string>("ChallengePickedChapter"));
        Call("PickChallenge", "seven_two");
        StringAssert.DoesNotContain("previous chapter", Get<string>("ChallengeReasons"));
    }

    [Test]
    public void Challenges_TheShippedDataBuildsEveryWeek()
    {
        MasterDraftJsonInstallerShim();
        var cars = CarCatalog.FromJson(Resources.Load<TextAsset>(CarCatalog.ResourcePath).text);
        var data = ChallengeCatalog.FromJson(Resources.Load<TextAsset>(ChallengeCatalog.ResourcePath).text);
        Assert.IsNotNull(cars);
        Assert.IsNotNull(data);
        var profile = MasterDraftProfile.New("challenge-real", "Ace", cars);

        for (int week = 1; week <= data.CycleWeeks; week++)
        {
            Call("BuildOfflineChallenges", profile, cars, data, week, null);
            Assert.AreEqual(Challenges.LiveEvents(data, week).Count, Get<int>("ChallengeLiveCount"), $"week {week}");
            Assert.IsNotNull(ContentButton("ch.go"), $"week {week}");
        }

        // Every chapter's panel draws (one week is enough: the week only changes the reasons).
        foreach (var ev in data.Events)
            foreach (var c in ev.chapters)
            {
                Call("PickChallenge", c.id);
                Assert.AreEqual(c.id, Get<string>("ChallengePickedChapter"));
                Assert.IsNotNull(ContentButton("ch.go"), c.id);
            }
    }

    // JsonUtility as Core's reader, as the game installs it (MasterDraftJsonInstaller lives in Assembly-CSharp).
    static void MasterDraftJsonInstallerShim() =>
        Type.GetType("MasterDraftJsonInstaller, Assembly-CSharp", throwOnError: true)
            .GetMethod("Install", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
}
