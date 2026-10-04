using System;
using System.Collections.Generic;

namespace Draftmaster.Tracks
{
    // The demo's season: three rounds, in order. NEXT WEEKEND walks it so finishing a race moves the career
    // on to the next venue instead of reloading the one just raced.
    public static class DemoCalendar
    {
        public static readonly IReadOnlyList<string> Rounds = new[] { "WatkinsGlen", "Daytona", "Martinsville" };

        public static string Opener => Rounds[0];

        // 0-based round of a track id, or -1 when the track is not on the demo calendar.
        public static int RoundOf(string trackId)
        {
            for (int i = 0; i < Rounds.Count; i++)
                if (string.Equals(Rounds[i], trackId, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        // The venue after this one. Off-calendar tracks (a travel-map detour, a single race) rejoin at the
        // opener; the last round wraps back round to it so the demo never strands the player.
        public static string After(string trackId)
        {
            int round = RoundOf(trackId);
            return Rounds[(round + 1) % Rounds.Count];
        }
    }
}
