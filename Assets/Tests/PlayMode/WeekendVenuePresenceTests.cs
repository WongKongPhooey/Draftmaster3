using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// The weekend happens in the paddock now, so the paddock has to have the places in it.
//
// This loads the race scene the way the game does and checks the world it builds: a pit box to meet at, a
// motorhome to debrief in, a drivers' room with chairs, a fan fence with a crowd behind it, a hospitality
// tent, an intro stage, and a seat in a grandstand. Then it books an obligation and walks the player to it,
// which is the whole loop the schedule now drives: commit → objective → walk → talk to whoever is waiting.
//
// Reflection everywhere: this assembly cannot reference Assembly-CSharp.
public class WeekendVenuePresenceTests
{
    const string Race = "RaceScene";

    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static
                           | BindingFlags.Public | BindingFlags.NonPublic;

    // WeekendVenue, in declaration order.
    const int PitBox = 1, Motorhome = 2, MeetingRoom = 3, SigningFence = 4, SponsorSuite = 5, IntroStage = 6, Grandstand = 7;

    static bool _loaded;

    [UnitySetUp]
    public IEnumerator ArriveAtTheTrack()
    {
        if (_loaded && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == Race) yield break;

        PlayModeScenes.Go(Race);
        yield return PlayModeScenes.WaitForScene(Race);

        // The venues are placed off the paddock rectangle and the motorhome lot, both of which take a
        // moment to appear, so wait for the builder to have finished rather than for a fixed number of
        // frames.
        yield return PlayModeScenes.WaitFor(() => AnchorCount() > 0,
                                            "no weekend venue was ever placed in the race scene");
        for (int i = 0; i < 20; i++) yield return null;   // let the rest of them land
        _loaded = true;
    }

    [UnityTest]
    public IEnumerator ThePaddockHasEverywhereTheWeekendSendsYou()
    {
        yield return null;

        AssertVenue(PitBox, "the pit box (plan meetings, and where a broadcaster catches you)");
        AssertVenue(Motorhome, "the player's own motorhome (debriefs)");
        AssertVenue(MeetingRoom, "the drivers' room (drivers meeting, press conference)");
        AssertVenue(SigningFence, "the fan fence (signing sessions)");
        AssertVenue(SponsorSuite, "the hospitality tent (sponsor duty, photo shoots)");
        AssertVenue(IntroStage, "the intro stage (driver introductions)");
        AssertVenue(Grandstand, "a grandstand seat (watching somebody else's session)");
    }

    // The paddock rectangle is as long as the pit straight, which at a road course is several hundred
    // metres. Laying the venues out across the whole of it once put the hospitality tent 300m from the
    // drivers' room, which turns a three-day schedule into a commute — they have to be a cluster you walk
    // between, not a route march.
    [UnityTest]
    public IEnumerator TheVenuesAreAllWithinAWalkOfEachOther()
    {
        yield return null;

        const float FurthestSensible = 200f;   // generous: a big paddock is still a big paddock

        var home = AnchorFor(Motorhome);
        Assert.IsNotNull(home, "No motorhome to measure the paddock from.");

        foreach (int venue in new[] { PitBox, MeetingRoom, SigningFence, SponsorSuite, IntroStage })
        {
            var anchor = AnchorFor(venue);
            if (anchor == null) continue;

            float metres = Vector2.Distance(home.transform.position, anchor.transform.position);
            Assert.Less(metres, FurthestSensible,
                        $"Venue {venue} is {metres:0} m from the player's motorhome — that is a hike, not a paddock.");
        }
    }

    // ...and every one of them has to be somewhere the player is allowed to stand. The paddock rectangle
    // the venues are laid out on is derived from the pit lane; the walkable area is an authored polygon and
    // is often smaller, so a mark placed on the rectangle's edge can sit outside the boundary, where the
    // player is clamped back the moment they walk at it — an obligation you can see and never attend.
    [UnityTest]
    public IEnumerator EveryVenueIsSomewhereThePlayerCanStand()
    {
        yield return null;

        var boundary = PlayModeScenes.GameType("PaddockBoundary");
        bool anyActive = (bool)boundary.GetProperty("AnyActive", Any).GetValue(null);
        if (!anyActive) Assert.Ignore("This track has no walkable boundary to be outside of.");

        var constrain = boundary.GetMethod("Constrain", Any);

        foreach (int venue in new[] { PitBox, Motorhome, MeetingRoom, SigningFence, SponsorSuite, IntroStage })
        {
            var anchor = AnchorFor(venue);
            if (anchor == null) continue;

            Vector3 stand = (Vector3)anchor.GetType().GetProperty("StandPosition", Any).GetValue(anchor);
            var inside = (Vector2)constrain.Invoke(null, new object[] { (Vector2)stand });

            float pushedBack = Vector2.Distance(inside, stand);
            float arriveRange = (float)anchor.GetType().GetField("arriveRange").GetValue(anchor);

            Assert.Less(pushedBack, arriveRange,
                        $"Venue {venue}'s standing mark is {pushedBack:0.0} m outside the walkable paddock, and " +
                        $"it only counts as attended within {arriveRange:0.0} m — the player can never reach it.");
        }
    }

    // Nothing the weekend builds may be solid where the player is, or anywhere in the motorhome row.
    //
    // The drivers' room was once measured from the middle of the paddock and went up around the player's
    // own RV: four walls and a doorway on the wrong side of them, so the player spawned inside the
    // motorhome and could not walk out of it. The venues are laid out from the end of the motorhome row
    // now, and this is the check that keeps them there.
    [UnityTest]
    public IEnumerator NoVenueIsBuiltOnTopOfThePlayer()
    {
        yield return null;

        // Measured at the motorhome, not at wherever the player happens to be stood: they are supposed to
        // walk up to a fence and a top table, so "solid near the player" is only a fault where they wake up.
        var home = AnchorFor(Motorhome);
        Assert.IsNotNull(home, "No motorhome to check around.");

        var venues = GameObject.Find("WeekendVenues");
        Assert.IsNotNull(venues, "The weekend built no venues at all.");

        // Generous: the player has to be able to stand up, turn round and walk out.
        var trapped = Physics2D.OverlapCircleAll(home.transform.position, 4f);
        foreach (var hit in trapped)
        {
            if (hit == null || hit.isTrigger) continue;
            if (!hit.transform.IsChildOf(venues.transform)) continue;

            Assert.Fail($"'{PathOf(hit.transform)}' is solid within 4 m of the player's motorhome door — " +
                        "the weekend has built something around where they wake up.");
        }

        // ...and the same for the motorhome row as a whole, so the next driver along is not walled in either.
        var lot = GameObject.Find("MotorhomeLotBoundary");
        if (lot != null)
        {
            var bounds = lot.GetComponent<Collider2D>().bounds;
            foreach (var solid in venues.GetComponentsInChildren<Collider2D>(true))
            {
                if (solid.isTrigger) continue;
                Assert.IsFalse(bounds.Intersects(solid.bounds),
                               $"'{PathOf(solid.transform)}' is built into the motorhome row.");
            }
        }
    }

    static string PathOf(Transform t)
    {
        string path = t.name;
        for (var p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return path;
    }

    // Every venue that is a conversation needs somebody stood at it. A place with no host is a walk to
    // nothing.
    [UnityTest]
    public IEnumerator SomebodyIsWaitingAtEveryVenueYouTalkAt()
    {
        yield return null;

        foreach (int venue in new[] { PitBox, Motorhome, MeetingRoom, SigningFence, SponsorSuite, IntroStage })
            Assert.IsNotNull(HostFor(venue),
                             $"Nobody is stood at venue {venue}, so an obligation booked there could never start.");
    }

    // The room is the drivers meeting: a chair for everybody entered at the circuit, and bodies in the
    // front rows so it reads as a room full of drivers.
    [UnityTest]
    public IEnumerator TheDriversRoomHasAChairForEveryDriver()
    {
        yield return null;

        var room = GameObject.Find("DriversRoom");
        Assert.IsNotNull(room, "The drivers' room was not built.");

        int seats = 0, drivers = 0;
        foreach (var t in room.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.StartsWith("Seat_")) seats++;
            if (t.name.StartsWith("SeatedDriver_")) drivers++;
        }

        int entered = 0;
        var catalog = PlayModeScenes.GameType("Draftmaster.Weekend.SeriesCatalog");
        var all = catalog.GetField("All", Any).GetValue(null) as System.Array;
        var fieldSize = catalog.GetMethod("FieldSize", Any);
        foreach (var series in all) entered += (int)fieldSize.Invoke(null, new[] { series });

        Assert.AreEqual(entered, seats,
                        "The drivers' room should seat every driver entered at the circuit across all three championships.");
        Assert.Greater(drivers, 0, "Nobody is sat in the drivers' room, so the meeting plays to an empty room.");
    }

    // Signing is done through the fence with the fans on the other side of it — the barrier is the point.
    [UnityTest]
    public IEnumerator ThereIsACrowdOnTheOtherSideOfTheFence()
    {
        yield return null;

        var fence = GameObject.Find("FanFence");
        Assert.IsNotNull(fence, "The fan fence was not built.");
        Assert.IsNotNull(fence.transform.Find("Rail"), "The fence has no rail, so there is nothing to sign across.");

        int fans = 0;
        foreach (var t in fence.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("Fan_")) fans++;
        Assert.Greater(fans, 4, "There is no crowd behind the fence.");

        // The rail is solid: the player signs from the paddock side rather than walking into the fan zone.
        var rail = fence.transform.Find("Rail");
        Assert.IsNotNull(rail.GetComponent<BoxCollider2D>(), "The fence rail is not solid — the player can walk through it.");
    }

    // The loop the schedule now drives, walked end to end: book something, be told where it is, go there,
    // and find the conversation waiting.
    [UnityTest]
    public IEnumerator BookingAnObligationSendsYouToItAndItStartsWhenYouArrive()
    {
        yield return null;

        var appointment = PlayModeScenes.GameType("WeekendAppointment");
        var director = PlayModeScenes.GameType("WeekendDirector");
        var timetable = director.GetProperty("Timetable", Any).GetValue(null);

        var booking = FirstTalkableBooking(timetable);
        if (booking == null) Assert.Ignore("This weekend booked nothing that is a conversation.");

        director.GetMethod("Begin", Any).Invoke(null, new[] { booking });

        Assert.IsTrue((bool)appointment.GetProperty("Any", Any).GetValue(null),
                      "Committing to a booking did not make an appointment, so nothing would point the player at it.");

        float distance = (float)appointment.GetMethod("DistanceRemaining", Any).Invoke(null, null);
        Assert.Greater(distance, 0f, "The venue is not somewhere to walk to — it is already on top of the player.");

        // Take the marker's own shortcut rather than simulating a walk across the paddock.
        var hud = PlayModeScenes.GameType("WeekendObjectiveHUD");
        Assert.IsTrue((bool)hud.GetMethod("TravelThere", Any).Invoke(null, null), "TRAVEL THERE went nowhere.");
        yield return null;

        float after = (float)appointment.GetMethod("DistanceRemaining", Any).Invoke(null, null);
        Assert.IsTrue((bool)appointment.GetMethod("PlayerHasArrived", Any).Invoke(null, null),
                      $"Travelling to '{booking.GetType().GetField("title").GetValue(booking)}' left the player " +
                      $"{after:0.0} m away — the venue's standing mark is somewhere they are not allowed to stand.");

        // ...and the person stood there has business with them.
        int venueIndex = (int)PlayModeScenes.GameType("Draftmaster.Weekend.WeekendVenues")
            .GetMethod("For", Any).Invoke(null, new[] { booking.GetType().GetField("kind").GetValue(booking) });

        var host = HostFor(venueIndex);
        Assert.IsNotNull(host, "Nobody is at the venue this booking was sent to.");
        Assert.IsTrue((bool)host.GetType().GetProperty("HasBusiness", Any).GetValue(host),
                      "The host at the venue has nothing for the player, so pressing E would just be small talk.");

        // Walking up and pressing the action button opens the conversation rather than a panel.
        host.GetType().GetMethod("Interact", Any).Invoke(host, null);
        yield return null;

        Assert.IsTrue((bool)host.GetType().GetProperty("IsTalking", Any).GetValue(host),
                      "Interacting with the host did not start the obligation.");

        // Leave the weekend as we found it.
        appointment.GetMethod("Clear", Any).Invoke(null, null);
    }

    // ------------------------------------------------------------------ helpers

    // A booking the player could actually commit to right now: a conversation, at a venue that has somebody
    // stood at it, and one the ledger's clock will allow (not already missed, not clashing, not later in
    // the weekend than the cursor has reached).
    static object FirstTalkableBooking(object timetable)
    {
        var activities = timetable.GetType().GetProperty("Activities", Any).GetValue(timetable) as IEnumerable;
        var venues = PlayModeScenes.GameType("Draftmaster.Weekend.WeekendVenues").GetMethod("For", Any);
        var canDo = PlayModeScenes.GameType("Draftmaster.Weekend.WeekendLedger").GetMethod("CanDo", Any);

        foreach (var a in activities)
        {
            var kind = a.GetType().GetField("kind").GetValue(a);
            int venue = (int)venues.Invoke(null, new[] { kind });
            // Anything that is a conversation with a host: not driving, not resting, not a grandstand seat.
            if (venue == 0 || venue == Grandstand) continue;
            if (HostFor(venue) == null) continue;

            var args = new object[] { a, null };
            if (!(bool)canDo.Invoke(null, args)) continue;
            return a;
        }
        return null;
    }

    static void AssertVenue(int venue, string what)
    {
        Assert.IsNotNull(AnchorFor(venue), $"The paddock has no {what}.");
    }

    static Component AnchorFor(int venue)
    {
        var type = PlayModeScenes.GameType("WeekendVenueAnchor");
        foreach (var anchor in Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var field = type.GetField("venue");
            if ((int)field.GetValue(anchor) == venue) return anchor as Component;
        }
        return null;
    }

    static Component HostFor(int venue)
    {
        var type = PlayModeScenes.GameType("WeekendVenueHost");
        foreach (var host in Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var field = type.GetField("venue");
            if ((int)field.GetValue(host) == venue) return host as Component;
        }
        return null;
    }

    static int AnchorCount()
    {
        var type = PlayModeScenes.GameType("WeekendVenueAnchor");
        return Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
    }
}
