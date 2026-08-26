using System.Reflection;
using Draftmaster.Weekend;
using NUnit.Framework;
using UnityEngine;

// The cast a track is laid out with, and what they say when.
//
// Two rules this covers. Every circuit has the same core people whatever series is running, because a
// paddock where the crew chief is somewhere different every round is a paddock nobody can learn. And a
// marker's dialogue can change across the three days — the crew chief on Friday is briefing you into a
// practice run and on Sunday into a race — which is the difference between a cast and a set of signposts.
//
// Reflection throughout: this assembly cannot reference Assembly-CSharp.
public class WeekendCastTests
{
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static
                           | BindingFlags.Public | BindingFlags.NonPublic;

    static System.Type Type(string name)
    {
        var type = System.Type.GetType(name + ", Assembly-CSharp");
        Assert.IsNotNull(type, $"{name} is gone from Assembly-CSharp; this test is out of date.");
        return type;
    }

    // Everybody a driver's day is built around, at every track, in every series.
    static readonly string[] CoreRoles =
    {
        "PitGreeter", "CrewChief", "RaceEngineer", "ChiefStrategist", "PRManager",
    };

    [Test]
    public void TheCoreCastHasEverybodyADriversDayIsBuiltAround()
    {
        var roleType = Type("PlacedNPC+Role");
        foreach (string role in CoreRoles)
            Assert.IsTrue(System.Enum.IsDefined(roleType, role),
                          $"There is no {role} role, so no track can be laid out with one.");
    }

    // Installing the cast into an empty scene creates every one of them, and running it twice does not
    // double them up — a track that has been dressed already must survive a second click.
    [Test]
    public void InstallingTheCoreCastIsCompleteAndRepeatable()
    {
        var defaults = Type("PlacedNPCDefaults");
        var placed = Type("PlacedNPC");
        var root = new GameObject("CastUnderTest");

        try
        {
            var ensure = defaults.GetMethod("EnsureCoreCast", Any);
            Assert.IsNotNull(ensure, "PlacedNPCDefaults.EnsureCoreCast is gone.");

            int first = (int)ensure.Invoke(null, new object[] { root.transform });
            Assert.Greater(first, 0, "Installing the core cast into an empty scene created nobody.");

            var built = root.GetComponentsInChildren(placed, true);
            foreach (string role in CoreRoles)
            {
                bool found = false;
                foreach (var npc in built)
                    if (npc.GetType().GetField("role").GetValue(npc).ToString() == role) { found = true; break; }
                Assert.IsTrue(found, $"The core cast installed nobody for {role}.");
            }

            int second = (int)ensure.Invoke(null, new object[] { root.transform });
            Assert.AreEqual(0, second, "Installing the cast a second time added duplicates.");
        }
        finally { Object.DestroyImmediate(root); }
    }

    // The crew chief is the case that motivated per-half-day dialogue: what he says depends on what session
    // he is briefing you into.
    [Test]
    public void TheCrewChiefSaysSomethingDifferentOnSunday()
    {
        var defaults = Type("PlacedNPCDefaults");
        var root = new GameObject("ChiefUnderTest");

        try
        {
            var chief = defaults.GetMethod("CreateChief", Any).Invoke(null, new object[] { root.transform });
            var linesFor = chief.GetType().GetMethod("LinesFor", Any);
            Assert.IsNotNull(linesFor, "PlacedNPC.LinesFor is gone; nothing reads the schedule.");

            var friday = (string[])linesFor.Invoke(chief, new object[] { WeekendSlot.FridayAM });
            var sunday = (string[])linesFor.Invoke(chief, new object[] { WeekendSlot.SundayPM });

            Assert.IsNotEmpty(friday, "The crew chief has nothing to say on Friday.");
            Assert.IsNotEmpty(sunday, "The crew chief has nothing to say on race day.");
            CollectionAssert.AreNotEqual(friday, sunday,
                                         "The crew chief briefs a practice run and a race with the same words.");
        }
        finally { Object.DestroyImmediate(root); }
    }

    // A half-day nothing covers falls back to the marker's own lines rather than going silent.
    [Test]
    public void AHalfDayNothingCoversFallsBackToTheDefaultLines()
    {
        var placed = Type("PlacedNPC");
        var go = new GameObject("MarkerUnderTest");

        try
        {
            var npc = go.AddComponent(placed);
            var defaultLines = new[] { "Standing line." };
            placed.GetField("lines").SetValue(npc, defaultLines);

            // One set, Sunday only.
            var setType = Type("PlacedNPC+ScheduledLines");
            var set = System.Activator.CreateInstance(setType);
            setType.GetField("lines").SetValue(set, new[] { "Race day line." });
            setType.GetMethod("Set", Any).Invoke(set, new object[] { WeekendSlot.SundayAM, true });

            var schedule = placed.GetField("schedule").GetValue(npc) as System.Collections.IList;
            schedule.Add(set);

            var linesFor = placed.GetMethod("LinesFor", Any);
            CollectionAssert.AreEqual(new[] { "Race day line." },
                                      (string[])linesFor.Invoke(npc, new object[] { WeekendSlot.SundayAM }));
            CollectionAssert.AreEqual(defaultLines,
                                      (string[])linesFor.Invoke(npc, new object[] { WeekendSlot.FridayAM }),
                                      "A half-day with no set should fall back to the marker's own lines.");
        }
        finally { Object.DestroyImmediate(go); }
    }

    // An empty set is not a set: it must not silence an NPC that has perfectly good default lines.
    [Test]
    public void AnEmptySetDoesNotSilenceAnyone()
    {
        var placed = Type("PlacedNPC");
        var go = new GameObject("MarkerUnderTest");

        try
        {
            var npc = go.AddComponent(placed);
            placed.GetField("lines").SetValue(npc, new[] { "Standing line." });

            var setType = Type("PlacedNPC+ScheduledLines");
            var set = System.Activator.CreateInstance(setType);
            setType.GetField("lines").SetValue(set, new string[0]);
            setType.GetMethod("Set", Any).Invoke(set, new object[] { WeekendSlot.FridayAM, true });
            (placed.GetField("schedule").GetValue(npc) as System.Collections.IList).Add(set);

            var lines = (string[])placed.GetMethod("LinesFor", Any)
                .Invoke(npc, new object[] { WeekendSlot.FridayAM });
            CollectionAssert.AreEqual(new[] { "Standing line." }, lines,
                                      "An empty scheduled set left the NPC with nothing to say.");
        }
        finally { Object.DestroyImmediate(go); }
    }
}
