using Draftmaster.Weekend;
using UnityEngine;

// The booking the player has said yes to and has not turned up for yet.
//
// Committing to something on the timetable no longer runs it — it makes an appointment. The sheet closes,
// an objective marker points at the place it happens, and the obligation only starts when the player is
// stood there and interacts with whoever is waiting. That is the whole difference between a weekend you
// read and a weekend you walk around.
//
// Kept in PlayerPrefs rather than a static for the same reason PendingRouteId is: the weekend crosses scene
// loads, and an appointment made in the paddock has to survive the reload that follows a session.
public static class WeekendAppointment
{
    const string Key = "weekend.appointment";

    // The id of the booking being walked to, or "" when the player is not due anywhere.
    public static string PendingId
    {
        get => PlayerPrefs.GetString(Key, "");
        private set { PlayerPrefs.SetString(Key, value ?? ""); PlayerPrefs.Save(); }
    }

    public static bool Any => !string.IsNullOrEmpty(PendingId) && Pending != null;

    // The activity itself, resolved against the current timetable. Null when nothing is booked, or when the
    // weekend has moved on far enough that the booking no longer exists.
    public static WeekendActivity Pending
    {
        get
        {
            string id = PlayerPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(id)) return null;

            var a = WeekendDirector.Timetable?.ById(id);
            if (a == null) return null;

            // Somebody else's clock moved past it — a missed appointment is not an appointment.
            if (WeekendLedger.IsDone(a.id) || WeekendLedger.IsMissed(a.id)) { Clear(); return null; }
            return a;
        }
    }

    public static WeekendVenue PendingVenue
    {
        get
        {
            var a = Pending;
            return a == null ? WeekendVenue.None : WeekendVenues.For(a.kind);
        }
    }

    // Say yes to a booking: from here it is a place to be, not a panel to read.
    public static void Make(WeekendActivity a)
    {
        if (a == null) return;
        PendingId = a.id;
    }

    public static void Clear() => PendingId = "";

    // True when this activity is the one the player is currently due at — what a venue's host checks before
    // it will start anything.
    public static bool IsPending(WeekendActivity a) => a != null && a.id == PlayerPrefs.GetString(Key, "");

    // Where the player has to be. Null when nothing is booked or the track has no such place.
    public static WeekendVenueAnchor Where()
    {
        var venue = PendingVenue;
        if (venue == WeekendVenue.None) return null;

        // A booking may name the exact object it happens at ("markerLocation": "Podium_Marker" in the plan
        // file); without one it goes to whichever anchor of its venue is nearest, as every generated venue does.
        var pending = Pending;
        return WeekendVenueAnchor.Find(venue, pending != null ? pending.markerLocation : "");
    }

    // The thing to walk to: the venue's mark, or — for the player's own sessions, which are not kept
    // anywhere in the paddock — the car itself. A session on the sheet is still somewhere to be.
    public static Transform Target()
    {
        var a = Pending;
        if (a == null) return null;
        if (!a.IsOnTrack)
        {
            var anchor = Where();
            return anchor != null ? anchor.transform : null;
        }

        return PlayerCar();
    }

    // The scene's parked player car. Held onto rather than searched for every call: Target() is asked
    // several times a frame while the objective marker is up, and a full-scene search per ask is real
    // money in a paddock. A scene change destroys the PitLaneStart, which reads as a Unity null here and
    // triggers one fresh search — capped at one per frame so a scene that simply has no pit-lane flow
    // does not pay for a scan on every ask.
    static PitLaneStart _pitLane;
    static int _pitLaneSearchedFrame = -1;

    static Transform PlayerCar()
    {
        if (_pitLane == null && _pitLaneSearchedFrame != Time.frameCount)
        {
            _pitLaneSearchedFrame = Time.frameCount;
            _pitLane = Object.FindFirstObjectByType<PitLaneStart>();
        }
        return _pitLane != null && _pitLane.car != null ? _pitLane.car.transform : null;
    }

    // What the marker calls the place.
    public static string TargetLabel()
    {
        var a = Pending;
        if (a == null) return "";
        if (a.IsOnTrack) return "your car";

        var anchor = Where();
        return anchor != null ? anchor.Label : WeekendVenues.Label(PendingVenue);
    }

    // Metres left to walk, or -1 when there is nothing to walk to (no appointment, no anchor, or the player
    // is not on foot).
    public static float DistanceRemaining()
    {
        var target = Target();
        var player = WeekendVenueAnchor.OnFootPlayer();
        if (target == null || player == null) return -1f;
        return Vector2.Distance(player.position, target.position);
    }

    public static bool PlayerHasArrived()
    {
        var a = Pending;
        if (a == null) return false;
        if (!a.IsOnTrack)
        {
            var anchor = Where();
            return anchor != null && anchor.PlayerIsHere();
        }

        // A session counts as arrived at when the player is close enough to climb in; PitLaneStart owns
        // the getting-in itself.
        float d = DistanceRemaining();
        return d >= 0f && d <= 4f;
    }
}
