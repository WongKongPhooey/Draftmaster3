using Draftmaster.Audio;
using NUnit.Framework;

// OPTIONS > SOUND's rules: how a press moves a level, how it is drawn, and that every channel has a name, a
// line of help and a key of its own.
public class SoundMixTests
{
    [Test]
    public void StepMovesTenAndHoldsAtTheEnds()
    {
        Assert.AreEqual(60, SoundMix.Step(50, 1));
        Assert.AreEqual(40, SoundMix.Step(50, -1));
        Assert.AreEqual(100, SoundMix.Step(100, 1), "turning a full level up must not drop it to silence");
        Assert.AreEqual(0, SoundMix.Step(0, -1));
    }

    [Test]
    public void CycleGoesUpAndWrapsFromFullToOff()
    {
        Assert.AreEqual(10, SoundMix.Cycle(0));
        Assert.AreEqual(100, SoundMix.Cycle(90));
        Assert.AreEqual(0, SoundMix.Cycle(100), "a phone has no left: tapping on from full must come round");
    }

    [Test]
    public void OffStepValuesSnapToTheNearestStep()
    {
        Assert.AreEqual(40, SoundMix.Snap(35));
        Assert.AreEqual(30, SoundMix.Snap(34));
        Assert.AreEqual(50, SoundMix.Step(35, 1));
        Assert.AreEqual(100, SoundMix.Snap(250));
        Assert.AreEqual(0, SoundMix.Snap(-20));
    }

    [Test]
    public void ScaleIsZeroToOne()
    {
        Assert.AreEqual(0f, SoundMix.Scale(0));
        Assert.AreEqual(0.5f, SoundMix.Scale(50), 1e-6f);
        Assert.AreEqual(1f, SoundMix.Scale(100));
        Assert.AreEqual(1f, SoundMix.Scale(300));
    }

    [Test]
    public void FormatDrawsABarAndThePercent()
    {
        Assert.AreEqual("[##########]  100%", SoundMix.Format(100));
        Assert.AreEqual("[######----]  60%", SoundMix.Format(60));
        Assert.AreEqual("[----------]  OFF", SoundMix.Format(0));
    }

    [Test]
    public void EveryChannelIsNamedDescribedAndKeyedApart()
    {
        var keys = new System.Collections.Generic.HashSet<string>();
        foreach (var c in SoundMix.All)
        {
            Assert.IsNotEmpty(SoundMix.Label(c), c.ToString());
            Assert.IsNotEmpty(SoundMix.Describe(c), c.ToString());
            Assert.LessOrEqual(SoundMix.Describe(c).Length, 65, $"{c}'s help line must fit the footer");
            Assert.IsTrue(keys.Add(SoundMix.PrefKey(c)), $"{c} shares a pref key");
        }
        Assert.AreEqual(System.Enum.GetValues(typeof(SoundChannel)).Length, SoundMix.All.Length,
                        "a channel missing from All gets no row on the SOUND page");
    }
}
