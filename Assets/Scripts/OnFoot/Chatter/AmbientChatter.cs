using UnityEngine;

namespace Draftmaster.Chatter
{
    // Where the speaker is standing. Each area gets its own pool so a tyre bay doesn't talk about hot dogs.
    public enum ChatterArea { Paddock, PitLane, Garage }

    // What the crowd makes of the player right now, derived from fan appeal.
    public enum ChatterMood { Dismissive, Neutral, Impressed }

    // One-liners background NPCs mutter as the player walks past — the half of a crowd that makes it feel
    // populated rather than decorated. Deliberately NOT a conversation: no prompt, no input, no state on the
    // player. Line choice is pure and seeded so it can be unit-tested without entering Play Mode.
    //
    // The component that speaks these is NPCAmbientChatter.
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
            "Long old day. Third coffee already.",
            "Hauler's blocking the whole lane again.",
            "Track temp's climbing. Watch the rears.",
            "Anyone seen the tyre sheets?",
            "Grid walk's in twenty. Look busy.",
            "Weather radar says we're fine 'til five.",
            "That's a lovely bit of bodywork, that.",
            "Mind your back — cart coming through.",
        };

        static readonly string[] PaddockImpressed =
        {
            "That's the one everybody's talking about.",
            "Saw your last stint. Proper drive, that.",
            "Reckon they've got a shot this weekend.",
            "Told you they'd be quick here.",
            "Ask for a photo. Go on, ask.",
        };

        static readonly string[] PaddockDismissive =
        {
            "Who's that, then? No idea.",
            "Bit early to be walking about, isn't it?",
            "Another hopeful. We get a few.",
            "Don't recognise the number.",
        };

        static readonly string[] PitLaneNeutral =
        {
            "Guns are charged. Fuel rig's next.",
            "Box, box — practice run, on three.",
            "Keep the lane clear, please.",
            "Right rear's the slow one today.",
            "Limiter's on from the blend line.",
            "Wall's live. Eyes up.",
        };

        static readonly string[] PitLaneImpressed =
        {
            "Fastest stop of the day was theirs.",
            "Crew's buzzing about that last lap.",
            "Give 'em room, that's the one to watch.",
        };

        static readonly string[] PitLaneDismissive =
        {
            "Mind the airlines, whoever you are.",
            "You're stood in the fast lane, mate.",
            "Credentials, please. Anyone check those?",
        };

        static readonly string[] GarageNeutral =
        {
            "Chassis is on the plates 'til lunch.",
            "That gearbox is coming out again.",
            "Sponsors want the car spotless by four.",
            "We're two tenths off on the sim.",
        };

        static readonly string[] GarageImpressed =
        {
            "Setup notes from that run were gold.",
            "Whole shop's talking about the weekend.",
        };

        static readonly string[] GarageDismissive =
        {
            "Don't touch anything, please.",
            "Tools stay in the shop. Every time.",
        };

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
        {
            var pool = Lines(area, mood);
            if (pool == null || pool.Length == 0) return string.Empty;
            if (pool.Length == 1) return pool[0];

            // Non-negative index from an arbitrary (possibly negative) seed.
            int i = (int)((uint)Hash(seed) % (uint)pool.Length);
            if (pool[i] == lastLine) i = (i + 1) % pool.Length;
            return pool[i];
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
