using Draftmaster.MasterDraft;
using UnityEngine;
using UnityEngine.SceneManagement;

// GO on the Go Race screen: hands a Quick Race to the shared race scene the way SINGLE RACE does, but in
// GameSession.Mode.MasterDraft, so the race scene spawns a MasterDraftRaceHost (action hand, team effects,
// coins at the flag) and its results screen offers the way back to the Master Draft menu.
//
// The race reads which track, which driver and whether the session is live from the career's own
// PlayerPrefs keys, so those are parked first (OneOffRacePrefs) and put back when the Master Draft menu or
// the title screen next opens.
public static class MasterDraftRaceLauncher
{
    public const string RaceScene = "RaceScene";
    public const string MenuScene = "MasterDraft";

    // Null on success (the scene is loading), otherwise why not.
    public static string Launch(string trackId, CardInstance driver)
    {
        var session = MasterDraftSession.Instance;
        if (session == null || session.Profile == null) return "Your collection isn't loaded.";

        var problems = TeamRules.Validate(session.Profile.team, session.Profile);
        if (problems.Count > 0) return problems[0];
        if (!TrackCatalog.HasGeometry(trackId)) return "That track has no layout built.";
        if (driver == null || driver.def.type != CardType.Driver) return "Pick a driver.";
        if (!Application.CanStreamedLevelBeLoaded(RaceScene)) return $"{RaceScene} isn't in the build settings.";

        OneOffRacePrefs.Capture();
        TrackSelection.Select(trackId);

        // The driver card is who the player races as: their number (and so their livery) and their name.
        if (driver.def.carNumber > 0) PlayerPrefs.SetInt(PlayerDriver.NumberKey, driver.def.carNumber);
        if (!string.IsNullOrWhiteSpace(driver.def.name)) PlayerPrefs.SetString(PlayerDriver.NameKey, driver.def.name);
        PlayerPrefs.Save();

        session.Pending = new MasterDraftSession.PendingRace { trackId = trackId, driverUid = driver.uid };
        session.LastRace = null;

        // One race, live the moment the scene loads, no career around it — the SINGLE RACE shape.
        GameSession.CurrentMode = GameSession.Mode.MasterDraft;
        RaceWeekend.Current = RaceWeekend.Session.Race;
        RaceWeekend.SessionLive = true;

        SceneManager.LoadScene(RaceScene);
        return null;
    }
}
