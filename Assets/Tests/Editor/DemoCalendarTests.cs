using Draftmaster.Tracks;
using NUnit.Framework;
using UnityEngine;

// The demo's three rounds: finishing one has to move the career on, not reload the venue just raced.
public class DemoCalendarTests
{
    [Test]
    public void Rounds_RunWatkinsGlen_Daytona_Martinsville()
    {
        CollectionAssert.AreEqual(new[] { "WatkinsGlen", "Daytona", "Martinsville" }, DemoCalendar.Rounds);
        Assert.AreEqual("WatkinsGlen", DemoCalendar.Opener);
    }

    [Test]
    public void After_WalksTheCalendarInOrder_AndWrapsAtTheEnd()
    {
        Assert.AreEqual("Daytona", DemoCalendar.After("WatkinsGlen"));
        Assert.AreEqual("Martinsville", DemoCalendar.After("Daytona"));
        Assert.AreEqual("WatkinsGlen", DemoCalendar.After("Martinsville"));
    }

    [Test]
    public void After_AnOffCalendarTrack_RejoinsAtTheOpener()
    {
        Assert.AreEqual("WatkinsGlen", DemoCalendar.After("Bristol"));
        Assert.AreEqual("WatkinsGlen", DemoCalendar.After(null));
    }

    [Test]
    public void EveryRound_HasGeometryAndAPackage()
    {
        foreach (var id in DemoCalendar.Rounds)
        {
            Assert.IsNotNull(Resources.Load<ScriptableObject>("Tracks/" + id), $"no geometry for {id}");
            Assert.IsNotNull(Resources.Load<GameObject>("TrackPackages/" + id), $"no package for {id}");
        }
    }
}
