using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// EditMode coverage for the player's RV carrying its own walkable ground (RVExterior.InstallWalkablePocket).
//
// The bug it pins: a motorhome moved out past the authored PaddockBoundary had its spawn clamped to the old
// paddock edge, the masked room was built there, and walking out of the door dropped the player back where
// the RV used to be. With the pocket, the rig and its doorway are walkable wherever it stands, the spawn
// clamp leaves the marker alone, and parking the rig into the lot takes the pocket with it.
//
// Assembly-CSharp can't be referenced by an asmdef, so the types are reached by reflection.
public class RVWalkablePocketTests
{
    static readonly System.Type RvType = System.Type.GetType("RVExterior, Assembly-CSharp");
    static readonly System.Type BoundaryType = System.Type.GetType("PaddockBoundary, Assembly-CSharp");

    readonly List<GameObject> _made = new();

    [TearDown]
    public void TearDown()
    {
        // The RV first: its OnDestroy takes the pocket down with it.
        for (int i = _made.Count - 1; i >= 0; i--)
            if (_made[i] != null) Object.DestroyImmediate(_made[i]);
        _made.Clear();
    }

    // The authored paddock: a 40 x 40 m square round the origin.
    void Paddock()
    {
        var go = new GameObject("TestPaddock");
        _made.Add(go);
        var poly = go.AddComponent<PolygonCollider2D>();
        poly.points = new[] { new Vector2(-20, -20), new Vector2(20, -20), new Vector2(20, 20), new Vector2(-20, 20) };
        go.AddComponent(BoundaryType);
    }

    // A 3.75 x 10 m shell like RV.prefab's, with the door on its +X edge.
    Component Rig(Vector3 at)
    {
        var go = new GameObject("TestRV");
        _made.Add(go);
        go.transform.position = at;
        var body = new GameObject("ColliderBody");
        body.transform.SetParent(go.transform, false);
        body.transform.localPosition = new Vector3(0f, -2f, 0f);
        body.AddComponent<BoxCollider2D>().size = new Vector2(3.75f, 10f);
        Physics2D.SyncTransforms();
        return go.AddComponent(RvType);
    }

    static bool Walkable(Vector2 p)
        => (bool)BoundaryType.GetMethod("IsInside", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { p });

    static Vector2 Constrain(Vector2 p)
        => (Vector2)BoundaryType.GetMethod("Constrain", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { p });

    static void Install(Component rv) => RvType.GetMethod("InstallWalkablePocket").Invoke(rv, null);
    static void MoveTo(Component rv, Vector3 to) => RvType.GetMethod("MoveTo").Invoke(rv, new object[] { to });
    static Vector3 Door(Component rv) => (Vector3)RvType.GetProperty("DoorWorldPosition").GetValue(rv);
    static Vector2 DoorDir(Component rv) => (Vector2)RvType.GetProperty("DoorWorldDirection").GetValue(rv);

    [Test]
    public void TypesExist()
    {
        Assert.IsNotNull(RvType, "RVExterior not found in Assembly-CSharp.");
        Assert.IsNotNull(BoundaryType, "PaddockBoundary not found in Assembly-CSharp.");
    }

    [Test]
    public void RigOutsideThePaddock_IsWalkable_SoTheSpawnIsNotClampedBack()
    {
        Paddock();
        var rv = Rig(new Vector3(0f, 60f, 0f));   // 40 m north of the paddock's edge
        Vector2 marker = rv.transform.position;

        Assert.IsFalse(Walkable(marker), "Precondition: the rig stands outside the authored paddock.");

        Install(rv);

        Assert.IsTrue(Walkable(marker), "The rig's own spawn marker is not walkable.");
        Assert.AreEqual(marker, Constrain(marker), "The spawn is still dragged off the marker by the clamp.");

        // Stepping out of the door: a couple of metres past the doorway must still be ground.
        Vector2 outside = (Vector2)Door(rv) + DoorDir(rv) * 2.5f;
        Assert.IsTrue(Walkable(outside), "The ground just outside the RV door is not walkable.");
    }

    [Test]
    public void ParkingTheRig_TakesThePocketAlong()
    {
        Paddock();
        var rv = Rig(new Vector3(0f, 60f, 0f));
        Install(rv);

        var parked = new Vector3(80f, 120f, 0f);
        MoveTo(rv, parked);

        Assert.IsTrue(Walkable(parked), "The rig's new spot is not walkable after it was parked.");
        Assert.IsFalse(Walkable(new Vector2(0f, 60f)), "The pocket stayed behind at the rig's old spot.");
    }

    [Test]
    public void NoAuthoredBoundary_InstallsNothing()
    {
        var active = (System.Collections.IList)BoundaryType.GetField("Active").GetValue(null);
        Assume.That(active.Count, Is.EqualTo(0), "The open scene has a PaddockBoundary of its own.");

        var rv = Rig(new Vector3(0f, 60f, 0f));
        Install(rv);

        // With no boundary at all the paddock is unbounded; a pocket here would newly fence the player in.
        Assert.AreEqual(0, active.Count, "A pocket was installed on a scene with no boundary.");
    }
}
