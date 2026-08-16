using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Draftmaster.Sponsors
{
    // Burns sponsor decals into a copy of a car's livery and hands back a new Sprite.
    //
    // Why bake rather than parent little sprites to the car: the bodywork is not a sprite at runtime. Each
    // car's paint becomes a subdivided, deformable mesh (VehicleDamage) whose UVs come straight off the
    // livery, so a decal baked into the texture dents with the panel it is painted on, needs no extra
    // renderer or sorting order per car, and can never drift out of place as the car rotates. It also means
    // the AI field gets sponsors for free through the exact same call.
    //
    // Decals are blitted 1:1 — never scaled — so the art stays on the project's 12.8 px/m pixel grid.
    public static class SponsorLiveryBaker
    {
        // Baked results are shared: a 43-car field runs a handful of distinct decal sets, and every car
        // wearing the same combination can point at one texture.
        static readonly Dictionary<string, Sprite> _cache = new();
        static bool _warnedUnreadable;

        public struct Decal
        {
            public SponsorSlot slot;
            public Texture2D art;
        }

        // Returns a sprite with the decals painted on, or `livery` unchanged when there is nothing to paint
        // (or the source art isn't readable — see PixelSpriteImport, which marks liveries and sponsor decals
        // Read/Write for exactly this).
        public static Sprite Bake(Sprite livery, CarSponsorLayout layout, IList<Decal> decals)
        {
            if (livery == null || layout == null || decals == null || decals.Count == 0) return livery;

            string key = CacheKey(livery, decals);
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var src = livery.texture;
            if (src == null || !src.isReadable)
            {
                if (!_warnedUnreadable)
                {
                    _warnedUnreadable = true;
                    Debug.LogWarning($"SponsorLiveryBaker: '{(src != null ? src.name : "null")}' is not Read/Write enabled, " +
                                     "so sponsor decals can't be painted. Run Draftmaster > Art > Retarget World Sprites to Pixel Standard.");
                }
                return livery;
            }

            Rect r = livery.textureRect;
            int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
            if (w <= 0 || h <= 0) return livery;

            var pixels = src.GetPixels32(0);
            var baked = new Color32[w * h];
            int srcX = Mathf.RoundToInt(r.x), srcY = Mathf.RoundToInt(r.y);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    baked[y * w + x] = pixels[(srcY + y) * src.width + (srcX + x)];

            for (int i = 0; i < decals.Count; i++)
            {
                var d = decals[i];
                if (d.art == null || d.slot == SponsorSlot.None) continue;
                if (!d.art.isReadable)
                {
                    Debug.LogWarning($"SponsorLiveryBaker: decal '{d.art.name}' is not Read/Write enabled — skipped.");
                    continue;
                }
                Blit(baked, w, h, d.art, layout.Anchor(d.slot, d.art.width, d.art.height));
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = $"{livery.name}_sponsored",
                filterMode = FilterMode.Point,       // pixel art: never smooth it
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels32(baked);
            tex.Apply(false, false);

            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f),
                                       livery.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = tex.name;
            _cache[key] = sprite;
            return sprite;
        }

        // Alpha-over one decal, clipped to the livery. Straight source-over: pixel art decals are hard-edged,
        // so anything with alpha simply replaces what's under it, weighted for the odd soft edge.
        static void Blit(Color32[] dst, int dstW, int dstH, Texture2D art, Vector2Int anchor)
        {
            var srcPixels = art.GetPixels32(0);
            for (int y = 0; y < art.height; y++)
            {
                int ty = anchor.y + y;
                if (ty < 0 || ty >= dstH) continue;
                for (int x = 0; x < art.width; x++)
                {
                    int tx = anchor.x + x;
                    if (tx < 0 || tx >= dstW) continue;

                    Color32 s = srcPixels[y * art.width + x];
                    if (s.a == 0) continue;
                    int di = ty * dstW + tx;
                    if (s.a == 255) { dst[di] = s; continue; }

                    Color32 d = dst[di];
                    float a = s.a / 255f;
                    dst[di] = new Color32(
                        (byte)(s.r * a + d.r * (1f - a)),
                        (byte)(s.g * a + d.g * (1f - a)),
                        (byte)(s.b * a + d.b * (1f - a)),
                        (byte)Mathf.Max(s.a, d.a));
                }
            }
        }

        static string CacheKey(Sprite livery, IList<Decal> decals)
        {
            var sb = new StringBuilder(livery.name);
            for (int i = 0; i < decals.Count; i++)
            {
                sb.Append('|').Append((int)decals[i].slot).Append(':');
                sb.Append(decals[i].art != null ? decals[i].art.name : "-");
            }
            return sb.ToString();
        }

        // Drop the shared textures — used when the player re-places a decal so the car repaints, and by tests.
        public static void ClearCache()
        {
            foreach (var kv in _cache)
            {
                if (kv.Value == null) continue;
                if (kv.Value.texture != null) Discard(kv.Value.texture);
                Discard(kv.Value);
            }
            _cache.Clear();
        }

        // Destroy is a runtime call and errors out in edit mode, where the editor tools and the EditMode
        // tests bake from.
        static void Discard(Object o)
        {
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
