using System.Collections.Generic;

namespace Draftmaster.Weekend
{
    // Radio row: a reporter from one of the stations walks over with a microphone and asks you to name a
    // driver. Best on a road course, most aggressive, cleanest, the one you would least like to be side by
    // side with — and the four names on offer are drawn from the field you race against this weekend.
    //
    // Whoever you name hears about it. A question that flatters the driver you pick moves you toward them; a
    // question that digs at them moves you away. There is no answer that keeps everybody sweet, which is the
    // point of putting the player on the radio.
    public static class RadioRowContent
    {
        public const string Title = "RADIO ROW";

        // How many drivers the reporter's question offers to choose between.
        public const int Choices = 4;

        // What naming a driver does to the player's relationship with them.
        public const float PraiseDelta = 8f;
        public const float DigDelta = -8f;

        // A driver the question can name. `name` is what the player says; `key` is the identity
        // DriverRelationships files them under (the name they race under).
        public struct FieldDriver
        {
            public string name;
            public string key;

            public FieldDriver(string name, string key) { this.name = name; this.key = key; }
        }

        class Prompt
        {
            public string question;
            public bool praise;      // true: naming them is a compliment; false: it is a dig
            public string answer;    // what the player says, {0} = the driver named
            public string reply;     // the reporter's reply, {0} = the driver named

            public Prompt(string question, bool praise, string answer, string reply)
            {
                this.question = question; this.praise = praise; this.answer = answer; this.reply = reply;
            }
        }

        static readonly Prompt[] Prompts =
        {
            new("Which driver in this field is the best on a road course?", true,
                "{0}. Nobody brakes later and still makes the corner.",
                "Big compliment. I'll make sure {0} hears it."),
            new("Who is the most aggressive driver out there right now?", false,
                "{0}. You know they're coming, you just never know where from.",
                "Oh, {0} is going to love that. We'll play it on the hour."),
            new("Who is the cleanest racer you line up against?", true,
                "{0}. You can run door to door with them for ten laps and never touch.",
                "That's a nice thing to hear about {0}. Not many people say it."),
            new("Who would you least like to be side by side with at two hundred miles an hour?", false,
                "{0}. I give them a lot of room. Maybe too much.",
                "Strong words. {0} is three tables down, you know."),
            new("Who is the most underrated driver in the garage?", true,
                "{0}. Put them in a better car and see what happens.",
                "{0} will be buying you a drink for that one."),
            new("Who in this field is most likely to cause the big one?", false,
                "{0}. Somebody has to, and it's usually them.",
                "Well, that's the clip of the day. Good luck with {0} on Sunday."),
            new("If you could pick one driver here as a team-mate, who would it be?", true,
                "{0}. Fast, honest, and they'd actually share the notes.",
                "Bet {0} didn't see that coming."),
            new("Who is the driver you most want to beat this weekend?", false,
                "{0}. Nothing personal. Well, a little personal.",
                "So it's personal with {0} now. Noted. Very noted."),
            new("Who is the best in the field at saving a bad car?", true,
                "{0}. They can drag a top ten out of something that should run twentieth.",
                "High praise. {0} has earned it this year."),
            new("Who gets away with more than they should out there?", false,
                "{0}. Watch the replays and tell me I'm wrong.",
                "The officials might want a word, and {0} definitely will."),
        };

        static readonly (string name, string outlet)[] Stations =
        {
            ("Ed Kinsella", "Pit Lane Radio"),
            ("Rhonda Pruitt", "Victory Lane Radio"),
            ("Gil Mercado", "WFLG 98.1"),
            ("Shay Donnelly", "Motor Sports Network"),
            ("Bo Tillman", "The Morning Drive"),
            ("Carla Nunes", "Turn Four Radio"),
        };

        // Is this booking radio row (rather than one of the other media hits)?
        public static bool Is(WeekendActivity a) =>
            a != null && a.kind == ActivityKind.MediaHit && a.title == Title;

        // The conversation, or null when there is nobody in the field to name — the caller falls back to an
        // ordinary media hit rather than asking a question with no answers.
        public static WeekendConversation Build(WeekendActivity a, int weekendId, IReadOnlyList<FieldDriver> field)
        {
            var named = Candidates(field);
            if (named.Count == 0) return null;

            // Seeded off the weekend and the booking, so a reload of the scene asks the same question and
            // offers the same four drivers.
            var rng = WeekendRandom.For(weekendId, StableHash(a != null ? a.id : ""), 77);
            var prompt = Prompts[rng.Range(0, Prompts.Length)];
            var station = Stations[rng.Range(0, Stations.Length)];

            rng.Shuffle(named);
            if (named.Count > Choices) named.RemoveRange(Choices, named.Count - Choices);

            var beat = new WeekendBeat
            {
                speaker = $"{station.name}, {station.outlet}",
                line = $"{station.name}, {station.outlet}. Quick one for the listeners. {prompt.question}",
                question = prompt.question,
            };

            foreach (var d in named)
            {
                beat.choices.Add(WeekendConversation.Say(
                    string.Format(prompt.answer, d.name),
                    string.Format(prompt.reply, d.name),
                    // Naming a name is radio: a dig makes the better clip, and the room likes either.
                    media: prompt.praise ? 3f : 5f,
                    appeal: prompt.praise ? 0.5f : 1f,
                    score: 0.6f,
                    rivalName: d.key,
                    rivalDelta: prompt.praise ? PraiseDelta : DigDelta));
            }

            var c = new WeekendConversation
            {
                statKey = "pressconferences",
                statCount = 1,
                greeting = new[] { "Radio row's set up. One of the stations is coming over. They want a name." },
                farewell = new[] { "That'll be on the air by lunch. Whoever you named will have heard it by two." },
            };
            c.Add(beat);

            c.headline = o =>
                o.rivalDelta > 0f ? $"You put {Spoken(named, o.rivalName)} over the air, and in a good way."
                : o.rivalDelta < 0f ? $"You named {Spoken(named, o.rivalName)} on the radio. They will have heard."
                : "Radio row done. Nobody named, nobody upset.";
            return c;
        }

        // Every distinct driver who can be named, in roster order.
        static List<FieldDriver> Candidates(IReadOnlyList<FieldDriver> field)
        {
            var list = new List<FieldDriver>();
            if (field == null) return list;
            var seen = new HashSet<string>();
            foreach (var d in field)
            {
                if (string.IsNullOrEmpty(d.key) || !seen.Add(d.key)) continue;
                list.Add(new FieldDriver(string.IsNullOrEmpty(d.name) ? d.key : d.name, d.key));
            }
            return list;
        }

        static string Spoken(List<FieldDriver> named, string key)
        {
            foreach (var d in named)
                if (d.key == key) return d.name;
            return string.IsNullOrEmpty(key) ? "a driver" : key;
        }

        // string.GetHashCode is not promised to be stable between runs; the seed has to be.
        static int StableHash(string s)
        {
            unchecked
            {
                int h = 23;
                if (s != null)
                    foreach (char ch in s) h = h * 31 + ch;
                return h;
            }
        }
    }
}
