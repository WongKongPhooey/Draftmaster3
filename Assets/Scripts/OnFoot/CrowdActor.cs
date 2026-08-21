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
[AddComponentMenu("")]
[DisallowMultipleComponent]
public class CrowdActor : MonoBehaviour
{
    // Every crowd member in the scene. The director walks this instead of doing its own searches.
    public static readonly List<CrowdActor> All = new();

    [Tooltip("Behaviours switched off when this NPC is frozen. Collected automatically at Awake — every " +
             "MonoBehaviour on the object except this one, the appearance and any speech bubble.")]
    public List<Behaviour> managed = new();

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

    public CrowdLod Lod => _lod;

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
