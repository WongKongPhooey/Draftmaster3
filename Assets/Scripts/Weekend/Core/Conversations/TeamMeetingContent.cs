namespace Draftmaster.Weekend
{
    // The meetings, as they are actually had: the crew chief stood at the box with the car in bits behind
    // him before the weekend starts, and the engineer sat across the dinette in your own motorhome after
    // you have driven it.
    //
    // There is no right answer, only a shape: a qualifying car is not a race car, and a driver who tells
    // the truth about a bad car gets a better car and a worse room. What a meeting is worth is setup
    // knowledge — the pace the weekend banks — and it is worth far more once you have driven the thing.
    public static class TeamMeetingContent
    {
        // True when the player actually ran their own practice session this weekend. A debrief about a
        // session you skipped is a shorter meeting with less in it.
        public static bool RanOwnPractice()
        {
            var practice = WeekendLedger.Timetable?.PlayerSession(ActivityKind.Practice);
            return practice != null && WeekendLedger.IsDone(practice.id);
        }

        public static WeekendConversation Build(WeekendActivity a)
        {
            bool debrief = a != null && a.kind == ActivityKind.Debrief;
            return debrief ? Debrief() : Briefing();
        }

        // ------------------------------------------------------------------ after the session, in the RV

        static WeekendConversation Debrief()
        {
            bool ran = RanOwnPractice();
            float weight = ran ? 1f : 0.35f;

            var c = new WeekendConversation
            {
                statKey = "teammeetings",
                greeting = ran
                    ? new[] { "Come in, shut the door. I've got the tyre traces up." }
                    : new[] { "Come in. Not much to look at — we didn't run, so this is all sim." },
                farewell = new[] { "Right. I'll take it to the boys and we'll have it under you in the morning." },
            };

            c.Add(new WeekendBeat
            {
                speaker = "ENGINEER",
                line = ran
                    ? "Tyre data says we fall off hard after fifteen laps. Long run or one lap — what do you want out of this car?"
                    : "Tell me what you think you want and I'll build to it.",
                question = "What do you want out of the car?",
                choices =
                {
                    WeekendConversation.Say(
                        "Build me a race car. I'll qualify where I qualify.",
                        "Good. That's the car I wanted to build anyway.",
                        setup: 0.14f * weight, morale: 8f, score: 0.8f),
                    WeekendConversation.Say(
                        "Give me one lap. I'll take track position and defend it.",
                        "Then it'll be a handful at lap forty. Your call — you're driving it.",
                        setup: 0.10f * weight, morale: 2f, appeal: 0.8f, score: 0.6f),
                    WeekendConversation.Say(
                        "The car's junk. Fix the front end before we talk about anything else.",
                        "...alright. We'll pull the nose apart.",
                        setup: 0.17f * weight, morale: -9f, media: 3f, score: 0.9f),
                },
            });

            c.headline = o =>
                o.setupGain >= 0.12f ? "You left with a plan and the engineers left with a job. That is a good debrief."
                : o.teamMorale < 0f ? "Honest, useful, and the room did not enjoy it."
                : "Plan agreed. Everybody knows what the car is now.";
            return c;
        }

        // ------------------------------------------------------------------ before the weekend, at the box

        static WeekendConversation Briefing()
        {
            var c = new WeekendConversation
            {
                statKey = "teammeetings",
                greeting = new[]
                {
                    "There you are. Mind the jack.",
                    "Three days, one race. Let's agree what we're doing with them.",
                },
                farewell = new[] { "That'll do me. Go and get some air before the truck series roll out." },
            };

            c.Add(new WeekendBeat
            {
                speaker = "CREW CHIEF",
                line = "Sim says this place is all about the middle of the corner. How are we running it?",
                question = "How are we running the weekend?",
                choices =
                {
                    WeekendConversation.Say(
                        "Your call. You've been right more than I have.",
                        "Then we run my sheet. Thank you.",
                        setup: 0.09f, morale: 10f, score: 0.7f),
                    WeekendConversation.Say(
                        "Aggressive on strategy. Track position over tyres.",
                        "Bold. I'll have the numbers ready either way.",
                        setup: 0.07f, morale: 2f, appeal: 1.2f, media: 3f, score: 0.6f),
                    WeekendConversation.Say(
                        "Play it safe. Points now, swings later in the year.",
                        "Sensible. Nobody ever got fired for sensible.",
                        setup: 0.06f, morale: 5f, sponsor: 6f, media: -3f, score: 0.5f),
                },
            });

            c.Add(new WeekendBeat
            {
                speaker = "TEAM MANAGER",
                preamble = new[] { "One more thing before you go." },
                line = "The sponsor's people are all over you this weekend. How much of it do you want me to take off your plate?",
                question = "How much of the sponsor's weekend do you want?",
                choices =
                {
                    WeekendConversation.Say(
                        "None. I'll do the lot — that's the job.",
                        "They'll love hearing that. So will I.",
                        sponsor: 12f, morale: 4f, score: 0.8f),
                    WeekendConversation.Say(
                        "Keep the ones I have to do and lose the rest.",
                        "Fair. I'll thin it out.",
                        sponsor: 2f, setup: 0.04f, score: 0.6f),
                    WeekendConversation.Say(
                        "All of it. I'm here to drive the car.",
                        "I'll tell them you're focused. They'll hear something else.",
                        sponsor: -10f, setup: 0.08f, morale: 3f, score: 0.4f),
                },
            });

            c.headline = o =>
                o.setupGain >= 0.15f ? "You left with a plan and the engineers left with a job. That is a good meeting."
                : o.sponsorMood < 0f ? "The plan is set. The sponsor's people are less sure about you."
                : "Plan agreed. Everybody knows what the weekend is now.";
            return c;
        }
    }
}
