using System.Collections.Generic;
using Draftmaster.Weekend;
using UnityEngine;

// A place in the paddock where a booking happens.
//
// The timetable says when; this says where. One of these sits at the pit box, at the player's motorhome
// door, at the drivers' room, at the fan fence, at the hospitality tent, at the intro stage and in front of
// each grandstand — some found on things the scene already has (the pit box marker, the player's RV), the
// rest built by WeekendVenueSites out of the paddock rectangle.
//
// The anchor is the thing the objective marker points at and the thing the player has to be stood near
// before an obligation will start. It carries no behaviour beyond being findable and being reachable.
//
// Reachable is a hard rule, enforced here rather than trusted to whoever placed it: an anchor outside the
// PaddockBoundary is an arrow pointing through a fence at somewhere the player is clamped out of, and the
// obligation behind it can never be attended. Whatever position it is given, it is pulled back to the
// nearest point inside the walkable area — on spawn, and again whenever the boundary itself changes.
public class WeekendVenueAnchor : MonoBehaviour
{
    public static readonly List<WeekendVenueAnchor> All = new();

    [Tooltip("Which kind of booking is kept here.")]
    public WeekendVenue venue = WeekendVenue.PitBox;

    [Tooltip("How close the player has to be for the obligation to count as attended, metres. Generous: " +
             "being told to walk somewhere and then missing it by a metre is not a puzzle worth having.")]
    public float arriveRange = 3.5f;

    [Tooltip("Where the player ends up when they take the marker's TRAVEL THERE option. Empty = this " +
             "object's own position, stepped back a little so they are not stood inside the furniture.")]
    public Transform standPoint;

    [Tooltip("Shown on the objective marker instead of the venue's usual name. For a grandstand that is " +
             "worth naming — 'the frontstretch stand' rather than 'the grandstand'.")]
    public string label = "";

    [Tooltip("Name of the WeekendMarker object this was authored from, so a plan file's " +
             "\"markerLocation\": \"Podium_Marker\" can send one booking to this exact spot while the " +
             "rest of its kind go to the ordinary venue.")]
    public string markerLocation = "";

    [Tooltip("The authored marker, when there is one. It owns the perimeter (its collider IS the arrival " +
             "test) and the teleport destination.")]
    public WeekendMarker marker;

    public string Label => string.IsNullOrEmpty(label) ? WeekendVenues.Label(venue) : label;

    public Vector3 StandPosition => standPoint != null ? standPoint.position : transform.position;

    // Where the player ends up once the booking actually starts. Normally the standing mark they walked to;
    // for a place that cannot be walked to, the marker's teleport target on the other side of the fence.
    public Vector3 ArrivalPosition => marker != null && marker.HasTeleport ? marker.TeleportPosition : StandPosition;

    // Does starting this booking move the player somewhere they could not have walked?
    public bool TeleportsOnArrival => marker != null && marker.HasTeleport;

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
        PaddockBoundary.Changed += ConstrainToPaddock;
        ConstrainToPaddock();
    }

    void OnDisable()
    {
        All.Remove(this);
        PaddockBoundary.Changed -= ConstrainToPaddock;
    }

    // Anchors built in code get their position before this component is added but their stand point after,
    // and the paddock's own boundaries are generated a frame or two into the scene — so the rule is applied
    // again once the scene has finished assembling itself.
    void Start() { ConstrainToPaddock(); }

    // The rule: this marker stands inside the walkable paddock, wherever it was asked to stand. A venue out
    // beyond the fence — a grandstand across the track, a tent placed off the paddock rectangle rather than
    // the authored polygon — is pulled to the nearest point the player can actually walk to, so the marker
    // is at worst the way out towards it instead of unreachable.
    //
    // No boundary in the scene means no walkable area to speak of, and nothing to enforce.
    public void ConstrainToPaddock()
    {
        if (this == null || !PaddockBoundary.AnyActive) return;

        // An authored marker is where somebody put it, full stop. Pulling it about would be the tooling
        // overruling the author, and the editor already reports one that is outside the boundary
        // (Draftmaster > Weekend > Check Markers In Open Scene) so it is a fault to fix rather than to hide.
        if (marker != null) return;

        Vector3 wanted = transform.position;
        Vector2 inside = Inside(wanted);
        if ((inside - (Vector2)wanted).sqrMagnitude > 0.0001f)
        {
            transform.position = new Vector3(inside.x, inside.y, wanted.z);
            Debug.Log($"WeekendVenueAnchor: {venue} was {Vector2.Distance(inside, wanted):0.0}m outside the " +
                      "paddock boundary and has been pulled back to the edge nearest it.", this);
        }

        if (standPoint == null || standPoint == transform) return;

        Vector3 stand = standPoint.position;
        Vector2 standInside = Inside(stand);
        standPoint.position = new Vector3(standInside.x, standInside.y, stand.z);
    }

    // The nearest point in the walkable area, and then a step further in. Constrain() lands a stray point
    // exactly ON the edge, which is the one place the player can stand but never quite be inside, so the
    // marker is nudged off the fence line and back into the paddock proper.
    static Vector2 Inside(Vector2 wanted)
    {
        const float Inset = 0.75f;

        Vector2 edge = PaddockBoundary.Constrain(wanted);
        Vector2 inward = edge - wanted;
        if (inward.sqrMagnitude < 0.0001f) return edge;      // already inside; leave it exactly where it is

        return PaddockBoundary.Constrain(edge + inward.normalized * Inset);
    }

    // Re-apply the rule to every marker in the scene. Used by anything that moves the walkable area around
    // after the venues have been placed.
    public static void ConstrainAll()
    {
        for (int i = 0; i < All.Count; i++)
            if (All[i] != null) All[i].ConstrainToPaddock();
    }

    // The anchor for a venue nearest a point — grandstands are the only venue with more than one, and the
    // nearest seat is the one worth walking to.
    public static WeekendVenueAnchor Nearest(WeekendVenue venue, Vector3 to)
    {
        WeekendVenueAnchor best = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < All.Count; i++)
        {
            var a = All[i];
            if (a == null || a.venue != venue) continue;
            float d = (a.transform.position - to).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = a; }
        }
        return best;
    }

    public static WeekendVenueAnchor Find(WeekendVenue venue)
    {
        var player = OnFootPlayer();
        return Nearest(venue, player != null ? player.position : Vector3.zero);
    }

    // The anchor a specific booking should send the player to. A booking that names a marker object goes to
    // that exact one; everything else goes to the nearest anchor for its venue, exactly as before.
    public static WeekendVenueAnchor Find(WeekendVenue venue, string markerLocation)
    {
        if (!string.IsNullOrWhiteSpace(markerLocation))
        {
            string want = Simplify(markerLocation);
            for (int i = 0; i < All.Count; i++)
            {
                var a = All[i];
                if (a != null && !string.IsNullOrEmpty(a.markerLocation) && Simplify(a.markerLocation) == want)
                    return a;
            }
        }
        return Find(venue);
    }

    // Matched the way every other marker name is: ignoring case, spaces and underscores, so "pitbox marker"
    // in a plan file still finds "PitBox_Marker" in the scene.
    static string Simplify(string text) => WeekendMarkerNames.Simplify(text);

    public static bool Exists(WeekendVenue venue) => Find(venue) != null;

    // The walking player, if there is one. Everything here is about somebody on foot, so a scene with the
    // player in the car simply has no distance to measure.
    //
    // Read off OnFootController's own registry rather than searched for: the objective HUD asks this
    // several times a frame (once per IMGUI event, plus its Update), and a full-scene search that often
    // is the single most expensive thing in the paddock.
    public static Transform OnFootPlayer()
    {
        var controller = OnFootController.Current;
        return controller != null ? controller.transform : null;
    }

    public bool PlayerIsHere()
    {
        var player = OnFootPlayer();
        if (player == null) return false;

        // An authored marker owns its own perimeter — the shape somebody drew round the pit stall, not a
        // radius from a point. Only a venue with no marker falls back to the range.
        if (marker != null) return marker.Contains(player.position);

        return Vector2.Distance(player.position, transform.position) <= arriveRange;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.78f, 0.25f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, arriveRange);
    }
#endif
}
