using System.Collections.Generic;
using System.Linq;
using Draftmaster.MasterDraft;
using NUnit.Framework;

// Alt paints in packs (Core PackGenerator.AltPaintOneIn): a car card now and then comes out wearing an alternate paint
// — the shiny card — and opening it adds that paint to the Garage's Cars tab. Engine-free, so it also runs under
// `dotnet test Tools/HeadlessTests`. The shine itself is drawn by MasterDraftCardView (PackOpeningScreenTests).
public class MasterDraftAltPaintTests
{
    static CardCatalog Catalog() => new CardCatalog(null, null);

    static PaintDef Paint(string id, int number, int alt) =>
        new PaintDef { id = id, carset = "cup26", number = number, alt = alt, livery = "cup26livery" + number + (alt > 0 ? "blankalt" + alt : "blank"), rarity = 2 };

    // Two alternates among three main schemes.
    static PaintCatalog Paints() => new PaintCatalog(new PaintsFile
    {
        paints = new List<PaintDef>
        {
            Paint("cup26_9", 9, 0), Paint("cup26_21", 21, 0), Paint("cup26_21_alt1", 21, 1),
            Paint("cup26_24", 24, 0), Paint("cup26_24_alt1", 24, 1),
        },
    });

    [Test]
    public void OnlyCarCardsComeOutShinyAndOnlyInAnAltPaint()
    {
        var catalog = Catalog();
        var paints = Paints();
        int cars = 0, shiny = 0;
        var seen = new HashSet<string>();
        for (long seed = 0; seed < 3000; seed++)
        {
            foreach (var c in PackGenerator.Open(PackKind.Standard, catalog, seed, paints))
            {
                if (c.type == CardType.Car) cars++;
                if (!c.Shiny)
                {
                    Assert.IsTrue(string.IsNullOrEmpty(c.paint), $"seed {seed}: {c.id} has a paint but isn't shiny");
                    continue;
                }
                shiny++;
                Assert.AreEqual(CardType.Car, c.type, $"seed {seed}: only cars wear paints");
                var p = paints.Get(c.paint);
                Assert.IsNotNull(p, $"seed {seed}: {c.paint} isn't in the catalogue");
                Assert.Greater(p.alt, 0, $"seed {seed}: {c.paint} is a main scheme, not an alt");
                Assert.AreEqual(p.livery, c.paintLivery);
                Assert.AreEqual(p.DisplayName, c.paintName);
                seen.Add(c.paint);
            }
        }

        // One car in AltPaintOneIn, give or take; both alts turn up.
        double rate = (double)shiny / cars;
        Assert.AreEqual(1.0 / PackGenerator.AltPaintOneIn, rate, 0.03, $"{shiny} shiny of {cars} cars");
        CollectionAssert.AreEquivalent(new[] { "cup26_21_alt1", "cup26_24_alt1" }, seen);
    }

    [Test]
    public void APacksCardsAreTheSameWithOrWithoutPaints()
    {
        var catalog = Catalog();
        var paints = Paints();
        for (long seed = 0; seed < 200; seed++)
        foreach (var kind in new[] { PackKind.Starter, PackKind.Standard })
        {
            var plain = PackGenerator.Open(kind, catalog, seed);
            var painted = PackGenerator.Open(kind, catalog, seed, paints);
            CollectionAssert.AreEqual(plain.Select(c => c.id).ToList(), painted.Select(c => c.id).ToList(), $"{kind} seed {seed}");
            Assert.IsFalse(plain.Any(c => c.Shiny), "no catalogue, no alt paints");

            var again = PackGenerator.Open(kind, catalog, seed, paints);
            CollectionAssert.AreEqual(painted.Select(c => c.paint ?? "").ToList(), again.Select(c => c.paint ?? "").ToList(),
                                      $"{kind} seed {seed}: the same seed paints the same cars");
        }
    }

    [Test]
    public void APaintedCardDoesNotPaintTheCatalogue()
    {
        var catalog = Catalog();
        var paints = Paints();
        for (long seed = 0; seed < 500; seed++) PackGenerator.Open(PackKind.Standard, catalog, seed, paints);
        Assert.IsTrue(catalog.All.All(c => string.IsNullOrEmpty(c.paint)));
    }

    [Test]
    public void OpeningAShinyAddsItsPaintToTheGarage()
    {
        var catalog = Catalog();
        var paints = Paints();
        var profile = MasterDraftProfile.New("alt-paint-player");
        profile.coins = 1000000;
        var pulled = new List<CardInstance>();
        pulled.AddRange(ProfileOps.ClaimStarter(profile, catalog, paints).cards);
        for (int i = 0; i < 80; i++)
        {
            var r = ProfileOps.BuyStandardPack(profile, catalog, paints);
            Assert.IsTrue(r.ok, r.error);
            pulled.AddRange(r.cards);
        }

        var shiny = pulled.Where(c => c.def.Shiny).Select(c => c.def.paint).Distinct().ToList();
        Assert.IsNotEmpty(shiny, "no alt paint in 80 packs");
        CollectionAssert.AreEquivalent(shiny, profile.paints, "each alt paint pulled is in the Cars tab, once");

        // The card keeps its paint through a save.
        var back = MasterDraftProfile.FromJson(profile.ToJson());
        var saved = back.cards.First(c => c.def.Shiny);
        var original = profile.cards.First(c => c.uid == saved.uid);
        Assert.AreEqual(original.def.paint, saved.def.paint);
        Assert.AreEqual(original.def.paintLivery, saved.def.paintLivery);
        Assert.AreEqual(original.def.paintName, saved.def.paintName);
        CollectionAssert.AreEquivalent(profile.paints, back.paints);
    }

    [Test]
    public void WithoutAPaintCatalogueNothingIsShinyOrAwarded()
    {
        var profile = MasterDraftProfile.New("no-paints");
        profile.coins = 1000000;
        ProfileOps.ClaimStarter(profile, Catalog());
        for (int i = 0; i < 30; i++) ProfileOps.BuyStandardPack(profile, Catalog());
        Assert.IsFalse(profile.cards.Any(c => c.def.Shiny));
        Assert.IsEmpty(profile.paints);
    }
}
