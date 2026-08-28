namespace Draftmaster.Weekend
{
    // The conversation a career only ever has once: the crew chief at the pit box, on the first Friday
    // morning, telling a new driver that the phone in their pocket is where the rest of the game is kept.
    //
    // It exists because everything the on-foot half of this game asks you to remember - what is on today,
    // which favour you agreed to for whoever stopped you by the fence, which of those are finished and
    // waiting to be handed back - is on the phone, and nothing else in the paddock ever says so out loud.
    // A player who never presses the key never finds any of it.
    //
    // Written as a conversation rather than a tooltip for the same reason every other obligation is: the
    // weekend happens to you in the world. The key itself is passed in by the runtime layer (WeekendScripts
    // reads it off PhoneUI) so the lines cannot go stale if it is ever rebound - this assembly cannot see
    // the phone, and should not have to.
    public static class OrientationContent
    {
        // What the lines say when nobody has told us otherwise. PhoneUI's default toggle.
        public const string DefaultPhoneKey = "P";

        public static WeekendConversation Build(WeekendActivity a, string phoneKey = DefaultPhoneKey)
        {
            string key = string.IsNullOrEmpty(phoneKey) ? DefaultPhoneKey : phoneKey.ToUpperInvariant();

            var c = new WeekendConversation
            {
                greeting = new[]
                {
                    "Before you go anywhere near that car - has anybody set your phone up?",
                    "Thought not. Two minutes, and then I'll leave you alone about it.",
                },
                farewell = new[]
                {
                    "That's it. That's the whole briefing.",
                    "Anything I need you for this weekend turns up on there. Go and be somewhere.",
                },
            };

            // ---- the key itself. Everything else is useless if this beat does not land. ----
            c.Add(new WeekendBeat
            {
                speaker = "CREW CHIEF",
                preamble = new[]
                {
                    $"Phone's in your pocket. {key} brings it up, anywhere you're on foot.",
                    "Arrows to move round the tiles, E to open one, Esc to back out.",
                },
                line = "Go on then. What have you got?",
                question = $"Press {key} - what is on it?",
                choices =
                {
                    WeekendConversation.Say(
                        $"Six tiles. {key} to open it, Esc to put it away.",
                        "That's the lot. You'd be amazed how many rookies never find it.",
                        morale: 5f, setup: 0.02f, score: 0.9f),
                    WeekendConversation.Say(
                        "SCHEDULE - that's the same sheet as the timetable, is it?",
                        "Same three days, yes. The big one's still F10 if you want the whole weekend at once.",
                        morale: 3f, setup: 0.03f, score: 0.8f),
                    WeekendConversation.Say(
                        "I'll have a look at it later.",
                        $"You'll have a look at it Sunday morning with a sponsor's rep stood waiting. Press {key}.",
                        morale: -4f, score: 0.3f),
                },
            });

            // ---- TASKS: the active job list, which is the thing this whole booking exists to point at ----
            c.Add(new WeekendBeat
            {
                speaker = "CREW CHIEF",
                preamble = new[]
                {
                    "The one marked TASKS is the one you actually want.",
                    "What's left in the session, what the team's waiting on, and every job you've picked up walking round this place.",
                },
                line = "If that tile's carrying a number, what does that mean?",
                question = "What is the number on the TASKS tile?",
                choices =
                {
                    WeekendConversation.Say(
                        "Something's finished and somebody's waiting on me to say so.",
                        "Correct. Done isn't done till you've been back to whoever asked. Get it to nothing before we load out.",
                        morale: 6f, setup: 0.02f, score: 1f),
                    WeekendConversation.Say(
                        "How many people want something from me.",
                        "Close enough. It's the finished ones - but they all end up on that list either way.",
                        morale: 3f, score: 0.6f),
                    WeekendConversation.Say(
                        "That I've got too much on and not enough of it is driving.",
                        "Welcome to it. Read the list anyway.",
                        morale: 1f, appeal: 0.5f, score: 0.4f),
                },
            });

            // ---- NOTES: who asked, and for what. The paddock hands quests out in conversation. ----
            c.Add(new WeekendBeat
            {
                speaker = "CREW CHIEF",
                preamble = new[]
                {
                    "There's a NOTES tile as well. Every time you say yes to somebody in this paddock, it writes itself down in there.",
                    "Their name, what they wanted, which weekend they asked. Ticked off when you've done it.",
                },
                line = "So when somebody stops you by the fence on Friday and you've forgotten them by Sunday - where do you look?",
                question = "Where do you look up who asked for what?",
                choices =
                {
                    WeekendConversation.Say(
                        "NOTES. Name, job, and whether I've done it.",
                        "Good. Nobody in this sport gets remembered for the driving alone.",
                        morale: 6f, appeal: 1f, setup: 0.02f, score: 1f),
                    WeekendConversation.Say(
                        "TASKS, and NOTES for who asked me.",
                        "Between the two of them, yes. TASKS is what's outstanding, NOTES is who you owe it to.",
                        morale: 5f, setup: 0.02f, score: 0.9f),
                    WeekendConversation.Say(
                        "I'd just find them and ask.",
                        "You'd be walking this paddock till dark. It's on the phone.",
                        morale: -2f, score: 0.35f),
                },
            });

            c.headline = o => o.score >= 0.7f
                ? $"{key} opens the phone. TASKS is what is outstanding, NOTES is who asked for it."
                : $"You have at least been shown the phone. {key} opens it - TASKS and NOTES are the two that matter.";
            return c;
        }
    }
}
