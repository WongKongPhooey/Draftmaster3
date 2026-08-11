using UnityEngine;

// Themed IMGUI styles, for the panels that draw with OnGUI rather than a Canvas.
//
// Several panels in this project are IMGUI on purpose: the spline scenes use the 3D URP renderer, where
// a Canvas needs wiring and a font asset, while OnGUI just works with no prefab and no scene setup.
// That convenience is why they all ended up looking like the Unity default skin. These styles give them
// the same plate, palette and pixel font as the rest of the kit, without giving up the zero-setup part.
//
// Styles are built once and rebuilt if the theme is swapped at edit time.
public static class PixelGUI
{
    static PixelUITheme _builtFor;
    static int _builtAtScale;
    static GUIStyle _window, _heading, _row, _rowSelected, _footer, _cursor;
    static Texture2D _flat;

    public static PixelUITheme Theme => PixelUITheme.Instance;

    // IMGUI draws in raw screen pixels, unlike the Canvas UI which is authored on a 640x360 grid and
    // scaled up. Left unscaled, a 16px row that reads fine on a 720p monitor is tiny on a 4K one. This
    // steps the whole panel up in whole numbers, so glyphs stay on the pixel grid rather than being
    // resampled to a fractional size.
    public static int Scale
    {
        get
        {
            var t = Theme;
            if (t != null && t.imguiScaleOverride > 0) return t.imguiScaleOverride;
            return Mathf.Max(1, Mathf.FloorToInt(Screen.height / 540f));
        }
    }

    // Scales a pixel measurement authored at 1x.
    public static float Px(float baseline) => baseline * Scale;

    // The 9-sliced window plate. Drawn with GUI.Box, since only a GUIStyle's border does 9-slicing in IMGUI.
    public static GUIStyle Window { get { Ensure(); return _window; } }
    public static GUIStyle Heading { get { Ensure(); return _heading; } }
    public static GUIStyle Row { get { Ensure(); return _row; } }
    public static GUIStyle RowSelected { get { Ensure(); return _rowSelected; } }
    public static GUIStyle Footer { get { Ensure(); return _footer; } }
    public static GUIStyle Cursor { get { Ensure(); return _cursor; } }

    // A 1x1 white texture, for tint fills (selection bands, dimmers).
    public static Texture2D Flat
    {
        get
        {
            if (_flat == null)
            {
                _flat = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                _flat.SetPixel(0, 0, Color.white);
                _flat.Apply();
            }
            return _flat;
        }
    }

    static void Ensure()
    {
        var t = Theme;
        int scale = Scale;
        if (_window != null && _builtFor == t && _builtAtScale == scale) return;
        _builtFor = t;
        _builtAtScale = scale;

        Font font = t != null ? t.imguiFont : null;
        Color text = t != null ? t.text : Color.white;
        Color dim = t != null ? t.textDim : new Color(0.7f, 0.7f, 0.75f);
        Color gold = t != null ? t.gold : new Color(1f, 0.83f, 0.42f);

        _window = new GUIStyle();
        if (t != null && t.window != null)
        {
            _window.normal.background = t.window.texture;
            // Must match the sprite's authored 9-slice border or the corners smear.
            _window.border = new RectOffset(6, 6, 6, 6);
        }
        _window.padding = new RectOffset(12 * scale, 12 * scale, 10 * scale, 10 * scale);

        _heading = new GUIStyle
        {
            font = font,
            fontSize = 16 * scale,
            wordWrap = true,
            alignment = TextAnchor.UpperLeft,
            richText = true,
        };
        _heading.normal.textColor = gold;

        _row = new GUIStyle
        {
            font = font,
            fontSize = 16 * scale,
            alignment = TextAnchor.MiddleLeft,
            richText = true,
        };
        _row.normal.textColor = dim;
        _row.hover.textColor = text;

        _rowSelected = new GUIStyle(_row);
        _rowSelected.normal.textColor = text;
        _rowSelected.hover.textColor = text;

        _footer = new GUIStyle
        {
            font = font,
            fontSize = 12 * scale,
            alignment = TextAnchor.MiddleLeft,
        };
        _footer.normal.textColor = t != null ? t.textDisabled : new Color(0.6f, 0.6f, 0.66f);

        _cursor = new GUIStyle
        {
            font = font,
            fontSize = 16 * scale,
            alignment = TextAnchor.MiddleCenter,
        };
        _cursor.normal.textColor = gold;
    }

    // Fills a rect with a flat tint. Used for selection bands, which read better over the plate than a
    // second sprite would.
    public static void Fill(Rect r, Color c)
    {
        var prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Flat);
        GUI.color = prev;
    }

    // Draws the selection cursor sprite at the left of a row.
    public static void DrawCursor(Rect row, float size = 12f)
    {
        var t = Theme;
        if (t == null || t.cursor == null) return;
        var sprite = t.cursor;
        var tex = sprite.texture;
        var r = sprite.textureRect;
        var uv = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);

        var prev = GUI.color;
        GUI.color = t.gold;
        GUI.DrawTextureWithTexCoords(
            new Rect(row.x, row.y + (row.height - size) * 0.5f, size, size), tex, uv);
        GUI.color = prev;
    }
}
