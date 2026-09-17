using System.Collections.Generic;
using Draftmaster.Controls;
using NUnit.Framework;

// The on-screen driving controls for a phone: where the steering strip and pedals sit on real phone and
// tablet screens, and which finger is doing what. TouchDriveControls only reads the fingers and draws; the
// rules are TouchLayout / TouchDriveState, and they are all here.
public class TouchDriveTests
{
    // Landscape phones and tablets, from a small old handset to a 4:3 tablet.
    static readonly (float w, float h)[] Screens =
    {
        (800f, 360f), (854f, 480f), (1280f, 720f), (1920f, 1080f), (2400f, 1080f), (2960f, 1440f), (2048f, 1536f),
    };

    static TouchLayout Layout(float w, float h) =>
        new TouchLayout(new TouchRect(0f, 0f, w, h), TouchLayout.UnitFor(w, h));

    static bool Inside(TouchRect inner, TouchRect outer) =>
        inner.x >= outer.x && inner.y >= outer.y && inner.xMax <= outer.xMax && inner.yMax <= outer.yMax;

    // ------------------------------------------------------------------ layout

    [Test]
    public void Unit_IsAWholeNumber_FromTheShortSide_RoundedUp()
    {
        Assert.AreEqual(1f, TouchLayout.UnitFor(800f, 360f));
        Assert.AreEqual(2f, TouchLayout.UnitFor(854f, 480f), "between steps rounds up: a pedal is a thumb target");
        Assert.AreEqual(2f, TouchLayout.UnitFor(1280f, 720f));
        Assert.AreEqual(3f, TouchLayout.UnitFor(2400f, 1080f));
        Assert.AreEqual(4f, TouchLayout.UnitFor(1920f, 1200f));
        Assert.AreEqual(3f, TouchLayout.UnitFor(1080f, 2400f), "portrait takes the same unit as landscape");
        Assert.AreEqual(1f, TouchLayout.UnitFor(320f, 200f), "never below one pixel");
    }

    [Test]
    public void OnEveryScreen_NothingOverlaps_AndEverythingIsOnScreen()
    {
        foreach (var (w, h) in Screens)
        {
            var l = Layout(w, h);
            string at = $"{w}x{h}";

            Assert.IsTrue(Inside(l.throttle, l.safe), $"{at}: throttle {l.throttle} runs off the screen");
            Assert.IsTrue(Inside(l.brake, l.safe), $"{at}: brake {l.brake} runs off the screen");
            Assert.IsTrue(Inside(l.pause, l.safe), $"{at}: pause {l.pause} runs off the screen");
            Assert.IsTrue(Inside(l.steerRest, l.safe), $"{at}: steering strip {l.steerRest} runs off the screen");

            Assert.IsFalse(l.brake.Overlaps(l.throttle), $"{at}: the pedals overlap");
            Assert.IsFalse(l.brakeHit.Overlaps(l.throttleHit), $"{at}: one thumb could press both pedals");
            Assert.IsTrue(Inside(l.brake, l.brakeHit), $"{at}: part of the drawn brake doesn't press it");
            Assert.IsTrue(Inside(l.throttle, l.throttleHit), $"{at}: part of the drawn throttle doesn't press it");

            Assert.IsFalse(l.steerZone.Overlaps(l.brakeHit), $"{at}: steering zone reaches the brake");
            Assert.IsFalse(l.steerZone.Overlaps(l.throttleHit), $"{at}: steering zone reaches the throttle");
            Assert.IsFalse(l.pause.Inflate(TouchLayout.Slop * l.unit).Overlaps(l.steerZone),
                           $"{at}: a tap on pause could take the wheel");
            Assert.IsFalse(l.pause.Overlaps(l.brakeHit) || l.pause.Overlaps(l.throttleHit),
                           $"{at}: pause sits on a pedal");

            // Where the strip waits is where a thumb has to land to steer.
            Assert.IsTrue(Inside(l.steerRest, l.steerZone), $"{at}: the resting strip {l.steerRest} isn't in the steering zone {l.steerZone}");

            // The top of the screen is the HUD's (position, speed, lap): not a place a steer can start.
            Assert.GreaterOrEqual(l.steerZone.y, h * 0.2f, $"{at}: steering zone reaches up into the HUD");
        }
    }

    [Test]
    public void Pedals_AreBigEnoughForAThumb_AndStayInTheBottomHalf()
    {
        foreach (var (w, h) in Screens)
        {
            var l = Layout(w, h);
            // A landscape phone is ~65 mm tall and a thumb pad a good 8 mm across: a pedal under a sixth of the
            // height is a stab in the dark.
            Assert.GreaterOrEqual(l.throttle.height, h / 6f, $"{w}x{h}: throttle is too short");
            Assert.GreaterOrEqual(l.throttle.width, h / 8f, $"{w}x{h}: throttle is too narrow");

            // The race HUD's tyre/draft plate sits on the right a quarter to half way down; the pedals (and the
            // headset button that stands on them) stay below it.
            Assert.GreaterOrEqual(l.throttleHit.y, h * 0.45f, $"{w}x{h}: pedals reach up into the right-hand HUD");
        }
    }

    // A notch or camera cut-out on either end of a landscape phone takes a strip off the safe area.
    [Test]
    public void Notch_KeepsEveryControlInsideTheSafeArea()
    {
        var safe = new TouchRect(110f, 0f, 2400f - 220f, 1080f - 40f);
        var l = new TouchLayout(safe, TouchLayout.UnitFor(2400f, 1080f));

        Assert.IsTrue(Inside(l.throttle, safe), $"throttle {l.throttle} is under the notch");
        Assert.IsTrue(Inside(l.brake, safe), $"brake {l.brake} is under the notch");
        Assert.IsTrue(Inside(l.steerRest, safe), $"steering strip {l.steerRest} is under the notch");
        Assert.GreaterOrEqual(l.steerZone.x, safe.x);
        Assert.AreEqual(safe.centerX, l.pause.centerX, 0.01f);
    }

    // ------------------------------------------------------------------ fingers

    static readonly TouchLayout L = Layout(1920f, 1080f);

    static List<TouchPoint> Fingers(params TouchPoint[] p) => new List<TouchPoint>(p);
    static TouchPoint F(int id, float x, float y) => new TouchPoint(id, x, y);

    // A point well inside the steering zone.
    static float SteerX => L.steerZone.x + L.steerZone.width * 0.4f;
    static float SteerY => L.steerZone.y + L.steerZone.height * 0.6f;

    [Test]
    public void Steering_IsRelativeToWhereTheThumbLands()
    {
        var s = new TouchDriveState();
        float t = L.steerTravel;

        s.Update(Fingers(F(1, SteerX, SteerY)), L);
        Assert.IsTrue(s.Steering);
        Assert.AreEqual(0f, s.Steer, "landing is straight ahead, wherever the thumb lands");

        s.Update(Fingers(F(1, SteerX + t * 0.5f, SteerY)), L);
        Assert.AreEqual(0.5f, s.Steer, 1e-4f);

        s.Update(Fingers(F(1, SteerX - t, SteerY + 40f)), L);
        Assert.AreEqual(-1f, s.Steer, 1e-4f, "vertical drift doesn't matter; full travel left is full lock");
    }

    [Test]
    public void Steering_PastFullLock_DragsTheCentreAlong()
    {
        var s = new TouchDriveState();
        float t = L.steerTravel;

        s.Update(Fingers(F(1, SteerX, SteerY)), L);
        s.Update(Fingers(F(1, SteerX + t * 2f, SteerY)), L);
        Assert.AreEqual(1f, s.Steer, 1e-4f);

        // Coming back half a travel from there is half lock — not still full lock because the thumb is still
        // right of where it landed.
        s.Update(Fingers(F(1, SteerX + t * 1.5f, SteerY)), L);
        Assert.AreEqual(0.5f, s.Steer, 1e-4f);
    }

    [Test]
    public void OnlyOneThumbSteers()
    {
        var s = new TouchDriveState();
        float t = L.steerTravel;

        s.Update(Fingers(F(1, SteerX, SteerY)), L);
        s.Update(Fingers(F(1, SteerX, SteerY), F(2, SteerX + t * 0.5f, SteerY - 50f)), L);
        Assert.AreEqual(0f, s.Steer, "a second thumb in the zone doesn't steer");

        s.Update(Fingers(F(1, SteerX, SteerY), F(2, L.throttle.centerX, L.throttle.centerY)), L);
        Assert.AreEqual(0f, s.Throttle, "...nor press a pedal once it wanders over one");

        s.Update(Fingers(F(1, SteerX - t * 0.25f, SteerY), F(2, SteerX + t, SteerY)), L);
        Assert.AreEqual(-0.25f, s.Steer, 1e-4f);
    }

    [Test]
    public void SteeringThumb_StrayingOverThePedals_PressesNothing()
    {
        var s = new TouchDriveState();
        s.Update(Fingers(F(1, SteerX, SteerY)), L);
        s.Update(Fingers(F(1, L.brake.centerX, L.brake.centerY)), L);
        Assert.AreEqual(0f, s.Brake);
        Assert.AreEqual(0f, s.Throttle);
        Assert.AreEqual(1f, s.Steer, "still steering, at full lock");
    }

    [Test]
    public void PedalThumb_SlidesFromBrakeToThrottle()
    {
        var s = new TouchDriveState();

        s.Update(Fingers(F(1, L.brake.centerX, L.brake.centerY)), L);
        Assert.AreEqual(1f, s.Brake);
        Assert.AreEqual(0f, s.Throttle);

        s.Update(Fingers(F(1, L.throttle.centerX, L.throttle.centerY)), L);
        Assert.AreEqual(0f, s.Brake);
        Assert.AreEqual(1f, s.Throttle);

        // Off the pedals altogether (up the right side of the screen): neither.
        s.Update(Fingers(F(1, L.throttle.centerX, L.throttle.y - L.unit * 40f)), L);
        Assert.AreEqual(0f, s.Brake);
        Assert.AreEqual(0f, s.Throttle);
        Assert.IsFalse(s.Steering);
    }

    [Test]
    public void AThumbAnywhereAcrossThePedals_PressesExactlyOne()
    {
        var s = new TouchDriveState();
        int id = 0;
        for (float x = L.brake.x; x < L.throttle.xMax; x += 1f)
        {
            s.Update(Fingers(F(++id, x, L.brake.centerY)), L);
            Assert.AreEqual(1f, s.Brake + s.Throttle, $"x = {x}: brake {s.Brake}, throttle {s.Throttle}");
        }
    }

    [Test]
    public void TwoPedalThumbs_PressBoth()
    {
        var s = new TouchDriveState();
        s.Update(Fingers(F(1, L.brake.centerX, L.brake.centerY), F(2, L.throttle.centerX, L.throttle.centerY)), L);
        Assert.AreEqual(1f, s.Brake);
        Assert.AreEqual(1f, s.Throttle);
    }

    [Test]
    public void DrivingWithBothThumbs()
    {
        var s = new TouchDriveState();
        float t = L.steerTravel;
        s.Update(Fingers(F(1, SteerX, SteerY), F(2, L.throttle.centerX, L.throttle.centerY)), L);
        s.Update(Fingers(F(1, SteerX + t, SteerY), F(2, L.throttle.centerX, L.throttle.centerY)), L);
        Assert.AreEqual(1f, s.Steer, 1e-4f);
        Assert.AreEqual(1f, s.Throttle);
        Assert.AreEqual(0f, s.Brake);
    }

    [Test]
    public void PauseTap_FiresOncePerLanding()
    {
        var s = new TouchDriveState();
        var p = F(1, L.pause.centerX, L.pause.centerY);

        s.Update(Fingers(p), L);
        Assert.IsTrue(s.PauseTapped);
        s.Update(Fingers(p), L);
        Assert.IsFalse(s.PauseTapped, "a held finger is one tap");
        s.Update(Fingers(), L);
        s.Update(Fingers(F(2, L.pause.centerX, L.pause.centerY)), L);
        Assert.IsTrue(s.PauseTapped);
        Assert.IsFalse(s.Steering);
        Assert.AreEqual(0f, s.Throttle + s.Brake);
    }

    [Test]
    public void LiftingEveryFinger_LetsGoOfEverything()
    {
        var s = new TouchDriveState();
        s.Update(Fingers(F(1, SteerX, SteerY), F(2, L.throttle.centerX, L.throttle.centerY)), L);
        s.Update(Fingers(F(1, SteerX + L.steerTravel, SteerY), F(2, L.throttle.centerX, L.throttle.centerY)), L);
        s.Update(Fingers(), L);
        Assert.IsFalse(s.Steering);
        Assert.AreEqual(0f, s.Steer);
        Assert.AreEqual(0f, s.Throttle);
        Assert.AreEqual(0f, s.Brake);
    }

    // At 30 fps a quick re-grip lifts one thumb and plants the next inside a single frame.
    [Test]
    public void AThumbReplantedInOneFrame_TakesTheWheel()
    {
        var s = new TouchDriveState();
        s.Update(Fingers(F(1, SteerX, SteerY)), L);
        s.Update(Fingers(F(2, SteerX + 30f, SteerY)), L);
        Assert.IsTrue(s.Steering);
        Assert.AreEqual(SteerX + 30f, s.SteerCentreX, "the new thumb's landing is the new centre");

        s.Update(Fingers(F(2, SteerX + 30f + L.steerTravel * 0.5f, SteerY)), L);
        Assert.AreEqual(0.5f, s.Steer, 1e-4f);
    }

    // The pause menu puts the controls away; when it closes, the fingers still down are placed again.
    [Test]
    public void AfterAReset_HeldThumbsCarryOn_ButTheHeldPauseFingerDoesNotTapAgain()
    {
        var s = new TouchDriveState();
        var wheel = F(1, SteerX, SteerY);
        var gas = F(2, L.throttle.centerX, L.throttle.centerY);
        var pause = F(3, L.pause.centerX, L.pause.centerY);

        s.Update(Fingers(wheel, gas, pause), L);
        Assert.IsTrue(s.PauseTapped);

        s.Reset();
        Assert.IsFalse(s.Steering);
        Assert.AreEqual(0f, s.Throttle);

        var wheelMoved = F(1, SteerX + 60f, SteerY);
        s.Update(Fingers(wheelMoved, gas, pause), L);
        Assert.IsFalse(s.PauseTapped, "the finger that opened the menu is not a second tap");
        Assert.IsTrue(s.Steering);
        Assert.AreEqual(0f, s.Steer, "the wheel is taken from where the thumb is now, with no jump");
        Assert.AreEqual(1f, s.Throttle);

        s.Update(Fingers(wheelMoved, gas), L);
        s.Update(Fingers(wheelMoved, gas, F(4, L.pause.centerX, L.pause.centerY)), L);
        Assert.IsTrue(s.PauseTapped, "a fresh tap still pauses");
    }

    // ------------------------------------------------------------------ Android's pad-button echoes

    [Test]
    public void PadKeyEcho_OnlyTheKeyPressJustAfterAPadPress_IsAnEcho()
    {
        var e = new PadKeyEcho();
        Assert.IsFalse(e.IsEcho(0.0), "no pad press yet: Esc is the phone's back gesture");
        Assert.IsFalse(e.IsEcho(5.0));

        e.PadPressed(10.0);
        Assert.IsTrue(e.IsEcho(10.0), "same timestamp — Android stamps the fallback with the button's time");
        Assert.IsTrue(e.IsEcho(10.05));
        Assert.IsTrue(e.IsEcho(10.0 + PadKeyEcho.Window));
        Assert.IsFalse(e.IsEcho(10.0 + PadKeyEcho.Window + 0.01), "a real key a moment later is a real key");
        Assert.IsFalse(e.IsEcho(9.9), "a key before the pad press isn't its echo");

        e.PadPressed(8.0);   // an older event arriving late doesn't wind the clock back
        Assert.IsTrue(e.IsEcho(10.1));

        e.Reset();
        Assert.IsFalse(e.IsEcho(10.1));
    }
}
