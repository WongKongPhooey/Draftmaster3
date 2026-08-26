namespace Draftmaster.Weekend
{
    // The hours the money actually pays for, had under the hospitality awning in the middle of the paddock
    // rather than in a menu: a guest asks you something with the brand stood next to them, or a
    // photographer wants twenty minutes, or somebody's regional sales team wants to watch you change a
    // wheel.
    //
    // The trade is always the same shape. There is a line the sponsor paid for, and there is the honest or
    // funny answer, and they are rarely the same sentence. Staying on message buys sponsor mood and prints
    // nothing; going off it buys fans and press and costs you the room.
    public static class SponsorContent
    {
        public static WeekendConversation Build(WeekendActivity a, string sponsorName)
        {
            string brand = string.IsNullOrEmpty(sponsorName) ? "the sponsor" : sponsorName;
            bool shoot = a != null && a.kind == ActivityKind.PhotoShoot;
            return shoot ? PhotoShoot(a, brand) : Hospitality(a, brand);
        }

        // ------------------------------------------------------------------ the suite

        static WeekendConversation Hospitality(WeekendActivity a, string brand)
        {
            var c = new WeekendConversation
            {
                statKey = "sponsordays",
                statCount = 1,
                greeting = new[]
                {
                    $"There he is. Right — twenty of {brand}'s best customers, and they have been drinking since eleven.",
                    "Shake hands, take the questions, and try to say the name once a sentence.",
                },
                farewell = new[] { "That will do. They will be talking about that all the way home." },
            };

            c.Add(new WeekendBeat
            {
                speaker = "GUEST",
                line = "So what's it actually like out there? Is it as fast as it looks on the telly?",
                question = "What's it like out there?",
                choices =
                {
                    WeekendConversation.Say(
                        $"Faster. And the car's only that good because of the people behind {brand}.",
                        "That is exactly the answer they printed on the invite.",
                        sponsor: 9f, score: 0.8f),
                    WeekendConversation.Say(
                        "Honestly? Half of it is terrifying and the other half is paperwork.",
                        "The room laughs. Somebody's phone is out.",
                        sponsor: -3f, appeal: 2.2f, media: 4f, score: 0.7f),
                    WeekendConversation.Say(
                        "It's a job. A good one, but a job.",
                        "Fair enough, they say, and go back to their drinks.",
                        sponsor: -1f, morale: 1f, score: 0.4f),
                },
            });

            c.Add(new WeekendBeat
            {
                speaker = "REGIONAL MANAGER",
                preamble = new[] { "One of them has a question they have clearly been saving." },
                line = "Go on then — are you going to win one this year?",
                question = "Are you going to win one this year?",
                choices =
                {
                    WeekendConversation.Say(
                        $"With this car and {brand} on the hood? I'd back us.",
                        "That gets a cheer. The area manager looks delighted.",
                        sponsor: 10f, appeal: 1.5f, score: 0.9f),
                    WeekendConversation.Say(
                        "Ask me after Sunday.",
                        "Safe. They laugh politely and move on.",
                        sponsor: 2f, score: 0.5f),
                    WeekendConversation.Say(
                        "If the car's under me, yes. If it's the one we had last week, no.",
                        "Honest. The crew chief is going to hear about that by dinner.",
                        sponsor: -4f, morale: -5f, media: 5f, appeal: 1f, score: 0.6f),
                },
            });

            c.Add(new WeekendBeat
            {
                speaker = "HOSPITALITY HOST",
                line = "Last one — will you do the photo with the whole table? They have all got the cap on.",
                question = "Do the photo?",
                choices =
                {
                    WeekendConversation.Say("Every one of them. And I'll sign the caps.",
                        "You are there twenty minutes longer than the schedule said. Nobody minds.",
                        sponsor: 8f, appeal: 2f, money: 350, score: 1f, statCount: 0),
                    WeekendConversation.Say("One photo, all of them in it. Then I have to go.",
                        "Done in four minutes and everybody got what they came for.",
                        sponsor: 4f, appeal: 0.8f, score: 0.7f),
                    WeekendConversation.Say("I'm due at the car.",
                        "The host smiles at you and apologises to them.",
                        sponsor: -9f, score: 0.2f, ends: true),
                },
            });

            c.headline = o => o.sponsorMood >= 18f
                ? "An hour of hands shaken and the brand's name said out loud. They will pay for that again."
                : o.sponsorMood <= 0f
                    ? "You were funny, and honest, and the people paying for the hood did not enjoy it."
                    : "Turned up, did the hour, said the name. Nobody unhappy.";
            return c;
        }

        // ------------------------------------------------------------------ the shoot

        static WeekendConversation PhotoShoot(WeekendActivity a, string brand)
        {
            var c = new WeekendConversation
            {
                statKey = "sponsordays",
                statCount = 1,
                greeting = new[]
                {
                    "Twenty minutes and the light is going, so let's not waste any of it.",
                    "Stand there, look at the car, and do what I ask.",
                },
                farewell = new[] { "Got it. That is the season's poster sorted." },
            };

            c.Add(new WeekendBeat
            {
                speaker = "PHOTOGRAPHER",
                line = "First set: helmet under the arm, hand on the roof. What are you giving me — hero, or human?",
                question = "How are you playing it?",
                choices =
                {
                    WeekendConversation.Say("Hero. Chin up, straight down the lens.",
                        "That's the one that goes on the hauler.",
                        sponsor: 8f, appeal: 1.6f, money: 350, score: 0.9f),
                    WeekendConversation.Say("Human. Give me the crew in the shot too.",
                        "Now that's a picture. The boys will love it.",
                        sponsor: 4f, morale: 8f, appeal: 1.2f, score: 0.9f),
                    WeekendConversation.Say("Whatever's quickest.",
                        "Quick it is. It'll be fine.",
                        sponsor: 1f, score: 0.4f),
                },
            });

            c.Add(new WeekendBeat
            {
                speaker = "BRAND REP",
                preamble = new[] { $"{brand}'s marketing lead has walked over holding a cap." },
                line = "Can we get the logo cleanly in every frame? It is the whole reason we are stood here.",
                question = "Do you wear the cap?",
                choices =
                {
                    WeekendConversation.Say("Cap on, logo forward, every shot.",
                        "Perfect. That is the deliverable signed off.",
                        sponsor: 12f, money: 450, score: 1f),
                    WeekendConversation.Say("On for half of them. I want a couple that look like me.",
                        "Reasonable. We can work with half.",
                        sponsor: 5f, appeal: 1.4f, score: 0.7f),
                    WeekendConversation.Say("It's a photo of a driver, not a hat.",
                        "The rep writes something down and does not smile.",
                        sponsor: -12f, media: 3f, appeal: 1.8f, score: 0.4f),
                },
            });

            c.headline = o => o.sponsorMood >= 15f
                ? "Twenty minutes, the light held, and the logo is in every frame."
                : o.sponsorMood <= 0f
                    ? "The pictures are good. The brand wanted different pictures."
                    : "Shot done. Half of it is usable, which is about par.";
            return c;
        }
    }
}
