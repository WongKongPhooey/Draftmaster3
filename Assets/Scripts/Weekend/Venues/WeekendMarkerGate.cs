using Draftmaster.Weekend;
using UnityEngine;

// The step between a marker you can walk to and a place you cannot.
//
// Some venues are not in the paddock. The grandstands at a road course are across the track, behind a
// boundary the player is clamped inside; a room can be up a flight of stairs that does not exist. So the
// marker goes where they CAN get to — the gate at the paddock exit — and carries a `teleportTo` pointing at
// where the thing actually happens. Walk into the marker, press the action button, and the wipe puts you in
// the seat.
//
// Added automatically by WeekendVenueSites to any authored marker that has a teleport target, so nothing has
// to be wired up: give the marker a child called `Seat` and the gate appears.
//
// Deliberately NOT a conversation. It is a door. The obligation itself starts on whoever is waiting on the
// other side, exactly as it would if the player had walked there.
public class WeekendMarkerGate : NPCInteractable
{
    [Tooltip("Where this gate leads. Set from the marker's teleportTo when the venue builder makes one.")]
    public Transform destination;

    [Tooltip("Said when the player presses the action button here with nothing booked that comes this way.")]
    [TextArea]
    public string[] closedLines =
    {
        "Nothing on out there at the moment.",
    };

    [Tooltip("Which venue's bookings this gate leads to. Only those open it.")]
    public WeekendVenue venue = WeekendVenue.Grandstand;

    public override bool IsTalking => false;   // walking through a gate is not a conversation

    public override bool Interact()
    {
        var pending = WeekendAppointment.Pending;

        // Only for the thing that is actually booked through here. Anything else and this is a fence with a
        // gate in it that happens not to be open right now.
        if (pending == null || WeekendVenues.For(pending.kind) != venue || destination == null)
        {
            lines = closedLines;
            return base.Interact();
        }

        if (ScreenFade.Busy) return false;

        // Behind a wipe, for the same reason TRAVEL THERE is: this is a walk that happened, not a player
        // blinking and finding themselves somewhere else.
        var player = WeekendVenueAnchor.OnFootPlayer();
        Vector3 to = destination.position;
        Vector3 back = transform.position;      // the gate: where the walk out there started

        ScreenFade.Cut(() =>
        {
            if (player == null) return;
            to.z = player.position.z;

            // The body owns the pose — moving only the transform lets the rigidbody snap it back next frame.
            var body = player.GetComponent<Rigidbody2D>();
            if (body != null) body.position = to;
            player.position = to;

            // Somewhere to watch a session from is not a panel to sit through: the obligation was to be
            // there, so arriving completes it, and what is left is a seat and a way back. GrandstandVisit
            // owns both. Done inside the wipe so the booking is settled before the screen comes back and
            // the result card is not read over the paddock the player has just left.
            if (pending.IsSpectate) GrandstandVisit.Begin(pending, back);
        });

        return false;
    }
}
