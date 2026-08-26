using System.Collections.Generic;
using Draftmaster.Weekend;
using NUnit.Framework;

// The weekend's rules, tested where they are pure.
//
// The timetable, the simulated results for the other two championships and the press question bank all live
// in Draftmaster.Weekend precisely so they can be tested without an editor scene, a database or a car. The
// runtime layer (WeekendDirector, the runners, the schedule screen) is deliberately not tested here - it is
// scenes, IMGUI and PlayerPrefs, and it holds no rules of its own.
public class WeekendTimetableTests
{
    // ------------------------------------------------------------------ determinism

    [Test]
    public void Timetable_IsDeterministic_ForTheSameWeekend()
    {
        var a = WeekendTimetable.Build(RacingSeries.Trucks, 7, "Martinsville");
        var b = WeekendTimetable.Build(RacingSeries.Trucks, 7, "Martinsville");

        Assert.AreEqual(a.Activities.Count, b.Activities.Count, "same weekend, different number of bookings");
        for (int i = 0; i < a.Activities.Count; i++)
        {
            Assert.AreEqual(a.Activities[i].id, b.Activities[i].id);
            Assert.AreEqual(a.Activities[i].startMinute, b.Activities[i].startMinute);
            Assert.AreEqual(a.Activities[i].title, b.Activities[i].title);
        }
    }

    [Test]
    public void Timetable_DiffersBetweenWeekends()
    {
        // Not every booking moves, but the rotating feature obligation must, or every weekend reads the same.
        var titles = new HashSet<string>();
        for (int weekend = 0; weekend < 12; weekend++)
        {
            var t = WeekendTimetable.Build(RacingSeries.Cup, weekend, "Daytona");
            foreach (var act in t.Activities) titles.Add(act.title);
        }
        Assert.Greater(titles.Count, 20, "twelve weekends produced almost no variety");
    }

    // ------------------------------------------------------------------ the three championships

    [Test]
    public void PlayerSeriesSessions_AreDrivable_AndTheOthersAreSpectator()
    {
        foreach (var mine in SeriesCatalog.All)
        {
            var t = WeekendTimetable.Build(mine, 3, "Bristol");

            int drivable = 0, spectate = 0;
            foreach (var a in t.Activities)
            {
                if (a.IsOnTrack) { drivable++; Assert.AreEqual(mine, a.series); }
                if (a.IsSpectate) { spectate++; Assert.AreNotEqual(mine, a.series); }
            }

            Assert.AreEqual(3, drivable, $"{mine}: expected practice, qualifying and a race to drive");
            Assert.AreEqual(6, spectate, $"{mine}: expected the other two championships' three sessions each");
        }
    }

    [Test]
    public void EverySeries_HasItsOwnPracticeQualifyingAndRace()
    {
        var t = WeekendTimetable.Build(RacingSeries.National, 1, "Phoenix");
        Assert.IsNotNull(t.PlayerSession(ActivityKind.Practice));
        Assert.IsNotNull(t.PlayerSession(ActivityKind.Qualifying));
        Assert.IsNotNull(t.PlayerSession(ActivityKind.Race));
    }

    [Test]
    public void SessionOrder_IsPracticeThenQualifyingThenRace()
    {
        foreach (var s in SeriesCatalog.All)
        {
            var p = WeekendTimetable.PracticeTime(s);
            var q = WeekendTimetable.QualifyingTime(s);
            var r = WeekendTimetable.RaceTime(s);

            Assert.Less(Absolute(p), Absolute(q), $"{s}: qualifying is not after practice");
            Assert.Less(Absolute(q), Absolute(r), $"{s}: the race is not after qualifying");
        }
    }

    // Minutes from the start of the weekend, so two half-days can be compared.
    static int Absolute(WeekendTimetable.SessionTime t) => (int)t.slot * 1440 + t.startMinute;

    // ------------------------------------------------------------------ the shape of the days

    [Test]
    public void EveryHalfDay_HasSomethingInIt()
    {
        foreach (var mine in SeriesCatalog.All)
        {
            var t = WeekendTimetable.Build(mine, 5, "Talladega");
            foreach (var slot in WeekendSlots.All)
                Assert.IsNotEmpty(t.InSlot(slot), $"{mine}: nothing at all booked on {WeekendSlots.Label(slot)}");
        }
    }

    [Test]
    public void EveryBooking_FitsInsideItsHalfDay()
    {
        foreach (var mine in SeriesCatalog.All)
        {
            var t = WeekendTimetable.Build(mine, 2, "Watkins Glen");
            foreach (var a in t.Activities)
            {
                Assert.GreaterOrEqual(a.startMinute, WeekendSlots.OpensAt(a.slot),
                    $"{a.title} starts before {WeekendSlots.Label(a.slot)} opens");
                Assert.LessOrEqual(a.EndMinute, WeekendSlots.ClosesAt(a.slot),
                    $"{a.title} runs past the end of {WeekendSlots.Label(a.slot)}");
            }
        }
    }

    [Test]
    public void SomeBookingsClash_SoTheScheduleIsAChoice()
    {
        var t = WeekendTimetable.Build(RacingSeries.Trucks, 4, "Richmond");
        int clashing = 0;
        foreach (var a in t.Activities)
            if (t.ClashesFor(a).Count > 0) clashing++;

        Assert.Greater(clashing, 4, "nothing overlaps - the timetable is a list, not a schedule");
    }

    // ------------------------------------------------------------------ race day

    [Test]
    public void DriversMeeting_IsTwoHoursBeforeYourRace_AndIntrosHalfAnHour()
    {
        foreach (var mine in SeriesCatalog.All)
        {
            var t = WeekendTimetable.Build(mine, 6, "Charlotte");
            var race = t.PlayerSession(ActivityKind.Race);
            Assert.IsNotNull(race);

            WeekendActivity meeting = null, intros = null;
            foreach (var a in t.Activities)
            {
                if (a.kind == ActivityKind.DriversMeeting) meeting = a;
                if (a.kind == ActivityKind.DriverIntros) intros = a;
            }

            Assert.IsNotNull(meeting, $"{mine}: no drivers meeting on race day");
            Assert.IsNotNull(intros, $"{mine}: no driver introductions on race day");
            Assert.AreEqual(race.startMinute - 120, meeting.startMinute, $"{mine}: drivers meeting is not two hours before green");
            Assert.AreEqual(race.startMinute - 30, intros.startMinute, $"{mine}: intros are not half an hour before green");
            Assert.IsTrue(meeting.mandatory && intros.mandatory, $"{mine}: race-day ceremony should be mandatory");
        }
    }

    [Test]
    public void SkippingAContractedAppearance_Costs_ButSkippingAFreeOneDoesNot()
    {
        var t = WeekendTimetable.Build(RacingSeries.Cup, 8, "Sonoma");

        bool sawPaidObligation = false, sawOptional = false;
        foreach (var a in t.Activities)
        {
            if (ActivityKinds.IsSponsorDuty(a.kind))
            {
                Assert.IsTrue(a.mandatory, $"{a.title}: a contracted appearance should be an obligation");
                Assert.Greater(a.skipMoneyPenalty, 0, $"{a.title}: no-showing a paid appearance should cost money");
                sawPaidObligation = true;
            }
            if (a.kind == ActivityKind.HaulerParade)
            {
                Assert.IsFalse(a.mandatory, $"{a.title}: goodwill is not contractual");
                sawOptional = true;
            }
        }
        Assert.IsTrue(sawPaidObligation && sawOptional);
    }

    // ------------------------------------------------------------------ clock formatting

    [Test]
    public void Clock_ReadsAsATimetable()
    {
        Assert.AreEqual("08:00", WeekendSlots.Clock(8 * 60));
        Assert.AreEqual("14:30", WeekendSlots.Clock(14 * 60 + 30));
        Assert.AreEqual("14:30 - 16:00", WeekendSlots.ClockRange(14 * 60 + 30, 90));
        Assert.AreEqual("45m", WeekendSlots.Duration(45));
        Assert.AreEqual("2h", WeekendSlots.Duration(120));
        Assert.AreEqual("3h 10m", WeekendSlots.Duration(190));
    }

    [Test]
    public void Clash_IsOverlapNotAdjacency()
    {
        var a = new WeekendActivity { slot = WeekendSlot.FridayPM, startMinute = 13 * 60, minutes = 60 };
        var touching = new WeekendActivity { slot = WeekendSlot.FridayPM, startMinute = 14 * 60, minutes = 30 };
        var overlapping = new WeekendActivity { slot = WeekendSlot.FridayPM, startMinute = 13 * 60 + 30, minutes = 30 };
        var otherDay = new WeekendActivity { slot = WeekendSlot.SaturdayPM, startMinute = 13 * 60, minutes = 60 };

        Assert.IsFalse(a.ClashesWith(touching), "back-to-back bookings should both be doable");
        Assert.IsTrue(a.ClashesWith(overlapping));
        Assert.IsFalse(a.ClashesWith(otherDay));
    }
}
