using System.Collections.Generic;

namespace Draftmaster.Weekend
{
    // What the player knows about the other drivers, and how they come to know it.
    //
    // Every driver on the RV laptop has a row of stats (DriverAttributeSheet: ability, potential, track-type
    // aptitudes, standing, racecraft). A rival's are hidden until scouted, and a session uncovers a few of them
    // for every driver in it:
    //
    //   Your own series, from the car     practice 1, qualifying 1, race 2 per driver — 4 a weekend, so a
    //                                     field you race against every week is fully known by the 5th.
    //   Another series, from the wall     1 per driver per session watched (their practice or qualifying,
    //                                     both optional, or their race) — slower, because you are guessing
    //                                     from the grandstand rather than reading them door to door.
    //
    // What a session uncovers first is what it would actually show you: qualifying shows who can do one lap,
    // the race shows racecraft, practice shows who suits this kind of track. After that, a fixed per-driver
    // order, so the same driver never reveals the same thing twice and two saves agree on a driver's order.
    //
    // Pure: a driver's knowledge is a bitmask over the sheet's labels. ScoutingLedger (runtime) keeps the masks
    // in the save and asks this which bits to set.
    public static class DriverScouting
    {
        public const int OwnPracticeReveals = 1;
        public const int OwnQualifyingReveals = 1;
        public const int OwnRaceReveals = 2;
        public const int WatchReveals = 1;

        // How many stats one session uncovers per driver in it. 0 for anything that is not a session.
        public static int RevealsFor(ActivityKind kind) => kind switch
        {
            ActivityKind.Practice => OwnPracticeReveals,
            ActivityKind.Qualifying => OwnQualifyingReveals,
            ActivityKind.Race => OwnRaceReveals,
            ActivityKind.SpectatePractice => WatchReveals,
            ActivityKind.SpectateQualifying => WatchReveals,
            ActivityKind.SpectateRace => WatchReveals,
            _ => 0,
        };

        // Labels a session shows first, in order. `trackAptitude` is the sheet label for the kind of track
        // the weekend is at ("SHORT TRACKS", "ROAD COURSES"...), or null.
        public static List<string> Preferred(ActivityKind kind, string trackAptitude)
        {
            var list = new List<string>();
            switch (kind)
            {
                case ActivityKind.Qualifying:
                case ActivityKind.SpectateQualifying:
                    list.Add("QUALIFYING");
                    if (trackAptitude != null) list.Add(trackAptitude);
                    break;
                case ActivityKind.Race:
                case ActivityKind.SpectateRace:
                    list.AddRange(new[] { "AGGRESSION", "AWARENESS", "CONSISTENCY", "TYRE MGMT", "FUEL MGMT" });
                    break;
                default:   // practice, watched or driven
                    if (trackAptitude != null) list.Add(trackAptitude);
                    list.AddRange(new[] { "CONSISTENCY", "ADAPTABILITY", "TYRE MGMT" });
                    break;
            }
            return list;
        }

        // `mask` with up to `count` more of `labels` uncovered. `seed` fixes the driver's order once the
        // session's preferred labels are all known — use something stable per driver (a hash of the name).
        public static int Reveal(int mask, IReadOnlyList<string> labels, ActivityKind kind, string trackAptitude,
                                 int count, int seed)
        {
            if (labels == null || count <= 0) return mask;

            foreach (var label in Preferred(kind, trackAptitude))
            {
                if (count <= 0) return mask;
                int i = IndexOf(labels, label);
                if (i < 0 || Known(mask, i)) continue;
                mask |= 1 << i;
                count--;
            }

            foreach (int i in Order(labels.Count, seed))
            {
                if (count <= 0) break;
                if (Known(mask, i)) continue;
                mask |= 1 << i;
                count--;
            }
            return mask;
        }

        public static bool Known(int mask, int index) => index >= 0 && index < 31 && (mask & (1 << index)) != 0;

        public static int KnownCount(int mask, int total)
        {
            int n = 0;
            for (int i = 0; i < total && i < 31; i++) if (Known(mask, i)) n++;
            return n;
        }

        public static bool AllKnown(int mask, int total) => KnownCount(mask, total) >= total;

        // Stable 32-bit hash of a driver's name (string.GetHashCode is not stable across runs).
        public static int Seed(string name)
        {
            unchecked
            {
                int h = (int)2166136261;
                foreach (char c in name ?? "") h = (h ^ char.ToUpperInvariant(c)) * 16777619;
                return h;
            }
        }

        // A fixed shuffle of 0..n-1 for this seed.
        static List<int> Order(int n, int seed)
        {
            var order = new List<int>(n);
            for (int i = 0; i < n; i++) order.Add(i);
            uint state = (uint)seed | 1u;
            for (int i = n - 1; i > 0; i--)
            {
                state ^= state << 13; state ^= state >> 17; state ^= state << 5;
                int j = (int)(state % (uint)(i + 1));
                (order[i], order[j]) = (order[j], order[i]);
            }
            return order;
        }

        static int IndexOf(IReadOnlyList<string> labels, string label)
        {
            for (int i = 0; i < labels.Count; i++) if (labels[i] == label) return i;
            return -1;
        }
    }
}
