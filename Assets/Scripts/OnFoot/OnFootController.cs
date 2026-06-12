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
    [Tooltip("Rotate sprite to face walking direction. Off for fixed-facing sprites.")]
    public bool faceMoveDirection = true;
    [Tooltip("Sprite faces +Y by default. Set false if it faces +X.")]
    public bool spriteFacesUp = true;

    Rigidbody2D _rb;
    Animator _animator;
    NPCInteractable _activeNpc;
    bool _interactHeldPrev;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.freezeRotation = true;
        _animator = GetComponent<Animator>();
    }

    void FixedUpdate()
    {
        Vector2 move = ReadMove();

        // Lock movement while mid-conversation.
        if (_activeNpc != null && _activeNpc.IsTalking) move = Vector2.zero;

        _rb.linearVelocity = move * moveSpeed;

        if (faceMoveDirection && move.sqrMagnitude > 0.01f)
        {
            float ang = Mathf.Atan2(move.y, move.x) * Mathf.Rad2Deg + (spriteFacesUp ? -90f : 0f);
            float z = Mathf.MoveTowardsAngle(_rb.rotation, ang, turnRate * Time.fixedDeltaTime);
            _rb.MoveRotation(z);
        }

        if (_animator != null)
        {
            _animator.SetFloat("Horizontal", move.x);
            _animator.SetFloat("Vertical", move.y);
            _animator.SetFloat("Speed", move.sqrMagnitude);
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
        if (gp != null) m = gp.leftStick.ReadValue();
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
