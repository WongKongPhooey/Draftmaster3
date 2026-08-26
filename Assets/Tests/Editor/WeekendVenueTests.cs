using System.Collections.Generic;
using Draftmaster.Weekend;
using NUnit.Framework;

// The weekend happens in places now, and this is the half of that which can be checked without a scene:
// every booking knows where it is kept, the sheet says the same place the player will be sent to, and every
// obligation is a conversation somebody can actually have — beats with answers, answers with replies, and
// arithmetic that adds up to the outcome the ledger is handed.
//
// The other half — that the place exists in the world and that walking there starts it — is
// WeekendVenuePresenceTests in Assets/Tests/PlayMode.
public class WeekendVenueTests
{
    static readonly ActivityKind[] AllKinds = (ActivityKind[])System.Enum.GetValues(typeof(ActivityKind));

    // The player's own sessions load the race scene; everything else is somewhere you walk to. A kind that
    // is neither is a booking the player can commit to and then be given nowhere to go.
    [Test]
    public void EveryBookingIsEitherDrivenOrSomewhereYouWalkTo()
    {
        foreach (var kind in AllKinds)
        {
            var venue = WeekendVenues.For(kind);
            if (ActivityKinds.IsOnTrack(kind) || kind == ActivityKind.Rest)
            {
                Assert.AreEqual(WeekendVenue.None, venue, $"{kind} is driven or skipped; it should not have a venue.");
                continue;
            }

            Assert.AreNotEqual(WeekendVenue.None, venue,
                               $"{kind} has nowhere to happen — committing to it would leave the player with no " +
                               "objective and no way to do it.");
        }
    }

    // Every venue names itself three ways: on the sheet, on the marker, and in a sentence. A venue that
    // falls through to the default reads as "the track", which is where the player already is.
    [Test]
    public void EveryVenueSaysWhereItIs()
    {
        foreach (WeekendVenue venue in System.Enum.GetValues(typeof(WeekendVenue)))
        {
            if (venue == WeekendVenue.None) continue;

            Assert.AreNotEqual("the track", WeekendVenues.Label(venue), $"{venue} has no label of its own.");
            Assert.AreNotEqual("Track", WeekendVenues.ShortLabel(venue), $"{venue} has no column label of its own.");
            Assert.AreNotEqual("Head to the track", WeekendVenues.Directions(venue), $"{venue} has no directions.");
        }
    }

    // The sheet's location column is what the player reads before committing; the objective marker is what
    // they follow afterwards. If those two disagree the schedule is lying about where the weekend is.
    [Test]
    public void TheSheetNamesThePlaceThePlayerIsSentTo()
    {
        foreach (var series in SeriesCatalog.All)
        {
            var timetable = WeekendTimetable.Build(series, 3, "Watkins Glen");
            foreach (var a in timetable.Activities)
            {
                var venue = WeekendVenues.For(a.kind);
                if (venue == WeekendVenue.None) continue;

                Assert.AreEqual(WeekendVenues.ShortLabel(venue), a.location,
                                $"'{a.title}' is booked at {a.location} and would send the player to " +
                                $"{WeekendVenues.ShortLabel(venue)}.");
            }
        }
    }

    // ------------------------------------------------------------------ the conversations

    static IEnumerable<WeekendConversation> EveryScript()
    {
        var timetable = WeekendTimetable.Build(RacingSeries.Cup, 5, "Watkins Glen");
        WeekendLedger.Timetable = timetable;

        foreach (var a in timetable.Activities)
        {
            WeekendConversation script = a.kind switch
            {
                ActivityKind.TeamBriefing or ActivityKind.Debrief => TeamMeetingContent.Build(a),
                ActivityKind.DriversMeeting or ActivityKind.DriverIntros => CeremonyContent.Build(a),
                ActivityKind.SponsorDuty or ActivityKind.PhotoShoot => SponsorContent.Build(a, "Cutler Fuels"),
                ActivityKind.Autographs or ActivityKind.HaulerParade => SigningContent.Build(a),
                _ => null,
            };
            if (script != null) yield return script;
        }
    }

    // A beat the player cannot answer is a conversation that stops dead in front of them.
    [Test]
    public void EveryObligationCanBeTalkedThrough()
    {
        int scripts = 0;
        foreach (var script in EveryScript())
        {
            scripts++;
            Assert.IsNotEmpty(script.beats, "An obligation was built with nothing to say.");
            Assert.IsNotNull(script.headline, "An obligation has no wrap-up line, so its result card would be blank.");

            foreach (var beat in script.beats)
            {
                Assert.IsNotEmpty(beat.speaker, "A beat has nobody speaking it.");
                Assert.IsNotEmpty(beat.Question, "A beat asks nothing, so the choice list has no header.");
                Assert.GreaterOrEqual(beat.choices.Count, 2,
                                      $"'{beat.Question}' offers fewer than two answers — that is not a choice.");

                foreach (var choice in beat.choices)
                {
                    Assert.IsNotEmpty(choice.text, "An answer has no text.");
                    Assert.IsNotEmpty(choice.response, $"Nobody replies to '{choice.text}'.");
                }
            }
        }

        Assert.Greater(scripts, 3, "A Cup weekend should book several obligations that are conversations.");
    }

    // Every obligation has to be worth something in at least one direction, or it is an errand.
    [Test]
    public void EveryAnswerMovesSomething()
    {
        foreach (var script in EveryScript())
            foreach (var beat in script.beats)
            {
                bool anyMoves = false;
                foreach (var c in beat.choices)
                    if (c.setup != 0f || c.morale != 0f || c.media != 0f || c.sponsor != 0f
                        || c.appeal != 0f || c.money != 0 || c.statCount != 0) { anyMoves = true; break; }

                Assert.IsTrue(anyMoves, $"Nothing offered in answer to '{beat.Question}' changes anything.");
            }
    }

    // The arithmetic the venue host runs: answers fold in, the grade is the average of what was answered,
    // and the career counter is either counted per answer or once for the whole thing.
    [Test]
    public void AnswersAddUpIntoTheOutcome()
    {
        var script = new WeekendConversation { statKey = "teammeetings", statCount = 1 };
        script.headline = o => "done";

        var running = WeekendOutcome.Nothing;
        running.score = 0f;

        WeekendConversation.Accumulate(ref running, WeekendConversation.Say("a", "b", setup: 0.1f, morale: 5f, score: 1f));
        WeekendConversation.Accumulate(ref running, WeekendConversation.Say("c", "d", sponsor: -4f, score: 0f));

        var settled = script.Settle(running, 2);

        Assert.AreEqual(0.1f, settled.setupGain, 1e-4f);
        Assert.AreEqual(5f, settled.teamMorale, 1e-4f);
        Assert.AreEqual(-4f, settled.sponsorMood, 1e-4f);
        Assert.AreEqual(0.5f, settled.score, 1e-4f, "The grade is the average of the answers given.");
        Assert.AreEqual("teammeetings", settled.statKey);
        Assert.AreEqual(1, settled.statCount, "Nothing counted itself, so the obligation counts once.");
        Assert.AreEqual("done", settled.headline);
    }

    // A signing session counts signatures, not sessions: each fan served adds one.
    [Test]
    public void AnswersThatCountThemselvesAreNotOverwritten()
    {
        var script = new WeekendConversation { statKey = "autographs", statCount = 1 };

        var running = WeekendOutcome.Nothing;
        running.score = 0f;
        for (int i = 0; i < 3; i++)
            WeekendConversation.Accumulate(ref running, WeekendConversation.Say("sign", "thanks", appeal: 1f, statCount: 1));

        var settled = script.Settle(running, 3);
        Assert.AreEqual(3, settled.statCount, "Three signed should count as three, not as one signing session.");
    }

    // The signing queue is seeded off the booking, so walking away and coming back cannot re-roll a better
    // hour out of it.
    [Test]
    public void TheSigningQueueIsTheSameQueueEveryTime()
    {
        var timetable = WeekendTimetable.Build(RacingSeries.Cup, 9, "Watkins Glen");
        WeekendLedger.Timetable = timetable;

        WeekendActivity signing = null;
        foreach (var a in timetable.Activities)
            if (a.kind == ActivityKind.Autographs || a.kind == ActivityKind.HaulerParade) { signing = a; break; }
        if (signing == null) Assert.Ignore("This weekend booked no signing session.");

        var first = SigningContent.Build(signing);
        var second = SigningContent.Build(signing);

        Assert.AreEqual(first.beats.Count, second.beats.Count);
        for (int i = 0; i < first.beats.Count; i++)
            Assert.AreEqual(first.beats[i].speaker, second.beats[i].speaker,
                            "The queue re-rolled — the same booking must bring the same faces.");
    }
}
