using Draftmaster.Weekend;
using NUnit.Framework;

// Practice morale: laps and timesheet position both count, and position can outvote the laps.
public class PracticeVerdictTests
{
    const int Field = 40;

    [Test]
    public void NoLaps_CostsMorale_AndBanksNoSetup()
    {
        var o = PracticeVerdict.Evaluate(0, 0, Field);
        Assert.Less(o.teamMorale, 0f);
        Assert.AreEqual(0f, o.setupGain);
        Assert.AreEqual("practicesessions", o.statKey);
    }

    [Test]
    public void ProperRun_InMidfield_IsPositive()
    {
        Assert.Greater(PracticeVerdict.Evaluate(3, 20, Field).teamMorale, 0f);
    }

    [Test]
    public void ProperRun_InBottomFive_StillDropsMorale([Values(3, 6, 12, 20)] int laps)
    {
        for (int pos = Field - PracticeVerdict.BottomGroup + 1; pos <= Field; pos++)
            Assert.Less(PracticeVerdict.Evaluate(laps, pos, Field).teamMorale, 0f, $"{laps} laps P{pos}");
    }

    [Test]
    public void OneLap_InTopEight_StillLiftsMorale()
    {
        for (int pos = 1; pos <= PracticeVerdict.TopGroup; pos++)
            Assert.Greater(PracticeVerdict.Evaluate(1, pos, Field).teamMorale, 0f, $"P{pos}");
    }

    [Test]
    public void TopEight_HoldsInSmallerFields([Values(20, 30, 36)] int field)
    {
        Assert.Greater(PracticeVerdict.Evaluate(1, PracticeVerdict.TopGroup, field).teamMorale, 0f);
        Assert.Less(PracticeVerdict.Evaluate(PracticeVerdict.ProperRunLaps, field - PracticeVerdict.BottomGroup + 1, field)
                                   .teamMorale, 0f);
    }

    [Test]
    public void SetupGain_ComesFromLapsOnly()
    {
        Assert.AreEqual(PracticeVerdict.Evaluate(4, 1, Field).setupGain,
                        PracticeVerdict.Evaluate(4, Field, Field).setupGain, 1e-5f);
        Assert.Less(PracticeVerdict.Evaluate(1, 1, Field).setupGain,
                    PracticeVerdict.Evaluate(12, Field, Field).setupGain);
    }

    [Test]
    public void Morale_RisesWithLaps_AtFixedPosition()
    {
        float prev = float.MinValue;
        for (int laps = 0; laps <= PracticeVerdict.FullRunLaps; laps++)
        {
            float m = PracticeVerdict.Evaluate(laps, laps == 0 ? 0 : 15, Field).teamMorale;
            if (laps > 0) Assert.Greater(m, prev, $"{laps} laps");
            prev = m;
        }
    }

    [Test]
    public void Morale_FallsDownTheTimesheet_AtFixedLaps()
    {
        float prev = float.MaxValue;
        for (int pos = 1; pos <= Field; pos++)
        {
            float m = PracticeVerdict.Evaluate(5, pos, Field).teamMorale;
            Assert.Less(m, prev, $"P{pos}");
            prev = m;
        }
    }

    [Test]
    public void FastestInPractice_BeatsAFullRunInMidfield()
    {
        Assert.Greater(PracticeVerdict.Evaluate(1, 1, Field).teamMorale,
                       PracticeVerdict.Evaluate(12, 20, Field).teamMorale);
    }

    [Test]
    public void TinyField_StaysFinite_AndOrdered()
    {
        Assert.Greater(PracticeVerdict.Evaluate(3, 1, 4).teamMorale, PracticeVerdict.Evaluate(3, 4, 4).teamMorale);
        Assert.AreEqual(PracticeVerdict.WorkRateMorale(3), PracticeVerdict.Evaluate(3, 1, 1).teamMorale, 1e-5f);
    }

    [Test]
    public void Headline_NamesThePosition()
    {
        StringAssert.Contains("P5 of 40", PracticeVerdict.Evaluate(1, 5, Field).headline);
        StringAssert.Contains("never turned a lap", PracticeVerdict.Evaluate(0, 0, Field).headline);
    }
}
