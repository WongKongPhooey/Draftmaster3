using UnityEngine;

// A small scripted cutscene: freeze the player, slide the cinematic bars in, walk an NPC over to
// them, turn the pair to face each other, and open the NPC's dialogue. From there the normal
// conversation flow takes over (interact advances lines, bars stay in while IsTalking); when the
// conversation ends everything is released.
//
// Fired via Play() — typically from a CutsceneTrigger the player walks over, but any code can call
// it. Configured entirely from the spawner that creates it, so no scene wiring is needed.
public class NPCWalkUpCutscene : MonoBehaviour
{
    [Tooltip("The on-foot player. Locked in place while the NPC walks over.")]
    public OnFootController player;
    [Tooltip("The NPC who walks up and speaks. Its NPCInteractable lines are the cutscene dialogue.")]
    public NPCInteractable npc;
    [Tooltip("NPC approach speed (units/sec).")]
    public float walkSpeed = 2.2f;
    [Tooltip("The NPC stops this far (m) from the player and starts talking.")]
    public float stopDistance = 1.2f;
    [Tooltip("How fast the NPC rotates to face its walking direction (deg/sec).")]
    public float turnRate = 720f;
    [Tooltip("Rotation added to the movement angle so the sprite's drawn facing lines up (walk art faces -Y, so +90).")]
    public float spriteFacingOffsetDeg = 90f;

    enum Phase { Idle, WalkUp, Talking, Done }
    Phase _phase = Phase.Idle;
    Rigidbody2D _npcRb;
    Animator _npcAnim;
    bool _hasDirectionalAnim;

    // Kick the cutscene off. Safe to call repeatedly — only the first call while idle does anything.
    public void Play()
    {
        if (_phase != Phase.Idle || player == null || npc == null) return;

        player.MovementLocked = true;
        CinematicBars.PushForced();

        _npcRb = npc.GetComponent<Rigidbody2D>();
        _npcAnim = npc.GetComponent<Animator>();
        if (_npcAnim != null)
        {
            foreach (var p in _npcAnim.parameters)
                if (p.name == "Horizontal" || p.name == "Vertical") { _hasDirectionalAnim = true; break; }
        }

        // The pair clock each other the moment the trigger fires: both turn to face, then the NPC
        // starts walking over. The player's facing is held through the walk by MovementLocked.
        player.FaceToward(npc.transform.position);
        OnFootController.ApplyFacing(npc.transform, _npcRb,
            (Vector2)(player.transform.position - npc.transform.position), spriteFacingOffsetDeg);

        _phase = Phase.WalkUp;
    }

    void Update()
    {
        switch (_phase)
        {
            case Phase.WalkUp: StepWalkUp(); break;
            case Phase.Talking:
                if (npc == null || !npc.IsTalking)
                {
                    _phase = Phase.Done;
                    Destroy(gameObject); // one-time scene beat; takes any sibling trigger with it
                }
                break;
        }
    }

    void StepWalkUp()
    {
        if (npc == null || player == null) { Abort(); return; }

        Vector3 pos = npc.transform.position;
        Vector2 toPlayer = (Vector2)(player.transform.position - pos);
        if (toPlayer.magnitude <= stopDistance)
        {
            StartTalk();
            return;
        }

        Vector2 dir = toPlayer.normalized;
        Vector3 next = pos + (Vector3)(dir * walkSpeed * Time.deltaTime);
        if (_npcRb != null && _npcRb.bodyType != RigidbodyType2D.Dynamic) _npcRb.MovePosition(next);
        else npc.transform.position = next;

        AnimateWalk(dir);
    }

    void AnimateWalk(Vector2 dir)
    {
        // Directional rigs own the facing through Horizontal/Vertical params; plain rigs rotate the
        // body toward the walk direction (the same split OnFootController makes).
        if (_npcAnim != null)
        {
            _npcAnim.speed = 1f;
            foreach (var p in _npcAnim.parameters)
            {
                if (p.name == "Horizontal") _npcAnim.SetFloat("Horizontal", dir.x);
                else if (p.name == "Vertical") _npcAnim.SetFloat("Vertical", dir.y);
                else if (p.name == "Speed") _npcAnim.SetFloat("Speed", 1f);
            }
        }
        if (!_hasDirectionalAnim)
        {
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + spriteFacingOffsetDeg;
            if (_npcRb != null && _npcRb.bodyType != RigidbodyType2D.Dynamic)
                _npcRb.MoveRotation(Mathf.MoveTowardsAngle(_npcRb.rotation, ang, turnRate * Time.deltaTime));
            else
                npc.transform.rotation = Quaternion.Euler(0f, 0f,
                    Mathf.MoveTowardsAngle(npc.transform.eulerAngles.z, ang, turnRate * Time.deltaTime));
        }
    }

    void StartTalk()
    {
        // BeginConversation faces the pair at each other and opens the first line, so IsTalking is
        // already holding the bars in when the walk-up hold is released — no dip between phases.
        player.BeginConversation(npc);
        FreezeNpcAnim();
        player.MovementLocked = false; // the conversation lock takes over until the talk ends
        CinematicBars.PopForced();
        _phase = Phase.Talking;
    }

    void FreezeNpcAnim()
    {
        if (_npcAnim == null) return;
        foreach (var p in _npcAnim.parameters)
            if (p.name == "Speed") _npcAnim.SetFloat("Speed", 0f);
        _npcAnim.Update(0f);   // sample the idle pose at the conversation facing...
        _npcAnim.speed = 0f;   // ...then stop so it can't treadmill while talking
    }

    void Abort()
    {
        if (player != null) player.MovementLocked = false;
        CinematicBars.PopForced();
        _phase = Phase.Done;
        Destroy(gameObject);
    }
}
