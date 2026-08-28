using System.Collections.Generic;

namespace Draftmaster.Weekend
{
    // The vocabulary a hand-authored weekend is written in.
    //
    // A plan file names its bookings with short stable ids — "sponsor_event-photoshoot", "watch-qualifying",
    // "team-briefing" — and this turns one of those into an ActivityKind plus every default that booking
    // usually carries: what it is called, the line under it, the appearance fee, and what missing it costs.
    // So the shortest useful line in a plan file is
    //
    //     { "event": "sponsor_event-photoshoot", "start": "09:45" }
    //
    // and everything else is optional override. That is the whole point of the catalogue: the person laying
    // out a weekend is deciding WHEN things happen and WHERE, not retyping the same sponsor penalty clause
    // into six files.
    //
    // Ids are the contract with the JSON on disk, so they never change once shipped — rename one and every
    // authored weekend that used it silently loses a booking. Adding is free; renaming is not.
    public static class WeekendEventCatalog
    {
        // One bookable thing: what it is, and what it looks like when nothing is overridden.
        public struct EventType
        {
            public string id;
            public ActivityKind kind;

            public string title;
            public string subtitle;
            public int minutes;          // default length, overridable per booking

            public int appearanceFee;    // paid on attendance
            public bool mandatory;       // the sport, the team or the contract expects you
            public int skipMoney;        // fine for not turning up
            public float skipAppeal;     // fan appeal lost for not turning up
            public string skipReason;

            // Whose session this is, for the three spectate kinds — a plan file says
            // { "event": "watch-qualifying", "series": "Trucks" }. Ignored by everything else.
            public bool needsSeries;
        }

        public static readonly EventType[] All =
        {
            // ---- the player's own car on track -------------------------------------------------
            new EventType
            {
                id = "session-practice", kind = ActivityKind.Practice, minutes = 75,
                title = "PRACTICE",
                subtitle = "Run the R&D list: long runs, tyre falloff, and a mock qualifying lap at the end.",
            },
            new EventType
            {
                id = "session-qualifying", kind = ActivityKind.Qualifying, minutes = 60,
                title = "QUALIFYING",
                subtitle = "One lap that decides where you start. Miss it and you go to the back.",
            },
            new EventType
            {
                id = "session-race", kind = ActivityKind.Race, minutes = 180,
                title = "RACE",
                subtitle = "This is what the other two days were for.",
            },

            // ---- somebody else's session -------------------------------------------------------
            new EventType
            {
                id = "watch-practice", kind = ActivityKind.SpectatePractice, minutes = 75, needsSeries = true,
                title = "PRACTICE",
                subtitle = "Stand on the wall and watch what the field is doing with the track.",
            },
            new EventType
            {
                id = "watch-qualifying", kind = ActivityKind.SpectateQualifying, minutes = 60, needsSeries = true,
                title = "QUALIFYING",
                subtitle = "Watch the grid get set.",
            },
            new EventType
            {
                id = "watch-race", kind = ActivityKind.SpectateRace, minutes = 180, needsSeries = true,
                title = "RACE",
                subtitle = "Watching the leaders here is free homework.",
            },

            // ---- the team, behind closed doors -------------------------------------------------
            new EventType
            {
                id = "team-briefing", kind = ActivityKind.TeamBriefing, minutes = 60,
                title = "TEAM STRATEGY BRIEFING",
                subtitle = "The crew chief walks the weekend: what the sim says, what the tyre does here, who to race and who to leave alone.",
                mandatory = true,
                skipReason = "The crew chief set the weekend's plan without you in the room.",
            },
            new EventType
            {
                id = "team-debrief", kind = ActivityKind.Debrief, minutes = 45,
                title = "PRACTICE DEBRIEF",
                subtitle = "Engineers, tyre data, and your own read on the car. Pick what to change before qualifying.",
            },
            new EventType
            {
                id = "team-orientation", kind = ActivityKind.Orientation, minutes = 15,
                title = "ROOKIE ORIENTATION",
                subtitle = "Two minutes on the phone in your pocket: what is on today, what you have agreed to do for people, and where to read it back.",
            },

            // ---- what the sport requires -------------------------------------------------------
            new EventType
            {
                id = "official-drivers_meeting", kind = ActivityKind.DriversMeeting, minutes = 30,
                title = "DRIVERS MEETING",
                subtitle = "Mandatory. Officials read the rules for this track, then the room empties.",
                mandatory = true, skipMoney = 5000,
                skipReason = "Missing the drivers meeting is a fine and a start-at-the-rear penalty.",
            },
            new EventType
            {
                id = "official-driver_intros", kind = ActivityKind.DriverIntros, minutes = 25,
                title = "DRIVER INTRODUCTIONS",
                subtitle = "Your name over the PA and a walk down the stage in front of the grandstand.",
                mandatory = true, skipAppeal = 4f,
                skipReason = "The crowd heard your name and nobody walked out.",
            },

            // ---- media -------------------------------------------------------------------------
            new EventType
            {
                id = "media-press_conference", kind = ActivityKind.PressConference, minutes = 45,
                title = "PRESS CONFERENCE",
                subtitle = "Top table, three microphones, and the story they already decided to write.",
                skipReason = "The press wrote the story without your side of it.",
            },
            new EventType
            {
                id = "media-hit", kind = ActivityKind.MediaHit, minutes = 20,
                title = "BROADCAST HIT",
                subtitle = "Ninety seconds live on the pre-race show. One question, no second take.",
                skipReason = "The press wrote the story without your side of it.",
            },

            // ---- fans --------------------------------------------------------------------------
            new EventType
            {
                id = "fan_event-autographs", kind = ActivityKind.Autographs, minutes = 60,
                title = "SIGNING SESSION",
                subtitle = "A queue down the midway with hero cards and sharpies. Sign as many as the hour allows.",
                appearanceFee = 750, skipAppeal = 2f,
                skipReason = "The queue waited and then went home.",
            },
            new EventType
            {
                id = "fan_event-hauler_parade", kind = ActivityKind.HaulerParade, minutes = 30,
                title = "HAULER PARADE",
                subtitle = "Walk the transporters in past the fence with the crew. Cheap goodwill, and the fans remember it.",
                skipAppeal = 1f,
                skipReason = "The queue waited and then went home.",
            },

            // ---- sponsors ----------------------------------------------------------------------
            new EventType
            {
                id = "sponsor_event-duty", kind = ActivityKind.SponsorDuty, minutes = 60,
                title = "SPONSOR APPEARANCE",
                subtitle = "The people who pay for the hood want an hour of your day, and they booked it months ago.",
                appearanceFee = 1500, mandatory = true, skipMoney = 750,
                skipReason = "The guests turned up to a room with nobody in it.",
            },
            new EventType
            {
                id = "sponsor_event-photoshoot", kind = ActivityKind.PhotoShoot, minutes = 60,
                title = "SPONSOR PHOTO SHOOT",
                subtitle = "Stills for the season's marketing: firesuit on, same three poses, an hour of standing still.",
                appearanceFee = 1200, mandatory = true, skipMoney = 600,
                skipReason = "The photographer flew in for that hour and shot an empty backdrop.",
            },

            // ---- the hour you took off ---------------------------------------------------------
            new EventType
            {
                id = "rest", kind = ActivityKind.Rest, minutes = 60,
                title = "REST",
                subtitle = "An hour that is yours. Nothing gained, nothing lost, and the day moves on.",
            },
        };

        static Dictionary<string, EventType> _byId;

        static Dictionary<string, EventType> ById()
        {
            if (_byId != null) return _byId;
            _byId = new Dictionary<string, EventType>(All.Length);
            foreach (var e in All) _byId[Normalise(e.id)] = e;
            return _byId;
        }

        // Ids are matched case- and separator-insensitively, so a plan file written with
        // "Sponsor_Event-PhotoShoot" or "sponsor event photoshoot" still resolves. Authoring by hand in a
        // text editor is the whole point of the format — it should not fail on a capital letter.
        public static string Normalise(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            var chars = new System.Text.StringBuilder(id.Length);
            foreach (char c in id)
            {
                if (char.IsLetterOrDigit(c)) chars.Append(char.ToLowerInvariant(c));
                else if (c == '_' || c == '-' || c == ' ' || c == '.') chars.Append('-');
            }
            return chars.ToString();
        }

        public static bool TryGet(string id, out EventType type) => ById().TryGetValue(Normalise(id), out type);

        public static bool Exists(string id) => ById().ContainsKey(Normalise(id));

        // Every id, for the editor's dropdown and for the error message when one does not resolve.
        public static string[] Ids()
        {
            var ids = new string[All.Length];
            for (int i = 0; i < All.Length; i++) ids[i] = All[i].id;
            return ids;
        }

        // The catalogue entry for a kind — used when exporting an existing procedural timetable back out to
        // a plan file, so a round-trip produces the short id rather than a wall of overrides.
        public static bool TryGetByKind(ActivityKind kind, RacingSeries? series, out EventType type)
        {
            foreach (var e in All)
            {
                if (e.kind != kind) continue;
                type = e;
                return true;
            }
            type = default;
            return false;
        }
    }
}
