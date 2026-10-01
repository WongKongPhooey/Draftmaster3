using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// EditMode coverage for the paddock e-scooter — the runabout EScooterSpawner parks at the mouth of the
// player's own team garage, and the ride that happens when somebody presses the action button next to it.
//
// None of this can be checked by looking at a scene: the scooter is built at play time, against a garage row
// that is itself generated from the live roster. So these tests stand a rig up the way PopupGarageLot
// does, ask the spawner where a scooter goes against it, and measure what came out — and then mount a scooter
// on a walker and check the player is handed back exactly the speed they had before.
//
// The two that matter:
//   * the parking spot clears the shed AND a full scooter length fits in the gap, or the scooter is drawn
//     through the garage it is supposed to be parked outside;
//   * dismounting restores moveSpeed and runMultiplier, or stepping off leaves the player permanently
//     sprinting around the paddock at scooter speed.
//
// EScooter and EScooterSpawner live in Assembly-CSharp, which an asmdef can't reference, so they're
// reached by reflection — the same way PopupGarageTests reaches the garages.
public class EScooterTests
{
    static readonly System.Type ScooterType = System.Type.GetType("EScooter, Assembly-CSharp");
    static readonly System.Type SpawnerType = System.Type.GetType("EScooterSpawner, Assembly-CSharp");
    static readonly System.Type RigType = System.Type.GetType("PopupGarageRig, Assembly-CSharp");
    static readonly System.Type WalkerType = System.Type.GetType("OnFootController, Assembly-CSharp");

    // What PopupGarageLot hands each rig, restated here so a silent change to a default shows up as a
    // failing test rather than as a scooter that quietly moved.
    const float BodyWidth = 3.95f;
    const float BodyLength = 9.93f;
    const float CanopyWidth = 6.5f;
    const float CanopyLength = 7.2f;

    // The spawner's own default gap past the end of the rig.
    const float NoseGap = 1f;

    readonly List<GameObject> _made = new();

    [SetUp]
    public void Reset() => ForgetRiddenScooter();

    [TearDown]
    public void CleanUp()
    {
        foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
        _made.Clear();
        ForgetRiddenScooter();
    }

    // "Who is riding a scooter" is a static, and edit mode keeps statics between fixtures — a test that
    // mounts and is then torn down would otherwise leave the next one unable to get on anything.
    static void ForgetRiddenScooter()
    {
        var p = ScooterType?.GetProperty("Ridden", BindingFlags.Public | BindingFlags.Static);
        p?.GetSetMethod(true)?.Invoke(null, new object[] { null });
    }

    // --- reflection helpers -----------------------------------------------------------------------

    static object Field(object o, string name)
    {
        var f = o.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(f, $"{o.GetType().Name}.{name} is gone.");
        return f.GetValue(o);
    }

    static void SetField(object o, string name, object value)
    {
        var f = o.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(f, $"{o.GetType().Name}.{name} is gone.");
        f.SetValue(o, value);
    }

    static object Prop(object o, string name)
    {
        var p = o.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(p, $"{o.GetType().Name}.{name} is gone.");
        return p.GetValue(o);
    }

    static object Call(object o, string name, params object[] args)
    {
        var m = o.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(m, $"{o.GetType().Name}.{name}() is gone.");
        return m.Invoke(o, args);
    }

    static float SpawnerDefault(string field)
    {
        Assert.IsNotNull(SpawnerType, "EScooterSpawner is missing from Assembly-CSharp.");
        var go = new GameObject("SpawnerDefaults");
        try
        {
            var spawner = go.AddComponent(SpawnerType);
            return (float)Field(spawner, field);
        }
        finally { Object.DestroyImmediate(go); }
    }

    // --- the things under test --------------------------------------------------------------------

    // A rig configured the way PopupGarageLot configures one, parked where the caller asks.
    Component Rig(int canopySide, Vector3 position, Quaternion rotation)
    {
        Assert.IsNotNull(RigType, "PopupGarageRig is missing from Assembly-CSharp.");
        var go = new GameObject("Garage");
        _made.Add(go);
        go.transform.SetPositionAndRotation(position, rotation);

        var rig = go.AddComponent(RigType);
        SetField(rig, "carNumber", 20);
        SetField(rig, "teamName", "Test Motorsports");
        SetField(rig, "carset", "");
        SetField(rig, "canopySide", canopySide);
        SetField(rig, "carAtHome", true);
        SetField(rig, "bodyWidth", BodyWidth);
        SetField(rig, "bodyLength", BodyLength);
        SetField(rig, "canopyWidth", CanopyWidth);
        SetField(rig, "canopyLength", CanopyLength);
        return rig;
    }

    // An assembled scooter at the origin.
    Component Scooter()
    {
        Assert.IsNotNull(ScooterType, "EScooter is missing from Assembly-CSharp.");
        var go = new GameObject("EScooter");
        _made.Add(go);
        var scooter = go.AddComponent(ScooterType);
        Call(scooter, "Assemble");
        return scooter;
    }

    // A walker with the stock on-foot speeds on it. OnFootController needs a Rigidbody2D, which its
    // RequireComponent adds for us.
    Component Walker(Vector3 at)
    {
        Assert.IsNotNull(WalkerType, "OnFootController is missing from Assembly-CSharp.");
        var go = new GameObject("OnFootPlayer");
        _made.Add(go);
        go.transform.position = at;
        return go.AddComponent(WalkerType);
    }

    static Vector3 ParkingSpot(Component rig, float noseGap)
    {
        var m = SpawnerType.GetMethod("ParkingSpot", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(m, "EScooterSpawner.ParkingSpot() is gone; it is where the scooter parks.");
        return (Vector3)m.Invoke(null, new object[] { rig, noseGap });
    }

    // ---------------------------------------------------------------- where it parks

    [Test]
    public void TheScooterParksOffTheWalkwayEndOnTheCanopySide()
    {
        foreach (int side in new[] { 1, -1 })
        {
            var rig = Rig(side, Vector3.zero, Quaternion.identity);
            Vector3 local = rig.transform.InverseTransformPoint(ParkingSpot(rig, NoseGap));

            Assert.AreEqual(side * (BodyWidth + CanopyWidth) * 0.5f, local.x, 0.001f,
                $"canopySide {side}: the scooter is not parked in front of the open side of the garage.");
            Assert.AreEqual(BodyLength * 0.5f + NoseGap, local.y, 0.001f,
                $"canopySide {side}: the scooter is not parked off the cab end, which is the walkway side.");
        }
    }

    [Test]
    public void TheScooterIsClearOfTheShedAndTheCanopy()
    {
        var rig = Rig(1, Vector3.zero, Quaternion.identity);
        var scooter = Scooter();
        float scooterLength = (float)Field(scooter, "scooterLength");
        float scooterWidth = (float)Field(scooter, "scooterWidth");

        Vector3 local = rig.transform.InverseTransformPoint(ParkingSpot(rig, NoseGap));

        // The scooter is built nose-along-+Y and the rig's length runs the same way, so the tail of a parked
        // scooter is half a scooter length back toward the garage. That edge must still be past the shed.
        float tail = local.y - scooterLength * 0.5f;
        Assert.Greater(tail, BodyLength * 0.5f,
            "the tail of the parked scooter overlaps the garage shed — it reads as parked inside it.");
        Assert.Greater(tail, CanopyLength * 0.5f,
            "the tail of the parked scooter overlaps the canopy.");

        // And it must not be so far out that it is stood in the middle of the walkway between the rows.
        // PopupGarageLot leaves rowGap (7m by default) of open ground there.
        float intoWalkway = local.y + scooterLength * 0.5f - BodyLength * 0.5f;
        Assert.Less(intoWalkway, 7f,
            "the parked scooter reaches across the walkway into the row of garages behind it.");

        Assert.Greater(scooterWidth, 0.5f, "a scooter with bars narrower than a person is not something anybody can hold onto.");
    }

    [Test]
    public void TheDefaultGapFitsAWholeScooter()
    {
        var scooter = Scooter();
        float scooterLength = (float)Field(scooter, "scooterLength");
        float gap = SpawnerDefault("noseGap");

        Assert.Greater(gap, scooterLength * 0.5f,
            "EScooterSpawner.noseGap is smaller than half a scooter, so the default parking spot puts the " +
            "scooter through the end of the garage.");
    }

    [Test]
    public void TheParkingSpotFollowsAGarageThatIsTurnedRound()
    {
        // Paddocks are laid out off the player's RV rotation, so a rig is almost never axis-aligned.
        var rotation = Quaternion.Euler(0f, 0f, 37f);
        var rig = Rig(1, new Vector3(120f, -45f, 0f), rotation);

        Vector3 spot = ParkingSpot(rig, NoseGap);
        Vector3 local = rig.transform.InverseTransformPoint(spot);

        Assert.AreEqual((BodyWidth + CanopyWidth) * 0.5f, local.x, 0.001f, "the turned rig moved the scooter sideways.");
        Assert.AreEqual(BodyLength * 0.5f + NoseGap, local.y, 0.001f, "the turned rig moved the scooter along its length.");
        Assert.Greater(Vector2.Distance(spot, rig.transform.position), BodyLength * 0.4f,
            "the spot collapsed onto the rig's own position — the rotation was not applied.");
    }

    // ---------------------------------------------------------------- what it is made of

    [Test]
    public void TheScooterIsSpritesAndIsWalkThrough()
    {
        var scooter = Scooter();
        var go = ((Component)scooter).gameObject;

        Assert.GreaterOrEqual(go.GetComponentsInChildren<SpriteRenderer>(true).Length, 8,
            "the scooter is missing its wheels, deck, stem or handlebars.");
        Assert.AreEqual(0, go.GetComponentsInChildren<MeshRenderer>(true).Length,
            "the scooter is drawn with meshes; an opaque mesh in front of the ground plane hides the " +
            "sprite player stood on it (see PopupGarageRig's class comment).");
        Assert.AreEqual(0, go.GetComponentsInChildren<Collider2D>(true).Length,
            "the scooter is solid — the player steps off into its own footprint every time, and a solid " +
            "scooter at the garage mouth is something to get wedged on.");
    }

    [Test]
    public void AssemblingTwiceDoesNotBuildTwoScooters()
    {
        var scooter = Scooter();
        int parts = ((Component)scooter).gameObject.GetComponentsInChildren<SpriteRenderer>(true).Length;
        Call(scooter, "Assemble");
        Assert.AreEqual(parts, ((Component)scooter).gameObject.GetComponentsInChildren<SpriteRenderer>(true).Length,
            "a second Assemble() grew the scooter a second set of parts.");
    }

    // ---------------------------------------------------------------- riding it

    [Test]
    public void RidingSpeedsThePlayerUpAndSteppingOffHandsTheirLegsBack()
    {
        var scooter = Scooter();
        var walker = Walker(Vector3.zero);

        float walkSpeed = (float)Field(walker, "moveSpeed");
        float runMultiplier = (float)Field(walker, "runMultiplier");
        float rideSpeed = (float)Field(scooter, "rideSpeed");

        Assert.IsTrue((bool)Call(scooter, "Mount", walker), "the scooter refused a walker stood next to it.");
        Assert.IsTrue((bool)Prop(scooter, "Riding"), "the scooter does not believe it is being ridden.");
        Assert.AreEqual(rideSpeed, (float)Field(walker, "moveSpeed"), 0.001f, "riding did not speed the player up.");
        Assert.Greater(rideSpeed, walkSpeed, "the scooter is slower than walking, so nobody would ever use it.");
        Assert.AreEqual(1f, (float)Field(walker, "runMultiplier"), 0.001f,
            "the run modifier still multiplies scooter speed — holding shift on an e-scooter is not a thing.");

        Call(scooter, "Dismount");
        Assert.IsFalse((bool)Prop(scooter, "Riding"), "the scooter still thinks somebody is on it.");
        Assert.AreEqual(walkSpeed, (float)Field(walker, "moveSpeed"), 0.001f,
            "stepping off left the player walking at scooter speed.");
        Assert.AreEqual(runMultiplier, (float)Field(walker, "runMultiplier"), 0.001f,
            "stepping off left the player's run modifier flattened.");
    }

    [Test]
    public void TheScooterIsALittleSlowerButNimblerThanTheGolfCartItReplaced()
    {
        // The golf cart it replaced: 8 u/s flat out, 5 m/s² on the throttle, 200 deg/s of lock that only
        // had full bite at 2 u/s. The scooter trades a little top speed for agility on every other knob.
        const float CartTopSpeed = 8f, CartAccel = 5f, CartSteerRate = 200f, CartBiteSpeed = 2f;
        var scooter = Scooter();

        float rideSpeed = (float)Field(scooter, "rideSpeed");
        Assert.Less(rideSpeed, CartTopSpeed, "the scooter is as fast as the golf cart was.");
        Assert.Greater(rideSpeed, CartTopSpeed * 0.75f, "the scooter is a lot slower than the cart, not a little.");
        Assert.AreEqual(rideSpeed, SpawnerDefault("rideSpeed"), 0.001f,
            "the spawner overrides the scooter's top speed with a different one.");

        Assert.Greater((float)Field(scooter, "accelRate"), CartAccel, "the scooter is no quicker off the line.");
        Assert.Greater((float)Field(scooter, "steerRate"), CartSteerRate, "the scooter turns no tighter than the cart.");
        Assert.Less((float)Field(scooter, "steerBiteSpeed"), CartBiteSpeed,
            "the scooter's steering needs as much speed to bite as the cart's did.");
    }

    [Test]
    public void ARiddenScooterIsNeverATalkingOne()
    {
        // OnFootController zeroes movement for as long as the thing it is engaged with says it is talking.
        // A scooter that claimed a conversation would be a scooter that cannot be driven anywhere.
        var scooter = Scooter();
        var walker = Walker(Vector3.zero);

        Assert.IsFalse((bool)Prop(scooter, "IsTalking"), "a parked scooter claims to be mid-conversation.");
        Call(scooter, "Mount", walker);
        Assert.IsFalse((bool)Prop(scooter, "IsTalking"), "a ridden scooter claims to be mid-conversation.");
        Assert.IsFalse((bool)Call(scooter, "Interact"), "the scooter held the action button as an open conversation.");
    }

    [Test]
    public void TheActionButtonStepsOffAScooterYouAreRiding()
    {
        var scooter = Scooter();
        var walker = Walker(Vector3.zero);

        Call(scooter, "Mount", walker);
        Call(scooter, "Interact");

        Assert.IsFalse((bool)Prop(scooter, "Riding"), "pressing the action button while riding did not step off.");
    }

    [Test]
    public void SteppingOffLeavesTheScooterBesideThePlayerAndNotOnTopOfThem()
    {
        var scooter = Scooter();
        var walker = Walker(new Vector3(12f, -3f, 0f));

        Call(scooter, "Mount", walker);
        Call(scooter, "Dismount");

        float gap = (float)Field(scooter, "stepOffGap");
        float away = Vector2.Distance(((Component)scooter).transform.position, walker.transform.position);
        Assert.AreEqual(gap, away, 0.01f, "the scooter was not left at arm's length from whoever got out of it.");
    }

    [Test]
    public void OnlyOneScooterCanBeRiddenAtATime()
    {
        var first = Scooter();
        var second = Scooter();
        var walker = Walker(Vector3.zero);

        Assert.IsTrue((bool)Call(first, "Mount", walker));
        Assert.IsFalse((bool)Call(second, "Mount", walker),
            "a second scooter let the player on while they were already riding one — the first one's speed " +
            "would never be handed back.");

        Call(first, "Dismount");
        Assert.IsTrue((bool)Call(second, "Mount", walker), "nobody could get on a scooter once the first was empty.");
        Call(second, "Dismount");
    }

    [Test]
    public void AScooterDestroyedUnderTheRiderHandsTheirLegsBack()
    {
        var scooter = Scooter();
        var walker = Walker(Vector3.zero);
        float walkSpeed = (float)Field(walker, "moveSpeed");

        Call(scooter, "Mount", walker);

        // Edit mode does not run the MonoBehaviour messages, so the teardown Unity would fire on the way
        // out is invoked directly. What is being tested is that the handler hands the legs back at all.
        var onDestroy = ScooterType.GetMethod("OnDestroy", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(onDestroy, "EScooter no longer cleans up when it is destroyed.");
        onDestroy.Invoke(scooter, null);

        Assert.AreEqual(walkSpeed, (float)Field(walker, "moveSpeed"), 0.001f,
            "the scooter was destroyed out from under the rider and left them at scooter speed for good.");
    }

    [Test]
    public void TheScooterDoesNotHideTheTalkersOwnDisableHandler()
    {
        // NPCInteractable un-registers itself from All and puts its bubbles away in a private OnDisable.
        // Unity calls the most-derived message only, so a EScooter.OnDisable would silently stop the base
        // from ever running — leaving a destroyed scooter in the list every walker scans each frame.
        var declared = ScooterType.GetMethod("OnDisable",
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.IsNull(declared,
            "EScooter declares OnDisable, which hides NPCInteractable's own. Do the cleanup in OnDestroy.");
    }

    // ---------------------------------------------------------------- the randomly parked paddock scooter

    static bool PickRandomSpot(System.Random rng, IList<Rect> areas, System.Func<Vector2, bool> accept,
                               int attempts, out Vector2 spot)
    {
        var m = SpawnerType.GetMethod("PickRandomSpot", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(m, "EScooterSpawner.PickRandomSpot() is gone; it is where the paddock scooter parks.");
        var args = new object[] { rng, areas, accept, attempts, null };
        bool ok = (bool)m.Invoke(null, args);
        spot = (Vector2)args[4];
        return ok;
    }

    [Test]
    public void ThePaddockScooterParksSomewhereAcceptedInsideTheAreas()
    {
        var areas = new List<Rect> { new Rect(0f, 0f, 40f, 20f), new Rect(100f, 100f, 10f, 10f) };
        // A garage-shaped hole in the middle of the big area: the scooter must never land in it.
        var hole = new Rect(10f, 5f, 20f, 10f);
        System.Func<Vector2, bool> clear = p => !hole.Contains(p);

        for (int seed = 0; seed < 200; seed++)
        {
            Assert.IsTrue(PickRandomSpot(new System.Random(seed), areas, clear, 200, out var spot),
                $"seed {seed}: found nowhere to park with most of the paddock clear.");
            Assert.IsTrue(areas[0].Contains(spot) || areas[1].Contains(spot),
                $"seed {seed}: scooter parked at {spot}, outside every paddock area.");
            Assert.IsFalse(hole.Contains(spot), $"seed {seed}: scooter parked at {spot}, inside the blocked ground.");
        }
    }

    [Test]
    public void ThePaddockScooterIsNotAlwaysInTheSamePlace()
    {
        var areas = new List<Rect> { new Rect(-50f, -30f, 100f, 60f) };
        var spots = new HashSet<Vector2Int>();
        for (int seed = 0; seed < 20; seed++)
        {
            PickRandomSpot(new System.Random(seed), areas, null, 10, out var spot);
            spots.Add(Vector2Int.RoundToInt(spot));
        }
        Assert.Greater(spots.Count, 10, "twenty loads parked the paddock scooter in barely any different spots.");
    }

    [Test]
    public void ThePaddockScooterSpreadsOverTheAreasByTheirSize()
    {
        // One area nine times the size of the other should catch roughly nine in ten scooters.
        var areas = new List<Rect> { new Rect(0f, 0f, 30f, 30f), new Rect(100f, 0f, 10f, 10f) };
        var rng = new System.Random(1234);
        int big = 0;
        const int Loads = 2000;
        for (int i = 0; i < Loads; i++)
        {
            PickRandomSpot(rng, areas, null, 1, out var spot);
            if (areas[0].Contains(spot)) big++;
        }
        Assert.That(big / (float)Loads, Is.InRange(0.85f, 0.95f),
            "scooters are not spread over the paddock by area — a sliver of a lot gets as many as the paddock.");
    }

    [Test]
    public void ThePaddockScooterGivesUpWhenNothingIsClear()
    {
        var areas = new List<Rect> { new Rect(0f, 0f, 10f, 10f) };
        Assert.IsFalse(PickRandomSpot(new System.Random(7), areas, _ => false, 50, out _),
            "a paddock with no clear ground still got a scooter parked in it.");
        Assert.IsFalse(PickRandomSpot(new System.Random(7), new List<Rect>(), null, 50, out _),
            "a paddock with no areas at all still got a scooter parked in it.");
    }
}
