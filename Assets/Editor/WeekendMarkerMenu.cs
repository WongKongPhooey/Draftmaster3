using Draftmaster.Weekend;
using UnityEditor;
using UnityEngine;

// Making the markers. Creating one from the menu lands it already named, already typed, already sized and
// already parented into the open track package — which is the difference between "make an object and name
// it" being a workflow and being a thing you have to remember the spelling of.
public static class WeekendMarkerMenu
{
    [MenuItem("GameObject/Draftmaster/Weekend Marker", false, 12)]
    public static void CreateMarker(MenuCommand cmd)
    {
        var go = new GameObject(WeekendVenue.PitBox + WeekendMarker.NameSuffix);
        var marker = go.AddComponent<WeekendMarker>();
        marker.venue = WeekendVenue.PitBox;

        // A box to start from, because the size IS the perimeter — an unsized marker would be a point, and
        // the first thing anybody would have to do is add the collider by hand.
        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(6f, 6f);

        var parent = (cmd?.context as GameObject) ?? OpenTrackPackage();
        if (parent != null) GameObjectUtility.SetParentAndAlign(go, parent);

        var view = SceneView.lastActiveSceneView;
        Vector3 at = view != null ? view.pivot : Vector3.zero;
        go.transform.position = new Vector3(at.x, at.y, 0f);

        Undo.RegisterCreatedObjectUndo(go, "Create Weekend Marker");
        Selection.activeObject = go;

        Debug.Log("WeekendMarker: created. Rename it for the venue you want (PitBox_Marker, " +
                  "Hospitality_Marker, Signing_Marker, Stage_Marker, DriversRoom_Marker, Grandstand_Marker), " +
                  "drag the collider out to the size of the area, and that venue stops being guessed at " +
                  "runtime. For somewhere unreachable, add a child called 'Seat' where the player should " +
                  "end up and this becomes a gate that teleports them there.");
    }

    static GameObject OpenTrackPackage()
    {
        var package = Object.FindFirstObjectByType<TrackPackage>();
        if (package == null) return null;

        var paddock = package.paddockRoot != null ? package.paddockRoot : package.transform;
        return paddock.gameObject;
    }

    // Everything wrong with the markers in the open scene: one outside the walkable paddock with no teleport
    // to excuse it, two sharing a name so a plan file's markerLocation would be ambiguous, one with no size,
    // and every venue still being guessed at runtime.
    [MenuItem("Draftmaster/Weekend/Check Markers In Open Scene", priority = 23)]
    static void CheckMarkers()
    {
        var markers = Object.FindObjectsByType<WeekendMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (markers.Length == 0)
        {
            Debug.LogWarning("WeekendMarker: none in this scene. Every venue will be worked out from the pit " +
                             "lane at runtime, which is what puts markers on the fence line.");
            return;
        }

        var seen = new System.Collections.Generic.Dictionary<string, WeekendMarker>();
        int problems = 0;

        foreach (var marker in markers)
        {
            // A marker outside the paddock is only a fault if the player is expected to WALK to it. One that
            // teleports is meant to be the door, and the door is allowed to be at the edge.
            if (!marker.IsReachable(out float outsideBy) && !marker.HasTeleport)
            {
                problems++;
                Debug.LogError($"'{marker.name}' is {outsideBy:0.0} m outside the paddock boundary and has no " +
                               "teleport target — the player is clamped away from it and the booking can " +
                               "never start. Either move it inside, or give it a child called 'Seat' where " +
                               "the player should end up.", marker);
            }

            if (marker.Perimeter == null && marker.GetComponent<Renderer>() == null)
                Debug.Log($"'{marker.name}' has no collider and no renderer, so its perimeter is the " +
                          $"{marker.fallbackRange:0.0} m fallback radius. Add a collider to size it properly.", marker);

            string key = marker.name.Trim().ToLowerInvariant();
            if (seen.TryGetValue(key, out var other))
            {
                problems++;
                Debug.LogError($"Two markers are both called '{marker.name}' — a plan file naming that " +
                               "markerLocation would get whichever the scene happened to list first.", marker);
            }
            else seen[key] = marker;
        }

        foreach (WeekendVenue venue in System.Enum.GetValues(typeof(WeekendVenue)))
        {
            if (venue == WeekendVenue.None) continue;
            if (WeekendMarker.Any(venue)) continue;
            Debug.Log($"No marker for {venue} — it will be generated from the paddock rectangle at runtime. " +
                      $"Fine, unless that is the one landing in the wrong place. Name an object " +
                      $"'{WeekendMarker.DefaultNameFor(venue)}' to take it over.");
        }

        Debug.Log($"WeekendMarker: checked {markers.Length} marker(s), {problems} problem(s).");
    }
}
