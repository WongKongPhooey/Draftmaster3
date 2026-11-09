using System;
using System.Linq;
using Draftmaster.Tracks;
using NUnit.Framework;
using UnityEngine;

// RoadsideShopBuilder (Assembly-CSharp, reached by reflection): a layout builds into the room the game walks
// around, and reads back unchanged — which is what the Shop Layouts window saves.
public class RoadsideShopBuilderTests
{
    static readonly Type Builder = Type.GetType("RoadsideShopBuilder, Assembly-CSharp");
    static readonly Type Ctx = Type.GetType("RoadsideShopBuilder+Context, Assembly-CSharp");
    static readonly Type Piece = Type.GetType("ShopPiece, Assembly-CSharp");

    static ShopLayout Layout() => new ShopLayout
    {
        name = "test", width = 22f, height = 16f, doorX = -7f,
        counter = new ShopSpot { x = 6f, y = -4.5f }, clerk = new ShopSpot { x = 6f, y = -3.4f },
        bays = new[] { new ShopBay { x = -5f, y = 4f, rotation = 90f }, new ShopBay { x = 3f, y = 4f, state = "working" } },
        workers = new[] { new ShopSpot { x = 3f, y = 1.7f } },
        props = new[] { new ShopProp { kind = "rack", x = 0f, y = 7f, width = 3f, height = 0.8f } },
    };

    static GameObject Build(ShopLayout l, ShopKind kind, bool editMode, Transform parent)
    {
        var ctx = Activator.CreateInstance(Ctx);
        Ctx.GetField("kind").SetValue(ctx, kind);
        Ctx.GetField("locationId").SetValue(ctx, "test_yard");
        Ctx.GetField("editMode").SetValue(ctx, editMode);
        return (GameObject)Builder.GetMethod("Build").Invoke(null, new[] { l, parent, ctx });
    }

    [Test]
    public void ARoomReadsBackAsTheLayoutItWasBuiltFrom([Values] ShopKind kind)
    {
        var parent = new GameObject("Parent");
        try
        {
            var root = Build(Layout(), kind, true, parent.transform);
            var back = (ShopLayout)Builder.GetMethod("Read").Invoke(null, new object[] { root.transform, Layout() });
            Assert.AreEqual(-7f, back.doorX, 0.06f);
            Assert.AreEqual(6f, back.counter.x, 0.06f);
            Assert.AreEqual(-3.4f, back.clerk.y, 0.06f);
            Assert.AreEqual(2, back.bays.Length);
            var bay = back.bays.First(b => b.x < 0f);
            Assert.AreEqual(90f, bay.rotation, 0.06f);
            Assert.AreEqual("working", back.bays.First(b => b.x > 0f).state);
            Assert.AreEqual(1, back.workers.Length);
            Assert.AreEqual(1.7f, back.workers[0].y, 0.06f);
            Assert.AreEqual(1, back.props.Length);
            Assert.AreEqual("rack", back.props[0].kind);
            Assert.AreEqual(3f, back.props[0].width, 0.06f);
            Assert.IsTrue(back.authored);
            CollectionAssert.IsEmpty(ShopLayouts.Problems(back, kind));
        }
        finally { UnityEngine.Object.DestroyImmediate(parent); }
    }

    [Test]
    public void InTheGameTheCounterServesAndTheDoorLeads()
    {
        var parent = new GameObject("Parent");
        try
        {
            var root = Build(Layout(), ShopKind.Junkyard, false, parent.transform);
            Assert.IsNotNull(root.GetComponentInChildren(Type.GetType("ShopCounterNPC, Assembly-CSharp")), "someone at the counter");
            Assert.IsNotNull(root.GetComponentInChildren(Type.GetType("SceneDoorInteractable, Assembly-CSharp")), "a way out");
            Assert.AreEqual(7, root.GetComponentsInChildren(Piece).Length, "counter, clerk, two bays, a worker, a prop, the door");
            var walls = root.GetComponentsInChildren<BoxCollider2D>().Where(c => c.name.StartsWith("Wall")).ToArray();
            Assert.GreaterOrEqual(walls.Length, 4, "the room is walled in");
        }
        finally { UnityEngine.Object.DestroyImmediate(parent); }
    }
}
