using System.Collections.Generic;
using Draftmaster.Crowd;
using NUnit.Framework;
using UnityEngine;

// Splitting the paddock crowd into company, and the shapes they stand in once they have it. All pure --
// no scene, no play mode. The walking half of it (PaddockWalker keeping its slot) is covered separately
// in PaddockGroupTests, which has to reach into Assembly-CSharp for the component.
public class CrowdGroupingTests
{
    static CrowdGroupTuning Tuning => CrowdGroupTuning.Default;   // 45% in company, 2-4 a group, 0.85m apart

    // A repeatable roll source, so a failure is a failure again next time it is run.
    static System.Func<float> Rolls(int seed)
    {
        var rng = new System.Random(seed);
        return () => (float)rng.NextDouble();
    }

    static int Sum(List<int> sizes)
    {
        int n = 0;
        for (int i = 0; i < sizes.Count; i++) n += sizes[i];
        return n;
    }

    // ---------------------------------------------------------------- who arrives with whom

    [Test]
    public void EverybodyInThePaddockIsAccountedFor()
    {
        // The plan replaces a flat headcount loop, so anything it drops is a person missing from the
        // paddock and anything it adds is a person over the benchmarked budget.
        foreach (int population in new[] { 1, 2, 3, 7, 40, 220, 400 })
            for (int seed = 0; seed < 8; seed++)
            {
                var plan = CrowdGrouping.Plan(population, Tuning, Rolls(seed));
                Assert.AreEqual(population, Sum(plan),
                                $"Plan for {population} (seed {seed}) came back with a different headcount.");
            }
    }

    [Test]
    public void NoGroupIsBiggerOrSmallerThanTheAuthoredBounds()
    {
        var plan = CrowdGrouping.Plan(400, Tuning, Rolls(3));
        for (int i = 0; i < plan.Count; i++)
        {
            Assert.GreaterOrEqual(plan[i], 1, "A group of nobody was planned.");
            Assert.IsTrue(plan[i] == 1 || (plan[i] >= Tuning.minSize && plan[i] <= Tuning.maxSize),
                          $"Group of {plan[i]} is outside {Tuning.minSize}-{Tuning.maxSize} and is not a lone walker.");
        }
    }

    [Test]
    public void RoughlyTheAuthoredShareOfTheCrowdKeepsCompany()
    {
        // The point of the knob is that it means something: half the paddock in twos and threes should
        // come out as half the paddock, not a tenth of it.
        for (int seed = 0; seed < 6; seed++)
        {
            var plan = CrowdGrouping.Plan(400, Tuning, Rolls(seed));
            float share = CrowdGrouping.PeopleInCompany(plan) / 400f;
            Assert.AreEqual(Tuning.groupedFraction, share, 0.06f,
                            $"Seed {seed}: {share:P0} of the crowd had company, asked for {Tuning.groupedFraction:P0}.");
        }
    }

    [Test]
    public void AShareOfZeroIsTheOldPaddockOfIndividuals()
    {
        var t = Tuning;
        t.groupedFraction = 0f;
        var plan = CrowdGrouping.Plan(200, t, Rolls(1));
        Assert.AreEqual(200, plan.Count, "Somebody was given company with grouping turned off.");
        for (int i = 0; i < plan.Count; i++) Assert.AreEqual(1, plan[i]);
    }

    [Test]
    public void AShareOfOneLeavesAlmostNobodyWalkingAlone()
    {
        var t = Tuning;
        t.groupedFraction = 1f;
        var plan = CrowdGrouping.Plan(200, t, Rolls(1));
        Assert.AreEqual(200, Sum(plan));
        Assert.GreaterOrEqual(CrowdGrouping.PeopleInCompany(plan), 198,
                              "Everybody was supposed to have company.");
    }

    [Test]
    public void ANearlyEmptyPaddockStillGetsItsPeople()
    {
        Assert.AreEqual(0, CrowdGrouping.Plan(0, Tuning, Rolls(1)).Count, "Somebody was planned for nobody.");

        var one = CrowdGrouping.Plan(1, Tuning, Rolls(1));
        Assert.AreEqual(1, one.Count);
        Assert.AreEqual(1, one[0], "The only person in the paddock cannot be a pair.");
    }

    [Test]
    public void NonsenseTuningIsOrderedRatherThanObeyed()
    {
        var t = new CrowdGroupTuning { groupedFraction = 4f, minSize = -3, maxSize = 1, spacing = -1f };
        var s = CrowdGrouping.Sanitised(t);

        Assert.AreEqual(1f, s.groupedFraction, 0.0001f);
        Assert.GreaterOrEqual(s.minSize, 2, "A group of one is not a group.");
        Assert.GreaterOrEqual(s.maxSize, s.minSize);
        Assert.Greater(s.spacing, 0f, "Zero spacing would stack a huddle on one spot.");

        // And the plan built from it is still a whole paddock.
        Assert.AreEqual(50, Sum(CrowdGrouping.Plan(50, t, Rolls(2))));
    }

    [Test]
    public void GroupSizesSpanTheWholeAuthoredRange()
    {
        var seen = new HashSet<int>();
        for (float roll = 0f; roll <= 1f; roll += 0.05f) seen.Add(CrowdGrouping.SizeFor(roll, Tuning));

        Assert.IsTrue(seen.Contains(2) && seen.Contains(3) && seen.Contains(4),
                      "Pairs, threes and fours should all be reachable.");
        Assert.AreEqual(2, CrowdGrouping.SizeFor(0f, Tuning));
        Assert.AreEqual(4, CrowdGrouping.SizeFor(1f, Tuning), "A roll of exactly 1 must not run off the end.");
    }

    // ---------------------------------------------------------------- where they stand

    [Test]
    public void NeighboursInAHuddleStandOneSpacingApart()
    {
        // The ring is solved from the chord, so a four stands in a wider circle than a pair rather than
        // a tighter one -- otherwise a group of four is four people inside each other.
        float s = Tuning.spacing;
        for (int count = 2; count <= 4; count++)
            for (int i = 0; i < count; i++)
            {
                float gap = Vector2.Distance(CrowdGrouping.HuddleSlot(i, count, s),
                                             CrowdGrouping.HuddleSlot((i + 1) % count, count, s));
                Assert.AreEqual(s, gap, 0.01f, $"Group of {count}: slots {i} and {i + 1} are {gap:F2}m apart.");
            }
    }

    [Test]
    public void NobodyStandsOnTopOfAnybodyElse()
    {
        float s = Tuning.spacing;
        for (int count = 2; count <= CrowdGrouping.MaxGroupSize; count++)
            for (int i = 0; i < count; i++)
                for (int j = i + 1; j < count; j++)
                    Assert.Greater(Vector2.Distance(CrowdGrouping.HuddleSlot(i, count, s),
                                                    CrowdGrouping.HuddleSlot(j, count, s)),
                                   s * 0.5f, $"Group of {count}: members {i} and {j} overlap.");
    }

    [Test]
    public void AStoppedGroupIsCentredJustAheadOfItsLeader()
    {
        // The leader holds slot 0 at the front of the ring, so the group it turns to face is behind it.
        // That is what keeps stopping cheap -- the followers are already back there.
        float s = Tuning.spacing;
        Vector2 leader = new(10f, 4f);
        Vector2 forward = Vector2.up;

        Vector2 centre = CrowdGrouping.HuddleCentre(leader, 3, s, forward);
        Assert.Less(centre.y, leader.y, "The ring should sit behind a leader walking up the paddock.");
        Assert.AreEqual(CrowdGrouping.HuddleRadius(3, s), Vector2.Distance(centre, leader), 0.01f);
    }

    [Test]
    public void APairWalksSideBySideRatherThanInFile()
    {
        // Two people one behind the other read as two strangers who happen to be going the same way.
        float s = Tuning.spacing;
        Vector2 slot = CrowdGrouping.WalkingSlot(1, 2, s);

        Assert.Less(Mathf.Abs(slot.y), s * 0.25f, "The second of a pair fell in behind instead of alongside.");
        Assert.Greater(Mathf.Abs(slot.x), s * 0.5f, "The pair are walking on top of each other.");
    }

    [Test]
    public void AWalkingGroupOfThreeOrMoreStringsOutBehindTheLeader()
    {
        float s = Tuning.spacing;
        for (int count = 3; count <= 4; count++)
            for (int i = 1; i < count; i++)
                Assert.Less(CrowdGrouping.WalkingSlot(i, count, s).y, 0f,
                            $"Group of {count}: member {i} walks in front of the leader.");
    }

    [Test]
    public void StoppingIsAShuffleAndNotAWalk()
    {
        // The pack is the ring stretched, so halting moves nobody more than a third of the way to where
        // they were already standing. If the two shapes ever drift apart, a group stopping turns into
        // three people crossing over each other to find a new place.
        float s = Tuning.spacing;
        for (int count = 2; count <= CrowdGrouping.MaxGroupSize; count++)
            for (int i = 1; i < count; i++)
            {
                Vector2 standing = CrowdGrouping.HuddleSlot(i, count, s) - CrowdGrouping.HuddleSlot(0, count, s);
                float shuffle = Vector2.Distance(CrowdGrouping.WalkingSlot(i, count, s), standing);
                Assert.LessOrEqual(shuffle, standing.magnitude * 0.3f + 0.01f,
                                   $"Group of {count}: member {i} has to walk {shuffle:F2}m when the group stops.");
            }
    }

    [Test]
    public void SlotsTurnWithTheGroup()
    {
        // Straight up the paddock, a slot on the group's right is to the east.
        Vector2 right = CrowdGrouping.Rotate(Vector2.right, Vector2.up);
        Assert.AreEqual(1f, right.x, 0.001f);
        Assert.AreEqual(0f, right.y, 0.001f);

        // Turn the group to walk east and that same slot is now to the south.
        Vector2 turned = CrowdGrouping.Rotate(Vector2.right, Vector2.right);
        Assert.AreEqual(0f, turned.x, 0.001f);
        Assert.AreEqual(-1f, turned.y, 0.001f);

        // A group that has never walked anywhere still has a frame to stand in.
        Assert.AreEqual(Vector2.up, CrowdGrouping.Rotate(Vector2.up, Vector2.zero));
    }

    [Test]
    public void ALoneWalkerHasNoSlotToKeep()
    {
        Vector2 leader = new(3f, -7f);
        Assert.AreEqual(leader, CrowdGrouping.SlotWorld(0, 1, 0.85f, leader, Vector2.up, true));
        Assert.AreEqual(leader, CrowdGrouping.HuddleCentre(leader, 1, 0.85f, Vector2.up));
        Assert.AreEqual(Vector2.zero, CrowdGrouping.HuddleSlot(0, 1, 0.85f));
        Assert.AreEqual(0f, CrowdGrouping.HuddleRadius(1, 0.85f));
    }

    [Test]
    public void EverySlotStaysWithinArmsReachOfTheLeader()
    {
        // Whatever the group is doing, its members are near enough to read as being with each other --
        // and near enough that PaddockWalker's leash (4m) is never tripped just by standing still.
        float s = Tuning.spacing;
        Vector2 leader = new(-40f, 12f);
        foreach (bool moving in new[] { true, false })
            for (int count = 2; count <= CrowdGrouping.MaxGroupSize; count++)
                for (int i = 1; i < count; i++)
                {
                    Vector2 slot = CrowdGrouping.SlotWorld(i, count, s, leader, Vector2.up, moving);
                    float d = Vector2.Distance(slot, leader);
                    Assert.Less(d, 4f, $"Group of {count}, member {i} stands {d:F2}m from the leader.");
                }
    }
}
