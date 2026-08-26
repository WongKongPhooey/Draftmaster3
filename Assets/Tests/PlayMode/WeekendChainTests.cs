using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// The weekend has to lead the player around the paddock on its own.
//
// Arriving at a track books whatever is next on the sheet, the objective marker points at it, and finishing
// it books the one after — so a driver who never opens the timetable still gets walked through their three
// days. This checks that chain with the game running, plus the two things that tell the player where they
// are: the day and time under the spawn card, and the team liaison stood outside the motorhome.
//
// The ledger is a save file, so it is snapshotted and put back — a test run must not spend somebody's
// weekend for them.
public class WeekendChainTests
{
    const string Race = "RaceScene";

    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static
                           | BindingFlags.Public | BindingFlags.NonPublic;

    static readonly string[] SavedPrefs =
    {
        "weekend.ledger", "weekend.appointment", "weekend.route", "weekend.greeted",
        "track.current", "raceweekend.id",
    };
    readonly Dictionary<string, string> _prefs = new();

    [OneTimeSetUp]
    public void BorrowTheWeekend()
    {
        foreach (string key in SavedPrefs)
            _prefs[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key, "") : null;
    }

    [OneTimeTearDown]
    public void GiveItBack()
    {
        foreach (var pair in _prefs)
        {
            if (pair.Value == null) PlayerPrefs.DeleteKey(pair.Key);
            else PlayerPrefs.SetString(pair.Key, pair.Value);
        }
        PlayerPrefs.Save();
    }

    [UnitySetUp]
    public IEnumerator ArriveAtTheTrack()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != Race)
        {
            // Arrive with an empty diary: whatever a previous fixture (or a previous session) left booked
            // is not what "turning up at the track books the next thing" is about.
            PlayerPrefs.DeleteKey("weekend.appointment");
            PlayerPrefs.Save();
            PlayModeScenes.Go(Race);
            yield return PlayModeScenes.WaitForScene(Race);
        }

        // The venues have to exist before anything can be booked at one.
        yield return PlayModeScenes.WaitFor(
            () => GameObject.Find("WeekendVenues") != null,
            "the paddock never built its weekend venues");
        for (int i = 0; i < 20; i++) yield return null;
    }

    // Turning up at the circuit is enough: something is booked, and it is the earliest thing the player
    // could still do.
    [UnityTest]
    public IEnumerator ArrivingAtTheTrackBooksWhateverIsNext()
    {
        yield return null;

        var appointment = PlayModeScenes.GameType("WeekendAppointment");
        var plan = PlayModeScenes.GameType("Draftmaster.Weekend.WeekendSchedulePlan");

        Assert.IsTrue((bool)appointment.GetProperty("Any", Any).GetValue(null),
                      "Nothing was booked on arrival, so the player is stood in a paddock with no idea " +
                      "where to go and no reason to open the sheet.");

        var pending = appointment.GetProperty("Pending", Any).GetValue(null);
        var next = plan.GetMethod("NextWorthDoing", Any).Invoke(null, null);
        Assert.IsNotNull(next, "The plan has nothing worth doing, but something is booked.");
        Assert.AreEqual(Id(next), Id(pending), "The booking is not the next thing on the sheet.");

        // ...and it is somewhere to walk to.
        Assert.IsNotNull(appointment.GetMethod("Target", Any).Invoke(null, null),
                         "The booked thing has nothing in the world to walk to.");
    }

    // Finish one and the next is already live. That is the difference between a schedule and a route.
    [UnityTest]
    public IEnumerator FinishingSomethingBooksTheNextThing()
    {
        yield return null;

        var appointment = PlayModeScenes.GameType("WeekendAppointment");
        var director = PlayModeScenes.GameType("WeekendDirector");

        var first = appointment.GetProperty("Pending", Any).GetValue(null);
        if (first == null) Assert.Ignore("Nothing booked to finish.");
        string firstId = Id(first);

        // Settle it the way a venue host does, with an empty result.
        var outcomeType = PlayModeScenes.GameType("Draftmaster.Weekend.WeekendOutcome");
        object outcome = outcomeType.GetProperty("Nothing", Any).GetValue(null);

        var finish = director.GetMethod("Finish", Any);
        finish.Invoke(null, new[] { first, outcome, (object)true });
        yield return null;

        var second = appointment.GetProperty("Pending", Any).GetValue(null);
        Assert.IsNotNull(second, "Finishing a booking left the player with nowhere to be next.");
        Assert.AreNotEqual(firstId, Id(second), "The same booking is still live after being completed.");
    }

    // The spawn card says where AND when: a weekend is a schedule, and "Friday, 9:30 AM" is half of what
    // the player needs to know as they open their eyes in the motorhome.
    [UnityTest]
    public IEnumerator TheSpawnCardSaysWhatDayAndTimeItIs()
    {
        yield return null;

        var introType = PlayModeScenes.GameType("SpawnIntroUI");
        var intro = introType.GetProperty("Instance", Any).GetValue(null);
        Assert.IsNotNull(intro, "No spawn card was shown at all.");

        string subtitle = introType.GetProperty("SpawnSubtitle", Any).GetValue(intro) as string;
        Assert.IsNotEmpty(subtitle, "The spawn card has no day and time under the track name.");

        var slots = PlayModeScenes.GameType("Draftmaster.Weekend.WeekendSlots");
        var ledger = PlayModeScenes.GameType("Draftmaster.Weekend.WeekendLedger");
        var slot = ledger.GetProperty("CurrentSlot", Any).GetValue(null);
        string day = (string)slots.GetMethod("Day", Any).Invoke(null, new[] { slot });

        StringAssert.Contains(day, subtitle, "The spawn card names the wrong day.");
        StringAssert.Contains(":", subtitle, "The spawn card gives no time of day.");
    }

    // Somebody has to tell the player the weekend exists. When the next thing is an obligation rather than
    // their own session, the team liaison is stood outside the motorhome to say so.
    [UnityTest]
    public IEnumerator TheLiaisonIsThereWhenThereIsSomewhereToBe()
    {
        yield return null;

        var plan = PlayModeScenes.GameType("Draftmaster.Weekend.WeekendSchedulePlan");
        var next = plan.GetMethod("NextWorthDoing", Any).Invoke(null, null);
        if (next == null) Assert.Ignore("Nothing left on the sheet to be told about.");

        bool onTrack = (bool)next.GetType().GetProperty("IsOnTrack", Any).GetValue(next);
        if (onTrack) Assert.Ignore("The next thing is the player's own session — the race engineer meets them instead.");

        var placed = PlayModeScenes.GameType("PlacedNPC");
        var roleField = placed.GetField("role");
        bool found = false;
        foreach (var npc in Object.FindObjectsByType(placed, FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (roleField.GetValue(npc).ToString() == "TeamLiaison") { found = true; break; }

        Assert.IsTrue(found, "No team liaison outside the motorhome, so nobody tells the player where they are due.");
    }

    static string Id(object activity) => activity?.GetType().GetField("id").GetValue(activity) as string;
}
