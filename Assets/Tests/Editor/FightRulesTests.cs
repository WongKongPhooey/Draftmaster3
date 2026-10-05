using NUnit.Framework;
using UnityEngine;
using Draftmaster.Fights;

// EditMode coverage for the paddock-fight decision maths. The fight itself is MonoBehaviours that need Play
// Mode (which can't be driven while the editor is unfocused), so everything that decides an outcome —
// whether a move lands, what it costs, when the crews step in, who came off better — lives in FightRules
// and is verified here instead.
public class FightRulesTests
{
    [Test]
    public void CanChallenge_OnlyBelowRivalThreshold()
    {
        Assert.IsFalse(FightRules.CanChallenge(0f), "a neutral driver should never offer a fight");
        Assert.IsFalse(FightRules.CanChallenge(-29f), "just short of the threshold is still not a rival");
        Assert.IsTrue(FightRules.CanChallenge(FightRules.RivalThreshold));
        Assert.IsTrue(FightRules.CanChallenge(-80f));
    }

    [Test]
    public void CanChallenge_HonoursCallerThreshold()
    {
        Assert.IsTrue(FightRules.CanChallenge(-15f, -10f));
        Assert.IsFalse(FightRules.CanChallenge(-15f, -50f));
    }

    [Test]
    public void Damage_HooksHitHarderThanShoves()
    {
        float shove = FightRules.Damage(FightMove.Shove, 10, 0.5f);
        float hook = FightRules.Damage(FightMove.LeftHook, 10, 0.5f);
        Assert.Greater(hook, shove);
        Assert.AreEqual(FightRules.Damage(FightMove.LeftHook, 10, 0.5f),
                        FightRules.Damage(FightMove.RightHook, 10, 0.5f), 1e-4f);
    }

    [Test]
    public void Damage_ScalesWithAggression()
    {
        float mild = FightRules.Damage(FightMove.Shove, 1, 0.5f);
        float spicy = FightRules.Damage(FightMove.Shove, 20, 0.5f);
        Assert.Greater(spicy, mild);
        // Out-of-range stats are clamped rather than blowing the scale out.
        Assert.AreEqual(mild, FightRules.Damage(FightMove.Shove, -5, 0.5f), 1e-4f);
        Assert.AreEqual(spicy, FightRules.Damage(FightMove.Shove, 99, 0.5f), 1e-4f);
    }

    [Test]
    public void Damage_StaysWithinSpreadOfBase()
    {
        const int aggression = 10;
        float low = FightRules.Damage(FightMove.Shove, aggression, 0f);
        float high = FightRules.Damage(FightMove.Shove, aggression, 1f);
        Assert.Less(low, high);
        Assert.Greater(low, FightRules.ShoveDamage * 0.5f);
        Assert.Less(high, FightRules.ShoveDamage * 1.6f);
    }

    [Test]
    public void Connects_NeedsRangeAndFacing()
    {
        Assert.IsTrue(FightRules.Connects(1f, 1.25f, 1f, 0.25f), "dead ahead, in range");
        Assert.IsFalse(FightRules.Connects(2f, 1.25f, 1f, 0.25f), "out of reach");
        Assert.IsFalse(FightRules.Connects(1f, 1.25f, -0.9f, 0.25f), "swinging at their own back");
        Assert.IsTrue(FightRules.Connects(1.25f, 1.25f, 0.25f, 0.25f), "exactly on both limits still lands");
    }

    [Test]
    public void ApplyDamage_ClampsToZeroAndMax()
    {
        Assert.AreEqual(90f, FightRules.ApplyDamage(100f, 10f), 1e-4f);
        Assert.AreEqual(0f, FightRules.ApplyDamage(5f, 40f), 1e-4f);
        Assert.AreEqual(FightRules.MaxHealth, FightRules.ApplyDamage(200f, 0f), 1e-4f);
    }

    [Test]
    public void ShouldBreakUp_ExhaustionBeatsTheClock()
    {
        Assert.AreEqual(BreakupReason.None,
                        FightRules.ShouldBreakUp(1f, 14f, 100f, 90f, 20f));
        Assert.AreEqual(BreakupReason.Timeout,
                        FightRules.ShouldBreakUp(14f, 14f, 100f, 90f, 20f));
        Assert.AreEqual(BreakupReason.Exhausted,
                        FightRules.ShouldBreakUp(1f, 14f, 100f, 18f, 20f));
        // A fight that is both spent and out of time is reported as spent — that's the reason worth showing.
        Assert.AreEqual(BreakupReason.Exhausted,
                        FightRules.ShouldBreakUp(20f, 14f, 12f, 90f, 20f));
    }

    [Test]
    public void Winner_ReadsComposureWithADeadHeatBand()
    {
        Assert.AreEqual(1, FightRules.Winner(80f, 40f));
        Assert.AreEqual(-1, FightRules.Winner(30f, 55f));
        Assert.AreEqual(0, FightRules.Winner(50f, 50.5f), "within a point is honours even");
    }

    [Test]
    public void AiAttackInterval_AggressiveDriversComeForwardMoreOften()
    {
        float calm = FightRules.AiAttackInterval(1, 0.5f);
        float angry = FightRules.AiAttackInterval(20, 0.5f);
        Assert.Less(angry, calm);
        Assert.Greater(calm, 0f);
        // The jitter widens the gap but never inverts a swing into an instant one.
        Assert.Greater(FightRules.AiAttackInterval(20, 0f), 0.5f);
    }

    [Test]
    public void DesiredRange_SitsInsideReach()
    {
        float reach = 1.25f;
        float ideal = FightRules.DesiredRange(reach);
        Assert.Less(ideal, reach);
        Assert.Greater(ideal, reach * 0.5f);
    }

    [Test]
    public void HealthFraction_MapsToBarFill()
    {
        Assert.AreEqual(1f, FightRules.HealthFraction(FightRules.MaxHealth), 1e-4f);
        Assert.AreEqual(0.5f, FightRules.HealthFraction(FightRules.MaxHealth * 0.5f), 1e-4f);
        Assert.AreEqual(0f, FightRules.HealthFraction(-10f), 1e-4f);
    }

    [Test]
    public void EscortHold_IsBodiesTouching()
    {
        Assert.AreEqual(0.3f, FightRules.EscortHoldDistance(0.15f, 0.15f), 1e-4f, "front against back, no gap");
        Assert.AreEqual(0.25f, FightRules.EscortHoldDistance(0.15f, 0.15f, -0.05f), 1e-4f);
        Assert.Less(FightRules.EscortHoldDistance(0.2f, 0.2f), 0.75f, "the old fixed 0.75 m left daylight");
    }

    [Test]
    public void EscortHeading_OpenGroundGoesStraightOut()
    {
        Vector2 h = FightRules.ChooseEscortHeading(Vector2.right, _ => 99f, 6f, Vector2.zero);
        Assert.Greater(Vector2.Dot(h, Vector2.right), 0.999f);
    }

    [Test]
    public void EscortHeading_TurnsAwayFromABuilding()
    {
        // A motorhome straight ahead and up to 30 degrees either side: only 1 m of walk that way.
        float Run(Vector2 d) => Vector2.Dot(d, Vector2.right) > 0.8f ? 1f : 99f;
        Vector2 h = FightRules.ChooseEscortHeading(Vector2.right, Run, 6f, Vector2.zero);
        Assert.Less(Vector2.Dot(h, Vector2.right), 0.8f, "should not walk into the building");
        Assert.Greater(Vector2.Dot(h, Vector2.right), -0.6f, "should not walk back through the other driver");
    }

    [Test]
    public void EscortHeading_BoxedInTakesTheLongestRun()
    {
        // Everything blocked, but down-ish has the most room.
        float Run(Vector2 d) => Vector2.Dot(d, Vector2.down) > 0.8f ? 3f : 0.5f;
        Vector2 h = FightRules.ChooseEscortHeading(Vector2.right, Run, 6f, Vector2.zero);
        Assert.Greater(Vector2.Dot(h, Vector2.down), 0.8f);
    }

    [Test]
    public void EscortHeading_SteersApartFromTheOtherEscort()
    {
        // Straight out is a dead end, both sides open: the side away from the partner's heading wins.
        float Run(Vector2 d) => Vector2.Dot(d, Vector2.right) > 0.95f ? 0.5f : 99f;
        Vector2 h = FightRules.ChooseEscortHeading(Vector2.right, Run, 6f, Vector2.up);
        Assert.Less(h.y, 0f, "partner went up, so this one goes down");
    }
}
