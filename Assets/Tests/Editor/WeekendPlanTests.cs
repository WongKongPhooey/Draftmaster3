using System.Linq;
using Draftmaster.Weekend;
using NUnit.Framework;
using UnityEngine;

// The authored-weekend format: whether a plan file says what it means, whether the game builds what the
// file says, and whether a broken file fails loudly instead of quietly playing a different weekend.
//
// All of this is arithmetic and text, so it belongs in EditMode. The half that can only be seen — the
// marker standing on the authored spot — is in WeekendVenuePresenceTests.
public class WeekendPlanTests
{
    // ------------------------------------------------------------------ the vocabulary

    [Test]
    public void EveryEventIdIsUniqueAndResolves()
    {
        var ids = WeekendEventCatalog.Ids();
        CollectionAssert.AllItemsAreUnique(ids, "Two catalogue entries share an id — a plan file naming it would be ambiguous.");

        foreach (var id in ids)
        {
            Assert.IsTrue(WeekendEventCatalog.TryGet(id, out var type), $"'{id}' does not resolve back to itself.");
            Assert.Greater(type.minutes, 0, $"'{id}' has no default length, so a booking that omits minutes would be zero-length.");
            Assert.IsNotEmpty(type.title, $"'{id}' has no title, so it would be a blank row on the sheet.");
        }
    }

    [Test]
    public void EveryActivityKindCanBeAuthored()
    {
        // A kind the catalogue cannot express is a kind an authored weekend can never contain — the plan
        // file would silently be less capable than the generated schedule it replaces.
        foreach (ActivityKind kind in System.Enum.GetValues(typeof(ActivityKind)))
            Assert.IsTrue(WeekendEventCatalog.TryGetByKind(kind, null, out _),
                          $"ActivityKind.{kind} has no event id, so no plan file can ever book one.");
    }

    [Test]
    public void EventIdsAreMatchedForgivingly()
    {
        Assert.IsTrue(WeekendEventCatalog.TryGet("sponsor_event-photoshoot", out var canonical));
        foreach (var spelling in new[] { "Sponsor_Event-PhotoShoot", "sponsor event photoshoot", "SPONSOR_EVENT_PHOTOSHOOT" })
        {
            Assert.IsTrue(WeekendEventCatalog.TryGet(spelling, out var got), $"'{spelling}' did not resolve.");
            Assert.AreEqual(canonical.kind, got.kind);
        }
    }

    // ------------------------------------------------------------------ the format

    [Test]
    public void AnEmptyPlanIsSixEmptyHalfDays()
    {
        var plan = WeekendPlan.Empty("WatkinsGlen", RacingSeries.Cup);

        Assert.AreEqual(WeekendSlots.Count, plan.slots.Count);
        Assert.AreEqual(0, plan.EventCount, "A blank sheet should have nothing booked on it.");

        foreach (var slot in WeekendSlots.All)
            Assert.IsNotNull(plan.Slot(slot), $"{slot} is missing from a blank plan.");

        CollectionAssert.IsEmpty(plan.Problems(), "A blank plan is a valid plan — it is where authoring starts.");
    }

    [Test]
    public void ClockTimesParseTheWaysAPersonWritesThem()
    {
        Assert.AreEqual(9 * 60 + 45, WeekendPlan.ParseClock("09:45"));
        Assert.AreEqual(9 * 60 + 45, WeekendPlan.ParseClock("9:45"));
        Assert.AreEqual(9 * 60 + 45, WeekendPlan.ParseClock("0945"));
        Assert.AreEqual(14 * 60, WeekendPlan.ParseClock("14:00"));

        foreach (var bad in new[] { "", "  ", "half nine", "25:00", "09:70", "9" })
            Assert.AreEqual(-1, WeekendPlan.ParseClock(bad), $"'{bad}' should not parse as a time.");
    }

    // ------------------------------------------------------------------ validation

    [Test]
    public void APlanThatWouldPlayBadlyIsReportedRatherThanAccepted()
    {
        var plan = WeekendPlan.Empty("WatkinsGlen", RacingSeries.Cup);

        // Before the half-day opens: the player can never be there for it.
        plan.EnsureSlot(WeekendSlot.FridayAM).events.Add(
            new WeekendPlanEvent { @event = "team-briefing", start = "06:00" });

        // Runs past the close of the half-day.
        plan.EnsureSlot(WeekendSlot.SundayPM).events.Add(
            new WeekendPlanEvent { @event = "session-race", start = "17:00", minutes = 180 });

        // An id nobody ever defined.
        plan.EnsureSlot(WeekendSlot.SaturdayAM).events.Add(
            new WeekendPlanEvent { @event = "sponsor_event-yacht_party", start = "10:00" });

        // Not a time.
        plan.EnsureSlot(WeekendSlot.SaturdayPM).events.Add(
            new WeekendPlanEvent { @event = "media-hit", start = "lunchtime" });

        var problems = plan.Problems();
        Assert.AreEqual(4, problems.Count,
                        "Expected one problem per broken booking. Got:\n  " + string.Join("\n  ", problems));

        Assert.IsTrue(problems.Any(p => p.Contains("before the half-day opens")));
        Assert.IsTrue(problems.Any(p => p.Contains("past the")));
        Assert.IsTrue(problems.Any(p => p.Contains("no such event id")));
        Assert.IsTrue(problems.Any(p => p.Contains("expected a 24h clock time")));
    }

    [Test]
    public void AValidPlanReportsNothing()
    {
        var plan = WeekendPlan.Empty("WatkinsGlen", RacingSeries.Cup);
        plan.EnsureSlot(WeekendSlot.FridayAM).events.Add(
            new WeekendPlanEvent { @event = "sponsor_event-photoshoot", start = "09:45" });
        plan.EnsureSlot(WeekendSlot.FridayAM).events.Add(
            new WeekendPlanEvent { @event = "watch-qualifying", start = "10:00", series = "Trucks" });

        CollectionAssert.IsEmpty(plan.Problems());
    }

    // ------------------------------------------------------------------ plan -> timetable

    [Test]
    public void AnAuthoredWeekendIsExactlyWhatTheFileSays()
    {
        var plan = WeekendPlan.Empty("TestTrack", RacingSeries.Cup);
        plan.EnsureSlot(WeekendSlot.FridayAM).events.Add(
            new WeekendPlanEvent { @event = "sponsor_event-photoshoot", start = "09:45",
                                   markerLocation = "PhotoShoot_Marker" });
        plan.EnsureSlot(WeekendSlot.SundayPM).events.Add(
            new WeekendPlanEvent { @event = "session-race", start = "14:00", minutes = 180 });

        var timetable = new WeekendTimetable { playerSeries = RacingSeries.Cup, trackName = "TestTrack" };
        WeekendPlanLibrary.Apply(plan, timetable, RacingSeries.Cup);

        Assert.AreEqual(2, timetable.Activities.Count,
                        "The weekend has bookings the file never mentioned — the plan is supposed to be the whole schedule.");

        var shoot = timetable.Activities.First(a => a.kind == ActivityKind.PhotoShoot);
        Assert.AreEqual(WeekendSlot.FridayAM, shoot.slot);
        Assert.AreEqual(9 * 60 + 45, shoot.startMinute);
        Assert.AreEqual("PhotoShoot_Marker", shoot.markerLocation,
                        "The booking lost the marker it named, so it would fall back to the generated venue.");

        // Defaults come off the catalogue rather than having to be restated in the file.
        WeekendEventCatalog.TryGet("sponsor_event-photoshoot", out var type);
        Assert.AreEqual(type.minutes, shoot.minutes);
        Assert.AreEqual(type.appearanceFee, shoot.appearanceFee);
        Assert.AreEqual(type.mandatory, shoot.mandatory);
        Assert.AreEqual(type.subtitle, shoot.subtitle);
    }

    [Test]
    public void AnOverrideInTheFileBeatsTheCatalogue()
    {
        var plan = WeekendPlan.Empty("TestTrack", RacingSeries.Cup);
        plan.EnsureSlot(WeekendSlot.FridayPM).events.Add(new WeekendPlanEvent
        {
            @event = "sponsor_event-duty",
            start = "13:15",
            minutes = 45,
            title = "PIT-STOP CHALLENGE",
            fee = 900,
            mandatory = 2,
        });

        var timetable = new WeekendTimetable { playerSeries = RacingSeries.Cup };
        WeekendPlanLibrary.Apply(plan, timetable, RacingSeries.Cup);

        var duty = timetable.Activities.Single();
        Assert.AreEqual("PIT-STOP CHALLENGE", duty.title);
        Assert.AreEqual(45, duty.minutes);
        Assert.AreEqual(900, duty.appearanceFee);
        Assert.IsFalse(duty.mandatory, "mandatory:2 should force a booking optional even though the catalogue says otherwise.");
    }

    [Test]
    public void AWatchBookingKeepsWhoseSessionItIs()
    {
        var plan = WeekendPlan.Empty("TestTrack", RacingSeries.Cup);
        plan.EnsureSlot(WeekendSlot.FridayPM).events.Add(
            new WeekendPlanEvent { @event = "watch-qualifying", start = "13:00", series = "Trucks" });

        var timetable = new WeekendTimetable { playerSeries = RacingSeries.Cup };
        WeekendPlanLibrary.Apply(plan, timetable, RacingSeries.Cup);

        var watch = timetable.Activities.Single();
        Assert.AreEqual(RacingSeries.Trucks, watch.series,
                        "A spectate booking lost whose session it was — the schedule could not tell three championships apart.");
        StringAssert.StartsWith(SeriesCatalog.ShortCode(RacingSeries.Trucks), watch.title,
                                "A watch booking should be titled with the series code, e.g. 'TRK QUALIFYING'.");
        Assert.AreEqual(WeekendVenue.Grandstand, WeekendVenues.For(watch.kind));
    }

    [Test]
    public void ABrokenBookingCostsOnlyItselfAndNotTheWeekend()
    {
        var plan = WeekendPlan.Empty("TestTrack", RacingSeries.Cup);
        var slot = plan.EnsureSlot(WeekendSlot.FridayAM);
        slot.events.Add(new WeekendPlanEvent { @event = "not-a-real-event", start = "09:00" });
        slot.events.Add(new WeekendPlanEvent { @event = "team-briefing", start = "08:00" });
        slot.events.Add(new WeekendPlanEvent { @event = "media-hit", start = "not a time" });

        var timetable = new WeekendTimetable { playerSeries = RacingSeries.Cup };
        WeekendPlanLibrary.Apply(plan, timetable, RacingSeries.Cup);

        Assert.AreEqual(1, timetable.Activities.Count,
                        "One bad line should cost that booking, not the rest of the sheet.");
        Assert.AreEqual(ActivityKind.TeamBriefing, timetable.Activities[0].kind);
    }

    [Test]
    public void ADependencyIsResolvedToTheBookingItNames()
    {
        var plan = WeekendPlan.Empty("TestTrack", RacingSeries.Cup);
        plan.EnsureSlot(WeekendSlot.SaturdayAM).events.Add(
            new WeekendPlanEvent { @event = "session-practice", start = "10:30", minutes = 60 });
        plan.EnsureSlot(WeekendSlot.SaturdayAM).events.Add(
            new WeekendPlanEvent { @event = "team-debrief", start = "11:45", requires = "session-practice" });

        var timetable = new WeekendTimetable { playerSeries = RacingSeries.Cup };
        WeekendPlanLibrary.Apply(plan, timetable, RacingSeries.Cup);

        var practice = timetable.Activities.First(a => a.kind == ActivityKind.Practice);
        var debrief = timetable.Activities.First(a => a.kind == ActivityKind.Debrief);

        Assert.AreEqual(practice.id, debrief.requiresId,
                        "The debrief is not gated on its practice, so it would be offered before the run happened.");
    }

    // ------------------------------------------------------------------ markers

    [Test]
    public void EveryVenueHasAMarkerNameThatResolvesBackToIt()
    {
        // The default name a plan file gets when it does not override the marker location has to be a name
        // the convention actually recognises, or the documented default would silently match nothing.
        foreach (WeekendVenue venue in System.Enum.GetValues(typeof(WeekendVenue)))
        {
            if (venue == WeekendVenue.None) continue;

            string name = WeekendMarkerNames.DefaultNameFor(venue);
            Assert.IsTrue(WeekendMarkerNames.IsMarkerName(name), $"'{name}' is not recognised as a marker name.");
            Assert.AreEqual(venue, WeekendMarkerNames.VenueFromName(name),
                            $"'{name}' is the documented default for {venue} but does not resolve back to it.");
        }
    }

    [Test]
    public void MarkerNamesAreMatchedForgivingly()
    {
        foreach (var spelling in new[] { "PitBox_Marker", "Pitbox_Marker", "pit_box_marker", "PITBOX_MARKER" })
            Assert.AreEqual(WeekendVenue.PitBox, WeekendMarkerNames.VenueFromName(spelling),
                            $"'{spelling}' should name the pit box.");

        // A name that is a marker but matches no venue is still a marker — that is the override case, reached
        // by a plan file naming it explicitly.
        Assert.IsTrue(WeekendMarkerNames.IsMarkerName("Podium_Marker"));
        Assert.AreEqual(WeekendVenue.None, WeekendMarkerNames.VenueFromName("Podium_Marker"));

        Assert.IsFalse(WeekendMarkerNames.IsMarkerName("PitBox"), "Only objects ending _Marker are markers.");

        // A plan file's markerLocation is compared with the same rule, so the two can never drift apart.
        Assert.IsTrue(WeekendMarkerNames.SameName("pitbox marker", "PitBox_Marker"));
    }

    // ------------------------------------------------------------------ the shipped files

    [Test]
    public void EveryShippedPlanIsValid()
    {
        var files = Resources.LoadAll<TextAsset>(WeekendPlanLibrary.ResourceFolder);
        Assert.IsNotEmpty(files, "No authored weekends at all — WatkinsGlen.Cup.json should be there.");

        foreach (var file in files)
        {
            WeekendPlan plan = null;
            Assert.DoesNotThrow(() => plan = JsonUtility.FromJson<WeekendPlan>(file.text),
                                $"{file.name}.json is not readable JSON.");
            Assert.IsNotNull(plan, $"{file.name}.json parsed to nothing.");

            var problems = plan.Problems();
            CollectionAssert.IsEmpty(problems,
                                     $"{file.name}.json:\n  " + string.Join("\n  ", problems));
        }
    }

    [Test]
    public void WatkinsGlenCupIsAuthoredAndDrivable()
    {
        var plan = WeekendPlanLibrary.For("WatkinsGlen", RacingSeries.Cup);
        Assert.IsNotNull(plan, "WatkinsGlen/Cup has no plan file, so it still builds from the generated schedule.");

        var timetable = WeekendTimetable.Build(RacingSeries.Cup, weekendId: 3, trackName: "WatkinsGlen");
        Assert.IsTrue(timetable.authored, "The authored plan was not used — the round is still generating its own schedule.");

        // The three things a weekend cannot be missing.
        foreach (var kind in new[] { ActivityKind.Practice, ActivityKind.Qualifying, ActivityKind.Race })
            Assert.IsNotNull(timetable.PlayerSession(kind),
                             $"The authored Watkins Glen weekend has no {kind} for the player to drive.");

        // ...and the thing that started all this: a truck session to go and watch.
        Assert.IsTrue(timetable.Activities.Any(a => a.kind == ActivityKind.SpectateQualifying && a.series == RacingSeries.Trucks),
                      "No TRK qualifying to spectate at Watkins Glen.");
    }

    [Test]
    public void AnUnauthoredTrackStillBuildsItsOwnWeekend()
    {
        var timetable = WeekendTimetable.Build(RacingSeries.Cup, weekendId: 3, trackName: "NoSuchTrackExists");

        Assert.IsFalse(timetable.authored);
        Assert.IsNotEmpty(timetable.Activities,
                          "A track with no plan file lost its weekend entirely — the procedural fallback is what keeps the calendar playable.");
        Assert.IsNotNull(timetable.PlayerSession(ActivityKind.Race));
    }
}
