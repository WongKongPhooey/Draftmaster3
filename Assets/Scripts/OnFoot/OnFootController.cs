using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Top-down Hotline-Miami-style walking player. Gamepad + keyboard. Interact with nearby NPCs.
[RequireComponent(typeof(Rigidbody2D))]
public class OnFootController : MonoBehaviour
{
    // Every walking body currently in the scene, kept the same way NPCInteractable.All and
    // CrowdActor.All are.
    //
    // "Is the player on foot, and where are they" is asked several times a frame — the objective
    // marker alone asked it around a dozen times, once per IMGUI event, on top of the phone, the
    // crowd director and the fan spawner polling for it. Each of those was a full-scene
    // FindFirstObjectByType, which walks every object in a paddock of hundreds of NPCs. Registering
    // here turns all of it into a list read.
    public static readonly List<OnFootController> All = new();

    // The walking body, or null while the player is in the car / in a menu. First one still alive,
    // matching the scene order FindFirstObjectByType used to hand back.
    public static OnFootController Current
    {
        get
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i] != null) return All[i];   // Unity null: destroyed this frame, OnDisable pending
            return null;
        }
    }

    [Tooltip("Walk speed in units/sec.")]
    public float moveSpeed = 3.5f;
    [Tooltip("Speed multiplier while the run modifier is held (Left Shift / L1). Walk animation plays faster by the same factor.")]
    public float runMultiplier = 2f;
    [Tooltip("How fast the body rotates to face movement direction (deg/sec).")]
    public float turnRate = 720f;
    [Tooltip("Rotate sprite to face walking direction. Off for fixed-facing sprites. Auto-disabled when the Animator has Horizontal/Vertical params (directional anims own the facing).")]
    public bool faceMoveDirection = true;
    [Tooltip("Rotation added to the movement angle so the sprite's drawn facing lines up. TaylorEmerson faces -Y, so +90.")]
    public float spriteFacingOffsetDeg = 90f;
    [Tooltip("Stick input below this magnitude is ignored — kills drift-induced sliding.")]
    [Range(0f, 0.4f)] public float stickDeadzone = 0.15f;
    [Tooltip("Crisp stop: once the stick falls to this fraction of its peak tilt, movement snaps to 0. Masks a stick whose axis returns to centre slowly (it can plateau then crawl). Lower = snaps sooner / crisper, but less room to ease off mid-walk.")]
    [Range(0.3f, 0.95f)] public float releaseFraction = 0.7f;
    [Tooltip("After a snap, the stick must be pushed back above this magnitude to move again. A slow-returning axis only decreases, so it can't re-trigger.")]
    [Range(0.1f, 0.95f)] public float reEngageThreshold = 0.3f;
    [Tooltip("Input actions asset (PlayerControl). Movement is read from the OnFoot/Movement action each frame. A private runtime copy is used so it can't clash with the car's InputManager.")]
    public InputActionAsset controlsAsset;

    // Scripted sequences (cutscenes) set this to freeze the player in place: movement zeroes, the
    // walk animation idles, prompts hide, and interact presses are swallowed. Facing is left alone
    // so the sequence can orient the player itself. Not serialized — runtime control only.
    [System.NonSerialized] public bool MovementLocked;

    Rigidbody2D _rb;
    Animator _animator;
    NPCInteractable _activeNpc;
    bool _interactHeldPrev;
    bool _hasHorizontal, _hasVertical, _hasSpeed;
    Vector2 _talkFacing;          // direction the player faces while mid-conversation
    InputActionAsset _controls;   // private clone of controlsAsset
    InputAction _moveAction;       // OnFoot/Movement on the clone
    bool _warnedNoAsset;
    float _prevMoveMag;            // last frame's input magnitude, for re-engage detection
    float _peakMag;                // highest magnitude since the current push began
    bool _stickReleased;           // latched true while the stick springs back

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.freezeRotation = true;
        _animator = GetComponent<Animator>();
        if (_animator != null)
        {
            foreach (var p in _animator.parameters)
            {
                if (p.name == "Horizontal") _hasHorizontal = true;
                else if (p.name == "Vertical") _hasVertical = true;
                else if (p.name == "Speed") _hasSpeed = true;
            }
        }

        // NOTE: the action is built lazily (see EnsureMoveAction), not here.
        // This component is usually added via AddComponent at runtime, so controlsAsset
        // is assigned by the spawner *after* Awake has already run.
    }

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
        _moveAction?.Enable();
    }

    void OnDisable()
    {
        All.Remove(this);
        _moveAction?.Disable();
    }

    // Builds a private clone of controlsAsset and grabs OnFoot/Movement. Cloning keeps our
    // enable/disable from clashing with the car's InputManager, which shares PlayerControl.
    void EnsureMoveAction()
    {
        if (_moveAction != null) return;
        if (controlsAsset == null)
        {
            if (!_warnedNoAsset)
            {
                Debug.LogError("OnFootController: controlsAsset not assigned — no movement input.", this);
                _warnedNoAsset = true;
            }
            return;
        }
        _controls = Instantiate(controlsAsset);
        _moveAction = _controls.FindActionMap("OnFoot")?.FindAction("Movement");
        if (_moveAction == null)
        {
            Debug.LogError("OnFootController: OnFoot/Movement action not found in controlsAsset.", this);
            return;
        }
        if (isActiveAndEnabled) _moveAction.Enable();
    }

    void FixedUpdate()
    {
        EnsureMoveAction();
        Vector2 move = ReadMove();

        // Lock movement while mid-conversation or while a cutscene holds the player.
        if (MovementLocked || (_activeNpc != null && _activeNpc.IsTalking)) move = Vector2.zero;

        bool running = move != Vector2.zero && ReadRunHeld();
        _rb.linearVelocity = move * moveSpeed * (running ? runMultiplier : 1f);

        // Keep the walker inside any authored PaddockBoundary: clamp the predicted next position
        // and re-derive velocity from it, which naturally slides along the polygon edge.
        if (PaddockBoundary.AnyActive)
        {
            if (!PaddockBoundary.IsInside(_rb.position))
                _rb.position = PaddockBoundary.Constrain(_rb.position); // spawned/pushed outside — pull back in
            Vector2 next = _rb.position + _rb.linearVelocity * Time.fixedDeltaTime;
            Vector2 clamped = PaddockBoundary.Constrain(next);
            if (clamped != next)
                _rb.linearVelocity = (clamped - _rb.position) / Time.fixedDeltaTime;
        }

        if (faceMoveDirection && move.sqrMagnitude > 0.01f)
        {
            float ang = Mathf.Atan2(move.y, move.x) * Mathf.Rad2Deg + spriteFacingOffsetDeg;
            float z = Mathf.MoveTowardsAngle(_rb.rotation, ang, turnRate * Time.fixedDeltaTime);
            _rb.MoveRotation(z);
        }

        if (_animator != null)
        {
            // While talking (or held by a cutscene), hold the facing direction so directional idle
            // rigs keep looking at the NPC instead of snapping to the zeroed move vector.
            Vector2 face = (MovementLocked || (_activeNpc != null && _activeNpc.IsTalking)) ? _talkFacing : move;
            if (_hasHorizontal) _animator.SetFloat("Horizontal", face.x);
            if (_hasVertical) _animator.SetFloat("Vertical", face.y);
            if (_hasSpeed) _animator.SetFloat("Speed", move.sqrMagnitude);
            // Belt and braces: pause the rig while standing so the walk cycle can't treadmill in place.
            // Same walk clip while running, just played faster.
            _animator.speed = move.sqrMagnitude > 0.0001f ? (running ? runMultiplier : 1f) : 0f;
        }
    }

    void Update()
    {
        // Mid-fight: the fight owns the action keys (DriverFight reads them directly) and nobody is
        // talkable while it's going on — but walking still works, so the player can circle and back off.
        if (DriverFight.IsActive)
        {
            for (int i = 0; i < NPCInteractable.All.Count; i++)
                NPCInteractable.All[i].BuildFloatingPrompt(false);
            ReadInteractPressed();
            return;
        }

        // Frozen by a cutscene: no prompts, no interactions. Interact state still ticks so a held
        // key doesn't fire the moment the lock lifts.
        if (MovementLocked)
        {
            for (int i = 0; i < NPCInteractable.All.Count; i++)
                NPCInteractable.All[i].BuildFloatingPrompt(false);
            ReadInteractPressed();
            return;
        }

        UpdateNearestPrompt();

        bool interact = ReadInteractPressed();
        if (interact)
        {
            if (_activeNpc != null && _activeNpc.IsTalking)
            {
                if (!_activeNpc.Interact()) _activeNpc = null; // conversation ended
            }
            else
            {
                var npc = NearestInRange();
                if (npc != null)
                {
                    npc.SetInteractor(transform);
                    FaceEachOther(npc);
                    _activeNpc = npc;
                    npc.Interact();
                }
            }
        }
    }

    // Cutscene entry point: start a conversation with the given NPC exactly as if the player had
    // pressed interact next to them — face each other, open the first line, and keep the player
    // planted until the conversation ends. Interact then advances the lines as normal.
    public void BeginConversation(NPCInteractable npc)
    {
        if (npc == null) return;
        npc.SetInteractor(transform);
        FaceEachOther(npc);
        _activeNpc = npc;
        npc.Interact();
    }

    // Turn the player to look at a world point and hold that facing while locked/talking.
    // Cutscenes use this to orient the player before anything else happens.
    public void FaceToward(Vector3 worldPoint)
    {
        Vector2 dir = (Vector2)(worldPoint - transform.position);
        if (dir.sqrMagnitude < 0.0001f) return;
        _talkFacing = dir.normalized;
        ApplyFacing(transform, _rb, dir, spriteFacingOffsetDeg);
    }

    // Turn the player and the NPC to look at each other as the conversation opens. Speakers whose
    // transform carries meaning (a driver sat in a parked car) opt out of being turned.
    void FaceEachOther(NPCInteractable npc)
    {
        Transform other = npc.transform;
        Vector2 toNpc = (Vector2)(other.position - transform.position);
        _talkFacing = toNpc.sqrMagnitude > 0.0001f ? toNpc.normalized : Vector2.down;
        ApplyFacing(transform, _rb, toNpc, spriteFacingOffsetDeg);
        if (npc.turnsToFace)
            ApplyFacing(other, other.GetComponent<Rigidbody2D>(), -toNpc, spriteFacingOffsetDeg);
    }

    // Shared facing snap: rotates the body (respecting rb constraints) and orients directional idle
    // rigs through their Horizontal/Vertical params. Static so cutscenes can face NPCs with it too.
    public static void ApplyFacing(Transform who, Rigidbody2D body, Vector2 dir, float facingOffsetDeg)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + facingOffsetDeg;
        if (body != null) body.MoveRotation(ang);          // respects rb constraints (freezeRotation/kinematic)
        else who.rotation = Quaternion.Euler(0f, 0f, ang);

        // Orient directional idle rigs too, if the animator exposes Horizontal/Vertical.
        var anim = who.GetComponent<Animator>();
        if (anim != null)
        {
            Vector2 n = dir.normalized;
            foreach (var p in anim.parameters)
            {
                if (p.name == "Horizontal") anim.SetFloat("Horizontal", n.x);
                else if (p.name == "Vertical") anim.SetFloat("Vertical", n.y);
            }
        }
    }

    void UpdateNearestPrompt()
    {
        var nearest = NearestInRange();
        for (int i = 0; i < NPCInteractable.All.Count; i++)
        {
            var npc = NPCInteractable.All[i];
            npc.BuildFloatingPrompt(npc == nearest && !npc.IsTalking);
        }
    }

    NPCInteractable NearestInRange()
    {
        Vector2 pos = transform.position;
        NPCInteractable best = null;
        float bestD = float.MaxValue;
        for (int i = 0; i < NPCInteractable.All.Count; i++)
        {
            var npc = NPCInteractable.All[i];
            if (!npc.InRange(pos)) continue;
            float d = Vector2.Distance(pos, npc.transform.position);
            if (d < bestD) { bestD = d; best = npc; }
        }
        return best;
    }

    Vector2 ReadMove()
    {
        if (_moveAction == null) return Vector2.zero;
        Vector2 m = _moveAction.ReadValue<Vector2>();
        if (m.magnitude < stickDeadzone) m = Vector2.zero; // drift guard (stick already deadzoned; keyboard is digital)
        m = Vector2.ClampMagnitude(m, 1f);

        // Crisp stop. A stick whose axis returns to centre slowly produces a coast: on release the
        // value drops then plateaus and crawls toward 0 (sometimes too slowly to read as "falling"
        // frame-to-frame). Compare against the peak tilt instead: once the stick has fallen to a
        // fraction of its peak, treat it as released and zero movement. Re-engage only when the
        // stick is actively pushed back up — a slow return only decreases, so it can't re-trigger.
        float mag = m.magnitude;
        const float eps = 0.03f;
        if (_stickReleased)
        {
            if (mag > _prevMoveMag + eps && mag >= reEngageThreshold) { _stickReleased = false; _peakMag = mag; }
        }
        else
        {
            if (mag > _peakMag) _peakMag = mag;
            if (_peakMag > stickDeadzone && mag < _peakMag * releaseFraction) _stickReleased = true;
        }
        _prevMoveMag = mag;
        return _stickReleased ? Vector2.zero : m;
    }

    // Run modifier: Left Shift on keyboard, L1/left shoulder on gamepad. Direct device read,
    // matching ReadInteractPressed — no action-map entry needed.
    bool ReadRunHeld()
    {
        var gp = Gamepad.current;
        if (gp != null && gp.leftShoulder.isPressed) return true;
        var kb = Keyboard.current;
        if (kb != null && kb.leftShiftKey.isPressed) return true;
        return false;
    }

    bool ReadInteractPressed()
    {
        bool held = false;
        var gp = Gamepad.current;
        if (gp != null) held |= gp.buttonSouth.isPressed;
        var kb = Keyboard.current;
        if (kb != null) held |= kb.eKey.isPressed || kb.spaceKey.isPressed;

        bool pressed = held && !_interactHeldPrev;
        _interactHeldPrev = held;
        return pressed;
    }
}
