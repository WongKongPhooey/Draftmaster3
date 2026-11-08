using System.Linq;
using Draftmaster.Weekend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Draftmaster > AI > Watch AI-Only Race… — spin up a race with nobody driving, to watch the AI in the editor.
//
// Enters play mode straight into RaceScene as a SINGLE RACE at the chosen track and series, with the player car
// handed to the AI before the formation lap (AIOnlyRaceWatch does the runtime half). The race scene reads its track,
// series and session from the career's own PlayerPrefs, so those keys are snapshotted here and put back the moment
// play mode ends — watching an AI race never moves the career. The snapshot lives in EditorPrefs, so an editor that
// dies mid-race puts the career back on its next load.
//
//   Watch AI-Only Race…                    the window: track, series, field size, Start.
//   Watch AI-Only Race (Last Settings)     the same race again, no window.
[InitializeOnLoad]
public class AIOnlyRaceMenu : EditorWindow
{
    const string RaceScenePath = "Assets/Scenes/RaceScene.unity";

    const string PrefTrack = "Draftmaster.AIOnlyRace.Track";
    const string PrefSeries = "Draftmaster.AIOnlyRace.Series";
    const string PrefCars = "Draftmaster.AIOnlyRace.Cars";
    const string PrefLaps = "Draftmaster.AIOnlyRace.Laps";
    const int MaxLaps = 200;

    const string Pending = "Draftmaster.AIOnlyRace.Pending";
    const string SnapPrefix = "Draftmaster.AIOnlyRace.Snap.";
    const string PrevStartScene = "Draftmaster.AIOnlyRace.PrevStartScene";

    // Every career key the launch writes (the same set OneOffRacePrefs guards for SINGLE RACE).
    static readonly string[] StringKeys = { "track.current", "career.savedat" };
    static readonly string[] IntKeys = { "weekend.series", "raceweekend.sessionlive" };

    static AIOnlyRaceMenu()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;

        // An editor that closed or crashed mid-race never saw EnteredEditMode: put the career back now.
        if (!EditorApplication.isPlayingOrWillChangePlaymode && EditorPrefs.GetBool(Pending, false))
            EditorApplication.delayCall += RestoreCareer;
    }

    // ------------------------------------------------------------------ menu

    [MenuItem("Draftmaster/AI/Watch AI-Only Race…", priority = 280)]
    static void Open()
    {
        var w = GetWindow<AIOnlyRaceMenu>(true, "AI-Only Race");
        w.minSize = new Vector2(320f, 170f);
    }

    [MenuItem("Draftmaster/AI/Watch AI-Only Race (Last Settings)", priority = 281)]
    static void LaunchLast() => Launch(LastTrack(), (RacingSeries)EditorPrefs.GetInt(PrefSeries, (int)SeriesCatalog.PlayerSeries),
                                       EditorPrefs.GetInt(PrefCars, 0), EditorPrefs.GetInt(PrefLaps, 0));

    [MenuItem("Draftmaster/AI/Watch AI-Only Race…", true)]
    [MenuItem("Draftmaster/AI/Watch AI-Only Race (Last Settings)", true)]
    static bool ValidateLaunch() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ------------------------------------------------------------------ window

    string[] _ids;
    string[] _labels;
    int _trackIndex;
    RacingSeries _series;
    int _cars;
    int _laps;

    void OnEnable()
    {
        var rows = TrackCatalog.Playable().OrderBy(t => t.DisplayName ?? t.Name).ToList();
        _ids = rows.Select(t => t.Name).ToArray();
        _labels = rows.Select(t => string.IsNullOrEmpty(t.DisplayName) ? TrackCatalog.Nicify(t.Name) : t.DisplayName).ToArray();
        _trackIndex = Mathf.Max(0, System.Array.IndexOf(_ids, LastTrack()));
        _series = (RacingSeries)EditorPrefs.GetInt(PrefSeries, (int)SeriesCatalog.PlayerSeries);
        _cars = EditorPrefs.GetInt(PrefCars, 0);
        _laps = EditorPrefs.GetInt(PrefLaps, 0);
    }

    void OnGUI()
    {
        if (_ids == null || _ids.Length == 0)
        {
            EditorGUILayout.HelpBox("No track has geometry built (Resources/Tracks).", MessageType.Warning);
            return;
        }

        _trackIndex = EditorGUILayout.Popup("Track", _trackIndex, _labels);
        _series = (RacingSeries)EditorGUILayout.EnumPopup("Series", _series);
        _cars = EditorGUILayout.IntSlider(new GUIContent("AI cars", "0 = the race scene's GridSpawner count. " +
                                                          "The player car runs on Auto on top of these."), _cars, 0, 43);
        _laps = EditorGUILayout.IntSlider(new GUIContent("Race laps", "0 = the race scene's RaceDirector distance. " +
                                                          "More laps = more data for the race logger."), _laps, 0, MaxLaps);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            if (GUILayout.Button("Start AI-Only Race", GUILayout.Height(28f)))
            {
                string id = _ids[_trackIndex];
                var series = _series;
                int cars = _cars, laps = _laps;
                // Launch after this GUI pass: entering play mode from inside OnGUI can trip layout errors.
                EditorApplication.delayCall += () => Launch(id, series, cars, laps);
                Close();
            }

        EditorGUILayout.HelpBox("In play: , . step the camera through the running order, [ ] change game speed, " +
                                "V takes the player car back. The career's track/series/session are restored on stop.",
                                MessageType.None);
    }

    static string LastTrack()
    {
        string id = EditorPrefs.GetString(PrefTrack, "");
        return TrackCatalog.HasGeometry(id) ? id : TrackSelection.CurrentId;
    }

    // ------------------------------------------------------------------ launch

    static void Launch(string trackId, RacingSeries series, int cars, int laps)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[AIOnlyRace] Stop play mode first.");
            return;
        }
        if (!TrackCatalog.HasGeometry(trackId))
        {
            Debug.LogWarning($"[AIOnlyRace] '{trackId}' has no geometry asset — pick a built track.");
            return;
        }
        var raceScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(RaceScenePath);
        if (raceScene == null)
        {
            Debug.LogError($"[AIOnlyRace] {RaceScenePath} not found.");
            return;
        }
        // Unsaved edits in the open scene would be lost on the way into play mode.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorPrefs.SetString(PrefTrack, trackId);
        EditorPrefs.SetInt(PrefSeries, (int)series);
        EditorPrefs.SetInt(PrefCars, cars);
        EditorPrefs.SetInt(PrefLaps, laps);

        // A one-off race left stashed from an earlier run would be restored over these writes on boot
        // (OneOffRacePrefs.RestoreOnBoot): settle it first, so the snapshot below is the real career.
        if (OneOffRacePrefs.Stashed) OneOffRacePrefs.Restore();

        // Snapshot once: a second launch before the restore ran must not snapshot the AI race's own values.
        if (!EditorPrefs.GetBool(Pending, false))
        {
            Snapshot();
            var prev = EditorSceneManager.playModeStartScene;
            EditorPrefs.SetString(PrevStartScene, prev != null ? AssetDatabase.GetAssetPath(prev) : "");
            EditorPrefs.SetBool(Pending, true);
        }

        // Written straight to the keys rather than through TrackSelection.Select / RaceWeekend.SessionLive, which
        // also date the career save: watching the AI is not progress.
        PlayerPrefs.SetString("track.current", trackId);
        PlayerPrefs.SetInt("weekend.series", (int)series);
        PlayerPrefs.SetInt("raceweekend.sessionlive", 1);
        PlayerPrefs.SetInt(AIOnlyRaceWatch.FlagKey, 1);
        PlayerPrefs.SetInt(AIOnlyRaceWatch.CarsKey, Mathf.Max(0, cars));
        PlayerPrefs.SetInt(AIOnlyRaceWatch.LapsKey, Mathf.Clamp(laps, 0, MaxLaps));
        PlayerPrefs.Save();

        EditorSceneManager.playModeStartScene = raceScene;
        Debug.Log($"[AIOnlyRace] Starting an AI-only {SeriesCatalog.Name(series)} race at {TrackCatalog.DisplayName(trackId)}.");
        EditorApplication.isPlaying = true;
    }

    // ------------------------------------------------------------------ restore

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode && EditorPrefs.GetBool(Pending, false))
            RestoreCareer();
    }

    static void Snapshot()
    {
        foreach (var key in StringKeys)
        {
            bool has = PlayerPrefs.HasKey(key);
            EditorPrefs.SetBool(SnapPrefix + key + ".has", has);
            if (has) EditorPrefs.SetString(SnapPrefix + key, PlayerPrefs.GetString(key));
        }
        foreach (var key in IntKeys)
        {
            bool has = PlayerPrefs.HasKey(key);
            EditorPrefs.SetBool(SnapPrefix + key + ".has", has);
            if (has) EditorPrefs.SetInt(SnapPrefix + key, PlayerPrefs.GetInt(key));
        }
    }

    static void RestoreCareer()
    {
        if (!EditorPrefs.GetBool(Pending, false)) return;

        foreach (var key in StringKeys)
        {
            if (EditorPrefs.GetBool(SnapPrefix + key + ".has", false))
                PlayerPrefs.SetString(key, EditorPrefs.GetString(SnapPrefix + key, ""));
            else PlayerPrefs.DeleteKey(key);
            EditorPrefs.DeleteKey(SnapPrefix + key);
            EditorPrefs.DeleteKey(SnapPrefix + key + ".has");
        }
        foreach (var key in IntKeys)
        {
            if (EditorPrefs.GetBool(SnapPrefix + key + ".has", false))
                PlayerPrefs.SetInt(key, EditorPrefs.GetInt(SnapPrefix + key, 0));
            else PlayerPrefs.DeleteKey(key);
            EditorPrefs.DeleteKey(SnapPrefix + key);
            EditorPrefs.DeleteKey(SnapPrefix + key + ".has");
        }
        // Never consumed if play mode was cancelled before the scene loaded: it must not turn the next Play into
        // an AI race.
        PlayerPrefs.DeleteKey(AIOnlyRaceWatch.FlagKey);
        PlayerPrefs.DeleteKey(AIOnlyRaceWatch.CarsKey);
        PlayerPrefs.DeleteKey(AIOnlyRaceWatch.LapsKey);
        PlayerPrefs.Save();

        string prev = EditorPrefs.GetString(PrevStartScene, "");
        EditorSceneManager.playModeStartScene =
            string.IsNullOrEmpty(prev) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(prev);
        EditorPrefs.DeleteKey(PrevStartScene);
        EditorPrefs.SetBool(Pending, false);

        // With domain reload off in Enter Play Mode Options these statics would carry the one-off race into the
        // next Play press. Put them back to what a fresh boot has.
        GameSession.CurrentMode = GameSession.Mode.SinglePlayer;
        RaceWeekend.Current = RaceWeekend.Session.Practice;

        Debug.Log("[AIOnlyRace] Career track, series and session restored.");
    }
}
