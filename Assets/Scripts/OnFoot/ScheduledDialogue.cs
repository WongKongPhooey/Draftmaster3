using Draftmaster.Weekend;
using UnityEngine;

// Keeps a placed NPC's lines in step with the weekend's clock.
//
// The core cast stands in the same spot for three days, but what they have to say does not: on Friday
// morning the crew chief is talking about a practice session that has not run, and on Sunday lunchtime the
// same man is talking about the race you are about to start. The marker carries a set of lines per
// half-day (PlacedNPC.schedule); this swaps them in as the weekend moves.
//
// Only ever swaps while they are NOT talking — changing the script out from under an open conversation
// would jump the player mid-sentence — and only when the half-day has actually changed, so it costs a
// comparison per frame and nothing else.
public class ScheduledDialogue : MonoBehaviour
{
    [Tooltip("The marker this body was built from; its schedule is the source of the lines.")]
    public PlacedNPC marker;

    [Tooltip("The speaker whose lines are swapped.")]
    public NPCInteractable speaker;

    WeekendSlot _appliedFor;
    bool _applied;

    void Start() => Apply(force: true);

    void LateUpdate()
    {
        if (marker == null || speaker == null) { enabled = false; return; }
        if (speaker.IsTalking) return;

        var slot = WeekendLedger.CurrentSlot;
        if (_applied && slot == _appliedFor) return;

        _appliedFor = slot;
        _applied = true;
        Apply(force: false);
    }

    void Apply(bool force)
    {
        if (marker == null || speaker == null) return;
        if (speaker.IsTalking && !force) return;

        var slot = WeekendLedger.CurrentSlot;
        _appliedFor = slot;
        _applied = true;

        var set = marker.ScheduledFor(slot);
        speaker.lines = set != null ? set.lines : marker.lines;

        // A half-day's own objective banner overrides the marker's, so "head to the drivers' room" on race
        // morning can differ from "head to your car" on Friday without a second NPC.
        if (set != null && !string.IsNullOrEmpty(set.objectiveOnFinish))
            marker.objectiveOnFinish = set.objectiveOnFinish;
    }
}
