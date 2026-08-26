namespace Draftmaster.Weekend
{
    // Where in the world a booking actually happens.
    //
    // A race weekend is a place, not a menu. The plan meeting is at the pit box with the car in bits behind
    // you; the debrief is in your own motorhome; the drivers meeting and the press conference are in the
    // room every circuit has, with a chair for every driver entered; signing is done through the fence at
    // the edge of the paddock with the fans on the other side of it; and watching somebody else's session
    // means sitting in a grandstand while the cars go past. The timetable says when. This says where — and
    // the player has to walk there.
    //
    // Pure and in the core assembly so the mapping is testable and the timetable can label a booking with
    // the place it happens without the runtime layer being involved.
    public enum WeekendVenue
    {
        None = 0,      // nothing to walk to — the player's own sessions hand off to the race scene
        PitBox,        // the team's box on pit road: plan meetings, and where a broadcaster grabs you
        Motorhome,     // the player's own RV in the drivers' lot: debriefs, and where you take an hour off
        MeetingRoom,   // the drivers' room: a top table and a seat for every driver at the circuit
        SigningFence,  // the fan barrier along the paddock boundary — you sign from the inside of it
        SponsorSuite,  // the hospitality awning out in the middle of the paddock
        IntroStage,    // the stage at the end of pit road, where the field is announced before the race
        Grandstand,    // a seat in the crowd for somebody else's session
    }

    public static class WeekendVenues
    {
        // Where each kind of booking is kept. The three on-track kinds are None: those load the race scene
        // rather than sending the player somewhere on foot.
        public static WeekendVenue For(ActivityKind kind)
        {
            switch (kind)
            {
                case ActivityKind.Practice:
                case ActivityKind.Qualifying:
                case ActivityKind.Race:
                    return WeekendVenue.None;

                // The plan is made standing at the car; the debrief is done sitting down, away from it.
                case ActivityKind.TeamBriefing: return WeekendVenue.PitBox;
                case ActivityKind.Debrief:      return WeekendVenue.Motorhome;

                case ActivityKind.DriversMeeting: return WeekendVenue.MeetingRoom;
                case ActivityKind.DriverIntros:   return WeekendVenue.IntroStage;

                // A press conference is the room; a media hit is a camera crew catching you at the box.
                case ActivityKind.PressConference: return WeekendVenue.MeetingRoom;
                case ActivityKind.MediaHit:        return WeekendVenue.PitBox;

                case ActivityKind.Autographs:
                case ActivityKind.HaulerParade:
                    return WeekendVenue.SigningFence;

                case ActivityKind.SponsorDuty:
                case ActivityKind.PhotoShoot:
                    return WeekendVenue.SponsorSuite;

                case ActivityKind.SpectatePractice:
                case ActivityKind.SpectateQualifying:
                case ActivityKind.SpectateRace:
                    return WeekendVenue.Grandstand;

                // An hour off is not somewhere you go — it is the hour you did not book anything in.
                case ActivityKind.Rest: return WeekendVenue.None;

                default: return WeekendVenue.None;
            }
        }

        // What the schedule and the objective marker call the place.
        public static string Label(WeekendVenue venue)
        {
            switch (venue)
            {
                case WeekendVenue.PitBox:       return "the pit box";
                case WeekendVenue.Motorhome:    return "your motorhome";
                case WeekendVenue.MeetingRoom:  return "the drivers' room";
                case WeekendVenue.SigningFence: return "the fan fence";
                case WeekendVenue.SponsorSuite: return "the hospitality tent";
                case WeekendVenue.IntroStage:   return "the intro stage";
                case WeekendVenue.Grandstand:   return "the grandstand";
                default:                        return "the track";
            }
        }

        // The line the timetable prints in a booking's location column. Capitalised, no article, because it
        // sits in a column of short place names rather than in a sentence.
        public static string ShortLabel(WeekendVenue venue)
        {
            switch (venue)
            {
                case WeekendVenue.PitBox:       return "Pit box";
                case WeekendVenue.Motorhome:    return "Motorhome";
                case WeekendVenue.MeetingRoom:  return "Drivers' room";
                case WeekendVenue.SigningFence: return "Fan fence";
                case WeekendVenue.SponsorSuite: return "Hospitality";
                case WeekendVenue.IntroStage:   return "Intro stage";
                case WeekendVenue.Grandstand:   return "Grandstand";
                default:                        return "Track";
            }
        }

        // What the objective marker says while the player is walking there.
        public static string Directions(WeekendVenue venue)
        {
            switch (venue)
            {
                case WeekendVenue.PitBox:       return "Head to the pit box";
                case WeekendVenue.Motorhome:    return "Head back to your motorhome";
                case WeekendVenue.MeetingRoom:  return "Head to the drivers' room";
                case WeekendVenue.SigningFence: return "Head to the fan fence";
                case WeekendVenue.SponsorSuite: return "Head to the hospitality tent";
                case WeekendVenue.IntroStage:   return "Head to the intro stage";
                case WeekendVenue.Grandstand:   return "Find a seat in the grandstand";
                default:                        return "Head to the track";
            }
        }

        // Booked activities the player has to be stood in the right place for. Everything that is not an
        // on-track session is somewhere, so this is the "walk there" test.
        public static bool IsWalkTo(ActivityKind kind) => For(kind) != WeekendVenue.None;
    }
}
