using System.Collections.Generic;
using System.IO;
using System.Linq;
using Draftmaster.MasterDraft;
using NUnit.Framework;

// Master Draft's paints (Core PaintCatalog.cs, Assets/Resources/MasterDraft/Paints.json): the Garage's Cars tab. Checks
// the shipped file — every Cup '26 blank paint in Resources is in it, each livery exists, each rarity is the DM2
// star rating of its source car — and the collection rules. Engine-free, so it also runs under
// `dotnet test Tools/HeadlessTests`.
public class MasterDraftPaintTests
{
    static string projectRoot;

    static string ProjectRoot()
    {
        if (projectRoot != null) return projectRoot;
        foreach (var start in new[] { TestContext.CurrentContext.TestDirectory, Directory.GetCurrentDirectory() })
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "Assets", "Resources", "MasterDraft", "Paints.json")))
                    return projectRoot = dir.FullName;
        Assert.Fail("Assets/Resources/MasterDraft/Paints.json not found above " + TestContext.CurrentContext.TestDirectory);
        return null;
    }

    static string Resource(string path) => File.ReadAllText(Path.Combine(ProjectRoot(), "Assets", "Resources", path + ".json"));

    static PaintCatalog Load() => PaintCatalog.FromJson(Resource(PaintCatalog.ResourcePath));

    static PaintDef Paint(string id, int number, int rarity, int alt = 0, string sponsor = "") =>
        new PaintDef { id = id, carset = "cup26", number = number, alt = alt, livery = id + "livery", rarity = rarity, sponsor = sponsor };

    // ---- the shipped file --------------------------------------------------------------------------------

    [Test]
    public void EveryCup26BlankPaintIsInTheCatalogue()
    {
        var paints = Load();
        string resources = Path.Combine(ProjectRoot(), "Assets", "Resources");
        var blanks = Directory.GetFiles(resources, "cup26livery*.png")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n.Contains("blank"))
            .ToList();
        Assert.Greater(blanks.Count, 0);
        CollectionAssert.AreEquivalent(blanks, paints.Paints.Select(p => p.livery).ToList());
        foreach (var p in paints.Paints)
            Assert.IsTrue(File.Exists(Path.Combine(resources, p.livery + ".png")), $"{p.id}: no sprite {p.livery}");
        Assert.AreEqual(paints.Count, paints.Paints.Select(p => p.id).Distinct().Count());
    }

    [Test]
    public void RarityIsTheDm2StarRatingOfTheSourceCar()
    {
        var paints = Load();
        var cars = CarCatalog.FromJson(Resource(CarCatalog.ResourcePath));
        foreach (var p in paints.Paints)
        {
            Assert.That(p.rarity, Is.InRange(CarCatalog.MinRarity, CarCatalog.MaxRarity), p.id);
            if (string.IsNullOrEmpty(p.rarityFrom)) { Assert.AreEqual(1, p.rarity, $"{p.id}: no DM2 source reads Common"); continue; }
            var car = cars.Get(p.rarityFrom);
            Assert.IsNotNull(car, $"{p.id}: unknown source {p.rarityFrom}");
            Assert.AreEqual(p.number, car.number, p.id);
            Assert.AreEqual(car.rarity, p.rarity, $"{p.id} from {p.rarityFrom}");
        }
        // Spot checks: the #9 was a 3-star in DM2's Cup '24, the #8 a 4-star.
        Assert.AreEqual(3, paints.Get("cup26_9").rarity);
        Assert.AreEqual(4, paints.Get("cup26_8").rarity);
        Assert.AreEqual(paints.Get("cup26_21").rarity, paints.Get("cup26_21_alt1").rarity, "an alternate keeps its car's rarity");
    }

    // ---- the rules ----------------------------------------------------------------------------------------

    [Test]
    public void UnnamedPaintsSayWhatTheyAre()
    {
        Assert.AreEqual("No. 9 paint", Paint("a", 9, 1).DisplayName);
        Assert.AreEqual("No. 21 alt paint", Paint("b", 21, 1, alt: 1).DisplayName);
        Assert.AreEqual("Ridgeline Freight", Paint("c", 9, 1, sponsor: "Ridgeline Freight").DisplayName);
    }

    [Test]
    public void CollectedPaintsComeFirstRarestFirst()
    {
        var paints = new PaintCatalog(new PaintsFile
        {
            paints = new List<PaintDef> { Paint("p1", 1, 1), Paint("p2", 2, 4), Paint("p3", 3, 2), Paint("p4", 4, 4), Paint("p5", 5, 3) },
        });
        var owned = new List<string> { "p1", "p5" };
        CollectionAssert.AreEqual(new[] { "p5", "p1", "p2", "p4", "p3" },
            PaintCollection.List(paints, owned, 0, CollectionSort.Rarity).Select(p => p.id).ToArray());
        CollectionAssert.AreEqual(new[] { "p1", "p5", "p2", "p3", "p4" },
            PaintCollection.List(paints, owned, 0, CollectionSort.Number).Select(p => p.id).ToArray());
        CollectionAssert.AreEqual(new[] { "p2", "p4" },
            PaintCollection.List(paints, owned, 4, CollectionSort.Rarity).Select(p => p.id).ToArray());
        Assert.AreEqual(2, PaintCollection.Collected(paints, owned));

        Assert.IsTrue(PaintCollection.Award(owned, paints, "p2"));
        Assert.IsFalse(PaintCollection.Award(owned, paints, "p2"), "already held");
        Assert.IsFalse(PaintCollection.Award(owned, paints, "nope"), "not in the catalogue");
        Assert.AreEqual(3, PaintCollection.Collected(paints, owned));
    }

    [Test]
    public void AProfileKeepsItsPaintsOncePerPaint()
    {
        var p = MasterDraftProfile.New("paints");
        p.paints.AddRange(new[] { "cup26_9", "cup26_9", "", "cup26_8" });
        var back = MasterDraftProfile.FromJson(p.ToJson());
        CollectionAssert.AreEqual(new[] { "cup26_9", "cup26_8" }, back.paints);
    }
}
