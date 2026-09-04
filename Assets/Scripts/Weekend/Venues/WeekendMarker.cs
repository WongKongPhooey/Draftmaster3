using System.Collections.Generic;
using Draftmaster.Weekend;
using UnityEngine;

// Where a booking's objective marker sits, and how close counts as being there.
//
// Make a GameObject in the track package, name it `PitBox_Marker`, and that is now the pit box as far as
// the weekend is concerned: the objective arrow points at it, and the obligation starts when the player is
// inside it. Size it and you have sized the perimeter — give it a BoxCollider2D the shape of the pit stall
// and standing anywhere in the stall counts, rather than within some radius of a point somebody computed.
//
// NAMING. Anything ending `_Marker` is picked up when the track loads. The part in front is matched against
// the venues, so `PitBox_Marker`, `Hospitality_Marker`, `Grandstand_Marker` and so on wire themselves up.
// A name that matches no venue — `Podium_Marker` — is still a marker, just one that has to be asked for by
// name from a plan file (`"markerLocation": "Podium_Marker"`).
//
// PERIMETER. Taken from the object itself, in this order: a Collider2D if it has one (any shape, and it is
// used as the actual test, so a rotated box or a polygon works), otherwise a Renderer's bounds, otherwise
// `fallbackRange` as a plain radius. So the workflow is: draw a sprite the size of the area, name it, done.
//
// TELEPORT. Some places cannot be walked to. The grandstands at a road course are across the track from the
// paddock, and the boundary stops well short of them. So `teleportTo` splits "where the marker is" from
// "where you end up": the marker goes at the paddock exit where the player can actually reach it, the
// teleport destination goes in the seat, and arriving at one puts you at the other behind a wipe.
[DisallowMultipleComponent]
public class WeekendMarker : MonoBehaviour
{
    // The suffix that makes a plain GameObject a marker without anybody adding a component.
    public const string NameSuffix = WeekendMarkerNames.Suffix;

    [Header("What this is the marker for")]
    [Tooltip("Which venue's bookings come here. Set from the object's name when it matches one; None means " +
             "this marker is only used by a plan file that asks for it by name.")]
    public WeekendVenue venue = WeekendVenue.None;

    [Header("Getting there")]
    [Tooltip("Radius used only when this object has no collider and no renderer to take a size from.")]
    public float fallbackRange = 4f;

    [Tooltip("Where the player ends up when the booking starts, for a place they cannot walk to. Empty = " +
             "they stay where they are. Put the marker where they CAN get to and this where the thing " +
             "actually happens — the grandstand seat across the track, the room inside the building.")]
    public Transform teleportTo;

    [Tooltip("Shown on the objective marker instead of the venue's usual name — 'the gate to the " +
             "grandstands' rather than 'the grandstand'.")]
    public string label = "";

    [Header("The view from there")]
    [Tooltip("Where the camera settles once the player has arrived, for somewhere they sit and watch. " +
             "Empty = the shot is worked out from the seat and the nearest piece of circuit. Author one as " +
             "a child called View / Vantage / Camera and it is picked up with the rest of the naming " +
             "convention.")]
    public Transform cameraView;

    [Tooltip("Orthographic size at the end of the pan — half the height of what is on screen, in metres. " +
             "0 = work it out from how far the seat is from the road.")]
    public float cameraZoom = 0f;

    [Tooltip("Seconds the camera takes to pull back from the player to the vantage.")]
    public float cameraPanSeconds = 2.2f;

    [Header("Editor")]
    public Color gizmoColor = new Color(1f, 0.78f, 0.25f, 0.85f);

    public static readonly List<WeekendMarker> All = new();

    Collider2D _collider;

    public Collider2D Perimeter
    {
        get
        {
            if (_collider == null) _collider = GetComponent<Collider2D>();
            return _collider;
        }
    }

    // Where the objective arrow points and the distance is measured from.
    public Vector3 MarkerPosition
    {
        get
        {
            var box = Perimeter;
            return box != null ? (Vector3)box.bounds.center : transform.position;
        }
    }

    // Where TRAVEL THERE puts the player: inside the perimeter, never the teleport target. Skipping the walk
    // must still leave them stood ON the marker, or the arrival test they are about to be measured against
    // fails the moment the shortcut succeeds.
    public Vector3 StandPosition => MarkerPosition;

    // Where the gate sends them once they are here, for a place that cannot be walked to at all.
    public Vector3 TeleportPosition => teleportTo != null ? teleportTo.position : MarkerPosition;

    public bool HasTeleport => teleportTo != null;

    // Where the camera pulls back to, and how wide, once the player is sat there. Null/0 means nobody has
    // authored one and the runtime works it out from the circuit (GrandstandWatch.Vantage).
    public bool HasCameraView => cameraView != null;

    public Vector3 CameraViewPosition => cameraView != null ? cameraView.position : TeleportPosition;

    public string Label => string.IsNullOrEmpty(label) ? WeekendVenues.Label(venue) : label;

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    // Is the player inside this marker? The collider IS the test when there is one, so an L-shaped polygon
    // round the back of the hauler works exactly as drawn.
    public bool Contains(Vector2 worldPoint)
    {
        var box = Perimeter;
        if (box != null) return box.OverlapPoint(worldPoint);

        var renderer = GetComponent<Renderer>();
        if (renderer != null) return renderer.bounds.Contains(new Vector3(worldPoint.x, worldPoint.y, renderer.bounds.center.z));

        return Vector2.Distance(worldPoint, transform.position) <= fallbackRange;
    }

    // The radius that best describes this marker, for anything that wants one number — the objective HUD's
    // "12 m away" readout and the anchor's fallback test.
    public float Range
    {
        get
        {
            var box = Perimeter;
            if (box != null) return Mathf.Max(box.bounds.extents.x, box.bounds.extents.y);

            var renderer = GetComponent<Renderer>();
            if (renderer != null) return Mathf.Max(renderer.bounds.extents.x, renderer.bounds.extents.y);

            return fallbackRange;
        }
    }

    // ------------------------------------------------------------------ the naming convention

    // The rules themselves are in WeekendMarkerNames, in the core assembly, so they are testable without a
    // scene. This half is just the part that needs a live hierarchy.

    public static WeekendVenue VenueFromName(string objectName) => WeekendMarkerNames.VenueFromName(objectName);

    public static bool IsMarkerName(string objectName) => WeekendMarkerNames.IsMarkerName(objectName);

    public static string DefaultNameFor(WeekendVenue venue) => WeekendMarkerNames.DefaultNameFor(venue);

    static string Simplify(string text) => WeekendMarkerNames.Simplify(text);

    // Give every `*_Marker` object in a loaded track its component. Called before any venue is generated, so
    // naming an object is genuinely the whole workflow. Objects that already carry the component are left
    // exactly as authored — the convention is a shortcut, never something that overwrites an inspector.
    public static int AdoptNamedObjects(Transform root)
    {
        if (root == null) return 0;

        int adopted = 0;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!IsMarkerName(t.name)) continue;
            if (t.GetComponent<WeekendMarker>() != null) continue;

            var marker = t.gameObject.AddComponent<WeekendMarker>();
            marker.venue = VenueFromName(t.name);

            // A child called Seat / Destination / Inside is the teleport target, so the split between "walk
            // here" and "end up there" can be authored without touching the inspector either. A child called
            // View / Vantage / Camera is where the camera settles once they are there.
            foreach (Transform child in t)
            {
                string name = Simplify(child.name);
                if (marker.teleportTo == null &&
                    (name == "seat" || name == "destination" || name == "inside" || name == "teleport"))
                {
                    marker.teleportTo = child;
                    continue;
                }
                if (marker.cameraView == null && (name == "view" || name == "vantage" || name == "camera"))
                    marker.cameraView = child;
            }

            adopted++;
        }

        if (adopted > 0) Debug.Log($"WeekendMarker: adopted {adopted} object(s) named '*{NameSuffix}'.");
        return adopted;
    }

    // ------------------------------------------------------------------ lookup

    // The marker a booking should use: the object it names, else any marker for that venue.
    public static WeekendMarker Find(WeekendVenue venue, string markerLocation = "")
    {
        if (!string.IsNullOrWhiteSpace(markerLocation))
        {
            string want = Simplify(markerLocation);
            foreach (var marker in All)
                if (marker != null && Simplify(marker.name) == want) return marker;
        }

        foreach (var marker in All)
            if (marker != null && marker.venue == venue) return marker;

        return null;
    }

    public static bool Any(WeekendVenue venue) => Find(venue) != null;

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;

        var box = Perimeter;
        if (box != null)
        {
            var b = box.bounds;
            Gizmos.DrawWireCube(b.center, new Vector3(b.size.x, b.size.y, 0.1f));
        }
        else Gizmos.DrawWireSphere(MarkerPosition, Range);

        if (teleportTo != null)
        {
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.5f);
            Gizmos.DrawLine(MarkerPosition, teleportTo.position);
            Gizmos.DrawWireSphere(teleportTo.position, 1f);
        }

        // The shot, drawn as what will actually be on screen: the camera settles in the middle of this box
        // and the box is what the player sees, so a vantage that misses the circuit is visible as one.
        if (cameraView != null)
        {
            Gizmos.color = new Color(0.35f, 0.85f, 1f, 0.8f);
            Gizmos.DrawLine(TeleportPosition, cameraView.position);
            float half = cameraZoom > 0f ? cameraZoom : 20f;
            Gizmos.DrawWireCube(cameraView.position, new Vector3(half * 2f * 16f / 9f, half * 2f, 0.1f));
        }

        string caption = venue == WeekendVenue.None ? name : venue + " — " + name;
        if (teleportTo != null) caption += " (teleports)";
        UnityEditor.Handles.color = gizmoColor;
        UnityEditor.Handles.Label(MarkerPosition + new Vector3(0.7f, 0.7f, 0f), caption);
    }

    // The rule from the boundary work: a marker outside the walkable paddock is one the player is clamped
    // away from. The TELEPORT target is exempt — being unreachable is the whole reason it exists.
    public bool IsReachable(out float outsideBy)
    {
        outsideBy = 0f;
        if (!PaddockBoundary.AnyActive) return true;

        Vector2 at = MarkerPosition;
        Vector2 inside = PaddockBoundary.Constrain(at);
        outsideBy = Vector2.Distance(inside, at);
        return outsideBy < 0.5f;
    }
#endif
}
