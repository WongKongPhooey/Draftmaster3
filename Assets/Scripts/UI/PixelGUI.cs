using System.Collections.Generic;
using UnityEngine;

// The Iron Oval widget set for IMGUI, mirroring IronOvalUI (which does the same job for uGUI Canvases).
//
// Several panels in this project are IMGUI on purpose: the spline scenes use the 3D URP renderer, where
// a Canvas needs wiring and a font asset, while OnGUI just works with no prefab and no scene setup. That
// convenience is why they all ended up looking like the Unity default skin. These styles and helpers give
// them the kit's plate, palette and three type roles without giving up the zero-setup part.
//
// Two things IMGUI cannot do natively that the kit needs, both solved here:
//
//  * Scaling. IMGUI draws in raw screen pixels, and a 9-slice drawn from a 12x12 source keeps 1px borders
//    no matter how big the display is. So every kit sprite is point-upscaled once to the current integer
//    Scale (Up()) and the styles' borders are scaled to match. One source pixel then covers Scale screen
//    pixels, exactly as it does on the 640x360 canvas.
//  * Tiling. GUIStyle backgrounds always stretch, and a stretched 8x8 dither is a flat wash. Tile() draws
//    the repeat tiles with DrawTextureWithTexCoords instead, at their authored pixel size.
//
// Type roles follow Docs/IronOvalKit.md: Silkscreen for headings, labels and buttons (8pt cell), VT323
// for dense readouts (16pt), Pixelify Sans for prose (20pt). Sizes are whole multiples of those cells.
public static class PixelGUI
{
    static PixelUITheme _builtFor;
    static int _builtAtScale;
    static GUIStyle _display;
    static GUIStyle _window, _focusedWindow, _heading, _headingSmall, _row, _rowSelected, _footer,
                    _cursor, _body, _data, _dataDim, _button, _tab, _tabSelected, _label, _labelDim;
    static Texture2D _flat;
    static readonly Dictionary<SpriteScale, Texture2D> _upscaled = new Dictionary<SpriteScale, Texture2D>();
    static readonly Dictionary<Color, Texture2D> _solids = new Dictionary<Color, Texture2D>();
    static readonly HashSet<Sprite> _warnedUnreadable = new HashSet<Sprite>();

    struct SpriteScale : System.IEquatable<SpriteScale>
    {
        public Sprite sprite;
        public int scale;
        public bool Equals(SpriteScale other) => sprite == other.sprite && scale == other.scale;
        public override bool Equals(object o) => o is SpriteScale s && Equals(s);
        public override int GetHashCode() => (sprite == null ? 0 : sprite.GetHashCode()) * 397 ^ scale;
    }

    public static PixelUITheme Theme => PixelUITheme.Instance;

    // The 640x360 grid the whole UI is authored on. The Canvas half of it says so itself — every kit canvas
    // carries a CanvasScaler with this reference resolution, matching on height — and the IMGUI half has to
    // agree or the two drift apart on the way up.
    public const float DesignHeight = 360f;

    // IMGUI draws in raw screen pixels, unlike the Canvas UI which is authored on the grid above and scaled
    // up. Left unscaled, a 16px row that reads fine on a 720p monitor is tiny on a 4K one. This steps the
    // whole panel up in whole numbers, so glyphs stay on the pixel grid rather than being resampled to a
    // fractional size.
    //
    // Divided by the DESIGN HEIGHT, not by some number of its own. This used to read Screen.height / 540,
    // which is a grid one and a half times the authored one, so every IMGUI panel came out two thirds the
    // size of the Canvas UI around it — and the gap widened with the display, because the two rounded to
    // whole numbers at different rates: at 1080p the canvas ran at 3x against IMGUI's 2x, at 1440p 4x
    // against 2x, at 4K 6x against 4x. The pause menu on a big monitor was the visible end of that.
    public static int Scale
    {
        get
        {
            var t = Theme;
            if (t != null && t.imguiScaleOverride > 0) return t.imguiScaleOverride;
            return Mathf.Max(1, Mathf.FloorToInt(Screen.height / DesignHeight));
        }
    }

    // Scales a pixel measurement authored at 1x.
    public static float Px(float baseline) => baseline * Scale;

    // Rounds a measurement UP to a whole UI pixel. Sizes taken from measured text come back fractional
    // (a two-letter Silkscreen label is 36.01 wide at 3x, not 36), and a plate sized off one lands its
    // frame between screen pixels, where the border smears and the content inside it looks like it has
    // burst the edge. Anything that becomes a box's width or height goes through this first.
    public static float SnapUp(float pixels)
    {
        int s = Scale;
        return Mathf.Ceil(pixels / s) * s;
    }

    // One line of label/heading text at the current face and scale, plus its leading — the height a row
    // of text actually needs. Panels lay their rows out with this rather than a literal, so swapping the
    // theme onto a face drawn on a bigger cell (Silkscreen 8 -> VT323 16) pushes rows apart instead of
    // drawing them over each other.
    public static float LineH { get { Ensure(); return _label.fontSize + Px(2f); } }

    // The same for the fixed-advance data face, which is what rows, columns and readouts are set in.
    public static float DataLineH { get { Ensure(); return _data.fontSize + Px(2f); } }

    // ---- palette ------------------------------------------------------------------------------------
    // Shortcuts, so a panel does not have to null-check the theme on every colour it draws.
    public static Color Text => Theme != null ? Theme.text : Color.white;
    public static Color TextDim => Theme != null ? Theme.textDim : new Color(0.73f, 0.68f, 0.60f);
    public static Color TextDisabled => Theme != null ? Theme.textDisabled : new Color(0.43f, 0.46f, 0.56f);
    public static Color Gold => Theme != null ? Theme.gold : new Color(0.91f, 0.69f, 0.24f);
    public static Color Danger => Theme != null ? Theme.danger : new Color(0.77f, 0.27f, 0.18f);
    public static Color Info => Theme != null ? Theme.info : new Color(0.35f, 0.60f, 0.84f);
    public static Color Confirm => Theme != null ? Theme.confirm : new Color(0.44f, 0.66f, 0.35f);
    public static Color Plate => Theme != null ? Theme.plate : new Color(0.09f, 0.13f, 0.21f);
    public static Color PlateDeep => Theme != null ? Theme.plateDeep : new Color(0.07f, 0.08f, 0.11f);
    public static Color PlateLight => Theme != null ? Theme.plateLight : new Color(0.17f, 0.19f, 0.27f);
    public static Color Ink => Theme != null ? Theme.ink : new Color(0.02f, 0.02f, 0.04f);
    public static Color ScreenBase => Theme != null ? Theme.screenBase : new Color(0.04f, 0.04f, 0.06f);

    // ---- styles -------------------------------------------------------------------------------------
    // The 9-sliced window plate, for GUI.Box and GUILayout areas. Panel() is the richer version: it adds
    // the dithered interior, which a GUIStyle background cannot tile.
    public static GUIStyle Window { get { Ensure(); return _window; } }
    // Silkscreen, gold, 16pt cell — section headers.
    public static GUIStyle Heading { get { Ensure(); return _heading; } }
    // Silkscreen at its native 8pt — the small all-caps label over a readout.
    public static GUIStyle HeadingSmall { get { Ensure(); return _headingSmall; } }
    // VT323 list row, dim until selected.
    public static GUIStyle Row { get { Ensure(); return _row; } }
    public static GUIStyle RowSelected { get { Ensure(); return _rowSelected; } }
    public static GUIStyle Footer { get { Ensure(); return _footer; } }
    public static GUIStyle Cursor { get { Ensure(); return _cursor; } }
    // Pixelify Sans prose, for dialogue and quest copy.
    public static GUIStyle Body { get { Ensure(); return _body; } }
    // VT323 columns: timing, running order, telemetry.
    public static GUIStyle Data { get { Ensure(); return _data; } }
    // Big numerals: the HUD's position and speed. Pixelify at 40 — on its 20px grid, so it stays hard.
    public static GUIStyle Display { get { Ensure(); return _display; } }
    public static GUIStyle DataDim { get { Ensure(); return _dataDim; } }
    // Silkscreen caption that is not a row in a list.
    public static GUIStyle Label { get { Ensure(); return _label; } }
    public static GUIStyle LabelDim { get { Ensure(); return _labelDim; } }
    // The red confirm plate and the flat nav tab.
    public static GUIStyle ButtonStyle { get { Ensure(); return _button; } }
    public static GUIStyle TabStyle { get { Ensure(); return _tab; } }
    public static GUIStyle TabSelectedStyle { get { Ensure(); return _tabSelected; } }

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
        _focusedWindow = null;

        // Faces fall back to the one guaranteed IMGUI font, so a half-configured theme still reads.
        Font display = t != null && t.imguiDisplayFont != null ? t.imguiDisplayFont : (t != null ? t.imguiFont : null);
        Font data = t != null ? t.imguiFont : null;
        Font prose = t != null && t.imguiBodyFont != null ? t.imguiBodyFont : data;

        // Sizes follow the FACE, not the role — a bitmap face resampled off its own cell loses its
        // stems. Silkscreen is drawn on an 8pt cell, VT323 on 16, Pixelify Sans on 20, so each style
        // asks its face which cell it wants. With the theme on one typeface throughout (VT323) this
        // collapses to 16 for text and 32 for headings.
        int labelPt = CellOf(display, 8) * scale;
        int headingPt = CellOf(display, 8) * 2 * scale;
        int vt = 16 * scale;
        int prosePt = CellOf(prose, 20) * scale;

        _window = new GUIStyle();
        var frame = t != null ? t.frameCream : null;
        if (frame != null)
        {
            _window.normal.background = Up(frame);
            // Must match the sprite's authored 9-slice border (4px), scaled with the art.
            _window.border = new RectOffset(4 * scale, 4 * scale, 4 * scale, 4 * scale);
        }
        _window.padding = new RectOffset(12 * scale, 12 * scale, 10 * scale, 10 * scale);

        _heading = Style(display, headingPt, Gold, TextAnchor.UpperLeft, wordWrap: true, rich: true);
        _headingSmall = Style(display, labelPt, Gold, TextAnchor.UpperLeft, rich: true);
        _label = Style(display, labelPt, Text, TextAnchor.MiddleLeft, rich: true);
        _labelDim = Style(display, labelPt, TextDim, TextAnchor.MiddleLeft, rich: true);

        _row = Style(data, vt, TextDim, TextAnchor.MiddleLeft, rich: true);
        _row.hover.textColor = Text;
        _rowSelected = new GUIStyle(_row);
        _rowSelected.normal.textColor = Text;
        _rowSelected.hover.textColor = Text;

        _data = Style(data, vt, Text, TextAnchor.MiddleLeft, rich: true);
        _dataDim = Style(data, vt, TextDim, TextAnchor.MiddleLeft, rich: true);
        _body = Style(prose, prosePt, Text, TextAnchor.UpperLeft, wordWrap: true, rich: true);
        _display = Style(prose, prosePt * 2, Text, TextAnchor.UpperLeft);
        _footer = Style(display, labelPt, TextDisabled, TextAnchor.MiddleLeft);
        _cursor = Style(display, labelPt, Gold, TextAnchor.MiddleCenter);

        // The confirm plate. Its highlight and shade are baked into the sprite, so there is no second
        // drawing for hover or press — the kit asks for the pressed state to come from a 2px offset,
        // which Button() below applies to the label.
        _button = Style(display, labelPt, Text, TextAnchor.MiddleCenter);
        var plate = t != null ? t.buttonRed : null;
        if (plate != null)
        {
            var tex = Up(plate);
            _button.normal.background = tex;
            _button.hover.background = tex;
            _button.active.background = tex;
            _button.focused.background = tex;
            _button.onNormal.background = tex;
            _button.border = new RectOffset(3 * scale, 3 * scale, 3 * scale, 3 * scale);
        }
        _button.padding = new RectOffset(6 * scale, 6 * scale, 4 * scale, 4 * scale);

        // Flat nav tab: solid plate, no 9-slice — Tab() draws the plate and its hard shadow itself.
        _tab = Style(display, labelPt, TextDim, TextAnchor.MiddleCenter);
        _tabSelected = Style(display, labelPt, Text, TextAnchor.MiddleCenter);
    }

    // The pixel cell a bitmap face is drawn for, exposed so a panel that wants a *smaller* face than the
    // kit's default — the phone, which is a screen drawn inside the screen — can still land on a whole
    // multiple of the cell rather than resampling the face down to a fractional size.
    public static int FontCell(Font font, int fallback = 16) => CellOf(font, fallback);

    // The pixel cell a bitmap face is drawn for. Sizes must be whole multiples of it or the glyphs
    // are resampled and go soft — the one thing this whole UI exists to avoid.
    static int CellOf(Font font, int fallback)
    {
        if (font == null) return fallback;
        string n = font.name;
        if (n.IndexOf("VT323", System.StringComparison.OrdinalIgnoreCase) >= 0) return 16;
        if (n.IndexOf("Silkscreen", System.StringComparison.OrdinalIgnoreCase) >= 0) return 8;
        if (n.IndexOf("Pixelify", System.StringComparison.OrdinalIgnoreCase) >= 0) return 20;
        if (n.IndexOf("fixedsys", System.StringComparison.OrdinalIgnoreCase) >= 0) return 16;
        return fallback;
    }

    static GUIStyle Style(Font font, int size, Color colour, TextAnchor anchor,
                          bool wordWrap = false, bool rich = false)
    {
        var style = new GUIStyle
        {
            font = font,
            fontSize = size,
            alignment = anchor,
            wordWrap = wordWrap,
            richText = rich,
        };
        style.normal.textColor = colour;
        style.hover.textColor = colour;
        style.active.textColor = colour;
        style.focused.textColor = colour;
        style.onNormal.textColor = colour;
        style.onHover.textColor = colour;
        style.onActive.textColor = colour;
        return style;
    }

    // ---- art ----------------------------------------------------------------------------------------

    // Point-upscales a kit sprite to the current Scale, so a 12x12 frame drawn on a 1080p screen has 2px
    // borders rather than 1px hairlines. Cached per sprite and scale — the copies are tiny (a 16x16 tile
    // at 4x is 64x64) and are rebuilt only when the display scale changes.
    public static Texture2D Up(Sprite sprite)
    {
        if (sprite == null) return null;
        int scale = Scale;
        var key = new SpriteScale { sprite = sprite, scale = scale };
        if (_upscaled.TryGetValue(key, out var cached) && cached != null) return cached;

        var src = sprite.texture;
        if (src == null) return null;
        var r = sprite.textureRect;
        int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
        if (w <= 0 || h <= 0) return null;

        Color[] pixels;
        try
        {
            pixels = src.GetPixels(Mathf.RoundToInt(r.x), Mathf.RoundToInt(r.y), w, h);
        }
        catch (System.Exception)
        {
            // Not readable: GetPixels needs Read/Write on the importer, which IronOvalKitSetup sets for the
            // kit but a texture swapped in by hand may not have. Falling back to the unscaled source keeps
            // the panel drawing (thin borders, but drawing) instead of throwing every frame out of OnGUI.
            if (_warnedUnreadable.Add(sprite))
                Debug.LogWarning($"[PixelGUI] '{src.name}' is not Read/Write enabled, so it cannot be scaled " +
                                 "to the display. Run Draftmaster > Art > Set Up Iron Oval Kit.");
            _upscaled[key] = src;
            return src;
        }

        var tex = new Texture2D(w * scale, h * scale, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = src.wrapMode,
            hideFlags = HideFlags.HideAndDontSave,
        };
        var scaled = new Color[w * scale * h * scale];
        int sw = w * scale;
        for (int y = 0; y < h * scale; y++)
            for (int x = 0; x < sw; x++)
                scaled[y * sw + x] = pixels[(y / scale) * w + (x / scale)];
        tex.SetPixels(scaled);
        tex.Apply();

        _upscaled[key] = tex;
        return tex;
    }

    // A 1x1 texture in a flat colour, for the IMGUI styles that need a background image rather than a
    // draw call — sliders, scroll bars, selection bands. Cached per colour.
    public static Texture2D Solid(Color colour)
    {
        if (_solids.TryGetValue(colour, out var cached) && cached != null) return cached;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            hideFlags = HideFlags.HideAndDontSave,
        };
        tex.SetPixel(0, 0, colour);
        tex.Apply();
        _solids[colour] = tex;
        return tex;
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

    // Repeats a kit tile at its authored pixel size (times Scale) across the rect. Never stretched: the
    // dither, the hatch and the scanline all depend on their pixel period.
    public static void Tile(Rect r, Sprite sprite, Color? tint = null)
    {
        var tex = Up(sprite);
        if (tex == null)
        {
            Fill(r, tint ?? Plate);
            return;
        }
        var prev = GUI.color;
        if (tint.HasValue) GUI.color = tint.Value;
        GUI.DrawTextureWithTexCoords(r, tex, new Rect(0f, 0f, r.width / tex.width, r.height / tex.height));
        GUI.color = prev;
    }

    // One kit sprite drawn once, filling the rect. For the cell sheet and the cursor, which are sub-rects
    // of a larger texture.
    public static void DrawSprite(Rect r, Sprite sprite, Color? tint = null)
    {
        var tex = Up(sprite);
        if (tex == null) return;
        var prev = GUI.color;
        if (tint.HasValue) GUI.color = tint.Value;
        GUI.DrawTexture(r, tex);
        GUI.color = prev;
    }

    // ---- surfaces -----------------------------------------------------------------------------------

    // A window: 9-sliced frame with the dithered interior tiled inside it. `focused` swaps the cream
    // frame for the gold one, which is how the kit shows focus.
    public static void Panel(Rect r, bool focused = false)
    {
        var t = Theme;
        int scale = Scale;
        var frame = t == null ? null : (focused ? t.frameGold : t.frameCream);
        if (frame == null)
        {
            Fill(r, Plate);
            return;
        }

        // The frame's own centre is the plate colour, so it is drawn first and the dither goes over the
        // area inside the border. Both are opaque, so there is no double-darkening.
        var style = focused ? FocusedWindow() : Window;
        // GUIStyle.Draw throws on anything but a repaint, and an exception here aborts the rest of OnGUI -
        // which means the panel paints but no control below it is ever created on the mouse events, so every
        // button on the screen is dead. Repaint-only, and the frame is the only thing that needs the guard:
        // GUI.DrawTexture (Fill/Tile) already no-ops off-repaint.
        if (Event.current == null || Event.current.type == EventType.Repaint)
            style.Draw(r, GUIContent.none, false, false, false, false);

        float inset = 4f * scale;
        var inner = new Rect(r.x + inset, r.y + inset, r.width - inset * 2f, r.height - inset * 2f);
        if (inner.width > 0f && inner.height > 0f && t.panelFill != null) Tile(inner, t.panelFill);
    }

    // The key that closes this panel, on a tab poking out of its top-left corner like the divider in a
    // filofax. Every in-race panel is toggled by a function key and none of them said which, so a player
    // who opened one had to guess their way back out of it.
    //
    // Out of the TOP-LEFT rather than the top-right: the right-hand corner is where a panel's own readout
    // usually ends up (the tyre board's temperatures, the timing plate's best lap), and the left is dead
    // space under every heading in the kit. The tab overlaps the frame by a pixel so it reads as part of
    // the panel rather than a label floating beside it, and it tucks inside the panel when there is no
    // room above — a panel at the top of the screen would otherwise hang its tab off the edge.
    //
    // Pass the key the way the player would say it: "F6", "ESC", "C".
    public static void KeyTab(Rect panel, string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        Ensure();

        var content = new GUIContent(key.ToUpperInvariant());
        float w = Mathf.Ceil(_labelDim.CalcSize(content).x) + Px(8f);
        float h = _labelDim.fontSize + Px(4f);
        float x = panel.x + Px(6f);
        float y = panel.y - h + Px(1f);          // overlapping the frame's top edge by one scaled pixel

        // No room above: sit the tab inside the panel's top-left instead of off the top of the screen.
        if (y < 0f) y = panel.y + Px(1f);

        var r = new Rect(Mathf.Round(x), Mathf.Round(y), Mathf.Round(w), Mathf.Round(h));
        float b = Px(1f);
        Fill(new Rect(r.x - b, r.y - b, r.width + b * 2f, r.height + b * 2f), Ink);
        Fill(r, PlateLight);

        var prevAlign = _labelDim.alignment;
        var prevColour = _labelDim.normal.textColor;
        _labelDim.alignment = TextAnchor.MiddleCenter;
        _labelDim.normal.textColor = Gold;
        GUI.Label(r, content, _labelDim);
        _labelDim.normal.textColor = prevColour;
        _labelDim.alignment = prevAlign;
    }

    // A prompt for the button that does the thing in front of you, sat at the bottom of the screen: the
    // keycap, then the line. Same furniture as every other panel in the kit — gold frame, dithered plate —
    // because a prompt drawn in a plainer style than the HUD around it reads as debug text rather than as
    // part of the game.
    //
    // Sized to the text and drawn at the kit's own scale, so it grows with the rest of the UI instead of
    // staying a small grey strip while everything else is twice the size.
    //
    // Returns the rect it drew into, for anything that wants to stack something above it.
    public static Rect Prompt(string key, string text, float bottomMargin = 12f)
    {
        if (string.IsNullOrEmpty(text)) return default;
        Ensure();

        var keyContent = new GUIContent((key ?? "").ToUpperInvariant());
        var body = Body;

        float capW = string.IsNullOrEmpty(key) ? 0f : Mathf.Ceil(_label.CalcSize(keyContent).x) + Px(10f);
        float capH = _label.fontSize + Px(6f);
        float textW = Mathf.Ceil(body.CalcSize(new GUIContent(text)).x);
        float rowH = Mathf.Max(capH, body.fontSize + Px(4f));
        float gap = string.IsNullOrEmpty(key) ? 0f : Px(8f);

        float inset = Px(8f);
        float w = Mathf.Min(capW + gap + textW + inset * 2f + Px(8f), Screen.width - Px(16f));
        float h = rowH + inset * 2f;
        var box = new Rect(Mathf.Round((Screen.width - w) * 0.5f),
                           Mathf.Round(Screen.height - h - Px(bottomMargin)), w, h);

        Panel(box, focused: true);
        var c = PanelContent(box, 4f);

        float x = c.x;
        if (capW > 0f)
        {
            var cap = new Rect(Mathf.Round(x), Mathf.Round(c.y + (c.height - capH) * 0.5f), capW, capH);
            float b = Px(1f);
            Fill(new Rect(cap.x - b, cap.y - b, cap.width + b * 2f, cap.height + b * 2f), Ink);
            Fill(cap, PlateLight);

            var prevAlign = _label.alignment;
            var prevColour = _label.normal.textColor;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.normal.textColor = Gold;
            GUI.Label(cap, keyContent, _label);
            _label.normal.textColor = prevColour;
            _label.alignment = prevAlign;

            x += capW + gap;
        }

        var line = new Rect(x, c.y, c.xMax - x, c.height);
        var wasAlign = body.alignment;
        body.alignment = TextAnchor.MiddleLeft;
        GUI.Label(line, text, body);
        body.alignment = wasAlign;

        return box;
    }

    // Inner rect of a Panel — where its content goes, one frame border plus the kit's 12px margin in.
    public static Rect PanelContent(Rect r, float margin = 12f)
    {
        float inset = Px(4f) + Px(margin);
        return new Rect(r.x + inset, r.y + inset, r.width - inset * 2f, r.height - inset * 2f);
    }

    static GUIStyle FocusedWindow()
    {
        Ensure();
        var t = Theme;
        if (_focusedWindow == null)
        {
            _focusedWindow = new GUIStyle(_window);
            if (t != null && t.frameGold != null) _focusedWindow.normal.background = Up(t.frameGold);
        }
        return _focusedWindow;
    }

    // The modal scrim: the deep dither over the whole screen, so a frozen race reads as background. The
    // kit asks for 85% rather than a black wash, which keeps the scene legible underneath.
    public static void Scrim(float alpha = 0.85f)
    {
        var t = Theme;
        var r = new Rect(0f, 0f, Screen.width, Screen.height);
        if (t == null || t.panelFillDeep == null)
        {
            Fill(r, new Color(0f, 0f, 0f, alpha));
            return;
        }
        Tile(r, t.panelFillDeep, new Color(1f, 1f, 1f, alpha));
    }

    // The kerb band, for results headers and screen dividers. UI furniture only — not the track.
    public static void Kerb(Rect r)
    {
        var t = Theme;
        if (t == null || t.kerbStripe == null) { Fill(r, Danger); return; }
        Tile(r, t.kerbStripe);
    }

    // A 1px rule at the current scale.
    public static void Rule(float x, float y, float width, Color? colour = null) =>
        Fill(new Rect(x, y, width, Px(1f)), colour ?? PlateLight);

    // A hollow rectangle, one scaled pixel thick. Used to outline a control — a typed-into field, a
    // selected cell — so it reads as an edge rather than as text sitting on the plate.
    public static void Frame(Rect r, Color? colour = null, float thickness = 1f)
    {
        var c = colour ?? PlateLight;
        float t = Px(thickness);
        Fill(new Rect(r.x, r.y, r.width, t), c);                 // top
        Fill(new Rect(r.x, r.yMax - t, r.width, t), c);          // bottom
        Fill(new Rect(r.x, r.y, t, r.height), c);                // left
        Fill(new Rect(r.xMax - t, r.y, t, r.height), c);         // right
    }

    // Hatched placeholder for art that is not drawn yet — reads as pending rather than broken.
    public static void Hatch(Rect r)
    {
        var t = Theme;
        if (t == null || t.hatchSlot == null) { Fill(r, PlateLight); return; }
        Tile(r, t.hatchSlot);
    }

    // ---- data ---------------------------------------------------------------------------------------

    // Width of a `max`-cell segmented bar, so a caller can lay one out before drawing it.
    public static float CellsWidth(int max) => Px(max * 10f - 2f);
    public static float CellsHeight => Px(10f);

    // Segmented bar from the stat-cell sheet: `max` cells, the first `value` filled. A tint draws the
    // cell by hand instead of using the sheet's gold one — tinting the drawn cell is a multiply and
    // cannot reach another hue, so the HUD's red tyre and blue draft bars are flat colour, as the kit
    // draws them.
    public static void Cells(Rect r, int value, int max, Color? fillTint = null)
    {
        if (max <= 0) return;

        var t = Theme;
        float gap = Px(2f);
        float pitch = Px(10f);

        // The rect is the room the caller has, not a suggestion. Ten cells at full pitch are CellsWidth(10)
        // across, which is wider than several of the HUD plates they are drawn on — and a row that ignored
        // its rect simply carried on past the panel edge. Closing the pitch up keeps the row inside the box
        // it was given; a row with room to spare is drawn at the authored pitch as before.
        float natural = max * pitch - gap;
        if (r.width > 0f && r.width < natural) pitch = (r.width + gap) / max;

        float cell = Mathf.Max(1f, pitch - gap);
        float h = r.height > 0f ? Mathf.Min(r.height, CellsHeight) : CellsHeight;
        float rim = Px(1f);
        float bottom = Mathf.Round(r.y + h);
        // Rounding a squeezed pitch outwards can put the last cell a pixel past the rect. The rect is the
        // box, so the row stops there.
        float right = r.width > 0f ? Mathf.Round(r.xMax) : float.MaxValue;
        for (int i = 0; i < max; i++)
        {
            // Whole screen pixels. A squeezed pitch is fractional, and a cell straddling half a pixel
            // bleeds colour into the gap beside it — which is what made a part-full row read as one
            // smeared block wider than the boxes it was drawn in.
            float x0 = Mathf.Round(r.x + i * pitch);
            float y0 = Mathf.Round(r.y);
            float x1 = Mathf.Min(Mathf.Round(x0 + cell), right);
            if (x1 <= x0) break;
            var cr = new Rect(x0, y0, x1 - x0, bottom - y0);
            bool on = i < value;
            if (!on)
            {
                if (t != null && t.statCellEmpty != null) DrawSprite(cr, t.statCellEmpty);
                else Fill(cr, PlateLight);
            }
            else if (fillTint.HasValue) TintedCell(cr, fillTint.Value, rim);
            else if (t != null && t.statCellFilled != null) DrawSprite(cr, t.statCellFilled);
            else Fill(cr, Gold);
        }
    }

    // A hand-drawn cell in an arbitrary hue, built the way the sheet builds its gold one: a darker rim
    // one pixel thick with the colour inside it.
    //
    // It used to be a flat fill of the whole cell. That is a pixel bigger in every direction than the
    // empty cells it sits next to — those spend their outer pixel on the rim — so a coloured row looked
    // swollen and its segments ran together where the sheet's cells would have shown a seam.
    static void TintedCell(Rect r, Color colour, float rim)
    {
        var edge = new Color(colour.r * 0.6f, colour.g * 0.6f, colour.b * 0.6f, colour.a);
        Fill(r, edge);
        var inner = new Rect(r.x + rim, r.y + rim, r.width - rim * 2f, r.height - rim * 2f);
        if (inner.width > 0f && inner.height > 0f) Fill(inner, colour);
    }

    // Continuous meter, for values that are not naturally segmented (fuel, a wear percentage). Trough in
    // the inner-shade colour, flat fill, 1px ink outline so it holds an edge over any backdrop.
    public static void Bar(Rect r, float fill01, Color fill)
    {
        // Snapped to whole screen pixels first. A caller sizing a bar off measured text hands in a
        // fractional rect, and the outline then lands on half-lit pixels either side of where it should
        // be — which reads as the colour leaking out past its own frame rather than as a hard edge.
        float x0 = Mathf.Round(r.x), y0 = Mathf.Round(r.y);
        r = new Rect(x0, y0, Mathf.Round(r.xMax) - x0, Mathf.Round(r.yMax) - y0);

        Fill(r, Ink);
        float b = Px(1f);
        var inner = new Rect(r.x + b, r.y + b, r.width - b * 2f, r.height - b * 2f);
        Fill(inner, PlateLight);
        float w = Mathf.Round(inner.width * Mathf.Clamp01(fill01));
        if (w > 0f) Fill(new Rect(inner.x, inner.y, w, inner.height), fill);
    }

    // ---- controls -----------------------------------------------------------------------------------

    // The red confirm plate. Pressed shifts the label 2px down rather than swapping to a second sprite,
    // so there is only ever one drawing of the button to keep in sync.
    public static bool Button(Rect r, string label)
    {
        Ensure();
        bool held = GUI.enabled && Event.current != null && GUIUtility.hotControl != 0 &&
                    r.Contains(Event.current.mousePosition);
        bool clicked = GUI.Button(r, GUIContent.none, _button);
        var labelRect = held ? new Rect(r.x, r.y + Px(2f), r.width, r.height) : r;
        var style = GUI.enabled ? _button : _footer;
        var prevAlign = style.alignment;
        var prevBg = style.normal.background;
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.background = null;    // the plate is already drawn; this pass is the label only
        GUI.Label(labelRect, label, style);
        style.normal.background = prevBg;
        style.alignment = prevAlign;
        return clicked;
    }

    // The flat nav tab: solid plate with a 3px hard drop shadow, alarm red when selected. Used for the
    // DRIVE / TEAM / CHIEF row and anywhere else a mode is picked.
    public static bool Tab(Rect r, string label, bool selected)
    {
        Ensure();
        float shadow = Px(3f);
        Fill(new Rect(r.x + shadow, r.y + shadow, r.width, r.height), Ink);
        Fill(r, selected ? Danger : PlateDeep);
        bool clicked = GUI.Button(r, GUIContent.none, GUIStyle.none);
        GUI.Label(r, label, selected ? _tabSelected : _tab);
        return clicked;
    }

    // Draws the selection cursor sprite at the left of a row.
    public static void DrawCursor(Rect row, float size = 12f)
    {
        var t = Theme;
        var sprite = t == null ? null : (t.cursorArrow != null ? t.cursorArrow : t.cursor);
        if (sprite == null) return;
        // The arrow is 6x8; keep that ratio so it does not smear when a caller asks for a square.
        float h = size, w = size * 0.75f;
        DrawSprite(new Rect(row.x, row.y + (row.height - h) * 0.5f, w, h), sprite, Gold);
    }
}
