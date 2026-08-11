using UnityEngine;

// F6: draws the whole pixel UI kit on screen — plate, buttons, bars, cursor, every icon, and both fonts
// at their authored sizes.
//
// It exists because the kit is easy to build and hard to see: most of it only appears inside a
// conversation or a panel that has been converted, so "nothing looks different" is the expected result
// until each surface is migrated. This shows the whole set in one place, and doubles as a check that the
// theme asset actually loaded at runtime.
//
// Self-installing like the other debug panels — no scene wiring.
public class PixelUIShowcase : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (FindFirstObjectByType<PixelUIShowcase>() != null) return;
        var go = new GameObject("PixelUIShowcase");
        go.AddComponent<PixelUIShowcase>();
        DontDestroyOnLoad(go);
    }

    static readonly string[] kIcons =
    {
        "money", "part", "fuel", "trophy", "star", "quest", "map",
        "speech", "clock", "flag", "tyre", "heart", "wrench-set", "warning"
    };

    [Tooltip("Public so the panel can be opened without a keypress — handy for screenshotting the kit.")]
    public bool open;
    int _selected = 2;

    void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.f6Key.wasPressedThisFrame) open = !open;
    }

    void OnGUI()
    {
        if (!open) return;

        var theme = PixelGUI.Theme;
        const float w = 520f, h = 430f;
        float x = 40f, y = 40f;

        GUI.Box(new Rect(x, y, w, h), GUIContent.none, PixelGUI.Window);

        if (theme == null)
        {
            GUI.Label(new Rect(x + 20f, y + 20f, w - 40f, 60f),
                "PixelUITheme did not load.\nRun Draftmaster > Art > Set Up Pixel UI Kit.",
                PixelGUI.Heading);
            return;
        }

        float cy = y + 16f;
        GUI.Label(new Rect(x + 20f, cy, w - 40f, 24f), "PIXEL UI KIT", PixelGUI.Heading);
        cy += 26f;
        GUI.Label(new Rect(x + 20f, cy, w - 40f, 20f),
                  $"body: {(theme.imguiFont != null ? theme.imguiFont.name : "MISSING")}   " +
                  $"canvas {PixelUITheme.ReferenceWidth}x{PixelUITheme.ReferenceHeight}", PixelGUI.Footer);
        cy += 26f;

        // Selection list — the JRPG cursor idiom the dialogue choices use.
        GUI.Label(new Rect(x + 20f, cy, w - 40f, 20f), "Menu rows", PixelGUI.Heading);
        cy += 22f;
        string[] rows = { "Talk to the crew chief", "Head to the paddock", "Open the mission board" };
        for (int i = 0; i < rows.Length; i++)
        {
            var row = new Rect(x + 20f, cy, w - 40f, 22f);
            bool sel = i == _selected;
            if (row.Contains(Event.current.mousePosition)) { _selected = i; sel = true; }
            if (sel)
            {
                var band = theme.gold; band.a = 0.16f;
                PixelGUI.Fill(row, band);
                PixelGUI.DrawCursor(row);
            }
            GUI.Label(new Rect(row.x + 20f, row.y, row.width - 20f, row.height), rows[i],
                      sel ? PixelGUI.RowSelected : PixelGUI.Row);
            cy += 22f;
        }
        cy += 10f;

        // Icons, at native 16px and at 2x, so the pixel grid is obvious.
        GUI.Label(new Rect(x + 20f, cy, w - 40f, 20f), "Icons  (16px, then 2x)", PixelGUI.Heading);
        cy += 24f;
        float ix = x + 20f;
        foreach (var key in kIcons)
        {
            DrawIcon(theme, key, new Rect(ix, cy, 16f, 16f));
            ix += 20f;
        }
        cy += 24f;
        ix = x + 20f;
        foreach (var key in kIcons)
        {
            DrawIcon(theme, key, new Rect(ix, cy, 32f, 32f));
            ix += 34f;
        }
        cy += 42f;

        // Bars.
        GUI.Label(new Rect(x + 20f, cy, w - 40f, 20f), "Bars", PixelGUI.Heading);
        cy += 22f;
        DrawBar(theme.barTrack, theme.barGold, new Rect(x + 20f, cy, 200f, 12f), 0.72f);
        DrawBar(theme.barTrack, theme.barRed, new Rect(x + 240f, cy, 200f, 12f), 0.28f);
        cy += 24f;

        // Palette swatches, so the colours can be eyeballed against the world art.
        GUI.Label(new Rect(x + 20f, cy, w - 40f, 20f), "Palette", PixelGUI.Heading);
        cy += 22f;
        Color[] swatches = { theme.ink, theme.plateDeep, theme.plate, theme.plateLight,
                             theme.gold, theme.goldShade, theme.text, theme.textDim,
                             theme.danger, theme.confirm, theme.caution, theme.info };
        float sx = x + 20f;
        foreach (var c in swatches)
        {
            PixelGUI.Fill(new Rect(sx, cy, 24f, 18f), c);
            sx += 26f;
        }
        cy += 26f;

        GUI.Label(new Rect(x + 20f, cy, w - 40f, 20f), "F6 closes this panel", PixelGUI.Footer);
    }

    static void DrawIcon(PixelUITheme theme, string key, Rect r)
    {
        var sprite = theme.Icon(key);
        if (sprite == null) return;
        var tex = sprite.texture;
        var tr = sprite.textureRect;
        GUI.DrawTextureWithTexCoords(
            r, tex, new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height));
    }

    static void DrawBar(Sprite track, Sprite fill, Rect r, float amount)
    {
        if (track != null) DrawSprite(track, r);
        if (fill != null)
            DrawSprite(fill, new Rect(r.x + 2f, r.y + 2f, (r.width - 4f) * Mathf.Clamp01(amount), r.height - 4f));
    }

    static void DrawSprite(Sprite sprite, Rect r)
    {
        var tex = sprite.texture;
        var tr = sprite.textureRect;
        GUI.DrawTextureWithTexCoords(
            r, tex, new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height));
    }
}
