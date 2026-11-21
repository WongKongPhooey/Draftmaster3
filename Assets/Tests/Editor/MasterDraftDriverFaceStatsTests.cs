using System.Linq;
using Draftmaster.MasterDraft;
using NUnit.Framework;

// The three stats a driver card face prints (CardText.DriverFaceStats): the driver's highest, best first,
// with at least one of Short Track / Speedway / Superspeedway / Road Course always among them.
public class MasterDraftDriverFaceStatsTests
{
    static CardDefinition Driver(int sht, int spw, int ssw, int rd, int drt,
                                 int qua, int con, int agg, int awr, int tyr) => new CardDefinition
    {
        type = CardType.Driver,
        shortTracks = sht, speedways = spw, superspeedways = ssw, roadCourses = rd, dirtCourses = drt,
        qualifying = qua, consistency = con, aggression = agg, awareness = awr, tyreManagement = tyr,
    };

    static string Face(CardDefinition d) => string.Join(", ", CardText.DriverFaceStats(d).Select(s => s.ToString()));

    static readonly string[] Headline = { "SHT", "SPW", "SSW", "RD" };

    [Test]
    public void PicksTheThreeHighestAcrossTracksAndCraft()
    {
        var d = Driver(sht: 9, spw: 12, ssw: 18, rd: 6, drt: 3, qua: 16, con: 17, agg: 10, awr: 11, tyr: 8);
        Assert.AreEqual("SSW 18, CON 17, QUA 16", Face(d));
    }

    [Test]
    public void ThirdGivesWayToTheBestHeadlineTrackWhenCraftFillsTheTopThree()
    {
        var d = Driver(sht: 9, spw: 12, ssw: 11, rd: 6, drt: 3, qua: 16, con: 17, agg: 18, awr: 15, tyr: 14);
        Assert.AreEqual("AGG 18, CON 17, SPW 12", Face(d));
    }

    [Test]
    public void DirtDoesNotCountAsAHeadlineTrack()
    {
        var d = Driver(sht: 5, spw: 7, ssw: 4, rd: 6, drt: 20, qua: 19, con: 18, agg: 3, awr: 2, tyr: 1);
        Assert.AreEqual("DRT 20, QUA 19, SPW 7", Face(d));
    }

    [Test]
    public void TiesFavourTrackTypes()
    {
        var d = Driver(sht: 10, spw: 10, ssw: 10, rd: 10, drt: 10, qua: 10, con: 10, agg: 10, awr: 10, tyr: 10);
        Assert.AreEqual("SHT 10, SPW 10, SSW 10", Face(d));
    }

    [Test]
    public void NullCardHasNoStats()
    {
        Assert.AreEqual(0, CardText.DriverFaceStats(null).Length);
    }

    [Test]
    public void EveryDriverShowsThreeInOrderWithAHeadlineTrack()
    {
        var rng = new System.Random(20261121);
        for (int n = 0; n < 5000; n++)
        {
            var d = Driver(rng.Next(21), rng.Next(21), rng.Next(21), rng.Next(21), rng.Next(21),
                           rng.Next(21), rng.Next(21), rng.Next(21), rng.Next(21), rng.Next(21));
            var face = CardText.DriverFaceStats(d);
            int[] all = { d.shortTracks, d.speedways, d.superspeedways, d.roadCourses, d.dirtCourses,
                          d.qualifying, d.consistency, d.aggression, d.awareness, d.tyreManagement };
            var sorted = all.OrderByDescending(v => v).ToArray();

            Assert.AreEqual(CardText.DriverFaceStatCount, face.Length, Face(d));
            Assert.IsTrue(face.Any(s => s.headlineTrack), $"no headline track: {Face(d)}");
            Assert.IsTrue(face.All(s => s.headlineTrack == Headline.Contains(s.label)), Face(d));
            Assert.AreEqual(face.Length, face.Select(s => s.label).Distinct().Count(), $"repeat: {Face(d)}");
            for (int i = 1; i < face.Length; i++)
                Assert.GreaterOrEqual(face[i - 1].value, face[i].value, $"out of order: {Face(d)}");
            // The first two are always the two highest; the third is the third highest unless it had to make
            // way for a headline track, which is then the best of those four.
            Assert.AreEqual(sorted[0], face[0].value, Face(d));
            Assert.AreEqual(sorted[1], face[1].value, Face(d));
            int bestHeadline = new[] { d.shortTracks, d.speedways, d.superspeedways, d.roadCourses }.Max();
            if (!face[0].headlineTrack && !face[1].headlineTrack && sorted[2] > bestHeadline)
                Assert.AreEqual(bestHeadline, face[2].value, Face(d));
            else
                Assert.AreEqual(sorted[2], face[2].value, Face(d));
        }
    }
}
