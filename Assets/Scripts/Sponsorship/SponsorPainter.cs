using System.Collections.Generic;
using Draftmaster.Data;
using Draftmaster.Sponsors;
using UnityEngine;

// Paints sponsor decals onto a car's bodywork.
//
// Both the player's car and every AI car end up as a VehicleDamage mesh built from a livery Sprite, so
// there is exactly one thing to swap: that sprite. SponsorLiveryBaker composites the decals into a copy of
// the paint and this hands the result to VehicleDamage (or a plain SpriteRenderer, if a car is still using
// one). Because the decal is IN the texture it dents with the panel and needs no second renderer.
public static class SponsorPainter
{
    // What each car is currently wearing, keyed by instance ID, so the poll in SponsorPaintDirector doesn't
    // rebuild the same mesh every second. Cleared per scene by the director.
    static readonly Dictionary<int, string> _painted = new();

    public static void Forget() => _painted.Clear();

    // The player's car wears whatever is placed in the book. Returns true if the paintwork changed.
    public static bool PaintPlayer(GameObject car)
    {
        if (car == null) return false;
        var decals = new List<SponsorLiveryBaker.Decal>();
        foreach (var deal in SponsorBook.Deals)
        {
            if (!deal.IsActive || !deal.IsPlaced) continue;
            var art = SponsorArt.Load(deal.logoKey, deal.slot);
            if (art != null) decals.Add(new SponsorLiveryBaker.Decal { slot = deal.slot, art = art });
        }
        return Apply(car, decals, PaintBook.Current);   // the player's own paint job, from the Team HQ designer
    }

    // AI cars carry sponsors too, so the field doesn't look like a grid of blank test mules. Which brands
    // land on which car is deterministic from the car number: the #8 wears the same two sponsors every
    // session, and no state has to be stored for 42 cars.
    public static bool PaintAi(GameObject car, int carNumber)
    {
        if (car == null || carNumber < 0) return false;

        var catalogue = SponsorCatalog.All();
        if (catalogue.Count == 0) return false;

        var rng = new System.Random(carNumber * 7919 + 104729);
        int count = 2 + rng.Next(3);              // two or three panels sold, never the whole car
        var slots = new List<SponsorSlot>(SponsorSlots.All);
        for (int i = slots.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (slots[i], slots[j]) = (slots[j], slots[i]); }

        var used = new HashSet<int>();
        var decals = new List<SponsorLiveryBaker.Decal>();
        for (int i = 0; i < count && i < slots.Count; i++)
        {
            Sponsor pick = null;
            for (int attempt = 0; attempt < 6 && pick == null; attempt++)
            {
                var candidate = catalogue[rng.Next(catalogue.Count)];
                if (SponsorBook.HasSponsor(candidate.Id)) continue;  // the player's brands are exclusive to them
                if (used.Add(candidate.Id)) pick = candidate;      // one brand per car
            }
            if (pick == null) continue;

            var art = SponsorArt.Load(SponsorCatalog.LogoKey(pick.Name), slots[i]);
            if (art != null) decals.Add(new SponsorLiveryBaker.Decal { slot = slots[i], art = art });
        }
        return Apply(car, decals);
    }

    // ---------------------------------------------------------------- paint

    static bool Apply(GameObject car, List<SponsorLiveryBaker.Decal> decals, PaintScheme scheme = null)
    {
        string signature = (scheme != null ? scheme.Signature : "factory") + "/" + Signature(decals);
        int id = car.GetInstanceID();
        if (_painted.TryGetValue(id, out string worn) && worn == signature) return false;   // already wearing exactly this

        var damage = car.GetComponentInChildren<VehicleDamage>();
        var sr = car.GetComponentInChildren<SpriteRenderer>();

        Sprite baseSprite = damage != null && damage.sourceSprite != null ? damage.sourceSprite
                          : (sr != null ? sr.sprite : null);
        if (baseSprite == null) return false;

        // Re-paint from the ORIGINAL livery, never from an already-baked one, or removing a decal would
        // leave the old one underneath. The carset name survives the bake ("cup26livery8_sponsored"), so
        // the base paint is always recoverable by name.
        Sprite original = OriginalLivery(baseSprite);
        if (original == null) original = baseSprite;

        var layout = SponsorArt.LayoutFor(CarsetOf(original.name));
        if (layout == null)
        {
            Debug.LogWarning("SponsorPainter: no CarSponsorLayout at Resources/Sponsors/cup26Layout — decals skipped.");
            return false;
        }

        // Paint first (a livery with no PaintShop layers comes back unchanged), decals on top.
        Sprite basePaint = PaintShop.Render(original, scheme);
        Sprite painted = decals.Count > 0 ? SponsorLiveryBaker.Bake(basePaint, layout, decals) : basePaint;
        if (painted == null) return false;

        if (damage != null)
        {
            damage.sourceSprite = painted;
            damage.material = null;      // rebuilt from the new texture
            damage.Build();
        }
        else if (sr != null)
        {
            sr.sprite = painted;
        }
        else return false;

        _painted[id] = signature;
        return true;
    }

    // "cup26livery8_sponsored" / "cup26livery89_painted_..." -> the original sprite from Resources.
    static Sprite OriginalLivery(Sprite sprite)
    {
        if (sprite == null) return null;
        string liveryName = PaintShop.OriginalName(sprite.name);
        if (liveryName == sprite.name) return sprite;
        return Resources.Load<Sprite>(liveryName);
    }

    // "cup26livery8" -> "cup26".
    static string CarsetOf(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName)) return null;
        int at = spriteName.IndexOf("livery", System.StringComparison.OrdinalIgnoreCase);
        return at > 0 ? spriteName.Substring(0, at) : null;
    }

    static string Signature(List<SponsorLiveryBaker.Decal> decals)
    {
        if (decals == null || decals.Count == 0) return "bare";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < decals.Count; i++)
            sb.Append((int)decals[i].slot).Append(':').Append(decals[i].art != null ? decals[i].art.name : "-").Append('|');
        return sb.ToString();
    }
}
