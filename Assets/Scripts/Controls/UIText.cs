using System.Collections.Generic;
using System.Text;

namespace Draftmaster.Controls
{
    // The game's one rule for letter case on screen (Docs/Type-Scale.md, "Letter case"):
    //
    //   Labels, headings, buttons, tabs, stat names and names of things are Title Case — "Driver Stats",
    //   "End Session", "Short Tracks".
    //   Sentences — notices, notes, dialogue, hints — are sentence case — "Out for payback. Watch your mirrors."
    //   Nothing is set in all caps except an acronym ("DNF", "XP", "NASCAR").
    //
    // Text written in the source is written that way. TitleCase is for text that arrives as a key or an
    // enum name — a stat's sheet key, a Standing — and has to be shown.
    public static class UIText
    {
        // Kept in capitals wherever they turn up.
        static readonly HashSet<string> Acronyms = new()
        {
            "AI", "ARCA", "CB", "DNF", "DNQ", "DQ", "FPS", "HQ", "ID", "MPH", "KPH", "NASCAR", "N/A", "OK",
            "PR", "RV", "SC", "TV", "UK", "US", "USA", "XP",
        };

        // Lower case inside a title, capital only when they open it.
        static readonly HashSet<string> SmallWords = new()
        {
            "a", "an", "and", "as", "at", "by", "for", "in", "of", "on", "or", "the", "to", "vs", "with",
        };

        // "TOP 10S" -> "Top 10s", "MINI-MAP" -> "Mini-Map", "OUT OF THE RACE" -> "Out of the Race". An acronym
        // stays in capitals; anything already in mixed case is left as it was written.
        public static string TitleCase(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var sb = new StringBuilder(text.Length);
            var words = text.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(Word(words[i], first: i == 0));
            }
            return sb.ToString();
        }

        static string Word(string word, bool first)
        {
            if (word.Length == 0) return word;
            if (!IsAllCaps(word)) return word;

            string bare = word.Trim('(', ')', '"', '\'', ',', '.', ':', ';', '!', '?');
            if (Acronyms.Contains(bare)) return word;

            var lower = word.ToLowerInvariant();
            if (!first && SmallWords.Contains(bare.ToLowerInvariant())) return lower;

            // Capital at the start of each hyphen-joined part, and only on a letter that starts a run of
            // letters — the "s" of "10S" stays small.
            var chars = lower.ToCharArray();
            bool startOfPart = true;
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (c == '-' || c == '/') { startOfPart = true; continue; }
                if (char.IsLetter(c))
                {
                    if (startOfPart) chars[i] = char.ToUpperInvariant(c);
                    startOfPart = false;
                }
                else if (char.IsDigit(c)) startOfPart = false;
            }
            return new string(chars);
        }

        // Has letters, and none of them lower case.
        static bool IsAllCaps(string word)
        {
            bool letters = false;
            foreach (char c in word)
            {
                if (!char.IsLetter(c)) continue;
                letters = true;
                if (char.IsLower(c)) return false;
            }
            return letters;
        }
    }
}
