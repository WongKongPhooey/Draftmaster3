using Draftmaster.Weekend;
using NUnit.Framework;
using UnityEngine;

// An hour in the stand, and how long it actually takes.
//
// The promise the seat makes is that watching somebody else's session is a few minutes, not the hour the
// sheet blocks out for it: the session is held open against a compressed clock at ten weekend minutes a
// minute, hard-capped so nothing ever asks the player to sit for longer than six. The cars are never sped
// up — GrandstandVisit holds the real field open and this decides how long for.
public class GrandstandWatchTests
{
    const float Tolerance = 0.01f;

    [Test]
    public void AnHourSession_IsSixMinutesInTheSeat()
    {
        Assert.AreEqual(360f, GrandstandWatch.WatchSeconds(60), Tolerance);
    }

    [Test]
    public void TenTimesSpeed_IsWhatTheClockRunsAt()
    {
        // Every ordinary session length compresses by exactly the advertised factor.
        foreach (int minutes in new[] { 15, 20, 30, 45, 55 })
            Assert.AreEqual(minutes * 60f / GrandstandWatch.Speed, GrandstandWatch.WatchSeconds(minutes), Tolerance,
                            $"{minutes} minute session");
    }

    [Test]
    public void ALongRace_CompressesHarderRatherThanRunningLong()
    {
        // A two-hour race at a flat 10x would be twelve minutes in a plastic seat. It is capped instead.
        Assert.AreEqual(GrandstandWatch.MaxSeconds, GrandstandWatch.WatchSeconds(120), Tolerance);
        Assert.AreEqual(GrandstandWatch.MaxSeconds, GrandstandWatch.WatchSeconds(240), Tolerance);
    }

    [Test]
    public void AShortSession_StillLastsLongEnoughToBeOne()
    {
        // Arriving and the chequered flag must never be the same beat.
        Assert.AreEqual(GrandstandWatch.MinSeconds, GrandstandWatch.WatchSeconds(1), Tolerance);
        Assert.AreEqual(GrandstandWatch.MinSeconds, GrandstandWatch.WatchSeconds(0), Tolerance);
    }

    [Test]
    public void NoSessionOnAnySheet_KeepsThePlayerSittingPastTheCap()
    {
        // The rule against the real timetables rather than against numbers picked here: whatever a weekend
        // schedules, and however a plan file moves it about, the seat is a few minutes.
        foreach (var series in SeriesCatalog.All)
        {
            var t = WeekendTimetable.Build(series, 3, "Bristol");
            foreach (var a in t.Activities)
            {
                if (!a.IsSpectate) continue;
                float seconds = GrandstandWatch.WatchSeconds(a.minutes);
                Assert.LessOrEqual(seconds, GrandstandWatch.MaxSeconds, $"{a.title} runs too long");
                Assert.GreaterOrEqual(seconds, GrandstandWatch.MinSeconds, $"{a.title} is over before it starts");
            }
        }
    }

    // ------------------------------------------------------------------ the session clock

    [Test]
    public void TheSessionClock_RunsFromGreenToTheFullHour()
    {
        float watch = GrandstandWatch.WatchSeconds(60);

        Assert.AreEqual(0, GrandstandWatch.SessionMinuteAt(0f, 60));
        Assert.AreEqual(30, GrandstandWatch.SessionMinuteAt(watch * 0.5f, 60));
        Assert.AreEqual(60, GrandstandWatch.SessionMinuteAt(watch, 60));
    }

    [Test]
    public void TheSessionClock_NeverRunsPastTheEnd()
    {
        // Sitting there after the flag reads as the full session, not as 90 minutes of a 60 minute one.
        Assert.AreEqual(60, GrandstandWatch.SessionMinuteAt(9999f, 60));
        Assert.AreEqual(0, GrandstandWatch.SessionMinuteAt(-5f, 60));
    }

    [Test]
    public void Progress_IsZeroToOne()
    {
        Assert.AreEqual(0f, GrandstandWatch.Progress01(0f, 100f), Tolerance);
        Assert.AreEqual(0.5f, GrandstandWatch.Progress01(50f, 100f), Tolerance);
        Assert.AreEqual(1f, GrandstandWatch.Progress01(500f, 100f), Tolerance);
        Assert.AreEqual(1f, GrandstandWatch.Progress01(1f, 0f), Tolerance);   // no session = over
    }

    // ------------------------------------------------------------------ the shot

    [Test]
    public void TheFallbackVantage_SitsBetweenTheSeatAndTheRoad()
    {
        var seat = new Vector2(0f, 0f);
        var road = new Vector2(0f, 40f);

        var view = GrandstandWatch.Vantage(seat, road);

        Assert.Greater(view.y, seat.y, "the camera never sits behind the seat");
        Assert.Less(view.y, road.y, "and never out in the middle of the racing surface");
        Assert.AreEqual(40f * GrandstandWatch.DefaultPull01, view.y, Tolerance);
    }

    [Test]
    public void TheFallbackVantage_ClampsItsPull()
    {
        var seat = new Vector2(10f, 10f);
        var road = new Vector2(10f, 50f);

        Assert.AreEqual(seat, GrandstandWatch.Vantage(seat, road, -3f));
        Assert.AreEqual(road, GrandstandWatch.Vantage(seat, road, 4f));
    }

    [Test]
    public void TheFallbackZoom_WidensWithDistanceAndStaysSane()
    {
        Assert.AreEqual(14f, GrandstandWatch.ZoomFor(0f), Tolerance, "a seat on top of the road still frames it");
        Assert.AreEqual(45f, GrandstandWatch.ZoomFor(500f), Tolerance, "and a stand miles away does not pull to orbit");

        float near = GrandstandWatch.ZoomFor(12f);
        float far = GrandstandWatch.ZoomFor(20f);
        Assert.Less(near, far, "further from the road = wider shot");
    }
}
