using System.Collections.Generic;
using Draftmaster.Weekend;
using NUnit.Framework;
using UnityEditor;

// The National (Xfinity) and Truck fields are the real 2025 entry lists, and every real driver's career is
// kept per series. SeriesEntryLists2025 + SeriesCareers2025, read by SeriesSimulator, SimulatedField and the
// laptop's driver sheet (GarageScreenUI).
public class RealSeriesFieldsTests
{
    static readonly RacingSeries[] Supporting = { RacingSeries.National, RacingSeries.Trucks };

    [Test]
    public void NoTwoCarsInAFieldShareANumber()
    {
        foreach (var series in Supporting)
        {
            var seen = new HashSet<int>();
            foreach (var e in SeriesEntryLists2025.For(series))
                Assert.IsTrue(seen.Add(e.Number), $"{series}: #{e.Number} is entered twice");
            Assert.Greater(seen.Count, 20, $"{series} field");
        }
    }

    // GridSpawner and the laptop look the paint up by number in the series' carset; a number with no art
    // would be a car that never spawns.
    [Test]
    public void EveryCarHasPaint()
    {
        foreach (var series in Supporting)
        {
            string carset = series == RacingSeries.Trucks ? "cts25" : "xfi25";
            foreach (var e in SeriesEntryLists2025.For(series))
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>($"Assets/Resources/{carset}livery{e.Number}.png"),
                                 $"{series}: no {carset}livery{e.Number}.png for {e.Name}");
        }
    }

    [Test]
    public void EveryFullTimeDriverHasACareerInTheirSeries()
    {
        foreach (var series in Supporting)
            foreach (var e in SeriesEntryLists2025.For(series))
            {
                Assert.IsTrue(SeriesCareers2025.TryGet(e.Name, series, out var r), $"{series}: no career row for {e.Name}");
                Assert.Greater(r.Starts, 0, $"{e.Name} ran a 2025 season in {series}");
                Assert.LessOrEqual(r.Wins, r.Top10s, $"{e.Name}: every win is a top 10");
                Assert.LessOrEqual(r.Top10s, r.Starts, $"{e.Name}: more top 10s than starts");
            }
    }

    [Test]
    public void TheSimulatorRacesTheRealFieldInItsRealNumbers()
    {
        foreach (var series in Supporting)
        {
            var field = SeriesEntryLists2025.For(series);
            var names = SeriesSimulator.RosterFor(series);
            Assert.AreEqual(field.Count, names.Length);
            for (int i = 0; i < field.Count; i++)
            {
                Assert.AreEqual(field[i].Name, names[i]);
                Assert.AreEqual(field[i].Number, SeriesSimulator.NumberFor(series, i));
            }

            var race = SeriesSimulator.Race(series, 3, "Bristol", 32f);
            foreach (var entry in race.entries)
            {
                Assert.IsTrue(SeriesEntryLists2025.TryNumber(series, entry.driverName, out int number), entry.driverName);
                Assert.AreEqual(number, entry.carNumber, $"{entry.driverName} races their own number");
            }
        }
    }

    // A driver's numbers are kept per series: Kyle Larson's Cup career is not his Xfinity career.
    [Test]
    public void CareersAreKeptPerSeries()
    {
        Assert.IsTrue(SeriesCareers2025.TryGet("Kyle Larson", RacingSeries.Cup, out var cup));
        Assert.AreEqual(402, cup.Starts);
        Assert.AreEqual(32, cup.Wins);
        CollectionAssert.AreEqual(new[] { 2021, 2025 }, cup.TitleYears);

        Assert.IsTrue(SeriesCareers2025.TryGet("Kyle Larson", RacingSeries.National, out var national));
        Assert.AreNotEqual(cup.Starts, national.Starts);

        Assert.IsTrue(SeriesCareers2025.TryGet("Jimmie Johnson", RacingSeries.Cup, out var jj));
        Assert.AreEqual(7, jj.Titles);

        Assert.IsFalse(SeriesCareers2025.TryGet("Layne Riggs", RacingSeries.Cup, out _), "no Cup start before 2026");
        Assert.IsTrue(SeriesCareers2025.TryGet("Layne Riggs", RacingSeries.Trucks, out _));
    }

    [Test]
    public void NamesMatchWithoutAccentsOrDots()
    {
        Assert.AreEqual(SeriesCareers2025.Key("Daniel Suárez"), SeriesCareers2025.Key("Daniel Suarez"));
        Assert.AreEqual(SeriesCareers2025.Key("J. J. Yeley"), SeriesCareers2025.Key("J.J. Yeley"));
        Assert.IsTrue(SeriesCareers2025.TryGet("Daniel Suarez", RacingSeries.Cup, out _));
    }
}
