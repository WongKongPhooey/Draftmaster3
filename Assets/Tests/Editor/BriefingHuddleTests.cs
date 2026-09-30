using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// EditMode coverage for the crew gathering in front of the crew chief at the team strategy briefing.
//
// The arc is the whole feature: the crew stood in front of the chief on the side the driver walks up from,
// all within a 70° arc, none of them on an exact mark — and the same nudges each meeting, so they do not
// shuffle about. And it is for the briefing only — the phone orientation is held at the same venue and is
// not a meeting.
//
// BriefingHuddle and CrewChiefPresence live in Assembly-CSharp, which an asmdef can't reference, so they are
// reached by reflection.
public class BriefingHuddleTests
{
    static readonly Type HuddleType = Type.GetType("BriefingHuddle, Assembly-CSharp");
    static readonly Type PresenceType = Type.GetType("CrewChiefPresence, Assembly-CSharp");
    static readonly Type ActivityType = Type.GetType("Draftmaster.Weekend.WeekendActivity, Draftmaster.Weekend");
    static readonly Type KindType = Type.GetType("Draftmaster.Weekend.ActivityKind, Draftmaster.Weekend");
    static readonly Type VenueType = Type.GetType("Draftmaster.Weekend.WeekendVenue, Draftmaster.Weekend");

    const float Radius = 2.1f, Arc = 70f;

    static Vector2[] Places(Vector2 centre, Vector2 toward, int count, float radius, float arc,
                            int seed = 0, float radiusJitter = 0f, float angleJitter = 0f) =>
        (Vector2[])HuddleType.GetMethod("Places").Invoke(null,
            new object[] { centre, toward, count, radius, arc, seed, radiusJitter, angleJitter });

    static float[] FacingOffsets(int count, int seed, float jitter) =>
        (float[])HuddleType.GetMethod("FacingOffsets").Invoke(null, new object[] { count, seed, jitter });

    static float Bearing(Vector2 centre, Vector2 at, Vector2 front) => Vector2.SignedAngle(front, at - centre);

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
        Assert.IsNotNull(PresenceType, "CrewChiefPresence is gone — two crew chiefs in the paddock again.");
        Assert.IsNotNull(ActivityType);
        Assert.IsNotNull(KindType);
        Assert.IsNotNull(VenueType);
    }

    [Test]
    public void WithoutJitter_TheCrewSpreadEvenlyAcrossA70DegreeArc_InFrontOfTheChief()
    {
        var centre = new Vector2(-300f, 60f);
        var front = Vector2.down;
        var places = Places(centre, front, 5, Radius, Arc);

        Assert.AreEqual(5, places.Length);
        foreach (var p in places)
        {
            Assert.AreEqual(Radius, Vector2.Distance(p, centre), 1e-3f);
            Assert.LessOrEqual(Mathf.Abs(Bearing(centre, p, front)), Arc * 0.5f + 1e-3f, $"{p} is outside the arc.");
        }
        // Symmetric about the front; the ends half a share (7°) in from the arc's edges.
        Assert.AreEqual(-28f, Bearing(centre, places[0], front), 1e-3f);
        Assert.AreEqual(0f, Bearing(centre, places[2], front), 1e-3f);
        Assert.AreEqual(28f, Bearing(centre, places[4], front), 1e-3f);
    }

    [Test]
    public void WithJitter_NobodyIsOnAnExactMark_ButEverybodyStaysInTheArc_InOrder()
    {
        var centre = Vector2.zero;
        Vector2 front = new Vector2(1f, -1f).normalized;
        var places = Places(centre, front, 5, Radius, Arc, 1234, 0.2f, 3f);

        bool offMark = false;
        float last = -999f;
        foreach (var p in places)
        {
            float r = Vector2.Distance(centre, p);
            Assert.That(r, Is.InRange(Radius - 0.2f - 1e-4f, Radius + 0.2f + 1e-4f), $"{p} strayed {r} m out.");
            if (Mathf.Abs(r - Radius) > 1e-3f) offMark = true;

            float b = Bearing(centre, p, front);
            Assert.LessOrEqual(Mathf.Abs(b), Arc * 0.5f, $"{p} jittered out of the arc.");
            Assert.Greater(b, last, "Two of the crew swapped places.");
            last = b;
        }
        Assert.IsTrue(offMark, "Jitter asked for, but everybody is on the exact radius.");
    }

    [Test]
    public void TheSameMeeting_PutsEverybodyInTheSamePlace()
    {
        var a = Places(Vector2.zero, Vector2.down, 5, Radius, Arc, 99, 0.2f, 3f);
        var b = Places(Vector2.zero, Vector2.down, 5, Radius, Arc, 99, 0.2f, 3f);
        for (int i = 0; i < a.Length; i++) Assert.AreEqual(a[i], b[i], "The crew shuffled between meetings.");
    }

    [Test]
    public void NobodyStandsOnTopOfAnybodyElse()
    {
        var places = Places(Vector2.zero, Vector2.down, 5, Radius, Arc, 1234, 0.2f, 3f);
        for (int i = 0; i < places.Length; i++)
            for (int j = i + 1; j < places.Length; j++)
                Assert.Greater(Vector2.Distance(places[i], places[j]), 0.25f, "Two of the crew overlap.");
    }

    [Test]
    public void EverybodyFacesTheChief_ButNotAllSquareOn()
    {
        var turn = FacingOffsets(5, 1234, 12f);
        Assert.AreEqual(5, turn.Length);
        bool varied = false;
        foreach (var t in turn)
        {
            Assert.LessOrEqual(Mathf.Abs(t), 12f, "Turned so far they are not facing the chief.");
            if (Mathf.Abs(t - turn[0]) > 0.5f) varied = true;
        }
        Assert.IsTrue(varied, "Everybody turned by the same amount.");
    }

    [Test]
    public void CrewGather_ForTheStrategyBriefingOnly()
    {
        Assert.IsTrue(Wanted("TeamBriefing", "PitBox"), "No crew at the strategy briefing.");
        Assert.IsFalse(Wanted("Orientation", "PitBox"), "The phone orientation is not a team meeting.");
        Assert.IsFalse(Wanted(null, "PitBox"), "Crew stood round the chief with nothing booked.");
        Assert.IsFalse(Wanted("TeamBriefing", "Motorhome"), "A huddle at a venue the briefing is not held at.");
    }

    static bool PitBoxOnDuty(bool sessionLive, bool pitBoxExists) =>
        (bool)PresenceType.GetMethod("PitBoxOnDuty").Invoke(null, new object[] { sessionLive, pitBoxExists });

    [Test]
    public void OneCrewChief_ByTheCarOnlyWhileTheDriversSessionIsLive()
    {
        Assert.IsTrue(PitBoxOnDuty(true, true), "No chief at the pit box while the player is out driving.");
        Assert.IsFalse(PitBoxOnDuty(false, true), "The chief is at the pit box with no session running.");
        Assert.IsFalse(PitBoxOnDuty(true, false), "No pit-box chief built, and the venue one was sent away too.");
    }

    static Vector2 Step(Vector2 from, Vector2 to, float speed, float dt, float arrive, out bool arrived)
    {
        var args = new object[] { from, to, speed, dt, arrive, false };
        var r = (Vector2)HuddleType.GetMethod("StepToward").Invoke(null, args);
        arrived = (bool)args[5];
        return r;
    }

    [Test]
    public void AfterTheMeeting_TheCrewWalkBack_RatherThanVanish()
    {
        Assert.IsNotNull(HuddleType.GetMethod("StepToward"), "The crew have no walk back to the pit box.");

        var from = Vector2.zero;
        var to = new Vector2(10f, 0f);
        var next = Step(from, to, 1.2f, 0.5f, 0.3f, out bool arrived);
        Assert.IsFalse(arrived, "Somebody ten metres from the box counted as back already.");
        Assert.AreEqual(0.6f, Vector2.Distance(from, next), 1e-4f, "A step is walking pace times the frame.");
        Assert.Less(Vector2.Distance(next, to), Vector2.Distance(from, to), "The step led away from the box.");
    }

    [Test]
    public void TheWalkBack_EndsOnTheBox_NeverPastIt()
    {
        var to = new Vector2(1f, 1f);
        var next = Step(new Vector2(0.9f, 1f), to, 5f, 1f, 0f, out bool arrived);
        Assert.IsTrue(arrived);
        Assert.AreEqual(to, next, "Overshot the pit box.");

        Step(new Vector2(1.1f, 1f), to, 1f, 0.02f, 0.3f, out arrived);
        Assert.IsTrue(arrived, "Within arrival radius but still walking.");
    }
}
