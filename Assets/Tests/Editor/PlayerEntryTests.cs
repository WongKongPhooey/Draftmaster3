using System.Linq;
using System.Reflection;
using Draftmaster.Weekend;
using NUnit.Framework;

// Which championship the player is in, when nobody has said.
//
// This decides what the whole weekend IS. The sheet is built per series — your practice, and two other
// people's to watch — so an entry that disagrees with the car turns somebody else's session into yours.
// That is what happened: nothing had ever chosen a championship, the fallback was the bottom of the ladder,
// and so a Cup driver in a Cup car was told the TRUCK practice was his own hour in the car. The objective
// marker duly pointed at his Cup car and asked him to go and drive it, instead of sending him to the
// grandstand to watch, which is what a Cup driver does while the trucks are out.
//
// Assembly-CSharp cannot be referenced from an asmdef, so PlayerEntry is reached by reflection.
public class PlayerEntryTests
{
    static readonly System.Type EntryType = System.Type.GetType("PlayerEntry, Assembly-CSharp");

    static bool TrySeries(string carset, out RacingSeries series)
    {
        var method = EntryType.GetMethod("TrySeriesFromCarset", BindingFlags.Public | BindingFlags.Static);
        var args = new object[] { carset, default(RacingSeries) };
        bool ok = (bool)method.Invoke(null, args);
        series = (RacingSeries)args[1];
        return ok;
    }

    [Test]
    public void TheCarSaysWhichChampionshipItRacesIn()
    {
        Assert.IsTrue(TrySeries("cup26", out var cup));
        Assert.AreEqual(RacingSeries.Cup, cup, "A car in Cup paint is a Cup entry.");

        Assert.IsTrue(TrySeries("cts25", out var trucks));
        Assert.AreEqual(RacingSeries.Trucks, trucks);

        Assert.IsTrue(TrySeries("xfi25", out var national));
        Assert.AreEqual(RacingSeries.National, national);
    }

    [Test]
    public void AnUnknownCarsetIsNotGuessedAt()
    {
        // Better to leave the entry alone than to enter somebody in a championship on a hunch: the answer
        // is only used when nobody has chosen, and a wrong guess is indistinguishable from a choice.
        Assert.IsFalse(TrySeries("", out _));
        Assert.IsFalse(TrySeries(null, out _));
        Assert.IsFalse(TrySeries("somebodys-mod-paint", out _));
    }

    [Test]
    public void AChampionshipsOwnSessionsAreDrivableAndTheOthersAreWatched()
    {
        // The rule the entry feeds. Getting the entry wrong flips every one of these.
        var sheet = WeekendTimetable.Build(RacingSeries.Cup, 1, "Watkins Glen");

        var mine = sheet.Activities.FirstOrDefault(a => a.series == RacingSeries.Cup &&
                                                        a.kind == ActivityKind.Practice);
        var theirs = sheet.Activities.FirstOrDefault(a => a.series == RacingSeries.Trucks &&
                                                         a.kind == ActivityKind.SpectatePractice);

        Assert.IsNotNull(mine, "A Cup entry should have a Cup practice to drive.");
        Assert.IsNotNull(theirs, "A Cup entry should have the truck practice to watch.");

        Assert.AreEqual(WeekendVenue.None, WeekendVenues.For(mine.kind),
                        "Your own session is a scene load, not somewhere to walk to.");
        Assert.AreEqual(WeekendVenue.Grandstand, WeekendVenues.For(theirs.kind),
                        "Somebody else's practice is watched from the grandstand.");
    }
}
