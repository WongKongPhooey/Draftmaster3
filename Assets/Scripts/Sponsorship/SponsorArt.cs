using System.Collections.Generic;
using Draftmaster.Sponsors;
using UnityEngine;

// Loads sponsor art. Each brand has a full-size image (boards, UI), a hood decal and a small decal shared by
// the trunk and both rear quarters — see SponsorKeys for the folders. Decals are drawn at the project pixel
// standard (12.8 px/m) and never scaled. Draftmaster > Sponsors > Generate Placeholder Decals writes a set
// for every brand in the database; drop real art over the top with the same filename and nothing else
// changes.
public static class SponsorArt
{
    static readonly Dictionary<string, Texture2D> _cache = new();

    // The decal a slot wears: hood.png on the hood, small.png everywhere else.
    public static Texture2D Load(string logoKey, SponsorSlot slot)
    {
        if (string.IsNullOrEmpty(logoKey)) return null;
        return First(SponsorKeys.DecalArtPaths(slot, logoKey));
    }

    // The full-size brand image for boards and UI, falling back to the hood decal.
    public static Texture2D LoadFull(string logoKey)
    {
        if (string.IsNullOrEmpty(logoKey)) return null;
        return First(SponsorKeys.FullArtPaths(logoKey));
    }

    public static bool Has(string logoKey) => Load(logoKey, SponsorSlot.Hood) != null;

    public static void Invalidate() => _cache.Clear();

    static Texture2D First(string[] paths)
    {
        foreach (string path in paths)
        {
            if (!_cache.TryGetValue(path, out var tex))
            {
                tex = Resources.Load<Texture2D>(path);
                _cache[path] = tex;     // cache misses too: a brand with no art shouldn't hit the disk every race
            }
            if (tex != null) return tex;
        }
        return null;
    }

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
