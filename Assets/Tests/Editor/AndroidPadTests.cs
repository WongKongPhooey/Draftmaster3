using Draftmaster.Controls;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Android;
using UnityEngine.InputSystem.Android.LowLevel;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.Layouts;

// A controller paired with a phone has to drive the game the way an Xbox pad does on a PC. The game reads
// pads only through Gamepad.current — the left stick and triggers in PlayerVehicleController, the buttons
// in PadInput — so what matters is that an Android pad turns up as a Gamepad with those controls where the
// game expects them, whatever make it is. These add the pads the way Android reports them (interface
// "Android", class "AndroidGameController", capabilities JSON) to a throwaway input system.
//
// Also here: Android's habit of turning an unclaimed B press into a Back key, which the input system
// reports as Esc (PadKeyEchoFilter).
public class AndroidPadTests : InputTestFixture
{
    const int VendorMicrosoft = 0x045e;
    const int VendorSony = 0x054c;
    const int Vendor8BitDo = 0x2dc8;

    // AndroidInputSource.Gamepad | AndroidInputSource.Joystick — what a pad reports.
    const int SourceGamepadJoystick = 1025 | 16777232;

    static readonly AndroidAxis[] FullPadAxes =
    {
        AndroidAxis.X, AndroidAxis.Y, AndroidAxis.Z, AndroidAxis.Rz, AndroidAxis.HatX, AndroidAxis.HatY,
        AndroidAxis.Ltrigger, AndroidAxis.Rtrigger, AndroidAxis.Brake, AndroidAxis.Gas,
    };

    // The capabilities struct the input system parses is internal; this is its JSON, field for field.
    static InputDevice AddAndroidPad(int vendor, int product, string name, string maker, AndroidAxis[] axes)
    {
        string axisList = axes == null ? "" : string.Join(",", System.Array.ConvertAll(axes, a => ((int)a).ToString()));
        string caps = "{\"deviceDescriptor\":\"test-" + vendor + "-" + product + "\",\"productId\":" + product +
                      ",\"vendorId\":" + vendor + ",\"isVirtual\":false,\"motionAxes\":[" + axisList +
                      "],\"inputSources\":" + SourceGamepadJoystick + "}";
        return InputSystem.AddDevice(new InputDeviceDescription
        {
            interfaceName = "Android",
            deviceClass = "AndroidGameController",
            product = name,
            manufacturer = maker,
            capabilities = caps,
        });
    }

    static void Send(InputDevice device, AndroidGameControllerState state)
    {
        InputSystem.QueueStateEvent(device, state);
        InputSystem.Update();
    }

    [TearDown]
    public override void TearDown()
    {
        PadKeyEchoFilter.Uninstall();
        base.TearDown();
    }

    // ------------------------------------------------------------------ which pad is which

    [Test]
    public void XboxPad_OnAPhone_DrivesLikeOnAPC()
    {
        var device = AddAndroidPad(VendorMicrosoft, 0x0b13, "Xbox Wireless Controller", "Microsoft", FullPadAxes);
        Assert.IsInstanceOf<XboxOneGamepadAndroid>(device, $"came up as {device.layout}");
        var pad = (Gamepad)device;

        // Throttle on RT, brake on LT, steer on the left stick — PlayerVehicleController's reads.
        Send(pad, new AndroidGameControllerState()
            .WithAxis(AndroidAxis.Gas, 1f)
            .WithAxis(AndroidAxis.X, -0.8f));
        Assert.AreSame(pad, Gamepad.current, "the pad in use is the one the game reads");
        Assert.AreEqual(1f, pad.rightTrigger.ReadValue(), 0.01f, "RT");
        Assert.AreEqual(0f, pad.leftTrigger.ReadValue(), 0.01f, "LT");
        Assert.Less(pad.leftStick.ReadValue().x, -0.5f, "left stick left");

        Send(pad, new AndroidGameControllerState().WithAxis(AndroidAxis.Brake, 1f));
        Assert.AreEqual(1f, pad.leftTrigger.ReadValue(), 0.01f, "LT");
        Assert.AreEqual(0f, pad.rightTrigger.ReadValue(), 0.01f, "RT");

        // The buttons PadBindings hands out: face buttons, shoulders, Menu / View, d-pad.
        Send(pad, new AndroidGameControllerState()
            .WithButton(AndroidKeyCode.ButtonA)
            .WithButton(AndroidKeyCode.ButtonB)
            .WithButton(AndroidKeyCode.ButtonL1)
            .WithButton(AndroidKeyCode.ButtonStart)
            .WithAxis(AndroidAxis.HatX, -1f)
            .WithAxis(AndroidAxis.HatY, 1f));
        Assert.IsTrue(pad.buttonSouth.isPressed, "A");
        Assert.IsTrue(pad.buttonEast.isPressed, "B");
        Assert.IsFalse(pad.buttonWest.isPressed, "X");
        Assert.IsTrue(pad.leftShoulder.isPressed, "LB");
        Assert.IsTrue(pad.startButton.isPressed, "Menu");
        Assert.IsFalse(pad.selectButton.isPressed, "View");
        Assert.IsTrue(pad.dpad.left.isPressed, "d-pad left");
        Assert.IsTrue(pad.dpad.down.isPressed, "d-pad down");
        Assert.IsFalse(pad.dpad.up.isPressed, "d-pad up");
    }

    [Test]
    public void PlayStationPad_OnAPhone_IsKnownAsOne()
    {
        var device = AddAndroidPad(VendorSony, 0x09cc, "Wireless Controller", "Sony Interactive Entertainment", FullPadAxes);
        Assert.IsInstanceOf<DualShockGamepad>(device, $"came up as {device.layout}");
        // InputGlyphs.IsPlayStation goes by the layout name first; this is what it matches on.
        StringAssert.Contains("DualShock", device.layout);

        var pad = (Gamepad)device;
        Send(pad, new AndroidGameControllerState().WithButton(AndroidKeyCode.ButtonB).WithAxis(AndroidAxis.Gas, 1f));
        Assert.IsTrue(pad.buttonEast.isPressed, "Circle");
        Assert.AreEqual(1f, pad.rightTrigger.ReadValue(), 0.01f, "R2");
    }

    // An 8BitDo, a GameSir, a Razer Kishi: no vendor the input system knows, so a generic Android pad. It must
    // still be a Gamepad, or Gamepad.current never sees it and the game ignores it.
    [Test]
    public void AnyOtherPad_OnAPhone_IsStillAGamepad()
    {
        var device = AddAndroidPad(Vendor8BitDo, 0x6001, "8BitDo Pro 2", "8BitDo", FullPadAxes);
        Assert.IsInstanceOf<AndroidGamepad>(device, $"came up as {device.layout}");
        var pad = (Gamepad)device;

        Send(pad, new AndroidGameControllerState()
            .WithAxis(AndroidAxis.Gas, 1f)
            .WithAxis(AndroidAxis.X, 0.8f)
            .WithAxis(AndroidAxis.HatY, -1f)
            .WithButton(AndroidKeyCode.ButtonX));
        Assert.AreEqual(1f, pad.rightTrigger.ReadValue(), 0.01f, "RT");
        Assert.Greater(pad.leftStick.ReadValue().x, 0.5f, "left stick right");
        Assert.IsTrue(pad.dpad.up.isPressed, "d-pad up (from the hat axis)");
        Assert.IsTrue(pad.buttonWest.isPressed, "X");
        Assert.AreSame(pad, Gamepad.current, "the pad in use is the one the game reads");
    }

    // Some pads send the d-pad as key presses rather than a hat axis.
    [Test]
    public void APadWithADpadOfButtons_OnAPhone_StillHasADpad()
    {
        var device = AddAndroidPad(Vendor8BitDo, 0x3106, "Pad", "Generic", null);
        Assert.IsInstanceOf<AndroidGamepadWithDpadButtons>(device, $"came up as {device.layout}");
        var pad = (Gamepad)device;

        Send(pad, new AndroidGameControllerState().WithButton(AndroidKeyCode.DpadRight));
        Assert.IsTrue(pad.dpad.right.isPressed);
    }

    // ------------------------------------------------------------------ B's Back-key echo

    [Test]
    public void TheEscAndroidMakesOutOfB_IsDropped()
    {
        var pad = InputSystem.AddDevice<Gamepad>();
        var keyboard = InputSystem.AddDevice<Keyboard>();
        PadKeyEchoFilter.Install();

        currentTime = 10;
        Press(pad.buttonEast);
        Assert.IsTrue(pad.buttonEast.isPressed, "the pad's own B still counts");

        currentTime = 10.02;
        Press(keyboard.escapeKey);
        Assert.IsFalse(keyboard.escapeKey.isPressed, "the fallback Back key is not a second press");
        Assert.IsFalse(keyboard.escapeKey.wasPressedThisFrame);

        currentTime = 10.05;
        Release(keyboard.escapeKey);
        Release(pad.buttonEast);
        Assert.IsFalse(keyboard.escapeKey.isPressed);

        // The phone's own back gesture, well after any pad press, is Esc as before.
        currentTime = 12;
        Press(keyboard.escapeKey);
        Assert.IsTrue(keyboard.escapeKey.isPressed, "a real Back press still pauses");
    }

    [Test]
    public void WithoutAPad_EscIsNeverDropped()
    {
        var keyboard = InputSystem.AddDevice<Keyboard>();
        PadKeyEchoFilter.Install();

        currentTime = 1;
        Press(keyboard.escapeKey);
        Assert.IsTrue(keyboard.escapeKey.isPressed);
    }

    [Test]
    public void ASmallSteerOnTheStick_IsNotAPadPress()
    {
        var pad = InputSystem.AddDevice<Gamepad>();
        var keyboard = InputSystem.AddDevice<Keyboard>();
        PadKeyEchoFilter.Install();

        currentTime = 5;
        Set(pad.leftStick, new Vector2(0.2f, 0f));   // a small steer is not a button press
        currentTime = 5.01;
        Press(keyboard.escapeKey);
        Assert.IsTrue(keyboard.escapeKey.isPressed);
    }

    [Test]
    public void TheFilter_IsOffUntilInstalled()
    {
        var pad = InputSystem.AddDevice<Gamepad>();
        var keyboard = InputSystem.AddDevice<Keyboard>();
        Assert.IsFalse(PadKeyEchoFilter.Installed, "only InputDeviceWatcher, on Android, switches it on");

        currentTime = 3;
        Press(pad.buttonEast);
        currentTime = 3.02;
        Press(keyboard.escapeKey);
        Assert.IsTrue(keyboard.escapeKey.isPressed, "a PC gets every key");
    }
}

// Project settings a phone build depends on.
public class AndroidBuildSettingsTests
{
    // An empty list means every kind of device; a non-empty one has to name pads and touchscreens, or an
    // Android build would never create the devices the game reads.
    [Test]
    public void InputSettings_LetPadsAndTouchscreensThrough()
    {
        var supported = InputSystem.settings.supportedDevices;
        if (supported.Count == 0) return;
        CollectionAssert.Contains(supported, "Gamepad");
        CollectionAssert.Contains(supported, "Touchscreen");
    }

    // Thumbs on both sides of the screen only works in landscape.
    [Test]
    public void ThePhoneBuild_RunsInLandscape()
    {
        var o = PlayerSettings.defaultInterfaceOrientation;
        if (o == UIOrientation.AutoRotation)
        {
            Assert.IsFalse(PlayerSettings.allowedAutorotateToPortrait, "auto-rotation lets the game turn portrait");
            Assert.IsFalse(PlayerSettings.allowedAutorotateToPortraitUpsideDown, "auto-rotation lets the game turn portrait");
            Assert.IsTrue(PlayerSettings.allowedAutorotateToLandscapeLeft || PlayerSettings.allowedAutorotateToLandscapeRight);
            return;
        }
        Assert.That(o == UIOrientation.LandscapeLeft || o == UIOrientation.LandscapeRight,
                    $"default orientation is {o}");
    }
}
