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

    [Header("Authored outfit")]
    [Tooltip("On: build the outfit from the authored choices below (Stardew-style — pick a style and a colour per layer) instead of randomising. Spawner-built crowd NPCs leave this off.")]
    public bool useAuthoredOutfit = false;
    [Tooltip("One entry per library category (the custom inspector keeps this in sync). A category without an entry falls back to a random pick.")]
    public LayerChoice[] authoredOutfit;

    // A designer's pick for one layer: which sheet and what colour. styleIndex -1 keeps the style
    // random while the tint stays authored.
    [System.Serializable]
    public class LayerChoice
    {
        [Tooltip("Library category this choice applies to (matched by name).")]
        public string category;
        [Tooltip("Untick to leave this layer off the character entirely.")]
        public bool include = true;
        [Tooltip("Index into the category's options. -1 = random style.")]
        public int styleIndex = 0;
        [Tooltip("Colour multiplied onto the layer. White = art as drawn. Tintable parts should be painted in a white/grey ramp.")]
        public Color tint = Color.white;
    }

    readonly List<SpriteRenderer> _renderers = new();
    readonly List<Sprite[]> _frames = new();
    readonly List<string> _categories = new();   // library category each layer came from, same order
    readonly List<LayerChoice> _worn = new();     // every category's outcome from the last Build, rolled or not
    int _frameCount;

    public int FrameCount => _frameCount;
    public bool Built => _renderers.Count > 0;

    // What the last Build actually put on — every category, including the ones left off — as an authored
    // outfit. Built from a seed, a person can't otherwise be copied: hand this to another body
    // (NPCFactory.Dress) and it is the same person, not a lookalike rolled from different dice.
    public LayerChoice[] WornOutfit()
    {
        var copy = new LayerChoice[_worn.Count];
        for (int i = 0; i < _worn.Count; i++)
            copy[i] = new LayerChoice
            {
                category = _worn[i].category, include = _worn[i].include,
                styleIndex = _worn[i].styleIndex, tint = _worn[i].tint,
            };
        return copy;
    }

    // Added in the inspector: fill in the library and, on a PlacedNPC marker, switch straight to authored
    // mode — a wardrobe is by definition a chosen outfit, not a random one.
    void Reset()
    {
        if (library == null) library = Resources.Load<NPCPartLibrary>("NPC/NPCPartLibrary");
        if (GetComponent<PlacedNPC>() != null) useAuthoredOutfit = true;
    }

    // Scene-authored NPCs build themselves. Spawner-built NPCs call Build() right after AddComponent,
    // so Built is already true (or the component is being destroyed) by the time Start runs.
    //
    // One exception: this component sat on a PlacedNPC marker is a WARDROBE, not a character. The marker
    // is an invisible pin — often at the world origin, with the body it describes stood somewhere else
    // entirely — so building a person on it would leave one stood in mid-air at 0,0. PlacedNPC copies the
    // outfit onto the body it spawns and clears the preview; this is the belt and braces for a marker that
    // never gets to build (its appearance conditions failed) leaving its editor preview on screen.
    void Start()
    {
        if (GetComponent<PlacedNPC>() != null) { Clear(); return; }
        if (!Built) Build();
    }

    // Builds the outfit and instantiates the layer renderers. Authored choices are used where present
    // (useAuthoredOutfit), anything else is randomised. Returns false if nothing could be built
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

            LayerChoice choice = useAuthoredOutfit ? FindChoice(cat.name) : null;
            if ((choice != null && !choice.include) ||
                (choice == null && cat.optional && rng.NextDouble() > cat.presentChance))
            {
                _worn.Add(new LayerChoice { category = cat.name, include = false });
                continue;
            }

            int style = (choice != null && choice.styleIndex >= 0)
                ? Mathf.Clamp(choice.styleIndex, 0, cat.options.Length - 1)
                : rng.Next(cat.options.Length);
            Texture2D sheet = cat.options[style];
            if (sheet == null) { _worn.Add(new LayerChoice { category = cat.name, include = false }); continue; }

            var frames = Slice(sheet);
            if (frames.Length == 0) { _worn.Add(new LayerChoice { category = cat.name, include = false }); continue; }

            var go = new GameObject(string.IsNullOrEmpty(cat.name) ? "Layer" : cat.name);
            var t = go.transform;
            t.SetParent(transform, false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            go.AddComponent<NPCLayerTag>(); // marks it as ours so Clear can sweep stale editor previews

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = order++;
            if (layerMaterial != null) sr.sharedMaterial = layerMaterial;
            sr.color = choice != null ? choice.tint : PickTint(cat, rng);
            sr.sprite = frames[0];
            _worn.Add(new LayerChoice { category = cat.name, include = true, styleIndex = style, tint = sr.color });

            _renderers.Add(sr);
            _frames.Add(frames);
            _categories.Add(cat.name);
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

    // Put this character in a team's kit: the car's two colours on the layers TeamUniform says they belong
    // on, everything else left as it was rolled. Returns how many layers took a colour, so a caller can tell
    // an outfit that could not be dressed (a library with none of the uniform's categories) from one that was.
    public int WearTeamColours(Color primary, Color secondary)
    {
        int changed = 0;
        for (int i = 0; i < _renderers.Count && i < _categories.Count; i++)
        {
            if (_renderers[i] == null) continue;
            if (!Draftmaster.Crowd.TeamUniform.TryColour(_categories[i], primary, secondary, out Color tint)) continue;
            _renderers[i].color = tint;
            changed++;
        }
        return changed;
    }

    LayerChoice FindChoice(string category)
    {
        if (authoredOutfit == null) return null;
        foreach (var c in authoredOutfit)
            if (c != null && c.category == category) return c;
        return null;
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

    // Frames come from the shared cache, not a fresh slice per NPC. What makes one NPC look different
    // from another is the tint on its SpriteRenderer and which sheets it drew, never the Sprite objects
    // themselves — so a paddock of a hundred people shares the same hundred-odd frames between them.
    Sprite[] Slice(Texture2D sheet)
    {
        return Draftmaster.Crowd.NPCSpriteCache.Slice(
            sheet, library.frameWidth, library.frameHeight, library.pivot, library.pixelsPerUnit);
    }

    // Also runs in edit mode (inspector preview), so it can't just Destroy. The NPCLayerTag sweep
    // catches layers this instance doesn't know about — editor previews saved into the scene, or
    // leftovers from before a domain reload wiped the _renderers list.
    public void Clear()
    {
        foreach (var r in _renderers) if (r != null) DestroyNode(r.gameObject);
        _renderers.Clear();
        _frames.Clear();
        _categories.Clear();
        _worn.Clear();
        _frameCount = 0;

        var stale = GetComponentsInChildren<NPCLayerTag>(true);
        foreach (var tag in stale) if (tag != null) DestroyNode(tag.gameObject);
    }

    static void DestroyNode(GameObject go)
    {
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }
}

// Marker for generated layer objects — lets NPCLayeredAppearance.Clear find and remove layers that
// survived a domain reload or were saved into the scene by the editor preview. Hidden from the Add
// Component menu.
[AddComponentMenu("")]
public class NPCLayerTag : MonoBehaviour { }
