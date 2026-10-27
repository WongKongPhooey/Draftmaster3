using System.Collections.Generic;
using Draftmaster.Weekend;
using NUnit.Framework;

// Radio row: a reporter asks the player to name one of four drivers from the field, and the answer moves the
// player's relationship with whoever they named.
public class RadioRowContentTests
{
    static List<RadioRowContent.FieldDriver> Field(int n)
    {
        var list = new List<RadioRowContent.FieldDriver>();
        for (int i = 0; i < n; i++) list.Add(new RadioRowContent.FieldDriver($"Driver Number{i}", $"Number{i}"));
        return list;
    }

    static WeekendActivity RadioRow() => new WeekendActivity
    {
        id = "2.615.31", kind = ActivityKind.MediaHit, title = RadioRowContent.Title,
    };

    [Test]
    public void OnlyTheRadioRowMediaHitIsRadioRow()
    {
        Assert.IsTrue(RadioRowContent.Is(RadioRow()));
        Assert.IsFalse(RadioRowContent.Is(new WeekendActivity { kind = ActivityKind.MediaHit, title = "Podcast Taping" }));
        Assert.IsFalse(RadioRowContent.Is(new WeekendActivity { kind = ActivityKind.PressConference, title = RadioRowContent.Title }));
        Assert.IsFalse(RadioRowContent.Is(null));
    }

    [Test]
    public void AsksOneQuestion_WithFourDistinctDriversFromTheField()
    {
        var field = Field(30);
        var c = RadioRowContent.Build(RadioRow(), 5, field);

        Assert.IsNotNull(c);
        Assert.AreEqual(1, c.beats.Count);
        var beat = c.beats[0];
        Assert.AreEqual(RadioRowContent.Choices, beat.choices.Count);
        StringAssert.DoesNotContain("Crew Chief", beat.speaker);
        Assert.IsFalse(string.IsNullOrEmpty(beat.speaker), "the reporter is named");

        var keys = new HashSet<string>();
        foreach (var choice in beat.choices)
        {
            Assert.IsTrue(keys.Add(choice.rivalName), $"{choice.rivalName} offered twice");
            Assert.IsTrue(field.Exists(d => d.key == choice.rivalName), $"{choice.rivalName} is not in the field");
            Assert.AreNotEqual(0f, choice.rivalDelta, "naming a driver moves the relationship");
            StringAssert.Contains("Driver " + choice.rivalName, choice.text, "the answer names the driver");
        }
    }

    [Test]
    public void EveryChoiceMovesTheRelationshipTheSameWay_ForThatQuestion()
    {
        // A question is either a compliment or a dig: every answer to it goes the same direction.
        bool sawPraise = false, sawDig = false;
        for (int week = 1; week <= 60; week++)
        {
            var beat = RadioRowContent.Build(RadioRow(), week, Field(20)).beats[0];
            float sign = beat.choices[0].rivalDelta;
            foreach (var choice in beat.choices)
                Assert.AreEqual(sign, choice.rivalDelta, beat.question);
            if (sign > 0f) sawPraise = true;
            if (sign < 0f) sawDig = true;
        }
        Assert.IsTrue(sawPraise, "some questions flatter the driver named");
        Assert.IsTrue(sawDig, "some questions dig at the driver named");
    }

    [Test]
    public void SameWeekend_SameQuestionAndDrivers()
    {
        var a = RadioRowContent.Build(RadioRow(), 9, Field(25)).beats[0];
        var b = RadioRowContent.Build(RadioRow(), 9, Field(25)).beats[0];
        Assert.AreEqual(a.question, b.question);
        for (int i = 0; i < a.choices.Count; i++)
            Assert.AreEqual(a.choices[i].rivalName, b.choices[i].rivalName);
    }

    [Test]
    public void SmallOrEmptyField()
    {
        Assert.IsNull(RadioRowContent.Build(RadioRow(), 1, Field(0)), "nobody to name: fall back to a plain media hit");
        Assert.AreEqual(2, RadioRowContent.Build(RadioRow(), 1, Field(2)).beats[0].choices.Count);
    }

    [Test]
    public void SettledOutcome_CarriesTheNamedDriverToTheRelationshipHook()
    {
        var c = RadioRowContent.Build(RadioRow(), 3, Field(10));
        var choice = c.beats[0].choices[2];

        var running = WeekendOutcome.Nothing;
        running.score = 0f;
        WeekendConversation.Accumulate(ref running, choice);
        var o = c.Settle(running, 1);

        Assert.AreEqual(choice.rivalName, o.rivalName);
        Assert.AreEqual(choice.rivalDelta, o.rivalDelta);
        Assert.IsFalse(string.IsNullOrEmpty(o.headline));
    }
}
