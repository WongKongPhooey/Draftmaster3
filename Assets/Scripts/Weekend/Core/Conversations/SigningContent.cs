namespace Draftmaster.Weekend
{
    // The signing session, done through the fence at the edge of the paddock with the fans on the other
    // side of it, and the hauler parade, which is the same thing walked rather than stood at.
    //
    // A queue, one person at a time, each of them holding something and wanting thirty seconds. What they
    // are worth is not a timing bar — it is what you give them, and the window is the whole decision. The
    // fence holds at least ten people, and the window is one plain signature short of all of them, so:
    //
    //   * Sign it, next. One slot of the window each. Sign every one plainly and the last fan is still stood
    //     there when the barrier closes — reaching the whole fence takes waving past at least one of them.
    //   * Ask their name, pose for the photo. Two slots each, worth far more to the person in front of you,
    //     and every one of them puts you a fan behind: it takes a wave past somebody to catch up again. Stop
    //     for every fan in a queue of ten and you meet five.
    //   * Wave, and keep moving. Costs no time at all — it is how you catch up — but the fan feels it.
    //
    // So a queue of ten is all reached by, say, three photos, four waves and three plain signatures.
    //
    // Seeded off the booking so the same session always brings the same faces — the ledger records against
    // this activity, and a queue that re-rolled every time the player walked away and back would be a way
    // of shopping for a better hour.
    public static class SigningContent
    {
        // A queue is a fan per this many minutes of the window, never fewer than MinQueue. The window is
        // then cut into one slot fewer than there are fans: a plain signature takes a slot, a name or a
        // photo takes two, and a wave takes none.
        const float MinutesPerFan = 5f;
        const int MinQueue = 10;
        const float FastSlots = 1f;
        const float NameSlots = 2f;
        const float PhotoSlots = 2f;
        const float WaveSlots = 0f;

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
            new("WOMAN IN LAST YEAR'S SHIRT", "a sponsor decal off an old car",
                "Peeled this off the show car in ninety-nine. Reckoned you'd know what it was."),
            new("MAN HOLDING A BABY", "a tiny pair of ear defenders",
                "First race. She'll not remember it. I will."),
            new("BLOKE WHO CAME ALONE", "a helmet, not yours",
                "I race Saturdays. Nothing like this. But I race."),
            new("GIRL AT THE BACK OF THE QUEUE", "a notebook full of lap times",
                "I write every one of your races down. Ask me about Bristol. Go on."),
        };

        // Who is at the front of the queue, as the bubble over them names them. Everyone in the line is just
        // a fan: what they are holding and what they say is who they are.
        public const string FanSpeaker = "A FAN";

        // The longest queue a session can bring — the whole cast. The fence builds this many places along
        // the rail, so every beat has somebody stood there to say it.
        public static int MaxQueue => Queue.Length;

        public static WeekendConversation Build(WeekendActivity a)
        {
            bool parade = a != null && a.kind == ActivityKind.HaulerParade;

            var rng = WeekendRandom.For(WeekendLedger.WeekendId, a != null ? a.id.GetHashCode() : 0, 41);
            var order = new Fan[Queue.Length];
            System.Array.Copy(Queue, order, Queue.Length);
            rng.Shuffle(order);

            // The window the booking blocks out, and the queue stood at the fence for it. The window holds
            // one plain signature fewer than the queue has fans, so reaching the end means waving past
            // somebody — and one more wave for every fan you stopped to talk to.
            float window = a != null && a.minutes > 0 ? a.minutes : 30f;
            int queueLength = (int)(window / MinutesPerFan);
            if (queueLength < MinQueue) queueLength = MinQueue;
            if (queueLength > order.Length) queueLength = order.Length;
            float slot = window / (queueLength - 1);

            var c = new WeekendConversation
            {
                statKey = "autographs",
                statCount = 0,
                minuteBudget = window,
                minuteStep = FastSlots * slot,
                greeting = parade
                    ? new[]
                    {
                        "Gates opened twenty minutes ago and the haulers are still coming in.",
                        "They are three deep along the fence and every one of them can see you.",
                        "Walk it at their pace or walk it at ours — anyone you stop for is somebody at the back who never gets to you.",
                    }
                    : new[]
                    {
                        "Table's set up on the inside of the fence. Marker's there.",
                        "You have got the hour, and the hour is the whole job.",
                        "Sign and move and you will nearly get to the end of them. Stop and talk and you will not — not without walking past somebody.",
                    },
                farewell = new[] { "That is the lot of them — every single one. Nobody went home empty-handed." },
                timeUpFarewell = new[]
                {
                    "That is your time. They are moving the rest of the queue along now.",
                    "Some of them had been stood there since the gates opened. They will tell people that.",
                },
            };

            for (int i = 0; i < queueLength; i++)
            {
                var fan = order[i];
                c.Add(new WeekendBeat
                {
                    speaker = FanSpeaker,
                    preamble = Preamble(i, queueLength),
                    line = fan.line,
                    question = $"{Describe(fan.who)}, holding {fan.holding}.",
                    choices =
                    {
                        WeekendConversation.Say(
                            "Sign it, and ask them their name.",
                            "They tell you, twice, and read it back off the card the whole way to the car park.",
                            appeal: 1.6f, sponsor: 0.1f, score: 1f, statCount: 1, minutes: NameSlots * slot),
                        WeekendConversation.Say(
                            "Sign it and get a photo with them.",
                            "It is on the internet before you have put the lid back on the marker.",
                            appeal: 1.9f, sponsor: 0f, media: 1.5f, score: 1f, statCount: 1, minutes: PhotoSlots * slot),
                        WeekendConversation.Say(
                            "Sign it. Next.",
                            "Signed, handed back, and the queue moves a place.",
                            appeal: 0.25f, sponsor: 0.8f, score: 0.55f, statCount: 1, minutes: FastSlots * slot),
                        WeekendConversation.Say(
                            "Wave, and keep moving.",
                            "They put their arm down slowly.",
                            appeal: -0.6f, sponsor: -0.2f, score: 0.2f, minutes: WaveSlots * slot),
                    },
                });
            }

            // How many got to the front, filled in when the obligation settles and read by the headline.
            int served = 0;

            // What the hour was worth as an hour, on top of what each person in it was worth.
            c.epilogue = (o, answered) =>
            {
                served = answered;
                int left = queueLength - answered;

                if (left > 0)
                {
                    // Everyone still behind the barrier when it closed queued for nothing, and the rep who
                    // booked the appearance was counting how many of them got a card.
                    o.fanAppeal -= 0.5f * left;
                    o.sponsorMood -= 0.3f * left;
                }
                else
                {
                    o.fanAppeal += 1f;
                    o.sponsorMood += 1.5f;
                }

                // And how it was worked. The grade is the average of the answers, so a queue given a minute
                // each comes out at 1 and a queue given a signature and a shoulder at about a half — which
                // is below the line, and takes the hour's fan support with it however many got signed.
                // Bounded either way: one afternoon at a fence is not a career.
                float tone = (o.score - 0.75f) * 1.8f * answered;
                if (tone > 6f) tone = 6f;
                else if (tone < -6f) tone = -6f;
                o.fanAppeal += tone;
                return o;
            };

            c.headline = o =>
            {
                int left = queueLength - served;
                if (o.statCount == 0) return "You stood at the fence for an hour and did not sign a thing.";
                if (left <= 0 && o.fanAppeal <= 1f)
                    return $"{o.statCount} signed, the whole queue cleared, and not one of them got a word out of you.";
                if (left <= 0) return $"{o.statCount} of them, every one, and the fence emptied happy.";
                if (o.fanAppeal >= 6f)
                    return $"{o.statCount} of them got a minute of you. The {left} behind them got a closed barrier.";
                return $"{o.statCount} served at the fence. {left} were still stood there when time ran out.";
            };
            return c;
        }

        // "KID IN A TEAM SHIRT" -> "Kid in a team shirt": the description reads as the start of a sentence
        // over the choices now that the bubble just says it is a fan.
        static string Describe(string who)
        {
            if (string.IsNullOrEmpty(who)) return "A fan";
            string lower = who.ToLowerInvariant();
            return char.ToUpperInvariant(lower[0]) + lower.Substring(1);
        }

        // The fence talking to itself while the queue moves. Nothing here is an answer — it is the pressure
        // of the window, which is the thing the player is spending.
        static string[] Preamble(int index, int queueLength)
        {
            if (index == 0) return null;
            if (index == queueLength - 1) return new[] { "Last one the barrier will hold." };
            if (index == queueLength / 2) return new[] { "Somebody in a tabard looks at their watch and then at you." };
            return new[] { "The queue shuffles forward." };
        }
    }
}
