using System;
using System.Text;

namespace Draftmaster.Chatter
{
    // Who a line is about, so the paddock can use somebody's name instead of talking around it.
    //
    // Every dialogue table in the game is a plain string, which is why the crowd has always addressed the
    // player as "you" and the crew has always called the crew chief "the chief": nothing in a compiled line
    // could know a name that is decided at runtime. This is the small hole in that wall — a line may write
    // {playerfirst} or {chieffirst} and get the real person, filled in wherever the line is spoken.
    //
    // Deliberately pure: no Unity API, no database, no Resources. Who those two people ARE is answered by
    // DialogueNames in the game assembly, which installs itself into the two hooks below at start-up. That
    // keeps this side testable without a scene — set a provider, fill a line, assert the text.
    //
    // Unknown braces are left exactly as they were found. Other systems already substitute their own
    // ({team}/{num} in DriverPresenceDirector, {path} in CareerPathNPC) and run before or after this one, so
    // filling a token this class does not own would eat theirs.
    public static class SpeakerIdentity
    {
        // The four tokens a line may use. Case is ignored, so {PlayerFirst} works too.
        public const string PlayerToken      = "{player}";
        public const string PlayerFirstToken = "{playerfirst}";
        public const string ChiefToken       = "{chief}";
        public const string ChiefFirstToken  = "{chieffirst}";

        // What is said when nobody has a name yet — a demo save with no career, a scene with no roster.
        // Each one has to read as English in the position its token is written in, because a line that
        // cannot be filled is still going to be spoken.
        public const string PlayerFallback      = "the driver";
        public const string PlayerFirstFallback = "mate";
        public const string ChiefFallback       = "Crew Chief";
        public const string ChiefFirstFallback  = "Chief";

        // Installed by DialogueNames. Either may be null (or return nothing), which is what the fallbacks
        // above are for.
        public static Func<string> PlayerNameProvider;
        public static Func<string> CrewChiefNameProvider;

        // Drop both hooks. Tests call this so one test's stub cannot leak into the next.
        public static void Reset()
        {
            PlayerNameProvider = null;
            CrewChiefNameProvider = null;
        }

        // The names as the providers answer them, "" when they cannot.
        public static string PlayerName => Ask(PlayerNameProvider);
        public static string CrewChiefName => Ask(CrewChiefNameProvider);

        public static string PlayerFullName => Or(PlayerName, PlayerFallback);
        public static string PlayerFirstName => Or(FirstNameOf(PlayerName), PlayerFirstFallback);
        public static string CrewChiefFullName => Or(CrewChiefName, ChiefFallback);
        public static string CrewChiefFirstName => Or(FirstNameOf(CrewChiefName), ChiefFirstFallback);

        // "Kyle Larson" -> "Kyle". A one-word name is its own first name; anything blank stays blank.
        public static string FirstNameOf(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return "";
            string trimmed = fullName.Trim();
            int space = trimmed.IndexOfAny(new[] { ' ', '\t' });
            return space < 0 ? trimmed : trimmed.Substring(0, space);
        }

        // Cheap gate so the common case — a line with no braces in it — never allocates.
        public static bool HasToken(string line) => !string.IsNullOrEmpty(line) && line.IndexOf('{') >= 0;

        // Fill every token this class owns. Anything else in braces is copied through untouched.
        public static string Fill(string line)
        {
            if (!HasToken(line)) return line;

            StringBuilder sb = null;
            int i = 0, copiedTo = 0;
            while (i < line.Length)
            {
                if (line[i] != '{') { i++; continue; }

                int close = line.IndexOf('}', i + 1);
                if (close < 0) break;

                string value = ValueOf(line.Substring(i + 1, close - i - 1));
                if (value == null) { i = close + 1; continue; }   // somebody else's token

                sb ??= new StringBuilder(line.Length + 16);
                sb.Append(line, copiedTo, i - copiedTo).Append(value);
                i = copiedTo = close + 1;
            }

            if (sb == null) return line;
            return sb.Append(line, copiedTo, line.Length - copiedTo).ToString();
        }

        // Fill a whole table. Returns the same array when there was nothing in it to fill, so the caller
        // does not copy a table every time an NPC opens their mouth.
        public static string[] Fill(string[] lines)
        {
            if (lines == null) return null;

            string[] filled = null;
            for (int i = 0; i < lines.Length; i++)
            {
                string one = Fill(lines[i]);
                if (ReferenceEquals(one, lines[i])) continue;
                filled ??= (string[])lines.Clone();
                filled[i] = one;
            }
            return filled ?? lines;
        }

        static string ValueOf(string key)
        {
            switch (key.Trim().ToLowerInvariant())
            {
                case "player":      return PlayerFullName;
                case "playerfirst": return PlayerFirstName;
                case "chief":       return CrewChiefFullName;
                case "chieffirst":  return CrewChiefFirstName;
                default:            return null;
            }
        }

        static string Ask(Func<string> provider)
        {
            if (provider == null) return "";
            string name;
            try { name = provider(); }
            catch { return ""; }   // a name is never worth throwing out of a speech bubble
            return string.IsNullOrWhiteSpace(name) ? "" : name.Trim();
        }

        static string Or(string value, string fallback) => string.IsNullOrEmpty(value) ? fallback : value;
    }
}
