using Draftmaster.Tracks;
using NUnit.Framework;

// The rules behind SINGLE RACE's Track / Series / Car select screens (Draftmaster.Tracks.RaceFlow), engine-free so
// they also run in Tools/HeadlessTests. The screen itself is covered by SingleRaceScreenTests.
public class RaceFlowTests
{
    [Test]
    public void EveryCalendarVenueHasAPlace()
    {
        foreach (var dim in TrackDimensions.All)
            Assert.IsTrue(RaceFlow.HasPlace(dim.id), $"{dim.id} has no town for its track tile.");
        Assert.AreEqual("Watkins Glen, NY", RaceFlow.Place("WatkinsGlen"));
        Assert.AreEqual("USA", RaceFlow.Place("Nowhere", "USA"));
    }

    [Test]
    public void TagsAndFiltersFollowTheDesign()
    {
        Assert.AreEqual(RaceFlow.TrackTag.Street, RaceFlow.Tag("SanDiego", roadCourse: true, dirt: false));
        Assert.AreEqual(RaceFlow.TrackTag.Road, RaceFlow.Tag("WatkinsGlen", true, false));
        Assert.AreEqual(RaceFlow.TrackTag.Oval, RaceFlow.Tag("Daytona", false, false));
        Assert.AreEqual(RaceFlow.TrackTag.Dirt, RaceFlow.Tag("Eldora", false, true));

        // ROAD includes the street circuits.
        Assert.IsTrue(RaceFlow.Passes(RaceFlow.TrackFilter.Road, RaceFlow.TrackTag.Street));
        Assert.IsFalse(RaceFlow.Passes(RaceFlow.TrackFilter.Oval, RaceFlow.TrackTag.Road));
        Assert.IsTrue(RaceFlow.Passes(RaceFlow.TrackFilter.All, RaceFlow.TrackTag.Dirt));

        Assert.AreEqual(RaceFlow.TrackFilter.Dirt, RaceFlow.StepFilter(RaceFlow.TrackFilter.All, -1));
        Assert.AreEqual(RaceFlow.TrackFilter.All, RaceFlow.StepFilter(RaceFlow.TrackFilter.Dirt, 1));
    }

    [Test]
    public void LabelsReadLikeTheReference()
    {
        Assert.AreEqual("2.45 MI", RaceFlow.Miles(2.45f));
        Assert.AreEqual("3.0 MI", RaceFlow.Miles(3f));
        Assert.AreEqual("2.439 MI", RaceFlow.Miles(2.439f));
        Assert.AreEqual("48 CARS", RaceFlow.FieldChip(48));
        Assert.AreEqual("NO FIELD", RaceFlow.FieldChip(0));
        Assert.AreEqual("No field entered", RaceFlow.FieldLine(0));
        Assert.AreEqual("9 / 17", RaceFlow.Position(8, 17));
        Assert.AreEqual("van Gisbergen", RaceFlow.Surname("Shane", "van Gisbergen", null));
        Assert.AreEqual("Emerson", RaceFlow.Surname(null, null, "#89 Ace Emerson"));
        Assert.AreEqual(0xc4452fu, RaceFlow.SeriesColour("CUP", 0));
        Assert.AreEqual("OPEN WHEEL", RaceFlow.SeriesKind(1));
    }

    [Test]
    public void TheCursorWalksTheGrid()
    {
        // 9 cars, 6 across: row 0 = 0..5, row 1 = 6..8.
        Assert.AreEqual(1, RaceFlow.Move(0, 9, 6, 1, 0));
        Assert.AreEqual(6, RaceFlow.Move(5, 9, 6, 1, 0), "right crosses the row end");
        Assert.AreEqual(0, RaceFlow.Move(0, 9, 6, -1, 0), "left stops at the first tile");
        Assert.AreEqual(8, RaceFlow.Move(4, 9, 6, 0, 1), "down with nothing below lands on the last tile");
        Assert.AreEqual(7, RaceFlow.Move(7, 9, 6, 0, 1), "down from the last row stays");
        Assert.AreEqual(2, RaceFlow.Move(8, 9, 6, 0, -1));
        Assert.AreEqual(1, RaceFlow.Move(1, 9, 6, 0, -1), "up from the first row stays");
    }

    [Test]
    public void ColumnsAndScaleFollowTheHandoff()
    {
        Assert.AreEqual(4, RaceFlow.Columns(640 - 8, 140, 10, 4));
        Assert.AreEqual(6, RaceFlow.Columns(640 - 8, 92, 8, 6));
        Assert.AreEqual(5, RaceFlow.Columns(800 - 8, 150, 10, 5));
        Assert.AreEqual(3, RaceFlow.Columns(480, 140, 10, 4));
        Assert.AreEqual(1, RaceFlow.Columns(50, 140, 10, 4));

        Assert.AreEqual(3, RaceFlow.ScaleFactor(1920, 1080, false));
        Assert.AreEqual(2, RaceFlow.ScaleFactor(1366, 768, false));
        Assert.AreEqual(4, RaceFlow.ScaleFactor(2560, 1440, false));
        Assert.AreEqual(3, RaceFlow.ScaleFactor(2400, 1080, true));
        Assert.AreEqual(1, RaceFlow.ScaleFactor(500, 300, false));
    }

    [Test]
    public void OnlyTheBuiltVenuesCanBePicked()
    {
        foreach (var id in new[] { "WatkinsGlen", "Daytona", "FortWorth" })
            Assert.IsTrue(RaceFlow.IsAuthored(id), id);
        foreach (var id in new[] { "Martinsville", "Talladega", "Phoenix", "", null })
            Assert.IsFalse(RaceFlow.IsAuthored(id), id ?? "null");
    }
}
