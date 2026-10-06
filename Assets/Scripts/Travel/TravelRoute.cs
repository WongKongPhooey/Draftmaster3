using System.Collections.Generic;
using Draftmaster.Tracks;
using UnityEngine;

// The route drawn on the travel map: from where the car is, through the stops the player has tapped, to
// this week's race. PlayerPrefs-backed under "travel." like TravelState, so it survives the map closing, a
// walk round a landmark and a quit, and CareerReset wipes it with the rest. Only the tapped stops are
// stored; the roads between them are worked out again (TravelRoutePlan) every time it is asked, so the
// route is always the current one from wherever the car now stands.
public static class TravelRoute
{
    const string StopsKey = "travel.route.stops";

    static IEnumerable<string> Neighbors(string id) => TravelGraph.Neighbors(id);

    // The places the player has added, in driving order. The destination is never one of them.
    public static List<string> Stops()
    {
        var list = new List<string>();
        foreach (var s in PlayerPrefs.GetString(StopsKey, "").Split(','))
        {
            var id = s.Trim();
            if (id.Length > 0 && TravelGraph.Get(id) != null) list.Add(id);
        }
        return list;
    }

    static void Save(List<string> stops)
    {
        PlayerPrefs.SetString(StopsKey, string.Join(",", stops));
        PlayerPrefs.Save();
    }

    public static bool HasStop(string nodeId) => Stops().Contains(nodeId);

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(StopsKey);
        PlayerPrefs.Save();
    }

    // Every node still to be driven through, in order, ending at the destination. Empty with no destination
    // or once there; null when the stops cannot all be reached.
    public static List<string> Path()
    {
        if (!TravelState.HasDestination) return new List<string>();
        return TravelRoutePlan.Route(TravelState.CurrentNodeId, Stops(), TravelState.DestinationId, Neighbors);
    }

    // The next node along the route — the one the car drives to — or null when there is nowhere to go.
    public static string NextHop()
    {
        var path = Path();
        return path != null && path.Count > 0 ? path[0] : null;
    }

    // Stops the whole route would take with `nodeId` added at its best point, or -1 when it cannot be.
    public static int LengthWith(string nodeId)
    {
        var stops = TravelRoutePlan.InsertStop(TravelState.CurrentNodeId, Stops(), nodeId,
                                               TravelState.DestinationId, Neighbors);
        if (stops == null) return -1;
        var route = TravelRoutePlan.Route(TravelState.CurrentNodeId, stops, TravelState.DestinationId, Neighbors);
        return route?.Count ?? -1;
    }

    // Tap a place to call in there: it goes wherever it lengthens the drive least, and the route is redrawn
    // through it. False when it is already on the list, is where the car is or where it is going, or
    // cannot be reached.
    public static bool Add(string nodeId)
    {
        if (!TravelState.HasDestination || TravelGraph.Get(nodeId) == null) return false;
        if (nodeId == TravelState.CurrentNodeId || nodeId == TravelState.DestinationId) return false;
        var current = Stops();
        if (current.Contains(nodeId)) return false;
        var stops = TravelRoutePlan.InsertStop(TravelState.CurrentNodeId, current, nodeId,
                                               TravelState.DestinationId, Neighbors);
        if (stops == null) return false;
        Save(stops);
        return true;
    }

    public static bool Remove(string nodeId)
    {
        var stops = Stops();
        if (!stops.Remove(nodeId)) return false;
        Save(stops);
        return true;
    }

    // The car pulled in somewhere: a stop reached is a stop done.
    public static void OnArrived(string nodeId) => Remove(nodeId);
}
