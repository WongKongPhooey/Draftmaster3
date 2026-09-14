using System;
using System.Collections.Generic;
using Draftmaster.Chatter;
using NUnit.Framework;

// The paddock's one-liners now know what the circuit is doing.
//
// Fan appeal already decided the crowd's TONE (DialogueNameTests covers that); this covers the SUBJECT —
// that each area with a session has something different to say during setup, practice, qualifying and the
// race, that the session lines sit alongside the mood lines rather than replacing them, and that the whole
// lot still reads as English once the name tokens are filled in.
public class ChatterTopicTests
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

    static readonly ChatterTopic[] RealTopics =
    {
        ChatterTopic.Idle, ChatterTopic.Practice, ChatterTopic.Qualifying, ChatterTopic.Race,
    };

    // ---------------------------------------------------------------- the pools exist and differ

    [Test]
    public void ThePaddockAndThePitLaneHaveSomethingToSayAboutEverySession()
    {
        foreach (var area in new[] { ChatterArea.Paddock, ChatterArea.PitLane })
            foreach (var topic in RealTopics)
                Assert.Greater(AmbientChatter.Topical(area, topic).Length, 2,
                               $"{area} has nothing to say during {topic}");
    }

    [Test]
    public void NoTwoSessionsSoundTheSame()
    {
        // The whole point: a crowd that says the same things on Friday morning as it does on the grid on
        // Sunday has not been given a session at all.
        foreach (var area in new[] { ChatterArea.Paddock, ChatterArea.PitLane })
            for (int i = 0; i < RealTopics.Length; i++)
                for (int j = i + 1; j < RealTopics.Length; j++)
                {
                    var a = new HashSet<string>(AmbientChatter.Topical(area, RealTopics[i]));
                    a.IntersectWith(AmbientChatter.Topical(area, RealTopics[j]));
                    CollectionAssert.IsEmpty(a,
                        $"{area} says the same thing during {RealTopics[i]} and {RealTopics[j]}");
                }
    }

    [Test]
    public void TheTeamsOwnGarageHasNoSessionAndFallsBackToItsMoodLines()
    {
        // ChatterArea.Garage is the team's shop, which is somewhere else entirely — there is no session
        // running there to talk about.
        foreach (var topic in RealTopics)
            CollectionAssert.IsEmpty(AmbientChatter.Topical(ChatterArea.Garage, topic));
    }

    [Test]
    public void AskingForNoTopicGetsNoSessionPool()
    {
        foreach (ChatterArea area in Enum.GetValues(typeof(ChatterArea)))
            CollectionAssert.IsEmpty(AmbientChatter.Topical(area, ChatterTopic.None));
    }

    // ---------------------------------------------------------------- how they are picked

    [Test]
    public void ATopiclessPickIsExactlyTheOldBehaviour()
    {
        // The topic-less overload is still what a caller with nothing to report about the session gets,
        // and it must not have moved: other tests pin its exact output for a given seed.
        for (int seed = 0; seed < 50; seed++)
            Assert.AreEqual(AmbientChatter.Pick(ChatterArea.Paddock, ChatterMood.Neutral, seed),
                            AmbientChatter.Pick(ChatterArea.Paddock, ChatterMood.Neutral,
                                                ChatterTopic.None, seed));
    }

    [Test]
    public void TheCrowdSplitsItsTimeBetweenTheSessionAndItsOpinionOfYou()
    {
        // Half and half, roughly. All session lines and the crowd turns into a public-address system; no
        // session lines and nothing has changed.
        var mood = new HashSet<string>();
        foreach (ChatterMood m in Enum.GetValues(typeof(ChatterMood)))
            foreach (var line in AmbientChatter.BuiltIn(ChatterArea.Paddock, m))
                mood.Add(SpeakerIdentity.Fill(line));

        int fromSession = 0;
        const int samples = 400;
        for (int seed = 0; seed < samples; seed++)
        {
            string line = AmbientChatter.Pick(ChatterArea.Paddock, ChatterMood.Neutral,
                                              ChatterTopic.Race, seed);
            if (!mood.Contains(line)) fromSession++;
        }

        Assert.Greater(fromSession, samples * 0.3f, "hardly any lines are about the session");
        Assert.Less(fromSession, samples * 0.7f, "the crowd has stopped reacting to the player at all");
    }

    [Test]
    public void EverySessionIsAudibleInTheCrowd()
    {
        // Whatever the split, a walk through the paddock during a given session has to actually produce
        // that session's lines rather than only ever rolling the mood pool.
        foreach (var topic in RealTopics)
        {
            var pool = new HashSet<string>();
            foreach (var line in AmbientChatter.Topical(ChatterArea.Paddock, topic))
                pool.Add(SpeakerIdentity.Fill(line));

            bool heard = false;
            for (int seed = 0; seed < 200 && !heard; seed++)
                heard = pool.Contains(AmbientChatter.Pick(ChatterArea.Paddock, ChatterMood.Neutral,
                                                          topic, seed));

            Assert.IsTrue(heard, $"never heard a {topic} line in 200 walk-pasts");
        }
    }

    [Test]
    public void APickIsStillReproducibleFromItsSeed()
    {
        for (int seed = 0; seed < 20; seed++)
            Assert.AreEqual(AmbientChatter.Pick(ChatterArea.PitLane, ChatterMood.Impressed,
                                                ChatterTopic.Qualifying, seed),
                            AmbientChatter.Pick(ChatterArea.PitLane, ChatterMood.Impressed,
                                                ChatterTopic.Qualifying, seed));
    }

    [Test]
    public void TheSameSpeakerDoesNotRepeatItselfBackToBack()
    {
        string first = AmbientChatter.Pick(ChatterArea.Paddock, ChatterMood.Neutral, ChatterTopic.Race, 11);
        string second = AmbientChatter.Pick(ChatterArea.Paddock, ChatterMood.Neutral, ChatterTopic.Race,
                                            11, first);
        Assert.AreNotEqual(first, second);
    }

    // ---------------------------------------------------------------- the content itself

    [Test]
    public void EverySessionLineFillsCleanlyAndReadsAsEnglish()
    {
        SpeakerIdentity.PlayerNameProvider = () => "Kyle Larson";
        SpeakerIdentity.CrewChiefNameProvider = () => "Ron Doyle";

        foreach (var line in AllSessionLines())
        {
            string filled = SpeakerIdentity.Fill(line);
            Assert.IsFalse(filled.Contains("{"), $"unfilled token left in \"{line}\"");
            Assert.IsFalse(string.IsNullOrWhiteSpace(filled), "an empty session line");
            Assert.IsFalse(filled.Contains(".."), $"double punctuation in \"{filled}\"");
        }
    }

    [Test]
    public void SessionLinesStillFillWhenNobodyHasBeenNamedYet()
    {
        // Before a career exists — an exhibition race, the first seconds of a demo — the providers are not
        // set. A bark must still be a sentence rather than a line of template.
        SpeakerIdentity.Reset();
        foreach (var line in AllSessionLines())
            Assert.IsFalse(SpeakerIdentity.Fill(line).Contains("{"),
                           $"\"{line}\" needs a name that isn't known yet");
    }

    static IEnumerable<string> AllSessionLines()
    {
        foreach (ChatterArea area in Enum.GetValues(typeof(ChatterArea)))
            foreach (var topic in RealTopics)
                foreach (var line in AmbientChatter.Topical(area, topic))
                    yield return line;
    }
}
