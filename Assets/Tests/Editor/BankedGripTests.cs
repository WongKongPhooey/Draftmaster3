using Draftmaster.Sim;
using NUnit.Framework;

// Banked turns: the tyres loaded harder by cornering into the bank, plus gravity down it.
public class BankedGripTests
{
    [Test]
    public void LevelGroundChangesNothing()
    {
        Assert.AreEqual(20f, BankedGrip.Capacity(20f, 0f), 1e-4f);
        Assert.AreEqual(1f, BankedGrip.LoadFactor(0f, 15f), 1e-4f);
        Assert.AreEqual(0f, BankedGrip.DownslopeAccel(0f), 1e-4f);
    }

    [Test]
    public void MoreBankHoldsMore()
    {
        float flat = 18f;   // ~1.8 g, the game's AI grip
        float bristol = BankedGrip.Capacity(flat, 26f);
        float daytona = BankedGrip.Capacity(flat, 31f);
        float backStretch = BankedGrip.Capacity(flat, 3f);
        Assert.Greater(backStretch, flat);
        Assert.Greater(bristol, backStretch);
        Assert.Greater(daytona, bristol);
    }

    [Test]
    public void DaytonaIsFlatOutButNotLimitless()
    {
        // A Cup car at 190 mph (85 m/s) round Daytona's ~300 m turns needs ~24 m/s².
        float daytona = BankedGrip.Capacity(18f, 31f);
        Assert.Greater(daytona, 24f, "Daytona's banking should hold a Cup car flat out");
        Assert.Less(daytona, 18f * BankedGrip.MaxLoadFactor + BankedGrip.G, "the load factor is capped");
    }

    [Test]
    public void LoadFactorIsCapped()
    {
        Assert.AreEqual(BankedGrip.MaxLoadFactor, BankedGrip.LoadFactor(31f, 1000f), 1e-4f);
    }
}
