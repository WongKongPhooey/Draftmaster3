using System.Collections.Generic;
using Draftmaster.Sponsors;
using UnityEngine;

// Loads the little car-scale decal PNGs. One per brand at Resources/Sponsors/Car/<key>.png, drawn at the
// project pixel standard (12.8 px/m) so a 12x6 logo is 0.94m x 0.47m of bodywork — about right for a
// quarter panel. Draftmaster > Sponsors > Generate Placeholder Decals writes a set for every brand in the
// database; drop real art over the top with the same filename and nothing else changes.
public static class SponsorArt
{
    static readonly Dictionary<string, Texture2D> _cache = new();

    public static Texture2D Load(string logoKey)
    {
        if (string.IsNullOrEmpty(logoKey)) return null;
        if (_cache.TryGetValue(logoKey, out var cached)) return cached;

        var tex = Resources.Load<Texture2D>(SponsorCatalog.CarArtFolder + logoKey);
        _cache[logoKey] = tex;      // cache misses too: a brand with no art shouldn't hit the disk every race
        return tex;
    }

    public static bool Has(string logoKey) => Load(logoKey) != null;

    public static void Invalidate() => _cache.Clear();

    // The layout describing where the panels are on a carset's paint. One asset per carset at
    // Resources/Sponsors/<carset>Layout; the cup26 asset doubles as the fallback for any carset without one.
    public static CarSponsorLayout LayoutFor(string carsetPrefix)
    {
        CarSponsorLayout layout = null;
        if (!string.IsNullOrEmpty(carsetPrefix)) layout = Resources.Load<CarSponsorLayout>($"Sponsors/{carsetPrefix}Layout");
        if (layout == null) layout = Resources.Load<CarSponsorLayout>("Sponsors/cup26Layout");
        return layout;
    }
}
