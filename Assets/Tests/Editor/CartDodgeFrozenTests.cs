using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// EditMode coverage for a cart dodge meeting the crowd LOD freeze.
//
// CartDodge is bolted onto an NPC by the cart, after CrowdActor has collected which behaviours it freezes,
// so the director never switches it off. The cart outruns the director's rota, and an NPC told to jump
// clear is regularly frozen mid-jump: its body taken out of the simulation. Two things went wrong there,
// and both read in the paddock as people walking on the spot:
//
//   * MovePosition on a body out of the simulation does nothing, so the jump never landed and the walk
//     cycle ran forever;
//   * a dodge that did finish switched the walker back on inside a frozen body, which then marched in place.
//
// Assembly-CSharp can't be referenced by an asmdef, so everything is reached by reflection. Awake does not
// run for components added in EditMode, so it is invoked by hand.
public class CartDodgeFrozenTests
{
    static readonly Type DodgeType = Type.GetType("CartDodge, Assembly-CSharp");
    static readonly Type WalkerType = Type.GetType("PaddockWalker, Assembly-CSharp");
    static readonly Type CrowdType = Type.GetType("CrowdActor, Assembly-CSharp");
    static readonly Type LodType = Type.GetType("Draftmaster.Crowd.CrowdLod, Draftmaster.Crowd");

    readonly List<GameObject> _made = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _made) if (go != null) UnityEngine.Object.DestroyImmediate(go);
        _made.Clear();
    }

    static void Awake(Component c) =>
        c.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(c, null);

    // A paddock walker as PaddockSpawner builds one: kinematic body, the wander script, and a crowd actor
    // governing it.
    (GameObject go, Behaviour walker, Component crowd, Rigidbody2D rb) Person(Vector2 at)
    {
        var go = new GameObject("TestWalker");
        _made.Add(go);
        go.transform.position = at;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.position = at;
        var walker = (Behaviour)go.AddComponent(WalkerType);
        Awake(walker);
        var crowd = go.AddComponent(CrowdType);
        Awake(crowd);
        return (go, walker, crowd, rb);
    }

    static void Freeze(Component crowd) =>
        CrowdType.GetMethod("Apply").Invoke(crowd, new[] { Enum.Parse(LodType, "Frozen") });

    static bool Dodging(Component dodge) => (bool)DodgeType.GetProperty("Dodging").GetValue(dodge);

    static void RunToEnd(Component dodge)
    {
        // An awake body is moved with MovePosition, which lands on the next physics step — and EditMode has
        // none, so step 2D physics by hand alongside the dodge.
        var tick = DodgeType.GetMethod("Tick");
        var mode = Physics2D.simulationMode;
        Physics2D.simulationMode = SimulationMode2D.Script;
        try
        {
            for (int i = 0; i < 500 && Dodging(dodge); i++)
            {
                tick.Invoke(dodge, new object[] { 0.02f });
                Physics2D.Simulate(0.02f);
            }
        }
        finally { Physics2D.simulationMode = mode; }
    }

    [Test]
    public void TypesExist()
    {
        Assert.IsNotNull(DodgeType, "CartDodge is gone.");
        Assert.IsNotNull(WalkerType, "PaddockWalker is gone.");
        Assert.IsNotNull(CrowdType, "CrowdActor is gone.");
        Assert.IsNotNull(LodType, "CrowdLod is gone.");
        Assert.IsNotNull(DodgeType.GetMethod("Tick"), "CartDodge.Tick is gone — nothing to step the jump with.");
    }

    [Test]
    public void FrozenMidJump_StillLands_AndLeavesTheWalkerAsleep()
    {
        var p = Person(new Vector2(0f, 5f));
        var dodge = p.go.AddComponent(DodgeType);
        Awake(dodge);

        // Cart coming up the y axis straight at them: they jump sideways along x.
        DodgeType.GetMethod("Scatter").Invoke(dodge, new object[] { new Vector2(0f, 0f), new Vector2(0f, 8f) });
        Assert.IsTrue(Dodging(dodge), "Precondition: the person was told to jump.");

        // The cart has gone by and the director freezes them before the jump is over.
        Freeze(p.crowd);
        Assert.IsFalse(p.rb.simulated, "Precondition: a frozen body is out of the simulation.");

        RunToEnd(dodge);

        Assert.IsFalse(Dodging(dodge), "The jump never finished — the NPC walks on the spot forever.");
        Assert.Greater(Mathf.Abs(p.go.transform.position.x), 1f, "The NPC never moved out of the cart's path.");
        Assert.AreEqual(p.go.transform.position.x, p.rb.position.x, 0.01f, "Body and transform disagree after the jump.");
        Assert.IsFalse(p.walker.enabled, "The walker was woken inside a frozen body and will march in place.");
    }

    [Test]
    public void AwakeThroughout_HandsTheWalkerBack()
    {
        var p = Person(new Vector2(0f, 5f));
        var dodge = p.go.AddComponent(DodgeType);
        Awake(dodge);

        DodgeType.GetMethod("Scatter").Invoke(dodge, new object[] { new Vector2(0f, 0f), new Vector2(0f, 8f) });
        Assert.IsFalse(p.walker.enabled, "Precondition: the dodge takes the walker over while it jumps.");

        RunToEnd(dodge);

        Assert.IsFalse(Dodging(dodge));
        Assert.IsTrue(p.walker.enabled, "An awake walker was not handed back after the jump.");
    }
}
