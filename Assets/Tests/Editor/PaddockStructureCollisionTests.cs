using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// The grandstands and the pit box carts are solid to the on-foot player.
//
// Both used to be nothing but quads, so the player walked straight across the painted crowd and through
// the cart parked on every pit box. A stand now lays a solid block over its footprint at runtime
// (Grandstand.BuildStructure), opened only while the player is sat in one watching a session — the gate
// drops them on a seat inside it — and the pit box cart and its tyre stack carry box colliders.
//
// Assembly-CSharp cannot be referenced from an asmdef, so the types are reached by reflection, as
// GrandstandKeepOutTests does.
public class PaddockStructureCollisionTests
{
    static readonly System.Type StandType = System.Type.GetType("Grandstand, Assembly-CSharp");
    static readonly System.Type PitStandType = System.Type.GetType("PitBoxStand, Assembly-CSharp");

    const float StandLength = 110f;
    const float StandDepth = 14f;

    readonly List<GameObject> _spawned = new();

    [SetUp]
    public void SetUp()
    {
        Assert.NotNull(StandType, "Grandstand is missing from Assembly-CSharp.");
        Assert.NotNull(PitStandType, "PitBoxStand is missing from Assembly-CSharp.");
        SetOpen(false);
    }

    [TearDown]
    public void TearDown()
    {
        SetOpen(false);
        for (int i = 0; i < _spawned.Count; i++)
            if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
        _spawned.Clear();
        Physics2D.SyncTransforms();
    }

    // --- fixtures ---------------------------------------------------------------------------------

    // BuildStructure is called by hand because Build() only lays one in play mode.
    GameObject Stand(bool solid = true)
    {
        var go = new GameObject("Grandstand_Test");
        _spawned.Add(go);

        var stand = go.AddComponent(StandType);
        SetField(stand, "length", StandLength);
        SetField(stand, "depth", StandDepth);
        SetField(stand, "solid", solid);
        Call(stand, "BuildStructure");

        Physics2D.SyncTransforms();
        return go;
    }

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

    static void SetOpen(bool open) =>
        StandType?.GetMethod("SetOpenToPlayer", BindingFlags.Public | BindingFlags.Static)
                 ?.Invoke(null, new object[] { open });

    static string StructureName =>
        (string)StandType.GetField("StructureName", BindingFlags.Public | BindingFlags.Static).GetValue(null);

    static BoxCollider2D Structure(GameObject stand)
    {
        var t = stand.transform.Find(StructureName);
        Assert.IsNotNull(t, "A solid stand laid no '" + StructureName + "' block.");
        var box = t.GetComponent<BoxCollider2D>();
        Assert.IsNotNull(box, "The stand's block has no collider on it.");
        return box;
    }

    // --- the grandstand -----------------------------------------------------------------------------

    [Test]
    public void AStandIsASolidBlockOverItsFootprint()
    {
        var box = Structure(Stand());

        Assert.IsFalse(box.isTrigger, "The stand's block is a trigger — the player walks straight across it.");
        Assert.IsTrue(box.enabled, "The stand's block is switched off with nobody watching from it.");
        Assert.AreEqual(new Vector2(StandLength, StandDepth), box.size, "The block is not the size of the stand.");
        Assert.IsTrue(box.OverlapPoint(new Vector2(0f, 0f)), "The middle of the stand is not covered.");
        Assert.IsFalse(box.OverlapPoint(new Vector2(0f, -StandDepth)), "The block reaches out in front of the stand.");
    }

    [Test]
    public void WatchingFromAStandOpensItAndLeavingClosesIt()
    {
        var box = Structure(Stand());

        SetOpen(true);
        Assert.IsFalse(box.enabled,
            "The stand stays solid while the player is sat in it — the physics shoves them off the seat.");

        // A stand built while somebody is already watching comes up open as well.
        var late = Structure(Stand());
        Assert.IsFalse(late.enabled, "A stand built mid-visit came up solid around the player.");

        SetOpen(false);
        Assert.IsTrue(box.enabled, "The stand never went solid again after the player left it.");
        Assert.IsTrue(late.enabled, "The late stand never went solid again after the player left it.");
    }

    [Test]
    public void RebuildingLaysOneBlock()
    {
        var go = Stand();
        var stand = go.GetComponent(StandType);
        SetField(stand, "length", StandLength * 0.5f);
        Call(stand, "BuildStructure");

        int blocks = 0;
        foreach (Transform child in go.transform)
            if (child.name == StructureName) blocks++;
        Assert.AreEqual(1, blocks, "Rebuilding a stand stacked up a second solid block.");
        Assert.AreEqual(StandLength * 0.5f, Structure(go).size.x, 0.001f, "A resized stand kept its old block.");
    }

    [Test]
    public void AViewingBankIsNotSolid()
    {
        var go = Stand(solid: false);
        Assert.IsNull(go.transform.Find(StructureName), "solid is off and the stand laid a block anyway.");
    }

    // --- the pit box cart ---------------------------------------------------------------------------

    [Test]
    public void ThePitBoxCartIsSolid()
    {
        var parent = new GameObject("PitBox_Test");
        _spawned.Add(parent);

        var build = PitStandType.GetMethod("Build", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(build, "PitBoxStand.Build is gone.");
        var stand = (Component)build.Invoke(null, new object[] { parent.transform, 0, 3.4f, -0.02f });

        foreach (var part in new[] { "Cart", "Tyres" })
        {
            var t = stand.transform.Find(part);
            Assert.IsNotNull(t, "The pit box stand has no '" + part + "'.");
            var box = t.GetComponent<BoxCollider2D>();
            Assert.IsNotNull(box, "The pit box " + part.ToLowerInvariant() + " has no collider — the player walks through it.");
            Assert.IsFalse(box.isTrigger, "The pit box " + part.ToLowerInvariant() + " collider is a trigger.");
        }

        var cart = stand.transform.Find("Cart").GetComponent<BoxCollider2D>();
        Assert.AreEqual(1.5f, cart.size.x, 0.001f, "The cart's collider is not the cart's width.");
        Assert.AreEqual(2.4f, cart.size.y, 0.001f, "The cart's collider is not the cart's length.");
    }
}
