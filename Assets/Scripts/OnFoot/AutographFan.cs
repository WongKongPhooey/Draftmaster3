using UnityEngine;
using Draftmaster.Fans;

// A pit-lane fan who only wants the player's autograph. When an on-foot player comes within noticeRadius the
// fan spots them and walks over, stopping just short so the normal NPCInteractable flow takes it from there
// (press E, advance the lines): finishing the exchange "signs" an autograph and lifts fan appeal. If the
// player lets the fan stand there asking and never engages, the fan runs out of patience, gives up, and
// wanders off — costing a little appeal. That's the "ignoring them reduces it over time" half.
//
// Patience only ticks while the fan has actually noticed an on-foot player (within noticeRadius); wheeling
// past at racing speed, or being on foot at the other end of the pit lane, doesn't count as ignoring a fan.
public class AutographFan : NPCInteractable
{
    [Tooltip("Fan appeal gained when the player finishes signing.")]
    public float appealForSigning = 1.5f;
    [Tooltip("Fan appeal lost when the fan gives up unsigned.")]
    public float appealForIgnoring = 1f;
    [Tooltip("Seconds (of the fan having noticed the on-foot player) before giving up and leaving.")]
    public float patienceSeconds = 45f;
    [Tooltip("Seconds the resolved fan lingers before despawning.")]
    public float leaveSeconds = 3f;

    [Header("Approach")]
    [Tooltip("Fans notice an on-foot player within this range (m) and walk over for the autograph.")]
    public float noticeRadius = 14f;
    [Tooltip("How close (m) the fan gets before stopping to ask.")]
    public float stopDistance = 1.4f;
    [Tooltip("Walking speed (m/s) while approaching the player.")]
    public float walkSpeed = 1.7f;
    [Tooltip("Paper-doll walk-cycle frames per second while moving.")]
    public float walkFrameRate = 6f;

    bool _resolved;        // signed OR gave up — either way it stops counting and leaves
    float _ignoredTime;    // seconds the fan has been asking (player noticed) without signing
    float _personalSpace;  // this fan's own stop distance, jittered so a group doesn't stack on one point
    NPCLayeredAppearance _appearance;
    float _frameTimer;
    int _frame;

    void Awake()
    {
        repeatable = false;   // one autograph, and no looping the dialogue
        _appearance = GetComponent<NPCLayeredAppearance>();
        // Jitter kept under interactRange (2.2m) so every fan still ends up close enough to talk to.
        _personalSpace = stopDistance + Random.value * 0.6f;
    }

    // Wrap the base conversation: when it transitions from talking to finished, the exchange is done —
    // sign the autograph exactly once.
    public override bool Interact()
    {
        bool wasTalking = IsTalking;
        bool stillTalking = base.Interact();
        if (wasTalking && !stillTalking && !_resolved) Sign();
        return stillTalking;
    }

    void Update()
    {
        if (_resolved) return;

        var player = AutographFanSpawner.OnFootPlayer;
        if (player == null) { Idle(); return; }   // nobody on foot — driving past never counts as ignoring

        Vector3 to = player.transform.position - transform.position;
        to.z = 0f;
        float dist = to.magnitude;

        if (IsTalking) { Face(to); Idle(); return; }
        if (dist > noticeRadius) { Idle(); return; }   // hasn't spotted the player — patience holds

        // Spotted: walk up, stop just short, then stand there asking until signed or out of patience.
        if (dist > _personalSpace)
        {
            Vector3 dir = to / dist;
            transform.position += dir * (walkSpeed * Time.deltaTime);
            Face(dir);
            Animate();
        }
        else
        {
            Face(to);
            Idle();
        }

        _ignoredTime += Time.deltaTime;
        if (_ignoredTime >= patienceSeconds) GiveUp();
    }

    void Face(Vector3 dir)
    {
        OnFootController.ApplyFacing(transform, null, new Vector2(dir.x, dir.y), 90f);
    }

    void Animate()
    {
        if (_appearance == null || _appearance.FrameCount == 0) return;
        _frameTimer += Time.deltaTime;
        float step = 1f / Mathf.Max(0.01f, walkFrameRate);
        while (_frameTimer >= step)
        {
            _frameTimer -= step;
            _frame++;
            _appearance.SetFrame(_frame);
        }
    }

    void Idle()
    {
        if (_frame == 0) return;
        _frame = 0;
        _appearance?.SetFrame(0);
    }

    void Sign()
    {
        if (_resolved) return;
        _resolved = true;
        FanAppeal.Add(appealForSigning);
        Leave();
    }

    void GiveUp()
    {
        if (_resolved) return;
        _resolved = true;
        FanAppeal.Add(-appealForIgnoring);
        Leave();
    }

    // Stop being interactable (disabling the component removes it from NPCInteractable.All and clears its
    // prompt/bubbles via the base OnDisable), then despawn after a short beat.
    void Leave()
    {
        enabled = false;
        Destroy(gameObject, Mathf.Max(0.1f, leaveSeconds));
    }
}
