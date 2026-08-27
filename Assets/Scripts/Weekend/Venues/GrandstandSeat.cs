using Draftmaster.Weekend;
using UnityEngine;

// A seat in the crowd. Walk up to a grandstand with a session booked to watch and sit down in it.
//
// The stands are already generated along every straight at every track (TrackDressingFactory), so there is
// a seat at every circuit without anyone authoring one — WeekendVenueSites drops one of these on the front
// row of each. Sitting hands over to GrandstandSpectate, which plays the session out beside the live world
// rather than on top of it.
public class GrandstandSeat : NPCInteractable
{
    [Tooltip("What the seat says when there is no session booked to watch from it.")]
    [TextArea]
    public string[] emptyLines =
    {
        "Good view of the exit of the last corner from here. Nothing running at the moment, though.",
    };

    public override bool IsTalking => false;   // sitting down is not a conversation

    public override bool Interact()
    {
        var pending = WeekendAppointment.Pending;
        if (pending == null || !pending.IsSpectate)
        {
            lines = emptyLines;
            return base.Interact();
        }

        // Behind a wipe. From the gate this is the walk out to the stand and back through the crowd; from a
        // seat it is settling into it. Either way the session opens with the grandstand already around you.
        if (ScreenFade.Busy) return false;
        ScreenFade.Cut(() => GrandstandSpectate.Begin(pending));
        return false;
    }
}
