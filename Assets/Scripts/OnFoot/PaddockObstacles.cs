using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The solid scenery a walking NPC has to go round — motorhomes, popup garages, haulers, and any prop
// that was built with a collider on it.
//
// The player never had this problem: they are a DYNAMIC Rigidbody2D, so the plain static BoxCollider2Ds
// that RVExterior, PopupGarageRig, DriverMotorhomeLot and PaddockProps put down already stop them.
// A paddock walker is a KINEMATIC body driven by MovePosition, and nothing stops one of those — it is
// moved where it is told and the physics engine sorts the overlap out afterwards, which for a static
// collider means not at all. So the crowd walked straight through the side of every motorhome in the lot.
//
// Rather than have every builder register what it put down, the question is asked of the physics world
// itself: what solid collider is standing on this patch of ground? That catches hand-authored scenery in a
// track package as well as the generated lot, and it uses Box2D's own broadphase, so a crowd of hundreds
// costs one cheap overlap query each per frame.
//
// The one thing the physics world cannot answer on its own is a hole that is open ON PURPOSE: a popup
// garage's shell is a ring of walls with the doorway cut out, because the player walks into the meeting
// room behind it, so the floor inside reads as clear tarmac. A PaddockNoGo volume states that keep-out
// where nothing solid can, and is the only trigger this treats as a wall.
//
// The one thing that must NOT count is people. NPCs and the player are solid too, and treating each other
// as walls would seize the crowd solid the moment it packed together — bumping into somebody is already
// handled properly by PaddockWalker.Bumped (stop, look at them, walk on). So a collider is scenery unless
// there is a person somewhere above it in the hierarchy, and that verdict is cached per collider because
// walking the parents is the expensive half.
//
// PaddockBoundary answers "where may I walk"; this answers "what is standing in the way while I do".
public static class PaddockObstacles
{
    // Reused per query — Physics2D fills this list rather than allocating a fresh array every step.
    static readonly List<Collider2D> _hits = new();

    // collider instance id -> "is this scenery?". Colliders don't change what they are, and the parent
    // walk that decides it is far dearer than the overlap query that finds them.
    static readonly Dictionary<int, bool> _scenery = new();

    static ContactFilter2D _filter;
    static bool _filterBuilt;

    // Triggers come back from the query but are thrown out by Classify, with one exception: a volume that
    // says outright it is keep-out ground (PaddockNoGo). That is how a popup garage's floor is kept clear —
    // its shell is a ring of walls with the doorway cut out of it, so the room inside is open ground to the
    // physics world and has to be closed off some other way. Everything else with isTrigger set — the
    // paddock boundary, the lot areas, every interaction range — is bookkeeping, and honouring it would pin
    // the crowd inside its own paperwork.
    static ContactFilter2D Filter
    {
        get
        {
            if (!_filterBuilt)
            {
                _filter = new ContactFilter2D();
                _filter.NoFilter();
                _filter.useTriggers = true;
                _filterBuilt = true;
            }
            return _filter;
        }
    }

    // A new scene brings new colliders; instance ids from the old one are dead weight.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void HookSceneChanges()
    {
        _scenery.Clear();
        SceneManager.activeSceneChanged -= OnSceneChanged;
        SceneManager.activeSceneChanged += OnSceneChanged;
    }

    static void OnSceneChanged(Scene from, Scene to) => _scenery.Clear();

    // Drop the cached verdicts. Tests call this between fixtures; nothing in play needs it.
    public static void ForgetCache() => _scenery.Clear();

    // True if solid scenery covers the disc of the given radius around this point.
    public static bool IsBlocked(Vector2 point, float radius) => Blocker(point, radius) != null;

    // The first piece of solid scenery overlapping that disc, or null if the ground is clear.
    public static Collider2D Blocker(Vector2 point, float radius)
    {
        int n = Physics2D.OverlapCircle(point, Mathf.Max(0.01f, radius), Filter, _hits);
        for (int i = 0; i < n; i++)
        {
            var c = _hits[i];
            if (IsScenery(c)) return c;
        }
        return null;
    }

    // Take a step from `from` toward `to`, going round anything solid in the way.
    //
    // Returns false only when there is no way forward at all — the caller should then give up on
    // wherever it was heading rather than grind against the bodywork. Otherwise `result` is either the
    // step as asked for, or the part of it that runs ALONG the obstruction with the part that runs into
    // it thrown away, which is what makes a walker slide down the side of a motorhome instead of
    // stopping dead against it. It is the same trick OnFootController uses on the player.
    public static bool TryStep(Vector2 from, Vector2 to, float radius, out Vector2 result)
    {
        result = to;

        Collider2D hit = Blocker(to, radius);
        if (hit == null) return true;

        // Which way is "out of the wall" from where we are standing now. ClosestPoint returns the point
        // itself when it is inside the collider, so a zero-length normal means we are already in it —
        // that is PushOut's job, not this one.
        Vector2 surface = hit.ClosestPoint(from);
        Vector2 normal = from - surface;
        if (normal.sqrMagnitude < 1e-6f) { result = from; return false; }
        normal.Normalize();

        Vector2 step = to - from;
        Vector2 along = step - normal * Vector2.Dot(step, normal);
        if (along.sqrMagnitude < 1e-8f) { result = from; return false; }   // dead head-on into a face

        Vector2 slid = from + along;
        if (Blocker(slid, radius) != null) { result = from; return false; } // an inside corner: nowhere to slide

        result = slid;
        return true;
    }

    // Standing inside something — recycled on top of a motorhome by the crowd director, or a rig
    // assembled around them — so walk out to the nearest clear ground.
    //
    // Tried away from the middle of whatever is holding them first (straight out through the nearest
    // face of a box), then round the compass. Somewhere clear that is also inside the walkable area wins
    // over somewhere merely clear, so nobody is shoved out of the paddock to escape a caravan.
    // The point comes back unchanged when there is nowhere better, which leaves the caller no worse off.
    public static Vector2 PushOut(Vector2 point, float radius, float maxDistance = 12f)
    {
        Collider2D hit = Blocker(point, radius);
        if (hit == null) return point;

        float step = Mathf.Max(0.5f, radius * 2f);

        Vector2 away = point - (Vector2)hit.bounds.center;
        if (away.sqrMagnitude < 1e-6f) away = Vector2.up;
        away.Normalize();

        Vector2 fallback = point;
        bool haveFallback = false;

        for (float d = step; d <= maxDistance; d += step)
        {
            // The way out of this body first, then eight more directions at the same range.
            for (int i = -1; i < 8; i++)
            {
                Vector2 dir = i < 0 ? away : new Vector2(Mathf.Cos(i * Mathf.PI * 0.25f), Mathf.Sin(i * Mathf.PI * 0.25f));
                Vector2 p = point + dir * d;
                if (Blocker(p, radius) != null) continue;

                if (PaddockBoundary.IsInside(p)) return p;
                if (!haveFallback) { fallback = p; haveFallback = true; }
            }
        }

        return haveFallback ? fallback : point;
    }

    static bool IsScenery(Collider2D c)
    {
        if (c == null || !c.enabled) return false;

        int id = c.GetInstanceID();
        if (_scenery.TryGetValue(id, out bool known)) return known;

        // Cheap insurance against a very long session in one scene quietly growing this forever.
        if (_scenery.Count > 8192) _scenery.Clear();

        bool scenery = Classify(c);
        _scenery[id] = scenery;
        return scenery;
    }

    // Anything solid is scenery unless it is somebody. The person tests look up the hierarchy because an
    // NPC's collider may sit on the body while the behaviours sit on the root, and inactive parents count
    // — a crowd member the director has frozen is still a person, not a wall.
    static bool Classify(Collider2D c)
    {
        Transform t = c.transform;

        // A trigger is bookkeeping and never a wall — unless it is one of the few volumes put down to say
        // "keep the crowd off this ground", which is a popup garage's floor and nothing else so far. The
        // marker sits on the same object as its collider, so this is a component lookup rather than a walk.
        if (c.isTrigger) return c.GetComponent<PaddockNoGo>() != null;

        if (t.GetComponentInParent<NPCLayeredAppearance>(true) != null) return false;
        if (t.GetComponentInParent<PaddockWalker>(true) != null) return false;
        if (t.GetComponentInParent<NPCInteractable>(true) != null) return false;
        if (t.GetComponentInParent<CrowdActor>(true) != null) return false;
        if (t.GetComponentInParent<OnFootController>(true) != null) return false;
        if (t.GetComponentInParent<MovementOnFoot>(true) != null) return false;
        return true;
    }
}
