using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// EditMode coverage for driving the paddock golf cart and for the people who get out of its way.
//
// Both halves are deliberately arithmetic with no scene in them — CartDrive is throttle, brake and lock,
// and CartDodge's two rules ("is this person in the way" and "which way do they jump") are geometry — so
// the things that actually make it feel like a cart can be pinned down here rather than by driving it:
//
//   * a stopped cart cannot pivot on the spot, which is the whole difference between a vehicle and a person;
//   * back-stick stops it before it ever reverses, so a stab of brake is never a lurch backwards;
//   * steering mirrors in reverse, as it does in a real car park;
//   * a cart stopped against a fence loses its speed there instead of storing it up.
//
// Both live in Assembly-CSharp, which an asmdef can't reference, so they're reached by reflection — the
// same way GolfCartTests reaches the cart itself.
public class CartDriveTests
{
    static readonly Type DriveType = Type.GetType("CartDrive, Assembly-CSharp");
    static readonly Type DodgeType = Type.GetType("CartDodge, Assembly-CSharp");

    const float Dt = 0.02f;   // one fixed step

    // --- reflection helpers -------------------------------------------------------------------------

    static object NewDrive(Vector2 heading)
    {
        Assert.IsNotNull(DriveType, "CartDrive is gone — the cart has no driving model.");
        object drive = Activator.CreateInstance(DriveType);
        DriveType.GetMethod("Reset").Invoke(drive, new object[] { heading });
        return drive;
    }

    static Vector2 Step(object drive, Vector2 stick, float dt = Dt) =>
        (Vector2)DriveType.GetMethod("Step").Invoke(drive, new object[] { stick, dt });

    // Hold a stick for `seconds` worth of fixed steps and hand back the last velocity.
    static Vector2 Hold(object drive, Vector2 stick, float seconds)
    {
        Vector2 v = Vector2.zero;
        for (int i = 0; i < Mathf.RoundToInt(seconds / Dt); i++) v = Step(drive, stick);
        return v;
    }

    static float Speed(object drive) => (float)DriveType.GetProperty("Speed").GetValue(drive);
    static Vector2 Heading(object drive) => (Vector2)DriveType.GetProperty("Heading").GetValue(drive);
    static void Blocked(object drive, Vector2 actual) =>
        DriveType.GetMethod("Blocked").Invoke(drive, new object[] { actual });

    static void SetTunable(object drive, string name, float value) =>
        DriveType.GetField(name).SetValue(drive, value);

    static float Tunable(object drive, string name) => (float)DriveType.GetField(name).GetValue(drive);

    static bool ShouldDodge(Vector2 cartPos, Vector2 cartVel, Vector2 person, float clearance, float lookahead)
    {
        Assert.IsNotNull(DodgeType, "CartDodge is gone — nobody gets out of the cart's way.");
        return (bool)DodgeType.GetMethod("ShouldDodge", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { cartPos, cartVel, person, clearance, lookahead });
    }

    static Vector2 EscapeDirection(Vector2 cartPos, Vector2 cartHeading, Vector2 person) =>
        (Vector2)DodgeType.GetMethod("EscapeDirection", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { cartPos, cartHeading, person });

    // --- the pedals ---------------------------------------------------------------------------------

    [Test]
    public void ForwardOnTheStickAcceleratesAlongTheNose()
    {
        var drive = NewDrive(Vector2.right);
        Vector2 v = Hold(drive, Vector2.up, 0.5f);

        Assert.Greater(Speed(drive), 0.1f, "Holding forward did not accelerate the cart.");
        // Pushing forward is the throttle, not a direction to travel in: a cart pointing east that is
        // asked to accelerate goes east, whichever way the stick happens to be leaning.
        Assert.Greater(v.x, 0f, "The cart moved somewhere other than along its own nose.");
        Assert.AreEqual(0f, v.y, 1e-4f, "The cart drifted sideways off the stick direction.");
    }

    [Test]
    public void TopSpeedIsNeverExceeded()
    {
        var drive = NewDrive(Vector2.up);
        Hold(drive, Vector2.up, 10f);
        Assert.AreEqual(Tunable(drive, "topSpeed"), Speed(drive), 0.01f,
            "Holding the throttle ran the cart past its top speed.");
    }

    [Test]
    public void LettingGoCoastsToAStopRatherThanDroppingDead()
    {
        var drive = NewDrive(Vector2.up);
        Hold(drive, Vector2.up, 2f);
        float rolling = Speed(drive);

        Step(drive, Vector2.zero);
        Assert.Less(Speed(drive), rolling, "Off the throttle the cart did not slow at all.");
        Assert.Greater(Speed(drive), 0f, "Off the throttle the cart stopped dead in a single step.");

        Hold(drive, Vector2.zero, 5f);
        Assert.AreEqual(0f, Speed(drive), 1e-4f, "The cart coasted forever.");
    }

    [Test]
    public void BackOnTheStickStopsItBeforeItEverReverses()
    {
        var drive = NewDrive(Vector2.up);
        Hold(drive, Vector2.up, 0.2f);       // pottering along at walking pace
        Assert.Greater(Speed(drive), 0.5f);

        // Braking to a halt and holding the stick a beat longer than needed is what stopping looks like.
        // It must scrub the speed off and sit there, never snap into reverse.
        Hold(drive, Vector2.down, 0.3f);
        Assert.AreEqual(0f, Speed(drive), 1e-4f, "A stab of brake threw the cart into reverse.");
    }

    [Test]
    public void HoldingTheBrakeOnceStoppedBacksItUpSlowly()
    {
        var drive = NewDrive(Vector2.up);
        Hold(drive, Vector2.down, 3f);

        Assert.Less(Speed(drive), 0f, "Holding the brake from a standstill never reversed.");
        Assert.GreaterOrEqual(Speed(drive), -Tunable(drive, "reverseSpeed") - 0.01f,
            "The cart reversed faster than its reverse speed.");
        Assert.Less(Tunable(drive, "reverseSpeed"), Tunable(drive, "topSpeed"),
            "Reverse should be slower than forwards.");
    }

    [Test]
    public void ThrottleAgainstReverseIsTheBrake()
    {
        var drive = NewDrive(Vector2.up);
        Hold(drive, Vector2.down, 3f);        // backing up
        float backwards = Speed(drive);

        Hold(drive, Vector2.up, 0.2f);
        Assert.Greater(Speed(drive), backwards, "Forward on the stick did not pull the cart out of reverse.");
    }

    // --- the steering -------------------------------------------------------------------------------

    [Test]
    public void AStoppedCartCannotPivotOnTheSpot()
    {
        var drive = NewDrive(Vector2.up);
        Vector2 before = Heading(drive);

        Hold(drive, Vector2.left, 1f);       // full lock, no throttle
        Assert.AreEqual(before.x, Heading(drive).x, 1e-4f, "A stopped cart turned like a person.");
        Assert.AreEqual(before.y, Heading(drive).y, 1e-4f, "A stopped cart turned like a person.");
        Assert.AreEqual(0f, Speed(drive), 1e-4f, "Steering alone moved the cart.");
    }

    [Test]
    public void SteeringRightSwingsTheNoseClockwise()
    {
        var drive = NewDrive(Vector2.up);
        Hold(drive, Vector2.up, 1f);                         // get it rolling
        Hold(drive, new Vector2(1f, 1f), 0.2f);              // throttle and full right lock

        // Nose was north; turning right puts it east of north.
        Assert.Greater(Heading(drive).x, 0.05f, "Pushing right did not steer the cart right.");
        Assert.Greater(Heading(drive).y, 0f, "Half a second of lock swung the cart further than it should.");
    }

    [Test]
    public void SteeringIsMirroredWhileReversing()
    {
        var forward = NewDrive(Vector2.up);
        Hold(forward, Vector2.up, 1f);
        Hold(forward, new Vector2(1f, 1f), 0.4f);

        var backward = NewDrive(Vector2.up);
        Hold(backward, Vector2.down, 3f);                    // stopped, then backing up
        Hold(backward, new Vector2(1f, -1f), 0.4f);          // same lock, still reversing

        Assert.Greater(Heading(forward).x, 0f, "Right lock did not turn the cart right going forwards.");
        Assert.Less(Heading(backward).x, 0f,
            "Right lock swung the nose the same way in reverse — a reversing cart steers the other way.");
    }

    [Test]
    public void ASlowCartSteersLazilyAndAFastOneDoesNot()
    {
        var crawling = NewDrive(Vector2.up);
        SetTunable(crawling, "topSpeed", 0.5f);              // pinned well below the bite speed
        Hold(crawling, Vector2.up, 2f);
        float crawlHeading = Heading(crawling).x;
        Hold(crawling, new Vector2(1f, 1f), 0.3f);
        float crawlTurn = Mathf.Abs(Heading(crawling).x - crawlHeading);

        var rolling = NewDrive(Vector2.up);
        Hold(rolling, Vector2.up, 2f);
        float rollHeading = Heading(rolling).x;
        Hold(rolling, new Vector2(1f, 1f), 0.3f);
        float rollTurn = Mathf.Abs(Heading(rolling).x - rollHeading);

        Assert.Greater(rollTurn, crawlTurn, "Steering did not bite harder once the cart was up to speed.");
    }

    [Test]
    public void ARestingStickDoesNothingAtAll()
    {
        var drive = NewDrive(Vector2.up);
        float deadzone = Tunable(drive, "deadzone");
        Hold(drive, new Vector2(deadzone * 0.5f, deadzone * 0.5f), 2f);

        Assert.AreEqual(0f, Speed(drive), 1e-4f, "Stick drift crept the cart across the paddock.");
        Assert.AreEqual(1f, Heading(drive).y, 1e-4f, "Stick drift steered the cart.");
    }

    // --- running into things ------------------------------------------------------------------------

    [Test]
    public void ACartStoppedAgainstAFenceLosesItsSpeedThere()
    {
        var drive = NewDrive(Vector2.up);
        Hold(drive, Vector2.up, 2f);
        Assert.Greater(Speed(drive), 1f);

        Blocked(drive, Vector2.zero);       // the boundary clamp ate the whole step
        Assert.AreEqual(0f, Speed(drive), 1e-4f,
            "Speed survived hitting a fence — the cart would leap away the moment it came off it.");
    }

    [Test]
    public void BeingSlowedDoesNotSpeedTheCartUp()
    {
        var drive = NewDrive(Vector2.up);
        Hold(drive, Vector2.up, 0.4f);
        float rolling = Speed(drive);

        // A shove out of somebody can add velocity; it must never be read as free throttle.
        Blocked(drive, Vector2.up * (rolling + 5f));
        Assert.AreEqual(rolling, Speed(drive), 1e-4f, "A bump handed the cart speed it never earned.");
    }

    // --- getting out of the way ---------------------------------------------------------------------

    [Test]
    public void SomebodyInFrontOfTheCartIsToldToMove()
    {
        // Cart at the origin doing 8 m/s north; a walker four metres up the road, dead in its path.
        Assert.IsTrue(ShouldDodge(Vector2.zero, Vector2.up * 8f, new Vector2(0.2f, 3.5f), 1f, 0.45f),
            "Somebody stood in the cart's path was never warned.");
    }

    [Test]
    public void SomebodyBesideOrBehindTheCartIsLeftAlone()
    {
        Assert.IsFalse(ShouldDodge(Vector2.zero, Vector2.up * 8f, new Vector2(3f, 1f), 1f, 0.45f),
            "Somebody well off to the side was made to dive for no reason.");
        Assert.IsFalse(ShouldDodge(Vector2.zero, Vector2.up * 8f, new Vector2(0f, -2f), 1f, 0.45f),
            "Somebody behind the cart dived out of the way of a cart already past them.");
    }

    [Test]
    public void AParkedCartOnlyThreatensWhatIsUnderIt()
    {
        Assert.IsTrue(ShouldDodge(Vector2.zero, Vector2.zero, new Vector2(0.5f, 0f), 1f, 0.45f),
            "Somebody stood in a stationary cart's own footprint was ignored.");
        Assert.IsFalse(ShouldDodge(Vector2.zero, Vector2.zero, new Vector2(0f, 3f), 1f, 0.45f),
            "A stationary cart scattered people it was nowhere near.");
    }

    [Test]
    public void TheyJumpSquareAcrossThePathAndOutOfIt()
    {
        // Cart heading north, person a touch to the right of its nose: they go right, at ninety degrees.
        Vector2 dir = EscapeDirection(Vector2.zero, Vector2.up, new Vector2(0.3f, 2f));
        Assert.AreEqual(0f, Vector2.Dot(dir, Vector2.up), 1e-4f, "The dive was not perpendicular to the cart.");
        Assert.Greater(dir.x, 0f, "They dived back across the cart instead of off the side they were on.");
        Assert.AreEqual(1f, dir.magnitude, 1e-4f, "The escape direction is not a unit vector.");

        Vector2 other = EscapeDirection(Vector2.zero, Vector2.up, new Vector2(-0.3f, 2f));
        Assert.Less(other.x, 0f, "Somebody on the left of the path was sent across it to the right.");
    }

    [Test]
    public void TwoPeopleStoodTogetherDeadAheadGoTheSameWay()
    {
        // Exactly on the centre line is a coin flip nobody wants: a pair stood side by side would dive
        // through each other. It resolves the same way every time instead.
        Vector2 a = EscapeDirection(Vector2.zero, Vector2.up, new Vector2(0f, 2f));
        Vector2 b = EscapeDirection(Vector2.zero, Vector2.up, new Vector2(0f, 2.4f));
        Assert.AreEqual(a.x, b.x, 1e-4f, "Two people dead ahead were sent opposite ways.");
        Assert.AreEqual(a.y, b.y, 1e-4f, "Two people dead ahead were sent opposite ways.");
    }
}
