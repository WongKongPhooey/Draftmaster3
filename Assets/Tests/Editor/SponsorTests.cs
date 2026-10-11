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
        Assert.AreEqual(SponsorSlots.All.Length - 1, SponsorBook.FreeSlots(), "Hood + quarters free, tail taken.");
    }

    [Test]
    public void QuartersIsOneBrandOnBothRearQuarters()
    {
        var old = SponsorBook.Sign(Deal());
        SponsorBook.Place(old.id, SponsorSlot.QuarterLeft);      // placed before Quarters existed
        Assert.AreEqual(old.id, SponsorBook.InSlot(SponsorSlot.Quarters).id, "A single quarter still occupies the pair.");

        var fresh = SponsorBook.Sign(Deal(perRace: 1000));
        SponsorBook.Place(fresh.id, SponsorSlot.Quarters);
        Assert.IsFalse(SponsorBook.ById(old.id).IsPlaced, "Taking both quarters bumps whoever had one.");
        Assert.AreEqual(900, SponsorBook.PayoutForFinish(20), "Both quarters pay 90%.");
    }

    [Test]
    public void ArrangeLaysTheWholeCarOutAtOnce()
    {
        var a = SponsorBook.Sign(Deal(perRace: 1000));
        var b = SponsorBook.Sign(Deal(perRace: 1000));
        var c = SponsorBook.Sign(Deal(perRace: 1000));
        SponsorBook.Place(c.id, SponsorSlot.Hood);

        SponsorBook.Arrange(new System.Collections.Generic.Dictionary<SponsorSlot, int>
        {
            { SponsorSlot.Hood, a.id }, { SponsorSlot.Quarters, b.id }, { SponsorSlot.Tail, 0 },
        });

        Assert.AreEqual(SponsorSlot.Hood, SponsorBook.ById(a.id).slot);
        Assert.AreEqual(SponsorSlot.Quarters, SponsorBook.ById(b.id).slot);
        Assert.IsFalse(SponsorBook.ById(c.id).IsPlaced, "Anything not named comes off the car.");
    }

    [Test]
    public void CompositorPaintsOnlyTheMaskAndKeepsDetailOnTop()
    {
        var clear = new Color32(0, 0, 0, 0);
        var mask = new[] { new Color32(255, 255, 255, 255), new Color32(128, 128, 128, 255), clear, new Color32(255, 255, 255, 255) };
        var detail = new[] { clear, clear, clear, new Color32(1, 2, 3, 255) };
        var pattern = new[] { new Color32(255, 255, 255, 255), clear, clear, clear };
        var scheme = new PaintScheme
        {
            baseColour = new Color32(200, 100, 0, 255),
            pattern = "stripe",
            accentColour = new Color32(10, 20, 30, 255),
        };

        var px = LiveryCompositor.Compose(mask, detail, pattern, 4, 1, scheme);
        Assert.AreEqual(new Color32(10, 20, 30, 255), px[0], "Pattern pixels take the accent.");
        Assert.AreEqual(new Color32(100, 50, 0, 255), px[1], "Mask grey shades the base coat.");
        Assert.AreEqual(0, px[2].a, "Off the mask stays transparent.");
        Assert.AreEqual(new Color32(1, 2, 3, 255), px[3], "Detail sits on top of the paint.");
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
    public void TurnsMatchWhatAnImageViewerShows()
    {
        // 2 wide x 1 tall: A on the left, B on the right (rows bottom-up, one row).
        var a = new Color32(1, 0, 0, 255);
        var b = new Color32(2, 0, 0, 255);
        var src = new[] { a, b };

        // Clockwise: left end goes to the top -> 1 wide x 2 tall, A in the top row (index 1), B at the bottom.
        var cw = DecalTurns.Rotate(src, 2, 1, DecalTurn.Clockwise, out int w, out int h);
        Assert.AreEqual((1, 2), (w, h));
        Assert.AreEqual(a, cw[1]);
        Assert.AreEqual(b, cw[0]);

        // Counter-clockwise: left end goes to the bottom.
        var ccw = DecalTurns.Rotate(src, 2, 1, DecalTurn.CounterClockwise, out w, out h);
        Assert.AreEqual((1, 2), (w, h));
        Assert.AreEqual(a, ccw[0]);
        Assert.AreEqual(b, ccw[1]);

        var half = DecalTurns.Rotate(src, 2, 1, DecalTurn.Half, out w, out h);
        Assert.AreEqual((2, 1), (w, h));
        Assert.AreEqual(b, half[0]);
        Assert.AreEqual(a, half[1]);
    }

    [Test]
    public void SmallArtIsSizedForTheQuartersAndTurnedOntoTheTrunk()
    {
        var layout = ScriptableObject.CreateInstance<CarSponsorLayout>();
        layout.tail = new RectInt(58, 8, 4, 16);
        layout.tailTurn = DecalTurn.Clockwise;
        layout.quarterRight = new RectInt(48, 2, 16, 4);
        layout.quarterRightTurn = DecalTurn.None;

        Assert.AreEqual(new Vector2Int(16, 4), layout.ArtSize(SponsorSlot.Tail),
            "A 4x16 trunk strip turned a quarter takes the same 16x4 art as the quarter panels.");
        Assert.AreEqual(layout.ArtSize(SponsorSlot.QuarterRight), layout.ArtSize(SponsorSlot.Tail));

        Object.DestroyImmediate(layout);
    }

    [Test]
    public void StarterDealIsGivenOncePerCareerOnItsPanel()
    {
        var deal = new SponsorDeal { sponsorId = 99, sponsorName = "Aces & Spades", perRace = 1500, racesTotal = 36 };
        Assert.IsTrue(SponsorBook.GiveStarter(deal, SponsorSlot.Hood));
        Assert.AreEqual(SponsorSlot.Hood, SponsorBook.InSlot(SponsorSlot.Hood)?.slot);
        Assert.AreEqual(36, SponsorBook.ById(deal.id).racesRemaining);

        var again = new SponsorDeal { sponsorId = 99, sponsorName = "Aces & Spades", perRace = 1500, racesTotal = 36 };
        Assert.IsFalse(SponsorBook.GiveStarter(again, SponsorSlot.Hood), "Only once per career.");
        Assert.AreEqual(1, SponsorBook.Count);

        SponsorBook.ClearAll();
        Assert.IsTrue(SponsorBook.GiveStarter(again, SponsorSlot.Hood), "A wiped book is a new career.");
    }

    [Test]
    public void StarterDealNeverBumpsAPlacedSponsor()
    {
        var mine = SponsorBook.Sign(new SponsorDeal { sponsorId = 1, perRace = 2000, racesTotal = 5 });
        SponsorBook.Place(mine.id, SponsorSlot.Hood);

        SponsorBook.GiveStarter(new SponsorDeal { sponsorId = 99, perRace = 1500, racesTotal = 36 }, SponsorSlot.Hood);
        Assert.AreEqual(mine.id, SponsorBook.InSlot(SponsorSlot.Hood).id, "An old save keeps the hood it already sold.");
    }

    [Test]
    public void HoodOutEarnsTheQuarters()
    {
        Assert.Greater(SponsorSlots.PayMultiplier(SponsorSlot.Hood), SponsorSlots.PayMultiplier(SponsorSlot.Tail));
        Assert.Greater(SponsorSlots.PayMultiplier(SponsorSlot.Tail), SponsorSlots.PayMultiplier(SponsorSlot.QuarterLeft));
        Assert.AreEqual(0f, SponsorSlots.PayMultiplier(SponsorSlot.None), "Off the car is worth nothing.");
    }
}
