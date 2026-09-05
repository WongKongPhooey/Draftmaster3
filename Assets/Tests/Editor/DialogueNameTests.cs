using System;
using System.Collections.Generic;
using Draftmaster.Chatter;
using Draftmaster.Weekend;
using NUnit.Framework;

// The names the paddock now uses out loud.
//
// SpeakerIdentity is the pure half of that (DialogueNames, which answers WHO those people are, lives in the
// game assembly and cannot be reached from here) so this covers the part that can be reasoned about without
// a scene: the token syntax, the fallbacks, and the fact that every compiled dialogue table still reads as
// English once it has been filled in.
public class DialogueNameTests
{
    Func<ChatterArea, ChatterMood, string[]> _savedProvider;

    [SetUp]
    public void SetUp()
    {
        _savedProvider = AmbientChatter.Provider;
        AmbientChatter.Provider = null;
        SpeakerIdentity.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        AmbientChatter.Provider = _savedProvider;
        SpeakerIdentity.Reset();
    }

    static void NameThem(string player, string chief)
    {
        SpeakerIdentity.PlayerNameProvider = () => player;
        SpeakerIdentity.CrewChiefNameProvider = () => chief;
    }

    // ---------------------------------------------------------------- first names

    [Test]
    public void FirstName_IsTheWordBeforeTheFirstSpace()
    {
        Assert.AreEqual("Kyle", SpeakerIdentity.FirstNameOf("Kyle Larson"));
        Assert.AreEqual("Ron", SpeakerIdentity.FirstNameOf("  Ron Doyle  "));
        Assert.AreEqual("Juan", SpeakerIdentity.FirstNameOf("Juan Pablo Montoya"));
    }

    [Test]
    public void FirstName_OfAOneWordNameIsThatName()
    {
        Assert.AreEqual("Sting", SpeakerIdentity.FirstNameOf("Sting"));
    }

    [Test]
    public void FirstName_OfNothingIsNothing()
    {
        Assert.AreEqual("", SpeakerIdentity.FirstNameOf(null));
        Assert.AreEqual("", SpeakerIdentity.FirstNameOf("   "));
    }

    // ---------------------------------------------------------------- filling

    [Test]
    public void Fill_ReplacesEveryTokenItOwns()
    {
        NameThem("Kyle Larson", "Ron Doyle");

        Assert.AreEqual("Kyle Larson / Kyle / Ron Doyle / Ron",
                        SpeakerIdentity.Fill("{player} / {playerfirst} / {chief} / {chieffirst}"));
    }

    [Test]
    public void Fill_IgnoresTheCaseOfATokenName()
    {
        NameThem("Kyle Larson", "Ron Doyle");

        Assert.AreEqual("Morning, Kyle. Ron wants you.",
                        SpeakerIdentity.Fill("Morning, {PlayerFirst}. {CHIEFFIRST} wants you."));
    }

    [Test]
    public void Fill_LeavesTokensBelongingToOtherSystemsAlone()
    {
        NameThem("Kyle Larson", "Ron Doyle");

        // {team}/{num} are DriverPresenceDirector's and {path} is CareerPathNPC's; both substitute at a
        // different moment, so eating them here would silently blank their lines.
        Assert.AreEqual("{team} in {num}: nice one, Kyle. {path}",
                        SpeakerIdentity.Fill("{team} in {num}: nice one, {playerfirst}. {path}"));
    }

    [Test]
    public void Fill_UsesTheFallbacksWhenNobodyIsNamed()
    {
        // No providers installed at all — a scene with no career and no roster.
        Assert.AreEqual("Nice one, mate. Chief wants you.",
                        SpeakerIdentity.Fill("Nice one, {playerfirst}. {chieffirst} wants you."));
        Assert.AreEqual("the driver / Crew Chief", SpeakerIdentity.Fill("{player} / {chief}"));
    }

    [Test]
    public void Fill_UsesTheFallbacksWhenAProviderAnswersWithNothing()
    {
        NameThem("   ", null);

        Assert.AreEqual("mate", SpeakerIdentity.Fill("{playerfirst}"));
        Assert.AreEqual("Chief", SpeakerIdentity.Fill("{chieffirst}"));
    }

    [Test]
    public void Fill_SurvivesAProviderThatThrows()
    {
        SpeakerIdentity.PlayerNameProvider = () => throw new InvalidOperationException("no database");

        Assert.AreEqual("Morning, mate.", SpeakerIdentity.Fill("Morning, {playerfirst}."));
    }

    [Test]
    public void Fill_ReturnsTheSameStringWhenThereIsNothingToFill()
    {
        NameThem("Kyle Larson", "Ron Doyle");

        const string plain = "Guns are charged. Fuel rig's next.";
        Assert.AreSame(plain, SpeakerIdentity.Fill(plain));
        Assert.AreSame(plain, SpeakerIdentity.Fill(plain), "a line with no braces must never allocate");
    }

    [Test]
    public void Fill_LeavesAnUnclosedBraceWhereItIs()
    {
        NameThem("Kyle Larson", "Ron Doyle");

        Assert.AreEqual("Kyle, what is {this", SpeakerIdentity.Fill("{playerfirst}, what is {this"));
    }

    [Test]
    public void Fill_OfATableOnlyCopiesItWhenSomethingChanged()
    {
        NameThem("Kyle Larson", "Ron Doyle");

        var untouched = new[] { "Wall's live. Eyes up.", "Keep the lane clear, please." };
        Assert.AreSame(untouched, SpeakerIdentity.Fill(untouched));

        var named = new[] { "Wall's live. Eyes up.", "Box is yours, {playerfirst}." };
        var filled = SpeakerIdentity.Fill(named);
        Assert.AreNotSame(named, filled);
        Assert.AreEqual("Box is yours, {playerfirst}.", named[1], "the source table must not be edited");
        Assert.AreEqual("Box is yours, Kyle.", filled[1]);
    }

    // ---------------------------------------------------------------- the ambient crowd

    [Test]
    public void Pick_SpeaksAFilledLine()
    {
        NameThem("Kyle Larson", "Ron Doyle");
        AmbientChatter.Provider = (a, m) => new[] { "Morning, {playerfirst}." };

        Assert.AreEqual("Morning, Kyle.", AmbientChatter.Pick(ChatterArea.Paddock, ChatterMood.Neutral, 1));
    }

    [Test]
    public void Pick_DoesNotRepeatTheLineItJustSpoke()
    {
        NameThem("Kyle Larson", "Ron Doyle");
        AmbientChatter.Provider = (a, m) => new[] { "Morning, {playerfirst}.", "Afternoon, {playerfirst}." };

        string first = AmbientChatter.Pick(ChatterArea.Paddock, ChatterMood.Neutral, 7);
        string second = AmbientChatter.Pick(ChatterArea.Paddock, ChatterMood.Neutral, 7, first);

        Assert.AreNotEqual(first, second,
            "the repeat guard compares what was spoken, so it has to compare filled lines");
    }

    [Test]
    public void EveryBuiltInLineFillsCleanly()
    {
        NameThem("Kyle Larson", "Ron Doyle");

        foreach (var line in AllBuiltInLines())
        {
            string filled = SpeakerIdentity.Fill(line);
            Assert.IsFalse(filled.Contains("{"),
                $"chatter line still has an unfilled token after filling: \"{filled}\"");
        }
    }

    [Test]
    public void TheCrowdKnowsTheDriversNameAndTheCrewKnowsTheChiefs()
    {
        NameThem("Kyle Larson", "Ron Doyle");

        bool namesPlayer = false, namesChief = false;
        foreach (var line in AllBuiltInLines())
        {
            if (line.Contains(SpeakerIdentity.PlayerFirstToken)) namesPlayer = true;
            if (line.Contains(SpeakerIdentity.ChiefFirstToken)) namesChief = true;
        }

        Assert.IsTrue(namesPlayer, "no built-in chatter line calls the driver by their first name");
        Assert.IsTrue(namesChief, "no built-in chatter line calls the crew chief by their first name");
    }

    [Test]
    public void TheChiefIsNamedByTheCrewAreasRatherThanByStrangers()
    {
        // The garage and the pit lane are the team's own ground — those are the people who would use the
        // chief's first name. A stranger in the paddock crowd calling them "Ron" would be odd.
        Assert.IsTrue(MentionsChief(ChatterArea.Garage), "nobody in the garage names the crew chief");
        Assert.IsTrue(MentionsChief(ChatterArea.PitLane), "nobody in the pit lane names the crew chief");
    }

    static bool MentionsChief(ChatterArea area)
    {
        foreach (ChatterMood mood in Enum.GetValues(typeof(ChatterMood)))
            foreach (var line in AmbientChatter.BuiltIn(area, mood))
                if (line.Contains(SpeakerIdentity.ChiefFirstToken)) return true;
        return false;
    }

    static IEnumerable<string> AllBuiltInLines()
    {
        foreach (ChatterArea area in Enum.GetValues(typeof(ChatterArea)))
            foreach (ChatterMood mood in Enum.GetValues(typeof(ChatterMood)))
                foreach (var line in AmbientChatter.BuiltIn(area, mood))
                    yield return line;
    }

    // ---------------------------------------------------------------- the weekend's own content

    [Test]
    public void EveryVenueIdleLineFillsCleanly()
    {
        NameThem("Kyle Larson", "Ron Doyle");

        foreach (var host in WeekendVenueCast.All)
            Assert.IsFalse(SpeakerIdentity.Fill(host.idleLine).Contains("{"),
                $"{host.venue}'s idle line still has an unfilled token: \"{host.idleLine}\"");
    }

    [Test]
    public void TheCrewChiefAtTheBoxGreetsTheDriverByName()
    {
        bool named = false;
        foreach (var host in WeekendVenueCast.All)
            if (host.venue == WeekendVenue.PitBox && host.idleLine.Contains(SpeakerIdentity.PlayerFirstToken))
                named = true;

        Assert.IsTrue(named, "the crew chief at the pit box does not use the driver's name");
    }
}
