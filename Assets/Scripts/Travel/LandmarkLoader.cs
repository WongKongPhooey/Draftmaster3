using UnityEngine;
using UnityEngine.SceneManagement;

// Round-trip into the shared on-foot Landmark scene from the travel map, and back. One scene serves
// all 13 minor locations: LandmarkDirector inside it reads TravelState.CurrentNodeId and instantiates
// that location's environment prefab. The scene the player came from is remembered (PlayerPrefs, so a
// mid-visit quit loses nothing) and the travel map reopens automatically on return — walking around
// never costs a stop.
public static class LandmarkLoader
{
    public const string SceneName = "Landmark";
    // Team HQ is its own scene (the on-foot factory), not a dressed copy of the shared Landmark.
    public const string TeamHQSceneName = "TeamGarage";
    const string ReturnKey = "landmark.return";
    const string VisitKey = "landmark.scene";

    static bool _reopenMap;

    public static bool SceneInBuild => Application.CanStreamedLevelBeLoaded(SceneName);

    // Park the car and step out: remember where we came from, then load the shared Landmark scene.
    public static void Visit(string locationId) => VisitScene(SceneName);

    public static bool TeamHQInBuild => Application.CanStreamedLevelBeLoaded(TeamHQSceneName);

    // Pull in at Team HQ: the factory floor, where the crew, the laptop and the Paint Scheme Designer are.
    public static void VisitTeamHQ() => VisitScene(TeamHQSceneName);

    static void VisitScene(string sceneName)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"LandmarkLoader: '{sceneName}' scene is not in Build Settings.");
            return;
        }
        PlayerPrefs.SetString(ReturnKey, SceneManager.GetActiveScene().name);
        PlayerPrefs.SetString(VisitKey, sceneName);
        PlayerPrefs.Save();
        CoopScene.Load(sceneName);
    }

    // True in a scene the player walked into off the travel map — its way out goes back to the road.
    public static bool CameFromRoad =>
        PlayerPrefs.GetString(VisitKey, "") == SceneManager.GetActiveScene().name &&
        !string.IsNullOrEmpty(PlayerPrefs.GetString(ReturnKey, ""));

    // Back in the car: return to the scene the map was floating over and reopen it so the trip continues.
    public static void ExitToRoad()
    {
        _reopenMap = true;
        string back = PlayerPrefs.GetString(ReturnKey, "");
        PlayerPrefs.DeleteKey(VisitKey);
        PlayerPrefs.Save();
        if (!string.IsNullOrEmpty(back) && Application.CanStreamedLevelBeLoaded(back))
            CoopScene.Load(back);
        else
            CoopScene.Load(0); // return scene lost (cleared prefs?) — main menu beats a landmark loop
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (_reopenMap && scene.name != SceneName && scene.name != TeamHQSceneName)
            {
                _reopenMap = false;
                TravelMapScreen.Open();
            }
        };
    }
}
