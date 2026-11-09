using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Writes each track package's layout as plain JSON to Tools/TrackExports/<TrackId>.json, so paddock placement
// can be worked out without the Unity Editor — by hand, or by overnight agents writing paddock sidecars
// (Resources/Paddocks/<id>.json, see PaddockSidecar) and checking them with Tools/paddock_sidecar.py.
//
// Everything is in PACKAGE-LOCAL metres, the same frame a sidecar is written in: the centreline and pit lane
// (with their widths), the start line, and the footprint of every object already in the package — scenery,
// markers and lot areas — so a new placement can keep clear of them. Read-only on the packages: they are
// opened with LoadPrefabContents and unloaded without saving.
//
// Re-run it after a track's layout or scenery changes; the files are committed so worktree agents see them.
public static class TrackGeometryExport
{
    const float StepMetres = 5f;
    const int MaxObjectDepth = 3;

    static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "TrackExports"));

    [MenuItem("Draftmaster/Tracks/Export Geometry For Paddock Layout (All Packages)", priority = 440)]
    static void ExportAll()
    {
        int done = 0, skipped = 0;
        var ids = new List<string>();
        foreach (var row in TrackCatalog.All) ids.Add(row.Name);
        try
        {
            for (int i = 0; i < ids.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Export track geometry", ids[i], i / (float)ids.Count);
                if (Export(ids[i], out string why)) done++;
                else { skipped++; Debug.LogWarning($"TrackGeometryExport: {ids[i]} skipped — {why}"); }
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        Debug.Log($"TrackGeometryExport: wrote {done} track(s) to {OutDir} ({skipped} skipped).");
    }

    [MenuItem("Draftmaster/Tracks/Export Geometry For Paddock Layout (Selected Track)", priority = 441)]
    static void ExportSelected()
    {
        string id = TrackSelection.CurrentId;
        if (Export(id, out string why)) Debug.Log($"TrackGeometryExport: wrote {id} to {OutDir}.");
        else Debug.LogWarning($"TrackGeometryExport: {id} skipped — {why}");
    }

    public static bool Export(string trackId, out string why)
    {
        why = null;
        var prefab = TrackCatalog.Package(trackId);
        if (prefab == null) { why = "no package"; return false; }
        string path = AssetDatabase.GetAssetPath(prefab);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var package = root.GetComponent<TrackPackage>();
            var builder = package != null ? package.Builder : root.GetComponentInChildren<TrackBuilder>(true);
            if (builder == null) { why = "no TrackBuilder in the package"; return false; }
            if (builder.track == null) builder.track = TrackCatalog.Geometry(trackId);
            if (builder.track == null) { why = "no TrackInfoV2 geometry"; return false; }

            var frame = root.transform;
            var sb = new StringBuilder();
            sb.Append("{\n");
            Str(sb, "trackId", trackId); sb.Append(",\n");
            Str(sb, "displayName", TrackCatalog.DisplayName(trackId)); sb.Append(",\n");
            Str(sb, "type", TrackCatalog.TypeOf(trackId).ToString()); sb.Append(",\n");
            sb.Append("  \"lengthMiles\": ").Append(F(TrackCatalog.LengthMiles(trackId))).Append(",\n");
            Str(sb, "frame", "package-local metres; x right, y up; rotation degrees anticlockwise about z"); sb.Append(",\n");
            Str(sb, "exportedFrom", path); sb.Append(",\n");

            var centre = builder.SampleCenterline();
            var start = Local(frame, builder.transform, centre.Count > 0 ? centre[0].position : Vector2.zero);
            sb.Append("  \"startLine\": ").Append(Pt(start)).Append(",\n");
            sb.Append("  \"closedLoop\": ").Append(builder.track.closedLoop ? "true" : "false").Append(",\n");
            sb.Append("  \"centerline\": ").Append(Line(frame, builder.transform, centre)).Append(",\n");
            var pit = builder.track.hasPitLane ? builder.SamplePitCenterline() : new List<TrackBuilder.Sample>();
            sb.Append("  \"pitLane\": ").Append(Line(frame, builder.transform, pit)).Append(",\n");
            sb.Append("  \"objects\": [\n");
            var objects = new List<string>();
            Walk(frame, builder.transform, frame, 0, objects);
            sb.Append(string.Join(",\n", objects));
            sb.Append("\n  ]\n}\n");

            Directory.CreateDirectory(OutDir);
            File.WriteAllText(Path.Combine(OutDir, trackId + ".json"), sb.ToString(), new UTF8Encoding(false));
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Each object's footprint: a marker or lot area as its rotated box, anything else as the package-local
    // bounding box of its renderers and colliders. Descends a few levels so a "Scenery" group is listed by
    // its pieces, but never into the road builder's own generated children.
    static void Walk(Transform frame, Transform builder, Transform t, int depth, List<string> outList)
    {
        foreach (Transform child in t)
        {
            if (child == builder) continue;
            string kind = WeekendMarker.IsMarkerName(child.name) ? "marker"
                        : child.GetComponent<PaddockLotArea>() != null ? "lot"
                        : null;
            if (kind != null)
            {
                var box = child.GetComponent<BoxCollider2D>();
                Vector2 size = box != null ? Vector2.Scale(box.size, (Vector2)child.lossyScale) : Vector2.one;
                Vector3 c = frame.InverseTransformPoint(box != null ? child.TransformPoint(box.offset) : child.position);
                float rot = (Quaternion.Inverse(frame.rotation) * child.rotation).eulerAngles.z;
                string extra = kind == "lot" ? $", \"lotKind\": \"{child.GetComponent<PaddockLotArea>().kind}\"" : "";
                outList.Add($"    {{\"name\": {Q(child.name)}, \"path\": {Q(PathOf(frame, child))}, \"kind\": \"{kind}\"{extra}, " +
                            $"\"x\": {F(c.x)}, \"y\": {F(c.y)}, \"width\": {F(Mathf.Abs(size.x))}, \"height\": {F(Mathf.Abs(size.y))}, \"rotation\": {F(rot)}}}");
                continue;
            }

            bool leaf = depth + 1 >= MaxObjectDepth || child.childCount == 0 || child.childCount > 40;
            if (!leaf)
            {
                Walk(frame, builder, child, depth + 1, outList);
                continue;
            }
            if (!TryBounds(frame, child, out var min, out var max)) continue;
            outList.Add($"    {{\"name\": {Q(child.name)}, \"path\": {Q(PathOf(frame, child))}, \"kind\": \"object\", " +
                        $"\"x\": {F((min.x + max.x) * 0.5f)}, \"y\": {F((min.y + max.y) * 0.5f)}, " +
                        $"\"width\": {F(max.x - min.x)}, \"height\": {F(max.y - min.y)}, \"rotation\": 0}}");
        }
    }

    static bool TryBounds(Transform frame, Transform t, out Vector2 min, out Vector2 max)
    {
        Vector2 lo = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 hi = new Vector2(float.MinValue, float.MinValue);
        bool any = false;
        void Add(Bounds b)
        {
            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector3(i % 2 == 0 ? b.min.x : b.max.x, i < 2 ? b.min.y : b.max.y, b.center.z);
                Vector2 p = frame.InverseTransformPoint(corner);
                lo = Vector2.Min(lo, p);
                hi = Vector2.Max(hi, p);
            }
            any = true;
        }
        foreach (var r in t.GetComponentsInChildren<Renderer>(true)) Add(r.bounds);
        foreach (var c in t.GetComponentsInChildren<Collider2D>(true))
        {
            // LoadPrefabContents runs no physics, so a 2D collider's bounds are empty; use its shape instead.
            if (c is BoxCollider2D box)
            {
                var b = new Bounds(c.transform.TransformPoint(box.offset), Vector3.Scale(box.size, c.transform.lossyScale));
                Add(b);
            }
        }
        min = lo;
        max = hi;
        return any && max.x - min.x < 5000f && max.y - min.y < 5000f;
    }

    static string Line(Transform frame, Transform builder, List<TrackBuilder.Sample> samples)
    {
        if (samples == null || samples.Count == 0) return "[]";
        var sb = new StringBuilder("[");
        float next = 0f;
        for (int i = 0; i < samples.Count; i++)
        {
            bool last = i == samples.Count - 1;
            if (samples[i].distance < next && !last) continue;
            next = samples[i].distance + StepMetres;
            var p = Local(frame, builder, samples[i].position);
            if (sb.Length > 1) sb.Append(", ");
            sb.Append('[').Append(F(p.x)).Append(", ").Append(F(p.y)).Append(", ").Append(F(samples[i].width)).Append(']');
        }
        return sb.Append(']').ToString();
    }

    static Vector2 Local(Transform frame, Transform builder, Vector2 builderLocal) =>
        frame.InverseTransformPoint(builder.TransformPoint(new Vector3(builderLocal.x, builderLocal.y, 0f)));

    static string PathOf(Transform frame, Transform t)
    {
        var parts = new List<string>();
        for (var x = t; x != null && x != frame; x = x.parent) parts.Insert(0, x.name);
        return string.Join("/", parts);
    }

    static string Pt(Vector2 p) => $"[{F(p.x)}, {F(p.y)}]";
    static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    static void Str(StringBuilder sb, string key, string value) => sb.Append("  ").Append(Q(key)).Append(": ").Append(Q(value));
    static string Q(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
