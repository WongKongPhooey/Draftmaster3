using Draftmaster.Sim;
using NUnit.Framework;

// Re-opening the setup sheet mid-session (back in the car after a tow, in practice or qualifying) must not be
// a free pit stop: only what the driver changed goes on the car. SetupAdjustment, read by
// CarSetup.ApplyChangesTo.
public class CarSetupAdjustTests
{
    const int Soft = 0, Hard = 1;

    [Test]
    public void SameCompoundKeepsTheWornSet() => Assert.IsFalse(SetupAdjustment.FitsNewTyres(Soft, Soft));

    [Test]
    public void NewCompoundIsAFreshSet()
    {
        Assert.IsTrue(SetupAdjustment.FitsNewTyres(Soft, Hard));
        Assert.IsTrue(SetupAdjustment.FitsNewTyres(Hard, Soft));
    }

    [Test]
    public void UntouchedFuelSliderKeepsWhatIsLeft()
    {
        Assert.IsFalse(SetupAdjustment.ChangesFuel(12f, 12f));
        Assert.IsFalse(SetupAdjustment.ChangesFuel(12f, 12.2f), "Float noise is not a moved slider.");
    }

    [Test]
    public void MovedFuelSliderFillsToTheNewLoad()
    {
        Assert.IsTrue(SetupAdjustment.ChangesFuel(12f, 13f));
        Assert.IsTrue(SetupAdjustment.ChangesFuel(12f, 4f), "Draining counts as a change too.");
    }
}
