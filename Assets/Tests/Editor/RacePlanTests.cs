using System;
using System.Linq;
using Draftmaster.Weekend;
using NUnit.Framework;
using UnityEngine;

// The race plan: one meeting on race morning, in the motorhome the driver woke up in, where fuel, tyres and
// balance for the race are set. It replaced the old race-morning "race plan meeting" at the pit box, which
// set nothing. The race session then runs the plan rather than offering the pit-box setup panel.
public class RacePlanTests
{
    static readonly RacingSeries[] Series = { RacingSeries.Trucks, RacingSeries.National, RacingSeries.Cup };

    [Test]
    public void EveryWeekendHasOneRacePlanOnRaceMorningInTheMotorhome()
    {
        foreach (var s in Series)
        {
            var t = WeekendTimetable.Build(s, 11);
            var plans = t.Activities.Where(a => a.kind == ActivityKind.RacePlan).ToList();
            Assert.AreEqual(1, plans.Count, $"{s}: expected exactly one race plan.");

            var plan = plans[0];
            var race = t.PlayerSession(ActivityKind.Race);
            Assert.IsNotNull(race, $"{s}: no race on the sheet.");
            Assert.AreEqual(WeekendSlots.MorningOf(race.slot), plan.slot, $"{s}: the race plan is not on race morning.");
            Assert.AreEqual(WeekendVenue.Motorhome, WeekendVenues.For(plan.kind));
            Assert.AreEqual(WeekendVenues.ShortLabel(WeekendVenue.Motorhome), plan.location);
            Assert.IsTrue(ActivityKinds.IsTeam(plan.kind), "The race plan should read as a TEAM booking.");
        }
    }

    [Test]
    public void CupRacePlanIsFirstThingSundayMorning()
    {
        var t = WeekendTimetable.Build(RacingSeries.Cup, 3);
        var plan = t.FirstOfKind(ActivityKind.RacePlan);
        Assert.AreEqual(WeekendSlot.SundayAM, plan.slot);
        Assert.AreEqual(WeekendSlots.OpensAt(WeekendSlot.SundayAM), plan.startMinute,
                        "The race plan should be the first thing after waking up.");
        foreach (var a in t.Activities)
            if (a != plan && a.slot == WeekendSlot.SundayAM)
                Assert.GreaterOrEqual(a.startMinute, plan.EndMinute, $"'{a.title}' is booked before or over the race plan.");
    }

    [Test]
    public void TruckRacePlanDoesNotLandOnTheStrategyBriefing()
    {
        var t = WeekendTimetable.Build(RacingSeries.Trucks, 3);
        var plan = t.FirstOfKind(ActivityKind.RacePlan);
        var briefing = t.FirstOfKind(ActivityKind.TeamBriefing);
        Assert.IsFalse(plan.ClashesWith(briefing), "The truck race plan clashes with the strategy briefing.");
        Assert.IsFalse(plan.ClashesWith(t.PlayerSession(ActivityKind.Practice)), "The truck race plan clashes with practice.");
    }

    [Test]
    public void TheOldRaceMorningMeetingIsGone()
    {
        foreach (var s in Series)
        {
            var t = WeekendTimetable.Build(s, 7);
            Assert.AreEqual(1, t.Activities.Count(a => a.kind == ActivityKind.TeamBriefing),
                            $"{s}: only the Friday strategy briefing should be a TeamBriefing now.");
            Assert.IsFalse(t.Activities.Any(a => a.title == "RACE PLAN MEETING"), $"{s}: the old race plan meeting is back.");
        }
    }

    [Test]
    public void RacePlanConversationLeadsIntoTheSetupSheet()
    {
        var a = WeekendTimetable.Build(RacingSeries.Cup, 3).FirstOfKind(ActivityKind.RacePlan);
        var script = TeamMeetingContent.Build(a);
        Assert.IsNotEmpty(script.beats);
        Assert.AreEqual("ENGINEER", script.beats[0].speaker, "The motorhome host is the engineer.");
        Assert.IsNotNull(script.headline);
    }

    [Test]
    public void MinimalCareerKeepsTheRacePlan()
    {
        Assert.IsTrue(CareerModes.Keeps(CareerMode.Minimal, ActivityKind.RacePlan));
        Assert.IsFalse(CareerModes.Keeps(CareerMode.DrivingOnly, ActivityKind.RacePlan));
    }

    [Test]
    public void PlanFilesCanBookTheRacePlan()
    {
        Assert.IsTrue(WeekendEventCatalog.TryGet("team-raceplan", out var type), "No catalogue entry for team-raceplan.");
        Assert.AreEqual(ActivityKind.RacePlan, type.kind);
    }

    // ------------------------------------------------------------------ the stored plan (Assembly-CSharp)

    static readonly string[] PlanKeys = { "raceplan.weekend", "raceplan.compound", "raceplan.fuel", "raceplan.balance" };

    [Test]
    public void RacePlanIsStoredForItsOwnWeekendOnly()
    {
        var setupType = Type.GetType("CarSetup, Assembly-CSharp");
        Assert.IsNotNull(setupType, "CarSetup is missing.");
        var save = setupType.GetMethod("SaveAsRacePlan");
        var load = setupType.GetMethod("LoadRacePlan");
        Assert.IsNotNull(save, "CarSetup.SaveAsRacePlan is missing.");
        Assert.IsNotNull(load, "CarSetup.LoadRacePlan is missing.");

        // Leave whatever the developer's own career had stored exactly as it was.
        bool had = PlayerPrefs.HasKey(PlanKeys[0]);
        int w = PlayerPrefs.GetInt(PlanKeys[0]);
        int c = PlayerPrefs.GetInt(PlanKeys[1]);
        float f = PlayerPrefs.GetFloat(PlanKeys[2]);
        float b = PlayerPrefs.GetFloat(PlanKeys[3]);
        try
        {
            var setup = Activator.CreateInstance(setupType);
            setupType.GetField("fuelLitres").SetValue(setup, 7f);
            setupType.GetField("balance").SetValue(setup, -0.5f);
            save.Invoke(setup, new object[] { 424242 });

            var back = load.Invoke(null, new object[] { 424242 });
            Assert.IsNotNull(back, "The plan saved for this weekend did not come back.");
            Assert.AreEqual(7f, (float)setupType.GetField("fuelLitres").GetValue(back), 0.001f);
            Assert.AreEqual(-0.5f, (float)setupType.GetField("balance").GetValue(back), 0.001f);

            Assert.IsNull(load.Invoke(null, new object[] { 424243 }), "Last weekend's race plan leaked into this one.");
        }
        finally
        {
            if (had)
            {
                PlayerPrefs.SetInt(PlanKeys[0], w);
                PlayerPrefs.SetInt(PlanKeys[1], c);
                PlayerPrefs.SetFloat(PlanKeys[2], f);
                PlayerPrefs.SetFloat(PlanKeys[3], b);
            }
            else foreach (var k in PlanKeys) PlayerPrefs.DeleteKey(k);
            PlayerPrefs.Save();
        }
    }
}
