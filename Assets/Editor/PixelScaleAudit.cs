#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Reports the effective pixel density (texture pixels per world metre) of every renderer in the
// open scenes, measured against the project standard in PixelArt.PixelsPerMetre.
//
// The car is the point of truth: a 64x32 livery imported at 12.8 px/unit is a 5.0m x 2.5m car,
// i.e. 12.8 texture pixels per metre. Any surface that resolves to a different number renders its
// pixels at a different size than the car's, which is what makes the scene look like a collage.
//
// Read-only. Writes Docs/PixelScaleAudit.md and prints a summary to the console.
public static class PixelScaleAudit
{
    const string kReportPath = "Docs/PixelScaleAudit.md";

    // Ratio outside this band counts as a mismatch worth reporting.
    const float kTolerance = 0.02f;      // +/-2% is "on standard"
    // Anisotropy (u vs v density) above this counts as stretched-in-one-axis.
    const float kStretchTolerance = 0.05f;

    class Row
    {
        public string path;
        public string material;
        public string texture;
        public int texW, texH;
        public float worldU, worldV;     // metres spanned by the mesh in each UV direction
        public float uvSpanU, uvSpanV;   // UV range covered by the mesh
        public Vector2 tiling;
        // Lowest and highest texel density found anywhere on the mesh, in texture pixels per metre.
        public float pxPerMetreU, pxPerMetreV;
        public string note;

        public float RatioU => pxPerMetreU / PixelArt.PixelsPerMetre;
        public float RatioV => pxPerMetreV / PixelArt.PixelsPerMetre;

        // The baseline (lowest) density is what the surface is authored at; anything above it on the same
        // mesh comes from geometry. A ribbon bent through a corner carries radial stripes, so the inner
        // rail is denser than the outer -- correct, and reported as curve spread rather than a defect.
        public bool OffStandard => Mathf.Abs(RatioU - 1f) > kTolerance;
        public bool Stretched => !OffStandard && pxPerMetreU > 0f &&
                                 (pxPerMetreV / pxPerMetreU - 1f) > kStretchTolerance;
    }

    [MenuItem("Draftmaster/Art/Audit Pixel Scale", priority = 100)]
    public static void Run()
    {
        var rows = new List<Row>();

        foreach (var mr in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (Excluded(mr.transform)) continue;
            rows.AddRange(MeasureMesh(mr));
        }

        foreach (var sr in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (Excluded(sr.transform)) continue;
            var r = MeasureSprite(sr);
            if (r != null) rows.Add(r);
        }

        rows = rows.OrderBy(r => r.OffStandard ? 0 : 1).ThenBy(r => r.path).ToList();
        WriteReport(rows);

        int bad = rows.Count(r => r.OffStandard);
        int stretched = rows.Count(r => r.Stretched);
        Debug.Log($"[PixelScaleAudit] {rows.Count} renderers measured. " +
                  $"{bad} off the {PixelArt.PixelsPerMetre} px/m standard, {stretched} stretched in one axis. " +
                  $"Report: {kReportPath}");
    }

    static IEnumerable<Row> MeasureMesh(MeshRenderer mr)
    {
        var mf = mr.GetComponent<MeshFilter>();
        var mesh = mf != null ? mf.sharedMesh : null;
        var mats = mr.sharedMaterials;

        if (mats == null || mats.Length == 0 || mats.All(m => m == null))
        {
            yield return new Row
            {
                path = Path(mr.transform),
                material = "(none)",
                note = "no material assigned (built at runtime?) — not measurable at edit time"
            };
            yield break;
        }

        // Measure per submesh, not per distinct material: a multi-submesh mesh (an ExtraTrackSpline road
        // plus its edge lines) mixes UV conventions in one vertex array, so measuring every triangle
        // against one material's texture size reads wild densities that aren't really there.
        for (int sub = 0; sub < mats.Length; sub++)
        {
            var mat = mats[sub];
            if (mat == null) continue;
            var row = new Row { path = Path(mr.transform), material = mat.name };
            var tex = MainTexture(mat);
            if (tex == null) { row.note = "material has no texture (flat colour)"; yield return row; continue; }

            row.texture = tex.name;
            row.texW = tex.width;
            row.texH = tex.height;
            row.tiling = mat.HasProperty("_BaseMap") ? mat.GetTextureScale("_BaseMap") : mat.mainTextureScale;

            if (mesh == null) { row.note = "no mesh (runtime-built)"; yield return row; continue; }
            if (sub >= mesh.subMeshCount) { row.note = "material slot has no submesh"; yield return row; continue; }

            if (!MeasureDensity(mesh, sub, mr.transform, row.texW, row.texH, row.tiling,
                                out row.pxPerMetreU, out row.pxPerMetreV, out row.worldU, out row.worldV))
            {
                row.note = "mesh has no UVs";
                yield return row; continue;
            }
            yield return row;
        }
    }

    static Row MeasureSprite(SpriteRenderer sr)
    {
        if (sr.sprite == null) return null;
        var s = sr.sprite;
        var scale = sr.transform.lossyScale;
        float worldW = (s.rect.width / s.pixelsPerUnit) * Mathf.Abs(scale.x);
        float worldH = (s.rect.height / s.pixelsPerUnit) * Mathf.Abs(scale.y);

        return new Row
        {
            path = Path(sr.transform),
            material = "(SpriteRenderer)",
            texture = s.name,
            texW = (int)s.rect.width,
            texH = (int)s.rect.height,
            tiling = Vector2.one,
            worldU = worldW,
            worldV = worldH,
            uvSpanU = 1f,
            uvSpanV = 1f,
            pxPerMetreU = worldW > 0.0001f ? s.rect.width / worldW : 0f,
            pxPerMetreV = worldH > 0.0001f ? s.rect.height / worldH : 0f,
            note = Mathf.Approximately(scale.x, scale.y) ? "" : "non-uniform transform scale"
        };
    }

    // Texel density along every triangle edge, in texture pixels per world metre.
    //
    // For an edge with UV delta (du,dv) the texels crossed are (du*texW*tilingX, dv*texH*tilingY), so
    // |texels| / |world metres| is the density that edge renders at. This is invariant to the object's
    // rotation and to which UV convention the mesh uses (ribbon UVs that follow the strip, or
    // world-anchored UVs where both axes move together on a diagonal edge) -- the earlier
    // "classify each edge as a u-edge or a v-edge" approach mismeasured rotated world-anchored meshes,
    // because on those meshes no edge is purely one axis.
    //
    // Reports the low and high density found, so a surface that is stretched in one axis shows up as a
    // spread rather than being averaged away.
    static bool MeasureDensity(Mesh mesh, int submesh, Transform t, int texW, int texH, Vector2 tiling,
                               out float densityLow, out float densityHigh,
                               out float worldSpanX, out float worldSpanY)
    {
        densityLow = densityHigh = worldSpanX = worldSpanY = 0f;
        var uv = mesh.uv;
        var v = mesh.vertices;
        if (uv == null || uv.Length == 0 || uv.Length != v.Length) return false;

        var b = mesh.bounds.size;
        var ls = t.lossyScale;
        worldSpanX = Mathf.Abs(b.x * ls.x);
        worldSpanY = Mathf.Abs(b.y * ls.y);

        var tris = mesh.GetTriangles(submesh);

        // The two UV conventions in use need different measurements, so both are computed and the one
        // that fits the mesh is reported:
        //
        //  - "world"  : UVs are the world position scaled, used for isotropic surfaces (asphalt, grass).
        //               Every edge, including diagonals, carries the full density, so |texel delta| over
        //               world length is exact for all of them.
        //  - "ribbon" : UVs follow the strip (across, along), used for directional textures (kerbs,
        //               painted lines). Only axis-aligned edges are meaningful -- a diagonal mixes both
        //               axes, and on a bend the cross-section edges fan out because kerb stripes are
        //               radial by design, so including them would report correct geometry as a defect.
        //
        // Whichever convention produces the tighter spread is the one the mesh actually uses.
        float worldLo = float.MaxValue, worldHi = 0f; int worldN = 0;
        float ribbonLo = float.MaxValue, ribbonHi = 0f; int ribbonN = 0;

        for (int i = 0; i + 2 < tris.Length; i += 3)
        {
            for (int e = 0; e < 3; e++)
            {
                int a = tris[i + e], c = tris[i + (e + 1) % 3];
                Vector2 duv = uv[c] - uv[a];
                float world = Vector3.Distance(t.TransformPoint(v[a]), t.TransformPoint(v[c]));
                if (world < 1e-4f) continue;

                float du = Mathf.Abs(duv.x * texW * tiling.x);
                float dv = Mathf.Abs(duv.y * texH * tiling.y);

                float combined = new Vector2(du, dv).magnitude;
                if (combined > 1e-4f)
                {
                    float d = combined / world;
                    worldLo = Mathf.Min(worldLo, d); worldHi = Mathf.Max(worldHi, d); worldN++;
                }

                float axis = du > dv * 8f ? du : (dv > du * 8f ? dv : 0f);
                if (axis > 1e-4f)
                {
                    float d = axis / world;
                    ribbonLo = Mathf.Min(ribbonLo, d); ribbonHi = Mathf.Max(ribbonHi, d); ribbonN++;
                }
            }
        }

        if (worldN == 0 && ribbonN == 0) return false;

        float worldSpread = worldN > 0 ? worldHi / Mathf.Max(0.0001f, worldLo) : float.MaxValue;
        float ribbonSpread = ribbonN > 0 ? ribbonHi / Mathf.Max(0.0001f, ribbonLo) : float.MaxValue;

        if (ribbonN > 0 && ribbonSpread <= worldSpread) { densityLow = ribbonLo; densityHigh = ribbonHi; }
        else { densityLow = worldLo; densityHigh = worldHi; }
        return true;
    }

    // Authoring aids and text meshes aren't world art and would only add noise to the report.
    // TrackReferenceImage is a photographic aerial the track is traced over; TextMeshPro builds its own
    // geometry from an SDF atlas, where "texture pixels per metre" is meaningless.
    static bool Excluded(Transform t)
    {
        if (t.GetComponent<TrackReferenceImage>() != null) return true;
        if (t.GetComponent<TMPro.TMP_Text>() != null) return true;
        return false;
    }

    static Texture MainTexture(Material m)
    {
        if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) return m.GetTexture("_BaseMap");
        if (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null) return m.GetTexture("_MainTex");
        return null;
    }

    static string Path(Transform t)
    {
        var sb = new StringBuilder(t.name);
        while (t.parent != null) { t = t.parent; sb.Insert(0, t.name + "/"); }
        return sb.ToString();
    }

    static void WriteReport(List<Row> rows)
    {
        var ci = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("# Pixel scale audit");
        sb.AppendLine();
        sb.AppendLine($"Generated {DateTime.Now:yyyy-MM-dd HH:mm} from scene(s): " +
                      string.Join(", ", Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
                          .Select(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).name)));
        sb.AppendLine();
        sb.AppendLine($"Standard: **{PixelArt.PixelsPerMetre.ToString(ci)} texture pixels per world metre** " +
                      $"(1 px = {(1f / PixelArt.PixelsPerMetre).ToString("0.####", ci)} m), " +
                      "taken from the 64x32 car livery imported at 12.8 px/unit = a 5.0m x 2.5m car.");
        sb.AppendLine();
        sb.AppendLine("`ratio` is measured density / standard. 1.00 = correct. 4.00 = pixels are drawn 4x too small (texture is 4x too dense). 0.25 = pixels 4x too chunky.");
        sb.AppendLine();
        sb.AppendLine("`px/m low`/`px/m high` are the lowest and highest texel density measured across the mesh's edges. " +
                      "A gap between them is a stretch; both should read 12.8.");
        sb.AppendLine();
        sb.AppendLine("| object | material | texture | tex px | tiling | world size (m) | px/m low | px/m high | ratio low | ratio high | flags |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");

        foreach (var r in rows)
        {
            string flags = "";
            if (!string.IsNullOrEmpty(r.note)) flags = r.note;
            else
            {
                if (r.OffStandard) flags += "OFF-STANDARD ";
                if (r.Stretched) flags += "curve spread ";
                if (flags == "") flags = "ok";
            }
            sb.AppendLine($"| {r.path} | {r.material} | {r.texture} | {r.texW}x{r.texH} | " +
                          $"{r.tiling.x.ToString("0.###", ci)},{r.tiling.y.ToString("0.###", ci)} | " +
                          $"{r.worldU.ToString("0.##", ci)} x {r.worldV.ToString("0.##", ci)} | " +
                          $"{r.pxPerMetreU.ToString("0.##", ci)} | {r.pxPerMetreV.ToString("0.##", ci)} | " +
                          $"{r.RatioU.ToString("0.##", ci)} | {r.RatioV.ToString("0.##", ci)} | {flags.Trim()} |");
        }

        Directory.CreateDirectory("Docs");
        File.WriteAllText(kReportPath, sb.ToString());
        AssetDatabase.Refresh();
    }
}
#endif
