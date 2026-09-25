using System.Collections.Generic;
using Draftmaster.Controls;
using NUnit.Framework;

// The on-screen walking controls for a phone: where the floating stick lives, what a thumb pushing it asks
// for, and which stray finger counts as a tap. TouchWalkControls only reads the fingers and draws; the rules
// are TouchWalkLayout / TouchWalkState, and they are all here.
//
// Coordinates are IMGUI's: top-left origin, y growing downwards. Movement comes back the other way up,
// because the world does.
public class TouchWalkTests
{
    static readonly (float w, float h)[] Screens =
    {
        (800f, 360f), (854f, 480f), (1280f, 720f), (1920f, 1080f), (2400f, 1080f), (2960f, 1440f), (2048f, 1536f),
    };

    static TouchWalkLayout Layout(float w = 1280f, float h = 720f) =>
        new TouchWalkLayout(new TouchRect(0f, 0f, w, h), TouchLayout.UnitFor(w, h));

    static List<TouchPoint> Fingers(params TouchPoint[] f) => new List<TouchPoint>(f);
    static List<TouchPoint> None() => new List<TouchPoint>();

    // ------------------------------------------------------------------ layout

    [Test]
    public void OnEveryScreen_TheStickZoneAndItsRestingRingAreOnScreen()
    {
        foreach (var (w, h) in Screens)
        {
            var l = Layout(w, h);
            var screen = new TouchRect(0f, 0f, w, h);

            Assert.IsTrue(l.stickZone.xMax <= screen.xMax && l.stickZone.yMax <= screen.yMax,
                          $"{w}x{h}: the stick zone runs off the screen.");
            Assert.IsTrue(l.stickRest.x >= 0f && l.stickRest.y >= 0f &&
                          l.stickRest.xMax <= w && l.stickRest.yMax <= h,
                          $"{w}x{h}: the resting ring is drawn off the screen.");
            Assert.IsTrue(l.stickZone.Contains(l.stickRest.centerX, l.stickRest.centerY),
                          $"{w}x{h}: a thumb put on the ring the game drew would not take the stick.");
            Assert.Greater(l.travel, 0f, $"{w}x{h}: no stick travel, so a push reads as nothing.");
        }
    }

    [Test]
    public void TheStickLeavesTheRightHalfAndTheTopStripAlone()
    {
        var l = Layout(1280f, 720f);
        Assert.IsFalse(l.stickZone.Contains(1000f, 600f), "the right half is not the stick");
        Assert.IsFalse(l.stickZone.Contains(100f, 20f), "the top strip carries the banner and the clock");
        Assert.IsTrue(l.stickZone.Contains(100f, 600f), "the bottom left is the stick");
    }

    [Test]
    public void OnEveryScreen_ThePhoneButtonIsInTheCorner_AndClearOfTheRestingRing()
    {
        foreach (var (w, h) in Screens)
        {
            var l = Layout(w, h);
            var b = l.phoneButton;
            Assert.IsTrue(b.x >= 0f && b.y >= 0f && b.xMax <= w && b.yMax <= h,
                          $"{w}x{h}: the phone button is drawn off the screen.");
            Assert.Less(b.centerX, w * 0.25f, $"{w}x{h}: the phone button is not bottom-left.");
            Assert.Greater(b.centerY, h * 0.75f, $"{w}x{h}: the phone button is not bottom-left.");
            Assert.LessOrEqual(b.xMax, l.stickRest.x,
                               $"{w}x{h}: the resting ring is drawn over the phone button.");
        }
    }

    [Test]
    public void AFingerOnThePhoneButton_NeitherWalksNorTaps()
    {
        var l = Layout();
        var s = new TouchWalkState();
        float x = l.phoneButton.centerX, y = l.phoneButton.centerY;

        s.Update(Fingers(new TouchPoint(1, x, y)), l, 0f);
        Assert.IsFalse(s.Walking, "pressing the phone button took the stick");

        s.Update(None(), l, 0.1f);
        Assert.IsFalse(s.Tapped, "pressing the phone button also tapped whoever was stood behind it");

        // ...and it doesn't hold the stick hostage: the next thumb in the zone still walks.
        s.Update(Fingers(new TouchPoint(2, 300f, 600f)), l, 0.2f);
        Assert.IsTrue(s.Walking, "the stick stopped working after the phone button was used");
    }

    // ------------------------------------------------------------------ walking

    [Test]
    public void ThumbDown_TakesTheStickWhereItLands_AndPushingWalks()
    {
        var l = Layout();
        var s = new TouchWalkState();

        s.Update(Fingers(new TouchPoint(1, 200f, 600f)), l, 0f);
        Assert.IsTrue(s.Walking, "a thumb in the stick zone did not take the stick");
        Assert.AreEqual(200f, s.CentreX, 0.01f, "the stick centred somewhere other than where the thumb landed");
        Assert.AreEqual(600f, s.CentreY, 0.01f);
        Assert.AreEqual(0f, s.MoveX, 0.001f, "landing on the glass walked somewhere on its own");
        Assert.AreEqual(0f, s.MoveY, 0.001f);

        // Push right: full travel is a full push, and nothing vertical.
        s.Update(Fingers(new TouchPoint(1, 200f + l.travel, 600f)), l, 0.1f);
        Assert.AreEqual(1f, s.MoveX, 0.001f);
        Assert.AreEqual(0f, s.MoveY, 0.001f);

        // Push up the screen: forwards, not backwards.
        s.Update(Fingers(new TouchPoint(1, 200f, 600f - l.travel)), l, 0.2f);
        Assert.Greater(s.MoveY, 0.99f, "pushing up the screen has to walk away from the camera, not towards it");
        Assert.AreEqual(0f, s.MoveX, 0.001f);
    }

    [Test]
    public void ARestingThumbIsNotAPush()
    {
        var l = Layout();
        var s = new TouchWalkState();
        s.Update(Fingers(new TouchPoint(1, 200f, 600f)), l, 0f);

        float small = l.travel * (TouchWalkLayout.Deadzone * 0.5f);
        s.Update(Fingers(new TouchPoint(1, 200f + small, 600f)), l, 0.1f);
        Assert.AreEqual(0f, s.MoveX, 0.001f, "a thumb resting on the stick walked");
    }

    [Test]
    public void PushingPastTheEdge_DragsTheCentre_SoPullingBackTurnsAroundAtOnce()
    {
        var l = Layout();
        var s = new TouchWalkState();
        s.Update(Fingers(new TouchPoint(1, 200f, 600f)), l, 0f);

        // Way past full travel to the right.
        s.Update(Fingers(new TouchPoint(1, 200f + l.travel * 4f, 600f)), l, 0.1f);
        Assert.AreEqual(1f, s.MoveX, 0.001f);

        // Now pull back. The centre was dragged out to 3x travel, so coming back to 2.5x is half a travel
        // the other side of it — already walking left, not sat in a dead patch waiting for the thumb to
        // come all the way home. (Not a full -0.5: half travel is past the deadzone, and what is left is
        // rescaled from its edge.)
        s.Update(Fingers(new TouchPoint(1, 200f + l.travel * 2.5f, 600f)), l, 0.2f);
        Assert.Less(s.MoveX, -0.2f, "the centre did not follow the thumb, so reversing crossed a dead patch");

        // ...and a full travel back from the dragged centre is a full push the other way, with no part of
        // the reach out to 4x travel still to be paid back.
        s.Update(Fingers(new TouchPoint(1, 200f + l.travel * 2f, 600f)), l, 0.3f);
        Assert.AreEqual(-1f, s.MoveX, 0.001f, "reversing did not reach full push, so the centre lagged the thumb");
    }

    [Test]
    public void LiftingTheThumb_StopsDead()
    {
        var l = Layout();
        var s = new TouchWalkState();
        s.Update(Fingers(new TouchPoint(1, 200f, 600f)), l, 0f);
        s.Update(Fingers(new TouchPoint(1, 200f + l.travel, 600f)), l, 0.1f);
        Assert.AreEqual(1f, s.MoveX, 0.001f);

        s.Update(None(), l, 0.2f);
        Assert.IsFalse(s.Walking);
        Assert.AreEqual(0f, s.MoveX, 0.001f, "the body kept walking after the thumb came off");
        Assert.AreEqual(0f, s.MoveY, 0.001f);
    }

    [Test]
    public void OnlyOneThumbSteersAtATime()
    {
        var l = Layout();
        var s = new TouchWalkState();
        s.Update(Fingers(new TouchPoint(1, 200f, 600f)), l, 0f);
        s.Update(Fingers(new TouchPoint(1, 200f + l.travel, 600f),
                         new TouchPoint(2, 100f, 500f)), l, 0.1f);

        Assert.AreEqual(1f, s.MoveX, 0.001f, "a second thumb in the zone stole the stick");
        Assert.AreEqual(200f, s.CentreX, 0.01f);
    }

    // ------------------------------------------------------------------ tapping

    [Test]
    public void AFingerOutsideTheStick_LiftedQuickly_IsATapWhereItLanded()
    {
        var l = Layout();
        var s = new TouchWalkState();

        s.Update(Fingers(new TouchPoint(7, 900f, 400f)), l, 0f);
        Assert.IsFalse(s.Tapped, "a tap fired while the finger was still down");

        s.Update(None(), l, 0.1f);
        Assert.IsTrue(s.Tapped, "a quick finger outside the stick was not a tap");
        Assert.AreEqual(900f, s.TapX, 0.01f);
        Assert.AreEqual(400f, s.TapY, 0.01f);

        s.Update(None(), l, 0.2f);
        Assert.IsFalse(s.Tapped, "the same tap fired twice");
    }

    [Test]
    public void AFingerThatRestsOrWanders_IsNotATap()
    {
        var l = Layout();

        var slow = new TouchWalkState();
        slow.Update(Fingers(new TouchPoint(7, 900f, 400f)), l, 0f);
        slow.Update(None(), l, TouchWalkLayout.TapSeconds + 0.1f);
        Assert.IsFalse(slow.Tapped, "a finger left on the glass counted as a tap when it finally came off");

        var dragged = new TouchWalkState();
        dragged.Update(Fingers(new TouchPoint(7, 900f, 400f)), l, 0f);
        dragged.Update(Fingers(new TouchPoint(7, 900f + l.tapSlop * 3f, 400f)), l, 0.05f);
        dragged.Update(None(), l, 0.1f);
        Assert.IsFalse(dragged.Tapped, "a finger dragged across the screen counted as a tap");
    }

    [Test]
    public void TakingTheStickIsNotATap()
    {
        var l = Layout();
        var s = new TouchWalkState();
        s.Update(Fingers(new TouchPoint(1, 200f, 600f)), l, 0f);
        s.Update(None(), l, 0.1f);
        Assert.IsFalse(s.Tapped, "a quick prod at the stick started a conversation");
    }

    [Test]
    public void PuttingTheControlsAway_ForgetsTheFingersOnTheGlass()
    {
        var l = Layout();
        var s = new TouchWalkState();
        s.Update(Fingers(new TouchPoint(7, 900f, 400f)), l, 0f);

        s.Reset();
        s.Update(None(), l, 0.1f);
        Assert.IsFalse(s.Tapped, "a finger that was down when a menu opened tapped as the menu closed");
        Assert.IsFalse(s.Walking);
    }
}
