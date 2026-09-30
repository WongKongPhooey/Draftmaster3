using UnityEngine;

// The bed in the player's motorhome. At the end of a Friday or Saturday — the evening's last obligation done
// and nothing left on the sheet until morning (Draftmaster.Weekend.WeekendBedtime) — walking up to it and
// pressing the action button, or tapping it on a phone, puts the driver to sleep; they wake the next morning
// to the same alarm the career opens on (PitLaneStart.GoToSleep).
//
// Same trick as LaptopInteractable: subclass NPCInteractable so OnFootController's proximity prompt, action
// button and tap handling work unchanged, and override what "interact" does. Built by RVInterior next to
// whatever "Bed" the room has, and switched on by PitLaneStart only while it is bedtime — the rest of the
// weekend it is furniture, with no prompt over it.
public class BedInteractable : NPCInteractable
{
    public override bool IsTalking => false;

    public override bool Interact()
    {
        var flow = FindFirstObjectByType<PitLaneStart>();
        if (flow != null) flow.GoToSleep();
        return false; // the night takes over — no ongoing conversation for OnFootController to track
    }
}
