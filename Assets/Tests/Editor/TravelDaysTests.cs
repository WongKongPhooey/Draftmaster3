using Draftmaster.Tracks;
using NUnit.Framework;

// The satnav's clock: one half-day per stop, Monday morning to Friday morning.
public class TravelDaysTests
{
    [TestCase(0, "Mon AM")]
    [TestCase(1, "Mon PM")]
    [TestCase(2, "Tue AM")]
    [TestCase(5, "Wed PM")]
    [TestCase(7, "Thu PM")]
    [TestCase(8, "Fri AM")]
    public void Each_stop_is_half_a_day(int stops, string expected) =>
        Assert.AreEqual(expected, TravelDays.Label(stops));

    [Test]
    public void The_clock_holds_at_Friday_morning() =>
        Assert.AreEqual("Fri AM", TravelDays.Label(11));

    [Test]
    public void A_negative_count_reads_as_Monday_morning() =>
        Assert.AreEqual("Mon AM", TravelDays.Label(-1));
}
