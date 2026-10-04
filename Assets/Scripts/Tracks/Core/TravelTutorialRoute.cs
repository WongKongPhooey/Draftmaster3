using System;
using System.Collections.Generic;

namespace Draftmaster.Tracks
{
    // The demo's first road trip, Watkins Glen to Daytona, is the travel map's tutorial: on the way the player
    // has to call in at the team's HQ and at a garage, so they learn that stops are places with things in
    // them before they ever plan a leg of their own. This is the pure half — which places, and whether a
    // route can still take them all in. TravelTutorial (runtime) holds the progress and asks these.
    public static class TravelTutorialRoute
    {
        public const string From = "WatkinsGlen";
        public const string To = "Daytona";

        // Travel-graph node ids, in the order the prompt names them. Either order is allowed on the road.
        public const string TeamHQ = "team_factory";
        public const string Garage = "moonshine_garage";
        public static readonly IReadOnlyList<string> Waypoints = new[] { TeamHQ, Garage };

        // Fewest hops from `from` to `dest` passing through every one of `remaining`, in whichever order is
        // shortest. -1 when any leg is unreachable. `hops` is the graph's shortest-path count (-1 = none).
        public static int RouteHops(string from, IReadOnlyList<string> remaining, string dest,
                                    Func<string, string, int> hops)
        {
            if (remaining == null || remaining.Count == 0) return hops(from, dest);

            int best = -1;
            var rest = new List<string>(remaining.Count - 1);
            for (int i = 0; i < remaining.Count; i++)
            {
                int first = hops(from, remaining[i]);
                if (first < 0) continue;

                rest.Clear();
                for (int j = 0; j < remaining.Count; j++) if (j != i) rest.Add(remaining[j]);
                int tail = RouteHops(remaining[i], rest, dest, hops);
                if (tail < 0) continue;

                int total = first + tail;
                if (best < 0 || total < best) best = total;
            }
            return best;
        }

        // Whether stepping onto `next` still leaves a way to every remaining waypoint and then `dest` inside
        // the stops that will be left after the step. The destination itself is off limits until the
        // waypoints are done: arriving there ends the trip.
        public static bool CanStep(string next, IReadOnlyList<string> remaining, string dest, int stopsLeft,
                                   Func<string, string, int> hops)
        {
            if (stopsLeft <= 0) return false;

            var after = new List<string>(remaining ?? Array.Empty<string>());
            after.Remove(next);
            if (next == dest && after.Count > 0) return false;

            int need = RouteHops(next, after, dest, hops);
            return need >= 0 && need <= stopsLeft - 1;
        }
    }
}
