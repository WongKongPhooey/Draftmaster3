using UnityEngine;
using UnityEngine.InputSystem;

// Top-down Hotline-Miami-style walking player. Gamepad + keyboard. Interact with nearby NPCs.
[RequireComponent(typeof(Rigidbody2D))]
public class OnFootController : MonoBehaviour
{
    [Tooltip("Walk speed in units/sec.")]
    public float moveSpeed = 3.5f;
    [Tooltip("How fast the body rotates to face movement direction (deg/sec).")]
    public float turnRate = 720f;
    [Tooltip("Rotate sprite to face walking direction. Off for fixed-facing sprites. Auto-disabled when the Animator has Horizontal/Vertical params (directional anims own the facing).")]
    public bool faceMoveDirection = true;
    [Tooltip("Rotation added to the movement angle so the sprite's drawn facing lines up. TaylorEmerson faces -Y, so +90.")]
    public float spriteFacingOffsetDeg = 90f;
    [Tooltip("Stick input below this magnitude is ignored — kills drift-induced sliding.")]
    [Range(0f, 0.4f)] public float stickDeadzone = 0.15f;

    Rigidbody2D _rb;
    Animator _animator;
    NPCInteractable _activeNpc;
    bool _interactHeldPrev;
    bool _hasHorizontal, _hasVertical, _hasSpeed;

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

    }

    void FixedUpdate()
    {
        Vector2 move = ReadMove();

        // Lock movement while mid-conversation.
        if (_activeNpc != null && _activeNpc.IsTalking) move = Vector2.zero;

        _rb.linearVelocity = move * moveSpeed;

        if (faceMoveDirection && move.sqrMagnitude > 0.01f)
        {
            float ang = Mathf.Atan2(move.y, move.x) * Mathf.Rad2Deg + spriteFacingOffsetDeg;
            float z = Mathf.MoveTowardsAngle(_rb.rotation, ang, turnRate * Time.fixedDeltaTime);
            _rb.MoveRotation(z);
        }

        if (_animator != null)
        {
            if (_hasHorizontal) _animator.SetFloat("Horizontal", move.x);
            if (_hasVertical) _animator.SetFloat("Vertical", move.y);
            if (_hasSpeed) _animator.SetFloat("Speed", move.sqrMagnitude);
            // Belt and braces: pause the rig while standing so the walk cycle can't treadmill in place.
            _animator.speed = move.sqrMagnitude > 0.0001f ? 1f : 0f;
        }
    }

    void Update()
    {
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
                if (npc != null) { _activeNpc = npc; npc.Interact(); }
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
        Vector2 m = Vector2.zero;
        var gp = Gamepad.current;
        if (gp != null)
        {
            m = gp.leftStick.ReadValue();
            if (m.magnitude < stickDeadzone) m = Vector2.zero; // drift guard
        }
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) m.x = -1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) m.x = 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) m.y = 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) m.y = -1f;
        }
        return Vector2.ClampMagnitude(m, 1f);
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
