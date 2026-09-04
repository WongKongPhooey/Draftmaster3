using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// Nobody the player is meant to talk to stands somewhere the player cannot go.
//
// The on-foot layer clamps the player to an authored walkable polygon (PaddockBoundary). The every-track
// cast is not placed by hand but derived from geometry — "three metres off the pit lane, a metre behind the
// car" — and those offsets were written before the boundary existed. At Watkins Glen that put the pit
// greeter, the crew chief and the chief strategist out in the fast lane: the paddock there starts at y=21.4
// and the three of them stood at y=16.7, visible across the pit road and impossible to reach.
//
// These cover the containment maths the fix rests on. Where the bodies actually end up needs a track, a
// pit spline and a play mode, so it is checked by running the scene; what is checked here is that the
// question "can the player stand here, and if not where is the nearest place they can" has honest answers.
//
// Assembly-CSharp cannot be referenced from an asmdef, so the type is reached by reflection the same way
// PaddockLotAreaTests reaches the lot areas.
public class PaddockReachTests
{
    static readonly System.Type BoundaryType = System.Type.GetType("PaddockBoundary, Assembly-CSharp");

    GameObject _go;

    // A rectangular walkable area, live and registered, the way a track package's boundary is.
    Component Rectangle(Vector2 centre, float width, float depth)
    {
        _go = new GameObject("PaddockBoundary");
        _go.transform.position = centre;

        var poly = _go.AddComponent<PolygonCollider2D>();
        float hx = width * 0.5f, hy = depth * 0.5f;
        poly.points = new[]
        {
            new Vector2(-hx, -hy), new Vector2(hx, -hy), new Vector2(hx, hy), new Vector2(-hx, hy)
        };
        var boundary = _go.AddComponent(BoundaryType);

        // Edit mode does not step the physics world on its own, and OverlapPoint asks that world rather
        // than the collider's serialised points. Without this every containment test answers "yes".
        Physics2D.SyncTransforms();
        Assert.AreEqual(1, ActiveCount(), "The boundary didn't register itself.");
        return boundary;
    }

    static int ActiveCount()
    {
        var list = (System.Collections.ICollection)
                   BoundaryType.GetField("Active", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        return list.Count;
    }

    static bool Inside(Vector2 p) =>
        (bool)BoundaryType.GetMethod("Inside", BindingFlags.Public | BindingFlags.Static)
                          .Invoke(null, new object[] { p });

    static Vector2 ConstrainInside(Vector2 p, float inset = 1.5f) =>
        (Vector2)BoundaryType.GetMethod("ConstrainInside", BindingFlags.Public | BindingFlags.Static)
                             .Invoke(null, new object[] { p, inset });

    [TearDown]
    public void TearDown()
    {
        if (_go != null) Object.DestroyImmediate(_go);
        _go = null;
    }

    [Test]
    public void WithNoBoundaryEverywhereIsWalkable()
    {
        // Most scenes have no boundary at all, and a containment test that answered "no" there would strand
        // every anchored NPC in the project.
        Assert.IsTrue(Inside(new Vector2(1234f, -987f)));
    }

    [Test]
    public void APointOutsideTheWalkableAreaIsSeenAsOutside()
    {
        Rectangle(new Vector2(0f, 30f), 100f, 20f);   // walkable: y from 20 to 40

        Assert.IsTrue(Inside(new Vector2(0f, 30f)), "The middle of the paddock should be walkable.");
        Assert.IsFalse(Inside(new Vector2(0f, 16f)), "The pit lane, outside the paddock, should not be.");
    }

    [Test]
    public void APointOutsideComesBackProperlyInsideRatherThanOnTheLine()
    {
        // The Watkins Glen numbers: the paddock edge at y=21.4, a body derived at y=16.7. Clamping to the
        // nearest point puts them IN the fence, half on the side the player can never reach.
        Rectangle(new Vector2(0f, 30f), 100f, 17.2f); // walkable: y from 21.4 to 38.6

        Vector2 fixedUp = ConstrainInside(new Vector2(-5f, 16.7f), 1.5f);

        Assert.IsTrue(Inside(fixedUp), "The corrected point is still outside the walkable area.");
        Assert.Greater(fixedUp.y, 21.4f + 0.5f, "The body was left standing in the fence line.");
        Assert.AreEqual(-5f, fixedUp.x, 2f, "It should come back at the nearest point, not slide down the fence.");
    }

    [Test]
    public void APointAlreadyWalkableIsLeftAlone()
    {
        Rectangle(new Vector2(0f, 30f), 100f, 20f);

        var where = new Vector2(12f, 33f);
        Assert.AreEqual(where, ConstrainInside(where), "A reachable spot was moved for no reason.");
    }

    [Test]
    public void ADisjointPocketCountsAsWalkableToo()
    {
        // Two boundaries, no overlap: the paddock and a viewing area. Being inside either is enough, and a
        // point in the second must not be dragged into the first.
        Rectangle(new Vector2(0f, 30f), 40f, 20f);
        var second = new GameObject("PaddockBoundary_Far");
        second.transform.position = new Vector3(200f, 30f, 0f);
        var poly = second.AddComponent<PolygonCollider2D>();
        poly.points = new[] { new Vector2(-20f, -10f), new Vector2(20f, -10f),
                              new Vector2(20f, 10f), new Vector2(-20f, 10f) };
        second.AddComponent(BoundaryType);

        try
        {
            var inFarPocket = new Vector2(200f, 30f);
            Assert.IsTrue(Inside(inFarPocket));
            Assert.AreEqual(inFarPocket, ConstrainInside(inFarPocket));
        }
        finally
        {
            Object.DestroyImmediate(second);
        }
    }
}
