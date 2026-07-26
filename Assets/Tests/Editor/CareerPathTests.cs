using NUnit.Framework;
using UnityEngine;
using Draftmaster.Fans;
using Draftmaster.Progression;

// EditMode coverage for the career-opening choice (CareerPathNPC's answer): the starting-stat table, the
// persistence of the answer, the "pays out exactly once" rule, and the content gate other NPCs use.
// This is the part of the beat that can't be play-tested with the editor unfocused.
public class CareerPathTests
{
    // Must match CareerPath's private keys / PlayerStatsLedger's prefix.
    const string PathKey = "career.path";
    const string AppliedKey = "career.path.applied";
    const string StatPrefix = "stat.";
    const string AppealKey = "fan.appeal";

    float _appealBefore;
    bool _hadAppeal;

    [SetUp]
    public void Before()
    {
        // The tests write to the real save's PlayerPrefs, so keep fan appeal and put it back afterwards.
        _hadAppeal = PlayerPrefs.HasKey(AppealKey);
        _appealBefore = PlayerPrefs.GetFloat(AppealKey, FanAppeal.Default);
        ClearState();
    }

    [TearDown]
    public void After()
    {
        ClearState();
        if (_hadAppeal) PlayerPrefs.SetFloat(AppealKey, _appealBefore);
        else PlayerPrefs.DeleteKey(AppealKey);
        PlayerPrefs.Save();
    }

    void ClearState()
    {
        PlayerPrefs.DeleteKey(PathKey);
        PlayerPrefs.DeleteKey(AppliedKey);
        foreach (var key in CareerPath.StatKeys) PlayerPrefs.DeleteKey(StatPrefix + key);
        PlayerPrefs.Save();
    }

    [Test]
    public void FreshSave_IsUnchosen()
    {
        Assert.AreEqual(CareerPath.Path.Unchosen, CareerPath.Current);
        Assert.IsFalse(CareerPath.HasChosen);
        Assert.IsFalse(CareerPath.StatsApplied);
    }

    [Test]
    public void Choose_PersistsPathAndPaysStartingStats()
    {
        Assert.IsTrue(CareerPath.Choose(CareerPath.Path.Driver));

        Assert.AreEqual(CareerPath.Path.Driver, CareerPath.Current);
        Assert.IsTrue(CareerPath.HasChosen);
        Assert.IsTrue(CareerPath.StatsApplied);

        foreach (var grant in CareerPath.StartingStats(CareerPath.Path.Driver))
            Assert.AreEqual(grant.value, CareerPath.Stat(grant.key), $"{grant.key} should have been granted");
    }

    [Test]
    public void Choose_IsOncePerSave()
    {
        Assert.IsTrue(CareerPath.Choose(CareerPath.Path.Scout));
        int scouting = CareerPath.Stat(CareerPath.StatScouting);

        Assert.IsFalse(CareerPath.Choose(CareerPath.Path.Driver), "a second answer must be refused");
        Assert.AreEqual(CareerPath.Path.Scout, CareerPath.Current);
        Assert.AreEqual(scouting, CareerPath.Stat(CareerPath.StatScouting), "stats must not be paid twice");
    }

    [Test]
    public void Choose_Unchosen_IsRejected()
    {
        Assert.IsFalse(CareerPath.Choose(CareerPath.Path.Unchosen));
        Assert.AreEqual(CareerPath.Path.Unchosen, CareerPath.Current);
    }

    [Test]
    public void Choose_MovesFanAppealByThePathsBonus()
    {
        FanAppeal.Value = 50f;
        CareerPath.Choose(CareerPath.Path.Driver);
        Assert.AreEqual(50f + CareerPath.StartingFanAppealBonus(CareerPath.Path.Driver), FanAppeal.Value, 1e-3f);
    }

    [Test]
    public void EveryPath_SpendsTheSameBudgetAcrossKnownStats()
    {
        foreach (var path in CareerPath.Choices)
        {
            int total = 0;
            foreach (var grant in CareerPath.StartingStats(path))
            {
                Assert.Contains(grant.key, CareerPath.StatKeys, $"{path} grants an unknown stat key");
                Assert.Greater(grant.value, 0, $"{path} grants a non-positive {grant.key}");
                total += grant.value;
            }
            Assert.AreEqual(CareerPath.StartingStatBudget, total, $"{path} should spend the standard budget");
        }
    }

    [Test]
    public void EachPath_LeadsItsOwnStat()
    {
        Assert.AreEqual(CareerPath.StatPitCraft, Highest(CareerPath.Path.PitCrew));
        Assert.AreEqual(CareerPath.StatDriving, Highest(CareerPath.Path.Driver));
        Assert.AreEqual(CareerPath.StatBusiness, Highest(CareerPath.Path.TeamOwner));
        Assert.AreEqual(CareerPath.StatScouting, Highest(CareerPath.Path.Scout));
    }

    [Test]
    public void Choices_AreTheFourAnswers()
    {
        var choices = CareerPath.Choices;
        Assert.AreEqual(4, choices.Length);
        Assert.Contains(CareerPath.Path.PitCrew, choices);
        Assert.Contains(CareerPath.Path.Driver, choices);
        Assert.Contains(CareerPath.Path.TeamOwner, choices);
        Assert.Contains(CareerPath.Path.Scout, choices);
        foreach (var p in choices)
            Assert.IsNotEmpty(CareerPath.Ambition(p), $"{p} needs a spoken answer");
    }

    [Test]
    public void Allows_EmptyGateMatchesAnything()
    {
        Assert.IsTrue(CareerPath.Allows(null), "no clause = no filter");
        Assert.IsTrue(CareerPath.Allows(new CareerPath.Path[0]));

        CareerPath.Choose(CareerPath.Path.TeamOwner);
        Assert.IsTrue(CareerPath.Allows(null));
    }

    [Test]
    public void Allows_GatesContentByChosenPath()
    {
        var ownersOnly = new[] { CareerPath.Path.TeamOwner };

        Assert.IsFalse(CareerPath.Allows(ownersOnly), "an unasked save doesn't match a specific path");

        CareerPath.Choose(CareerPath.Path.TeamOwner);
        Assert.IsTrue(CareerPath.Allows(ownersOnly));
        Assert.IsFalse(CareerPath.Allows(new[] { CareerPath.Path.Driver, CareerPath.Path.Scout }));
        Assert.IsTrue(CareerPath.Allows(new[] { CareerPath.Path.Driver, CareerPath.Path.TeamOwner }));
    }

    [Test]
    public void Reset_UnanswersTheQuestionAndRefundsStats()
    {
        FanAppeal.Value = 40f;
        CareerPath.Choose(CareerPath.Path.PitCrew);

        CareerPath.Reset();

        Assert.AreEqual(CareerPath.Path.Unchosen, CareerPath.Current);
        Assert.IsFalse(CareerPath.StatsApplied);
        foreach (var key in CareerPath.StatKeys)
            Assert.AreEqual(0, CareerPath.Stat(key), $"{key} should be back to zero");
        Assert.AreEqual(40f, FanAppeal.Value, 1e-3f);

        // And the question can be answered again, differently.
        Assert.IsTrue(CareerPath.Choose(CareerPath.Path.Driver));
        Assert.AreEqual(CareerPath.Path.Driver, CareerPath.Current);
    }

    [Test]
    public void DisplayName_CoversEveryPath()
    {
        Assert.IsNotEmpty(CareerPath.DisplayName(CareerPath.Path.Unchosen));
        foreach (var p in CareerPath.Choices)
            Assert.IsNotEmpty(CareerPath.DisplayName(p));
    }

    static string Highest(CareerPath.Path path)
    {
        string best = null;
        int bestValue = int.MinValue;
        foreach (var grant in CareerPath.StartingStats(path))
            if (grant.value > bestValue) { bestValue = grant.value; best = grant.key; }
        return best;
    }
}
