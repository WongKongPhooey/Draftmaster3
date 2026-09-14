using UnityEngine;

namespace Draftmaster.Chatter
{
    // Where the speaker is standing. Each area gets its own pool so a tyre bay doesn't talk about hot dogs.
    public enum ChatterArea { Paddock, PitLane, Garage }

    // What the crowd makes of the player right now, derived from fan appeal.
    public enum ChatterMood { Dismissive, Neutral, Impressed }

    // What the circuit is doing while the line is spoken.
    //
    // A paddock that says the same things during Friday setup as it does ten minutes before the Cup race
    // is a paddock nobody lives in. Fan appeal already decides the crowd's TONE; this decides its SUBJECT,
    // so the same people sound like they know what day it is.
    //
    // Kept as its own enum, with values matching the plain ints CrowdPolicy uses to size the crowd's
    // noise, so this module still knows nothing about the weekend rules. The caller does the mapping.
    // `None` is the no-topic case a caller with nothing to say about the session passes, and is what the
    // topic-less Pick overload uses.
    public enum ChatterTopic { None = -1, Idle = 0, Practice = 1, Qualifying = 2, Race = 3 }

    // One-liners background NPCs mutter as the player walks past — the half of a crowd that makes it feel
    // populated rather than decorated. Deliberately NOT a conversation: no prompt, no input, no state on the
    // player. Line choice is pure and seeded so it can be unit-tested without entering Play Mode.
    //
    // The component that speaks these is NPCAmbientChatter.
    //
    // A line may name the two people the paddock actually knows — {playerfirst} for the driver, {chieffirst}
    // for their crew chief — and Pick fills those in before handing the line over. Used sparingly on
    // purpose: a crowd that says your name every time reads as a crowd that has been told to.
    public static class AmbientChatter
    {
        // Fan-appeal thresholds (FanAppeal runs 0..100) at which the crowd's tone changes.
        public const float ImpressedAppeal = 62f;
        public const float DismissiveAppeal = 34f;

        public static ChatterMood MoodFor(float fanAppeal)
        {
            if (fanAppeal >= ImpressedAppeal) return ChatterMood.Impressed;
            if (fanAppeal <= DismissiveAppeal) return ChatterMood.Dismissive;
            return ChatterMood.Neutral;
        }

        static readonly string[] PaddockNeutral =
        {
            "Long day, third coffee already.",
            "Don't these people have places to be?.",
            "Track temp's climbing. Should mix it up.",
            "Anyone seen our tyre sheets?",
            "Just pick up a tyre and look busy.",
            "Weather radar says clear skies.",
            "My money's on {playerlast} today.",
            "Mind out, trucks coming through.",
            "Morning, {playerfirst}!",
            "{chieffirst} was after you, {playerfirst}.",
        };

        static readonly string[] PaddockImpressed =
        {
            "That's the one everybody's talking about.",
            "Saw your last stint. What a drive.",
            "Reckon they've got a shot this weekend.",
            "Told you they'd be quick here.",
            "Ask for a photo. Go on, ask.",
            "That's {playerfirst}. Told you they'd be around.",
            "{playerfirst}! quick photo?",
        };

        static readonly string[] PaddockDismissive =
        {
            "Who's that, then? No idea.",
            "Another hopeful. We get a few.",
            "Don't recognise the number.",
            "Some driver called {playerfirst}. Never heard of them.",
        };

        static readonly string[] PitLaneNeutral =
        {
            "Guns are charged. Fuel rig's next.",
            "Box, box — practice run, on three.",
            "Keep the lane clear, please.",
            "Right rear's the slow one today.",
            "Limiter's on from the blend line.",
            "Wall's live. Eyes up.",
            "{chieffirst} wants the right rear checked before we roll.",
            "{chieffirst} needs a fuel number before the stop.",
            "Box is yours whenever you're ready, {playerfirst}.",
        };

        static readonly string[] PitLaneImpressed =
        {
            "Fastest stop of the day was theirs.",
            "Crew's buzzing about that last lap.",
            "Give 'em room, that's the one to watch.",
            "{chieffirst} says that was the lap of the day.",
            "Nice one, {playerfirst}. Whole wall was watching.",
        };

        static readonly string[] PitLaneDismissive =
        {
            "Mind the airlines, whoever you are.",
            "You're stood in the fast lane, mate.",
            "Credentials, please. Anyone check those?",
            "{chieffirst} didn't say anything about visitors.",
        };

        static readonly string[] GarageNeutral =
        {
            "Chassis is on the plates 'til lunch.",
            "That gearbox is coming out again.",
            "Sponsors want the car spotless by four.",
            "We're two tenths off on the sim.",
            "{chieffirst} wants the ride heights before lunch.",
            "Ask {chieffirst}. It's their call, not mine.",
        };

        static readonly string[] GarageImpressed =
        {
            "Setup notes from that run were gold.",
            "Whole shop's talking about the weekend.",
            "{chieffirst} reckons we've finally built a car under {playerfirst}.",
        };

        static readonly string[] GarageDismissive =
        {
            "Don't touch anything, please.",
            "Tools stay in the shop. Every time.",
            "{chieffirst} said nobody touches the car. That includes you.",
        };

        // ---------------------------------------------------------------- what the session is

        // Session pools. These sit ALONGSIDE the mood pools rather than replacing them: roughly half the
        // barks in an area that has them are drawn from here and the rest still react to fan appeal, so
        // the crowd talks about the session without every single person doing so.
        //
        // Only the paddock and the pit lane get them. ChatterArea.Garage is the team's own shop, which is
        // somewhere else entirely and has no session running — it falls through to its mood pools.

        static readonly string[] PaddockIdle =
        {
            "Track's cold. Good hour to get the trolleys shifted.",
            "Nothing running till later. Cup of tea, then.",
            "Half this lot are queued up at the signing tent.",
            "Media pen's over by the tower if they're after you.",
            "Quiet, isn't it. Won't last.",
            "Nobody's on track, {playerfirst}. Go and get some lunch.",
        };

        static readonly string[] PaddockPractice =
        {
            "They're out. Hear that?",
            "Long-run pace is what matters today, not one lap.",
            "Half the field's still circulating on old rubber.",
            "Garage two's had the covers off since first thing.",
            "Everyone finds a second before qualifying. Everyone.",
            "You not out yet, {playerfirst}? Session's half gone.",
        };

        static readonly string[] PaddockQualifying =
        {
            "Quali's live. Nobody's watching hospitality now.",
            "One lap. That's all any of them get.",
            "Whoever's quickest today picks their own air tomorrow.",
            "Screens are up by the hauler if you want the times.",
            "Three tenths covering the top ten, apparently.",
            "One clean lap, {playerfirst}. That's the job.",
        };

        static readonly string[] PaddockRace =
        {
            "Grid's forming up. You can feel it from here.",
            "Everyone's gone trackside. Place is emptying out.",
            "Whole weekend comes down to the next couple of hours.",
            "Anthem's done. Won't be long now.",
            "Best part of the job, this bit. Right before.",
            "Go on then, {playerfirst}. Go and win it.",
        };

        static readonly string[] PitLaneIdle =
        {
            "Lane's closed. Nobody's coming in.",
            "Good time to put the guns back on charge.",
            "Get the rig packed down before the next one.",
            "Nothing running. Don't stand about looking busy.",
            "{chieffirst} wants the board rewritten before anyone rolls out.",
        };

        static readonly string[] PitLanePractice =
        {
            "Practice stops on the board. Look alive.",
            "Two runs, then we're changing the bar.",
            "Screen's got the whole field inside three tenths.",
            "Bring it in after this one.",
            "Dry run, everybody. Same as the real thing.",
        };

        static readonly string[] PitLaneQualifying =
        {
            "Out lap, hot lap, in. That's the whole plan.",
            "Clean air or nothing. Don't follow anybody out.",
            "Get some temperature in them before the line.",
            "Two sets left and we're spending one of them now.",
            "{chieffirst} says the gap closes at the end of the lap.",
        };

        static readonly string[] PitLaneRace =
        {
            "Race stops. Everyone on their marks.",
            "Long afternoon, this. Get your fluids in now.",
            "Box on lap twenty-eight unless it changes.",
            "Wall's live from the green. Heads up, all of you.",
            "Whatever happens out there, we do our bit in here.",
        };

        // The session pool for an area, or an empty array where the area has none. Never null.
        public static string[] Topical(ChatterArea area, ChatterTopic topic)
        {
            switch (area)
            {
                case ChatterArea.Paddock:
                    switch (topic)
                    {
                        case ChatterTopic.Idle: return PaddockIdle;
                        case ChatterTopic.Practice: return PaddockPractice;
                        case ChatterTopic.Qualifying: return PaddockQualifying;
                        case ChatterTopic.Race: return PaddockRace;
                    }
                    break;
                case ChatterArea.PitLane:
                    switch (topic)
                    {
                        case ChatterTopic.Idle: return PitLaneIdle;
                        case ChatterTopic.Practice: return PitLanePractice;
                        case ChatterTopic.Qualifying: return PitLaneQualifying;
                        case ChatterTopic.Race: return PitLaneRace;
                    }
                    break;
            }
            return System.Array.Empty<string>();
        }

        // Authored lines, layered over the built-in tables. DialogueLibrary installs this at runtime so a
        // track's own DialoguePool asset can add to (or replace) what the crowd says here; left null this
        // class stays exactly what it was — pure, seeded, testable, no Resources, no track lookup.
        // Returning null or an empty array falls through to the built-ins.
        public static System.Func<ChatterArea, ChatterMood, string[]> Provider;

        // The pool for an area/mood pairing. Never empty — an area with nothing mood-specific falls back
        // to its neutral lines, so a caller can always speak something.
        public static string[] Lines(ChatterArea area, ChatterMood mood)
        {
            if (Provider != null)
            {
                var authored = Provider(area, mood);
                if (authored != null && authored.Length > 0) return authored;
            }
            return BuiltIn(area, mood);
        }

        // The tables compiled into the game. Public so the authored pools can be layered on top of them
        // rather than having to restate them.
        public static string[] BuiltIn(ChatterArea area, ChatterMood mood)
        {
            switch (area)
            {
                case ChatterArea.PitLane:
                    if (mood == ChatterMood.Impressed) return PitLaneImpressed;
                    if (mood == ChatterMood.Dismissive) return PitLaneDismissive;
                    return PitLaneNeutral;
                case ChatterArea.Garage:
                    if (mood == ChatterMood.Impressed) return GarageImpressed;
                    if (mood == ChatterMood.Dismissive) return GarageDismissive;
                    return GarageNeutral;
                default:
                    if (mood == ChatterMood.Impressed) return PaddockImpressed;
                    if (mood == ChatterMood.Dismissive) return PaddockDismissive;
                    return PaddockNeutral;
            }
        }

        // Pick a line, avoiding an immediate repeat of `lastLine` whenever the pool has an alternative.
        // Seeded rather than Random.value so the same walk-past can be reproduced in a test.
        public static string Pick(ChatterArea area, ChatterMood mood, int seed, string lastLine = null)
            => PickFrom(Lines(area, mood), seed, lastLine);

        // As above, but the speaker also knows what the circuit is doing. Roughly half the barks come from
        // the session pool and the rest from the mood pool, so the crowd sounds like it knows what day it
        // is without turning into a public-address system. Which half a given bark falls in is decided by
        // the seed, so this stays as reproducible as the line choice itself.
        //
        // An area with no session lines (the team's own garage) or a caller with no session to report
        // (ChatterTopic.None) is exactly the topic-less call above.
        public static string Pick(ChatterArea area, ChatterMood mood, ChatterTopic topic,
                                  int seed, string lastLine = null)
        {
            var topical = Topical(area, topic);
            bool useTopic = topical.Length > 0 && (Hash(seed ^ TopicSalt) & 1) == 0;
            return PickFrom(useTopic ? topical : Lines(area, mood), seed, lastLine);
        }

        // Arbitrary constant, so the topic/mood coin flip is independent of the index the same seed picks.
        const int TopicSalt = unchecked((int)0x9E3779B9);

        static string PickFrom(string[] pool, int seed, string lastLine)
        {
            if (pool == null || pool.Length == 0) return string.Empty;
            if (pool.Length == 1) return SpeakerIdentity.Fill(pool[0]);

            // Non-negative index from an arbitrary (possibly negative) seed.
            int i = (int)((uint)Hash(seed) % (uint)pool.Length);
            // Compared filled, because that is what the caller kept: a pool line reading "Morning,
            // {playerfirst}" and the bark that was actually spoken are not the same string.
            if (SpeakerIdentity.Fill(pool[i]) == lastLine) i = (i + 1) % pool.Length;
            return SpeakerIdentity.Fill(pool[i]);
        }

        // Small integer avalanche so consecutive seeds (e.g. frame counts) don't walk the pool in order.
        static int Hash(int x)
        {
            unchecked
            {
                uint h = (uint)x;
                h ^= h >> 16; h *= 0x7feb352du;
                h ^= h >> 15; h *= 0x846ca68bu;
                h ^= h >> 16;
                return (int)h;
            }
        }

        // Seconds a bark stays up: long enough to read, scaled by length.
        public static float ReadSeconds(string line, float perCharacter = 0.055f, float minimum = 2.2f, float maximum = 5f)
            => string.IsNullOrEmpty(line) ? minimum : Mathf.Clamp(line.Length * perCharacter, minimum, maximum);
    }
}
