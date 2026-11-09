using System.Collections.Generic;
using System.IO;
using Draftmaster.Tracks;
using NUnit.Framework;

// The satnav's roadside shops as rules (Draftmaster.Tracks.RoadsideShops): the layout checks, the per-visit
// pick, the junkyard's weekly wrecks, the fabricator tiers — and every shipped layout file passing the checks.
// Engine-free, so it also runs headless (Tools/HeadlessTests).
public class RoadsideShopsTests
{
    static ShopLayout Good() => new ShopLayout
    {
        width = 22f, height = 16f, doorX = -7f,
        counter = new ShopSpot { x = 6f, y = -4.5f }, clerk = new ShopSpot { x = 6f, y = -3.4f },
        bays = new[] { new ShopBay { x = -5f, y = 4f }, new ShopBay { x = 3f, y = 4f, state = "working" } },
        workers = new[] { new ShopSpot { x = 3f, y = 1.7f } },
    };

    [Test]
    public void AGoodRoomHasNoProblems()
    {
        CollectionAssert.IsEmpty(ShopLayouts.Problems(Good(), ShopKind.Fabricator));
    }

    [Test]
    public void OverlapsTheDoorTheFloorAndAnUnmannedBayAreCaught()
    {
        var l = Good();
        l.bays = new[] { new ShopBay { x = -5f, y = 4f }, new ShopBay { x = -3f, y = 4.5f }, new ShopBay { x = 3f, y = 4f, state = "working" } };
        Assert.That(ShopLayouts.Problems(l, ShopKind.Fabricator), Has.Some.Contains("overlaps"));

        l = Good();
        l.bays = new[] { new ShopBay { x = -7f, y = -6.5f } };
        Assert.That(ShopLayouts.Problems(l, ShopKind.Fabricator), Has.Some.Contains("blocks the door"));

        l = Good();
        l.bays = new[] { new ShopBay { x = 10f, y = 0f } };
        Assert.That(ShopLayouts.Problems(l, ShopKind.Fabricator), Has.Some.Contains("off the floor"));

        l = Good();
        l.workers = new ShopSpot[0];
        Assert.That(ShopLayouts.Problems(l, ShopKind.Fabricator), Has.Some.Contains("nobody at it"));

        l = Good();
        l.clerk = new ShopSpot { x = 0f, y = 0f };
        Assert.That(ShopLayouts.Problems(l, ShopKind.Fabricator), Has.Some.Contains("clerk"));
    }

    [Test]
    public void TheSameVisitGetsTheSameRoomAndEveryRoomComesRound()
    {
        Assert.AreEqual(ShopLayouts.Pick("pitt_bros", 5, 8), ShopLayouts.Pick("pitt_bros", 5, 8));
        var seen = new HashSet<int>();
        for (int week = 0; week < 60; week++) seen.Add(ShopLayouts.Pick("pitt_bros", week, 8));
        Assert.AreEqual(8, seen.Count);
        Assert.AreEqual(-1, ShopLayouts.Pick("x", 1, 0));
    }

    [Test]
    public void AYardsWrecksAreFixedForTheWeekAndPricedByDamage()
    {
        var a = AltSeriesCatalog.Wrecks("mojave_yard", 3);
        var b = AltSeriesCatalog.Wrecks("mojave_yard", 3);
        Assert.AreEqual(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++)
        {
            Assert.AreEqual(a[i].id, b[i].id);
            Assert.AreEqual(a[i].price, b[i].price);
        }
        Assert.That(a.Count, Is.InRange(2, 4));
        foreach (var w in a)
        {
            Assert.That(w.damagePercent, Is.InRange(25, 90));
            Assert.Less(w.price, w.series.rebuiltValue, "a wreck costs less than the car rebuilt");
            Assert.Greater(w.repairCost, 0);
        }
        // Over many weeks every ladder turns up.
        var series = new HashSet<string>();
        for (int week = 0; week < 40; week++) foreach (var w in AltSeriesCatalog.Wrecks("mojave_yard", week)) series.Add(w.series.id);
        Assert.AreEqual(AltSeriesCatalog.All.Length, series.Count);
    }

    [Test]
    public void FabricatorTiersCompareWithYourOwnFabricator()
    {
        Assert.Less(FabricatorTiers.QualityOf(1), FabricatorTiers.QualityOf(2));
        Assert.Less(FabricatorTiers.QualityOf(2), FabricatorTiers.QualityOf(3));
        Assert.AreEqual("+20 on your fabricator", FabricatorTiers.VersusTeam(2, 58));
        Assert.AreEqual("-6 on your fabricator", FabricatorTiers.VersusTeam(1, 68));
        Assert.AreEqual("Elite", FabricatorTiers.Name(9));
    }

    // Every shipped layout passes the same checks the generator and the editor window use.
    [Test]
    public void EveryShippedLayoutIsSound()
    {
        string dir = FindShops();
        Assert.IsNotNull(dir, "Assets/Resources/Travel/Shops not found");
        int files = 0;
        foreach (var path in Directory.GetFiles(dir, "*.json"))
        {
            var file = Parse(File.ReadAllText(path));
            Assert.IsTrue(System.Enum.TryParse(file.kind, out ShopKind kind), $"{path}: kind '{file.kind}'");
            Assert.Greater(file.variants.Length, 0, path);
            for (int k = 0; k < file.variants.Length; k++)
                CollectionAssert.IsEmpty(ShopLayouts.Problems(file.variants[k], kind), $"{Path.GetFileName(path)} variant {k}");
            files++;
        }
        Assert.GreaterOrEqual(files, 3, "Fabricator, EngineBuilder and Junkyard");
    }

    static ShopLayoutFile Parse(string json)
    {
#if UNITY_5_3_OR_NEWER
        return UnityEngine.JsonUtility.FromJson<ShopLayoutFile>(json);
#else
        return System.Text.Json.JsonSerializer.Deserialize<ShopLayoutFile>(json,
            new System.Text.Json.JsonSerializerOptions { IncludeFields = true });
#endif
    }

    static string FindShops()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), TestContext.CurrentContext.TestDirectory })
            for (var d = new DirectoryInfo(start); d != null; d = d.Parent)
            {
                var candidate = Path.Combine(d.FullName, "Assets", "Resources", "Travel", "Shops");
                if (Directory.Exists(candidate)) return candidate;
            }
        return null;
    }
}
