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
    const string DemoLivery = "cup26livery89";
    const string DecalKey = "voltage-energy";

    static Texture2D LoadDecal(SponsorSlot slot, string key)
    {
        foreach (string path in SponsorKeys.DecalArtPaths(slot, key))
        {
            var tex = Resources.Load<Texture2D>(path);
            if (tex != null) return tex;
        }
        return null;
    }

    [Test]
    public void ShippedAssetsExistAndAreReadable()
    {
        var livery = Resources.Load<Sprite>(Livery);
        Assert.IsNotNull(livery, $"Resources/{Livery} is missing.");
        Assert.IsTrue(livery.texture.isReadable,
            "Liveries must be Read/Write enabled — run Draftmaster > Art > Retarget World Sprites to Pixel Standard.");

        var decal = LoadDecal(SponsorSlot.Hood, DecalKey);
        Assert.IsNotNull(decal, $"No hood art for {DecalKey} — run Draftmaster > Sponsors > Generate Placeholder Decals.");
        Assert.IsTrue(decal.isReadable, "Decal art must be Read/Write enabled for the baker to blit it.");

        Assert.IsNotNull(Resources.Load<CarSponsorLayout>(Layout), $"Resources/{Layout} is missing.");
    }

    [Test]
    public void BakingPaintsTheDecalOntoItsPanelAndLeavesTheRestAlone()
    {
        var livery = Resources.Load<Sprite>(Livery);
        var layout = Resources.Load<CarSponsorLayout>(Layout);
        var decal = LoadDecal(SponsorSlot.Hood, DecalKey);
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

    // Panels may hang off the body on purpose (the silhouette crops them), but each one has to be mostly
    // ON it, or the slot is pointing at nothing.
    [Test]
    public void EveryPanelLandsOnTheBodywork()
    {
        var livery = Resources.Load<Sprite>(DemoLivery);
        var layout = Resources.Load<CarSponsorLayout>(Layout);
        if (livery == null || layout == null) Assert.Ignore("Sponsor assets not generated yet.");

        var px = livery.texture.GetPixels32();
        int w = livery.texture.width, h = livery.texture.height;
        foreach (var sold in SponsorSlots.All)
        foreach (var slot in SponsorSlots.Panels(sold))
        {
            RectInt r = layout.RectFor(slot);
            int area = r.width * r.height, onBody = 0;
            for (int y = r.y; y < r.y + r.height; y++)
                for (int x = r.x; x < r.x + r.width; x++)
                    if (x >= 0 && x < w && y >= 0 && y < h && px[y * w + x].a > 0) onBody++;
            Assert.Greater(area, 0, $"{slot} has no size.");
            Assert.GreaterOrEqual(onBody * 2, area, $"{slot} is mostly off the car.");
        }
    }

    // The car's silhouette is the mask: a decal hanging off the body is cropped, never painted onto the
    // transparent surround, and the body's shape (its alpha) never changes.
    [Test]
    public void DecalsAreCroppedByTheCarSilhouette()
    {
        var livery = Resources.Load<Sprite>(DemoLivery);
        if (livery == null) Assert.Ignore($"Resources/{DemoLivery} is missing.");

        var layout = ScriptableObject.CreateInstance<CarSponsorLayout>();
        layout.quarterRight = new RectInt(52, 0, 16, 4);      // runs off the rear corner and the texture edge
        layout.quarterLeft = new RectInt(52, 28, 16, 4);

        var art = new Texture2D(16, 4, TextureFormat.RGBA32, false) { name = "silhouette-test" };
        var solid = new Color32[16 * 4];
        for (int i = 0; i < solid.Length; i++) solid[i] = new Color32(255, 0, 255, 255);
        art.SetPixels32(solid);
        art.Apply();

        SponsorLiveryBaker.ClearCache();
        var painted = SponsorLiveryBaker.Bake(livery, layout, new List<SponsorLiveryBaker.Decal>
        {
            new() { slot = SponsorSlot.QuarterLeft, art = art },
            new() { slot = SponsorSlot.QuarterRight, art = art },
        });

        var before = livery.texture.GetPixels32();
        var after = painted.texture.GetPixels32();
        int painted255 = 0;
        for (int i = 0; i < before.Length; i++)
        {
            Assert.AreEqual(before[i].a, after[i].a, "Baking must never change the car's shape.");
            if (before[i].a == 0) Assert.AreEqual(before[i], after[i], "Nothing may be painted off the bodywork.");
            else if (after[i].r == 255 && after[i].g == 0 && after[i].b == 255) painted255++;
        }
        Assert.Greater(painted255, 0, "The on-body part of the decal should still be painted.");

        SponsorLiveryBaker.ClearCache();
        Object.DestroyImmediate(art);
        Object.DestroyImmediate(layout);
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
            foreach (var slot in new[] { SponsorSlot.Hood, SponsorSlot.Tail })
                Assert.IsNotNull(LoadDecal(slot, SponsorKeys.LogoKey(brand)),
                    $"{brand} has no {slot} decal — its panel would stay blank. " +
                    "Run Draftmaster > Sponsors > Generate Placeholder Decals.");
        }
    }

    // The Paint Scheme Designer repaints the #89 from layers; if any of them stops importing Read/Write
    // (SponsorDecalImport covers Resources/PaintShop) the booth silently keeps the drawn paint.
    [Test]
    public void DemoCarPaintLayersAreReadable()
    {
        foreach (string layer in new[] { "PaintShop/cup26livery89_mask", "PaintShop/cup26livery89_detail" })
        {
            var tex = Resources.Load<Texture2D>(layer);
            Assert.IsNotNull(tex, $"Resources/{layer} is missing.");
            Assert.IsTrue(tex.isReadable, $"{layer} must be Read/Write enabled.");
            Assert.AreEqual(new Vector2Int(64, 32), new Vector2Int(tex.width, tex.height));
        }
        var patterns = Resources.LoadAll<Texture2D>("PaintShop/Patterns");
        Assert.Greater(patterns.Length, 0, "No livery patterns in Resources/PaintShop/Patterns.");
        foreach (var p in patterns)
        {
            Assert.IsTrue(p.isReadable, $"Pattern {p.name} must be Read/Write enabled.");
            Assert.AreEqual(new Vector2Int(64, 32), new Vector2Int(p.width, p.height), $"Pattern {p.name} must match the livery size.");
        }
    }

    [Test]
    public void DemoStarterSponsorHasHoodArt()
    {
        Assert.IsNotNull(LoadDecal(SponsorSlot.Hood, SponsorKeys.LogoKey("Aces & Spades")),
            "The #89's starting hood sponsor needs Resources/Sponsors/Hood/aces-and-spades.png.");
    }

    [Test]
    public void LogoKeysAreFilenameSafe()
    {
        Assert.AreEqual("voltage-energy", SponsorKeys.LogoKey("Voltage Energy"));
        Assert.AreEqual("maximart", SponsorKeys.LogoKey("MaxiMart"));
        Assert.AreEqual("sureguard-insure", SponsorKeys.LogoKey("Sureguard  Insure!"));
        Assert.AreEqual("sponsor", SponsorKeys.LogoKey("   "));
        Assert.AreEqual("aces-and-spades", SponsorKeys.LogoKey("Aces & Spades"), "Matches Sponsors/Hood/aces-and-spades.png.");
    }
}
