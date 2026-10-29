using NUnit.Framework;
using UnityEngine;
using Draftmaster.Fans;

// EditMode coverage for where the race winner's fans stand (WinnerFanCrowd): packed round a winner standing
// still, close enough to talk to at the front, and trailing behind — never in the way — when they walk.
public class WinnerCrowdLayoutTests
{
    const float InteractRange = 2.2f;   // NPCInteractable's default

    [Test]
    public void RowOf_FillsFrontRowFirst_ThenWiderRows()
    {
        WinnerCrowdLayout.RowOf(0, out int row, out int place, out int size);
        Assert.AreEqual((0, 0, WinnerCrowdLayout.FrontRow), (row, place, size));

        WinnerCrowdLayout.RowOf(WinnerCrowdLayout.FrontRow - 1, out row, out place, out _);
        Assert.AreEqual((0, WinnerCrowdLayout.FrontRow - 1), (row, place));

        WinnerCrowdLayout.RowOf(WinnerCrowdLayout.FrontRow, out row, out place, out size);
        Assert.AreEqual((1, 0, WinnerCrowdLayout.FrontRow + WinnerCrowdLayout.RowGrowth), (row, place, size));
    }

    [Test]
    public void Standing_FrontRowIsWithinTalkingRange()
    {
        Vector2 winner = new Vector2(10f, -4f);
        for (int i = 0; i < WinnerCrowdLayout.FrontRow; i++)
        {
            Vector2 s = WinnerCrowdLayout.Slot(i, 16, winner, Vector2.zero, moving: false);
            Assert.LessOrEqual(Vector2.Distance(s, winner), InteractRange, $"fan {i}");
        }
    }

    [Test]
    public void Standing_SurroundsTheWinner()
    {
        Vector2 winner = new Vector2(3f, 7f);
        const int count = 16;
        Vector2 sum = Vector2.zero;
        for (int i = 0; i < count; i++) sum += WinnerCrowdLayout.Slot(i, count, winner, Vector2.zero, false) - winner;
        // Evenly round every row, so the crowd's middle is the winner.
        Assert.Less((sum / count).magnitude, 0.1f);
    }

    [Test]
    public void Standing_NoTwoFansShareASpot()
    {
        const int count = 16;
        for (int i = 0; i < count; i++)
            for (int j = i + 1; j < count; j++)
            {
                Vector2 a = WinnerCrowdLayout.Slot(i, count, Vector2.zero, Vector2.zero, false);
                Vector2 b = WinnerCrowdLayout.Slot(j, count, Vector2.zero, Vector2.zero, false);
                Assert.Greater(Vector2.Distance(a, b), 0.4f, $"fans {i} and {j}");
            }
    }

    [Test]
    public void Moving_EveryoneTrailsBehind()
    {
        Vector2 winner = new Vector2(-2f, 5f);
        Vector2 heading = new Vector2(3f, 1f);   // any length
        const int count = 16;
        for (int i = 0; i < count; i++)
        {
            Vector2 rel = WinnerCrowdLayout.Slot(i, count, winner, heading, moving: true) - winner;
            Assert.Less(Vector2.Dot(rel, heading.normalized), 0f, $"fan {i} is ahead of a walking winner");
        }
    }

    [Test]
    public void Moving_LoneFanIsStraightBehind()
    {
        Vector2 rel = WinnerCrowdLayout.Slot(0, 1, Vector2.zero, Vector2.up, moving: true);
        Assert.AreEqual(0f, rel.x, 1e-4f);
        Assert.AreEqual(-WinnerCrowdLayout.InnerRadius, rel.y, 1e-4f);
    }

    [Test]
    public void Moving_WithNoHeading_FallsBackToTheRing()
    {
        Vector2 a = WinnerCrowdLayout.Slot(2, 10, Vector2.zero, Vector2.zero, moving: true);
        Vector2 b = WinnerCrowdLayout.Slot(2, 10, Vector2.zero, Vector2.zero, moving: false);
        Assert.AreEqual(b, a);
    }
}
