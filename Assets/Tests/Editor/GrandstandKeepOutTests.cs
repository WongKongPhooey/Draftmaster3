using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// The crowd stays off the grandstands.
//
// A stand is one flat quad with a crowd painted into the texture (Grandstand), so the ground under it is
// clear tarmac as far as the physics world is concerned. The paddock crowd routes around what physics can
// see (PaddockObstacles), and a paddock rectangle is derived from the pit lane without regard for what is
// inside it — so wherever the two overlap, people wandered up the seating and stood about in the middle of
// the painted crowd.
//
// The stand now lays its own keep-out volume. These cover what can be checked without a play mode: that
// the footprint reads as blocked, that it is a TRIGGER (so the player can still walk in and sit down to
// watch a session), that a walker's own step planning refuses to cross into it, and that the volume is
// laid once however many times the stand is rebuilt.
//
// Assembly-CSharp cannot be referenced from an asmdef, so the types are reached by reflection — the same
// way PaddockObstacleTests reaches the router.
public class GrandstandKeepOutTests
{
    static readonly System.Type StandType = System.Type.GetType("Grandstand, Assembly-CSharp");
    static readonly System.Type ObstaclesType = System.Type.GetType("PaddockObstacles, Assembly-CSharp");
    static readonly System.Type NoGoType = System.Type.GetType("PaddockNoGo, Assembly-CSharp");
    static readonly System.Type DirectorType = System.Type.GetType("CrowdDirector, Assembly-CSharp");

    const float StandLength = 110f;
    const float StandDepth = 14f;
    const float WalkerRadius = 0.45f;

    readonly List<GameObject> _spawned = new();

    [SetUp]
    public void SetUp()
    {
        Assert.NotNull(StandType, "Grandstand is missing from Assembly-CSharp.");
        Assert.NotNull(ObstaclesType, "PaddockObstacles is missing from Assembly-CSharp.");
        Assert.NotNull(NoGoType, "PaddockNoGo is missing from Assembly-CSharp.");
        ForgetCache();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < _spawned.Count; i++)
            if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
        _spawned.Clear();
        ForgetCache();
        Physics2D.SyncTransforms();
    }

    // --- fixtures ---------------------------------------------------------------------------------

    // A stand the size TrackDressingFactory builds, laid along +X at the origin with its keep-out down.
    // BuildKeepOut is called by hand because Build() only lays one in play mode — an edit-time volume
    // would be written into all 38 track packages the next time each was opened and saved.
    GameObject Stand(Vector2 centre, float rotationDeg = 0f, bool keepCrowdOff = true)
    {
        var go = new GameObject("Grandstand_Test");
        go.transform.position = centre;
        go.transform.rotation = Quaternion.Euler(0f, 0f, rotationDeg);
        _spawned.Add(go);

        var stand = go.AddComponent(StandType);
        SetField(stand, "length", StandLength);
        SetField(stand, "depth", StandDepth);
        SetField(stand, "keepCrowdOff", keepCrowdOff);
        Call(stand, "BuildKeepOut");

        Physics2D.SyncTransforms();
        return go;
    }

    // --- reflection wrappers ----------------------------------------------------------------------

    static void SetField(object o, string name, object value)
    {
        var f = o.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(f, o.GetType().Name + "." + name + " is gone.");
        f.SetValue(o, value);
    }

    static object Call(object o, string name, params object[] args)
    {
        var m = o.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(m, o.GetType().Name + "." + name + "() is gone.");
        return m.Invoke(o, args);
    }

    static void ForgetCache() =>
        ObstaclesType?.GetMethod("ForgetCache", BindingFlags.Public | BindingFlags.Static)
                     ?.Invoke(null, null);

    static bool IsBlocked(Vector2 p, float radius) =>
        (bool)ObstaclesType.GetMethod("IsBlocked", BindingFlags.Public | BindingFlags.Static)
                           .Invoke(null, new object[] { p, radius });

    static bool TryStep(Vector2 from, Vector2 to, float radius, out Vector2 result)
    {
        var args = new object[] { from, to, radius, Vector2.zero };
        bool ok = (bool)ObstaclesType.GetMethod("TryStep", BindingFlags.Public | BindingFlags.Static)
                                     .Invoke(null, args);
        result = (Vector2)args[3];
        return ok;
    }

    static string KeepOutName =>
        (string)StandType.GetField("KeepOutName", BindingFlags.Public | BindingFlags.Static).GetValue(null);

    // --- the seating is closed ground -------------------------------------------------------------

    [Test]
    public void SeatingReadsAsBlocked()
    {
        Stand(Vector2.zero);

        Assert.IsTrue(IsBlocked(Vector2.zero, WalkerRadius),
            "The middle of a grandstand is open ground — the crowd will wander up the seating.");
        Assert.IsTrue(IsBlocked(new Vector2(StandLength * 0.4f, StandDepth * 0.4f), WalkerRadius),
            "A corner of the stand's footprint is open ground.");
    }

    [Test]
    public void GroundInFrontOfTheStandIsStillOpen()
    {
        Stand(Vector2.zero);

        Assert.IsFalse(IsBlocked(new Vector2(0f, -StandDepth), WalkerRadius),
            "The keep-out reaches past the stand's own footprint and has closed the ground in front of it.");
        Assert.IsFalse(IsBlocked(new Vector2(StandLength, 0f), WalkerRadius),
            "The keep-out reaches past the end of the stand.");
    }

    [Test]
    public void TheVolumeTurnsWithTheStand()
    {
        // Stands are rotated to run along whichever straight they sit on, so the footprint has to turn with
        // them: at 90 degrees the length now runs up the screen rather than across it.
        Stand(Vector2.zero, rotationDeg: 90f);

        Assert.IsTrue(IsBlocked(new Vector2(0f, StandLength * 0.4f), WalkerRadius),
            "A turned stand's keep-out did not turn with it.");
        Assert.IsFalse(IsBlocked(new Vector2(StandLength * 0.4f, 0f), WalkerRadius),
            "A turned stand is still closing the ground its unrotated footprint used to cover.");
    }

    // --- and open to the player --------------------------------------------------------------------

    [Test]
    public void TheKeepOutIsATriggerSoThePlayerCanStillSitDown()
    {
        var go = Stand(Vector2.zero);
        var volume = go.transform.Find(KeepOutName);
        Assert.IsNotNull(volume, "A stand laid no '" + KeepOutName + "' volume.");

        var box = volume.GetComponent<BoxCollider2D>();
        Assert.IsNotNull(box, "The keep-out has no collider on it.");
        Assert.IsTrue(box.isTrigger,
            "The keep-out is solid, which shuts the player out of the stand they are meant to watch from.");
        Assert.IsNotNull(volume.GetComponent(NoGoType),
            "The keep-out carries no PaddockNoGo, so the crowd's router reads it as bookkeeping and walks in.");
        Assert.AreEqual(new Vector2(StandLength, StandDepth), box.size,
            "The keep-out is not the size of the stand.");
    }

    // --- a walker will not step into it -------------------------------------------------------------

    [Test]
    public void AWalkerCannotStepOntoTheSeating()
    {
        Stand(Vector2.zero);

        // Straight at the front row from a metre outside it.
        Vector2 from = new(0f, -StandDepth * 0.5f - 1f);
        Vector2 to = new(0f, -StandDepth * 0.5f + 0.5f);

        TryStep(from, to, WalkerRadius, out Vector2 result);
        Assert.IsFalse(IsBlocked(result, WalkerRadius),
            "A walker stepped onto the seating instead of being turned along the front of the stand.");
    }

    // --- rebuilt, not duplicated ---------------------------------------------------------------------

    [Test]
    public void RebuildingLaysOneVolumeAndResizesIt()
    {
        var go = Stand(Vector2.zero);
        var stand = go.GetComponent(StandType);

        SetField(stand, "length", StandLength * 0.5f);
        Call(stand, "BuildKeepOut");

        int volumes = 0;
        foreach (Transform child in go.transform)
            if (child.name == KeepOutName) volumes++;
        Assert.AreEqual(1, volumes, "Rebuilding a stand stacked up a second keep-out volume.");

        var box = go.transform.Find(KeepOutName).GetComponent<BoxCollider2D>();
        Assert.AreEqual(StandLength * 0.5f, box.size.x, 0.001f,
            "A resized stand kept its old keep-out footprint.");
    }

    [Test]
    public void TurningTheKeepOutOffOpensTheStandUp()
    {
        var go = Stand(Vector2.zero, keepCrowdOff: false);

        Assert.IsNull(go.transform.Find(KeepOutName),
            "keepCrowdOff is off and the stand laid a keep-out anyway.");
        Assert.IsFalse(IsBlocked(Vector2.zero, WalkerRadius),
            "A stand with the keep-out turned off is still closed to the crowd.");
    }

    // --- and nobody is dropped onto one ---------------------------------------------------------------

    [Test]
    public void RecycledCrowdIsGivenClearGroundToLandOn()
    {
        // The director puts drifting filler back down near the player. It used to ask only whether the spot
        // was inside the walkable boundary, which the seating of a stand inside the paddock is — so people
        // were dropped onto it and had to walk back out. Structural, because exercising the recycle itself
        // needs a play mode and an on-foot player.
        Assert.NotNull(DirectorType, "CrowdDirector is missing from Assembly-CSharp.");
        var f = DirectorType.GetField("recycleClearance", BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(f, "CrowdDirector.recycleClearance is gone; recycled NPCs can land inside scenery again.");

        var go = new GameObject("CrowdDirector_Test");
        _spawned.Add(go);
        var director = go.AddComponent(DirectorType);
        Assert.Greater((float)f.GetValue(director), 0f,
            "The default recycle clearance is 0, which skips the check entirely.");
    }
}
