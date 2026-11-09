using System.Collections.Generic;
using UnityEngine;

// Paddock placements kept as TEXT beside a track package: Resources/Paddocks/<TrackId>.json.
//
// Track packages are binary prefabs, so placing a venue marker or a motorhome lot used to need the Unity
// Editor and one track at a time. A sidecar holds the same things as plain data — `*_Marker` venues
// (WeekendMarker) and PaddockLotArea footprints — so they can be laid out for many tracks at once, by hand
// or by overnight agents working from the exported track geometry (Tools/TrackExports/<id>.json), and checked
// without Unity (Tools/paddock_sidecar.py validate / preview). Full workflow: Docs/Tracks.md, "Paddock
// sidecars".
//
// WHEN. TrackSceneLoader applies it the moment a package is in the scene, before WeekendVenueSites adopts
// `*_Marker` objects and before the lots lay out — to the game a sidecar object is indistinguishable from one
// authored in the package.
//
// HAND-AUTHORED WINS. A marker whose name the package already has is skipped, and so is a lot area of a kind
// the package already has one of (the lots only ever use the first area per kind). So a sidecar can never
// override work done in the Editor — it only fills gaps.
//
// FRAME. Package-local metres: x/y as the package root sees them, rotation in degrees about z. The geometry
// export is in the same frame, so a point copied from it lands on the same spot.
public static class PaddockSidecar
{
    public const string Folder = "Paddocks";
    public const string RootName = "PaddockSidecar";

    [System.Serializable]
    public class Point
    {
        public float x, y;
    }

    [System.Serializable]
    public class Marker
    {
        public string name;          // "<Venue>_Marker" — see WeekendMarkerNames for the venue words
        public float x, y;
        public float rotation;       // degrees
        public float width = 6f, height = 6f;   // the arrival perimeter (BoxCollider2D)
        public string label;         // optional; WeekendMarker.label
        public Point seat;           // optional: teleport target (a child named "Seat")
        public Point view;           // optional: camera vantage (a child named "View")
    }

    [System.Serializable]
    public class Lot
    {
        public string kind;          // "Motorhomes" | "Garages"
        public float x, y;
        public float rotation;       // degrees; local +X = the way a line of rigs runs
        public float width, height;
        public float gap = 2f;
        public float rowGap = 6f;
    }

    [System.Serializable]
    public class File
    {
        public string trackId;
        public Marker[] markers;
        public Lot[] lots;
    }

    public static File Load(string trackId)
    {
        if (string.IsNullOrEmpty(trackId)) return null;
        var asset = Resources.Load<TextAsset>($"{Folder}/{trackId}");
        if (asset == null) return null;
        try { return JsonUtility.FromJson<File>(asset.text); }
        catch (System.Exception e)
        {
            Debug.LogError($"PaddockSidecar: Resources/{Folder}/{trackId}.json is not valid JSON: {e.Message}");
            return null;
        }
    }

    // Build the sidecar's objects under the package. Safe to call again: a second call finds its own
    // objects by name and adds nothing. Returns how many objects it added.
    public static int Apply(TrackPackage package) =>
        package == null ? 0 : Apply(package, Load(package.trackId));

    public static int Apply(TrackPackage package, File data)
    {
        if (package == null || data == null) return 0;

        Transform root = package.transform;
        var existingNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        var existingKinds = new HashSet<PaddockLotKind>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) existingNames.Add(t.name);
        foreach (var area in root.GetComponentsInChildren<PaddockLotArea>(true)) existingKinds.Add(area.kind);

        Transform holder = root.Find(RootName);
        int added = 0;

        Transform Holder()
        {
            if (holder != null) return holder;
            var go = new GameObject(RootName);
            go.transform.SetParent(root, false);
            holder = go.transform;
            return holder;
        }

        foreach (var m in data.markers ?? System.Array.Empty<Marker>())
        {
            if (m == null || !WeekendMarker.IsMarkerName(m.name)) continue;
            if (existingNames.Contains(m.name)) continue;          // authored in the package: it wins
            if (WeekendMarker.VenueFromName(m.name) == Draftmaster.Weekend.WeekendVenue.None)
            {
                Debug.LogWarning($"PaddockSidecar: {package.trackId}: '{m.name}' names no venue — skipped.");
                continue;
            }
            var go = Place(root, Holder(), m.name, m.x, m.y, m.rotation, m.width, m.height);
            if (m.seat != null) Child(root, go.transform, "Seat", m.seat);
            if (m.view != null) Child(root, go.transform, "View", m.view);
            var marker = go.AddComponent<WeekendMarker>();
            marker.venue = WeekendMarker.VenueFromName(m.name);
            if (!string.IsNullOrEmpty(m.label)) marker.label = m.label;
            marker.teleportTo = go.transform.Find("Seat");
            marker.cameraView = go.transform.Find("View");
            existingNames.Add(m.name);
            added++;
        }

        foreach (var l in data.lots ?? System.Array.Empty<Lot>())
        {
            if (l == null || !System.Enum.TryParse(l.kind, true, out PaddockLotKind kind)) continue;
            if (existingKinds.Contains(kind)) continue;            // one area per kind; the package's wins
            if (l.width <= 0f || l.height <= 0f) continue;
            // The component's Awake/OnEnable runs inside AddComponent, so build it inactive and switch it on
            // once its numbers are in.
            var go = Place(root, Holder(), $"PaddockLotArea_{kind}", l.x, l.y, l.rotation, l.width, l.height);
            go.SetActive(false);
            var area = go.AddComponent<PaddockLotArea>();
            area.kind = kind;
            area.gap = l.gap > 0f ? l.gap : area.gap;
            area.rowGap = l.rowGap > 0f ? l.rowGap : area.rowGap;
            go.SetActive(true);
            existingKinds.Add(kind);
            added++;
        }

        if (added > 0)
            Debug.Log($"PaddockSidecar: {package.trackId}: placed {added} object(s) from Resources/{Folder}/{package.trackId}.json.");
        return added;
    }

    // Positions are package-local (`frame` = the package root) whatever the holder's own transform: the
    // holder is created at identity, but a package may already carry one that somebody moved.
    static GameObject Place(Transform frame, Transform parent, string name, float x, float y, float rotation,
                            float width, float height)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = frame.TransformPoint(new Vector3(x, y, 0f));
        go.transform.rotation = frame.rotation * Quaternion.Euler(0f, 0f, rotation);
        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(Mathf.Max(0.5f, width), Mathf.Max(0.5f, height));
        return go;
    }

    static void Child(Transform frame, Transform parent, string name, Point p)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = frame.TransformPoint(new Vector3(p.x, p.y, 0f));
    }
}
