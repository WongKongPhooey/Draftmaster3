using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// EditMode coverage for the crew gathering round the crew chief at the team strategy briefing.
//
// The ring is the whole feature: every crew member the same distance from the chief, spread round him, and
// the side the driver walks up from left open so they step into the gap rather than into somebody's back.
// And it is for the briefing only — the phone orientation is held at the same venue and is not a meeting.
//
// BriefingHuddle lives in Assembly-CSharp, which an asmdef can't reference, so it is reached by reflection.
public class BriefingHuddleTests
{
    static readonly Type HuddleType = Type.GetType("BriefingHuddle, Assembly-CSharp");
    static readonly Type ActivityType = Type.GetType("Draftmaster.Weekend.WeekendActivity, Draftmaster.Weekend");
    static readonly Type KindType = Type.GetType("Draftmaster.Weekend.ActivityKind, Draftmaster.Weekend");
    static readonly Type VenueType = Type.GetType("Draftmaster.Weekend.WeekendVenue, Draftmaster.Weekend");

    static Vector2[] Places(Vector2 centre, Vector2 toward, int count, float radius, float gap) =>
        (Vector2[])HuddleType.GetMethod("Places").Invoke(null, new object[] { centre, toward, count, radius, gap });

    static bool Wanted(string kind, string venue)
    {
        object activity = null;
        if (kind != null)
        {
            activity = Activator.CreateInstance(ActivityType);
            ActivityType.GetField("kind").SetValue(activity, Enum.Parse(KindType, kind));
        }
        return (bool)HuddleType.GetMethod("Wanted").Invoke(null, new[] { activity, Enum.Parse(VenueType, venue) });
    }

    [Test]
    public void TypesExist()
    {
        Assert.IsNotNull(HuddleType, "BriefingHuddle is gone — the chief briefs on his own again.");
        Assert.IsNotNull(ActivityType);
        Assert.IsNotNull(KindType);
        Assert.IsNotNull(VenueType);
    }

    [Test]
    public void EveryCrewMember_StandsTheSameDistanceFromTheChief()
    {
        var centre = new Vector2(-300f, 60f);
        foreach (var p in Places(centre, Vector2.down, 5, 1.5f, 110f))
            Assert.AreEqual(1.5f, Vector2.Distance(p, centre), 1e-3f);
    }

    [Test]
    public void TheSideThePlayerWalksUpFrom_IsLeftOpen()
    {
        var centre = Vector2.zero;
        Vector2 toward = new Vector2(1f, -1f).normalized;
        foreach (var p in Places(centre, toward, 5, 1.5f, 110f))
        {
            float off = Vector2.Angle(toward, p - centre);
            Assert.Greater(off, 55f, $"A crew member at {p} is stood in the gap the driver walks into.");
        }
    }

    [Test]
    public void NobodyStandsOnTopOfAnybodyElse()
    {
        var places = Places(Vector2.zero, Vector2.down, 5, 1.5f, 110f);
        for (int i = 0; i < places.Length; i++)
            for (int j = i + 1; j < places.Length; j++)
                Assert.Greater(Vector2.Distance(places[i], places[j]), 0.6f, "Two of the crew overlap.");
    }

    [Test]
    public void CrewGather_ForTheStrategyBriefingOnly()
    {
        Assert.IsTrue(Wanted("TeamBriefing", "PitBox"), "No crew at the strategy briefing.");
        Assert.IsFalse(Wanted("Orientation", "PitBox"), "The phone orientation is not a team meeting.");
        Assert.IsFalse(Wanted(null, "PitBox"), "Crew stood round the chief with nothing booked.");
        Assert.IsFalse(Wanted("TeamBriefing", "Motorhome"), "A huddle at a venue the briefing is not held at.");
    }
}
