using System.Collections.Generic;
using Draftmaster.Sponsors;
using UnityEngine;

// The game side of the Paint Scheme Designer: which liveries can be repainted, the colours and patterns on
// offer, and turning a PaintScheme into a livery Sprite the rest of the paint pipeline (sponsor decals,
// VehicleDamage) treats exactly like the original art.
//
// A livery can be repainted when it has been split into layers under Resources/PaintShop/:
//   <livery>_mask.png    paintable body — alpha = paintable, grey = shading (255 full colour)
//   <livery>_detail.png  everything that isn't paint: glass, number, tyres, lights
// Patterns are Resources/PaintShop/Patterns/<name>.png, 64x32, sprayed in the accent colour.
// A livery without layers keeps its drawn paint whatever the scheme says.
public static class PaintShop
{
    const string Folder = "PaintShop/";
    const string PatternFolder = "PaintShop/Patterns";
    public const string PaintedTag = "_painted";

    // Colours on the designer's swatch rows. Stock-car loud, plus the factory grey.
    public static readonly Color32[] Palette =
    {
        new Color32(175, 175, 175, 255), new Color32(245, 245, 240, 255), new Color32(24, 24, 28, 255),
        new Color32(200, 30, 35, 255),   new Color32(140, 0, 0, 255),     new Color32(240, 120, 20, 255),
        new Color32(240, 200, 20, 255),  new Color32(40, 150, 60, 255),   new Color32(20, 90, 50, 255),
        new Color32(40, 170, 220, 255),  new Color32(30, 70, 190, 255),   new Color32(20, 30, 90, 255),
        new Color32(120, 50, 170, 255),  new Color32(230, 90, 160, 255),  new Color32(120, 80, 40, 255),
        new Color32(200, 170, 110, 255),
    };

    static List<string> _patterns;

    // Pattern names, "" (plain) first.
    public static IReadOnlyList<string> Patterns
    {
        get
        {
            if (_patterns != null) return _patterns;
            _patterns = new List<string> { "" };
            var found = new List<string>();
            foreach (var t in Resources.LoadAll<Texture2D>(PatternFolder)) found.Add(t.name);
            found.Sort(System.StringComparer.OrdinalIgnoreCase);
            _patterns.AddRange(found);
            return _patterns;
        }
    }

    public static string PatternLabel(string pattern) =>
        string.IsNullOrEmpty(pattern) ? "PLAIN" : pattern.Replace('-', ' ').Replace('_', ' ').ToUpperInvariant();

    // The livery the player's car wears in this career, e.g. cup26livery89.
    public static string PlayerLiveryName => $"cup26livery{PlayerDriver.CarNumber}";

    public static bool CanRepaint(string liveryName) =>
        Layer(liveryName, "_mask") != null && Layer(liveryName, "_detail") != null;

    // "cup26livery89_painted_..._sponsored" -> "cup26livery89": the name of the art in Resources.
    public static string OriginalName(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName)) return spriteName;
        int cut = spriteName.Length;
        int painted = spriteName.IndexOf(PaintedTag, System.StringComparison.Ordinal);
        if (painted >= 0) cut = Mathf.Min(cut, painted);
        int sponsored = spriteName.IndexOf("_sponsored", System.StringComparison.Ordinal);
        if (sponsored >= 0) cut = Mathf.Min(cut, sponsored);
        return spriteName.Substring(0, cut);
    }

    static readonly Dictionary<string, Sprite> _cache = new();

    // `original` repainted in `scheme`, or `original` itself when there is no scheme or no layers for it.
    // Results are cached per (livery, scheme), so the AI-sized poll in SponsorPaintDirector costs nothing.
    public static Sprite Render(Sprite original, PaintScheme scheme)
    {
        if (original == null || scheme == null) return original;
        string livery = original.name;
        var mask = Layer(livery, "_mask");
        var detail = Layer(livery, "_detail");
        if (mask == null || detail == null) return original;

        string key = livery + PaintedTag + "_" + scheme.Signature;
        if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;

        int w = mask.width, h = mask.height;
        if (detail.width != w || detail.height != h || !mask.isReadable || !detail.isReadable)
        {
            Debug.LogWarning($"PaintShop: layers for {livery} must be the same size and Read/Write enabled — keeping its drawn paint.");
            return original;
        }

        Color32[] pattern = null;
        if (scheme.HasPattern)
        {
            var pt = Resources.Load<Texture2D>(PatternFolder + "/" + scheme.pattern);
            if (pt != null && pt.isReadable && pt.width == w && pt.height == h) pattern = pt.GetPixels32();
        }

        var px = LiveryCompositor.Compose(mask.GetPixels32(), detail.GetPixels32(), pattern, w, h, scheme);
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = key,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        tex.SetPixels32(px);
        tex.Apply(false, false);

        var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f),
                                   original.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.name = key;
        _cache[key] = sprite;
        return sprite;
    }

    // The full paint job — scheme, then sponsor decals from `dealForSlot` — for previews (the designer, the
    // car on the shop floor). Null when the livery isn't in Resources.
    public static Sprite RenderWithSponsors(string liveryName, PaintScheme scheme, IDictionary<SponsorSlot, string> logoForSlot)
    {
        var original = Resources.Load<Sprite>(liveryName);
        if (original == null) return null;
        var painted = Render(original, scheme);

        var decals = new List<SponsorLiveryBaker.Decal>();
        if (logoForSlot != null)
            foreach (var kv in logoForSlot)
            {
                if (kv.Key == SponsorSlot.None || string.IsNullOrEmpty(kv.Value)) continue;
                var art = SponsorArt.Load(kv.Value, kv.Key);
                if (art != null) decals.Add(new SponsorLiveryBaker.Decal { slot = kv.Key, art = art });
            }
        if (decals.Count == 0) return painted;
        var layout = SponsorArt.LayoutFor(CarsetOf(liveryName));
        return layout != null ? SponsorLiveryBaker.Bake(painted, layout, decals) : painted;
    }

    // What the player's car wears right now, from the saved scheme and the sponsor book.
    public static Sprite RenderPlayerCar()
    {
        var logos = new Dictionary<SponsorSlot, string>();
        foreach (var deal in SponsorBook.Deals)
            if (deal.IsActive && deal.IsPlaced) logos[deal.slot] = deal.logoKey;
        return RenderWithSponsors(PlayerLiveryName, PaintBook.Current, logos);
    }

    static string CarsetOf(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName)) return null;
        int at = spriteName.IndexOf("livery", System.StringComparison.OrdinalIgnoreCase);
        return at > 0 ? spriteName.Substring(0, at) : null;
    }

    static Texture2D Layer(string liveryName, string suffix) =>
        string.IsNullOrEmpty(liveryName) ? null : Resources.Load<Texture2D>(Folder + liveryName + suffix);
}
