using System.Collections.Generic;
using Draftmaster.Chatter;
using NUnit.Framework;

// The odd shout from somebody jumping out of the scooter's way (CartDodge -> DodgeChatter). Covers that only
// some of them speak, that the line choice is reproducible and does not repeat itself, and that the name
// tokens are filled — "Hey, was that {playerlast}?" going out with its braces in would be worse than silence.
public class DodgeChatterTests
{
    [SetUp] public void SetUp() => SpeakerIdentity.Reset();
    [TearDown] public void TearDown() => SpeakerIdentity.Reset();

    [Test]
    public void OnlySomeDodgesGetALine()
    {
        int spoke = 0;
        const int n = 4000;
        for (int seed = 0; seed < n; seed++)
            if (DodgeChatter.ShouldSpeak(seed)) spoke++;

        float share = spoke / (float)n;
        Assert.That(share, Is.InRange(DodgeChatter.SpeakChance - 0.05f, DodgeChatter.SpeakChance + 0.05f),
                    $"{share:P0} of dodges spoke; expected about {DodgeChatter.SpeakChance:P0}");
    }

    [Test]
    public void ChanceExtremesAreAbsolute()
    {
        for (int seed = -50; seed < 50; seed++)
        {
            Assert.IsFalse(DodgeChatter.ShouldSpeak(seed, 0f));
            Assert.IsTrue(DodgeChatter.ShouldSpeak(seed, 1f));
        }
    }

    [Test]
    public void SameSeedSameLine()
    {
        for (int seed = -20; seed < 20; seed++)
            Assert.AreEqual(DodgeChatter.Pick(seed), DodgeChatter.Pick(seed));
    }

    [Test]
    public void NeverRepeatsTheLastLine()
    {
        for (int seed = -200; seed < 200; seed++)
        {
            string first = DodgeChatter.Pick(seed);
            Assert.AreNotEqual(first, DodgeChatter.Pick(seed, first));
        }
    }

    [Test]
    public void EveryLineGetsPickedEventually()
    {
        var seen = new HashSet<string>();
        for (int seed = 0; seed < 2000; seed++) seen.Add(DodgeChatter.Pick(seed));
        Assert.AreEqual(DodgeChatter.All.Length, seen.Count);
    }

    [Test]
    public void NamesAreFilledIn()
    {
        SpeakerIdentity.PlayerNameProvider = () => "Ace Emerson";
        bool sawSurname = false;
        for (int seed = 0; seed < 2000; seed++)
        {
            string line = DodgeChatter.Pick(seed);
            StringAssert.DoesNotContain("{", line, $"unfilled token in \"{line}\"");
            if (line == "Hey, was that Emerson?") sawSurname = true;
        }
        Assert.IsTrue(sawSurname, "the \"was that <surname>?\" line never came out filled");
    }

    [Test]
    public void NoNameStillReadsAsEnglish()
    {
        // No career, no roster: the fallbacks are spoken instead of braces.
        for (int seed = 0; seed < 2000; seed++)
            StringAssert.DoesNotContain("{", DodgeChatter.Pick(seed));
    }
}
