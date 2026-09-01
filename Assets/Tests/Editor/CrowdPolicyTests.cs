using Draftmaster.Crowd;
using NUnit.Framework;
using UnityEngine;

// Rules for how much of a background NPC runs at any moment. All pure — no scene, no play mode.
public class CrowdPolicyTests
{
    static CrowdTuning Tuning => CrowdTuning.Default;   // full 12m, reduced 25m, slack 2m, budget 8

    // ---------------------------------------------------------------- levels

    [Test]
    public void NotOnFoot_FreezesTheWholeCrowdWhateverTheDistance()
    {
        var t = Tuning;
        Assert.AreEqual(CrowdLod.Frozen, CrowdPolicy.Evaluate(false, 0f, t), "stood on top of the car");
        Assert.AreEqual(CrowdLod.Frozen, CrowdPolicy.Evaluate(false, 5f, t));
        Assert.AreEqual(CrowdLod.Frozen, CrowdPolicy.Evaluate(false, 500f, t));
    }

    [Test]
    public void OnFoot_LevelFallsOffWithDistance()
    {
        var t = Tuning;
        Assert.AreEqual(CrowdLod.Full, CrowdPolicy.Evaluate(true, 0f, t));
        Assert.AreEqual(CrowdLod.Full, CrowdPolicy.Evaluate(true, t.fullRadius, t), "boundary counts as inside");
        Assert.AreEqual(CrowdLod.Reduced, CrowdPolicy.Evaluate(true, t.fullRadius + 0.01f, t));
        Assert.AreEqual(CrowdLod.Reduced, CrowdPolicy.Evaluate(true, t.reducedRadius, t));
        Assert.AreEqual(CrowdLod.Frozen, CrowdPolicy.Evaluate(true, t.reducedRadius + 0.01f, t));
    }

    [Test]
    public void FullRadiusCoversEveryRangeAnNpcCanBeInteractedWith()
    {
        // NPCAmbientChatter.noticeRange is 6.5m and re-arms at 1.4x that; NPCInteractable.interactRange
        // is 2.2m. If Full didn't cover both, an NPC could be silenced while still in earshot.
        const float chatterNotice = 6.5f * 1.4f;
        const float interact = 2.2f;
        Assert.Greater(Tuning.fullRadius, chatterNotice);
        Assert.Greater(Tuning.fullRadius, interact);
    }

    // ---------------------------------------------------------------- hysteresis

    [Test]
    public void Demotion_WaitsForTheSlack_ButPromotionIsImmediate()
    {
        var t = Tuning;
        float justOutside = t.fullRadius + 1f;           // inside the 2m slack

        // Already Full and drifting just past the line: stays Full.
        Assert.AreEqual(CrowdLod.Full,
            CrowdPolicy.EvaluateWithHysteresis(CrowdLod.Full, true, justOutside, t));

        // Clear of the slack: drops.
        Assert.AreEqual(CrowdLod.Reduced,
            CrowdPolicy.EvaluateWithHysteresis(CrowdLod.Full, true, t.fullRadius + t.hysteresis + 0.01f, t));

        // Coming the other way, an NPC at that same distance is promoted on the spot — waking late is
        // visible to the player, sleeping late costs a few frames.
        Assert.AreEqual(CrowdLod.Full,
            CrowdPolicy.EvaluateWithHysteresis(CrowdLod.Reduced, true, t.fullRadius - 0.01f, t));
    }

    [Test]
    public void FrozenNpcWakesStraightToTheRightLevel()
    {
        var t = Tuning;
        Assert.AreEqual(CrowdLod.Full, CrowdPolicy.EvaluateWithHysteresis(CrowdLod.Frozen, true, 1f, t));
        Assert.AreEqual(CrowdLod.Reduced, CrowdPolicy.EvaluateWithHysteresis(CrowdLod.Frozen, true, 20f, t));
    }

    [Test]
    public void LosingThePlayerIgnoresHysteresis()
    {
        Assert.AreEqual(CrowdLod.Frozen,
            CrowdPolicy.EvaluateWithHysteresis(CrowdLod.Full, false, 0f, Tuning));
    }

    [Test]
    public void WalkingOutAndBackDoesNotFlickerOnTheBoundary()
    {
        var t = Tuning;
        var lod = CrowdLod.Full;
        int changes = 0;

        // Pace back and forth across the full radius by a few centimetres, as a player leaning on a
        // fence next to an NPC would. Without hysteresis this flips every step.
        for (int i = 0; i < 200; i++)
        {
            float d = t.fullRadius + (i % 2 == 0 ? -0.05f : 0.05f);
            var next = CrowdPolicy.EvaluateWithHysteresis(lod, true, d, t);
            if (next != lod) changes++;
            lod = next;
        }
        Assert.AreEqual(0, changes, "level should never have changed inside the slack band");
    }

    // ---------------------------------------------------------------- the rota

    [Test]
    public void Stride_KeepsThePerFrameBudget()
    {
        Assert.AreEqual(1, CrowdPolicy.StrideFor(0, 8));
        Assert.AreEqual(1, CrowdPolicy.StrideFor(8, 8));
        Assert.AreEqual(3, CrowdPolicy.StrideFor(20, 8));
        Assert.AreEqual(13, CrowdPolicy.StrideFor(100, 8));
        Assert.AreEqual(25, CrowdPolicy.StrideFor(200, 8));

        // Whatever the population, a frame never re-evaluates more than the budget (+1 for the ceiling).
        foreach (int n in new[] { 1, 7, 20, 34, 100, 200, 500, 1000 })
        {
            int stride = CrowdPolicy.StrideFor(n, 8);
            int evaluatedThisFrame = Mathf.CeilToInt(n / (float)stride);
            Assert.LessOrEqual(evaluatedThisFrame, 8 + 1, $"population {n} evaluated {evaluatedThisFrame}/frame");
        }
    }

    [Test]
    public void Rota_VisitsEveryNpcExactlyOncePerStride()
    {
        const int n = 137;
        int stride = CrowdPolicy.StrideFor(n, 8);
        var visits = new int[n];
        for (int frame = 0; frame < stride; frame++)
            for (int i = 0; i < n; i++)
                if (CrowdPolicy.TicksThisFrame(i, frame, stride)) visits[i]++;

        for (int i = 0; i < n; i++)
            Assert.AreEqual(1, visits[i], $"NPC {i} was visited {visits[i]} times in one stride");
    }

    // ---------------------------------------------------------------- what each level switches off

    [Test]
    public void Frozen_TurnsEverythingOffIncludingPhysics()
    {
        Assert.IsFalse(CrowdPolicy.RunsAt(CrowdLod.Frozen, proximityOnly: false), "wandering");
        Assert.IsFalse(CrowdPolicy.RunsAt(CrowdLod.Frozen, proximityOnly: true), "chatter/conversation");
        Assert.IsFalse(CrowdPolicy.PhysicsRunsAt(CrowdLod.Frozen), "rigidbody and colliders");
        Assert.IsTrue(CrowdPolicy.StandsStillAt(CrowdLod.Frozen), "parked on the standing pose");
    }

    [Test]
    public void Reduced_KeepsWalkingButGoesQuiet()
    {
        Assert.IsTrue(CrowdPolicy.RunsAt(CrowdLod.Reduced, proximityOnly: false));
        Assert.IsFalse(CrowdPolicy.RunsAt(CrowdLod.Reduced, proximityOnly: true));
        Assert.IsTrue(CrowdPolicy.PhysicsRunsAt(CrowdLod.Reduced));
        Assert.IsFalse(CrowdPolicy.StandsStillAt(CrowdLod.Reduced));
    }

    [Test]
    public void Full_RunsEverything()
    {
        Assert.IsTrue(CrowdPolicy.RunsAt(CrowdLod.Full, proximityOnly: false));
        Assert.IsTrue(CrowdPolicy.RunsAt(CrowdLod.Full, proximityOnly: true));
        Assert.IsTrue(CrowdPolicy.PhysicsRunsAt(CrowdLod.Full));
        Assert.IsFalse(CrowdPolicy.StandsStillAt(CrowdLod.Full));
    }

    [Test]
    public void DrivingFreezesEveryBehaviourAndAllPhysics()
    {
        // The whole point of the exercise: once the player is in the car, no paddock NPC is running
        // anything at all, whatever the geometry says.
        foreach (float d in new[] { 0f, 1f, 12f, 25f, 1000f })
        {
            var lod = CrowdPolicy.Evaluate(false, d, Tuning);
            Assert.IsFalse(CrowdPolicy.RunsAt(lod, false), $"at {d}m");
            Assert.IsFalse(CrowdPolicy.RunsAt(lod, true), $"at {d}m");
            Assert.IsFalse(CrowdPolicy.PhysicsRunsAt(lod), $"at {d}m");
        }
    }

    // ---------------------------------------------------------------- sizing the paddock

    [Test]
    public void AwakeCountIsBoundedByTheRadius_NotThePopulation()
    {
        var t = Tuning;
        // A 300m x 30m paddock — roughly what PaddockSpawner frames alongside a long pit straight.
        const float length = 300f, depth = 30f;

        float at120 = CrowdPolicy.ExpectedAwakeCount(120, length, depth, t);
        float at400 = CrowdPolicy.ExpectedAwakeCount(400, length, depth, t);

        // Tripling the crowd triples the awake set too — but from a small base, because the awake band
        // is a 50m slice of a 300m paddock.
        Assert.Less(at120, 40f, $"{at120:0} awake of 120");
        Assert.Less(at400, 120f, $"{at400:0} awake of 400");
        Assert.Less(at120 / 120f, 0.2f, "under a fifth of the crowd should ever be running at once");
    }

    [Test]
    public void AwakeCountNeverExceedsThePopulation()
    {
        var t = Tuning;
        // A paddock smaller than the radius: everyone is inside it, and that is the ceiling.
        Assert.AreEqual(50f, CrowdPolicy.ExpectedAwakeCount(50, 10f, 10f, t), 0.001f);
        Assert.AreEqual(0f, CrowdPolicy.ExpectedAwakeCount(0, 300f, 30f, t), 0.001f);
    }

    [Test]
    public void Rota_ReactsWellInsideTheTimeItTakesToCrossAnLodBand()
    {
        // The gap between Full (12m) and Frozen (25m) is 13m. The on-foot player runs at a few m/s, so
        // even a 500-strong crowd must be re-checked far quicker than that band takes to cross.
        var t = CrowdTuning.Default;
        float band = t.reducedRadius - t.fullRadius;
        const float sprint = 6f; // m/s, generous
        float crossSeconds = band / sprint;

        foreach (int n in new[] { 20, 100, 200, 500 })
        {
            float latency = CrowdPolicy.WorstCaseLatencySeconds(n, t.evaluationsPerFrame, 60f);
            Assert.Less(latency, crossSeconds * 0.5f,
                $"crowd of {n}: {latency:0.000}s to re-evaluate vs {crossSeconds:0.00}s to cross the band");
        }
    }

    // ---------------------------------------------------------------- how many to spawn

    [Test]
    public void RaceDayIsTheFullestHalfDay_AndNobodyGetsAnEmptyPaddock()
    {
        // Sunday afternoon (index 5, WeekendSlot.SundayPM) is the Cup race and must be the peak; every
        // other half-day is busy but below it, and none of them empties the place out.
        float raceDay = CrowdPolicy.BusynessForHalfDay(5);
        Assert.AreEqual(1f, raceDay, 0.0001f, "race day should be a full house");

        for (int i = 0; i < 5; i++)
        {
            float f = CrowdPolicy.BusynessForHalfDay(i);
            Assert.Less(f, raceDay, $"half-day {i} should be quieter than race day");
            Assert.Greater(f, 0.5f, $"half-day {i} should still read as a busy paddock");
        }
    }

    [Test]
    public void TheWeekendFillsUpAsItGoesOn()
    {
        // Friday setup -> Friday truck race -> Saturday qualifying -> Saturday National race -> Sunday.
        // Never thins out from one half-day to the next.
        for (int i = 1; i < 6; i++)
            Assert.GreaterOrEqual(CrowdPolicy.BusynessForHalfDay(i), CrowdPolicy.BusynessForHalfDay(i - 1),
                $"half-day {i} should be at least as busy as {i - 1}");
    }

    [Test]
    public void OutsideAWeekend_ThePaddockIsFull()
    {
        // A single race carries no weekend ledger, and a single race is a race day.
        Assert.AreEqual(1f, CrowdPolicy.BusynessForHalfDay(-1), 0.0001f);
        Assert.AreEqual(1f, CrowdPolicy.BusynessForHalfDay(99), 0.0001f);
    }

    [Test]
    public void PopulationScalesTheFullHouseFigure()
    {
        int full = CrowdPolicy.ComfortableMaxPopulation;
        Assert.AreEqual(400, full, "the benchmarked ceiling");

        Assert.AreEqual(full, CrowdPolicy.PopulationForHalfDay(5, full), "race day spawns the lot");
        Assert.AreEqual(220, CrowdPolicy.PopulationForHalfDay(0, full), "Friday morning, 55% of 400");

        // An empty paddock stays empty; a paddock with anybody in it never scales down to nobody.
        Assert.AreEqual(0, CrowdPolicy.PopulationForHalfDay(0, 0));
        Assert.AreEqual(0, CrowdPolicy.PopulationForHalfDay(0, -50));
        Assert.AreEqual(1, CrowdPolicy.PopulationForHalfDay(0, 1));
    }

    [Test]
    public void TheComfortableMaximumStaysInsideTheFrameBudgetItWasMeasuredAt()
    {
        // CrowdBenchmarkTests measured ~4.5us/frame for an awake NPC. The densest paddock the spawner
        // frames is a short pit straight, so check the worst case rather than the roomy one: even there
        // the whole background crowd must stay well under a sixth of a 60fps frame, and while the player
        // is driving it costs nothing at all because everyone is frozen.
        const float perAwakeMs = 0.0045f;
        const float frameMs = 1000f / 60f;

        float awake = CrowdPolicy.ExpectedAwakeCount(CrowdPolicy.ComfortableMaxPopulation, 100f, 30f, Tuning);
        float cost = awake * perAwakeMs;

        Assert.Less(cost, frameMs / 6f,
            $"{awake:0} of {CrowdPolicy.ComfortableMaxPopulation} awake = {cost:0.00} ms/frame");
    }

}
