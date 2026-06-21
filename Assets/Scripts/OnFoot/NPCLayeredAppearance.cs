using System.Collections.Generic;
using UnityEngine;

// Builds a paper-doll NPC from an NPCPartLibrary: one child SpriteRenderer per outfit layer (base body,
// bottoms, shoes, top, hair, hat...), each sliced from a randomly chosen sprite sheet. Every layer shares a
// single frame index (SetFrame) so they animate in lock-step. Layers are children of this transform, so they
// move and rotate with the NPC and stay perfectly aligned.
public class NPCLayeredAppearance : MonoBehaviour
{
    [Tooltip("Part library to build the outfit from.")]
    public NPCPartLibrary library;
    [Tooltip("Sorting layer for all built layers.")]
    public string sortingLayerName = "Vehicles";
    [Tooltip("Sorting order of the back-most layer; each subsequent layer is +1.")]
    public int baseSortingOrder = 0;
    [Tooltip("Material applied to each layer (e.g. an unlit sprite material for the 3D URP renderer). Optional.")]
    public Material layerMaterial;

    readonly List<SpriteRenderer> _renderers = new();
    readonly List<Sprite[]> _frames = new();
    int _frameCount;

    public int FrameCount => _frameCount;
    public bool Built => _renderers.Count > 0;

    // Picks a random outfit and instantiates the layer renderers. Returns false if nothing could be built
    // (no library / no options yet) so the caller can fall back to the prefab's own sprite.
    public bool Build(int? seed = null)
    {
        Clear();
        if (library == null || library.categories == null || library.categories.Length == 0) return false;

        var rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        int order = baseSortingOrder;

        foreach (var cat in library.categories)
        {
            if (cat.options == null || cat.options.Length == 0) continue;
            if (cat.optional && rng.NextDouble() > cat.presentChance) continue;

            var sheet = cat.options[rng.Next(cat.options.Length)];
            if (sheet == null) continue;

            var frames = Slice(sheet);
            if (frames.Length == 0) continue;

            var go = new GameObject(string.IsNullOrEmpty(cat.name) ? "Layer" : cat.name);
            var t = go.transform;
            t.SetParent(transform, false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = order++;
            if (layerMaterial != null) sr.sharedMaterial = layerMaterial;
            sr.color = PickTint(cat, rng);
            sr.sprite = frames[0];

            _renderers.Add(sr);
            _frames.Add(frames);
            _frameCount = Mathf.Max(_frameCount, frames.Length);
        }
        return _renderers.Count > 0;
    }

    // Set every layer to frame i (each layer wraps within its own frame count, so static parts hold frame 0).
    public void SetFrame(int i)
    {
        for (int l = 0; l < _renderers.Count; l++)
        {
            var f = _frames[l];
            if (f.Length == 0) continue;
            _renderers[l].sprite = f[((i % f.Length) + f.Length) % f.Length];
        }
    }

    static Color PickTint(NPCPartLibrary.PartCategory cat, System.Random rng)
    {
        if (cat.tintOptions != null && cat.tintOptions.Length > 0)
            return cat.tintOptions[rng.Next(cat.tintOptions.Length)];
        if (cat.randomHue)
            return Color.HSVToRGB((float)rng.NextDouble(),
                                  0.5f + (float)rng.NextDouble() * 0.4f,
                                  0.6f + (float)rng.NextDouble() * 0.35f);
        return Color.white; // no tint
    }

    Sprite[] Slice(Texture2D sheet)
    {
        int fw = Mathf.Max(1, library.frameWidth);
        int fh = Mathf.Max(1, library.frameHeight);
        int count = Mathf.Max(1, sheet.width / fw);
        var arr = new Sprite[count];
        for (int i = 0; i < count; i++)
            arr[i] = Sprite.Create(sheet, new Rect(i * fw, 0, fw, fh), library.pivot,
                                   library.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        return arr;
    }

    void Clear()
    {
        foreach (var r in _renderers) if (r != null) Destroy(r.gameObject);
        _renderers.Clear();
        _frames.Clear();
        _frameCount = 0;
    }
}
