using System.Collections.Generic;
using Draftmaster.Sponsors;
using NUnit.Framework;
using UnityEngine;

// End-to-end check of the paint pipeline against the REAL assets: the cup26 livery, the generated decal
// art and the shipped panel layout. If a livery stops being Read/Write enabled, a decal goes missing, or a
// panel rect drifts off the bodywork, this fails rather than the car quietly turning up bare.
public class SponsorBakeTests
{
    const string Livery = "cup26livery8";
    const string Layout = "Sponsors/cup26Layout";
    const string Decal = "Sponsors/Car/voltage-energy";

    [Test]
    public void ShippedAssetsExistAndAreReadable()
    {
        var livery = Resources.Load<Sprite>(Livery);
        Assert.IsNotNull(livery, $"Resources/{Livery} is missing.");
        Assert.IsTrue(livery.texture.isReadable,
            "Liveries must be Read/Write enabled — run Draftmaster > Art > Retarget World Sprites to Pixel Standard.");

        var decal = Resources.Load<Texture2D>(Decal);
        Assert.IsNotNull(decal, $"Resources/{Decal} is missing — run Draftmaster > Sponsors > Generate Placeholder Decals.");
        Assert.IsTrue(decal.isReadable, "Decal art must be Read/Write enabled for the baker to blit it.");

        Assert.IsNotNull(Resources.Load<CarSponsorLayout>(Layout), $"Resources/{Layout} is missing.");
    }

    [Test]
    public void BakingPaintsTheDecalOntoItsPanelAndLeavesTheRestAlone()
    {
        var livery = Resources.Load<Sprite>(Livery);
        var layout = Resources.Load<CarSponsorLayout>(Layout);
        var decal = Resources.Load<Texture2D>(Decal);
        if (livery == null || layout == null || decal == null) Assert.Ignore("Sponsor assets not generated yet.");

        SponsorLiveryBaker.ClearCache();
        var painted = SponsorLiveryBaker.Bake(livery, layout,
            new List<SponsorLiveryBaker.Decal> { new() { slot = SponsorSlot.Hood, art = decal } });

        Assert.AreNotSame(livery, painted, "Baking must return a new sprite, not the bare livery.");
        Assert.AreEqual(livery.texture.width, painted.texture.width);
        Assert.AreEqual(livery.texture.height, painted.texture.height);
        Assert.AreEqual(livery.pixelsPerUnit, painted.pixelsPerUnit,
            "The paint has to stay on the 12.8 px/m standard or the car changes size.");

        var before = livery.texture.GetPixels32();
        var after = painted.texture.GetPixels32();
        int w = livery.texture.width;

        Vector2Int anchor = layout.Anchor(SponsorSlot.Hood, decal.width, decal.height);
        int changedInPanel = 0, changedOutside = 0;
        for (int y = 0; y < livery.texture.height; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                bool same = before[i].r == after[i].r && before[i].g == after[i].g &&
                            before[i].b == after[i].b && before[i].a == after[i].a;
                if (same) continue;

                bool inDecal = x >= anchor.x && x < anchor.x + decal.width &&
                               y >= anchor.y && y < anchor.y + decal.height;
                if (inDecal) changedInPanel++; else changedOutside++;
            }
        }

        Assert.Greater(changedInPanel, 20, "The hood decal should have repainted most of its footprint.");
        Assert.AreEqual(0, changedOutside, "Nothing outside the decal's footprint may change.");

        SponsorLiveryBaker.ClearCache();
    }

    [Test]
    public void NoDecalsMeansNoRepaint()
    {
        var livery = Resources.Load<Sprite>(Livery);
        var layout = Resources.Load<CarSponsorLayout>(Layout);
        if (livery == null || layout == null) Assert.Ignore("Sponsor assets not generated yet.");

        var painted = SponsorLiveryBaker.Bake(livery, layout, new List<SponsorLiveryBaker.Decal>());
        Assert.AreSame(livery, painted, "A car with nothing sold keeps its original paint — no texture copy.");
    }

    [Test]
    public void EveryPanelSitsInsideTheLivery()
    {
        var livery = Resources.Load<Sprite>(Livery);
        var layout = Resources.Load<CarSponsorLayout>(Layout);
        if (livery == null || layout == null) Assert.Ignore("Sponsor assets not generated yet.");

        foreach (var slot in SponsorSlots.All)
        {
            RectInt r = layout.RectFor(slot);
            Assert.GreaterOrEqual(r.x, 0, $"{slot} starts off the left edge.");
            Assert.GreaterOrEqual(r.y, 0, $"{slot} starts off the bottom edge.");
            Assert.LessOrEqual(r.x + r.width, livery.texture.width, $"{slot} runs off the nose/tail.");
            Assert.LessOrEqual(r.y + r.height, livery.texture.height, $"{slot} runs off the side.");
        }
    }

    // The brand list itself lives in Assembly-CSharp (DummySponsors), which a test assembly can't
    // reference, so this checks the seeded names' art directly through the shared key rule.
    static readonly string[] kSeededBrands =
    {
        "Voltage Energy", "Apateq Telecom", "MaxiMart", "TorqueParts", "Summit Bank", "Nexus Tech",
        "Roadhouse Grill", "Pioneer Oil", "Sureguard Insure", "Hydro Spring Water", "Ironclad Tools",
        "Skyline Airlines",
    };

    [Test]
    public void EveryBrandInTheSeedHasDecalArt()
    {
        foreach (string brand in kSeededBrands)
        {
            string path = SponsorKeys.CarArtPath(brand);
            Assert.IsNotNull(Resources.Load<Texture2D>(path),
                $"{brand} has no decal at Resources/{path} — its panel would stay blank. " +
                "Run Draftmaster > Sponsors > Generate Placeholder Decals.");
        }
    }

    [Test]
    public void LogoKeysAreFilenameSafe()
    {
        Assert.AreEqual("voltage-energy", SponsorKeys.LogoKey("Voltage Energy"));
        Assert.AreEqual("maximart", SponsorKeys.LogoKey("MaxiMart"));
        Assert.AreEqual("sureguard-insure", SponsorKeys.LogoKey("Sureguard  Insure!"));
        Assert.AreEqual("sponsor", SponsorKeys.LogoKey("   "));
    }
}
