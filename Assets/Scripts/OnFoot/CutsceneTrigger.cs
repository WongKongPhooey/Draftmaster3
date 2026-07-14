using UnityEngine;

// A circular walk-over cutscene trigger. Sits at a world position, polls the target (the on-foot
// player) each frame, and fires its callback once when the target steps inside the radius — an
// "extra way in" to a cutscene besides pressing interact on an NPC. Configured from code by the
// spawner that creates it (nothing in the binary scenes needs wiring), so the callback and gate
// are plain delegates rather than UnityEvents.
public class CutsceneTrigger : MonoBehaviour
{
    [Tooltip("Trigger radius (m) around this object's position.")]
    public float radius = 1.5f;
    [Tooltip("Fire once and disarm, or re-arm after each firing.")]
    public bool oneShot = true;

    [Tooltip("Whose position arms the trigger — the on-foot player.")]
    public Transform target;

    // Optional extra condition: the trigger only fires while this returns true (e.g. "player is
    // not inside the RV interior mask"). Null = always eligible.
    public System.Func<bool> Gate;
    // What the trigger fires. Assigned by the spawner.
    public System.Action Triggered;

    bool _fired;

    void Update()
    {
        if (_fired || target == null || Triggered == null) return;
        if (Gate != null && !Gate()) return;
        if (Vector2.Distance(target.position, transform.position) > radius) return;

        _fired = true;
        Triggered.Invoke();
        if (!oneShot) _fired = false;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
#endif
}
