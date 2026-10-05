using UnityEngine;

// One piece of scattered trackside scenery — a fan's motorhome, a spectator, a bush.
//
// What is SAVED is only the recipe (which sprite, how big, what tint, which outfit seed); the renderer is
// built on enable, in the editor as well as in play, on a child that is never saved. That matters twice
// over here: a spectator's paper-doll frames are runtime Sprite objects that can't be stored in a prefab at
// all, and a track package carrying a few hundred of these stays a few hundred small recipes rather than a
// few thousand renderers. Move, turn or delete a piece by hand like anything else; tick `locked` and a
// re-scatter leaves it where it is and plants around it.
//
// A prefab-look palette entry is instantiated directly and carries one of these only as the marker that
// says "the scatter put this here" — it draws nothing of its own.
[ExecuteAlways]
[SelectionBase]
public class SceneryPiece : MonoBehaviour
{
    public const string ArtName = "SceneryArt";

    [Tooltip("The palette entry this came from.")]
    public string kind;
    [Tooltip("Keep this piece (and its spot) when the track is re-scattered.")]
    public bool locked;
    public SceneryPalette.Look look = SceneryPalette.Look.Sprites;

    [Tooltip("Sprites look: what to draw.")]
    public Sprite sprite;
    [Tooltip("Metres: x along local +X, y along local Y. 0 = the sprite's own size.")]
    public Vector2 size;
    public Color tint = Color.white;
    [Tooltip("Spectator look: the dice the outfit is rolled from. Same seed, same person.")]
    public int outfitSeed;
    public int sortingOrder = 2;

    void OnEnable() => Rebuild();

#if UNITY_EDITOR
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && isActiveAndEnabled) Rebuild();
        };
    }
#endif

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        ClearArt();
        if (look == SceneryPalette.Look.Prefab) return;

        var art = new GameObject(ArtName);
        art.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
        art.transform.SetParent(transform, false);

        bool built = look == SceneryPalette.Look.Spectator ? BuildSpectator(art) : BuildSprite(art);
        if (!built) { Kill(art); return; }

        // The doll's layers are made after the flags were set on their parent; flags don't inherit.
        foreach (var t in art.GetComponentsInChildren<Transform>(true))
            t.gameObject.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
    }

    // Anything this (or an earlier domain's) Rebuild left behind, so a reload never doubles the art up.
    public void ClearArt()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child.name == ArtName) Kill(child.gameObject);
        }
    }

    bool BuildSprite(GameObject art)
    {
        if (sprite == null) return false;
        var sr = art.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = tint;
        sr.sharedMaterial = NPCFactory.UnlitSpriteMaterial;
        sr.sortingOrder = sortingOrder;

        Vector2 native = sprite.bounds.size;
        if (size.x > 0f && size.y > 0f && native.x > 0.0001f && native.y > 0.0001f)
            art.transform.localScale = new Vector3(size.x / native.x, size.y / native.y, 1f);
        return true;
    }

    // Built the same way NPCFactory dresses a body: the doll on its own child, scaled from its 8px frame up
    // to the standard on-foot person height.
    bool BuildSpectator(GameObject art)
    {
        var library = Resources.Load<NPCPartLibrary>("NPC/NPCPartLibrary");
        if (library == null) return false;

        var doll = art.AddComponent<NPCLayeredAppearance>();
        doll.library = library;
        doll.sortingLayerName = "Default";
        doll.baseSortingOrder = sortingOrder;
        doll.layerMaterial = NPCFactory.UnlitSpriteMaterial;
        if (!doll.Build(outfitSeed)) return false;

        float frameWorldH = Mathf.Max(0.01f, library.frameHeight / Mathf.Max(1f, library.pixelsPerUnit));
        art.transform.localScale = Vector3.one * (PitCrewSpawner.OnFootPersonHeight / frameWorldH);
        return true;
    }

    static void Kill(GameObject go)
    {
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }
}
