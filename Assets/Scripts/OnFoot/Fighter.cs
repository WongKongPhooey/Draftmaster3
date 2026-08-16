using System.Collections;
using UnityEngine;
using Draftmaster.Fights;

// One character in a paddock scrap: their composure (the health bar), the moves they can throw, and what
// happens when one lands on them. Bolted onto the player and onto the rival driver for the length of a
// fight by DriverFight, then removed — nothing carries a Fighter around outside of one.
//
// Deliberately unbloody: a landed move costs composure and knocks the target back a step. At zero composure
// a fighter is winded and stops swinging; nobody is ever hurt on screen, and the crews break it up long
// before that reads as a beating (see DriverFight.Breakup).
//
// Two rigs have to be supported. The player and PitLaneStart's NPCs are TaylorEmerson clones with an
// Animator that already owns a "Pushing" state (Assets/Animation/OnFoot/Push.anim); paddock drivers are
// paper-doll NPCLayeredAppearance rigs with only a walk cycle. Animator rigs play the real clip; paper-doll
// rigs get a scripted lunge instead, which reads as a shove without any new art.
[DisallowMultipleComponent]
public class Fighter : MonoBehaviour
{
    [Tooltip("Name shown over this fighter's health bar.")]
    public string displayName = "Driver";
    [Tooltip("Roster Aggression (1-20). Scales damage dealt and, for AI fighters, how often they come forward.")]
    public int aggression = 10;
    [Tooltip("The human is fighting this one — DriverFight reads their attack input and the on-foot controller owns their movement.")]
    public bool isPlayerControlled;

    [Header("Reach")]
    [Tooltip("How far a move reaches (m). The on-foot characters are ~0.6m tall, so arm's length is short.")]
    public float reach = 1.25f;
    [Tooltip("How square-on you have to be for a move to land: dot(facing, toTarget) must clear this. 1 = dead ahead only, 0 = anywhere in front.")]
    [Range(-1f, 1f)] public float minFacingDot = 0.25f;

    [Header("Timing")]
    [Tooltip("Seconds from starting a move to the moment it can connect.")]
    public float windupSeconds = 0.14f;
    [Tooltip("Seconds after the hit before this fighter can act again.")]
    public float recoverySeconds = 0.36f;
    [Tooltip("Seconds a fighter is rocked back after being hit — no moves, no walking away.")]
    public float staggerSeconds = 0.32f;

    [Header("Feel")]
    [Tooltip("How far a landed move knocks the target back (m).")]
    public float knockbackDistance = 0.4f;
    [Tooltip("How far a paper-doll fighter lunges forward on a move (m). Animator rigs use their own clip instead.")]
    public float lungeDistance = 0.3f;
    [Tooltip("Tint flashed over the target when a move lands. Kept to a pale flash — no blood, no injury art.")]
    public Color hitFlashColor = new Color(1f, 0.65f, 0.45f, 1f);
    [Tooltip("Seconds the hit flash lasts.")]
    public float hitFlashSeconds = 0.16f;

    [Header("Animator rigs")]
    [Tooltip("Animator state that plays the push. Left as authored in PlayerOnFoot.controller.")]
    public string pushStateName = "Pushing";
    [Tooltip("Animator state to return to when the move is done (the walk/idle blend tree).")]
    public string movementStateName = "Movement";

    // Raised whenever composure changes: (fighter, health, maxHealth). FightHealthBar listens.
    public event System.Action<Fighter, float, float> HealthChanged;
    // Raised the first time this fighter runs out of composure.
    public event System.Action<Fighter> Winded;

    public float Health { get; private set; } = FightRules.MaxHealth;
    public bool IsWinded => Health <= 0.01f;
    public Fighter Opponent { get; set; }

    // True while a move is in flight or the fighter is rocked back — nothing else may be started.
    public bool Busy => _acting || _staggered;
    // Set by DriverFight once the crews arrive: fighters stop swinging but stay standing.
    public bool AttacksLocked { get; set; }

    Rigidbody2D _rb;
    Animator _animator;
    NPCLayeredAppearance _appearance;
    OnFootController _controller;
    SpriteRenderer[] _renderers;
    Color[] _baseColors;
    bool _acting, _staggered;
    Vector3 _lunge, _appliedLunge;
    int _pushStateHash, _movementStateHash;
    bool _hasPushState, _hasMovementState;
    float _animatorSpeedBefore = 1f;

    // Attach a fighter to an existing character. Everything it needs is discovered from the object, so the
    // same call works for the player, a paper-doll paddock driver, or a prefab-clone NPC.
    public static Fighter Attach(GameObject who, string name, int aggression, bool playerControlled)
    {
        if (who == null) return null;
        var f = who.GetComponent<Fighter>();
        if (f == null) f = who.AddComponent<Fighter>();
        f.displayName = string.IsNullOrEmpty(name) ? who.name : name;
        f.aggression = Mathf.Clamp(aggression, 1, 20);
        f.isPlayerControlled = playerControlled;
        return f;
    }

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
        _appearance = GetComponent<NPCLayeredAppearance>();
        _controller = GetComponent<OnFootController>();

        _renderers = FightMotion.Renderers(gameObject);
        _baseColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++) _baseColors[i] = _renderers[i].color;

        if (_animator != null && _animator.runtimeAnimatorController != null)
        {
            _pushStateHash = Animator.StringToHash(pushStateName);
            _movementStateHash = Animator.StringToHash(movementStateName);
            _hasPushState = _animator.HasState(0, _pushStateHash);
            _hasMovementState = _animator.HasState(0, _movementStateHash);
            _animatorSpeedBefore = _animator.speed;
            // NPC clones are parked with animator.speed = 0 so their walk cycle can't treadmill; a fighter
            // needs the rig running again or the push clip would never advance a frame.
            if (_hasPushState) _animator.speed = 1f;
        }
    }

    void OnDestroy()
    {
        RestoreColors();
        if (_animator != null && _animator.runtimeAnimatorController != null)
        {
            if (_hasMovementState) _animator.Play(_movementStateHash, 0, 0f);
            _animator.speed = _animatorSpeedBefore;
        }
    }

    // The scripted lunge is applied as an offset on top of whatever moved the body this frame (the on-foot
    // controller's velocity, or the fight AI's own steps), so the two never fight over transform.position.
    void LateUpdate()
    {
        // OnFootController parks the Animator (speed 0) whenever the player is standing still, so a push
        // thrown from a standstill would freeze on its first frame. Hold the rig running for as long as the
        // move lasts — LateUpdate runs after the controller's FixedUpdate, so this is the value that sticks.
        if (_acting && _animator != null && _hasPushState) _animator.speed = 1f;

        if (_lunge == _appliedLunge) return;
        FightMotion.Move(transform, _rb, _lunge - _appliedLunge);
        _appliedLunge = _lunge;
    }

    // ---------------------------------------------------------------- moves

    // Throw a move at the current opponent. Returns false if the fighter is mid-move, staggered, winded or
    // locked out by the crews arriving.
    public bool TryThrow(FightMove move)
    {
        if (AttacksLocked || Busy || IsWinded || Opponent == null) return false;
        StartCoroutine(ThrowRoutine(move));
        return true;
    }

    IEnumerator ThrowRoutine(FightMove move)
    {
        _acting = true;

        // Square up first: a move that lands sideways looks like an accident.
        if (Opponent != null)
        {
            Vector2 toTarget = Opponent.transform.position - transform.position;
            FightMotion.Face(transform, _rb, toTarget);
        }

        PlayMoveVisual(move);

        yield return new WaitForSeconds(windupSeconds);

        if (Opponent != null && !Opponent.IsWinded)
        {
            Vector3 delta = Opponent.transform.position - transform.position;
            delta.z = 0f;
            float distance = delta.magnitude;
            Vector2 facing = FightMotion.Forward(transform);
            float dot = distance > 0.0001f ? Vector2.Dot(facing, (Vector2)(delta / distance)) : 1f;

            if (FightRules.Connects(distance, reach, dot, minFacingDot))
            {
                float damage = FightRules.Damage(move, aggression, Random.value);
                Opponent.TakeHit(damage, distance > 0.0001f ? (Vector2)(delta / distance) : facing);
            }
        }

        yield return new WaitForSeconds(recoverySeconds);
        EndLunge();
        _acting = false;
    }

    // Animator rigs play the authored push clip; paper-doll rigs lunge instead. Both read as a shove from
    // the top-down camera, which is what keeps this working before the hook animations exist.
    void PlayMoveVisual(FightMove move)
    {
        if (_animator != null && _hasPushState)
        {
            _animator.speed = 1f;
            _animator.Play(_pushStateHash, 0, 0f);
            return;
        }

        _lunge = (Vector3)FightMotion.Forward(transform) * lungeDistance;
        // A hook throws the shoulder across rather than straight in, so bias the lunge sideways.
        if (move != FightMove.Shove)
        {
            Vector2 forward = FightMotion.Forward(transform);
            Vector2 side = new Vector2(-forward.y, forward.x) * (move == FightMove.LeftHook ? 1f : -1f);
            _lunge += (Vector3)(side * lungeDistance * 0.4f);
        }
        if (_appearance != null) _appearance.SetFrame(1); // mid-stride pose reads as a body committed forward
    }

    void EndLunge()
    {
        _lunge = Vector3.zero;
        if (_animator != null && _hasPushState && _hasMovementState) _animator.Play(_movementStateHash, 0, 0f);
        if (_appearance != null) _appearance.SetFrame(0);
    }

    // ---------------------------------------------------------------- taking one

    public void TakeHit(float damage, Vector2 fromDirection)
    {
        if (IsWinded) return;

        bool wasStanding = Health > 0f;
        Health = FightRules.ApplyDamage(Health, damage);
        HealthChanged?.Invoke(this, Health, FightRules.MaxHealth);

        StartCoroutine(StaggerRoutine(fromDirection));

        if (wasStanding && IsWinded) Winded?.Invoke(this);
    }

    // Rocked back: the fighter slides away from the blow, can't act, and flashes. The player's own movement
    // is locked for the same beat so a held key doesn't cancel the reaction.
    IEnumerator StaggerRoutine(Vector2 fromDirection)
    {
        _staggered = true;
        bool lockedController = false;
        if (_controller != null && !_controller.MovementLocked)
        {
            _controller.MovementLocked = true;
            lockedController = true;
        }

        Flash(true);

        float elapsed = 0f;
        Vector3 step = (Vector3)(fromDirection.normalized * knockbackDistance);
        while (elapsed < staggerSeconds)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            // Ease the knock-back out over the stagger rather than teleporting on the frame of the hit.
            FightMotion.Move(transform, _rb, step * (dt / Mathf.Max(0.0001f, staggerSeconds)));
            if (elapsed >= hitFlashSeconds) Flash(false);
            yield return null;
        }

        Flash(false);
        // Don't hand movement back if the crews arrived mid-stagger — the director is walking them away now
        // and owns the lock until the fight is over.
        if (lockedController && _controller != null && !AttacksLocked) _controller.MovementLocked = false;
        _staggered = false;
    }

    void Flash(bool on)
    {
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            _renderers[i].color = on ? hitFlashColor : _baseColors[i];
        }
    }

    void RestoreColors()
    {
        if (_renderers == null || _baseColors == null) return;
        for (int i = 0; i < _renderers.Length && i < _baseColors.Length; i++)
            if (_renderers[i] != null) _renderers[i].color = _baseColors[i];
    }

    // ---------------------------------------------------------------- helpers used by the director

    public Rigidbody2D Body => _rb;
    public NPCLayeredAppearance Appearance => _appearance;
    public OnFootController Controller => _controller;

    // Turn to look at the opponent. Called every frame during a fight so both fighters stay squared up.
    public void FaceOpponent()
    {
        if (Opponent == null) return;
        Vector2 to = Opponent.transform.position - transform.position;
        FightMotion.Face(transform, _rb, to);
    }
}
