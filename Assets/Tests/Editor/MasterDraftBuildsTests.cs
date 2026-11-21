using System.Linq;
using Draftmaster.MasterDraft;
using NUnit.Framework;

// The Garage's builds (Core BuildRules): five per profile, one per kind of track to start with, each a driver, car and
// crew chief card and a number. Engine-free — also run headless by Tools/HeadlessTests. The screen itself is
// GarageScreenTests.
public class MasterDraftBuildsTests
{
    static MasterDraftProfile NewProfile() => MasterDraftProfile.New("builds-test");

    static CardInstance Driver(MasterDraftProfile p, string name, int overall, int shortTracks = 0, int roadCourses = 0, int number = 0) =>
        p.Add(new CardDefinition
        {
            id = name, type = CardType.Driver, name = name, overall = overall, shortTracks = shortTracks,
            roadCourses = roadCourses, carNumber = number,
        });

    static CardInstance Card(MasterDraftProfile p, CardType type, string name, int overall) =>
        p.Add(new CardDefinition { id = name, type = type, name = name, overall = overall });

    [Test]
    public void ANewProfileHasFiveBuildsOnePerKindOfTrack()
    {
        var p = NewProfile();
        Assert.AreEqual(BuildRules.MaxBuilds, p.builds.Count);
        CollectionAssert.AreEqual(CardText.AllTrackKinds, p.builds.Select(b => b.track).ToArray());
        CollectionAssert.AreEqual(new[] { "Short Tracks", "Speedways", "Superspeedways", "Road Courses", "Dirt" },
                                  Enumerable.Range(0, 5).Select(i => BuildRules.Name(p.builds, i)).ToArray());
        Assert.IsTrue(p.builds.All(BuildRules.IsEmpty), "no cards yet, nothing to fill them with");
    }

    [Test]
    public void AutoFillPicksTheBestDriverForTheTrackAndTheBestCarAndChief()
    {
        var p = NewProfile();
        var shortGuy = Driver(p, "Short", 70, shortTracks: 19, roadCourses: 5, number: 7);
        var roadGuy = Driver(p, "Road", 72, shortTracks: 6, roadCourses: 18, number: 22);
        Card(p, CardType.Car, "Slow Car", 60);
        var car = Card(p, CardType.Car, "Fast Car", 85);
        var chief = Card(p, CardType.CrewChief, "Chief", 70);

        BuildRules.FillEmpty(p);
        var shortBuild = p.builds.First(b => b.track == TrackKind.ShortTrack);
        var roadBuild = p.builds.First(b => b.track == TrackKind.RoadCourse);
        Assert.AreEqual(shortGuy.uid, shortBuild.driver);
        Assert.AreEqual(roadGuy.uid, roadBuild.driver);
        Assert.AreEqual(car.uid, shortBuild.car);
        Assert.AreEqual(chief.uid, shortBuild.crewChief);
        Assert.AreEqual(7, BuildRules.Number(shortBuild, p), "the number follows the driver card");
        Assert.AreEqual(22, BuildRules.Number(roadBuild, p));

        var cards = BuildRules.Resolve(roadBuild, p);
        Assert.IsTrue(cards.Complete);
        Assert.AreEqual(TeamRules.Rating(roadGuy.def, car.def, chief.def), cards.Rating);
    }

    [Test]
    public void TheStarterPackFillsTheBuilds()
    {
        var p = NewProfile();
        Assert.IsTrue(ProfileOps.ClaimStarter(p, new CardCatalog(null, null)).ok);
        Assert.AreEqual(BuildRules.MaxBuilds, p.builds.Count);
        foreach (var b in p.builds) Assert.IsTrue(BuildRules.Resolve(b, p).Complete, b.track.ToString());
    }

    [Test]
    public void AssignTakesOnlyOwnedCardsOfTheSlotsType()
    {
        var p = NewProfile();
        var d = Driver(p, "D", 70);
        var car = Card(p, CardType.Car, "C", 70);
        var b = p.builds[0];

        Assert.IsTrue(BuildRules.Assign(b, p, CardType.Driver, d.uid));
        Assert.AreEqual(d.uid, b.driver);
        Assert.IsFalse(BuildRules.Assign(b, p, CardType.Driver, car.uid), "a car isn't a driver");
        Assert.IsFalse(BuildRules.Assign(b, p, CardType.Car, "c999"), "not in the collection");
        Assert.AreEqual(d.uid, b.driver);
        Assert.AreEqual("", b.car);

        // The same card can sit in more than one build.
        Assert.IsTrue(BuildRules.Assign(p.builds[1], p, CardType.Driver, d.uid));
    }

    [Test]
    public void ChoicesPutTheBestDriverForTheTrackFirst()
    {
        var p = NewProfile();
        var star = Driver(p, "Star", 95, shortTracks: 10, roadCourses: 12);
        var ringer = Driver(p, "Ringer", 70, shortTracks: 4, roadCourses: 20);
        var road = p.builds.First(b => b.track == TrackKind.RoadCourse);
        var sht = p.builds.First(b => b.track == TrackKind.ShortTrack);
        CollectionAssert.AreEqual(new[] { ringer.uid, star.uid }, BuildRules.Choices(road, p, CardType.Driver).Select(c => c.uid).ToArray());
        CollectionAssert.AreEqual(new[] { star.uid, ringer.uid }, BuildRules.Choices(sht, p, CardType.Driver).Select(c => c.uid).ToArray());
    }

    [Test]
    public void NumbersStepAndWrapAndCanFollowTheDriverAgain()
    {
        var p = NewProfile();
        Driver(p, "D", 70, number: 98);
        var b = p.builds[0];
        BuildRules.AutoFill(b, p);
        Assert.AreEqual(BuildRules.DriversNumber, b.number);
        Assert.AreEqual(98, BuildRules.Number(b, p));

        Assert.AreEqual(99, BuildRules.StepNumber(b, p, 1));
        Assert.AreEqual(0, BuildRules.StepNumber(b, p, 1), "wraps past 99");
        Assert.AreEqual(99, BuildRules.StepNumber(b, p, -1), "and back");

        b.number = BuildRules.DriversNumber;
        Assert.AreEqual(98, BuildRules.Number(b, p));

        var empty = p.builds[1];
        BuildRules.Clear(empty);
        Assert.AreEqual(-1, BuildRules.Number(empty, p), "no driver, no number");
        Assert.AreEqual(0, BuildRules.StepNumber(empty, p, 1));
        BuildRules.Clear(empty);
        Assert.AreEqual(99, BuildRules.StepNumber(empty, p, -1));
    }

    [Test]
    public void ACardLeavingTheCollectionLeavesEveryBuild()
    {
        var p = NewProfile();
        var d = Driver(p, "D", 70);
        BuildRules.Assign(p.builds[0], p, CardType.Driver, d.uid);
        BuildRules.Assign(p.builds[3], p, CardType.Driver, d.uid);
        Assert.IsTrue(p.Remove(d.uid));
        Assert.IsTrue(p.builds.All(b => b.driver == ""));
    }

    [Test]
    public void BuildsSurviveASaveAndAreSanitisedOnLoad()
    {
        var p = NewProfile();
        var d = Driver(p, "D", 70);
        var car = Card(p, CardType.Car, "C", 70);
        var b = p.builds[2];
        BuildRules.Assign(b, p, CardType.Driver, d.uid);
        b.number = 43;
        b.track = TrackKind.RoadCourse;
        p.builds[4].car = d.uid;          // a driver in a car slot
        p.builds[4].crewChief = "c999";   // a card that isn't owned
        p.builds[4].number = 140;         // out of range

        var back = MasterDraftProfile.FromJson(p.ToJson());
        Assert.AreEqual(BuildRules.MaxBuilds, back.builds.Count);
        Assert.AreEqual(d.uid, back.builds[2].driver);
        Assert.AreEqual(43, back.builds[2].number);
        Assert.AreEqual(TrackKind.RoadCourse, back.builds[2].track);
        Assert.AreEqual("Road Courses 2", BuildRules.Name(back.builds, 3), "two Road Courses builds are numbered");
        Assert.AreEqual("", back.builds[4].car);
        Assert.AreEqual("", back.builds[4].crewChief);
        Assert.AreEqual(BuildRules.DriversNumber, back.builds[4].number);
        Assert.AreNotEqual(car.uid, back.builds[4].car);
    }

    [Test]
    public void AnOlderSaveWithNoBuildsGetsFiveFilledFromItsCards()
    {
        var p = NewProfile();
        Driver(p, "D", 70);
        Card(p, CardType.Car, "C", 70);
        Card(p, CardType.CrewChief, "CC", 70);
        p.builds.Clear();   // as a save from before builds existed

        var back = MasterDraftProfile.FromJson(p.ToJson());
        Assert.AreEqual(BuildRules.MaxBuilds, back.builds.Count);
        Assert.IsTrue(back.builds.All(b => BuildRules.Resolve(b, back).Complete));
    }

    [Test]
    public void TrackCyclesRoundTheFiveKinds()
    {
        var kind = TrackKind.ShortTrack;
        for (int i = 0; i < CardText.AllTrackKinds.Length; i++) kind = BuildRules.NextTrack(kind);
        Assert.AreEqual(TrackKind.ShortTrack, kind);
        Assert.AreEqual(TrackKind.DirtCourse, BuildRules.NextTrack(TrackKind.RoadCourse));
    }
}
