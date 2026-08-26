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
// before an obligation will start. It carries no behaviour beyond being findable.
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

    public string Label => string.IsNullOrEmpty(label) ? WeekendVenues.Label(venue) : label;

    public Vector3 StandPosition => standPoint != null ? standPoint.position : transform.position;

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

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
