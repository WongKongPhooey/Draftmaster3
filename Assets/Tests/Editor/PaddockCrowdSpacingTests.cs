using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// The paddock crowd spreads out and walks round the bodywork rather than into it.
//
// Two things went wrong in play. Whole stretches of the crowd ended up stood in a line fifty deep along one
// edge of the paddock: PaddockSpawner's rectangle (a guess made from the pit lane) and the track package's
// PaddockBoundary (the paddock as built) need not line up, and everybody put down outside the boundary had
// their first step clamped onto its nearest edge. And walkers marched straight into motorhomes, because
// their waypoints were rolled anywhere in a paddock a few hundred metres long with the motorhome lot in
// between, and the step router can only slide along the first panel it meets.
//
// What is checked here: spawn points and waypoints land inside the boundary, every leg of a route is in
// plain sight, nobody heads for a spot that is already packed, and the spacing count itself is honest.
//
// Assembly-CSharp cannot be referenced from an asmdef, so the types are reached by reflection the same
// way PaddockObstacleTests does.
public class PaddockCrowdSpacingTests
{
    static readonly System.Type ObstaclesType = System.Type.GetType("PaddockObstacles, Assembly-CSharp");
    static readonly System.Type WalkerType = System.Type.GetType("PaddockWalker, Assembly-CSharp");
    static readonly System.Type BoundaryType = System.Type.GetType("PaddockBoundary, Assembly-CSharp");
    static readonly System.Type ActorType = System.Type.GetType("CrowdActor, Assembly-CSharp");
    static readonly System.Type SpawnerType = System.Type.GetType("PaddockSpawner, Assembly-CSharp");

    const float Radius = 0.45f;

    readonly List<GameObject> _spawned = new();
    readonly List<object> _actorsAdded = new();

    [SetUp]
    public void SetUp()
    {
        Assert.NotNull(ObstaclesType, "PaddockObstacles is missing from Assembly-CSharp.");
        Assert.NotNull(WalkerType, "PaddockWalker is missing from Assembly-CSharp.");
        Assert.NotNull(BoundaryType, "PaddockBoundary is missing from Assembly-CSharp.");
        Assert.NotNull(ActorType, "CrowdActor is missing from Assembly-CSharp.");
        ForgetCache();
    }

    [TearDown]
    public void TearDown()
    {
        var all = ActorList();
        for (int i = 0; i < _actorsAdded.Count; i++) all.Remove(_actorsAdded[i]);
        _actorsAdded.Clear();

        for (int i = 0; i < _spawned.Count; i++)
            if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
        _spawned.Clear();
        ForgetCache();
        Physics2D.SyncTransforms();
    }

    // --- fixtures ---------------------------------------------------------------------------------

    GameObject Motorhome(Vector2 centre, Vector2 size)
    {
        var go = new GameObject("RV_Test");
        go.transform.position = centre;
        go.AddComponent<BoxCollider2D>().size = size;
        _spawned.Add(go);
        Physics2D.SyncTransforms();
        return go;
    }

    // A walkable pocket, the same shape the factory and the RV lot put down.
    Component Boundary(Vector2 centre, Vector2 size)
    {
        var b = (Component)BoundaryType.GetMethod("Pocket", BindingFlags.Public | BindingFlags.Static)
                                       .Invoke(null, new object[] { null, "Boundary_Test", centre, size });
        _spawned.Add(b.gameObject);
        Physics2D.SyncTransforms();
        return b;
    }

    // Somebody stood in the crowd. CrowdActor's OnEnable does not run in edit mode, so it is registered
    // with the crowd list by hand (and taken off it again at teardown).
    void Bystander(Vector2 at)
    {
        var go = new GameObject("PaddockNPC_Test");
        go.transform.position = at;
        var a = go.AddComponent(ActorType);
        _spawned.Add(go);
        ActorList().Add(a);
        _actorsAdded.Add(a);
    }

    Component Walker(Vector2 at, Vector3 centre, float halfLen, float halfDepth)
    {
        var go = new GameObject("PaddockWalker_Test");
        go.transform.position = new Vector3(at.x, at.y, -0.1f);
        var w = go.AddComponent(WalkerType);
        _spawned.Add(go);
        WalkerType.GetMethod("Configure", BindingFlags.Public | BindingFlags.Instance)
                  .Invoke(w, new object[] { centre, Vector3.right, Vector3.up, halfLen, halfDepth });
        return w;
    }

    // --- reflection wrappers ----------------------------------------------------------------------

    static System.Collections.IList ActorList() =>
        (System.Collections.IList)ActorType.GetField("All", BindingFlags.Public | BindingFlags.Static).GetValue(null);

    static void ForgetCache() =>
        ObstaclesType?.GetMethod("ForgetCache", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);

    static bool IsBlocked(Vector2 p, float radius) =>
        (bool)ObstaclesType.GetMethod("IsBlocked", BindingFlags.Public | BindingFlags.Static)
                           .Invoke(null, new object[] { p, radius });

    static bool PathClear(Vector2 a, Vector2 b, float radius) =>
        (bool)ObstaclesType.GetMethod("PathClear", BindingFlags.Public | BindingFlags.Static)
                           .Invoke(null, new object[] { a, b, radius });

    static bool IsInside(Vector2 p) =>
        (bool)BoundaryType.GetMethod("IsInside", BindingFlags.Public | BindingFlags.Static)
                          .Invoke(null, new object[] { p });

    static bool SharedArea(Vector2 a, Vector2 b) =>
        (bool)BoundaryType.GetMethod("SharedArea", BindingFlags.Public | BindingFlags.Static)
                          .Invoke(null, new object[] { a, b });

    static int CountWithin(Vector2 p, float radius, int stopAt = int.MaxValue) =>
        (int)ActorType.GetMethod("CountWithin", BindingFlags.Public | BindingFlags.Static)
                      .Invoke(null, new object[] { p, radius, stopAt });

    static List<Vector3> PathOf(Component walker) =>
        (List<Vector3>)WalkerType.GetField("_path", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(walker);

    static void Regenerate(Component walker) =>
        WalkerType.GetMethod("GeneratePath", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(walker, null);

    // --- seeing where you are going -----------------------------------------------------------------

    [Test]
    public void AMotorhomeInTheWayClosesTheLineButNotTheAisleBesideIt()
    {
        Motorhome(Vector2.zero, new Vector2(3f, 10f));

        Assert.IsFalse(PathClear(new Vector2(-10f, 0f), new Vector2(10f, 0f), Radius),
                       "A straight line through the middle of a motorhome was reported clear.");
        Assert.IsTrue(PathClear(new Vector2(-10f, 8f), new Vector2(10f, 8f), Radius),
                      "The aisle past the end of the motorhome was reported blocked.");
    }

    [Test]
    public void EveryLegOfAWalkersRouteIsInPlainSight()
    {
        // A row of motorhomes across the middle of the paddock, the way the lot parks them.
        for (int i = -4; i <= 4; i++) Motorhome(new Vector2(i * 6f, 0f), new Vector2(3f, 9f));

        for (int run = 0; run < 20; run++)
        {
            var w = Walker(new Vector2(run - 10f, -12f), Vector3.zero, 40f, 20f);
            Vector2 from = w.transform.position;
            var path = PathOf(w);
            Assert.Greater(path.Count, 0, "The walker planned nowhere to go.");
            for (int i = 0; i < path.Count; i++)
            {
                Assert.IsFalse(IsBlocked(path[i], Radius), $"Waypoint {i} is inside a motorhome.");
                Assert.IsTrue(PathClear(from, path[i], Radius),
                              $"Leg {i} ({from} -> {(Vector2)path[i]}) runs into a motorhome.");
                from = path[i];
            }
        }
    }

    // --- staying inside the paddock -----------------------------------------------------------------

    [Test]
    public void WaypointsStayInsideABoundaryTheRectangleOnlyPartlyCovers()
    {
        // The spawner's rectangle runs 200m along the pit lane; the paddock as built is 60m of it.
        Boundary(new Vector2(80f, 0f), new Vector2(60f, 30f));

        for (int run = 0; run < 20; run++)
        {
            var w = Walker(new Vector2(70f + run, 0f), Vector3.zero, 100f, 15f);
            var path = PathOf(w);
            for (int i = 0; i < path.Count; i++)
                Assert.IsTrue(IsInside(path[i]), $"Waypoint {i} at {path[i]} is outside the paddock boundary.");
        }
    }

    [Test]
    public void TheSpawnerPutsPeopleDownInsideTheBoundaryNotOnItsEdge()
    {
        Assert.NotNull(SpawnerType, "PaddockSpawner is missing from Assembly-CSharp.");
        var go = new GameObject("PaddockSpawner_Test");
        _spawned.Add(go);
        var spawner = go.AddComponent(SpawnerType);
        var pick = SpawnerType.GetMethod("ClearSpawnPoint", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(pick, "PaddockSpawner.ClearSpawnPoint is missing.");

        // Partly overlapping, and not overlapping at all — the boundary wins either way.
        Boundary(new Vector2(80f, 0f), new Vector2(60f, 30f));
        foreach (var centre in new[] { Vector3.zero, new Vector3(0f, 300f, 0f) })
        {
            for (int i = 0; i < 100; i++)
            {
                var p = (Vector3)pick.Invoke(spawner, new object[] { centre, Vector3.right, Vector3.up, 100f, 15f });
                Assert.IsTrue(IsInside(p), $"Spawned at {p}, outside the boundary (rectangle centred {centre}).");
            }
        }
    }

    [Test]
    public void TwoPocketsAreNotOneArea()
    {
        Boundary(new Vector2(0f, 0f), new Vector2(20f, 20f));
        Boundary(new Vector2(100f, 0f), new Vector2(20f, 20f));

        Assert.IsTrue(SharedArea(new Vector2(-5f, 0f), new Vector2(5f, 3f)), "Two points in one pocket.");
        Assert.IsFalse(SharedArea(new Vector2(0f, 0f), new Vector2(100f, 0f)),
                       "A recycle could land in a pocket across the racetrack from the player.");
    }

    // --- not piling up ----------------------------------------------------------------------------

    [Test]
    public void TheSpacingCountSeesOnlyWhoIsNearAndStopsWhenAsked()
    {
        for (int i = 0; i < 6; i++) Bystander(new Vector2(i * 0.3f, 0f));
        Bystander(new Vector2(20f, 0f));

        Assert.AreEqual(6, CountWithin(Vector2.zero, 2.5f));
        Assert.AreEqual(3, CountWithin(Vector2.zero, 2.5f, 3), "The count ran past its stop.");
        Assert.AreEqual(0, CountWithin(new Vector2(-50f, 0f), 2.5f));
    }

    [Test]
    public void NobodyHeadsForASpotThatIsAlreadyPacked()
    {
        // Half the paddock already crowded, a person every metre and a bit.
        for (float x = 2f; x <= 26f; x += 1.2f)
            for (float y = -26f; y <= 26f; y += 1.2f)
                Bystander(new Vector2(x, y));

        var w = Walker(Vector2.zero, Vector3.zero, 30f, 30f);
        for (int run = 0; run < 10; run++)
        {
            Regenerate(w);
            var path = PathOf(w);
            for (int i = 0; i < path.Count; i++)
                Assert.Less(CountWithin(path[i], 2.5f, 4), 4,
                            $"Waypoint {i} at {path[i]} is in the middle of a crowd.");
        }
    }
}
