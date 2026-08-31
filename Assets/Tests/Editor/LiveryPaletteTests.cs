using Draftmaster.Sim;
using NUnit.Framework;
using UnityEngine;

// Picking a car's two colours off its paint. Every rule here exists because the obvious version of this
// gets it wrong on a real livery: the outline is the most common colour, a white car has no second colour,
// and a sponsor patch is not the team's trim.
public class LiveryPaletteTests
{
    static Color32[] Paint(params (Color32 colour, int count)[] runs)
    {
        int total = 0;
        foreach (var r in runs) total += r.count;

        var pixels = new Color32[total];
        int i = 0;
        foreach (var r in runs)
            for (int n = 0; n < r.count; n++) pixels[i++] = r.colour;
        return pixels;
    }

    static readonly Color32 Blue = new Color32(20, 60, 200, 255);
    static readonly Color32 Yellow = new Color32(240, 210, 40, 255);
    static readonly Color32 Outline = new Color32(12, 12, 14, 255);
    static readonly Color32 Clear = new Color32(0, 0, 0, 0);

    [Test]
    public void TheMostCommonPaintIsThePrimary()
    {
        var pair = LiveryPalette.Extract(Paint((Blue, 500), (Yellow, 120)));
        Assert.Less(LiveryPalette.Distance(pair.primary, Blue), 0.05f, "The blue that covers the car is the car's colour.");
    }

    // The black outline every sprite is drawn with, plus tyres and glass, is usually the biggest single
    // block of pixels on a livery. Counting it makes every car in the game black.
    [Test]
    public void TheOutlineIsNotTheCarsColour()
    {
        var pair = LiveryPalette.Extract(Paint((Outline, 900), (Blue, 300), (Yellow, 100)));
        Assert.Less(LiveryPalette.Distance(pair.primary, Blue), 0.05f, "Outline and tyres are not paint.");
    }

    [Test]
    public void TransparentPixelsAreNotPaint()
    {
        var pair = LiveryPalette.Extract(Paint((Clear, 2000), (Blue, 200)));
        Assert.Less(LiveryPalette.Distance(pair.primary, Blue), 0.05f);
    }

    [Test]
    public void TheSecondColourIsTheOtherColourOnTheCar()
    {
        var pair = LiveryPalette.Extract(Paint((Blue, 500), (Yellow, 200), (Outline, 300)));
        Assert.Less(LiveryPalette.Distance(pair.secondary, Yellow), 0.05f);
    }

    // A single-colour car still needs a trim, and "almost the same blue" is not one — two swatches nobody
    // can tell apart read as a bug in the stand, not as a paint scheme.
    [Test]
    public void AOneColourCarGetsAShadeRatherThanANearIdenticalTwin()
    {
        var almostTheSameBlue = new Color32(24, 66, 208, 255);
        var pair = LiveryPalette.Extract(Paint((Blue, 600), (almostTheSameBlue, 300)));

        Assert.GreaterOrEqual(LiveryPalette.Distance(pair.primary, pair.secondary), 0.15f,
                              "The two colours have to be visibly different from each other.");
    }

    [Test]
    public void ASponsorPatchIsNotTheTeamsSecondColour()
    {
        // 1% of the car, in a colour nothing else uses: a decal, not the trim.
        var pair = LiveryPalette.Extract(Paint((Blue, 990), (Yellow, 10)));
        Assert.Greater(LiveryPalette.Distance(pair.secondary, Yellow), 0.2f,
                       "A ten-pixel patch should not become the team's colour.");
    }

    [Test]
    public void ADarkCarGetsALighterTrimAndALightCarADarkerOne()
    {
        var dark = LiveryPalette.Shade(new Color(0.1f, 0.1f, 0.12f));
        var light = LiveryPalette.Shade(new Color(0.95f, 0.95f, 0.95f));

        Assert.Greater(dark.r, 0.4f, "A near-black car needs a light trim to show at all.");
        Assert.Less(light.r, 0.6f, "A white car needs a dark trim.");
    }

    [Test]
    public void AnEmptyOrFullyTransparentImageStillAnswers()
    {
        var pair = LiveryPalette.Extract(new Color32[0]);
        Assert.AreEqual(Color.white, pair.primary, "No paint at all reads as unpainted, not as an error.");
    }
}
