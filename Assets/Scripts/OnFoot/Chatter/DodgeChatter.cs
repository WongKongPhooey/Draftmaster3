namespace Draftmaster.Chatter
{
    // What somebody shouts after jumping out of the way of the player's scooter.
    //
    // CartDodge already makes the paddock scatter; this gives a few of them a line on the way out, so a near
    // miss reads as a person with an opinion rather than a sprite that slid sideways. Only SOME of them:
    // a crowd where everybody you nearly hit says something is a crowd that has been told to, and a scooter
    // through a busy paddock nearly hits a lot of people. The odds and the line are both decided here, pure
    // and seeded like AmbientChatter, so they can be tested without a scene; CartDodge decides when.
    //
    // Lines may use the SpeakerIdentity tokens ({playerlast}, {playerfirst}) and are filled before they are
    // handed back.
    public static class DodgeChatter
    {
        // Share of dodges that get a line at all. The scene-wide gap in CartDodge thins it further.
        public const float SpeakChance = 0.3f;

        static readonly string[] Lines =
        {
            "Hey, was that {playerlast}?",
            "They ride just like they drive.",
            "Whoa! Watch it!",
            "Was that {playerfirst} on a scooter?",
            "Paddock's not a racetrack, mate!",
            "Lane discipline, {playerlast}!",
            "Is that how you take turn one as well?",
            "Never lifts, that one. Not even in here.",
            "Nearly had my coffee there.",
            "Somebody black-flag {playerlast}.",
            "Pit-lane speed limit applies here too!",
            "No wonder their spotter looks tired.",
        };

        // The built-in table, unfilled. Public for the tests.
        public static string[] All => Lines;

        // Does this dodge get a line? `seed` makes it reproducible; `chance` is 0..1.
        public static bool ShouldSpeak(int seed, float chance = SpeakChance)
        {
            if (chance <= 0f) return false;
            if (chance >= 1f) return true;
            uint h = (uint)Hash(seed ^ ChanceSalt);
            return (h % 10000u) < (uint)(chance * 10000f);
        }

        // A filled line, avoiding an immediate repeat of `lastLine` (as spoken, i.e. filled).
        public static string Pick(int seed, string lastLine = null)
        {
            int i = (int)((uint)Hash(seed) % (uint)Lines.Length);
            if (SpeakerIdentity.Fill(Lines[i]) == lastLine) i = (i + 1) % Lines.Length;
            return SpeakerIdentity.Fill(Lines[i]);
        }

        // Arbitrary, so whether a dodge speaks is independent of which line the same seed would pick.
        const int ChanceSalt = 0x5bd1e995;

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
    }
}
