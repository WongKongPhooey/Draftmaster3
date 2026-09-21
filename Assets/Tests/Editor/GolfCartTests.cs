using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// EditMode coverage for the paddock golf cart — the runabout GolfCartSpawner parks at the mouth of the
// player's own team garage, and the ride that happens when somebody presses the action button next to it.
//
// None of this can be checked by looking at a scene: the cart is built at play time, against a garage row
// that is itself generated from the live roster. So these tests stand a rig up the way PopupGarageLot
// does, ask the spawner where a cart goes against it, and measure what came out — and then mount a cart
// on a walker and check the player is handed back exactly the speed they had before.
//
// The two that matter:
//   * the parking spot clears the shed AND a full cart length fits in the gap, or the cart is drawn
//     through the garage it is supposed to be parked outside;
//   * dismounting restores moveSpeed and runMultiplier, or stepping off leaves the player permanently
//     sprinting around the paddock at cart speed.
//
// GolfCart and GolfCartSpawner live in Assembly-CSharp, which an asmdef can't reference, so they're
// reached by reflection — the same way PopupGarageTests reaches the garages.
public class GolfCartTests
{
    static readonly System.Type CartType = System.Type.GetType("GolfCart, Assembly-CSharp");
    static readonly System.Type SpawnerType = System.Type.GetType("GolfCartSpawner, Assembly-CSharp");
    static readonly System.Type RigType = System.Type.GetType("PopupGarageRig, Assembly-CSharp");
    static readonly System.Type WalkerType = System.Type.GetType("OnFootController, Assembly-CSharp");

    // What PopupGarageLot hands each rig, restated here so a silent change to a default shows up as a
    // failing test rather than as a cart that quietly moved.
    const float BodyWidth = 3.95f;
    const float BodyLength = 9.93f;
    const float CanopyWidth = 6.5f;
    const float CanopyLength = 7.2f;

    // The spawner's own default gap past the end of the rig.
    const float NoseGap = 1.6f;

    readonly List<GameObject> _made = new();

    [SetUp]
    public void Reset() => ForgetRiddenCart();

    [TearDown]
    public void CleanUp()
    {
        foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
        _made.Clear();
        ForgetRiddenCart();
    }

    // "Who is sat in a cart" is a static, and edit mode keeps statics between fixtures — a test that
    // mounts and is then torn down would otherwise leave the next one unable to get into anything.
    static void ForgetRiddenCart()
    {
        var p = CartType?.GetProperty("Ridden", BindingFlags.Public | BindingFlags.Static);
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
        Assert.IsNotNull(SpawnerType, "GolfCartSpawner is missing from Assembly-CSharp.");
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

    // An assembled cart at the origin.
    Component Cart()
    {
        Assert.IsNotNull(CartType, "GolfCart is missing from Assembly-CSharp.");
        var go = new GameObject("GolfCart");
        _made.Add(go);
        var cart = go.AddComponent(CartType);
        Call(cart, "Assemble");
        return cart;
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
        Assert.IsNotNull(m, "GolfCartSpawner.ParkingSpot() is gone; it is where the cart parks.");
        return (Vector3)m.Invoke(null, new object[] { rig, noseGap });
    }

    // ---------------------------------------------------------------- where it parks

    [Test]
    public void TheCartParksOffTheWalkwayEndOnTheCanopySide()
    {
        foreach (int side in new[] { 1, -1 })
        {
            var rig = Rig(side, Vector3.zero, Quaternion.identity);
            Vector3 local = rig.transform.InverseTransformPoint(ParkingSpot(rig, NoseGap));

            Assert.AreEqual(side * (BodyWidth + CanopyWidth) * 0.5f, local.x, 0.001f,
                $"canopySide {side}: the cart is not parked in front of the open side of the garage.");
            Assert.AreEqual(BodyLength * 0.5f + NoseGap, local.y, 0.001f,
                $"canopySide {side}: the cart is not parked off the cab end, which is the walkway side.");
        }
    }

    [Test]
    public void TheCartIsClearOfTheShedAndTheCanopy()
    {
        var rig = Rig(1, Vector3.zero, Quaternion.identity);
        var cart = Cart();
        float cartLength = (float)Field(cart, "cartLength");
        float cartWidth = (float)Field(cart, "cartWidth");

        Vector3 local = rig.transform.InverseTransformPoint(ParkingSpot(rig, NoseGap));

        // The cart is built nose-along-+Y and the rig's length runs the same way, so the tail of a parked
        // cart is half a cart length back toward the garage. That edge must still be past the shed.
        float tail = local.y - cartLength * 0.5f;
        Assert.Greater(tail, BodyLength * 0.5f,
            "the tail of the parked cart overlaps the garage shed — it reads as parked inside it.");
        Assert.Greater(tail, CanopyLength * 0.5f,
            "the tail of the parked cart overlaps the canopy.");

        // And it must not be so far out that it is stood in the middle of the walkway between the rows.
        // PopupGarageLot leaves rowGap (7m by default) of open ground there.
        float intoWalkway = local.y + cartLength * 0.5f - BodyLength * 0.5f;
        Assert.Less(intoWalkway, 7f,
            "the parked cart reaches across the walkway into the row of garages behind it.");

        Assert.Greater(cartWidth, 0.5f, "a cart narrower than a person is not a cart.");
    }

    [Test]
    public void TheDefaultGapFitsAWholeCart()
    {
        var cart = Cart();
        float cartLength = (float)Field(cart, "cartLength");
        float gap = SpawnerDefault("noseGap");

        Assert.Greater(gap, cartLength * 0.5f,
            "GolfCartSpawner.noseGap is smaller than half a cart, so the default parking spot puts the " +
            "cart through the end of the garage.");
    }

    [Test]
    public void TheParkingSpotFollowsAGarageThatIsTurnedRound()
    {
        // Paddocks are laid out off the player's RV rotation, so a rig is almost never axis-aligned.
        var rotation = Quaternion.Euler(0f, 0f, 37f);
        var rig = Rig(1, new Vector3(120f, -45f, 0f), rotation);

        Vector3 spot = ParkingSpot(rig, NoseGap);
        Vector3 local = rig.transform.InverseTransformPoint(spot);

        Assert.AreEqual((BodyWidth + CanopyWidth) * 0.5f, local.x, 0.001f, "the turned rig moved the cart sideways.");
        Assert.AreEqual(BodyLength * 0.5f + NoseGap, local.y, 0.001f, "the turned rig moved the cart along its length.");
        Assert.Greater(Vector2.Distance(spot, rig.transform.position), BodyLength * 0.4f,
            "the spot collapsed onto the rig's own position — the rotation was not applied.");
    }

    // ---------------------------------------------------------------- what it is made of

    [Test]
    public void TheCartIsSpritesAndIsWalkThrough()
    {
        var cart = Cart();
        var go = ((Component)cart).gameObject;

        Assert.GreaterOrEqual(go.GetComponentsInChildren<SpriteRenderer>(true).Length, 8,
            "the cart is missing its body, wheels, seat or roof.");
        Assert.AreEqual(0, go.GetComponentsInChildren<MeshRenderer>(true).Length,
            "the cart is drawn with meshes; an opaque mesh in front of the ground plane hides the " +
            "sprite player stood on it (see PopupGarageRig's class comment).");
        Assert.AreEqual(0, go.GetComponentsInChildren<Collider2D>(true).Length,
            "the cart is solid — the player steps off into its own footprint every time, and a solid " +
            "cart at the garage mouth is something to get wedged on.");
    }

    [Test]
    public void AssemblingTwiceDoesNotBuildTwoCarts()
    {
        var cart = Cart();
        int parts = ((Component)cart).gameObject.GetComponentsInChildren<SpriteRenderer>(true).Length;
        Call(cart, "Assemble");
        Assert.AreEqual(parts, ((Component)cart).gameObject.GetComponentsInChildren<SpriteRenderer>(true).Length,
            "a second Assemble() grew the cart a second set of parts.");
    }

    // ---------------------------------------------------------------- riding it

    [Test]
    public void RidingSpeedsThePlayerUpAndSteppingOffHandsTheirLegsBack()
    {
        var cart = Cart();
        var walker = Walker(Vector3.zero);

        float walkSpeed = (float)Field(walker, "moveSpeed");
        float runMultiplier = (float)Field(walker, "runMultiplier");
        float rideSpeed = (float)Field(cart, "rideSpeed");

        Assert.IsTrue((bool)Call(cart, "Mount", walker), "the cart refused a walker stood next to it.");
        Assert.IsTrue((bool)Prop(cart, "Riding"), "the cart does not believe it is being ridden.");
        Assert.AreEqual(rideSpeed, (float)Field(walker, "moveSpeed"), 0.001f, "riding did not speed the player up.");
        Assert.Greater(rideSpeed, walkSpeed, "the cart is slower than walking, so nobody would ever use it.");
        Assert.AreEqual(1f, (float)Field(walker, "runMultiplier"), 0.001f,
            "the run modifier still multiplies cart speed — holding shift in a golf cart is not a thing.");

        Call(cart, "Dismount");
        Assert.IsFalse((bool)Prop(cart, "Riding"), "the cart still thinks somebody is in it.");
        Assert.AreEqual(walkSpeed, (float)Field(walker, "moveSpeed"), 0.001f,
            "stepping off left the player walking at cart speed.");
        Assert.AreEqual(runMultiplier, (float)Field(walker, "runMultiplier"), 0.001f,
            "stepping off left the player's run modifier flattened.");
    }

    [Test]
    public void ARiddenCartIsNeverATalkingOne()
    {
        // OnFootController zeroes movement for as long as the thing it is engaged with says it is talking.
        // A cart that claimed a conversation would be a cart that cannot be driven anywhere.
        var cart = Cart();
        var walker = Walker(Vector3.zero);

        Assert.IsFalse((bool)Prop(cart, "IsTalking"), "a parked cart claims to be mid-conversation.");
        Call(cart, "Mount", walker);
        Assert.IsFalse((bool)Prop(cart, "IsTalking"), "a ridden cart claims to be mid-conversation.");
        Assert.IsFalse((bool)Call(cart, "Interact"), "the cart held the action button as an open conversation.");
    }

    [Test]
    public void TheActionButtonStepsOffACartYouAreSatIn()
    {
        var cart = Cart();
        var walker = Walker(Vector3.zero);

        Call(cart, "Mount", walker);
        Call(cart, "Interact");

        Assert.IsFalse((bool)Prop(cart, "Riding"), "pressing the action button while riding did not step off.");
    }

    [Test]
    public void SteppingOffLeavesTheCartBesideThePlayerAndNotOnTopOfThem()
    {
        var cart = Cart();
        var walker = Walker(new Vector3(12f, -3f, 0f));

        Call(cart, "Mount", walker);
        Call(cart, "Dismount");

        float gap = (float)Field(cart, "stepOffGap");
        float away = Vector2.Distance(((Component)cart).transform.position, walker.transform.position);
        Assert.AreEqual(gap, away, 0.01f, "the cart was not left at arm's length from whoever got out of it.");
    }

    [Test]
    public void OnlyOneCartCanBeRiddenAtATime()
    {
        var first = Cart();
        var second = Cart();
        var walker = Walker(Vector3.zero);

        Assert.IsTrue((bool)Call(first, "Mount", walker));
        Assert.IsFalse((bool)Call(second, "Mount", walker),
            "a second cart let the player in while they were already sat in one — the first one's speed " +
            "would never be handed back.");

        Call(first, "Dismount");
        Assert.IsTrue((bool)Call(second, "Mount", walker), "nobody could get into a cart once the first was empty.");
        Call(second, "Dismount");
    }

    [Test]
    public void ACartDestroyedUnderTheRiderHandsTheirLegsBack()
    {
        var cart = Cart();
        var walker = Walker(Vector3.zero);
        float walkSpeed = (float)Field(walker, "moveSpeed");

        Call(cart, "Mount", walker);

        // Edit mode does not run the MonoBehaviour messages, so the teardown Unity would fire on the way
        // out is invoked directly. What is being tested is that the handler hands the legs back at all.
        var onDestroy = CartType.GetMethod("OnDestroy", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(onDestroy, "GolfCart no longer cleans up when it is destroyed.");
        onDestroy.Invoke(cart, null);

        Assert.AreEqual(walkSpeed, (float)Field(walker, "moveSpeed"), 0.001f,
            "the cart was destroyed out from under the rider and left them at cart speed for good.");
    }

    [Test]
    public void TheCartDoesNotHideTheTalkersOwnDisableHandler()
    {
        // NPCInteractable un-registers itself from All and puts its bubbles away in a private OnDisable.
        // Unity calls the most-derived message only, so a GolfCart.OnDisable would silently stop the base
        // from ever running — leaving a destroyed cart in the list every walker scans each frame.
        var declared = CartType.GetMethod("OnDisable",
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.IsNull(declared,
            "GolfCart declares OnDisable, which hides NPCInteractable's own. Do the cleanup in OnDestroy.");
    }

    // ---------------------------------------------------------------- the randomly parked paddock cart

    static bool PickRandomSpot(System.Random rng, IList<Rect> areas, System.Func<Vector2, bool> accept,
                               int attempts, out Vector2 spot)
    {
        var m = SpawnerType.GetMethod("PickRandomSpot", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(m, "GolfCartSpawner.PickRandomSpot() is gone; it is where the paddock cart parks.");
        var args = new object[] { rng, areas, accept, attempts, null };
        bool ok = (bool)m.Invoke(null, args);
        spot = (Vector2)args[4];
        return ok;
    }

    [Test]
    public void ThePaddockCartParksSomewhereAcceptedInsideTheAreas()
    {
        var areas = new List<Rect> { new Rect(0f, 0f, 40f, 20f), new Rect(100f, 100f, 10f, 10f) };
        // A garage-shaped hole in the middle of the big area: the cart must never land in it.
        var hole = new Rect(10f, 5f, 20f, 10f);
        System.Func<Vector2, bool> clear = p => !hole.Contains(p);

        for (int seed = 0; seed < 200; seed++)
        {
            Assert.IsTrue(PickRandomSpot(new System.Random(seed), areas, clear, 200, out var spot),
                $"seed {seed}: found nowhere to park with most of the paddock clear.");
            Assert.IsTrue(areas[0].Contains(spot) || areas[1].Contains(spot),
                $"seed {seed}: cart parked at {spot}, outside every paddock area.");
            Assert.IsFalse(hole.Contains(spot), $"seed {seed}: cart parked at {spot}, inside the blocked ground.");
        }
    }

    [Test]
    public void ThePaddockCartIsNotAlwaysInTheSamePlace()
    {
        var areas = new List<Rect> { new Rect(-50f, -30f, 100f, 60f) };
        var spots = new HashSet<Vector2Int>();
        for (int seed = 0; seed < 20; seed++)
        {
            PickRandomSpot(new System.Random(seed), areas, null, 10, out var spot);
            spots.Add(Vector2Int.RoundToInt(spot));
        }
        Assert.Greater(spots.Count, 10, "twenty loads parked the paddock cart in barely any different spots.");
    }

    [Test]
    public void ThePaddockCartSpreadsOverTheAreasByTheirSize()
    {
        // One area nine times the size of the other should catch roughly nine in ten carts.
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
            "carts are not spread over the paddock by area — a sliver of a lot gets as many as the paddock.");
    }

    [Test]
    public void ThePaddockCartGivesUpWhenNothingIsClear()
    {
        var areas = new List<Rect> { new Rect(0f, 0f, 10f, 10f) };
        Assert.IsFalse(PickRandomSpot(new System.Random(7), areas, _ => false, 50, out _),
            "a paddock with no clear ground still got a cart parked in it.");
        Assert.IsFalse(PickRandomSpot(new System.Random(7), new List<Rect>(), null, 50, out _),
            "a paddock with no areas at all still got a cart parked in it.");
    }
}
