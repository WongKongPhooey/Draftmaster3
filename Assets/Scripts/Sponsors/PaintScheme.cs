using System;
using UnityEngine;

namespace Draftmaster.Sponsors
{
    // The player's paint job, as chosen with the Paint Scheme Designer at Team HQ: a base coat, an optional
    // livery pattern laid over it, and the colour that pattern is sprayed in. Sponsors are not part of it —
    // they live in SponsorBook, because a deal only pays while its decal is on the car.
    [Serializable]
    public class PaintScheme
    {
        public Color32 baseColour = new Color32(175, 175, 175, 255);
        public string pattern = "";                                    // Resources/PaintShop/Patterns/<pattern>, "" = plain
        public Color32 accentColour = new Color32(240, 240, 240, 255);

        public bool HasPattern => !string.IsNullOrEmpty(pattern);

        public PaintScheme Clone() => new PaintScheme
        {
            baseColour = baseColour,
            pattern = pattern,
            accentColour = accentColour,
        };

        // Stable text for caches and "is the car already wearing this" checks.
        public string Signature =>
            $"{Hex(baseColour)}-{(HasPattern ? pattern : "plain")}-{(HasPattern ? Hex(accentColour) : "x")}";

        static string Hex(Color32 c) => $"{c.r:X2}{c.g:X2}{c.b:X2}";
    }

    // Where the player's paint job is kept. PlayerPrefs-backed like SponsorBook; null means "factory paint"
    // (the livery exactly as drawn), which is what a new career starts in.
    public static class PaintBook
    {
        const string Key = "paint.scheme";

        static PaintScheme _cache;
        static bool _loaded;

        public static event Action Changed;

        public static PaintScheme Current
        {
            get
            {
                if (_loaded) return _cache;
                _loaded = true;
                string json = PlayerPrefs.GetString(Key, "");
                _cache = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<PaintScheme>(json);
                return _cache;
            }
        }

        public static void Save(PaintScheme scheme)
        {
            if (scheme == null) { ClearAll(); return; }
            _cache = scheme.Clone();
            _loaded = true;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(_cache));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // Back to factory paint.
        public static void ClearAll()
        {
            _cache = null;
            _loaded = true;
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void InvalidateCache()
        {
            _cache = null;
            _loaded = false;
        }
    }

    // Builds a livery from its layers:
    //   mask    — where the body is paintable. Alpha = paintable, grey level = shading (255 full colour, darker
    //             for the rim), so a base coat keeps the panel shading the artist drew.
    //   pattern — optional, white-ish shapes over the base coat, sprayed in the accent colour (its alpha
    //             blends), and only where the mask is paintable.
    //   detail  — everything that isn't paint (glass, number, tyres, lights), drawn last and untouched.
    // Sponsors go on afterwards, through SponsorLiveryBaker, so they sit on top of the paint like decals.
    public static class LiveryCompositor
    {
        public static Color32[] Compose(Color32[] mask, Color32[] detail, Color32[] pattern, int w, int h, PaintScheme scheme)
        {
            var outPx = new Color32[w * h];
            for (int i = 0; i < outPx.Length; i++)
            {
                Color32 m = mask[i];
                if (m.a > 0)
                {
                    float r = scheme.baseColour.r, g = scheme.baseColour.g, b = scheme.baseColour.b;
                    if (pattern != null && scheme.HasPattern)
                    {
                        float a = pattern[i].a / 255f;
                        r = scheme.accentColour.r * a + r * (1f - a);
                        g = scheme.accentColour.g * a + g * (1f - a);
                        b = scheme.accentColour.b * a + b * (1f - a);
                    }
                    float k = m.r / 255f;
                    outPx[i] = new Color32((byte)Mathf.RoundToInt(r * k), (byte)Mathf.RoundToInt(g * k),
                                           (byte)Mathf.RoundToInt(b * k), m.a);
                }

                Color32 d = detail[i];
                if (d.a == 255) outPx[i] = d;
                else if (d.a > 0)
                {
                    Color32 o = outPx[i];
                    float a = d.a / 255f;
                    outPx[i] = new Color32((byte)(d.r * a + o.r * (1f - a)), (byte)(d.g * a + o.g * (1f - a)),
                                           (byte)(d.b * a + o.b * (1f - a)), (byte)Mathf.Max(o.a, d.a));
                }
            }
            return outPx;
        }
    }
}
