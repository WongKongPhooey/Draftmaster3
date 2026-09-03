namespace Draftmaster.Weekend
{
    // The signing session, done through the fence at the edge of the paddock with the fans on the other
    // side of it, and the hauler parade, which is the same thing walked rather than stood at.
    //
    // A queue, one person at a time, each of them holding something and wanting thirty seconds. What they
    // are worth is not a timing bar — it is what you give them, and the window is the whole decision. The
    // fence holds exactly as many people as a driver working flat out could get through, so:
    //
    //   * Sign it, next. Five minutes a head, and you reach every one of them. The rep counts heads, the
    //     sponsor is delighted, and a queue that got a signature and a shoulder goes home telling the other
    //     story — the hour is worth almost nothing in fan support and can go backwards.
    //   * Ask their name, pose for the photo. Ten or twelve minutes each, worth far more to the person in
    //     front of you, and the people behind them never reach the front at all. Fan support up, sponsor
    //     mood down, and the ones left standing cost you something on the way out.
    //
    // Seeded off the booking so the same session always brings the same faces — the ledger records against
    // this activity, and a queue that re-rolled every time the player walked away and back would be a way
    // of shopping for a better hour.
    public static class SigningContent
    {
        // What each answer takes out of the window, in minutes. Fast is the yardstick: the queue is built
        // to be exactly as long as a driver signing and moving could clear.
        const float FastMinutes = 5f;
        const float NameMinutes = 10f;
        const float PhotoMinutes = 12f;
        const float WaveMinutes = 2f;

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

        public static WeekendConversation Build(WeekendActivity a)
        {
            bool parade = a != null && a.kind == ActivityKind.HaulerParade;

            var rng = WeekendRandom.For(WeekendLedger.WeekendId, a != null ? a.id.GetHashCode() : 0, 41);
            var order = new Fan[Queue.Length];
            System.Array.Copy(Queue, order, Queue.Length);
            rng.Shuffle(order);

            // The window the booking blocks out, and the queue that fits inside it. The fence is as long as
            // signing-and-moving would clear, so getting to the end of it is only possible flat out.
            float window = a != null && a.minutes > 0 ? a.minutes : 30f;
            int queueLength = (int)(window / FastMinutes);
            if (queueLength < 3) queueLength = 3;
            if (queueLength > order.Length) queueLength = order.Length;

            var c = new WeekendConversation
            {
                statKey = "autographs",
                statCount = 0,
                minuteBudget = window,
                minuteStep = FastMinutes,
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
                        "Sign and move and you will get to the end of them. Stop and talk and you will not.",
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
                    speaker = fan.who,
                    preamble = Preamble(i, queueLength),
                    line = fan.line,
                    question = $"They're holding {fan.holding}.",
                    choices =
                    {
                        WeekendConversation.Say(
                            "Sign it, and ask them their name.",
                            "They tell you, twice, and read it back off the card the whole way to the car park.",
                            appeal: 1.6f, sponsor: 0.1f, score: 1f, statCount: 1, minutes: NameMinutes),
                        WeekendConversation.Say(
                            "Sign it and get a photo with them.",
                            "It is on the internet before you have put the lid back on the marker.",
                            appeal: 1.9f, sponsor: 0f, media: 1.5f, score: 1f, statCount: 1, minutes: PhotoMinutes),
                        WeekendConversation.Say(
                            "Sign it. Next.",
                            "Signed, handed back, and the queue moves a place.",
                            appeal: 0.25f, sponsor: 0.8f, score: 0.55f, statCount: 1, minutes: FastMinutes),
                        WeekendConversation.Say(
                            "Wave, and keep moving.",
                            "They put their arm down slowly.",
                            appeal: -0.6f, sponsor: -0.2f, score: 0.2f, minutes: WaveMinutes),
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
