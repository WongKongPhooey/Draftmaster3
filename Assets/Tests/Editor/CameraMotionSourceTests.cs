using NUnit.Framework;
using Draftmaster.Sim;

// Where the driving camera reads a car's motion from (CameraFeel.PickMotionSource / IsTeleport). The case that
// mattered: on Auto behind the safety car the player's PlayerVehicleController is switched off and a kinematic
// SplineDriver writes the transform. The switched-off controller's speed and heading are frozen, and reading
// them held the swing camera still, so Auto looked like the fixed camera until the green.
public class CameraMotionSourceTests
{
    [Test]
    public void ARunningControllerIsAlwaysAsked()
    {
        Assert.AreEqual(CameraFeel.MotionSource.Controller, CameraFeel.PickMotionSource(true, false));
        Assert.AreEqual(CameraFeel.MotionSource.Controller, CameraFeel.PickMotionSource(true, true));
    }

    [Test]
    public void ASwitchedOffControllerHandsOverToTheSplineDrivingTheCar()
        => Assert.AreEqual(CameraFeel.MotionSource.Spline, CameraFeel.PickMotionSource(false, true));

    [Test]
    public void WithNothingDrivingTheCarItsTransformIsRead()
        => Assert.AreEqual(CameraFeel.MotionSource.Transform, CameraFeel.PickMotionSource(false, false));

    [Test]
    public void RacingSpeedIsNotATeleport()
    {
        const float dt = 1f / 60f;
        // 220 mph in one frame, and a long 0.2 s hitch at the same speed.
        Assert.IsFalse(CameraFeel.IsTeleport(98f * dt, dt));
        Assert.IsFalse(CameraFeel.IsTeleport(98f * 0.2f, 0.2f));
        Assert.IsFalse(CameraFeel.IsTeleport(0f, dt));
    }

    [Test]
    public void BeingPutDownElsewhereIsATeleport()
    {
        const float dt = 1f / 60f;
        Assert.IsTrue(CameraFeel.IsTeleport(400f, dt));   // a tow back to the box
        Assert.IsTrue(CameraFeel.IsTeleport(5f, dt));     // 300 m/s: nothing drives that fast
    }

    [Test]
    public void NoTimeStepNeverReadsAsATeleport()
        => Assert.IsFalse(CameraFeel.IsTeleport(400f, 0f));
}
