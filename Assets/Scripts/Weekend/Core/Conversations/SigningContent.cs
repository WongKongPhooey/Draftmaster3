namespace Draftmaster.Weekend
{
    // The signing session, done through the fence at the edge of the paddock with the fans on the other
    // side of it.
    //
    // A queue, one person at a time, each of them holding something and wanting thirty seconds. What they
    // are worth is not a timing bar any more — it is what you give them. Signing everything put in front of
    // you is worth fans; stopping to talk to the one who has been there since the gates opened is worth
    // more of them; and the driver who works the queue fast keeps the sponsor happy because the queue is
    // shorter at the end of the hour.
    //
    // Seeded off the booking so the same session always brings the same faces — the ledger records against
    // this activity, and a queue that re-rolled every time the player walked away and back would be a way
    // of shopping for a better hour.
    public static class SigningContent
    {
        // One fan in the queue: who they are and what they are holding.
        struct Fan
        {
            public string who, holding, line;
            public Fan(string who, string holding, string line) { this.who = who; this.holding = holding; this.line = line; }
        }

        static readonly Fan[] Queue =
        {
            new("KID IN A TEAM SHIRT", "a die-cast car",
                "It's your one! Mum said you probably wouldn't stop but I said you would."),
            new("MAN WITH A PROGRAMME", "this year's programme",
                "Page eleven, that's you. Been coming here since before you were born, son."),
            new("WOMAN IN A CAP", "a phone, already filming",
                "My daughter races karts. Would you say something to her? She's at home watching."),
            new("TEENAGER", "a torn piece of paper",
                "Sorry — this is all I've got. Queue's been two hours."),
            new("OLD BOY IN A FOLDING CHAIR", "a photo from a decade ago",
                "That's your car at this track the year it rained. I was stood right there."),
            new("BLOKE WITH A HAULER PASS", "a die-cast still in the box",
                "Don't open it. It's an investment, that."),
            new("TWO KIDS PUSHED TO THE FRONT", "a hat each",
                "Say thank you. THANK YOU. See? Told you he'd do it."),
            new("SOMEBODY IN A RIVAL'S SHIRT", "a marker, no paper",
                "I'm not even a fan of yours. But you drove the wheels off it last week."),
        };

        public static WeekendConversation Build(WeekendActivity a)
        {
            bool parade = a != null && a.kind == ActivityKind.HaulerParade;

            var rng = WeekendRandom.For(WeekendLedger.WeekendId, a != null ? a.id.GetHashCode() : 0, 41);
            var order = new Fan[Queue.Length];
            System.Array.Copy(Queue, order, Queue.Length);
            rng.Shuffle(order);

            int served = parade ? 3 : 4;   // a parade is walked, a signing is stood at, so the queue is longer

            var c = new WeekendConversation
            {
                statKey = "autographs",
                statCount = 0,
                greeting = parade
                    ? new[]
                    {
                        "Gates opened twenty minutes ago and the haulers are still coming in.",
                        "They are three deep along the fence and every one of them can see you.",
                    }
                    : new[]
                    {
                        "Table's set up on the inside of the fence. Marker's there.",
                        "You have got the hour. The queue is longer than the hour.",
                    },
                farewell = new[] { "That's your lot — they'll move the rest along. Good hour, that." },
            };

            for (int i = 0; i < served && i < order.Length; i++)
            {
                var fan = order[i];
                c.Add(new WeekendBeat
                {
                    speaker = fan.who,
                    preamble = i == 0 ? null : new[] { "The queue shuffles forward." },
                    line = fan.line,
                    question = $"They're holding {fan.holding}.",
                    choices =
                    {
                        WeekendConversation.Say(
                            "Sign it, and ask them their name.",
                            "They tell you, twice, and read it back off the card the whole way to the car park.",
                            appeal: 1.6f, sponsor: 0.6f, score: 1f, statCount: 1),
                        WeekendConversation.Say(
                            "Sign it and get a photo with them.",
                            "It is on the internet before you have put the lid back on the marker.",
                            appeal: 1.9f, sponsor: 0.4f, media: 1.5f, score: 1f, statCount: 1),
                        WeekendConversation.Say(
                            "Sign it. Next.",
                            "Signed, handed back, and the queue moves a place.",
                            appeal: 0.7f, sponsor: 1.2f, score: 0.6f, statCount: 1),
                        WeekendConversation.Say(
                            "Wave, and keep moving.",
                            "They put their arm down slowly.",
                            appeal: -0.6f, sponsor: -0.5f, score: 0.2f),
                    },
                });
            }

            c.headline = o =>
                o.statCount == 0 ? "You stood at the fence for an hour and did not sign a thing."
                : o.fanAppeal >= 6f ? $"{o.statCount} of them, and every one got a minute of you. That is how it is done."
                : o.fanAppeal <= 1f ? $"{o.statCount} signed, fast, and nobody got a word."
                : $"{o.statCount} served at the fence. The queue was still there at the end.";
            return c;
        }
    }
}
