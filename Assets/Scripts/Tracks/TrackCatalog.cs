using System.Collections.Generic;
using Draftmaster.Data;
using UnityEngine;

// The one place that answers "what tracks exist, and where is this one's stuff?".
//
// A track is identified by a single string id — "Daytona", "Martinsville" — and that id is already the
// shared key across the whole game: the Tracks table (Track.Name), the calendar (Race.TrackName), the
// travel map's circuit nodes, the geometry asset in Resources/Tracks, and the legacy per-track scenes.
// Nothing here invents a new identifier; it just makes the existing one resolvable in one call.
//
// A track is made of three things, and any of them may be missing while a track is being built:
//
//   1. A CATALOGUE ROW  (Draftmaster.Data.Track)  — name, type, length, banking, default laps.
//      Lives in the SQLite Tracks table, seeded from DummyTracks. Always available: when the database
//      hasn't opened yet, the seed list is used directly, the same fallback RosterLookup does for drivers.
//   2. GEOMETRY         (Resources/Tracks/<id>.asset, a TrackInfoV2) — the spline TrackBuilder builds.
//      Author it by hand or generate a starting point with OvalTrackFactory.
//   3. A CONTENT PACKAGE (Resources/TrackPackages/<id>.prefab) — everything else that is specific to this
//      track: the TrackBuilder object, its environment, ground, grandstands, paddock boundary, spawn
//      markers. TrackSceneLoader drops it into the shared race scene.
//
// Ask HasGeometry/HasPackage before offering a track in a menu: with 35 rounds on the calendar, most of
// them will be catalogue-only for a long time.
public static class TrackCatalog
{
    public const string GeometryFolder = "Tracks";        // Resources/Tracks/<id>.asset
    public const string PackageFolder = "TrackPackages";  // Resources/TrackPackages/<id>.prefab

    // The track the game falls back to when nothing has been selected — the current dev/reference build.
    public const string DefaultTrackId = "WatkinsGlen";

    static List<Track> _rows;

    // Every track in the catalogue, database first so edits in the driver/track database window take
    // effect, seed list second so this works before DatabaseManager has opened (and in EditMode tests).
    public static IReadOnlyList<Track> All
    {
        get
        {
            if (_rows != null) return _rows;

            var dbm = DatabaseManager.Instance;
            if (dbm != null && dbm.IsReady)
            {
                try
                {
                    var rows = new List<Track>(dbm.Connection.Table<Track>());
                    if (rows.Count > 0) { _rows = rows; return _rows; }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"TrackCatalog: falling back to the seed list ({e.Message}).");
                }
            }

            _rows = DummyTracks.Build();
            return _rows;
        }
    }

    // Drop the cache after editing the Tracks table so the next query re-reads it.
    public static void Invalidate() => _rows = null;

    public static Track Row(string trackId)
    {
        if (string.IsNullOrEmpty(trackId)) return null;
        var all = All;
        for (int i = 0; i < all.Count; i++)
            if (string.Equals(all[i].Name, trackId, System.StringComparison.OrdinalIgnoreCase)) return all[i];
        return null;
    }

    public static IEnumerable<Track> OfType(TrackType type)
    {
        var all = All;
        for (int i = 0; i < all.Count; i++)
            if (all[i].Type == type) yield return all[i];
    }

    // "Daytona" -> "Daytona International Speedway", falling back to a nicified id for a track that is
    // in the world (a travel-map circuit, say) but not yet in the catalogue.
    public static string DisplayName(string trackId)
    {
        var row = Row(trackId);
        if (row != null && !string.IsNullOrEmpty(row.DisplayName)) return row.DisplayName;
        return Nicify(trackId);
    }

    public static TrackType TypeOf(string trackId)
    {
        var row = Row(trackId);
        return row != null ? row.Type : TrackType.Speedway;
    }

    public static int DefaultLaps(string trackId)
    {
        var row = Row(trackId);
        return row != null && row.DefaultLaps > 0 ? row.DefaultLaps : 200;
    }

    public static float LengthMiles(string trackId)
    {
        var row = Row(trackId);
        return row != null && row.LengthMiles > 0f ? row.LengthMiles : 1.5f;
    }

    // ---------------------------------------------------------------- assets

    public static TrackInfoV2 Geometry(string trackId)
        => string.IsNullOrEmpty(trackId) ? null : Resources.Load<TrackInfoV2>($"{GeometryFolder}/{trackId}");

    public static GameObject Package(string trackId)
        => string.IsNullOrEmpty(trackId) ? null : Resources.Load<GameObject>($"{PackageFolder}/{trackId}");

    public static bool HasGeometry(string trackId) => Geometry(trackId) != null;
    public static bool HasPackage(string trackId) => Package(trackId) != null;

    // Tracks that can actually be raced right now: catalogued AND built. This is what a track-select
    // screen should list.
    public static IEnumerable<Track> Playable()
    {
        var all = All;
        for (int i = 0; i < all.Count; i++)
            if (HasGeometry(all[i].Name)) yield return all[i];
    }

    // "WatkinsGlen" -> "Watkins Glen". Same rule SpawnIntroUI uses for scene titles.
    public static string Nicify(string id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        var sb = new System.Text.StringBuilder(id.Length + 4);
        for (int i = 0; i < id.Length; i++)
        {
            char c = id[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(id[i - 1])) sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }
}
