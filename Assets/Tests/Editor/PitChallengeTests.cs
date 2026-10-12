using Draftmaster.Weekend;
using NUnit.Framework;

// The sponsor's pit-stop challenge: where it sits on the sheet, and what a time is worth.
public class PitChallengeTests
{
    [Test]
    public void EveryWeekendHasOneChallenge_OnAnEmptyTrack()
    {
        foreach (var mine in SeriesCatalog.All)
        {
            var t = WeekendTimetable.Build(mine, 4, "Charlotte");
            WeekendActivity challenge = null;
            int count = 0;
            foreach (var a in t.Activities)
                if (a.kind == ActivityKind.PitChallenge) { challenge = a; count++; }

            Assert.AreEqual(1, count, $"{mine}: expected one pit-stop challenge");
            Assert.AreEqual(WeekendVenue.PitBox, WeekendVenues.For(challenge.kind));
            Assert.IsFalse(challenge.mandatory, "a bit of fun, not a contract");
            Assert.AreEqual(0, challenge.skipMoneyPenalty);
            Assert.IsFalse(ActivityKinds.IsOnTrack(challenge.kind), "it is run in the paddock scene, not a session reload");

            // The run borrows pit road, so nobody's session can be on track while it is booked.
            foreach (var a in t.Activities)
                if (WeekendTrackSessions.IsTrackSession(a.kind))
                    Assert.IsFalse(a.ClashesWith(challenge), $"{mine}: {a.title} is on track during the challenge");
        }
    }

    [Test]
    public void FasterIsBetter_AndPersonalBestsAreABonus()
    {
        float par = PitChallenge.Par(250f, 7f);
        Assert.Greater(par, PitChallenge.ServiceSeconds);

        var brilliant = PitChallenge.Outcome(par - 1f, par, true, false);
        var quick = PitChallenge.Outcome(par + 1f, par, true, false);
        var slow = PitChallenge.Outcome(par + 20f, par, true, false);
        var missed = PitChallenge.Outcome(0f, par, false, false);

        Assert.Greater(brilliant.fanAppeal, quick.fanAppeal);
        Assert.Greater(quick.fanAppeal, slow.fanAppeal);
        Assert.Greater(brilliant.sponsorMood, quick.sponsorMood);
        Assert.Greater(quick.sponsorMood, slow.sponsorMood);
        Assert.LessOrEqual(missed.sponsorMood, 0f, "no time is no good to the sponsor");
        Assert.AreEqual(0, missed.statCount);

        var best = PitChallenge.Outcome(par + 1f, par, true, true);
        Assert.Greater(best.fanAppeal, quick.fanAppeal);
        StringAssert.Contains("best", best.headline);
    }

    [Test]
    public void ParGrowsWithTheRunIn()
    {
        Assert.Greater(PitChallenge.Par(400f, 7f), PitChallenge.Par(150f, 7f));
        Assert.AreEqual(PitChallenge.Grade.Missed, PitChallenge.GradeFor(10f, 20f, false));
        Assert.AreEqual(PitChallenge.Grade.Brilliant, PitChallenge.GradeFor(19f, 20f, true));
        Assert.AreEqual(PitChallenge.Grade.Slow, PitChallenge.GradeFor(40f, 20f, true));
    }

    [Test]
    public void TheIntroEndsOnOneAnswer()
    {
        var talk = PitChallenge.Intro(new WeekendActivity { id = "pc@test", kind = ActivityKind.PitChallenge });
        Assert.AreEqual(1, talk.beats.Count);
        Assert.AreEqual(1, talk.beats[0].choices.Count);
    }
}
