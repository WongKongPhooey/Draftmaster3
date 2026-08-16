using System.Collections.Generic;
using UnityEngine;

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
    [Tooltip("How far in front of the peacemaker the escorted driver is held (m).")]
    public float holdDistance = 0.75f;
    [Tooltip("How close (m) counts as having reached a spot.")]
    public float arriveRadius = 0.35f;

    // Raised once this peacemaker has finished separating their fighter (before the walk back).
    public event System.Action<FightPeacemaker> Separated;

    public Phase Current { get; private set; } = Phase.RunIn;
    // True from the moment they're physically between the fighters — DriverFight stops the swinging then.
    public bool InPosition => Current == Phase.Wedge || Current == Phase.Escort || Current == Phase.Done;

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
        Current = Phase.Escort;
    }

    // March the driver away from the fight: the peacemaker walks outward and the fighter is held just in
    // front of them, so the pair move off together rather than the NPC walking through them.
    void StepEscort()
    {
        if (_target == null) { Finish(); return; }

        Vector3 outward = _target.transform.position - _fightCentre;
        outward.z = 0f;
        if (outward.sqrMagnitude < 0.0001f) outward = Vector3.up;
        outward.Normalize();

        float travelled = Vector3.Distance(new Vector3(_target.transform.position.x, _target.transform.position.y, 0f),
                                           new Vector3(_fightCentre.x, _fightCentre.y, 0f));
        if (travelled >= separationDistance) { Finish(); return; }

        Vector3 step = outward * (escortSpeed * Time.deltaTime);
        Vector3 nextFighter = _target.transform.position + step;
        if (PaddockBoundary.AnyActive)
        {
            Vector2 clamped = PaddockBoundary.Constrain(nextFighter);
            // Walked into the paddock fence: far enough, let them go.
            if (((Vector2)nextFighter - clamped).sqrMagnitude > 0.0001f) { Finish(); return; }
        }

        FightMotion.PlaceAt(_target.transform, _target.Body, nextFighter);
        FightMotion.Face(_target.transform, _target.Body, (Vector2)outward);
        FightMotion.StepFrames(_target.Appearance, ref _targetFrameTimer, ref _targetFrame, frameRate);

        Vector3 behind = nextFighter - outward * holdDistance;
        FightMotion.PlaceAt(transform, _rb, behind);
        FightMotion.Face(transform, _rb, (Vector2)outward);
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
