using System.Collections.Generic;
using UnityEngine;
using Draftmaster.Fights;

// A bystander who breaks up a fight. Runs over from wherever they were stood, wedges themselves between the
// two drivers, then walks one of them away until there's daylight between them — which is how a paddock
// scrap actually ends, and how this one ends too (DriverFight never resolves a fight by knocking anybody out).
//
// Whatever the NPC was doing before is switched off for the duration (wandering, autograph hunting, being
// talkable) and switched back on when they're done, so a peacemaker returns to being ordinary scenery.
public class FightPeacemaker : MonoBehaviour
{
    public enum Phase { RunIn, Wedge, Escort, Return, Done }

    [Tooltip("Run speed while getting to the fight (m/s). Faster than a walk — they're breaking something up.")]
    public float runSpeed = 3.2f;
    [Tooltip("Walk speed while marching a driver away (m/s).")]
    public float escortSpeed = 1.4f;
    [Tooltip("Paper-doll walk frames per second while moving.")]
    public float frameRate = 9f;
    [Tooltip("Seconds spent stood between the two of them, arms out, before the escort starts.")]
    public float wedgeSeconds = 0.7f;
    [Tooltip("How far the escorted driver ends up from where the fight was (m).")]
    public float separationDistance = 6f;
    [Tooltip("Space left between the peacemaker's front and the escorted driver's back (m). 0 = touching, " +
             "negative presses them together. The hold distance itself comes from both bodies' sizes.")]
    public float contactGap = 0f;
    [Tooltip("Half a body's front-to-back depth (m) when it has no collider to measure.")]
    public float fallbackHalfDepth = 0.15f;
    [Tooltip("Clearance (m) kept from walls and buildings while choosing and walking the escort route.")]
    public float escortClearance = 0.25f;
    [Tooltip("How close (m) counts as having reached a spot.")]
    public float arriveRadius = 0.35f;

    // Raised once this peacemaker has finished separating their fighter (before the walk back).
    public event System.Action<FightPeacemaker> Separated;

    public Phase Current { get; private set; } = Phase.RunIn;
    // True from the moment they're physically between the fighters — DriverFight stops the swinging then.
    public bool InPosition => Current == Phase.Wedge || Current == Phase.Escort || Current == Phase.Done;

    // The other escort in this breakup (set by DriverFight), so the two drivers are walked different ways.
    public FightPeacemaker Partner { get; set; }
    // Which way this one is walking their driver; zero until the escort starts.
    public Vector2 EscortHeading { get; private set; }

    Fighter _target;          // the fighter this one walks away (null = extra body, just gets in the way)
    Vector3 _fightCentre;
    Vector3 _wedgePoint;
    Vector3 _origin;
    float _wedgeTimer;
    float _frameTimer;
    int _frame;
    float _targetFrameTimer;    // the escorted fighter's own walk cycle, stepped separately from ours
    int _targetFrame;
    bool _separated;
    float _escortLength;      // how far this escort walks: the full separation, or less if cornered
    float _escortWalked;
    float _holdDistance;
    bool _fenced;             // started inside the paddock fence, so must stay inside it

    Rigidbody2D _rb;
    NPCLayeredAppearance _appearance;
    readonly List<Behaviour> _suspended = new();

    // Take an ordinary NPC and send them in. target may be null for a third body that only gets in the way.
    public static FightPeacemaker Send(GameObject npc, Fighter target, Vector3 fightCentre, Vector3 wedgePoint)
    {
        if (npc == null) return null;
        var pm = npc.GetComponent<FightPeacemaker>();
        if (pm == null) pm = npc.AddComponent<FightPeacemaker>();
        pm._target = target;
        pm._fightCentre = fightCentre;
        pm._wedgePoint = wedgePoint;
        pm._origin = npc.transform.position;
        pm.Current = Phase.RunIn;
        pm.Suspend();
        return pm;
    }

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _appearance = GetComponent<NPCLayeredAppearance>();
    }

    // Switch off whatever was driving this NPC, remembering it so it can be switched back on.
    void Suspend()
    {
        _suspended.Clear();
        Stash(GetComponent<PaddockWalker>());
        Stash(GetComponent<AutographFan>());
        Stash(GetComponent<NPCInteractable>());   // also clears its floating prompt via OnDisable
        Stash(GetComponent<NPCAmbientChatter>());
    }

    void Stash(Behaviour b)
    {
        if (b == null || !b.enabled) return;
        b.enabled = false;
        _suspended.Add(b);
    }

    void Restore()
    {
        foreach (var b in _suspended) if (b != null) b.enabled = true;
        _suspended.Clear();
    }

    void Update()
    {
        switch (Current)
        {
            case Phase.RunIn: StepRunIn(); break;
            case Phase.Wedge: StepWedge(); break;
            case Phase.Escort: StepEscort(); break;
            case Phase.Return: StepReturn(); break;
        }
    }

    void StepRunIn()
    {
        if (FightMotion.WalkToward(transform, _rb, _wedgePoint, runSpeed, arriveRadius,
                                   _appearance, ref _frameTimer, ref _frame, frameRate))
        {
            Current = Phase.Wedge;
            _wedgeTimer = wedgeSeconds;
        }
    }

    // Stood between them with their back to one and their front to the other — the classic "leave it" pose,
    // faked here by facing the fighter they're about to walk away.
    void StepWedge()
    {
        if (_target != null)
            FightMotion.Face(transform, _rb, (Vector2)(_target.transform.position - transform.position));

        _wedgeTimer -= Time.deltaTime;
        if (_wedgeTimer > 0f) return;

        if (_target == null) { Finish(); return; }   // extra body: job done once the fight has stopped
        BeginEscort();
    }

    // Pick the route once, before setting off. Straight away from the fight is the natural way, but in a
    // packed paddock that is often the side of a motorhome — so the way out is whichever direction near it
    // has the most open ground, steered away from the other escort's route.
    void BeginEscort()
    {
        Vector2 from = TargetPosition();
        Vector2 outward = from - (Vector2)_fightCentre;
        Vector2 avoid = Partner != null ? Partner.EscortHeading : Vector2.zero;
        // A fight that drifted onto the fence line can't be held to it, or every route would read as blocked.
        _fenced = PaddockBoundary.AnyActive && PaddockBoundary.IsInside(from);

        EscortHeading = FightRules.ChooseEscortHeading(outward, dir => ClearRun(from, dir, separationDistance),
                                                       separationDistance, avoid);
        _escortLength = ClearRun(from, EscortHeading, separationDistance);
        _escortWalked = 0f;
        _holdDistance = FightRules.EscortHoldDistance(HalfDepth(_target.gameObject), HalfDepth(gameObject), contactGap);
        Current = Phase.Escort;
    }

    // Read from the body when there is one: PlaceAt writes rb.position, and the transform only catches up on
    // the next physics step, so a frame without one would read the old spot and the escort would stall.
    Vector2 TargetPosition() => _target.Body != null ? _target.Body.position : (Vector2)_target.transform.position;

    // How far a body could walk from `from` along dir, up to max, before solid scenery or the paddock fence.
    float ClearRun(Vector2 from, Vector2 dir, float max)
    {
        const float step = 0.5f;
        Vector2 prev = from;
        for (float d = step; d <= max + 1e-3f; d += step)
        {
            Vector2 p = from + dir * d;
            if (!PaddockObstacles.PathClear(prev, p, escortClearance)) return d - step;
            if (_fenced && !PaddockBoundary.IsInside(p)) return d - step;
            prev = p;
        }
        return max;
    }

    // Half a character's front-to-back depth, from its collider. The on-foot art faces -transform.up, so
    // depth is along local y. Clamped to a person's size so an oversized legacy collider can't open a gap.
    float HalfDepth(GameObject go)
    {
        var col = go.GetComponent<Collider2D>();
        float scale = Mathf.Abs(go.transform.lossyScale.y);
        float half = fallbackHalfDepth;
        if (col is BoxCollider2D box) half = box.size.y * 0.5f * scale;
        else if (col is CircleCollider2D circle) half = circle.radius * Mathf.Max(Mathf.Abs(go.transform.lossyScale.x), scale);
        else if (col is CapsuleCollider2D capsule) half = capsule.size.y * 0.5f * scale;
        return Mathf.Clamp(half, 0.06f, 0.25f);
    }

    // March the driver away from the fight: the peacemaker walks behind them, chest to their back, and the
    // pair move off together along the route picked in BeginEscort. Anything solid that turns up on the way
    // is slid along rather than walked into; boxed in completely, they let go where they stand.
    void StepEscort()
    {
        if (_target == null) { Finish(); return; }
        if (_escortWalked >= _escortLength) { Finish(); return; }

        Vector2 heading = EscortHeading;
        Vector2 from = TargetPosition();
        float stepLen = Mathf.Min(escortSpeed * Time.deltaTime, _escortLength - _escortWalked);
        Vector2 wanted = from + heading * stepLen;

        if (!PaddockObstacles.TryStep(from, wanted, escortClearance, out Vector2 next)) { Finish(); return; }
        if (_fenced && !PaddockBoundary.IsInside(next)) { Finish(); return; }

        float moved = (next - from).magnitude;
        if (moved < 1e-5f) { Finish(); return; }
        _escortWalked += moved;
        Vector2 moveDir = (next - from) / moved;

        FightMotion.PlaceAt(_target.transform, _target.Body, next);
        FightMotion.Face(_target.transform, _target.Body, moveDir);
        FightMotion.StepFrames(_target.Appearance, ref _targetFrameTimer, ref _targetFrame, frameRate);

        Vector3 behind = next - moveDir * _holdDistance;
        FightMotion.PlaceAt(transform, _rb, behind);
        FightMotion.Face(transform, _rb, moveDir);
    }

    void StepReturn()
    {
        if (FightMotion.WalkToward(transform, _rb, _origin, escortSpeed, arriveRadius,
                                   _appearance, ref _frameTimer, ref _frame, frameRate))
        {
            Current = Phase.Done;
            Restore();
            Destroy(this);
        }
    }

    void Finish()
    {
        if (!_separated)
        {
            _separated = true;
            Separated?.Invoke(this);
        }
        Current = Phase.Return;
    }

    void OnDestroy() => Restore();
}
