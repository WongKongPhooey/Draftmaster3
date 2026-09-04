using Draftmaster.Weekend;
using NUnit.Framework;

// Who has the circuit, and when.
//
// The rule the whole thing rests on: cars are on track for a designated practice, qualifying or race and at
// no other time. The three championships share the venue for three days, so the answer is not "is the player
// driving" but "is anybody's session running" — during the truck practice the trucks are out whether the
// player is in that championship, in a grandstand, or under a sponsor's awning on the other side of the
// paddock.
//
// Tested against the timetable rather than the hard-coded session times, so an authored weekend answers the
// same way. WeekendTrackSessions.RunningNow is deliberately not tested here: it only substitutes the live
// ledger's clock, which is PlayerPrefs.
public class WeekendTrackSessionTests
{
    // A venue with no plan file, so these run against the generated schedule.
    const string Track = "Bristol";

    // ------------------------------------------------------------------ every session puts its own series out

    [Test]
    public void EverySessionOnTheSheet_PutsItsOwnChampionshipOnTrack()
    {
        foreach (var mine in SeriesCatalog.All)
        {
            var t = WeekendTimetable.Build(mine, 4, Track);

            foreach (var a in t.Activities)
            {
                if (!WeekendTrackSessions.IsTrackSession(a.kind)) continue;

                var running = WeekendTrackSessions.RunningAt(t, a.slot, a.startMinute);
                Assert.NotNull(running, $"{mine}: nothing on track at the start of {a.title}");
                Assert.AreEqual(a.series, running.series, $"{mine}: wrong championship out during {a.title}");

                // And halfway through it, not just on the opening minute.
                var mid = WeekendTrackSessions.RunningAt(t, a.slot, a.startMinute + a.minutes / 2);
                Assert.NotNull(mid, $"{mine}: the circuit went cold in the middle of {a.title}");
                Assert.AreEqual(a.series, mid.series);
            }
        }
    }

    [Test]
    public void AllThreeChampionships_GetTheCircuitAtSomePoint()
    {
        var t = WeekendTimetable.Build(RacingSeries.Cup, 4, Track);

        foreach (var s in SeriesCatalog.All)
        {
            bool found = false;
            foreach (var slot in WeekendSlots.All)
            {
                for (int m = WeekendSlots.OpensAt(slot); m < WeekendSlots.ClosesAt(slot); m += 5)
                {
                    var running = WeekendTrackSessions.RunningAt(t, slot, m);
                    if (running != null && running.series == s) { found = true; break; }
                }
                if (found) break;
            }
            Assert.IsTrue(found, $"{s} never gets the track all weekend");
        }
    }

    // ------------------------------------------------------------------ the circuit is cold the rest of the time

    [Test]
    public void NothingIsOnTrack_WhenNoSessionIsRunning()
    {
        var t = WeekendTimetable.Build(RacingSeries.Cup, 4, Track);

        // 08:00 Friday is the team strategy briefing: an hour in the hauler lounge, and the first session of
        // the weekend is two hours away. Nobody is on track.
        Assert.IsNull(WeekendTrackSessions.RunningAt(t, WeekendSlot.FridayAM, 8 * 60));
        Assert.IsNull(WeekendTrackSessions.RunningAt(t, WeekendSlot.FridayAM, 9 * 60 + 30));
    }

    [Test]
    public void AMeetingOrASigningNeverPutsCarsOut()
    {
        // Sponsor duties, media and ceremonies fill most of the three days and several of them are booked
        // deliberately over somebody's session. None of them may ever be what is answered as being on track.
        foreach (var mine in SeriesCatalog.All)
        {
            var t = WeekendTimetable.Build(mine, 4, Track);

            foreach (var slot in WeekendSlots.All)
                for (int m = WeekendSlots.OpensAt(slot); m < WeekendSlots.ClosesAt(slot); m += 5)
                {
                    var running = WeekendTrackSessions.RunningAt(t, slot, m);
                    if (running == null) continue;
                    Assert.IsTrue(WeekendTrackSessions.IsTrackSession(running.kind),
                                  $"{mine}: '{running.title}' is not a session but was reported on track");
                }
        }
    }

    [Test]
    public void ASessionIsOverAtItsEndMinute()
    {
        var t = WeekendTimetable.Build(RacingSeries.Cup, 4, Track);
        var practice = t.PlayerSession(ActivityKind.Practice);
        Assert.NotNull(practice);

        Assert.NotNull(WeekendTrackSessions.RunningAt(t, practice.slot, practice.EndMinute - 1));
        // Touching end-to-start is not an overlap anywhere else on the sheet, and it is not one here: a
        // signing session booked on the hour the practice ends does not have cars going past it.
        var after = WeekendTrackSessions.RunningAt(t, practice.slot, practice.EndMinute);
        Assert.IsTrue(after == null || after.id != practice.id);
    }

    [Test]
    public void HalfADayWithNoSessionInIt_IsCompletelyCold()
    {
        // A truck driver's Sunday morning: their weekend finished on Friday night and the Cup race is not
        // until the afternoon.
        var t = WeekendTimetable.Build(RacingSeries.Trucks, 4, Track);

        for (int m = WeekendSlots.OpensAt(WeekendSlot.SundayAM); m < WeekendSlots.ClosesAt(WeekendSlot.SundayAM); m += 5)
            Assert.IsNull(WeekendTrackSessions.RunningAt(t, WeekendSlot.SundayAM, m),
                          "something was on track on a morning with no session booked");
    }

    // ------------------------------------------------------------------ the player's seat is not the question

    [Test]
    public void TheTruckPractice_RunsWhicheverChampionshipThePlayerIsIn()
    {
        var truckPractice = WeekendTimetable.PracticeTime(RacingSeries.Trucks);

        foreach (var mine in SeriesCatalog.All)
        {
            var t = WeekendTimetable.Build(mine, 4, Track);
            var running = WeekendTrackSessions.RunningAt(t, truckPractice.slot, truckPractice.startMinute + 10);

            Assert.NotNull(running, $"{mine}: the trucks did not practise");
            Assert.AreEqual(RacingSeries.Trucks, running.series);
            Assert.AreEqual(ActivityKind.Practice, WeekendTrackSessions.SessionKind(running.kind),
                            "a session the player watches is still a practice session");

            // Only the truck driver is in it; for the other two it is a spectate booking, which is exactly
            // the case where cars must be on track with no way for the player to join them.
            if (mine == RacingSeries.Trucks) Assert.IsTrue(running.IsOnTrack);
            else Assert.IsTrue(running.IsSpectate);
        }
    }

    [Test]
    public void SpectateKinds_MapToTheSessionTheyAre()
    {
        Assert.AreEqual(ActivityKind.Practice, WeekendTrackSessions.SessionKind(ActivityKind.SpectatePractice));
        Assert.AreEqual(ActivityKind.Qualifying, WeekendTrackSessions.SessionKind(ActivityKind.SpectateQualifying));
        Assert.AreEqual(ActivityKind.Race, WeekendTrackSessions.SessionKind(ActivityKind.SpectateRace));

        // The player's own kinds pass straight through.
        Assert.AreEqual(ActivityKind.Practice, WeekendTrackSessions.SessionKind(ActivityKind.Practice));
        Assert.AreEqual(ActivityKind.Qualifying, WeekendTrackSessions.SessionKind(ActivityKind.Qualifying));
        Assert.AreEqual(ActivityKind.Race, WeekendTrackSessions.SessionKind(ActivityKind.Race));

        // Nothing that happens in the paddock is a session.
        Assert.IsFalse(WeekendTrackSessions.IsTrackSession(ActivityKind.SponsorDuty));
        Assert.IsFalse(WeekendTrackSessions.IsTrackSession(ActivityKind.PressConference));
        Assert.IsFalse(WeekendTrackSessions.IsTrackSession(ActivityKind.DriversMeeting));
        Assert.IsFalse(WeekendTrackSessions.IsTrackSession(ActivityKind.Autographs));
        Assert.IsFalse(WeekendTrackSessions.IsTrackSession(ActivityKind.Rest));
    }

    // ------------------------------------------------------------------ authored weekends

    [Test]
    public void AnAuthoredSession_IsReadWhereItWasPut()
    {
        // A hand-written weekend can move a session anywhere; the answer comes off the sheet, not off the
        // generated session times, so it moves with it.
        var t = new WeekendTimetable { playerSeries = RacingSeries.Cup, weekendId = 1, trackName = "Nowhere" };
        t.AddAuthored(WeekendSlot.SaturdayAM, 8 * 60 + 30, 45, ActivityKind.SpectatePractice, RacingSeries.Trucks);
        t.AddAuthored(WeekendSlot.SaturdayAM, 10 * 60, 60, ActivityKind.Practice, RacingSeries.Cup);

        Assert.IsNull(WeekendTrackSessions.RunningAt(t, WeekendSlot.SaturdayAM, 8 * 60));

        var trucks = WeekendTrackSessions.RunningAt(t, WeekendSlot.SaturdayAM, 9 * 60);
        Assert.NotNull(trucks);
        Assert.AreEqual(RacingSeries.Trucks, trucks.series);

        Assert.IsNull(WeekendTrackSessions.RunningAt(t, WeekendSlot.SaturdayAM, 9 * 60 + 30));

        var cup = WeekendTrackSessions.RunningAt(t, WeekendSlot.SaturdayAM, 10 * 60 + 30);
        Assert.NotNull(cup);
        Assert.AreEqual(RacingSeries.Cup, cup.series);

        // A different half-day is a different day: the same clock time on Friday has nothing on it.
        Assert.IsNull(WeekendTrackSessions.RunningAt(t, WeekendSlot.FridayAM, 10 * 60 + 30));
    }

    [Test]
    public void ATimetableWithNoSessions_LeavesTheCircuitEmpty()
    {
        var t = new WeekendTimetable { playerSeries = RacingSeries.Cup, weekendId = 1, trackName = "Nowhere" };
        t.AddAuthored(WeekendSlot.FridayAM, 9 * 60, 60, ActivityKind.SponsorDuty, RacingSeries.Cup);

        for (int m = 8 * 60; m < 12 * 60; m += 5)
            Assert.IsNull(WeekendTrackSessions.RunningAt(t, WeekendSlot.FridayAM, m));

        Assert.IsNull(WeekendTrackSessions.RunningAt(null, WeekendSlot.FridayAM, 9 * 60));
    }
}
