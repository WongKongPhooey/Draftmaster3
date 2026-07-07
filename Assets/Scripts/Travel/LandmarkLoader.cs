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
    const string ReturnKey = "landmark.return";

    static bool _reopenMap;

    public static bool SceneInBuild => Application.CanStreamedLevelBeLoaded(SceneName);

    // Park the car and step out: remember where we came from, then load the shared Landmark scene.
    public static void Visit(string locationId)
    {
        if (!SceneInBuild)
        {
            Debug.LogError("LandmarkLoader: 'Landmark' scene is not in Build Settings — run Draftmaster > Travel Map > Build Landmark Scene.");
            return;
        }
        PlayerPrefs.SetString(ReturnKey, SceneManager.GetActiveScene().name);
        PlayerPrefs.Save();
        SceneManager.LoadScene(SceneName);
    }

    // Back in the car: return to the scene the map was floating over and reopen it so the trip continues.
    public static void ExitToRoad()
    {
        _reopenMap = true;
        string back = PlayerPrefs.GetString(ReturnKey, "");
        if (!string.IsNullOrEmpty(back) && Application.CanStreamedLevelBeLoaded(back))
            SceneManager.LoadScene(back);
        else
            SceneManager.LoadScene(0); // return scene lost (cleared prefs?) — main menu beats a landmark loop
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (_reopenMap && scene.name != SceneName)
            {
                _reopenMap = false;
                TravelMapScreen.Open();
            }
        };
    }
}
