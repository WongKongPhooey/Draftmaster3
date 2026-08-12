using UnityEngine;

// World-space "press this key" button icon. Replaces the giant yellow TextMesh "E" that used to float over
// interactables — it's the same Kenney pixel keycap art the rest of the pixel UI is heading toward, so a
// prompt reads as a physical key rather than a floating letter.
//
// Art: Assets/Resources/UI/Prompts/key_e.png — tile_0087 out of kenney_input-prompts-pixel (CC0, license
// copied in alongside it). One standalone 16x16 tile rather than a slice of the pack's tilemap.png, so
// there's no sprite sheet to keep sliced.
//
// Callers give a world height in metres and get the icon at that size whatever the sprite's import PPU is,
// so re-importing the art at a different pixels-per-unit can't change how big the prompt looks in game.
public static class InputPromptIcon
{
    public const string InteractKeyResource = "UI/Prompts/key_e";

    static Sprite _interactKey;
    static bool _interactKeyLoaded;
    static Material _unlit;

    // Null when the art is missing — callers fall back to their old text glyph rather than showing nothing.
    public static Sprite InteractKey
    {
        get
        {
            if (!_interactKeyLoaded)
            {
                _interactKeyLoaded = true;
                _interactKey = Resources.Load<Sprite>(InteractKeyResource);
                if (_interactKey == null)
                    Debug.LogWarning($"InputPromptIcon: no sprite at Resources/{InteractKeyResource} — prompts fall back to a text glyph.");
            }
            return _interactKey;
        }
    }

    // Build the icon under `parent`, sized so it stands `worldHeight` metres tall. Returns null if the art
    // is missing. The renderer is left for the caller to position — this only handles art, size and sorting.
    public static SpriteRenderer Create(Transform parent, string name, float worldHeight,
                                        string sortingLayerName, int sortingOrder)
    {
        var sprite = InteractKey;
        if (sprite == null) return null;

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = sortingOrder;
        // The on-foot scenes render through the 3D URP renderer, where Sprite-Lit-Default gets no Light2D
        // and comes out black. Same forced-unlit swap every other world sprite in these scenes does.
        sr.sharedMaterial = UnlitMaterial();

        Fit(go.transform, sprite, worldHeight);
        return sr;
    }

    // Scale a transform so `sprite` renders `worldHeight` metres tall, cancelling out the sprite's PPU.
    public static void Fit(Transform t, Sprite sprite, float worldHeight)
    {
        if (sprite == null || worldHeight <= 0f) return;
        float spriteH = sprite.bounds.size.y;
        if (spriteH < 1e-5f) return;
        t.localScale = Vector3.one * (worldHeight / spriteH);
    }

    static Material UnlitMaterial()
    {
        if (_unlit != null) return _unlit;
        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        _unlit = new Material(sh);
        return _unlit;
    }
}
