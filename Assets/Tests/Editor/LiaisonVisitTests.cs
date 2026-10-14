using Draftmaster.Weekend;
using NUnit.Framework;

// The team liaison turns up now and then rather than every time the driver leaves the motorhome, and says
// one of several things when she does. The first meeting has to be certain — she is how a new driver
// learns the weekend exists — and every line set has to carry the booking, or she sends them nowhere.
public class LiaisonVisitTests
{
    [Test]
    public void TheFirstMeetingIsCertain()
    {
        Assert.IsTrue(LiaisonVisit.ShouldVisit(metBefore: false, cameLastTime: false, roll: 0.999f));
        Assert.IsTrue(LiaisonVisit.ShouldVisit(metBefore: false, cameLastTime: true, roll: 0.999f));
    }

    [Test]
    public void SheNeverComesTwiceRunning()
    {
        Assert.IsFalse(LiaisonVisit.ShouldVisit(metBefore: true, cameLastTime: true, roll: 0f));
    }

    [Test]
    public void OtherwiseItIsARoll()
    {
        Assert.IsTrue(LiaisonVisit.ShouldVisit(true, false, 0f));
        Assert.IsFalse(LiaisonVisit.ShouldVisit(true, false, LiaisonVisit.Chance));
        Assert.Less(LiaisonVisit.Chance, 0.5f, "Occasionally means less often than not.");
    }

    [Test]
    public void EveryBookedSetNamesTheBookingAndTheTime()
    {
        Assert.Greater(LiaisonVisit.BookedVariants, 1);
        for (int v = 0; v < LiaisonVisit.BookedVariants; v++)
        {
            var text = string.Join("\n", LiaisonVisit.Lines(v, "Friday, 9:30 AM", "media session at the media centre, 10:00"));
            StringAssert.Contains("media session at the media centre, 10:00", text, $"set {v}");
            StringAssert.Contains("Friday, 9:30 AM", text, $"set {v}");
            StringAssert.DoesNotContain("{0}", text);
            StringAssert.DoesNotContain("{1}", text);
        }
    }

    [Test]
    public void ClearSetsDoNotSendThePlayerAnywhere()
    {
        Assert.Greater(LiaisonVisit.ClearVariants, 1);
        for (int v = 0; v < LiaisonVisit.ClearVariants; v++)
        {
            var lines = LiaisonVisit.Lines(v, "Friday, 9:30 AM", null);
            Assert.Greater(lines.Length, 0);
            foreach (var l in lines) StringAssert.DoesNotContain("{1}", l);
        }
    }

    [Test]
    public void TheSameSetNeverComesUpTwiceRunning()
    {
        for (int count = 2; count < 8; count++)
            for (int last = 0; last < count; last++)
                for (int roll = 0; roll < count; roll++)
                {
                    int pick = LiaisonVisit.NextVariant(count, last, roll);
                    Assert.AreNotEqual(last, pick);
                    Assert.That(pick, Is.InRange(0, count - 1));
                }
        Assert.AreEqual(0, LiaisonVisit.NextVariant(1, 0, 0));
    }
}
