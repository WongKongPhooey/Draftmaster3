using Draftmaster.Controls;
using NUnit.Framework;

// Portrait driving on a phone: the swing camera turns the screen upright while the player is in the car, the
// kit's UI scale follows the short side so panels stay the same size in the hand, and the race camera is
// opened out so the car keeps its size. DriveOrientation holds those rules; these pin them.
public class DriveOrientationTests
{
    // --- when the screen turns ---------------------------------------------------------------------------

    [Test]
    public void PortraitOnlyForAHandheldInTheCarWithTheSwingCamera()
    {
        Assert.IsTrue(DriveOrientation.WantsPortrait(handheld: true, swingCamera: true, inCar: true, resultsUp: false));

        Assert.IsFalse(DriveOrientation.WantsPortrait(false, true, true, false), "a desktop is never turned");
        Assert.IsFalse(DriveOrientation.WantsPortrait(true, false, true, false), "the fixed camera stays landscape");
        Assert.IsFalse(DriveOrientation.WantsPortrait(true, true, false, false), "on foot stays landscape");
        Assert.IsFalse(DriveOrientation.WantsPortrait(true, true, true, true), "the results table is landscape");
    }

    // --- the latch -----------------------------------------------------------------------------------------

    [Test]
    public void TurnsUprightAtOnce()
    {
        var latch = new PortraitLatch();
        Assert.IsTrue(latch.Update(true, 0.016f));
    }

    [Test]
    public void AOneFrameBlinkDoesNotSpinTheScreen()
    {
        // The player's car is swapped (reload, team switch, broadcast hand-over): the in-car test drops out
        // for a frame or two and comes straight back.
        var latch = new PortraitLatch();
        latch.Update(true, 0.016f);
        Assert.IsTrue(latch.Update(false, 0.016f));
        Assert.IsTrue(latch.Update(false, 0.016f));
        Assert.IsTrue(latch.Update(true, 0.016f));

        // And the blink before does not count towards the next one.
        for (int i = 0; i < 40; i++) Assert.IsTrue(latch.Update(false, 0.016f), $"frame {i}");
    }

    [Test]
    public void TurnsBackOnceTheReasonHasStayedGone()
    {
        var latch = new PortraitLatch(0.5f);
        latch.Update(true, 0.016f);
        Assert.IsTrue(latch.Update(false, 0.3f));
        Assert.IsFalse(latch.Update(false, 0.3f));
        Assert.IsFalse(latch.Portrait);
    }

    [Test]
    public void APausedFrameStillCountsTowardsLettingGo()
    {
        // Real time is passed in, so this is only about a zero or junk dt not wedging the latch upright.
        var latch = new PortraitLatch(0.5f);
        latch.Update(true, 0.016f);
        latch.Update(false, 0f);
        latch.Update(false, -1f);
        Assert.IsTrue(latch.Portrait);
        Assert.IsFalse(latch.Update(false, 0.6f));
    }

    [Test]
    public void ResetLetsGoAtOnce()
    {
        var latch = new PortraitLatch();
        latch.Update(true, 0.016f);
        latch.Reset();
        Assert.IsFalse(latch.Portrait);
    }

    // --- UI scale -----------------------------------------------------------------------------------------

    [Test]
    public void LandscapeScaleIsUnchanged()
    {
        // What PixelGUI always did: height over the 360-line design grid, rounded down.
        Assert.AreEqual(3, DriveOrientation.KitScale(1920f, 1080f));
        Assert.AreEqual(2, DriveOrientation.KitScale(1280f, 720f));
        Assert.AreEqual(6, DriveOrientation.KitScale(3840f, 2160f));
        Assert.AreEqual(1, DriveOrientation.KitScale(640f, 300f));
    }

    [Test]
    public void TheSamePhoneUprightKeepsTheSameScale()
    {
        // A 2400x1080 phone turned upright: the height would have made it 6x, twice the panels, on a screen
        // only 180 design pixels wide.
        Assert.AreEqual(DriveOrientation.KitScale(2400f, 1080f), DriveOrientation.KitScale(1080f, 2400f));
        Assert.AreEqual(3, DriveOrientation.KitScale(1080f, 2400f));
    }

    [Test]
    public void UprightPhoneStillHasTheWholeDesignWidth()
    {
        // Short side over scale is at least the 360-pixel design height, so a panel authored to fit the
        // landscape grid's height (the pause menu, the running order) fits across an upright phone.
        foreach (var w in new[] { 720f, 1080f, 1170f, 1440f })
        {
            int s = DriveOrientation.KitScale(w, w * 2.2f);
            Assert.GreaterOrEqual(w / s, 360f, $"{w} wide");
        }
    }

    // --- camera zoom ----------------------------------------------------------------------------------------

    [Test]
    public void LandscapeCameraIsLeftAlone()
    {
        Assert.AreEqual(1f, DriveOrientation.PortraitZoom(2400f, 1080f));
        Assert.AreEqual(1f, DriveOrientation.PortraitZoom(1000f, 1000f));
        Assert.AreEqual(1f, DriveOrientation.PortraitZoom(0f, 1000f));
    }

    [Test]
    public void UprightCameraKeepsPixelsPerMetre()
    {
        const float ortho = 12f;
        // Landscape: the short side (1080) spans 2 x ortho metres.
        float landscapePxPerM = 1080f / (2f * ortho);

        float upright = ortho * DriveOrientation.PortraitZoom(1080f, 2400f);
        float uprightPxPerM = 2400f / (2f * upright);

        Assert.That(uprightPxPerM, Is.EqualTo(landscapePxPerM).Within(0.0001f));
    }
}
