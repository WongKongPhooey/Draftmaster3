using NUnit.Framework;
using Draftmaster.Sim;

// Broadcast mode's TV director: which car the camera cuts to, when a hit is worth cutting to, and the ticker text.
public class BroadcastShotsTests
{
    const float Never = float.PositiveInfinity;

    [Test]
    public void FreshField_OpensOnTheLeader()
    {
        var gaps = new[] { 0f, 3f, 4f, 5f };
        var since = new[] { Never, Never, Never, Never };
        Assert.AreEqual(0, BroadcastShots.Pick(gaps, since, BroadcastShots.None));
    }

    [Test]
    public void CloseBattle_BeatsTheLeader()
    {
        // Car 2 is 0.2 s off car 1; nobody else is near anybody.
        var gaps = new[] { 0f, 3f, 0.2f, 5f };
        var since = new[] { Never, Never, Never, Never };
        Assert.AreEqual(2, BroadcastShots.Pick(gaps, since, BroadcastShots.None));
    }

    [Test]
    public void CloserBattle_WinsOverAWiderOne()
    {
        var gaps = new[] { 0f, 0.8f, 3f, 0.1f };
        var since = new[] { Never, Never, Never, Never };
        Assert.AreEqual(3, BroadcastShots.Pick(gaps, since, BroadcastShots.None));
    }

    [Test]
    public void NeverCutsToTheCarAlreadyOnCamera()
    {
        var gaps = new[] { 0f, 3f, 0.2f, 5f };
        var since = new[] { Never, Never, Never, Never };
        Assert.AreNotEqual(2, BroadcastShots.Pick(gaps, since, 2));
    }

    [Test]
    public void JustShown_WaitsItsTurn()
    {
        // The leader was on camera a moment ago; the rest of the field has never been shown.
        var gaps = new[] { 0f, 3f, 4f, 5f };
        var since = new[] { 1f, Never, Never, Never };
        Assert.AreEqual(1, BroadcastShots.Pick(gaps, since, BroadcastShots.None));
    }

    [Test]
    public void QuietField_IsWalkedThroughInOrder()
    {
        var gaps = new[] { 0f, 3f, 4f, 5f, 6f };
        var since = new[] { Never, Never, Never, Never, Never };
        int current = BroadcastShots.None;
        var seen = new System.Collections.Generic.HashSet<int>();
        // Every car shown within a field's worth of cuts, eight seconds a shot.
        for (int shot = 0; shot < gaps.Length; shot++)
        {
            for (int i = 0; i < since.Length; i++) since[i] += 8f;
            current = BroadcastShots.Pick(gaps, since, current);
            seen.Add(current);
            since[current] = 0f;
        }
        Assert.AreEqual(gaps.Length, seen.Count);
    }

    [Test]
    public void OverARace_BattleShownMost_LeaderRecurs_EveryoneOnTv()
    {
        // Ten cars, car 5 a tenth behind car 4, everyone else spread out. Sixty cuts, eight seconds each.
        var gaps = new[] { 0f, 2f, 3f, 2.5f, 4f, 0.1f, 3f, 5f, 2f, 6f };
        var since = new float[gaps.Length];
        for (int i = 0; i < since.Length; i++) since[i] = Never;
        var shown = new int[gaps.Length];
        int current = BroadcastShots.None;
        for (int shot = 0; shot < 60; shot++)
        {
            for (int i = 0; i < since.Length; i++) since[i] += 8f;
            current = BroadcastShots.Pick(gaps, since, current);
            shown[current]++;
            since[current] = 0f;
        }
        for (int i = 0; i < shown.Length; i++)
        {
            Assert.Greater(shown[i], 0, "car " + i + " never on TV");
            if (i != 5) Assert.Greater(shown[5], shown[i], "the battle should be on more than car " + i);
        }
        for (int i = 1; i < shown.Length; i++)
            if (i != 5) Assert.GreaterOrEqual(shown[0], shown[i], "the leader should be on at least as often as car " + i);
    }

    [Test]
    public void LoneCar_IsTheShot_EmptyFieldIsNone()
    {
        Assert.AreEqual(0, BroadcastShots.Pick(new[] { 0f }, new[] { 0f }, 0));
        Assert.AreEqual(BroadcastShots.None, BroadcastShots.Pick(new float[0], new float[0], BroadcastShots.None));
    }

    [Test]
    public void CutToHit_OnlyHardOnes_AndNotStraightAfterACut()
    {
        Assert.IsFalse(BroadcastShots.CutToHit(5f, 8f, 10f, 2.5f, false, 0f), "a nudge");
        Assert.IsTrue(BroadcastShots.CutToHit(12f, 8f, 10f, 2.5f, false, 0f), "a big hit");
        Assert.IsFalse(BroadcastShots.CutToHit(12f, 8f, 1f, 2.5f, false, 0f), "shot too young");
    }

    [Test]
    public void CutToHit_OnAHit_OnlyForAHarderOne()
    {
        Assert.IsFalse(BroadcastShots.CutToHit(10f, 8f, 4f, 2.5f, true, 14f));
        Assert.IsTrue(BroadcastShots.CutToHit(16f, 8f, 4f, 2.5f, true, 14f));
    }

    [Test]
    public void Gap_ReadsLikeTheTicker()
    {
        Assert.AreEqual("LEADER", BroadcastShots.Gap(1, 0f, 0));
        Assert.AreEqual("+0.512", BroadcastShots.Gap(2, 0.512f, 0));
        Assert.AreEqual("+12.000", BroadcastShots.Gap(5, 12f, 0));
        Assert.AreEqual("-1 LAP", BroadcastShots.Gap(30, 40f, 1));
        Assert.AreEqual("-3 LAPS", BroadcastShots.Gap(38, 140f, 3));
    }

    [Test]
    public void LapsDown_WholeLapsBehindTheLeader()
    {
        Assert.AreEqual(0, BroadcastShots.LapsDown(5000f, 4100f, 1000f));
        Assert.AreEqual(1, BroadcastShots.LapsDown(5000f, 3900f, 1000f));
        Assert.AreEqual(2, BroadcastShots.LapsDown(5000f, 2500f, 1000f));
        Assert.AreEqual(0, BroadcastShots.LapsDown(5000f, 2500f, 0f));
    }

    [Test]
    public void Clock_MinutesThenHours()
    {
        Assert.AreEqual("0:00", BroadcastShots.Clock(0f));
        Assert.AreEqual("7:05", BroadcastShots.Clock(425.9f));
        Assert.AreEqual("1:02:09", BroadcastShots.Clock(3729f));
        Assert.AreEqual("0:00", BroadcastShots.Clock(-3f));
    }

    [Test]
    public void Scroll_WrapsSeamlessly()
    {
        Assert.AreEqual(30f, BroadcastShots.Scroll(0f, 30f, 1f, 100f), 1e-4f);
        Assert.AreEqual(10f, BroadcastShots.Scroll(90f, 20f, 1f, 100f), 1e-4f);
        Assert.AreEqual(0f, BroadcastShots.Scroll(50f, 20f, 1f, 0f), 1e-4f);
    }
}
