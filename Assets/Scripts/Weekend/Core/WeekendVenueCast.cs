namespace Draftmaster.Weekend
{
    // Who is stood at each venue, and what they say when there is nothing booked.
    //
    // The runtime builder stands these people up (WeekendVenueSites.StaffTheVenues) and the editor's weekend
    // cast window lists them without entering play mode. Both read this, so "who runs the drivers' room" is
    // one answer rather than a literal in a builder somebody has to go and read.
    public static class WeekendVenueCast
    {
        public struct Host
        {
            public WeekendVenue venue;
            public string speaker;
            public string idleLine;

            // Metres along the paddock from the venue's own mark, so the host is beside the spot the player
            // walks to rather than stood on it.
            public float offsetAlong;
        }

        public static readonly Host[] All =
        {
            new Host
            {
                venue = WeekendVenue.PitBox, speaker = "CREW CHIEF", offsetAlong = 0.4f,
                idleLine = "Car's on the setup pad. Shout if you want anything changed.",
            },
            new Host
            {
                venue = WeekendVenue.Motorhome, speaker = "ENGINEER", offsetAlong = 0f,
                idleLine = "Kettle's on. Nothing to go through until the sheet says so.",
            },
            new Host
            {
                venue = WeekendVenue.MeetingRoom, speaker = "SERIES OFFICIAL", offsetAlong = 1.2f,
                idleLine = "Room's open. We start when the schedule says we start.",
            },
            new Host
            {
                venue = WeekendVenue.SponsorSuite, speaker = "SPONSOR REP", offsetAlong = 1f,
                idleLine = "Come back when you're down for an appearance and I'll walk you in.",
            },
            new Host
            {
                venue = WeekendVenue.SigningFence, speaker = "FAN LIAISON", offsetAlong = -1f,
                idleLine = "Queue builds up about ten minutes before you're due. Don't be late for them.",
            },
            new Host
            {
                venue = WeekendVenue.IntroStage, speaker = "STAGE MANAGER", offsetAlong = -1.4f,
                idleLine = "You're on after the 24 car. Not before.",
            },
        };

        // Who runs a given venue. The grandstand has nobody — you sit down in it yourself.
        public static string SpeakerAt(WeekendVenue venue)
        {
            foreach (var host in All)
                if (host.venue == venue) return host.speaker;
            return venue == WeekendVenue.Grandstand ? "(you take a seat)" : "";
        }
    }
}
