#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Applies the project pixel standard (PixelArt.PixelsPerMetre) to the open scene.
//
// The generators (TrackBuilder / TrackEnvironmentBuilder / ExtraTrackSpline / Grandstand) now bake the
// standard density straight into their UVs -- a vertex's UV is its world position in metres multiplied by
// PixelArt.UvScale. That only lands correctly if the material itself is not ALSO scaling the texture, so
// every material feeding a generated mesh must sit at tiling (1,1).
//
// A quad with plain 0..1 UVs (the Ground plane) cannot bake anything into its UVs, so its material carries
// the density instead: tiling = worldSize * PixelsPerMetre / textureSize. Both cases are handled here.
public static class PixelScaleApply
{
    [MenuItem("Draftmaster/Art/Apply Pixel Standard to Open Scene", priority = 101)]
    public static void Run()
    {
        // Rebuild first so every generated mesh carries the new UVs before materials are judged.
        RebuildGenerators();

        var bakedUvMaterials = new HashSet<Material>();          // meshes with density baked into UVs
        var quadMaterials = new Dictionary<Material, Vector2>(); // 0..1 UV quads -> tiling the material needs
        var conflicts = new List<string>();

        foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var mesh = mf.sharedMesh;
            var uv = mesh.uv;
            if (uv == null || uv.Length == 0) continue;

            float uMax = uv.Max(p => p.x), uMin = uv.Min(p => p.x);
            float vMax = uv.Max(p => p.y), vMin = uv.Min(p => p.y);
            bool isUnitQuad = Mathf.Abs((uMax - uMin) - 1f) < 0.001f && Mathf.Abs((vMax - vMin) - 1f) < 0.001f;

            foreach (var mat in mr.sharedMaterials.Where(m => m != null))
            {
                var tex = PixelArt.MainTextureOf(mat);
                if (tex == null) continue;

                if (isUnitQuad)
                {
                    var b = mesh.bounds.size;
                    var s = mr.transform.lossyScale;
                    Vector2 world = new Vector2(Mathf.Abs(b.x * s.x), Mathf.Abs(b.y * s.y));
                    Vector2 want = new Vector2(world.x * PixelArt.PixelsPerMetre / tex.width,
                                               world.y * PixelArt.PixelsPerMetre / tex.height);
                    if (quadMaterials.TryGetValue(mat, out var prev) && (prev - want).sqrMagnitude > 0.0001f)
                        conflicts.Add($"{mat.name}: two differently-sized quads want tiling {prev} and {want} " +
                                      $"(seen at '{mr.name}') — give one of them its own material.");
                    else quadMaterials[mat] = want;
                }
                else bakedUvMaterials.Add(mat);
            }
        }

        foreach (var mat in bakedUvMaterials.Where(quadMaterials.ContainsKey))
            conflicts.Add($"{mat.name}: used by BOTH a generated mesh (needs tiling 1,1) and a plain quad " +
                          $"(needs its own tiling) — duplicate the material for one of them.");

        int changed = 0;
        foreach (var mat in bakedUvMaterials)
        {
            if (quadMaterials.ContainsKey(mat)) continue; // conflicted, reported above, left alone
            if (SetTiling(mat, Vector2.one)) changed++;
        }
        foreach (var kv in quadMaterials)
        {
            if (bakedUvMaterials.Contains(kv.Key)) continue;
            if (SetTiling(kv.Key, kv.Value)) changed++;
        }

        AssetDatabase.SaveAssets();
        foreach (var c in conflicts) Debug.LogWarning($"[PixelScaleApply] {c}");
        Debug.Log($"[PixelScaleApply] {changed} material(s) retiled to the {PixelArt.PixelsPerMetre} px/m standard. " +
                  $"{bakedUvMaterials.Count} baked-UV, {quadMaterials.Count} quad, {conflicts.Count} conflict(s).");
    }

    static bool SetTiling(Material mat, Vector2 tiling)
    {
        Vector2 current = mat.HasProperty("_BaseMap") ? mat.GetTextureScale("_BaseMap") : mat.mainTextureScale;
        if ((current - tiling).sqrMagnitude < 0.000001f) return false;

        Undo.RecordObject(mat, "Apply pixel standard");
        if (mat.HasProperty("_BaseMap")) mat.SetTextureScale("_BaseMap", tiling);
        if (mat.HasProperty("_MainTex")) mat.SetTextureScale("_MainTex", tiling);
        EditorUtility.SetDirty(mat);
        Debug.Log($"[PixelScaleApply] {mat.name}: tiling {current} -> {tiling}", mat);
        return true;
    }

    [MenuItem("Draftmaster/Art/Rebuild Track Generators", priority = 102)]
    public static void RebuildGenerators()
    {
        foreach (var tb in Object.FindObjectsByType<TrackBuilder>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            tb.Build();
        foreach (var eb in Object.FindObjectsByType<TrackEnvironmentBuilder>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            eb.Build();
        foreach (var es in Object.FindObjectsByType<ExtraTrackSpline>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            es.Build();
        foreach (var gs in Object.FindObjectsByType<Grandstand>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            gs.Build();
    }
}
#endif
