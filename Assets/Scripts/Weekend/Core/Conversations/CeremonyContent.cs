namespace Draftmaster.Weekend
{
    // The two things the sport makes you turn up to on race day, as they are actually attended: the drivers
    // meeting in the room with a chair for every driver in it, and the walk out onto the intro stage with
    // your name over the PA.
    //
    // Neither is a skill test. The meeting is worth paying attention in — one of the four notes read out is
    // the one that catches somebody out at this circuit today — and the intros are worth whatever you give
    // the crowd.
    public static class CeremonyContent
    {
        public static WeekendConversation Build(WeekendActivity a)
        {
            bool intros = a != null && a.kind == ActivityKind.DriverIntros;
            return intros ? Intros(a) : DriversMeeting(a);
        }

        // ------------------------------------------------------------------ the drivers' room

        // Four notes, read out to a room of drivers who are mostly on their phones. One of them is about
        // this circuit and this weekend, and the driver who was listening is the one who does not get
        // caught by it. Which one is seeded off the booking, so the same weekend always asks the same
        // question and the ledger cannot be re-rolled by walking out and back in.
        static WeekendConversation DriversMeeting(WeekendActivity a)
        {
            string track = WeekendLedger.Timetable?.trackName;
            if (string.IsNullOrEmpty(track)) track = "this place";

            var notes = new[]
            {
                $"Pit entry at {track} is tighter than the sim — the commitment line is painted, and we are calling it hard today.",
                "Restart zone is the same as last year. Nobody goes before the leader, and we will be watching the second row.",
                "Track limits at the exit of the last corner: two wheels inside the white or it is a lap deleted.",
                "Weather: the front arrives about two hours after the green. Slicks now, and listen to your spotter.",
            };

            var rng = WeekendRandom.For(WeekendLedger.WeekendId, a != null ? a.id.GetHashCode() : 0, 17);
            int catches = rng.Range(0, notes.Length);

            var c = new WeekendConversation
            {
                statKey = "driversmeetings",
                statCount = 1,
                greeting = new[]
                {
                    "Sit down, we are starting.",
                    "Four notes. They are the same four every week and one of them is going to cost somebody a race today.",
                },
                farewell = new[] { "That is us. Cars on the grid in ninety minutes." },
            };

            var beat = new WeekendBeat
            {
                speaker = "RACE DIRECTOR",
                preamble = new[] { notes[0], notes[1], notes[2], notes[3] },
                line = "Anything anybody wants to raise before we let you go?",
                question = "Which of those catches somebody out today?",
            };

            for (int i = 0; i < notes.Length; i++)
            {
                bool right = i == catches;
                beat.choices.Add(WeekendConversation.Say(
                    Shorten(notes[i]),
                    right
                        ? "Correct. Glad somebody was listening — that is the one we will be strict on."
                        : "Maybe. That is not the one I would worry about today.",
                    setup: right ? 0.05f : 0.01f,
                    morale: right ? 4f : 0f,
                    score: right ? 1f : 0.3f));
            }

            c.Add(beat);
            c.headline = o => o.setupGain >= 0.04f
                ? "You were listening. One note in that room was worth a place today."
                : "You were in the room. Most of it went past you.";
            return c;
        }

        // The note as a one-line answer: the first clause, which is what a driver would actually say back.
        static string Shorten(string note)
        {
            int cut = note.IndexOf(':');
            if (cut < 0) cut = note.IndexOf('—');
            if (cut < 0) cut = note.IndexOf(',');
            string head = cut > 0 ? note.Substring(0, cut) : note;
            return head.Trim();
        }

        // ------------------------------------------------------------------ the stage

        // Introductions: the field is walked out one at a time and the crowd is either with you or waiting
        // for the next name. Three beats, each one either working the crowd or wasting them.
        static WeekendConversation Intros(WeekendActivity a)
        {
            var c = new WeekendConversation
            {
                statKey = "driverintros",
                statCount = 1,
                greeting = new[]
                {
                    "You're after the 24 car. Wave, walk, don't stop on the stairs.",
                    "They can hear everything up there, so give them something.",
                },
                farewell = new[] { "Good. Now go and get strapped in." },
            };

            c.Add(new WeekendBeat
            {
                speaker = "PA ANNOUNCER",
                preamble = new[] { "...and now, from the outside of row four..." },
                line = "How do you want it — the full introduction, or just the name?",
                question = "How do you go out?",
                choices =
                {
                    WeekendConversation.Say("Full introduction. Home town, car number, the lot.",
                        "That is what they came for.", appeal: 2.4f, sponsor: 2f, score: 0.9f),
                    WeekendConversation.Say("Name and number. Keep it moving.",
                        "Quick and clean. Understood.", appeal: 0.8f, morale: 1f, score: 0.5f),
                    WeekendConversation.Say("Say the sponsor's name twice.",
                        "...I can do that.", appeal: 0.4f, sponsor: 6f, media: -2f, score: 0.6f),
                },
            });

            c.Add(new WeekendBeat
            {
                speaker = "THE CROWD",
                preamble = new[] { "The stand on the frontstretch is on its feet for the car ahead of you." },
                line = "There is a gap in the noise, and it is yours.",
                question = "What do you do with it?",
                choices =
                {
                    WeekendConversation.Say("Both arms up. Give it back to them.",
                        "The whole grandstand goes with you.", appeal: 3.2f, sponsor: 2f, morale: 2f, score: 1f),
                    WeekendConversation.Say("Point at the kids on the fence.",
                        "Forty phones go up at once.", appeal: 2.8f, sponsor: 1f, score: 0.9f),
                    WeekendConversation.Say("Head down. Get to the car.",
                        "You are past them before they finish your name.", appeal: 0.2f, morale: 1f, score: 0.3f),
                },
            });

            c.headline = o => o.fanAppeal >= 5f
                ? "You walked that stage like somebody who has won here. They will remember it."
                : o.fanAppeal >= 2f
                    ? "Waved, walked, got in the car. Job done."
                    : "You were announced. That is about all that happened.";
            return c;
        }
    }
}
