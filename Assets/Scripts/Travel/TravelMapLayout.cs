using System.Collections.Generic;
using UnityEngine;

// Hand placement for the travel map, and nothing else.
//
// TravelGraph puts every venue where its real latitude and longitude land on the board. That is right
// about the country and wrong about the odd label that collides or the odd track that wants nudging, so
// the map is also draggable: open Resources/UI/TravelMap.prefab in Prefab Mode, move the Node_* markers,
// then run Draftmaster > Travel Map > Save And Rebuild Markers And Routes. That rewrites the tables
// below, and anything in them overrides the code for that id.
//
// Two different things are saved, because a circuit and a shop are not the same kind of thing. A CIRCUIT
// has a position, so dragging it saves where it was put. A SHOP or YARD is mounted on a road — it sits at
// the middle of one and splits it into two hops — so dragging one saves the ROAD it was dropped nearest,
// and its position goes on being the middle of whatever road that is. Drop it near a highway and it hops
// onto it.
//
// GENERATED FILE from that point on: the next save rewrites everything between the markers, so put
// comments outside them. Empty means nothing has been dragged and the map is pure geography.
public static class TravelMapLayout
{
    // id -> normalized board position. x 0 = west .. 1 = east, y 0 = north .. 1 = south (GUI y-down).
    static readonly Dictionary<string, Vector2> Placed = new()
    {
        // BEGIN PLACED
        { "AllAmerican", new Vector2(0.0900f, 0.4055f) },   // All American
        { "Daytona", new Vector2(0.7607f, 0.7393f) },   // Daytona
        // END PLACED
    };

    // id -> the road it is mounted on, as the two circuits it splits. Only the ones moved onto a road
    // other than the one the code mounts them on are listed.
    static readonly Dictionary<string, (string a, string b)> Mounts = new()
    {
        // BEGIN MOUNTS
        // END MOUNTS
    };

    public static int Count => Placed.Count;
    public static int MountCount => Mounts.Count;

    public static bool TryMount(string id, out string a, out string b)
    {
        if (!string.IsNullOrEmpty(id) && Mounts.TryGetValue(id, out var road)) { a = road.a; b = road.b; return true; }
        a = b = null;
        return false;
    }

    public static bool TryGet(string id, out Vector2 pos)
    {
        if (!string.IsNullOrEmpty(id) && Placed.TryGetValue(id, out pos)) return true;
        pos = default;
        return false;
    }
}
