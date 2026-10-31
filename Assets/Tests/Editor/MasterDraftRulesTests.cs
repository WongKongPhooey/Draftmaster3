using System.Collections.Generic;
using System.Linq;
using Draftmaster.MasterDraft;
using NUnit.Framework;
using UnityEngine;

// Master Draft's pure rules (Assets/Scripts/MasterDraft/Core): packs, team legality, the in-race hand,
// coin rewards and the saved profile. The catalogue is built from generated stand-ins, the same path the
// game takes when the career database isn't ready.
public class MasterDraftRulesTests
{
    static CardCatalog Catalog() => new CardCatalog(null, null);

    static MasterDraftProfile NewProfile(string id = "test-player") => MasterDraftProfile.New(id);

    // ------------------------------------------------------------------ packs

    [Test]
    public void StarterPackAlwaysHasEveryCardTypeAndALegalDeck()
    {
        var catalog = Catalog();
        for (long seed = 0; seed < 1500; seed++)
        {
            var cards = PackGenerator.Open(PackKind.Starter, catalog, seed * 7919 + 3);
            Assert.AreEqual(PackGenerator.StarterSize, cards.Count, $"seed {seed}: wrong size");
            foreach (CardType t in System.Enum.GetValues(typeof(CardType)))
                Assert.IsTrue(cards.Any(c => c.type == t), $"seed {seed}: no {t} card");

            // A starter on its own fields a legal deck: five actions, no more than two of any one.
            var actions = cards.Where(c => c.type == CardType.Action).ToList();
            Assert.GreaterOrEqual(actions.Count, TeamRules.MinDeck, $"seed {seed}: too few actions for a deck");
            Assert.IsTrue(actions.All(a => ActionCatalog.IsPlayable(a.effect)), $"seed {seed}: unplayable action dropped");
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
            Assert.IsTrue(cards.All(c => c.type != CardType.Action || ActionCatalog.IsPlayable(c.effect)),
                          $"seed {seed}: a not-yet-implemented action dropped");
            foreach (var c in cards) counts[(int)c.rarity]++;
        }

        // Commons are the bulk, Legendaries are rare but do drop.
        int total = packs * PackGenerator.StandardSize;
        Assert.Greater(counts[(int)Rarity.Common], total * 0.35, "too few commons");
        Assert.Greater(counts[(int)Rarity.Legendary], 0, "no legendary in 2000 packs");
        Assert.Less(counts[(int)Rarity.Legendary], total * 0.08, "legendaries too common");
    }

    [Test]
    public void UnimplementedActionsAreCataloguedButNeverDrop()
    {
        var catalog = Catalog();
        foreach (var spec in ActionCatalog.All.Where(s => !s.Implemented))
            Assert.IsNotNull(catalog.Find(ActionCatalog.DefinitionId(spec.Effect)), $"{spec.Name} missing from the catalogue");
        var rng = new DraftRng(1);
        for (int i = 0; i < 2000; i++)
        {
            var pick = catalog.Pick(CardType.Action, (Rarity)(i % 4), rng);
            Assert.IsTrue(ActionCatalog.IsPlayable(pick.effect), $"{pick.name} is not playable but dropped");
        }
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
        var playable = ActionCatalog.All.Where(s => s.Implemented).ToList();
        for (int i = 0; i < TeamRules.MinDeck; i++)
            team.actionDeck.Add(p.Add(ActionCatalog.Definition(playable[i % playable.Count])).uid);
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

        (p, team) = LegalTeam();
        team.actionDeck.RemoveAt(0);
        StringAssert.Contains("at least", string.Join(" ", TeamRules.Validate(team, p)));

        // Three copies of one action.
        (p, team) = LegalTeam();
        var push = ActionCatalog.Definition(ActionCatalog.Get(ActionEffect.PushToPass));
        for (int i = 0; i < 3; i++) team.actionDeck.Add(p.Add(push).uid);
        StringAssert.Contains("copies", string.Join(" ", TeamRules.Validate(team, p)));

        // Too big a deck.
        (p, team) = LegalTeam();
        foreach (var spec in ActionCatalog.All.Where(s => s.Implemented))
            for (int i = 0; i < 2; i++) team.actionDeck.Add(p.Add(ActionCatalog.Definition(spec)).uid);
        if (team.actionDeck.Count <= TeamRules.MaxDeck)
            while (team.actionDeck.Count <= TeamRules.MaxDeck) team.actionDeck.Add(team.actionDeck[0]);
        StringAssert.Contains("at most", string.Join(" ", TeamRules.Validate(team, p)));

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

    // ------------------------------------------------------------------ race hand

    static List<CardDefinition> Deck(int n)
    {
        var playable = ActionCatalog.All.Where(s => s.Implemented).ToList();
        var deck = new List<CardDefinition>();
        for (int i = 0; i < n; i++) deck.Add(ActionCatalog.Definition(playable[i % playable.Count]));
        return deck;
    }

    [Test]
    public void EachCrossingDrawsOneCardUpToTheHandCap()
    {
        var hand = new RaceHand(Deck(10), 1, openingHand: 0);
        Assert.AreEqual(0, hand.Hand.Count);
        for (int i = 1; i <= RaceHand.HandCap; i++)
        {
            Assert.IsNotNull(hand.OnStartFinishCrossed());
            Assert.AreEqual(i, hand.Hand.Count);
        }
        // Full: the draw is skipped and the card stays in the deck.
        int pile = hand.DrawPileCount;
        Assert.IsNull(hand.OnStartFinishCrossed());
        Assert.AreEqual(RaceHand.HandCap, hand.Hand.Count);
        Assert.AreEqual(pile, hand.DrawPileCount);
        Assert.AreEqual(RaceHand.HandCap + 1, hand.Crossings);
    }

    [Test]
    public void OpeningHandIsDealtAtTheGreen()
    {
        var hand = new RaceHand(Deck(6), 3);
        Assert.AreEqual(RaceHand.OpeningHand, hand.Hand.Count);
    }

    [Test]
    public void PlayingRemovesFromHandAndDiscardsReshuffleWhenTheDeckRunsOut()
    {
        var hand = new RaceHand(Deck(5), 2, openingHand: 0);
        for (int i = 0; i < 5; i++) hand.OnStartFinishCrossed();
        Assert.AreEqual(0, hand.DrawPileCount);

        var played = hand.Play(0);
        Assert.IsNotNull(played);
        Assert.AreEqual(4, hand.Hand.Count);
        Assert.AreEqual(1, hand.DiscardCount);
        Assert.AreEqual(1, hand.CardsPlayed);
        Assert.IsNull(hand.Play(9), "a bad index played something");

        // Deck empty: the next crossing shuffles the discard back in and draws it.
        var drawn = hand.OnStartFinishCrossed();
        Assert.AreSame(played, drawn);
        Assert.AreEqual(0, hand.DiscardCount);
        Assert.AreEqual(5, hand.Hand.Count);
    }

    [Test]
    public void EmptyDeckNeverDraws()
    {
        var hand = new RaceHand(new List<CardDefinition>(), 1);
        Assert.IsNull(hand.OnStartFinishCrossed());
        Assert.AreEqual(0, hand.Hand.Count);
    }

    [Test]
    public void UnplayableCardsStayInTheHand()
    {
        var spec = ActionCatalog.All.FirstOrDefault(s => !s.Implemented);
        if (spec == null) Assert.Ignore("Every action is implemented.");
        var hand = new RaceHand(new[] { ActionCatalog.Definition(spec) }, 1);
        Assert.IsNull(hand.Play(0));
        Assert.AreEqual(1, hand.Hand.Count);
    }

    // ------------------------------------------------------------------ economy + profile

    [Test]
    public void CoinRewardsFollowTheFinish()
    {
        Assert.AreEqual(Economy.WinnerCoins, Economy.ForFinish(1, 0));
        Assert.AreEqual(Economy.WinnerCoins - Economy.CoinsLostPerPlace, Economy.ForFinish(2, 0));
        Assert.AreEqual(Economy.MinimumFinishCoins, Economy.ForFinish(40, 0));
        Assert.AreEqual(Economy.MinimumFinishCoins, Economy.ForFinish(0, 0), "DNF pays the minimum");
        Assert.AreEqual(Economy.WinnerCoins + 3 * Economy.CoinsPerCardPlayed, Economy.ForFinish(1, 3));
        Assert.AreEqual(Economy.WinnerCoins + Economy.MaxCardsPaid * Economy.CoinsPerCardPlayed, Economy.ForFinish(1, 99),
                        "card bonus is capped");

        var p = NewProfile();
        int before = p.coins;
        var r = ProfileOps.AwardRace(p, 1, 2);
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
        Assert.AreEqual(p.team.actionDeck, back.team.actionDeck);
        Assert.AreEqual(p.cards[0].def.name, back.cards[0].def.name);
        Assert.IsTrue(back.starterClaimed);
        Assert.AreEqual(MasterDraftProfile.CurrentSchema, back.schemaVersion);

        Assert.IsNull(MasterDraftProfile.FromJson(""));
        Assert.IsNull(MasterDraftProfile.FromJson("not json"));
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
        var wrongType = (CardType)(((int)offer.wantType + 1) % 4);
        var rng = new DraftRng(4);
        var wrong = p.Add(catalog.Pick(wrongType, Rarity.Legendary, rng));
        Assert.IsFalse(TradeRules.Accept(p, offer.id, wrong.uid).ok);

        var right = p.Add(catalog.Pick(offer.wantType, Rarity.Legendary, rng));
        p.team.actionDeck.Add(right.uid);
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

    [Test]
    public void TimedActionsRunForTheirSecondsAndDoNotStack()
    {
        var fx = new ActiveEffects();
        var push = ActionCatalog.Get(ActionEffect.PushToPass);
        Assert.IsTrue(fx.Start(push, 10f));
        Assert.AreEqual(1f + push.Magnitude, fx.PowerScale(10f), 1e-5f);
        Assert.AreEqual(1f + push.Magnitude, fx.PowerScale(10f + push.Seconds - 0.01f), 1e-5f);
        Assert.AreEqual(1f, fx.PowerScale(10f + push.Seconds + 0.01f), 1e-5f);

        // Played again while running: the clock restarts, the boost doesn't double.
        fx.Start(push, 20f);
        fx.Start(push, 21f);
        Assert.AreEqual(1f + push.Magnitude, fx.PowerScale(22f), 1e-5f);
        Assert.AreEqual(push.Seconds - 1f, fx.Remaining(ActionEffect.PushToPass, 22f), 1e-4f);

        var slip = ActionCatalog.Get(ActionEffect.Slipstream);
        fx.Start(slip, 0f);
        Assert.AreEqual(slip.Magnitude, fx.DraftScale(1f), 1e-5f);
        Assert.AreEqual(1f, fx.DraftScale(slip.Seconds + 1f), 1e-5f);

        var fuel = ActionCatalog.Get(ActionEffect.FuelSaver);
        fx.Start(fuel, 0f);
        Assert.AreEqual(1f - fuel.Magnitude, fx.FuelBurnScale(1f), 1e-5f);

        // Instant cards start nothing.
        Assert.IsFalse(fx.Start(ActionCatalog.Get(ActionEffect.FreshRubber), 0f));
        Assert.IsFalse(fx.Start(ActionCatalog.Get(ActionEffect.QuickRepair), 0f));
    }

    [Test]
    public void PerfectPitStopWaitsForTheNextStop()
    {
        var fx = new ActiveEffects();
        Assert.AreEqual(1f, fx.PitTimeScale);
        var spec = ActionCatalog.Get(ActionEffect.PerfectPitStop);
        fx.Start(spec, 0f);
        Assert.IsTrue(fx.PerfectPitPending);
        Assert.AreEqual(spec.Magnitude, fx.PitTimeScale, 1e-5f);
        Assert.AreEqual(spec.Magnitude, fx.PitTimeScale, 1e-5f, "it lasts until a stop, not a clock");
        fx.PitStopUsed();
        Assert.IsFalse(fx.PerfectPitPending);
        Assert.AreEqual(1f, fx.PitTimeScale);
    }
}
