using UnityEngine;
using UnityEngine.SceneManagement;

// Puts the selected track into the shared race scene, before anything else asks where the road is.
//
// Order matters here. Almost every race system reads its TrackBuilder in Awake or Start (GridSpawner waits
// on the database, PitLaneStart samples the pit lane immediately, SplineDriver needs a spline the frame it
// enables), so the package has to exist and be bound BEFORE any of them run. That's what
// RuntimeInitializeLoadType.BeforeSceneLoad plus a sceneLoaded hook buys: the package is instantiated and
// its references bound in the same tick the scene comes up.
//
// A scene that already has a track authored in place (WatkinsGlen) is left exactly as it was — this only
// fires when the scene has no TrackBuilder of its own.
public static class TrackSceneLoader
{
    static bool _hooked;

    // The scene this loader serves. A menu scene has no cars and no road and must be left alone; the check
    // is "does this scene run a race", answered by the presence of the shared race systems.
    static bool IsRaceScene() => Object.FindFirstObjectByType<GridSpawner>() != null
                              || Object.FindFirstObjectByType<PitLaneStart>() != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (_hooked) return;
        SceneManager.sceneLoaded += OnSceneLoaded;
        _hooked = true;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive) return;
        EnsureTrack();
    }

    // Instantiate the selected track's package if the scene hasn't got a road of its own. Safe to call more
    // than once; returns the package in play (null in a scene that isn't a race).
    public static TrackPackage EnsureTrack()
    {
        if (!IsRaceScene()) return null;

        // Authored in place (the reference scene): register it and bind anything it left null.
        //
        // A track already in the scene ALWAYS wins over the selection, which is right for WatkinsGlen.unity
        // (authored in place, and the scene is the track) and silently wrong for anything else. It is very
        // easy to leave a package preview in the shared race scene and save it — Preview Selected Package
        // In Scene puts one there deliberately — and the result is a scene pinned to one track that ignores
        // every selection with no error anywhere. That cost a debugging session: RaceScene.unity had a
        // whole baked WatkinsGlen committed into it, 1.4 MB of it, and every race loaded Watkins Glen no
        // matter what the Track Builder window said.
        //
        // So say so. The scene still wins — it may be deliberate — but a mismatch is now one console line
        // instead of a mystery.
        var existing = Object.FindFirstObjectByType<TrackPackage>();
        if (existing != null)
        {
            string wanted = TrackSelection.CurrentId;
            if (!string.IsNullOrEmpty(wanted) && !string.IsNullOrEmpty(existing.trackId) &&
                !string.Equals(existing.trackId, wanted, System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning(
                    $"TrackSceneLoader: '{SceneManager.GetActiveScene().name}' already contains the " +
                    $"{TrackCatalog.DisplayName(existing.trackId)} track, so it is racing that and IGNORING " +
                    $"the selected {TrackCatalog.DisplayName(wanted)}. If that track was left there by " +
                    "Preview Selected Package In Scene, clear it with Draftmaster > Tracks > Clear Package " +
                    "Previews From Scene and save the scene.");
            }
            existing.BindSceneReferences();
            return existing;
        }

        var sceneBuilder = Object.FindFirstObjectByType<TrackBuilder>();
        if (sceneBuilder != null)
        {
            // A hand-built scene with no package component — adopt it so the rest of the game can still ask
            // TrackPackage.ActiveTrack rather than hunting for a TrackBuilder.
            var adopted = sceneBuilder.gameObject.AddComponent<TrackPackage>();
            adopted.trackBuilder = sceneBuilder;
            adopted.trackId = sceneBuilder.track != null
                ? sceneBuilder.track.name
                : SceneManager.GetActiveScene().name;
            adopted.BindSceneReferences();
            return adopted;
        }

        string id = TrackSelection.CurrentId;
        var prefab = TrackCatalog.Package(id);
        if (prefab == null)
        {
            Debug.LogError($"TrackSceneLoader: no track package at Resources/{TrackCatalog.PackageFolder}/{id} " +
                           "and no track in the scene. Build one with Draftmaster > Tracks > Build Package For Track.");
            return null;
        }

        var go = Object.Instantiate(prefab);
        go.name = $"Track_{id}";
        var package = go.GetComponent<TrackPackage>();
        if (package == null) package = go.AddComponent<TrackPackage>();
        if (string.IsNullOrEmpty(package.trackId)) package.trackId = id;

        int bound = package.BindSceneReferences();
        Debug.Log($"TrackSceneLoader: loaded {TrackCatalog.DisplayName(id)} " +
                  $"({TrackCatalog.TypeOf(id)}, {TrackCatalog.LengthMiles(id):0.###} mi) and bound {bound} references.");
        return package;
    }
}
