using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// One router for every career scene change, so a co-op guest follows the host without any of the calling
// code having to know a guest exists.
//
// This is the whole of "the guest is carried through a time skip". The career already funnels its scene
// changes through a handful of LoadScene calls — the weekend director routing a booked session, the travel
// map, a landmark, the garage sheet — and NGO's scene management drags every connected client into whatever
// the server loads. Swapping those calls for CoopScene.Load covers all of them at once.
//
// What it does NOT cover is a jump that changes no scene: an in-place time skip, or the host walking
// somewhere the guest did not follow. Coop.RecallGuest handles those.
public static class CoopScene
{
    // Load a scene the way this session requires: through NGO when we are hosting co-op (so the guest comes
    // too), plain otherwise.
    public static void Load(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;

        var nm = NetworkManager.Singleton;

        // Hosting co-op with the transport up: the server owns scene changes and replicates them.
        if (Coop.Active && nm != null && nm.IsListening && nm.IsServer)
        {
            if (nm.NetworkConfig != null && nm.NetworkConfig.EnableSceneManagement && nm.SceneManager != null)
            {
                var status = nm.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
                if (status == SceneEventProgressStatus.Started) return;

                // Scene management can legitimately refuse — another scene event is already in flight, or the
                // scene is not in the build list on every peer. Falling through to a local load keeps the
                // HOST playable; the guest is then out of step, which the mirror's next push reports.
                Debug.LogWarning($"CoopScene.Load('{sceneName}'): NGO refused the scene event ({status}) — " +
                                 "loading locally, the guest will not follow.");
            }
            else
            {
                Debug.LogWarning("CoopScene.Load: NGO scene management is off — the guest will not follow.");
            }
        }

        // A guest never initiates a career scene change: the host owns the weekend, and a guest-side load
        // would drop it out of the host's scene with nothing to pull it back.
        if (Coop.IsGuest)
        {
            Debug.LogWarning($"CoopScene.Load('{sceneName}'): ignored — the guest does not drive the career.");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    // Same, for the callers that already hold a build index rather than a name.
    public static void Load(int buildIndex)
    {
        if (Coop.Active && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(buildIndex);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrEmpty(name)) { Load(name); return; }
        }
        SceneManager.LoadScene(buildIndex);
    }
}
