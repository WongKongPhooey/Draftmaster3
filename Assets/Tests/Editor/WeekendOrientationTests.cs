using System.Text;
using System.Text.RegularExpressions;
using Draftmaster.Weekend;
using NUnit.Framework;

// The rookie orientation — fifteen minutes at the pit box being shown the phone — is no longer booked by the
// generated schedule; it was taken off the first weekend on purpose. The booking kind and its conversation
// are kept so a hand-authored plan can still use "team-orientation".
//
// So these tests pin that the generator stays clean of it, and that the conversation, if a plan does book
// it, still names the key and the lists it exists to point at.
public class WeekendOrientationTests
{
    const string Track = "Watkins Glen";

    // What a plan file's "team-orientation" booking produces: the catalogue's defaults on Friday morning.
    static WeekendActivity Orientation() => new WeekendActivity
    {
        id = "team-orientation",
        kind = ActivityKind.Orientation,
        slot = WeekendSlot.FridayAM,
        series = RacingSeries.Cup,
        title = "ROOKIE ORIENTATION",
        location = "Pit box",
        startMinute = 9 * 60 + 30,
        minutes = 15,
    };

    // ------------------------------------------------------------------ the sheet

    [Test]
    public void NoGeneratedWeekend_BooksTheOrientation()
    {
        for (int weekend = 0; weekend < 12; weekend++)
            foreach (var series in SeriesCatalog.All)
                foreach (var a in WeekendTimetable.Build(series, weekend, Track).Activities)
                    Assert.AreNotEqual(ActivityKind.Orientation, a.kind,
                                       $"{series}: weekend {weekend} is running the rookie orientation again.");
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
        var a = Orientation();
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
        var a = Orientation();
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
        var a = Orientation();
        string said = Spoken(OrientationContent.Build(a, "K"));

        Assert.IsTrue(Regex.IsMatch(said, @"\bK\b"), "A rebound phone key never reaches the lines.");
        Assert.IsFalse(Regex.IsMatch(said, @"\bP\b"), "The default key is still hard-coded into the lines.");
    }

    // Somebody holding a pad is told the pad's buttons. The briefing used to name arrows, E and Esc whatever
    // was in the player's hands.
    [Test]
    public void OnAPad_ItNamesThePadsButtons_AndNoKeys()
    {
        var a = Orientation();
        var words = new OrientationContent.PhoneWords
        {
            move = "The d-pad", open = "A", back = "B", sheet = "d-pad down",
        };
        string said = Spoken(OrientationContent.Build(a, "VIEW", words));

        StringAssert.Contains("VIEW", said, "The pad's phone button never reaches the lines.");
        StringAssert.Contains("The d-pad to move", said);
        StringAssert.Contains("A to open one", said);
        StringAssert.Contains("still d-pad down", said);
        foreach (var key in new[] { @"Esc", @"F10", @"Arrows", @"E to" })
            Assert.IsFalse(Regex.IsMatch(said, key), $"A pad player is still told about '{key}'.");
    }

    // A phone has no keys at all: the lines ask for a tap on the phone button, and never say "press".
    [Test]
    public void OnATouchScreen_ItAsksForTaps_AndNamesNoKeys()
    {
        var a = Orientation();
        var words = new OrientationContent.PhoneWords
        {
            move = "Drag", open = "a tap", back = "the arrow", sheet = "the full weekend sheet",
            press = "Tap the phone button, bottom left",
        };
        string said = Spoken(OrientationContent.Build(a, "the phone button", words));

        StringAssert.Contains("Tap the phone button, bottom left", said);
        StringAssert.Contains("The phone button brings it up", said, "A sentence opening on the button lost its capital.");
        StringAssert.Contains("a tap to open one", said);
        foreach (var key in new[] { @"\bPress\b", @"\bEsc\b", @"\bF10\b", @"\bArrows\b", @"\bE to\b", @"THE PHONE BUTTON" })
            Assert.IsFalse(Regex.IsMatch(said, key), $"A touch player is still told '{key}'.");
    }

    // The result card is the last thing said about it, and it is the line a player is most likely to
    // actually read, so it carries the summary too.
    [Test]
    public void TheWrapUpRepeatsTheKey()
    {
        var a = Orientation();
        var c = OrientationContent.Build(a);

        var outcome = WeekendOutcome.Nothing;
        outcome.score = 1f;
        Assert.IsTrue(Regex.IsMatch(c.headline(outcome), @"\bP\b"));

        outcome.score = 0f;
        Assert.IsTrue(Regex.IsMatch(c.headline(outcome), @"\bP\b"),
                      "A badly answered orientation says nothing useful.");
    }
}
