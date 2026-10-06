using System;
using System.Collections.Generic;

namespace Draftmaster.Tracks
{
    // The travel map's planned route: from where the car is, through the stops the player has tapped, to
    // this week's race. Pure — the road network comes in as a neighbour function — so the rules are tested
    // without the map. TravelRoute (runtime) keeps the stop list and asks these.
    //
    // A route is the shortest road between each pair of consecutive points. The destination is never passed
    // through on the way to a stop: arriving there ends the trip, so a leg that would cross it detours round.
    public static class TravelRoutePlan
    {
        // Shortest road from `from` to `to` as the list of nodes after `from`, ending with `to` (empty when
        // they are the same place). Null when there is no way through. `avoid` is never stepped on unless it
        // is `to` itself. Neighbours are taken in the order given, so the same map always draws the same road.
        public static List<string> Path(string from, string to, Func<string, IEnumerable<string>> neighbors,
                                        string avoid = null)
        {
            if (from == to) return new List<string>();

            var cameFrom = new Dictionary<string, string> { [from] = null };
            var q = new Queue<string>();
            q.Enqueue(from);
            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                foreach (var nb in neighbors(cur) ?? Array.Empty<string>())
                {
                    if (cameFrom.ContainsKey(nb)) continue;
                    if (nb == avoid && nb != to) continue;
                    cameFrom[nb] = cur;
                    if (nb == to) return Unwind(cameFrom, from, to);
                    q.Enqueue(nb);
                }
            }
            return null;
        }

        static List<string> Unwind(Dictionary<string, string> cameFrom, string from, string to)
        {
            var path = new List<string>();
            for (var at = to; at != from; at = cameFrom[at]) path.Add(at);
            path.Reverse();
            return path;
        }

        // The whole drive: every node after `from`, through each of `stops` in order, ending at `dest`.
        // Null when any leg cannot be driven.
        public static List<string> Route(string from, IReadOnlyList<string> stops, string dest,
                                         Func<string, IEnumerable<string>> neighbors)
        {
            var route = new List<string>();
            string at = from;
            if (stops != null)
                foreach (var stop in stops)
                {
                    var leg = Path(at, stop, neighbors, avoid: dest);
                    if (leg == null) return null;
                    route.AddRange(leg);
                    at = stop;
                }
            var last = Path(at, dest, neighbors);
            if (last == null) return null;
            route.AddRange(last);
            return route;
        }

        // Where a newly tapped stop goes in the list: whichever slot makes the whole drive shortest, the
        // earliest slot on a tie. Returns the new list, or null when no slot gives a drivable route.
        public static List<string> InsertStop(string from, IReadOnlyList<string> stops, string stop, string dest,
                                              Func<string, IEnumerable<string>> neighbors)
        {
            var current = stops ?? Array.Empty<string>();
            List<string> best = null;
            int bestLength = int.MaxValue;
            for (int slot = 0; slot <= current.Count; slot++)
            {
                var candidate = new List<string>(current);
                candidate.Insert(slot, stop);
                var route = Route(from, candidate, dest, neighbors);
                if (route == null || route.Count >= bestLength) continue;
                best = candidate;
                bestLength = route.Count;
            }
            return best;
        }
    }
}
