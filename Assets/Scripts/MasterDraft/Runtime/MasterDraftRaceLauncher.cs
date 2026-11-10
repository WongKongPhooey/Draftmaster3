using Draftmaster.MasterDraft;
using UnityEngine;
using UnityEngine.SceneManagement;

// Hands a Master Draft race to the shared race scene the way SINGLE RACE does, but in GameSession.Mode.MasterDraft,
// so the race scene spawns a MasterDraftRaceHost and its results screen offers the way back to the Master Draft menu.
//
//  * LaunchSeriesRound (the Series screen): the next round of a championship in progress — its track, laps, the
//    championship car's livery, number and name, the series' AI level as the field's strength
//    (AIDifficulty.MasterDraftStrengthScale) and the car's class step (applied by the host). No cards.
//  * LaunchChallenge (the Challenges screen): one chapter of a live challenge event, the same way — its track and
//    laps, the garage car picked (or the car the chapter hands over), the chapter's AI level and the class step.
//    A moment (start at lap N, a set field order) runs as a race over the laps it has left from the green; see
//    ChallengeRace for what RaceScene can't stage yet.
//  * Launch (Go Race, the card mode until it retires): a Quick Race with the action hand and team effects.
//
// The race reads which track, which driver and whether the session is live from the career's own PlayerPrefs
// keys, so those are parked first (OneOffRacePrefs) and put back when the Master Draft menu or the title screen
// next opens.
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

        // A card race runs the field at the OPTIONS strength in the scene's own paint scheme family.
        AIDifficulty.MasterDraftStrengthScale = 0f;
        PlayerCarLivery.MasterDraftLivery = null;

        session.Pending = new MasterDraftSession.PendingRace { trackId = trackId, driverUid = driver.uid };
        session.LastRace = null;
        session.LastSeries = null;
        session.LastChallenge = null;

        GoLive();
        return null;
    }

    // The next round of the championship in progress for seriesId. Null on success (the scene is loading),
    // otherwise why not.
    public static string LaunchSeriesRound(string seriesId)
    {
        var session = MasterDraftSession.Instance;
        if (session == null || session.Profile == null) return "Your garage isn't loaded.";

        var series = session.Series?.Get(seriesId);
        var plan = SeriesRace.Plan(session.Profile, seriesId, session.Cars, out string error, series);
        if (plan == null) return error;
        if (!TrackCatalog.HasGeometry(plan.trackId)) return $"{TrackCatalog.DisplayName(plan.trackId)} has no layout built.";
        if (!Application.CanStreamedLevelBeLoaded(RaceScene)) return $"{RaceScene} isn't in the build settings.";

        OneOffRacePrefs.Capture();
        if (!TrackSelection.Select(plan.trackId)) return $"Couldn't select {plan.trackId}.";
        Stage(plan);

        session.Pending = new MasterDraftSession.PendingRace { trackId = plan.trackId, series = plan };
        session.LastRace = null;
        session.LastSeries = null;
        session.LastChallenge = null;

        GoLive();
        return null;
    }

    // One chapter of a challenge event, in the garage car carId (ignored when the chapter hands a car over). Null on
    // success (the scene is loading), otherwise why not.
    public static string LaunchChallenge(string chapterId, string carId)
    {
        var session = MasterDraftSession.Instance;
        if (session == null || session.Profile == null) return "Your garage isn't loaded.";

        var challenges = session.Challenges;
        var plan = ChallengeRace.Plan(session.Profile, challenges, chapterId, carId, session.Cars,
            MasterDraftSession.ChallengeWeek(challenges), out string error);
        if (plan == null) return error;
        if (!TrackCatalog.HasGeometry(plan.trackId)) return $"{TrackCatalog.DisplayName(plan.trackId)} has no layout built.";
        if (!Application.CanStreamedLevelBeLoaded(RaceScene)) return $"{RaceScene} isn't in the build settings.";

        OneOffRacePrefs.Capture();
        if (!TrackSelection.Select(plan.trackId)) return $"Couldn't select {plan.trackId}.";
        Stage(plan.number, plan.driver, plan.livery, plan.aiStrength);

        session.Pending = new MasterDraftSession.PendingRace { trackId = plan.trackId, challenge = plan };
        session.LastRace = null;
        session.LastSeries = null;
        session.LastChallenge = null;

        GoLive();
        return null;
    }

    // What the race scene reads before anything in it starts: who the player is, the paint, the field's strength.
    // Laps and the class step are the host's (MasterDraftRaceHost), from session.Pending.series / .challenge.
    public static void Stage(SeriesRacePlan plan) => Stage(plan.number, plan.driver, plan.livery, plan.aiStrength);

    static void Stage(int number, string driver, string livery, float aiStrength)
    {
        if (number > 0) PlayerPrefs.SetInt(PlayerDriver.NumberKey, number);
        if (!string.IsNullOrWhiteSpace(driver)) PlayerPrefs.SetString(PlayerDriver.NameKey, driver);
        PlayerPrefs.Save();
        PlayerCarLivery.MasterDraftLivery = livery;
        AIDifficulty.MasterDraftStrengthScale = aiStrength;
    }

    // One race, live the moment the scene loads, no career around it — the SINGLE RACE shape.
    static void GoLive()
    {
        GameSession.CurrentMode = GameSession.Mode.MasterDraft;
        RaceWeekend.Current = RaceWeekend.Session.Race;
        RaceWeekend.SessionLive = true;
        SceneManager.LoadScene(RaceScene);
    }
}
