using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Draftmaster.Progression;

// EditMode coverage for the paddock drinks machine's rules (WeekendDrinks): the machine is restocked from
// the weekend id alone and must come back with the same four cans after every scene reload, a can trades
// one career attribute for another in equal measure, only one can is taken per weekend, and the swing is
// gone the moment the weekend id moves on. None of that can be play-tested with the editor unfocused.
public class WeekendDrinksTests
{
    // Must match WeekendDrinks' private keys / CareerPath's stat prefix.
    const string WeekendKey = "vending.weekend";
    const string PickKey = "vending.pick";
    const string StatPrefix = "stat.";

    readonly Dictionary<string, int> _saved = new();

    [SetUp]
    public void Before()
    {
        // These tests write to the real save's PlayerPrefs. Keep everything they touch and put it back.
        _saved.Clear();
        Keep(WeekendKey);
        Keep(PickKey);
        foreach (var key in CareerPath.StatKeys) Keep(StatPrefix + key);

        WeekendDrinks.Clear();
    }

    [TearDown]
    public void After()
    {
        WeekendDrinks.Clear();
        foreach (var key in CareerPath.StatKeys) PlayerPrefs.DeleteKey(StatPrefix + key);

        foreach (var kv in _saved) PlayerPrefs.SetInt(kv.Key, kv.Value);
        PlayerPrefs.Save();
    }

    void Keep(string key)
    {
        if (PlayerPrefs.HasKey(key)) _saved[key] = PlayerPrefs.GetInt(key);
    }

    // ---------------------------------------------------------------- the rack

    [Test]
    public void Selection_IsFourCansWithLegalTrades()
    {
        for (int weekend = 0; weekend < 40; weekend++)
        {
            var drinks = WeekendDrinks.Selection(weekend);
            Assert.AreEqual(WeekendDrinks.SelectionSize, drinks.Length, $"weekend {weekend}");

            foreach (var drink in drinks)
            {
                Assert.IsTrue(drink.IsValid, $"weekend {weekend}: {drink.name} is not a usable can");
                Assert.Contains(drink.boostKey, CareerPath.StatKeys, $"weekend {weekend}: {drink.name}");
                Assert.Contains(drink.drainKey, CareerPath.StatKeys, $"weekend {weekend}: {drink.name}");
                Assert.AreNotEqual(drink.boostKey, drink.drainKey,
                                   $"weekend {weekend}: {drink.name} gives and takes the same attribute");
                Assert.GreaterOrEqual(drink.amount, WeekendDrinks.MinSwing);
                Assert.LessOrEqual(drink.amount, WeekendDrinks.MaxSwing);
            }
        }
    }

    [Test]
    public void Selection_HasNoRepeatedCanOrRepeatedTrade()
    {
        for (int weekend = 0; weekend < 40; weekend++)
        {
            var drinks = WeekendDrinks.Selection(weekend);
            var names = new HashSet<string>();
            var trades = new HashSet<string>();

            foreach (var drink in drinks)
            {
                Assert.IsTrue(names.Add(drink.name), $"weekend {weekend}: {drink.name} stocked twice");
                Assert.IsTrue(trades.Add($"{drink.boostKey}>{drink.drainKey}x{drink.amount}"),
                              $"weekend {weekend}: two cans offer the identical trade");
            }
        }
    }

    [Test]
    public void Selection_IsTheSameEveryTimeForTheSameWeekend()
    {
        // The race scene reloads between practice, qualifying and the race: the machine has to still have
        // Friday's cans in it on Sunday.
        var first = WeekendDrinks.Selection(7);
        var again = WeekendDrinks.Selection(7);

        for (int i = 0; i < first.Length; i++)
        {
            Assert.AreEqual(first[i].name, again[i].name);
            Assert.AreEqual(first[i].boostKey, again[i].boostKey);
            Assert.AreEqual(first[i].drainKey, again[i].drainKey);
            Assert.AreEqual(first[i].amount, again[i].amount);
        }
    }

    [Test]
    public void Selection_ChangesFromOneWeekendToTheNext()
    {
        int different = 0;
        for (int weekend = 0; weekend < 20; weekend++)
        {
            var a = WeekendDrinks.Selection(weekend);
            var b = WeekendDrinks.Selection(weekend + 1);
            if (a[0].name != b[0].name || a[0].boostKey != b[0].boostKey || a[0].amount != b[0].amount)
                different++;
        }
        Assert.Greater(different, 15, "the machine is restocked each weekend, not once and for all");
    }

    // ---------------------------------------------------------------- taking one

    [Test]
    public void FreshWeekend_HasTakenNothing()
    {
        Assert.IsFalse(WeekendDrinks.HasTaken(3));
        Assert.AreEqual(-1, WeekendDrinks.TakenIndex(3));
        foreach (var key in CareerPath.StatKeys) Assert.AreEqual(0, WeekendDrinks.Bonus(key, 3));
    }

    [Test]
    public void Take_AppliesEqualAndOppositeSwing()
    {
        var drink = WeekendDrinks.Selection(11)[2];
        Assert.IsTrue(WeekendDrinks.Take(11, 2));

        Assert.IsTrue(WeekendDrinks.HasTaken(11));
        Assert.AreEqual(drink.amount, WeekendDrinks.Bonus(drink.boostKey, 11));
        Assert.AreEqual(-drink.amount, WeekendDrinks.Bonus(drink.drainKey, 11));

        int total = 0;
        foreach (var key in CareerPath.StatKeys) total += WeekendDrinks.Bonus(key, 11);
        Assert.AreEqual(0, total, "a can gives and takes the same amount");
    }

    [Test]
    public void Take_IsOncePerWeekend()
    {
        Assert.IsTrue(WeekendDrinks.Take(4, 0));
        Assert.IsFalse(WeekendDrinks.Take(4, 1), "a second can in the same weekend");
        Assert.AreEqual(0, WeekendDrinks.TakenIndex(4), "the first can is the one that counts");
    }

    [Test]
    public void Take_RejectsACanThatIsNotInTheMachine()
    {
        Assert.IsFalse(WeekendDrinks.Take(4, -1));
        Assert.IsFalse(WeekendDrinks.Take(4, WeekendDrinks.SelectionSize));
        Assert.IsFalse(WeekendDrinks.HasTaken(4));
    }

    [Test]
    public void Swing_ExpiresWhenTheWeekendMovesOn()
    {
        var drink = WeekendDrinks.Selection(9)[1];
        Assert.IsTrue(WeekendDrinks.Take(9, 1));
        Assert.AreEqual(drink.amount, WeekendDrinks.Bonus(drink.boostKey, 9));

        // Next weekend: the pick is stamped with the old id, so it reads as nothing taken.
        Assert.IsFalse(WeekendDrinks.HasTaken(10));
        Assert.AreEqual(0, WeekendDrinks.Bonus(drink.boostKey, 10));
        Assert.AreEqual(0, WeekendDrinks.Bonus(drink.drainKey, 10));
    }

    [Test]
    public void EffectiveStat_IsTheCareerValuePlusTheSwing()
    {
        var drink = WeekendDrinks.Selection(6)[0];
        PlayerPrefs.SetInt(StatPrefix + drink.boostKey, 5);
        PlayerPrefs.SetInt(StatPrefix + drink.drainKey, 5);

        Assert.AreEqual(5, WeekendDrinks.EffectiveStat(drink.boostKey, 6), "nothing drunk yet");

        Assert.IsTrue(WeekendDrinks.Take(6, 0));
        Assert.AreEqual(5 + drink.amount, WeekendDrinks.EffectiveStat(drink.boostKey, 6));
        Assert.AreEqual(5 - drink.amount, WeekendDrinks.EffectiveStat(drink.drainKey, 6));

        // The ledger itself is untouched: the swing is an overlay, so it can never leave a career dented.
        Assert.AreEqual(5, CareerPath.Stat(drink.boostKey));
        Assert.AreEqual(5, CareerPath.Stat(drink.drainKey));
    }

    // ---------------------------------------------------------------- what the popup reads

    [Test]
    public void Label_NamesTheCanAndBothHalvesOfTheTrade()
    {
        var drink = WeekendDrinks.Selection(2)[0];
        string label = WeekendDrinks.Label(drink);

        StringAssert.Contains(drink.name, label);
        StringAssert.Contains("+" + drink.amount, label);
        StringAssert.Contains("-" + drink.amount, label);
        StringAssert.Contains(WeekendDrinks.StatLabel(drink.boostKey), label);
        StringAssert.Contains(WeekendDrinks.StatLabel(drink.drainKey), label);
    }

    [Test]
    public void StatLabel_ReadsAsWordsNotKeys()
    {
        Assert.AreEqual("Driving", WeekendDrinks.StatLabel(CareerPath.StatDriving));
        Assert.AreEqual("Pit Craft", WeekendDrinks.StatLabel(CareerPath.StatPitCraft));
        Assert.AreEqual("Engineering", WeekendDrinks.StatLabel(CareerPath.StatEngineering));
        Assert.AreEqual("Business", WeekendDrinks.StatLabel(CareerPath.StatBusiness));
        Assert.AreEqual("Scouting", WeekendDrinks.StatLabel(CareerPath.StatScouting));
        Assert.AreEqual("", WeekendDrinks.StatLabel(null));
    }
}
