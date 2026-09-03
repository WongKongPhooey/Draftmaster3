using System.Collections.Generic;
using Draftmaster.Crowd;
using UnityEngine;

// Marks an NPC as part of the background crowd and switches its behaviour and physics on and off as the
// CrowdDirector asks. Renderers are never touched: whatever level it is running at, the NPC is still
// drawn, still wearing its outfit, still stood where it was. Only the thinking stops.
//
// This is what lets the paddock be crowded. The expensive part of an NPC is not its sprite layers, it is
// the per-frame Update, the kinematic Rigidbody2D and the collider — and none of that buys anything for
// an NPC the player cannot currently see, hear or reach.
//
// A crowd member marked `recyclable` is also filler in the stronger sense: it is nobody in particular, so
// once it has wandered a long way off the director may pick it up and put it back down just out of shot
// with a new face on. See CrowdRecyclePolicy.
[AddComponentMenu("")]
[DisallowMultipleComponent]
public class CrowdActor : MonoBehaviour
{
    // Every crowd member in the scene. The director walks this instead of doing its own searches.
    public static readonly List<CrowdActor> All = new();

    [Tooltip("Behaviours switched off when this NPC is frozen. Collected automatically at Awake — every " +
             "MonoBehaviour on the object except this one, the appearance and any speech bubble.")]
    public List<Behaviour> managed = new();

    [Tooltip("This NPC is anonymous filler: the director may move it back to just out of shot once it " +
             "drifts past the recycle radius, keeping the crowd packed around wherever the player is. " +
             "Leave off for anyone the player might go looking for — conversational NPCs, quest givers, " +
             "drivers, reps, anyone placed by hand.")]
    public bool recyclable;

    [Tooltip("Roll a new outfit when this NPC is recycled, so the one that walks back into shot reads as " +
             "a different person rather than the same one teleported.")]
    public bool rerollOnRecycle = true;

    // The subset of `managed` that only matters with the player within a couple of metres (ambient
    // chatter, conversations). Off at Reduced as well as Frozen.
    readonly List<Behaviour> _proximity = new();
    // Enabled state as authored, so waking an NPC never switches on something that was off by design.
    readonly List<bool> _wasEnabled = new();

    Collider2D[] _colliders;
    bool[] _colliderWasEnabled;
    Rigidbody2D _rb;
    NPCLayeredAppearance _appearance;
    NPCInteractable _talk;

    CrowdLod _lod = CrowdLod.Full;
    bool _applied;
    CrowdRect _recycleArea;

    public CrowdLod Lod => _lod;

    // Where a recycled NPC is allowed to be put back down. Set by whatever spawned it — the crowd
    // module has no idea what shape a paddock is, and an NPC dropped outside one would be standing on
    // the racetrack.
    public CrowdRect RecycleArea => _recycleArea;
    public void SetRecycleArea(in CrowdRect area) => _recycleArea = area;

    void Awake()
    {
        _appearance = GetComponent<NPCLayeredAppearance>();
        _talk = GetComponent<NPCInteractable>();
        _rb = GetComponent<Rigidbody2D>();
        _colliders = GetComponents<Collider2D>();
        _colliderWasEnabled = new bool[_colliders.Length];
        for (int i = 0; i < _colliders.Length; i++) _colliderWasEnabled[i] = _colliders[i].enabled;

        if (managed.Count == 0) Collect();
        CacheEnabledStates();
    }

    // Auto-collect at Awake so a spawner only has to AddComponent<CrowdActor>() and anything bolted on
    // later (a quest hook, a new wander script) is governed without a second edit here.
    void Collect()
    {
        var all = GetComponents<MonoBehaviour>();
        foreach (var mb in all)
        {
            if (mb == null || mb == this) continue;
            if (mb is NPCLayeredAppearance) continue;   // owns the renderers — must keep working
            if (mb is SpeechBubble) continue;           // its own lifetime; silenced via the chatter
            managed.Add(mb);
        }
    }

    void CacheEnabledStates()
    {
        _wasEnabled.Clear();
        _proximity.Clear();
        for (int i = 0; i < managed.Count; i++)
        {
            var b = managed[i];
            _wasEnabled.Add(b != null && b.enabled);
            if (b is NPCAmbientChatter || b is NPCInteractable) _proximity.Add(b);
        }
    }

    void OnEnable()
    {
        All.Add(this);
        CrowdDirector.EnsureExists();
    }

    void OnDisable() => All.Remove(this);

    // Flat (XY) distance from this NPC to a point. The z axis is a sorting plane here, not depth, so
    // including it would make an NPC nudged toward the camera look further away than it is.
    public float DistanceTo(Vector2 p)
    {
        Vector3 t = transform.position;
        float dx = t.x - p.x, dy = t.y - p.y;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    // Never drop an NPC below Full while it is mid-conversation: disabling NPCInteractable ends the
    // conversation and destroys the bubbles out from under the player. In practice a talker is within
    // interact range so it would be Full anyway, but a cutscene can walk one away mid-sentence.
    public bool IsBusy => _talk != null && _talk.IsTalking;

    // Whether this one is fair game for the recycler. Anything the player is mid-conversation with is
    // not, however far away the conversation has wandered.
    public bool CanRecycle => recyclable && _recycleArea.IsValid && !IsBusy;

    // Take this NPC out of where it was and put it back down at `point`, as somebody else.
    //
    // Nothing here is visible: the caller only picks points that are off screen, and the NPC it is
    // moving was a hundred metres away. What the player sees is a paddock that stays busy wherever they
    // walk instead of thinning out at the ends.
    public void RecycleTo(Vector2 point)
    {
        var t = transform;
        Vector3 pos = t.position;
        t.position = new Vector3(point.x, point.y, pos.z);   // z is the sorting plane — leave it alone

        // A kinematic Rigidbody2D holds its own pose. Moving the transform without it leaves the body
        // behind, and the walker's next MovePosition would drag the NPC straight back across the paddock.
        if (_rb != null) _rb.position = point;

        // A fresh outfit is what makes this read as a new face rather than the same person teleported.
        // Frames come out of NPCSpriteCache, so a rebuild is a handful of GameObjects, not a re-slice.
        if (rerollOnRecycle && _appearance != null && _appearance.Built && !_appearance.Build())
        {
            // Only reachable if the part library has gone away since the first build. Stop trying rather
            // than keep producing NPCs with no renderers on them.
            rerollOnRecycle = false;
            Debug.LogWarning($"CrowdActor: could not rebuild an outfit for '{name}' on recycle — " +
                             "rerolling disabled for this NPC.", this);
        }

        // Whoever owns the wandering picks a fresh route from where it now stands.
        for (int i = 0; i < managed.Count; i++)
            if (managed[i] is ICrowdRecyclable r) r.OnRecycled();
    }

    public void Apply(CrowdLod lod)
    {
        if (IsBusy) lod = CrowdLod.Full;
        if (_applied && lod == _lod) return;
        _lod = lod;
        _applied = true;

        for (int i = 0; i < managed.Count; i++)
        {
            var b = managed[i];
            if (b == null) continue;
            if (!_wasEnabled[i]) continue;                       // authored off: leave it off
            bool on = CrowdPolicy.RunsAt(lod, _proximity.Contains(b));
            if (b.enabled != on) b.enabled = on;
        }

        // Physics: a kinematic body still sits in the broadphase and its collider still gets tested
        // against the player's every fixed step. Frozen NPCs are scenery, so take them out of the
        // simulation entirely and put them back exactly as they were.
        bool physicsOn = CrowdPolicy.PhysicsRunsAt(lod);
        if (_rb != null && _rb.simulated != physicsOn) _rb.simulated = physicsOn;
        for (int i = 0; i < _colliders.Length; i++)
        {
            var c = _colliders[i];
            if (c == null || !_colliderWasEnabled[i]) continue;
            if (c.enabled != physicsOn) c.enabled = physicsOn;
        }

        // Park on the standing pose so a frozen NPC reads as somebody stood in the paddock rather than
        // a body caught mid-stride. Renderers are never touched, so the outfit and position are exactly
        // as they were — the NPC is still there, it has just stopped thinking.
        // Unity null, not C# null: the appearance is torn off again when the part library has nothing to
        // build from, and a destroyed component is still a live C# reference.
        if (CrowdPolicy.StandsStillAt(lod) && _appearance != null) _appearance.SetFrame(0);
    }
}

// Implemented by a behaviour on a recyclable crowd NPC that holds state tied to where it was standing —
// a wander route, a home spot. Called by CrowdActor.RecycleTo once the NPC has been moved, so the
// behaviour can start again from the new position instead of walking back to the old one.
public interface ICrowdRecyclable
{
    void OnRecycled();
}
