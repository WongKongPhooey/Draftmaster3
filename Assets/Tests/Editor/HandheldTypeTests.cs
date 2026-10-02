using Draftmaster.Controls;
using NUnit.Framework;

// Phone type sizing: a speech bubble is world-space text, and on a handheld it has to come out on screen at
// the same height as the kit's body line whatever the camera's zoom. HandheldType.WorldLineMetres is the
// conversion; these pin it.
public class HandheldTypeTests
{
    // The kit's data face at 3x on a 1080-line phone: 53 px of line.
    const float BodyLinePx = 53f;

    [Test]
    public void LineFillsTheSameShareOfTheScreenAsTheHudLine()
    {
        const float screenH = 1080f, ortho = 3.5f;
        float metres = HandheldType.WorldLineMetres(BodyLinePx, screenH, ortho);

        // What that many metres projects to through the camera, in screen pixels.
        float onScreen = metres / (2f * ortho) * screenH;
        Assert.That(onScreen, Is.EqualTo(BodyLinePx).Within(0.001f));
    }

    [Test]
    public void ZoomingInShrinksTheWorldSizeSoTheScreenSizeHolds()
    {
        // Indoors the on-foot camera pulls in from 3.5 to 2.5. A fixed world size grew by 40% on screen there;
        // the fitted size shrinks by the same ratio instead.
        float outdoors = HandheldType.WorldLineMetres(BodyLinePx, 1080f, 3.5f);
        float indoors = HandheldType.WorldLineMetres(BodyLinePx, 1080f, 2.5f);
        Assert.That(indoors / outdoors, Is.EqualTo(2.5f / 3.5f).Within(0.0001f));
    }

    [Test]
    public void SmallerThanTheOldDoubledPhoneSize()
    {
        // The old phone rule was 2x the desktop's 0.22 m, which on the paddock camera was ~40% taller than the
        // objective strip's detail line — the "a bit too big" dialogue.
        float metres = HandheldType.WorldLineMetres(BodyLinePx, 1080f, 3.5f);
        Assert.That(metres, Is.LessThan(0.44f));
        Assert.That(metres, Is.GreaterThan(0.22f));
    }

    [TestCase(0f, 1080f, 3.5f)]
    [TestCase(53f, 0f, 3.5f)]
    [TestCase(53f, 1080f, 0f)]
    public void NothingToMeasureAgainstReturnsZero(float linePx, float screenH, float ortho)
    {
        Assert.That(HandheldType.WorldLineMetres(linePx, screenH, ortho), Is.EqualTo(0f));
    }

    // Buttons on a phone: the label is the data face (17.6 UI px a line at 1x), so the plate grows to a line
    // of it plus the button's 4px padding top and bottom, on whole UI pixels.
    [Test]
    public void HandheldButtonFitsOneBodyLineWithPadding()
    {
        // 3x: a 53 px line plus 12 px each side is 77, rounded up to 78 — a whole UI pixel (26 at 1x).
        float h = HandheldType.ButtonHeight(true, 54f, 53f, 12f, 3);
        Assert.That(h, Is.EqualTo(78f));
        Assert.That(h % 3f, Is.EqualTo(0f));
    }

    [Test]
    public void DesktopButtonKeepsItsLaidOutHeight()
    {
        Assert.That(HandheldType.ButtonHeight(false, 54f, 53f, 12f, 3), Is.EqualTo(54f));
    }

    [Test]
    public void HandheldButtonNeverShrinksBelowTheDesktopHeight()
    {
        // A plate already taller than one line plus padding is left alone.
        Assert.That(HandheldType.ButtonHeight(true, 120f, 53f, 12f, 3), Is.EqualTo(120f));
    }
}
