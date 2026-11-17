using System.Collections.Generic;
using UnityEngine;

// Pixel art for the SINGLE RACE screens, drawn at runtime at exactly the size it is shown (one texel per canvas
// pixel, point filtered), so it stays crisp under the screen's whole-number canvas scale.
//
// The design handoff (Designs/draftmaster3_race_flow_unity_handoff) ships PNGs for its 17 sample tracks and seven
// series. They are drawn here instead because the game has 38 venues and their real geometry: a track's layout is
// traced off its TrackInfoV2 spline, the same road the race drives, and a series' stripes dim when nobody is
// entered — a state the shipped PNGs can't show. The recipes are the handoff's: 3 px line in a 7 px ink outline,
// red start/finish square with a cream edge, 8 px blueprint grid, 135° stripes in 6 px bands, asphalt rows.
public static class RaceFlowArt
{
    public static Color Hex(uint rgb, byte alpha = 0xff) =>
        new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, alpha);

    static readonly Dictionary<string, Texture2D> Cache = new();

    static Texture2D NewTexture(int w, int h, string name, TextureWrapMode wrap = TextureWrapMode.Clamp)
    {
        var t = new Texture2D(Mathf.Max(1, w), Mathf.Max(1, h), TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = wrap,
            name = name,
            hideFlags = HideFlags.DontSave,
        };
        return t;
    }

    // ------------------------------------------------------------------ track layout

    // The track's centreline fitted into w x h, `line` coloured, in a 2 px ink edge, with the start/finish
    // square. Null when the track has no geometry.
    public static Texture2D TrackLayout(string trackId, int w, int h, Color line)
    {
        string key = $"track:{trackId}:{w}x{h}:{ColorUtility.ToHtmlStringRGB(line)}";
        if (Cache.TryGetValue(key, out var hit) && hit != null) return hit;

        var info = TrackCatalog.Geometry(trackId);
        if (info == null) return null;
        var points = Centreline(info, 360);
        if (points.Count < 2) return null;

        var tex = NewTexture(w, h, "TrackLayout_" + trackId);
        var px = new Color32[w * h];

        // Fit with room for the outline (3.5 px each side) and the start marker.
        const float margin = 4f;
        Vector2 min = points[0], max = points[0];
        foreach (var p in points) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
        Vector2 size = max - min;
        float scale = Mathf.Min((w - 2f * margin) / Mathf.Max(size.x, 1e-3f), (h - 2f * margin) / Mathf.Max(size.y, 1e-3f));
        Vector2 offset = new Vector2((w - size.x * scale) * 0.5f, (h - size.y * scale) * 0.5f);
        var mapped = new List<Vector2>(points.Count);
        foreach (var p in points) mapped.Add((p - min) * scale + offset);

        Stroke(px, w, h, mapped, 7, Hex(0x05060a));
        Stroke(px, w, h, mapped, 3, line);

        // Start/finish: 6x6, cream edge, red inside, centred on the line's first point.
        var s = mapped[0];
        int sx = Mathf.RoundToInt(s.x) - 3, sy = Mathf.RoundToInt(s.y) - 3;
        Fill(px, w, h, sx, sy, 6, 6, Hex(0xf4ead7));
        Fill(px, w, h, sx + 1, sy + 1, 4, 4, Hex(0xc4452f));

        tex.SetPixels32(px);
        tex.Apply(false, false);
        Cache[key] = tex;
        return tex;
    }

    // The spline sampled round the lap from the start/finish line, in world metres (x right, y up).
    public static List<Vector2> Centreline(TrackInfoV2 info, int samples)
    {
        var points = new List<Vector2>(samples + 1);
        float lap = info.TotalLength();
        if (lap <= 0f) return points;
        float start = info.startFinishDistance;
        for (int i = 0; i <= samples; i++)
        {
            float d = Mathf.Repeat(start + lap * i / samples, lap);
            info.SampleAuthoredSpline(d, out var pos, out _);
            points.Add(pos);
        }
        return points;
    }

    // A square brush stamped every half pixel along the polyline: hard, mitred-looking corners, no anti-aliasing.
    static void Stroke(Color32[] px, int w, int h, List<Vector2> pts, int brush, Color32 colour)
    {
        int lo = -(brush / 2), hi = brush - 1 - brush / 2;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector2 a = pts[i], b = pts[i + 1];
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) * 2f));
            for (int k = 0; k <= steps; k++)
            {
                Vector2 p = Vector2.Lerp(a, b, k / (float)steps);
                int cx = Mathf.FloorToInt(p.x), cy = Mathf.FloorToInt(p.y);
                for (int y = cy + lo; y <= cy + hi; y++)
                {
                    if (y < 0 || y >= h) continue;
                    for (int x = cx + lo; x <= cx + hi; x++)
                        if (x >= 0 && x < w) px[y * w + x] = colour;
                }
            }
        }
    }

    static void Fill(Color32[] px, int w, int h, int x0, int y0, int fw, int fh, Color32 colour)
    {
        for (int y = y0; y < y0 + fh; y++)
        {
            if (y < 0 || y >= h) continue;
            for (int x = x0; x < x0 + fw; x++)
                if (x >= 0 && x < w) px[y * w + x] = colour;
        }
    }

    // ------------------------------------------------------------------ backgrounds

    // The track art box: #0F1420 with #182136 1 px lines every 8 px, the lines on the top and left of each cell.
    // Exactly w x h, so the grid starts at the box's top-left corner the way the CSS gradient does.
    public static Texture2D Blueprint(int w, int h)
    {
        string key = $"grid:{w}x{h}";
        if (Cache.TryGetValue(key, out var hit) && hit != null) return hit;
        var tex = NewTexture(w, h, "Blueprint");
        var px = new Color32[w * h];
        Color32 bg = Hex(0x0f1420), line = Hex(0x182136);
        for (int y = 0; y < h; y++)
        {
            int fromTop = h - 1 - y;
            for (int x = 0; x < w; x++)
                px[y * w + x] = fromTop % 8 == 0 || x % 8 == 0 ? line : bg;
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        Cache[key] = tex;
        return tex;
    }

    // A series tile's art: 135° stripes in its colour, 6 px bands at alpha 0x66 / 0x33 over #0F1420 (0x2E / 0x18 when
    // nobody is entered), and a 4 px solid bar along the bottom.
    public static Texture2D SeriesStripes(uint colour, bool hasField, int w, int h)
    {
        string key = $"series:{colour:x6}:{hasField}:{w}x{h}";
        if (Cache.TryGetValue(key, out var hit) && hit != null) return hit;
        var tex = NewTexture(w, h, "SeriesStripes");
        var px = new Color32[w * h];
        Color c = Hex(colour), bg = Hex(0x0f1420);
        Color a = Color.Lerp(bg, c, (hasField ? 0x66 : 0x2e) / 255f);
        Color b = Color.Lerp(bg, c, (hasField ? 0x33 : 0x18) / 255f);
        a.a = b.a = 1f;
        float band = 6f * Mathf.Sqrt(2f);   // a 6 px band measured across the diagonal
        for (int y = 0; y < h; y++)
        {
            int fromTop = h - 1 - y;
            for (int x = 0; x < w; x++)
            {
                Color32 col;
                if (fromTop >= h - 4) col = c;
                else col = ((int)Mathf.Floor((x + fromTop) / band) & 1) == 0 ? a : b;
                px[y * w + x] = col;
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        Cache[key] = tex;
        return tex;
    }

    // The car slot: horizontal asphalt rows, 2 px #2B2B30 then 1 px #303036, counted up from the bottom.
    public static Texture2D Asphalt(int w, int h)
    {
        string key = $"asphalt:{w}x{h}";
        if (Cache.TryGetValue(key, out var hit) && hit != null) return hit;
        var tex = NewTexture(w, h, "Asphalt");
        var px = new Color32[w * h];
        Color32 dark = Hex(0x2b2b30), light = Hex(0x303036);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = y % 3 == 2 ? light : dark;
        tex.SetPixels32(px);
        tex.Apply(false, false);
        Cache[key] = tex;
        return tex;
    }

    // ------------------------------------------------------------------ small parts

    static Sprite _white, _cursor;

    // A 1x1 white sprite for flat fills (Image.sprite = null draws white too, but some code paths want a sprite).
    public static Sprite White
    {
        get
        {
            if (_white != null) return _white;
            var t = NewTexture(1, 1, "RaceFlowWhite");
            t.SetPixel(0, 0, Color.white);
            t.Apply(false, false);
            _white = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            _white.hideFlags = HideFlags.DontSave;
            return _white;
        }
    }

    // The ▶ cursor: a 4x7 pixel triangle in white (tinted gold by the Image). The pixel fonts have no ▶.
    public static Sprite Cursor
    {
        get
        {
            if (_cursor != null) return _cursor;
            var t = NewTexture(4, 7, "RaceFlowCursor");
            var px = new Color32[4 * 7];
            for (int y = 0; y < 7; y++)
            {
                int reach = 3 - Mathf.Abs(y - 3);   // 0,1,2,3,2,1,0
                for (int x = 0; x < 4; x++) px[y * 4 + x] = x <= reach ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
            t.SetPixels32(px);
            t.Apply(false, false);
            _cursor = Sprite.Create(t, new Rect(0, 0, 4, 7), new Vector2(0.5f, 0.5f), 1f);
            _cursor.hideFlags = HideFlags.DontSave;
            return _cursor;
        }
    }
}
