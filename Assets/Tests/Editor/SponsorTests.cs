using Draftmaster.Sponsors;
using NUnit.Framework;
using UnityEngine;

// Covers the money and negotiation rules — the parts that decide whether a sponsorship is worth chasing.
// The car painting and the NPC that speaks the lines are Unity-side and tested by playing the scene.
public class SponsorTests
{
    [SetUp]
    public void ClearBook()
    {
        SponsorBook.ClearAll();
        SponsorBook.InvalidateCache();
    }

    [TearDown]
    public void Cleanup() => SponsorBook.ClearAll();

    static SponsorDeal Deal(int perRace = 1000, int races = 8, int clausePos = 0, int clauseBonus = 0) => new SponsorDeal
    {
        sponsorName = "Test Brand",
        logoKey = "test-brand",
        perRace = perRace,
        racesTotal = races,
        racesRemaining = races,
        clausePosition = clausePos,
        clauseBonus = clauseBonus,
    };

    // ---------------------------------------------------------------- the core rule

    [Test]
    public void SignedButUnplacedDealEarnsNothing()
    {
        SponsorBook.Sign(Deal(perRace: 2000));
        Assert.AreEqual(0, SponsorBook.PayoutForFinish(1), "A deal that isn't on the car must not pay.");
        Assert.AreEqual(0, SponsorBook.PerRaceIncome());
    }

    [Test]
    public void PlacedDealPaysBySlot()
    {
        var deal = SponsorBook.Sign(Deal(perRace: 2000));

        SponsorBook.Place(deal.id, SponsorSlot.Hood);
        Assert.AreEqual(2000, SponsorBook.PayoutForFinish(20), "The hood pays the full rate.");

        SponsorBook.Place(deal.id, SponsorSlot.QuarterLeft);
        Assert.AreEqual(900, SponsorBook.PayoutForFinish(20), "A quarter panel pays 45%.");
    }

    [Test]
    public void ClauseBonusOnlyOnAGoodEnoughFinish()
    {
        var deal = SponsorBook.Sign(Deal(perRace: 1000, clausePos: 10, clauseBonus: 600));
        SponsorBook.Place(deal.id, SponsorSlot.Hood);

        Assert.AreEqual(1600, SponsorBook.PayoutForFinish(10), "Finishing on the clause pays the bonus.");
        Assert.AreEqual(1000, SponsorBook.PayoutForFinish(11), "Outside it, only the base rate.");
        Assert.AreEqual(1000, SponsorBook.PayoutForFinish(0), "A DNF earns no bonus.");
    }

    [Test]
    public void OnePanelHoldsOneDecal()
    {
        var first = SponsorBook.Sign(Deal(perRace: 1000));
        var second = SponsorBook.Sign(Deal(perRace: 3000));

        SponsorBook.Place(first.id, SponsorSlot.Hood);
        SponsorBook.Place(second.id, SponsorSlot.Hood);

        Assert.AreEqual(second.id, SponsorBook.InSlot(SponsorSlot.Hood).id, "The newcomer takes the panel.");
        Assert.AreEqual(SponsorSlot.None, SponsorBook.ById(first.id).slot, "The one it displaced comes off the car.");
        Assert.AreEqual(3000, SponsorBook.PayoutForFinish(20), "Only the decal actually on the car pays.");
    }

    [Test]
    public void MovingADealLeavesItsOldPanelFree()
    {
        var deal = SponsorBook.Sign(Deal());
        SponsorBook.Place(deal.id, SponsorSlot.Hood);
        SponsorBook.Place(deal.id, SponsorSlot.Tail);

        Assert.IsNull(SponsorBook.InSlot(SponsorSlot.Hood));
        Assert.AreEqual(deal.id, SponsorBook.InSlot(SponsorSlot.Tail).id);
        Assert.AreEqual(3, SponsorBook.FreeSlots());
    }

    [Test]
    public void DealsExpireAfterTheirRaces()
    {
        var deal = SponsorBook.Sign(Deal(races: 2));
        SponsorBook.Place(deal.id, SponsorSlot.Hood);

        Assert.AreEqual(0, SponsorBook.TickRace().Count, "Still one race left.");
        var expired = SponsorBook.TickRace();

        Assert.AreEqual(1, expired.Count, "The deal runs out on its last race.");
        Assert.AreEqual(0, SponsorBook.Count, "An expired deal leaves the book.");
        Assert.AreEqual(0, SponsorBook.PayoutForFinish(1));
    }

    [Test]
    public void UnplacedDealsBurnRacesToo()
    {
        var deal = SponsorBook.Sign(Deal(races: 3));
        SponsorBook.TickRace();
        Assert.AreEqual(2, SponsorBook.ById(deal.id).racesRemaining,
                        "A contract you never painted on the car still runs down.");
    }

    // ---------------------------------------------------------------- negotiation

    [Test]
    public void StandingGatesWhoWillTalk()
    {
        Assert.IsFalse(SponsorTerms.CanApproach(40f, 85), "A top brand won't talk to an unknown.");
        Assert.IsTrue(SponsorTerms.CanApproach(40f, 35), "A small brand will.");
    }

    [Test]
    public void BiggerBrandsPayMore()
    {
        Assert.Greater(SponsorTerms.BaseValue(95), SponsorTerms.BaseValue(55));
        // Sized against the live wallet economy: a race win pays $12,000, so a hood deal is a useful
        // fraction of that rather than a replacement for racing.
        Assert.That(SponsorTerms.BaseValue(95), Is.InRange(4000, 8000));
        Assert.That(SponsorTerms.BaseValue(55), Is.InRange(1200, 3500));
    }

    [Test]
    public void StandingAboveTheFloorOpensHigher()
    {
        int barely = SponsorTerms.OpeningValue(80, 60f, 60);
        int comfortably = SponsorTerms.OpeningValue(80, 100f, 60);
        Assert.Greater(comfortably, barely, "A bigger name gets a better opening number.");
        Assert.LessOrEqual(SponsorTerms.OpeningValue(80, 100f, 60), SponsorTerms.CeilingValue(80, 100f, 60));
    }

    [Test]
    public void PushingTwiceGetsTheFullAsk()
    {
        var offer = new SponsorTerms.Offer { perRace = 1000, races = 8 };
        int ceiling = 2000;

        var first = SponsorTerms.Respond(SponsorTerms.Move.PushGentle, offer, ceiling, 0);
        Assert.IsFalse(first.signed);
        Assert.IsFalse(first.walked);
        Assert.AreEqual(1075, first.offer.perRace, "They split the difference on the first push.");

        var second = SponsorTerms.Respond(SponsorTerms.Move.PushGentle, first.offer, ceiling, 1);
        Assert.AreEqual(Mathf.RoundToInt(1075 * 1.15f), second.offer.perRace, "And concede fully on the second.");
    }

    [Test]
    public void AnOutrageousAskEndsIt()
    {
        var offer = new SponsorTerms.Offer { perRace = 1000, races = 8 };
        var response = SponsorTerms.Respond(SponsorTerms.Move.PushHard, offer, ceiling: 1000, round: 2);
        Assert.IsTrue(response.walked, "Pushed past their ceiling twice, they leave.");
    }

    [Test]
    public void ShorterDealTradesLengthForRate()
    {
        var offer = new SponsorTerms.Offer { perRace = 1000, races = 10 };
        var response = SponsorTerms.Respond(SponsorTerms.Move.Shorten, offer, ceiling: 2000, round: 0);

        Assert.AreEqual(7, response.offer.races);
        Assert.Greater(response.offer.perRace, 1000);
        Assert.IsFalse(response.walked);
    }

    [Test]
    public void ShortestDealCantBeShortenedFurther()
    {
        var offer = new SponsorTerms.Offer { perRace = 1000, races = 4 };
        var response = SponsorTerms.Respond(SponsorTerms.Move.Shorten, offer, ceiling: 2000, round: 0);
        Assert.AreEqual(4, response.offer.races, "Four races is the floor.");
        Assert.AreEqual(1000, response.offer.perRace);
    }

    [Test]
    public void AcceptSignsTheTermsOnTheTable()
    {
        var offer = new SponsorTerms.Offer { perRace = 1500, races = 6 };
        var response = SponsorTerms.Respond(SponsorTerms.Move.Accept, offer, ceiling: 2000, round: 0);
        Assert.IsTrue(response.signed);
        Assert.AreEqual(1500, response.offer.perRace);
    }

    // ---------------------------------------------------------------- panels

    [Test]
    public void LayoutCentresDecalsInTheirPanel()
    {
        var layout = ScriptableObject.CreateInstance<CarSponsorLayout>();
        layout.hood = new RectInt(8, 8, 16, 16);

        Vector2Int anchor = layout.Anchor(SponsorSlot.Hood, 12, 6);
        Assert.AreEqual(new Vector2Int(10, 13), anchor, "A 12x6 decal sits centred in a 16x16 panel.");

        Object.DestroyImmediate(layout);
    }

    [Test]
    public void HoodOutEarnsTheQuarters()
    {
        Assert.Greater(SponsorSlots.PayMultiplier(SponsorSlot.Hood), SponsorSlots.PayMultiplier(SponsorSlot.Tail));
        Assert.Greater(SponsorSlots.PayMultiplier(SponsorSlot.Tail), SponsorSlots.PayMultiplier(SponsorSlot.QuarterLeft));
        Assert.AreEqual(0f, SponsorSlots.PayMultiplier(SponsorSlot.None), "Off the car is worth nothing.");
    }
}
