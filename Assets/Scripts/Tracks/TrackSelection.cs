using Draftmaster.Data;
using UnityEngine;

// Which track the race scene should build. Set before the scene loads (menu, calendar, travel map), read
// by TrackSceneLoader once it's up.
//
// Persisted in PlayerPrefs rather than kept in a static alone, because the weekend deliberately reloads the
// scene several times — PracticeDirector reloads between practice, qualifying and the race, and
// RaceDirector's NEXT WEEKEND reloads again — and a plain static would survive those but not a domain
// reload or a quit mid-weekend.
public static class TrackSelection
{
    const string Key = "track.current";

    // The track id the next/current race scene uses. Falls back to the travel map's current location when
    // nothing has been chosen, so "drive to Martinsville, then race" works without the menu setting it,
    // and to the reference track when even that is unknown.
    public static string CurrentId
    {
        get
        {
            string saved = PlayerPrefs.GetString(Key, "");
            if (!string.IsNullOrEmpty(saved)) return saved;

            string travel = TravelState.CurrentNodeId;
            if (!string.IsNullOrEmpty(travel) && TrackCatalog.Row(travel) != null) return travel;

            return TrackCatalog.DefaultTrackId;
        }
        set => Select(value);
    }

    public static Track Current => TrackCatalog.Row(CurrentId);
    public static TrackType CurrentType => TrackCatalog.TypeOf(CurrentId);
    public static string CurrentDisplayName => TrackCatalog.DisplayName(CurrentId);

    // Choose the track for the next race scene. Rejects an id with no geometry: better to stay on a track
    // that exists than to load a scene with no road in it.
    public static bool Select(string trackId)
    {
        if (string.IsNullOrEmpty(trackId)) return false;
        if (!TrackCatalog.HasGeometry(trackId))
        {
            Debug.LogWarning($"TrackSelection: '{trackId}' has no geometry asset at " +
                             $"Resources/{TrackCatalog.GeometryFolder}/{trackId} — selection unchanged. " +
                             "Generate a starting layout with Draftmaster > Tracks.");
            return false;
        }
        PlayerPrefs.SetString(Key, trackId);
        PlayerPrefs.Save();
        return true;
    }

    // Start a fresh weekend at a track: the selection plus the weekend reset the session flow expects.
    public static bool StartWeekendAt(string trackId)
    {
        if (!Select(trackId)) return false;
        RaceWeekend.ResetWeekend();
        return true;
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }
}
