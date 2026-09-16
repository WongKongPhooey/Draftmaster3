namespace Draftmaster.Weekend
{
    // The first time the phone in the driver's pocket goes off.
    //
    // The liaison sends a new driver across the paddock to the crew chief's strategy briefing, and a couple
    // of hundred metres short of the pit box the chief texts to ask where they have got to. It is the
    // phone's tutorial: a bleep, a "P - Check your phone" prompt, and one unread message on the MESSAGES
    // tile. Nothing in the paddock had ever made the player press the key before the orientation at 09:30,
    // and by then the briefing - the first thing they are asked to do - has already happened without it.
    //
    // This is the rule and the words; ChiefCheckInBeat (Assembly-CSharp) watches the walk, rings the phone
    // and puts the message on it. Once per save, remembered through AppearanceConditions under SaveKey, so
    // Draftmaster > NPCs > Clear Appearance Flags and Draftmaster > Demo > Re-arm The Opening both put it back.
    public static class ChiefCheckIn
    {
        // How far from the briefing the text arrives. Watkins Glen's walk from the motorhome to the pit box
        // is about 290 m, so this lands roughly a third of the way there - far enough out that the player is
        // plainly on their way, close enough that they have not arrived.
        public const float TriggerMetres = 200f;

        public const string SaveKey = "phone.chief.whereareyou";

        // The chief's thread on the phone, and this message's id inside it - resending it is a no-op.
        public const string ThreadId = "crew.chief";
        public const string MessageId = "chief.whereareyou";

        // Only the one booking: the text is about being late for the chief, and every other walk in the
        // paddock is to somebody else.
        public const ActivityKind Booking = ActivityKind.TeamBriefing;

        // Should the phone go off now?
        //
        //   alreadyFired  the save has had this beat
        //   booked        what the objective marker is pointing at
        //   metresLeft    from WeekendAppointment.DistanceRemaining; negative = nothing to walk to / not on foot
        //   arrived       the player is already stood at the venue - a T-travel lands them there in one wipe,
        //                 and "where are you?" to somebody standing in front of you is a bug, not a joke.
        //                 The beat stays armed, so the next briefing walk still gets it.
        //   busy          a conversation, a wipe, a menu or the phone itself has the player
        public static bool ShouldFire(bool alreadyFired, ActivityKind booked, float metresLeft, bool arrived, bool busy)
            => !alreadyFired
            && booked == Booking
            && metresLeft >= 0f
            && metresLeft <= TriggerMetres
            && !arrived
            && !busy;

        // The text. `startsAt` is the booking's own start as a person says it ("8:00 AM"). The name token is
        // filled by the runtime when the message lands, like every other line in the paddock, so it is
        // concatenated rather than interpolated - a {playerfirst} inside $"" would be a C# hole.
        public static string Message(string startsAt)
        {
            string when = string.IsNullOrEmpty(startsAt) ? "The briefing's about to start" : "Briefing's at " + startsAt;
            return "{playerfirst}, where are you? " + when +
                   " and the whole crew's stood round the pit box waiting on you.";
        }
    }
}
