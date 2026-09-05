using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// Paddock NPCs walk round the motorhomes and the popup garages instead of through them.
//
// The player was never able to walk through a motorhome — they are a dynamic Rigidbody2D and the lot puts
// down plain static BoxCollider2Ds. A paddock walker is a KINEMATIC body driven by MovePosition, which no
// collider stops, so the crowd strolled through the side of every rig in the lot.
//
// PaddockObstacles is the fix: ask the physics world what solid, non-trigger scenery is standing on a
// patch of ground, and let PaddockWalker steer round it. These cover the two halves that can be checked
// without a play mode — that the question has honest answers (a motorhome blocks, a person does not), and
// that a walker's own route planning honours them.
//
// Assembly-CSharp cannot be referenced from an asmdef, so the types are reached by reflection the same
// way PaddockReachTests reaches the boundary.
public class PaddockObstacleTests
{
    static readonly System.Type ObstaclesType = System.Type.GetType("PaddockObstacles, Assembly-CSharp");
    static readonly System.Type WalkerType = System.Type.GetType("PaddockWalker, Assembly-CSharp");
    static readonly System.Type AppearanceType = System.Type.GetType("NPCLayeredAppearance, Assembly-CSharp");
    static readonly System.Type NoGoType = System.Type.GetType("PaddockNoGo, Assembly-CSharp");

    readonly List<GameObject> _spawned = new();

    [SetUp]
    public void SetUp()
    {
        Assert.NotNull(ObstaclesType, "PaddockObstacles is missing from Assembly-CSharp.");
        Assert.NotNull(WalkerType, "PaddockWalker is missing from Assembly-CSharp.");
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

    // A parked motorhome: a solid box, exactly what DriverMotorhomeLot.BuildMotorhome puts down.
    GameObject Motorhome(Vector2 centre, Vector2 size)
    {
        var go = new GameObject("RV_Test");
        go.transform.position = centre;
        go.AddComponent<BoxCollider2D>().size = size;
        _spawned.Add(go);

        // Edit mode never steps the physics world on its own, and the overlap queries ask that world
        // rather than the collider's serialised numbers. Without this nothing is ever in the way.
        Physics2D.SyncTransforms();
        return go;
    }

    // A popup garage's floor: a trigger over the footprint, marked keep-out. Open to physics — the player
    // walks in through the notch in the shell — and closed to anybody who wanders.
    GameObject KeepOut(Vector2 centre, Vector2 size)
    {
        Assert.NotNull(NoGoType, "PaddockNoGo is missing from Assembly-CSharp.");

        var go = new GameObject("NoWandering_Test");
        go.transform.position = centre;
        var box = go.AddComponent<BoxCollider2D>();
        box.size = size;
        box.isTrigger = true;
        go.AddComponent(NoGoType);
        _spawned.Add(go);

        Physics2D.SyncTransforms();
        return go;
    }

    // Somebody standing about: solid too, but a person, not scenery.
    GameObject Person(Vector2 at)
    {
        var go = new GameObject("PaddockNPC_Test");
        go.transform.position = at;
        go.AddComponent<BoxCollider2D>().size = new Vector2(0.6f, 0.4f);
        go.AddComponent(AppearanceType);
        _spawned.Add(go);
        Physics2D.SyncTransforms();
        return go;
    }

    // --- reflection wrappers ----------------------------------------------------------------------

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

    static Vector2 PushOut(Vector2 p, float radius, float maxDistance = 12f) =>
        (Vector2)ObstaclesType.GetMethod("PushOut", BindingFlags.Public | BindingFlags.Static)
                              .Invoke(null, new object[] { p, radius, maxDistance });

    // --- what counts as in the way ----------------------------------------------------------------

    [Test]
    public void OpenGroundIsNotBlocked()
    {
        Assert.IsFalse(IsBlocked(new Vector2(500f, -500f), 0.45f),
                       "Empty tarmac was reported as blocked; the whole crowd would stand still.");
    }

    [Test]
    public void AParkedMotorhomeBlocksTheGroundItStandsOn()
    {
        Motorhome(new Vector2(0f, 0f), new Vector2(3f, 10f));

        Assert.IsTrue(IsBlocked(Vector2.zero, 0.45f), "The middle of a motorhome should be blocked.");
        Assert.IsTrue(IsBlocked(new Vector2(1.4f, 2f), 0.45f), "Its flank should be blocked.");
        Assert.IsFalse(IsBlocked(new Vector2(6f, 0f), 0.45f), "The aisle beside it should be walkable.");
    }

    [Test]
    public void PeopleAreNotScenery()
    {
        // Walkers bump into each other constantly (PaddockWalker.Bumped handles it: stop, look, walk on).
        // Treating a body as a wall would seize the crowd solid the moment it packed together.
        Person(new Vector2(0f, 0f));

        Assert.IsFalse(IsBlocked(Vector2.zero, 0.45f), "A person was treated as a wall.");
    }

    [Test]
    public void TriggersAreNotWalls()
    {
        // The paddock boundary, the lot areas and every interaction range are triggers. Honouring them
        // would pin the crowd inside its own bookkeeping.
        var go = Motorhome(new Vector2(0f, 0f), new Vector2(20f, 20f));
        go.GetComponent<BoxCollider2D>().isTrigger = true;
        ForgetCache();
        Physics2D.SyncTransforms();

        Assert.IsFalse(IsBlocked(Vector2.zero, 0.45f), "A trigger volume was treated as solid.");
    }

    // The exception, and the only one: ground that states it is keep-out. A popup garage's shell is a ring
    // of walls with the doorway cut out of it — the player has to be able to walk in — so the floor inside
    // is open ground as far as the physics world knows, and the crowd wandered in and stood about in it.
    // Nothing solid can say that without shutting the player out, so a PaddockNoGo trigger says it instead.
    [Test]
    public void GroundMarkedKeepOutIsAWallToTheCrowd()
    {
        KeepOut(new Vector2(0f, 0f), new Vector2(4f, 10f));

        Assert.IsTrue(IsBlocked(Vector2.zero, 0.45f),
                      "A keep-out volume let the crowd stand in it — that is the floor of somebody's garage.");
        Assert.IsFalse(IsBlocked(new Vector2(6f, 0f), 0.45f),
                       "The ground beside it was closed off too; the crowd can't walk the row.");
    }

    [Test]
    public void ADisabledColliderIsNotInTheWay()
    {
        // RVInterior switches the shell colliders off while the player is in the room.
        var go = Motorhome(new Vector2(0f, 0f), new Vector2(3f, 10f));
        Assert.IsTrue(IsBlocked(Vector2.zero, 0.45f));

        go.GetComponent<BoxCollider2D>().enabled = false;
        Physics2D.SyncTransforms();

        Assert.IsFalse(IsBlocked(Vector2.zero, 0.45f), "A switched-off collider still blocked the ground.");
    }

    // --- stepping round it ------------------------------------------------------------------------

    [Test]
    public void AClearStepIsTakenExactlyAsAsked()
    {
        Motorhome(new Vector2(0f, 0f), new Vector2(3f, 10f));

        var from = new Vector2(20f, 0f);
        var to = new Vector2(20.2f, 0f);
        Assert.IsTrue(TryStep(from, to, 0.45f, out Vector2 result));
        Assert.AreEqual(to.x, result.x, 0.0001f);
        Assert.AreEqual(to.y, result.y, 0.0001f);
    }

    [Test]
    public void AStepIntoAMotorhomeNeverEndsUpInsideIt()
    {
        // Walking due west into the flank of a rig that runs north-south. Whatever the walker does with
        // the step, the one thing it must not do is finish inside the bodywork.
        Motorhome(new Vector2(0f, 0f), new Vector2(3f, 10f));

        var from = new Vector2(2.2f, 1f);            // just clear of the east flank at x = 1.5
        var to = from + new Vector2(-0.5f, 0f);      // a step that would end up inside

        TryStep(from, to, 0.45f, out Vector2 result);

        Assert.IsFalse(IsBlocked(result, 0.45f), "The walker was left standing inside the motorhome.");
        Assert.Greater(result.x, 1.5f, "The walker ended up past the flank of the rig.");
    }

    [Test]
    public void AStepAlongAFlankSlidesRatherThanStopping()
    {
        // A diagonal into the same flank: the part of the step running into the panel is thrown away and
        // the part running along it is kept, so the walker slides down the side instead of stopping dead.
        Motorhome(new Vector2(0f, 0f), new Vector2(3f, 10f));

        var from = new Vector2(2.0f, 0f);
        var to = from + new Vector2(-0.4f, 0.4f);

        Assert.IsTrue(TryStep(from, to, 0.45f, out Vector2 result), "The walker gave up on a step it could slide.");
        Assert.IsFalse(IsBlocked(result, 0.45f), "The slid step still ended inside the rig.");
        Assert.Greater(result.y, from.y + 0.1f, "Nothing of the step along the flank survived.");
    }

    [Test]
    public void SomebodyPutDownInsideAMotorhomeWalksOutOfIt()
    {
        // The crowd director recycles anonymous walkers by putting them back down somewhere in the
        // paddock — which can be on top of a parked rig.
        Motorhome(new Vector2(0f, 0f), new Vector2(3f, 10f));

        Vector2 freed = PushOut(Vector2.zero, 0.45f);

        Assert.IsFalse(IsBlocked(freed, 0.45f), "They were left sealed inside the motorhome.");
        Assert.Less((freed - Vector2.zero).magnitude, 12f, "They were flung across the paddock to get out.");
    }

    [Test]
    public void SomebodyStandingInTheOpenIsLeftWhereTheyAre()
    {
        Motorhome(new Vector2(0f, 0f), new Vector2(3f, 10f));

        var where = new Vector2(9f, 3f);
        Assert.AreEqual(where, PushOut(where, 0.45f), "A walker on clear ground was moved for no reason.");
    }

    // --- the walker's own route -------------------------------------------------------------------

    [Test]
    public void AWalkerNeverAimsAtAPointInsideAMotorhome()
    {
        // A waypoint inside bodywork can no longer be reached, so a walker aiming at one would spend its
        // whole life pressed against the same panel. They are rejected at generation.
        Motorhome(new Vector2(0f, 0f), new Vector2(4f, 8f));

        var go = new GameObject("PaddockWalker_Test");
        go.transform.position = new Vector3(15f, 0f, 0f);
        var walker = go.AddComponent(WalkerType);
        _spawned.Add(go);

        var path = (System.Collections.Generic.List<Vector3>)
                   WalkerType.GetField("_path", BindingFlags.NonPublic | BindingFlags.Instance)
                             .GetValue(walker);

        WalkerType.GetMethod("Configure", BindingFlags.Public | BindingFlags.Instance)
                  .Invoke(walker, new object[] { Vector3.zero, Vector3.right, Vector3.up, 20f, 10f });

        Assert.Greater(path.Count, 0, "The walker generated no route at all.");
        for (int i = 0; i < path.Count; i++)
            Assert.IsFalse(IsBlocked(path[i], 0.45f),
                           $"Waypoint {i} at {path[i]} is inside the motorhome.");
    }

    [Test]
    public void TurningAvoidanceOffRestoresTheOldRouting()
    {
        // The behaviour is a flag on the component, so a scene that wants the old "walk through anything"
        // crowd can still have it — and if avoidance ever misbehaves on a track it can be switched off
        // without a code change.
        var field = WalkerType.GetField("avoidObstacles", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(field, "PaddockWalker lost its avoidObstacles switch.");

        var go = new GameObject("PaddockWalker_Test");
        var walker = go.AddComponent(WalkerType);
        _spawned.Add(go);

        Assert.IsTrue((bool)field.GetValue(walker), "Obstacle avoidance should be on by default.");
    }
}
