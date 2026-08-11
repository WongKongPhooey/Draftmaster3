using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Construction helpers for the code-built uGUI panels, so they come out of the same kit as everything
// else instead of each one inventing its own greys and fonts.
//
// The panels in this project are largely assembled in C# at runtime (crew chief HUD, garage/shop info
// panels, team switch, mission board, travel map). That is why the UI drifted: every panel picked its
// own colours. Routing them through here means restyling the game is a change to PixelUITheme, not a
// sweep through a dozen scripts.
public static class PixelUI
{
    public static PixelUITheme Theme => PixelUITheme.Instance;

    // Canvas set up for integer pixel scaling. Anything drawn on it lands on whole screen pixels at
    // 1080p (x3) and 4K (x6), which is what keeps a pixel UI from shimmering as it animates.
    public static Canvas CreateCanvas(string name, int sortOrder = 0)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortOrder;
        ApplyScaler(go.GetComponent<CanvasScaler>());
        return canvas;
    }

    public static void ApplyScaler(CanvasScaler scaler)
    {
        if (scaler == null) return;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(PixelUITheme.ReferenceWidth, PixelUITheme.ReferenceHeight);
        // Match height: the design is 16:9, and letting width drive on an ultrawide would shrink
        // everything. Height-matched keeps the row height constant and just reveals more width.
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        scaler.referencePixelsPerUnit = PixelUITheme.ReferencePixelsPerUnit;
    }

    // A themed window plate. Returns the panel's RectTransform for the caller to fill.
    public static RectTransform Panel(Transform parent, string name, Vector2 size, bool plain = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = size;

        var img = go.GetComponent<Image>();
        var t = Theme;
        img.sprite = t == null ? null : (plain ? t.windowPlain : t.window);
        img.type = Image.Type.Sliced;
        // Sliced sprites disappear when the rect is smaller than their borders unless this is on.
        img.pixelsPerUnitMultiplier = 1f;
        img.color = Color.white;
        if (img.sprite == null) img.color = t == null ? new Color(0.06f, 0.08f, 0.14f, 0.95f) : t.plate;
        return rt;
    }

    public enum TextRole { Body, Small, Heading }

    public static TextMeshProUGUI Label(Transform parent, string name, string content,
                                        TextRole role = TextRole.Body, Color? colour = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var label = go.AddComponent<TextMeshProUGUI>();
        label.text = content;
        ApplyText(label, role, colour);
        return label;
    }

    public static void ApplyText(TMP_Text label, TextRole role = TextRole.Body, Color? colour = null)
    {
        var t = Theme;
        if (t == null) return;

        // Headings use the racing display face; everything readable uses the pixel font. That contrast
        // is the whole idea -- a JRPG window carrying motorsport content.
        var font = role == TextRole.Heading && t.display != null ? t.display : t.body;
        if (font != null)
        {
            label.font = font;
            if (font.material != null) label.fontSharedMaterial = font.material;
        }

        switch (role)
        {
            case TextRole.Small: label.fontSize = t.smallSize; break;
            case TextRole.Heading: label.fontSize = t.headingSize; break;
            default: label.fontSize = t.bodySize; break;
        }
        label.color = colour ?? (role == TextRole.Small ? t.textDim : t.text);
        // Pixel fonts have no hinting to fall back on, so let them render at their authored size.
        label.enableAutoSizing = false;
    }

    public enum ButtonRole { Normal, Confirm, Danger }

    public static Button TextButton(Transform parent, string name, string content,
                                    Vector2 size, ButtonRole role = ButtonRole.Normal)
    {
        var t = Theme;
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        ((RectTransform)go.transform).sizeDelta = size;

        var img = go.GetComponent<Image>();
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f;
        if (t != null)
        {
            img.sprite = role == ButtonRole.Danger ? t.buttonDanger
                       : role == ButtonRole.Confirm ? t.buttonConfirm
                       : t.button;
        }

        var button = go.GetComponent<Button>();
        if (t != null && t.buttonHover != null)
        {
            // Sprite swap rather than colour tint: tinting flat pixel art muddies it, whereas a second
            // drawing keeps the highlight crisp.
            button.transition = Selectable.Transition.SpriteSwap;
            var state = button.spriteState;
            state.highlightedSprite = t.buttonHover;
            state.pressedSprite = t.buttonPressed;
            state.selectedSprite = t.buttonHover;
            button.spriteState = state;
        }

        var label = Label(go.transform, "Label", content);
        var lrt = label.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        label.alignment = TextAlignmentOptions.Center;
        // Dark text on the gold hover state would vanish against the plate, so buttons keep light text
        // and rely on the frame for their state read.
        return button;
    }

    // A 16x16 icon at its native size, or a whole multiple of it.
    public static Image Icon(Transform parent, string key, int scale = 1)
    {
        var go = new GameObject("Icon_" + key, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        var t = Theme;
        if (t != null) img.sprite = t.Icon(key);
        ((RectTransform)go.transform).sizeDelta = new Vector2(16 * scale, 16 * scale);
        return img;
    }

    // Track + fill pair for fuel, tyre wear, progress. Returns the fill Image so the caller can drive
    // its fillAmount.
    public static Image Bar(Transform parent, string name, Vector2 size, Color? fillColour = null)
    {
        var t = Theme;
        var track = new GameObject(name, typeof(RectTransform), typeof(Image));
        track.transform.SetParent(parent, false);
        ((RectTransform)track.transform).sizeDelta = size;
        var trackImg = track.GetComponent<Image>();
        if (t != null) trackImg.sprite = t.barTrack;
        trackImg.type = Image.Type.Sliced;
        trackImg.pixelsPerUnitMultiplier = 1f;

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(track.transform, false);
        var frt = (RectTransform)fill.transform;
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = new Vector2(2f, 2f);
        frt.offsetMax = new Vector2(-2f, -2f);

        var fillImg = fill.GetComponent<Image>();
        if (t != null) fillImg.sprite = t.barGold;
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.pixelsPerUnitMultiplier = 1f;
        if (fillColour.HasValue) fillImg.color = fillColour.Value;
        return fillImg;
    }
}
