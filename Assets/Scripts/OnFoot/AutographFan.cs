using UnityEngine;
using Draftmaster.Fans;

// A pit-lane fan who only wants the player's autograph. Talked to through the normal NPCInteractable
// flow (walk up, press E, advance the lines): finishing the exchange "signs" an autograph and lifts fan
// appeal. If the player is on foot in the pits but never engages, the fan runs out of patience, gives up,
// and wanders off — costing a little appeal. That's the "ignoring them reduces it over time" half.
//
// Patience only ticks while the player is actually on foot in the scene (AutographFanSpawner.PlayerOnFoot);
// wheeling past at racing speed doesn't count as ignoring a fan, so appeal never bleeds during a green-flag
// stint when the player couldn't have stopped to sign anyway.
public class AutographFan : NPCInteractable
{
    [Tooltip("Fan appeal gained when the player finishes signing.")]
    public float appealForSigning = 1.5f;
    [Tooltip("Fan appeal lost when the fan gives up unsigned.")]
    public float appealForIgnoring = 1f;
    [Tooltip("Seconds (of the player being on foot) the fan waits before giving up and leaving.")]
    public float patienceSeconds = 45f;
    [Tooltip("Seconds the resolved fan lingers before despawning.")]
    public float leaveSeconds = 3f;

    bool _resolved;        // signed OR gave up — either way it stops counting and leaves
    float _ignoredTime;    // seconds the player has been on foot without signing this fan

    void Awake()
    {
        repeatable = false;   // one autograph, and no looping the dialogue
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
        if (_resolved || IsTalking) return;

        // Only accrue "ignored" time while the player is on foot in the pits — not while driving.
        if (AutographFanSpawner.PlayerOnFoot)
        {
            _ignoredTime += Time.deltaTime;
            if (_ignoredTime >= patienceSeconds) GiveUp();
        }
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
