using System.Text;
using System.Text.RegularExpressions;
using Draftmaster.Weekend;
using NUnit.Framework;

// The first weekend of a career books one thing no later weekend does: fifteen minutes at the pit box being
// shown the phone. Everything the on-foot half of the game asks the player to keep track of - what is on
// today, the jobs they have taken on around the paddock, and which of those are finished and waiting to be
// handed back - lives on that phone, and this is the only place in the game that says so out loud.
//
// So these tests are about the two ways that can quietly break: the booking disappearing off the first
// morning (or turning up on every morning after it), and the conversation drifting until it no longer names
// the key or the lists it exists to point at.
public class WeekendOrientationTests
{
    const string Track = "Watkins Glen";

    static WeekendActivity OrientationIn(WeekendTimetable t)
    {
        WeekendActivity found = null;
        foreach (var a in t.Activities)
            if (a.kind == ActivityKind.Orientation)
            {
                Assert.IsNull(found, "The orientation is booked twice on one weekend.");
                found = a;
            }
        return found;
    }

    // ------------------------------------------------------------------ where it sits on the sheet

    [Test]
    public void TheFirstWeekend_BooksThePhoneOrientation_WhicheverSeriesYouAreIn()
    {
        foreach (var series in SeriesCatalog.All)
        {
            var t = WeekendTimetable.Build(series, 0, Track);
            var a = OrientationIn(t);

            Assert.IsNotNull(a, $"{series}: a new career's first weekend never explains the phone.");
            Assert.AreEqual(WeekendSlot.FridayAM, a.slot,
                            $"{series}: the orientation has to be on the first morning to be of any use.");
        }
    }

    [Test]
    public void LaterWeekends_DoNotShowYouThePhoneAgain()
    {
        for (int weekend = 1; weekend < 12; weekend++)
            foreach (var series in SeriesCatalog.All)
                Assert.IsNull(OrientationIn(WeekendTimetable.Build(series, weekend, Track)),
                              $"{series}: weekend {weekend} is still running the rookie orientation.");
    }

    // The one window on the first morning that costs nothing to take. If something moves into it, the
    // tutorial starts trading against a paid appearance, which is not a choice a new player can make.
    [Test]
    public void ItTakesTheGapNothingElseWants()
    {
        foreach (var series in SeriesCatalog.All)
        {
            var t = WeekendTimetable.Build(series, 0, Track);
            var a = OrientationIn(t);

            CollectionAssert.IsEmpty(t.ClashesFor(a),
                                     $"{series}: the orientation now overlaps something else on Friday morning.");
            Assert.GreaterOrEqual(a.startMinute, WeekendSlots.OpensAt(a.slot));
            Assert.LessOrEqual(a.EndMinute, WeekendSlots.ClosesAt(a.slot));

            // Finishing a booking walks the clock to its end and sweeps up anything it stepped over, so
            // nothing may start inside the orientation's window either.
            foreach (var other in t.InSlot(a.slot))
                if (!ReferenceEquals(other, a))
                    Assert.IsFalse(other.startMinute > a.startMinute && other.startMinute < a.EndMinute,
                                   $"{series}: '{other.title}' starts inside the orientation and would be " +
                                   "marked missed by doing it.");
        }
    }

    [Test]
    public void MissingIt_CostsNothing()
    {
        var a = OrientationIn(WeekendTimetable.Build(RacingSeries.Cup, 0, Track));

        Assert.IsFalse(a.mandatory, "Nobody should be fined for skipping the thing that explains the game.");
        Assert.AreEqual(0, a.skipMoneyPenalty);
        Assert.AreEqual(0f, a.skipAppealPenalty);
    }

    [Test]
    public void ItIsSomewhereThePlayerWalksTo()
    {
        var venue = WeekendVenues.For(ActivityKind.Orientation);
        Assert.AreNotEqual(WeekendVenue.None, venue, "The orientation has nowhere to happen.");

        var a = OrientationIn(WeekendTimetable.Build(RacingSeries.National, 0, Track));
        Assert.AreEqual(WeekendVenues.ShortLabel(venue), a.location,
                        "The sheet names a different place from the one the marker sends the player to.");
    }

    // ------------------------------------------------------------------ what is actually said

    static string Spoken(WeekendConversation c)
    {
        var sb = new StringBuilder();
        foreach (var line in c.greeting ?? new string[0]) sb.AppendLine(line);
        foreach (var beat in c.beats)
        {
            foreach (var line in beat.preamble ?? new string[0]) sb.AppendLine(line);
            sb.AppendLine(beat.line);
            sb.AppendLine(beat.Question);
            foreach (var choice in beat.choices) { sb.AppendLine(choice.text); sb.AppendLine(choice.response); }
        }
        foreach (var line in c.farewell ?? new string[0]) sb.AppendLine(line);
        return sb.ToString();
    }

    [Test]
    public void ItIsAConversationSomebodyCanActuallyHave()
    {
        var a = OrientationIn(WeekendTimetable.Build(RacingSeries.Cup, 0, Track));
        var c = OrientationContent.Build(a);

        Assert.IsNotEmpty(c.beats);
        Assert.IsNotNull(c.headline, "No wrap-up line, so the result card would come back blank.");

        foreach (var beat in c.beats)
        {
            Assert.IsNotEmpty(beat.speaker, "A beat has nobody speaking it.");
            Assert.IsNotEmpty(beat.Question, "A beat asks nothing, so the choice list has no header.");
            Assert.GreaterOrEqual(beat.choices.Count, 2, $"'{beat.Question}' is not a choice.");

            bool anyMoves = false;
            foreach (var choice in beat.choices)
            {
                Assert.IsNotEmpty(choice.text);
                Assert.IsNotEmpty(choice.response, $"Nobody replies to '{choice.text}'.");
                if (choice.morale != 0f || choice.setup != 0f || choice.appeal != 0f) anyMoves = true;
            }
            Assert.IsTrue(anyMoves, $"Nothing offered in answer to '{beat.Question}' changes anything.");
        }
    }

    // The whole point of the booking. If the lines stop naming the key or the two lists, it has become a
    // pleasant chat with the crew chief and the player still cannot find their jobs.
    [Test]
    public void ItNamesTheKey_AndTheListsWorthOpening()
    {
        var a = OrientationIn(WeekendTimetable.Build(RacingSeries.Cup, 0, Track));
        string said = Spoken(OrientationContent.Build(a));

        Assert.IsTrue(Regex.IsMatch(said, @"\bP\b"),
                      "The orientation never tells the player which key opens the phone.");
        StringAssert.Contains("TASKS", said, "The orientation never names the list of outstanding jobs.");
        StringAssert.Contains("NOTES", said, "The orientation never names the log of who asked for what.");
    }

    // The key is passed in from the runtime (WeekendScripts reads it off PhoneUI) precisely so a rebound
    // toggle cannot leave this conversation telling the player to press a key that does nothing.
    [Test]
    public void ItSaysWhicheverKeyThePhoneIsActuallyBoundTo()
    {
        var a = OrientationIn(WeekendTimetable.Build(RacingSeries.Cup, 0, Track));
        string said = Spoken(OrientationContent.Build(a, "K"));

        Assert.IsTrue(Regex.IsMatch(said, @"\bK\b"), "A rebound phone key never reaches the lines.");
        Assert.IsFalse(Regex.IsMatch(said, @"\bP\b"), "The default key is still hard-coded into the lines.");
    }

    // The result card is the last thing said about it, and it is the line a player is most likely to
    // actually read, so it carries the summary too.
    [Test]
    public void TheWrapUpRepeatsTheKey()
    {
        var a = OrientationIn(WeekendTimetable.Build(RacingSeries.Cup, 0, Track));
        var c = OrientationContent.Build(a);

        var outcome = WeekendOutcome.Nothing;
        outcome.score = 1f;
        Assert.IsTrue(Regex.IsMatch(c.headline(outcome), @"\bP\b"));

        outcome.score = 0f;
        Assert.IsTrue(Regex.IsMatch(c.headline(outcome), @"\bP\b"),
                      "A badly answered orientation says nothing useful.");
    }
}
