using Draftmaster.Controls;
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
//
// While a pad is the device in use the keycap is swapped for that pad's button (A / CROSS for interact) by an
// InputPromptGlyph on the icon, and swapped back the moment the keyboard is touched again.
public static class InputPromptIcon
{
    public const string InteractKeyResource = "UI/Prompts/key_e";

    // The same keycap with a question mark on it, for a player whose only input is a fingertip: there is no
    // E to press, so the prompt says "there is something here" and the tap is the answer.
    public const string TapPromptResource = "UI/Prompts/prompt_tap";

    static Sprite _interactKey;
    static bool _interactKeyLoaded;
    static Sprite _tapPrompt;
    static bool _tapPromptLoaded;
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

    // Null when the art is missing — the prompt then keeps whatever it would have shown otherwise.
    public static Sprite TapPrompt
    {
        get
        {
            if (!_tapPromptLoaded)
            {
                _tapPromptLoaded = true;
                _tapPrompt = Resources.Load<Sprite>(TapPromptResource);
                if (_tapPrompt == null)
                    Debug.LogWarning($"InputPromptIcon: no sprite at Resources/{TapPromptResource} — touch prompts show the keycap.");
            }
            return _tapPrompt;
        }
    }

    // Build the icon under `parent`, sized so it stands `worldHeight` metres tall. Returns null if the art
    // is missing. The renderer is left for the caller to position — this only handles art, size and sorting.
    // `pad` is the button that does the same job on a pad — the interact button unless told otherwise.
    public static SpriteRenderer Create(Transform parent, string name, float worldHeight,
                                        string sortingLayerName, int sortingOrder,
                                        PadButton pad = PadBindings.Interact)
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

        // Always attached, pad or not: it is also what holds the icon at one size on screen.
        InputPromptGlyph.Attach(sr, sprite, pad, worldHeight);
        return sr;
    }

    // Every key prompt is this fraction of the screen's height, wherever it is and whatever it hangs off.
    // 0.3 m under the paddock's on-foot camera (3.5 m half-height), which is where prompts were tuned.
    public const float ScreenHeightFraction = 0.043f;

    // How tall, in world units at `worldPos`, a prompt should be so it covers ScreenHeightFraction of the
    // screen — rounded to a whole number of screen pixels per art pixel so the pixel keycap stays crisp.
    // Falls back to `fallbackWorldHeight` with no camera to measure against.
    public static float ScreenHeightInWorld(Camera cam, Vector3 worldPos, Sprite sprite, float fallbackWorldHeight)
    {
        int screenH = Screen.height;
        if (cam == null || screenH <= 0) return fallbackWorldHeight;

        float worldPerScreenPx;
        if (cam.orthographic)
            worldPerScreenPx = 2f * cam.orthographicSize / screenH;
        else
        {
            float depth = Mathf.Max(0.01f, Vector3.Dot(worldPos - cam.transform.position, cam.transform.forward));
            worldPerScreenPx = 2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / screenH;
        }
        if (worldPerScreenPx <= 0f) return fallbackWorldHeight;

        float targetPx = ScreenHeightFraction * screenH;
        float artPx = sprite != null && sprite.rect.height > 0f ? sprite.rect.height : 16f;
        float snappedPx = artPx * Mathf.Max(1f, Mathf.Round(targetPx / artPx));
        return snappedPx * worldPerScreenPx;
    }

    // Scale a transform so `sprite` renders `worldHeight` metres tall in the WORLD, cancelling out the
    // sprite's PPU and whatever scale the transform's parents carry.
    public static void Fit(Transform t, Sprite sprite, float worldHeight)
    {
        if (sprite == null || worldHeight <= 0f) return;
        float spriteH = sprite.bounds.size.y;
        if (spriteH < 1e-5f) return;
        Vector3 parent = t.parent != null ? t.parent.lossyScale : Vector3.one;
        float s = worldHeight / spriteH;
        t.localScale = new Vector3(s / NonZero(parent.x), s / NonZero(parent.y), 1f);
    }

    static float NonZero(float v) => Mathf.Abs(v) < 1e-5f ? 1f : Mathf.Abs(v);

    static Material UnlitMaterial()
    {
        if (_unlit != null) return _unlit;
        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        _unlit = new Material(sh);
        return _unlit;
    }
}
