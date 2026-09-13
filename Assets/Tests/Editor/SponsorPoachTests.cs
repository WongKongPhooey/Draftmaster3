using Draftmaster.Fans;
using Draftmaster.Sponsors;
using NUnit.Framework;

// The brand that walks up to you in the paddock rather than waiting to be found: when it bothers, what it
// offers, and whether the target on the end of the contract actually pays.
//
// The NPC who speaks the lines, the walk-up cutscene and the term-sheet popup are Unity-side and checked
// by playing the scene; everything here is the arithmetic underneath them.
public class SponsorPoachTests
{
    [SetUp]
    public void ClearBook()
    {
        SponsorBook.ClearAll();
        SponsorBook.InvalidateCache();
    }

    [TearDown]
    public void Cleanup() => SponsorBook.ClearAll();

    static SponsorDeal Deal(int perRace = 1000, int races = 8,
                            int targetPos = 0, int targetCount = 0, int targetBonus = 0) => new SponsorDeal
    {
        sponsorName = "Test Brand",
        logoKey = "test-brand",
        perRace = perRace,
        racesTotal = races,
        racesRemaining = races,
        targetPosition = targetPos,
        targetCount = targetCount,
        targetBonus = targetBonus,
    };

    // ---------------------------------------------------------------- when they bother

    [Test]
    public void NobodyPoachesADriverNobodyHasHeardOf()
    {
        Assert.IsFalse(SponsorPoach.Qualifies(SponsorPoach.AppealRequired - 1f));
        Assert.IsTrue(SponsorPoach.Qualifies(SponsorPoach.AppealRequired));
        Assert.IsTrue(SponsorPoach.Qualifies(SponsorPoach.AppealRequired + 20f));
    }

    [Test]
    public void TheBarIsLowEnoughForAFreshSaveToSeeIt()
    {
        // The demo has to be able to show this beat on its first weekend, and a fresh save starts at
        // FanAppeal.Default. If the bar is ever raised above it, this is the test that should say so.
        Assert.IsTrue(SponsorPoach.Qualifies(FanAppeal.Default),
                      "A brand new career must clear the poaching bar, or the demo never sees the beat.");
    }

    // ---------------------------------------------------------------- the offer

    [Test]
    public void TheBestRateOnTheCarIsTheNumberToBeat()
    {
        Assert.AreEqual(0, SponsorPoach.BestRateOnTheBooks(SponsorBook.Deals), "An empty book beats nothing.");

        SponsorBook.Sign(Deal(perRace: 1200));
        SponsorBook.Sign(Deal(perRace: 2600));
        SponsorBook.Sign(Deal(perRace: 800));
        Assert.AreEqual(2600, SponsorPoach.BestRateOnTheBooks(SponsorBook.Deals));
    }

    [Test]
    public void ExpiredDealsAreNotWhatYouAreOn()
    {
        var dead = SponsorBook.Sign(Deal(perRace: 5000, races: 1));
        dead.racesRemaining = 0;
        SponsorBook.Sign(Deal(perRace: 900));
        Assert.AreEqual(900, SponsorPoach.BestRateOnTheBooks(SponsorBook.Deals));
    }

    [Test]
    public void TheyComeInClearOfWhateverIsOnTheCar()
    {
        // A small brand whose own opening number would be well under what the player already earns still
        // has to beat it, or the whole pitch is a lie.
        var offer = SponsorPoach.Offer(wealth: 30, prestige: 30, standing: 40f, minPrestige: 20, beat: 4000);
        Assert.GreaterOrEqual(offer.perRace, 5000, "1.25x of $4,000 is the floor they can walk in on.");
    }

    [Test]
    public void ABigBrandStillOpensOnItsOwnNumberWhenTheCarIsBare()
    {
        var theirs = SponsorTerms.Open(95, 90, 40f, 85);
        var offer = SponsorPoach.Offer(95, 90, 40f, 85, beat: 0);
        Assert.AreEqual(theirs.perRace, offer.perRace);
        Assert.AreEqual(theirs.races, offer.races);
    }

    [Test]
    public void APoachCarriesNoPerRaceClause()
    {
        // Their clause is the contract-long target instead — two bonuses on one sheet reads as a menu.
        var offer = SponsorPoach.Offer(95, 90, 90f, 85, beat: 0);
        Assert.AreEqual(0, offer.clausePosition);
        Assert.AreEqual(0, offer.clauseBonus);
    }

    [Test]
    public void TheDealTheyHandOverCarriesTheTarget()
    {
        var offer = SponsorPoach.Offer(95, 90, 40f, 85, beat: 0);
        var deal = SponsorPoach.Deal(7, "Voltage Energy", "voltage-energy", offer);

        Assert.AreEqual(7, deal.sponsorId);
        Assert.AreEqual(offer.races, deal.racesTotal);
        Assert.AreEqual(offer.races, deal.racesRemaining);
        Assert.AreEqual(SponsorPoach.TargetPosition, deal.targetPosition);
        Assert.AreEqual(SponsorPoach.TargetCount, deal.targetCount);
        Assert.AreEqual(offer.perRace * SponsorPoach.TargetCount, deal.targetBonus);
        Assert.IsTrue(deal.HasTarget);
        StringAssert.Contains("top 5", deal.TargetText);
        StringAssert.Contains("twice", deal.TargetText);
    }

    // ---------------------------------------------------------------- the target paying out

    [Test]
    public void ATargetOnADealThatIsNotOnTheCarPaysNothing()
    {
        var deal = SponsorBook.Sign(Deal(targetPos: 5, targetCount: 2, targetBonus: 4000));
        Assert.AreEqual(0, SponsorBook.SettleTargets(1));
        Assert.AreEqual(0, SponsorBook.SettleTargets(1));
        Assert.AreEqual(0, deal.targetProgress, "An unplaced decal banks no progress either.");
    }

    [Test]
    public void TheTargetPaysOnceTheCountIsReached()
    {
        var deal = SponsorBook.Sign(Deal(targetPos: 5, targetCount: 2, targetBonus: 4000));
        SponsorBook.Place(deal.id, SponsorSlot.Hood);

        Assert.AreEqual(0, SponsorBook.SettleTargets(3), "One top-5 is not two.");
        Assert.AreEqual(1, deal.targetProgress);
        Assert.AreEqual(4000, SponsorBook.SettleTargets(5), "The second one settles the contract target.");
        Assert.IsTrue(deal.targetPaid);
    }

    [Test]
    public void FinishesOutsideTheTargetDoNotCount()
    {
        var deal = SponsorBook.Sign(Deal(targetPos: 5, targetCount: 2, targetBonus: 4000));
        SponsorBook.Place(deal.id, SponsorSlot.Hood);

        SponsorBook.SettleTargets(6);
        SponsorBook.SettleTargets(0);      // a DNF is not a finish
        SponsorBook.SettleTargets(22);
        Assert.AreEqual(0, deal.targetProgress);
    }

    [Test]
    public void TheTargetNeverPaysTwice()
    {
        var deal = SponsorBook.Sign(Deal(targetPos: 5, targetCount: 2, targetBonus: 4000));
        SponsorBook.Place(deal.id, SponsorSlot.Hood);

        SponsorBook.SettleTargets(1);
        Assert.AreEqual(4000, SponsorBook.SettleTargets(1));
        Assert.AreEqual(0, SponsorBook.SettleTargets(1));
        Assert.AreEqual(0, SponsorBook.SettleTargets(1));
    }

    [Test]
    public void AnOrdinaryDealIsUntouchedByTargetSettlement()
    {
        var deal = SponsorBook.Sign(Deal(perRace: 2000));
        SponsorBook.Place(deal.id, SponsorSlot.Hood);

        Assert.IsFalse(deal.HasTarget);
        Assert.AreEqual(0, SponsorBook.SettleTargets(1));
        Assert.AreEqual(2000, SponsorBook.PayoutForFinish(1), "The per-race money is unaffected.");
    }

    [Test]
    public void AnExpiredDealCannotStillHitItsTarget()
    {
        var deal = SponsorBook.Sign(Deal(races: 1, targetPos: 5, targetCount: 1, targetBonus: 4000));
        SponsorBook.Place(deal.id, SponsorSlot.Hood);
        deal.racesRemaining = 0;

        Assert.AreEqual(0, SponsorBook.SettleTargets(1));
    }
}
