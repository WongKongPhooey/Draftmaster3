using System.Collections.Generic;
using Draftmaster.Tracks;
using UnityEngine;

// Progress through the travel map's tutorial leg (Watkins Glen -> Daytona via Team HQ and the garage — see
// TravelTutorialRoute for the rules). PlayerPrefs-backed under "travel." like the rest of TravelState, so a
// mid-trip quit, a walk round a landmark or CareerReset all do the right thing.
//
// While the leg is on, the map only offers Daytona as the destination, budgets enough stops for the
// detour, refuses any hop that would leave a waypoint out of reach, and holds START RACE WEEKEND back until
// both have been visited.
public static class TravelTutorial
{
    const string DoneKey = "travel.tutorial.done";
    const string LegKey = "travel.tutorial.leg";            // destination booked under the tutorial
    const string HitKeyPrefix = "travel.tutorial.hit.";     // + waypoint id -> called in this leg

    public const string PromptId = "travel.tutorial";

    public static bool Done => PlayerPrefs.GetInt(DoneKey, 0) == 1;
    public static bool LegBooked => PlayerPrefs.GetInt(LegKey, 0) == 1;

    // The tutorial is in charge of the map: either the trip is booked under it, or the player is parked at
    // Watkins Glen with the next leg still to plan.
    public static bool Active =>
        !Done && (LegBooked || (!TravelState.HasDestination && TravelState.CurrentNodeId == TravelTutorialRoute.From));

    public static List<string> Remaining()
    {
        var left = new List<string>();
        foreach (var id in TravelTutorialRoute.Waypoints)
            if (PlayerPrefs.GetInt(HitKeyPrefix + id, 0) == 0) left.Add(id);
        return left;
    }

    static int Hops(string a, string b) => TravelGraph.ShortestHops(a, b);

    // Choosing: only the tutorial's destination is on offer.
    public static bool CanChoose(string circuitId) =>
        !Active || LegBooked || circuitId == TravelTutorialRoute.To;

    // Stops for the leg: the shortest run through both waypoints, plus the usual detour allowance.
    public static int BudgetFrom(string fromId) =>
        TravelTutorialRoute.RouteHops(fromId, Remaining(), TravelTutorialRoute.To, Hops) + TravelGraph.DetourAllowance;

    public static void BookLeg()
    {
        PlayerPrefs.SetInt(LegKey, 1);
        foreach (var id in TravelTutorialRoute.Waypoints) PlayerPrefs.DeleteKey(HitKeyPrefix + id);
        PlayerPrefs.Save();
    }

    public static bool CanMoveTo(string nodeId) =>
        !Active || !LegBooked ||
        TravelTutorialRoute.CanStep(nodeId, Remaining(), TravelTutorialRoute.To, TravelState.StopsLeft, Hops);

    public static void OnArrived(string nodeId)
    {
        if (!Active || !LegBooked) return;
        foreach (var id in TravelTutorialRoute.Waypoints)
            if (id == nodeId) { PlayerPrefs.SetInt(HitKeyPrefix + id, 1); PlayerPrefs.Save(); }
    }

    public static bool CanStartWeekend => !Active || !LegBooked || Remaining().Count == 0;

    public static void Complete()
    {
        PlayerPrefs.SetInt(DoneKey, 1);
        PlayerPrefs.DeleteKey(LegKey);
        foreach (var id in TravelTutorialRoute.Waypoints) PlayerPrefs.DeleteKey(HitKeyPrefix + id);
        PlayerPrefs.Save();
    }

    static string NameOf(string id) => TravelGraph.Get(id)?.name ?? id;

    // What a waypoint is called on the map while the tutorial is pointing at it. The garage has never been
    // visited, so the map would otherwise draw it as a "?" the player is meant to drive to.
    public static string WaypointLabel(string nodeId)
    {
        if (nodeId == TravelTutorialRoute.TeamHQ) return "TEAM HQ";
        if (nodeId == TravelTutorialRoute.Garage) return "GARAGE - " + NameOf(nodeId).ToUpperInvariant();
        return null;
    }

    public static bool IsPendingWaypoint(string nodeId) => Active && Remaining().Contains(nodeId);

    // The prompt's one line, in the same strip as "Hold to run". Null once there is nothing to tell.
    public static string PromptText()
    {
        if (!Active) return null;
        string to = NameOf(TravelTutorialRoute.To);
        if (!LegBooked) return $"Pick {to} - on the way, stop at Team HQ and the Garage";

        var left = Remaining();
        if (left.Count == 2) return $"On the way to {to}, stop at Team HQ and the Garage";
        if (left.Count == 1)
            return left[0] == TravelTutorialRoute.TeamHQ
                ? $"Garage done - now Team HQ, then {to}"
                : $"Team HQ done - now the Garage, then {to}";
        return TravelState.CurrentNodeId == TravelTutorialRoute.To
            ? "Made it - START RACE WEEKEND"
            : $"Both stops made - head for {to}";
    }
}
