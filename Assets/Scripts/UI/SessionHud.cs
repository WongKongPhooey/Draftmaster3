using UnityEngine;
using UnityEngine.SceneManagement;

// The in-car HUD a session starts from.
//
// F1–F9 each toggle a panel, and every one of them remembers its own state — some in a scene field, some
// in PlayerPrefs, some in a DontDestroyOnLoad singleton that outlives the scene. So the car used to roll out
// of its box under whatever was left up last time, or whatever a scene had serialised as visible: running
// order, tyres, telemetry, the TEAM box, all at once. Heading out resets it: every panel down, the lap timing
// readout (F1) included. Each key still brings its panel straight back.
//
// Called once per session, by PitLaneStart when the player first takes the car out — not on climbing back
// in after a tow, when whatever they have opened since is theirs to keep.
//
// It also runs on every scene load. Heading out alone left a window: a single race opens the setup sheet
// in the box before the car is handed over, and until CONFIRM the screen showed whatever the panels came
// up as — the running order from its old F2 pref, the TEAM box, and any DontDestroyOnLoad panel (F5, F8,
// F9…) still open from the last race. A scene now starts with every panel down.
public static class SessionHud
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void HookSceneLoads()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;   // statics survive a play-mode entry without domain reload
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // sceneLoaded lands after the new scene's Awake/OnEnable and before its Start, so nothing draws a frame
    // first.
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => HeadOut();

    public static void HeadOut()
    {
        foreach (var p in Object.FindObjectsByType<LapTimingManager>(FindObjectsSortMode.None)) p.showPlayerHud = false;      // F1

        foreach (var p in Object.FindObjectsByType<LeaderboardUI>(FindObjectsSortMode.None)) p.Visible = false;              // F2
        foreach (var p in Object.FindObjectsByType<TeamSwitchController>(FindObjectsSortMode.None)) p.Hidden = true;         // F3
        foreach (var p in Object.FindObjectsByType<RivalryFeed>(FindObjectsSortMode.None)) p.StandingsOpen = false;          // F4
        foreach (var p in Object.FindObjectsByType<DriverInfoPanel>(FindObjectsSortMode.None)) p.Open = false;               // F5
        foreach (var p in Object.FindObjectsByType<TireTempWearUI>(FindObjectsSortMode.None)) p.visible = false;             // F6
        foreach (var p in Object.FindObjectsByType<SponsorBoardPanel>(FindObjectsSortMode.None)) p.Hide();                   // F6
        foreach (var p in Object.FindObjectsByType<PixelUIShowcase>(FindObjectsSortMode.None)) p.open = false;               // F6
        foreach (var p in Object.FindObjectsByType<PlayerTelemetryHUD>(FindObjectsSortMode.None)) p.visible = false;         // F7
        FormationDiagnostics.Open = false;                                                                                   // F8
        foreach (var p in Object.FindObjectsByType<HandlingTuner>(FindObjectsSortMode.None)) p.Open = false;                 // F9
    }
}
