using System.Collections.Generic;
using UnityEngine;

// Where the player is on the road map, where they're headed, and how many stops they have left.
// PlayerPrefs-backed like the rest of the career state. A "week" ticks every time a destination is
// chosen — it seeds junkyard stock, so shelves are stable for a whole leg and reroll between races.
public static class TravelState
{
    const string NodeKey = "travel.node";
    const string DestKey = "travel.dest";
    const string StopsKey = "travel.stops";
    const string WeekKey = "travel.week";
    const string VisitedKey = "travel.visited";
    const string BoughtKeyPrefix = "travel.bought."; // + week.locationId.partId -> junkyard item taken

    public static string CurrentNodeId
    {
        get
        {
            var id = PlayerPrefs.GetString(NodeKey, "WatkinsGlen");
            return TravelGraph.Get(id) != null ? id : "WatkinsGlen";
        }
        set { PlayerPrefs.SetString(NodeKey, value); PlayerPrefs.Save(); }
    }

    public static string DestinationId
    {
        get => PlayerPrefs.GetString(DestKey, "");
        set { PlayerPrefs.SetString(DestKey, value ?? ""); PlayerPrefs.Save(); }
    }

    public static int StopsLeft
    {
        get => PlayerPrefs.GetInt(StopsKey, 0);
        set { PlayerPrefs.SetInt(StopsKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }

    public static int Week
    {
        get => PlayerPrefs.GetInt(WeekKey, 1);
        set { PlayerPrefs.SetInt(WeekKey, value); PlayerPrefs.Save(); }
    }

    public static bool HasDestination => !string.IsNullOrEmpty(DestinationId);

    // Choose the next race venue: budget = direct route + a small detour allowance. New week -> junkyards reroll.
    public static bool ChooseDestination(string circuitId)
    {
        var node = TravelGraph.Get(circuitId);
        if (node == null || !node.isCircuit) return false;
        int direct = TravelGraph.ShortestHops(CurrentNodeId, circuitId);
        if (direct < 0) return false;
        DestinationId = circuitId;
        StopsLeft = direct + TravelGraph.DetourAllowance;
        Week = Week + 1;
        return true;
    }

    // Drive to an adjacent node, spending one stop. Marks it visited (that's the discovery mechanic) and
    // feeds the quest ledger so locations are immediately quest-able via StatThreshold objectives.
    public static bool MoveTo(string nodeId)
    {
        if (StopsLeft <= 0 || !TravelGraph.AreAdjacent(CurrentNodeId, nodeId)) return false;
        CurrentNodeId = nodeId;
        StopsLeft = StopsLeft - 1;
        bool firstVisit = MarkVisited(nodeId);
        PlayerStatsLedger.Increment("travelstops");
        if (firstVisit)
        {
            var n = TravelGraph.Get(nodeId);
            if (n != null && !n.isCircuit)
            {
                PlayerStatsLedger.Increment("locations");
                PlayerStatsLedger.Increment("visit." + nodeId);
            }
        }
        return true;
    }

    // Race done at the destination: it becomes home base until the next leg is planned.
    public static void ArriveAndClearDestination()
    {
        if (HasDestination) CurrentNodeId = DestinationId;
        DestinationId = "";
        StopsLeft = 0;
    }

    public static bool IsVisited(string nodeId)
    {
        var raw = PlayerPrefs.GetString(VisitedKey, "");
        foreach (var s in raw.Split(',')) if (s.Trim() == nodeId) return true;
        return false;
    }

    static bool MarkVisited(string nodeId)
    {
        if (IsVisited(nodeId)) return false;
        var raw = PlayerPrefs.GetString(VisitedKey, "");
        PlayerPrefs.SetString(VisitedKey, string.IsNullOrEmpty(raw) ? nodeId : raw + "," + nodeId);
        PlayerPrefs.Save();
        return true;
    }

    // Junkyard shelves empty as you buy: one of each rolled item per week per yard.
    public static bool WasBought(string locationId, string partId) =>
        PlayerPrefs.GetInt(BoughtKeyPrefix + Week + "." + locationId + "." + partId, 0) == 1;

    public static void MarkBought(string locationId, string partId)
    {
        PlayerPrefs.SetInt(BoughtKeyPrefix + Week + "." + locationId + "." + partId, 1);
        PlayerPrefs.Save();
    }

    public static List<TravelNode> AdjacentNodes()
    {
        var list = new List<TravelNode>();
        foreach (var id in TravelGraph.Neighbors(CurrentNodeId))
        {
            var n = TravelGraph.Get(id);
            if (n != null) list.Add(n);
        }
        return list;
    }
}
