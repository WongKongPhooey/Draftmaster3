using UnityEngine;

// Everything that belongs to ONE track, in one prefab: the road (TrackBuilder + its TrackInfoV2), the
// environment (barriers, runoff, kerbs), the ground plane, grandstands, the paddock boundary, the player
// spawn markers, the RV — anything a Daytona has that a Martinsville doesn't.
//
// Everything that belongs to EVERY track — the player car, GridSpawner, PitLaneStart, the HUDs, the
// directors, the camera, the database — stays in the shared race scene and never gets duplicated.
//
// That split is the whole point of the structure. With one scene per track, 35 rounds means 35 copies of
// the same twenty manager objects, and every fix to the race flow has to be repeated 35 times (the legacy
// Assets/Levels/Racetracks folder is exactly that problem, frozen). With packages, the race scene is
// authored once and the track is content.
//
// Both layouts work: a scene that already contains a track (WatkinsGlen does, authored in place) is left
// alone, and its TrackBuilder just registers itself here.
[DisallowMultipleComponent]
public class TrackPackage : MonoBehaviour
{
    [Tooltip("Catalogue id this package builds — must match Track.Name / the Resources asset names (e.g. \"Daytona\").")]
    public string trackId;

    [Tooltip("The road. Left null, the first TrackBuilder in the package's children is used.")]
    public TrackBuilder trackBuilder;

    [Tooltip("Optional: the object holding scenery for this track (grandstands, ground, decorations).")]
    public Transform environmentRoot;

    [Tooltip("Where the on-foot paddock sits for this track. Optional — PitLaneStart falls back to the pit lane itself.")]
    public Transform paddockRoot;

    // The package the race scene is currently running.
    public static TrackPackage Active { get; private set; }

    // The road everything else in the scene should bind to (null before a package is loaded).
    public static TrackBuilder ActiveTrack => Active != null ? Active.Builder : null;

    public TrackBuilder Builder
    {
        get
        {
            if (trackBuilder == null) trackBuilder = GetComponentInChildren<TrackBuilder>(true);
            return trackBuilder;
        }
    }

    void Awake()
    {
        if (Active != null && Active != this && Active.gameObject != gameObject)
        {
            Debug.LogWarning($"TrackPackage: '{trackId}' loaded while '{Active.trackId}' is already active. " +
                             "The race scene should contain exactly one track.", this);
        }
        Active = this;
        if (string.IsNullOrEmpty(trackId) && Builder != null && Builder.track != null)
            trackId = Builder.track.name;
    }

    void OnDestroy() { if (Active == this) Active = null; }

    // Point every component in the scene that wants a TrackBuilder at this one.
    //
    // Roughly fifteen components carry a `public TrackBuilder track` field (GridSpawner, PitLaneStart,
    // LapTimingManager, RacePositionTracker, SplineDriver, PitLimiter, the pit and paddock spawners...).
    // In a per-track scene those are dragged in by hand; in the shared scene the road doesn't exist until
    // the package is instantiated, so they are bound here instead. Only null fields are filled, so anything
    // deliberately wired to a different spline (an ExtraTrackSpline test rig) is left alone.
    public int BindSceneReferences()
    {
        var builder = Builder;
        if (builder == null)
        {
            Debug.LogError($"TrackPackage '{trackId}': no TrackBuilder in the package — nothing to bind.", this);
            return 0;
        }

        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic;

        int bound = 0;
        var all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var mb in all)
        {
            if (mb == null) continue;
            var type = mb.GetType();
            // Only touch project code; a package field on a Unity/third-party component is never ours to set.
            if (type.Assembly != typeof(TrackPackage).Assembly) continue;

            foreach (var field in type.GetFields(flags))
            {
                if (field.FieldType != typeof(TrackBuilder)) continue;
                if (field.IsInitOnly || field.IsLiteral) continue;
                if (field.GetValue(mb) as TrackBuilder != null) continue;   // already wired — leave it
                field.SetValue(mb, builder);
                bound++;
            }
        }
        return bound;
    }
}
