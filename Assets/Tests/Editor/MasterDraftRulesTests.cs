using System.Collections.Generic;
using System.Linq;
using Draftmaster.MasterDraft;
using NUnit.Framework;
using UnityEngine;

// Master Draft's pure rules (Assets/Scripts/MasterDraft/Core): packs, team legality,
// coin rewards and the saved profile. The catalogue is built from generated stand-ins, the same path the
// game takes when the career database isn't ready.
public class MasterDraftRulesTests
{
    static CardCatalog Catalog() => new CardCatalog(null, null);

    static MasterDraftProfile NewProfile(string id = "test-player") => MasterDraftProfile.New(id);

    // ------------------------------------------------------------------ packs

    [Test]
    public void StarterPackAlwaysHasEveryCardType()
    {
        var catalog = Catalog();
        for (long seed = 0; seed < 1500; seed++)
        {
            var cards = PackGenerator.Open(PackKind.Starter, catalog, seed * 7919 + 3);
            Assert.AreEqual(PackGenerator.StarterSize, cards.Count, $"seed {seed}: wrong size");
            foreach (CardType t in System.Enum.GetValues(typeof(CardType)))
                Assert.IsTrue(cards.Any(c => c.type == t), $"seed {seed}: no {t} card");
            Assert.IsTrue(cards.All(c => CardText.IsKnown(c.type)), $"seed {seed}: a card of a retired type");
        }
    }

    [Test]
    public void PacksAreDeterministicPerSeed()
    {
        var catalog = Catalog();
        var a = PackGenerator.Open(PackKind.Standard, catalog, 42).Select(c => c.id).ToList();
        var b = PackGenerator.Open(PackKind.Standard, catalog, 42).Select(c => c.id).ToList();
        CollectionAssert.AreEqual(a, b);
    }

    [Test]
    public void StandardPackSizeAndRarityBounds()
    {
        var catalog = Catalog();
        var counts = new int[4];
        int packs = 2000;
        for (long seed = 0; seed < packs; seed++)
        {
            var cards = PackGenerator.Open(PackKind.Standard, catalog, seed);
            Assert.AreEqual(PackGenerator.StandardSize, cards.Count);
            Assert.IsTrue(cards.Any(c => c.rarity >= Rarity.Rare), $"seed {seed}: no Rare-or-better feature card");
            Assert.IsTrue(cards.All(c => CardText.IsKnown(c.type)), $"seed {seed}: a card of a retired type");
            foreach (var c in cards) counts[(int)c.rarity]++;
        }

        // Commons are the bulk, Legendaries are rare but do drop.
        int total = packs * PackGenerator.StandardSize;
        Assert.Greater(counts[(int)Rarity.Common], total * 0.35, "too few commons");
        Assert.Greater(counts[(int)Rarity.Legendary], 0, "no legendary in 2000 packs");
        Assert.Less(counts[(int)Rarity.Legendary], total * 0.08, "legendaries too common");
    }

    [Test]
    public void StarterIsClaimableExactlyOnce()
    {
        var catalog = Catalog();
        var profile = NewProfile();
        var first = ProfileOps.ClaimStarter(profile, catalog);
        Assert.IsTrue(first.ok, first.error);
        Assert.AreEqual(PackGenerator.StarterSize, first.cards.Count);
        Assert.AreEqual(PackGenerator.StarterSize, profile.cards.Count);
        Assert.IsTrue(profile.starterClaimed);

        var second = ProfileOps.ClaimStarter(profile, catalog);
        Assert.IsFalse(second.ok);
        Assert.AreEqual(PackGenerator.StarterSize, profile.cards.Count, "a second claim added cards");
    }

    [Test]
    public void StarterBuildsALegalTeam()
    {
        var catalog = Catalog();
        for (int i = 0; i < 200; i++)
        {
            var profile = NewProfile("p" + i);
            ProfileOps.ClaimStarter(profile, catalog);
            var problems = TeamRules.Validate(profile.team, profile);
            Assert.IsEmpty(problems, $"profile p{i}: " + string.Join("; ", problems));
        }
    }

    [Test]
    public void StandardPackCostsCoinsAndRefusesWhenBroke()
    {
        var catalog = Catalog();
        var profile = NewProfile();
        profile.coins = Economy.StandardPackPrice + 10;
        var bought = ProfileOps.BuyStandardPack(profile, catalog);
        Assert.IsTrue(bought.ok, bought.error);
        Assert.AreEqual(10, profile.coins);
        Assert.AreEqual(PackGenerator.StandardSize, profile.cards.Count);

        var broke = ProfileOps.BuyStandardPack(profile, catalog);
        Assert.IsFalse(broke.ok);
        Assert.AreEqual(10, profile.coins);
        Assert.AreEqual(PackGenerator.StandardSize, profile.cards.Count);
    }

    [Test]
    public void CardUidsAreUniqueAcrossPacks()
    {
        var catalog = Catalog();
        var profile = NewProfile();
        profile.coins = 100000;
        ProfileOps.ClaimStarter(profile, catalog);
        for (int i = 0; i < 20; i++) ProfileOps.BuyStandardPack(profile, catalog);
        Assert.AreEqual(profile.cards.Count, profile.cards.Select(c => c.uid).Distinct().Count());
    }

    // ------------------------------------------------------------------ team

    static (MasterDraftProfile, MasterDraftTeam) LegalTeam()
    {
        var catalog = Catalog();
        var p = NewProfile();
        var rng = new DraftRng(5);
        var d1 = p.Add(catalog.Pick(CardType.Driver, Rarity.Epic, rng));
        var car = p.Add(catalog.Pick(CardType.Car, Rarity.Rare, rng));
        var cc = p.Add(catalog.Pick(CardType.CrewChief, Rarity.Rare, rng));
        var team = new MasterDraftTeam { activeDriver = d1.uid, car = car.uid, crewChief = cc.uid };
        p.team = team;
        return (p, team);
    }

    [Test]
    public void ALegalTeamValidates()
    {
        var (p, team) = LegalTeam();
        Assert.IsEmpty(TeamRules.Validate(team, p));
        Assert.Greater(TeamRules.Rating(team, p), 0);
    }

    [Test]
    public void TeamValidationGivesReadableReasons()
    {
        var (p, team) = LegalTeam();

        team.car = "";
        StringAssert.Contains("car", string.Join(" ", TeamRules.Validate(team, p)));

        // A car in the driver slot, and a card that isn't owned.
        (p, team) = LegalTeam();
        team.activeDriver = team.car;
        StringAssert.Contains("not a Driver", string.Join(" ", TeamRules.Validate(team, p)));
        (p, team) = LegalTeam();
        team.crewChief = "nope";
        StringAssert.Contains("not in your collection", string.Join(" ", TeamRules.Validate(team, p)));

        // Too many drivers on the bench.
        (p, team) = LegalTeam();
        var rng = new DraftRng(9);
        var catalog = Catalog();
        for (int i = 0; i < TeamRules.MaxBench + 1; i++) team.bench.Add(p.Add(catalog.Pick(CardType.Driver, Rarity.Common, rng)).uid);
        StringAssert.Contains("Bench", string.Join(" ", TeamRules.Validate(team, p)));
    }

    [Test]
    public void BestBuildTakesTheHighestRatedCardOfEachType()
    {
        var p = NewProfile();
        Assert.IsFalse(TeamRules.BestBuild(p).Complete, "an empty collection has no build");
        Assert.AreEqual(0, TeamRules.BestBuild(p).Rating);

        p.Add(new CardDefinition { id = "d1", type = CardType.Driver, name = "D1", overall = 70 });
        var d2 = p.Add(new CardDefinition { id = "d2", type = CardType.Driver, name = "D2", overall = 91 });
        p.Add(new CardDefinition { id = "d3", type = CardType.Driver, name = "D3", overall = 91 });   // tie: earlier uid wins
        var car = p.Add(new CardDefinition { id = "c1", type = CardType.Car, name = "C1", overall = 80 });
        p.Add(new CardDefinition { id = "c2", type = CardType.Car, name = "C2", overall = 60 });
        var build = TeamRules.BestBuild(p);
        Assert.AreSame(d2, build.driver);
        Assert.AreSame(car, build.car);
        Assert.IsNull(build.crewChief);
        Assert.IsFalse(build.Complete);

        var chief = p.Add(new CardDefinition { id = "cc", type = CardType.CrewChief, name = "CC", overall = 50 });
        build = TeamRules.BestBuild(p);
        Assert.AreSame(chief, build.crewChief);
        Assert.IsTrue(build.Complete);
        Assert.AreEqual((int)System.Math.Round(91 * 0.5 + 80 * 0.25 + 50 * 0.25), build.Rating);
    }

    [Test]
    public void BestDriverForTrackUsesTheAptitude()
    {
        var p = NewProfile();
        var road = p.Add(new CardDefinition { id = "a", type = CardType.Driver, name = "Road", overall = 70, roadCourses = 19, superspeedways = 8 });
        var plate = p.Add(new CardDefinition { id = "b", type = CardType.Driver, name = "Plate", overall = 90, roadCourses = 9, superspeedways = 18 });
        var drivers = new[] { road, plate };
        Assert.AreSame(road, TeamRules.BestDriverFor(TrackKind.RoadCourse, drivers));
        Assert.AreSame(plate, TeamRules.BestDriverFor(TrackKind.Superspeedway, drivers));
        // Tie on aptitude goes to the higher OVR.
        Assert.AreSame(plate, TeamRules.BestDriverFor(TrackKind.ShortTrack, drivers));
        Assert.IsNull(TeamRules.BestDriverFor(TrackKind.ShortTrack, new CardInstance[0]));
    }

    // ------------------------------------------------------------------ economy + profile

    [Test]
    public void CoinRewardsFollowTheFinish()
    {
        Assert.AreEqual(Economy.WinnerCoins, Economy.ForFinish(1));
        Assert.AreEqual(Economy.WinnerCoins - Economy.CoinsLostPerPlace, Economy.ForFinish(2));
        Assert.AreEqual(Economy.MinimumFinishCoins, Economy.ForFinish(40));
        Assert.AreEqual(Economy.MinimumFinishCoins, Economy.ForFinish(0), "DNF pays the minimum");

        var p = NewProfile();
        int before = p.coins;
        var r = ProfileOps.AwardRace(p, 1);
        Assert.AreEqual(before + r.awarded, p.coins);
        Assert.AreEqual(1, p.wins);
        Assert.AreEqual(1, p.racesRun);
    }

    [Test]
    public void ProfileRoundTripsThroughJson()
    {
        var catalog = Catalog();
        var p = NewProfile("round-trip");
        ProfileOps.ClaimStarter(p, catalog);
        TradeRules.EnsureBotOffers(p, catalog);
        var spare = TradeRules.Tradeable(p);
        if (spare.Count > 0) TradeRules.Propose(p, spare[0].uid, CardType.Driver, Rarity.Rare);

        var back = MasterDraftProfile.FromJson(p.ToJson());
        Assert.IsNotNull(back);
        Assert.AreEqual(p.ToJson(), back.ToJson());
        Assert.AreEqual(p.cards.Count, back.cards.Count);
        Assert.AreEqual(p.team.bench, back.team.bench);
        Assert.AreEqual(p.cards[0].def.name, back.cards[0].def.name);
        Assert.IsTrue(back.starterClaimed);
        Assert.AreEqual(MasterDraftProfile.CurrentSchema, back.schemaVersion);

        Assert.IsNull(MasterDraftProfile.FromJson(""));
        Assert.IsNull(MasterDraftProfile.FromJson("not json"));
    }

    [Test]
    public void RetiredActionCardsLeaveTheCollectionOnLoad()
    {
        var catalog = Catalog();
        var p = NewProfile("old-actions");
        ProfileOps.ClaimStarter(p, catalog);
        int kept = p.cards.Count;
        // Type 3 was Action. A save from before the action cards were removed still holds some.
        var action = p.Add(new CardDefinition { id = "ACT-PushToPass", type = (CardType)3, name = "Push To Pass" });
        p.team.bench.Add(action.uid);
        p.myListings.Add(new TradeOffer { id = "mine-" + action.uid, mine = true, myCardUid = action.uid });

        var back = MasterDraftProfile.FromJson(p.ToJson());
        Assert.AreEqual(kept, back.cards.Count);
        Assert.IsNull(back.Find(action.uid));
        Assert.IsFalse(back.team.Uses(action.uid));
        Assert.IsEmpty(back.myListings);
        Assert.IsEmpty(TeamRules.Validate(back.team, back));
    }

    // ------------------------------------------------------------------ trades

    [Test]
    public void AcceptingABotOfferSwapsTheCards()
    {
        var catalog = Catalog();
        var p = NewProfile("trader");
        TradeRules.EnsureBotOffers(p, catalog);
        Assert.AreEqual(TradeRules.BotOfferCount, p.botOffers.Count);

        var offer = p.botOffers[0];
        var rng = new DraftRng(3);
        var mine = p.Add(catalog.Pick(offer.wantType, Rarity.Legendary, rng));
        Assert.GreaterOrEqual(mine.def.rarity, offer.wantMinRarity);

        var result = TradeRules.Accept(p, offer.id, mine.uid);
        Assert.IsTrue(result.ok, result.error);
        Assert.IsNull(p.Find(mine.uid));
        Assert.AreEqual(offer.giving.id, p.cards.Single().def.id);
        Assert.IsFalse(p.botOffers.Contains(offer));
    }

    [Test]
    public void TradesRefuseTheWrongCardOrATeamCard()
    {
        var catalog = Catalog();
        var p = NewProfile("trader2");
        TradeRules.EnsureBotOffers(p, catalog);
        var offer = p.botOffers[0];
        var wrongType = (CardType)(((int)offer.wantType + 1) % PackGenerator.TypeWeights.Length);
        var rng = new DraftRng(4);
        var wrong = p.Add(catalog.Pick(wrongType, Rarity.Legendary, rng));
        Assert.IsFalse(TradeRules.Accept(p, offer.id, wrong.uid).ok);

        var right = p.Add(catalog.Pick(offer.wantType, Rarity.Legendary, rng));
        p.team.activeDriver = right.uid;
        Assert.IsFalse(TradeRules.Accept(p, offer.id, right.uid).ok, "traded away a card in the team");
        Assert.IsNotNull(p.Find(right.uid));
    }

    // ------------------------------------------------------------------ race effects

    [Test]
    public void CarAndChiefEffectsStayModest()
    {
        foreach (var car in CardFactory.Cars())
        {
            foreach (float f in new[] { TeamEffects.PowerScale(car), TeamEffects.GripScale(car), TeamEffects.DraftScale(car) })
            {
                Assert.LessOrEqual(Mathf.Abs(f - 1f), TeamEffects.CarSwing + 1e-5f, car.name);
            }
        }
        var best = new CardDefinition { type = CardType.CrewChief, pitSpeed = 99, strategy = 99, setup = 99 };
        var worst = new CardDefinition { type = CardType.CrewChief, pitSpeed = 1, strategy = 1, setup = 1 };
        Assert.Less(TeamEffects.PitTimeScale(best), 1f);
        Assert.Greater(TeamEffects.PitTimeScale(worst), 1f);
        Assert.GreaterOrEqual(TeamEffects.PitTimeScale(best), 1f - TeamEffects.PitSwing - 1e-5f);
        Assert.Less(TeamEffects.FuelBurnScale(best), 1f);
        Assert.Less(TeamEffects.TyreWearScale(best), 1f);
        Assert.AreEqual(1f, TeamEffects.PowerScale(null));
        Assert.AreEqual(1f, TeamEffects.PitTimeScale(null));

        // A better car is stronger: the Champion trim out-powers the Club trim of the same make.
        var cars = CardFactory.Cars().Where(c => c.manufacturer == "CHV").OrderBy(c => c.rarity).ToList();
        Assert.Greater(TeamEffects.PowerScale(cars.Last()), TeamEffects.PowerScale(cars.First()));
    }
}
