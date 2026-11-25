using UnityEngine;

// The on-foot objective, drawn as the Draftmaster3_UI_Unity design has it (Liaison_PC, 2026-11-11): a boxed
// arrow pointing the way to whatever the player is due at, and beside it a plate with the objective's name
// (gold Silkscreen capitals, tracked 2px) over the distance left ("299 m", cream monogram).
//
// Fixed to a corner rather than chasing the screen edge: bottom-left on a PC, as the design places it; top-
// left beside the pause button on a phone, as DriveScout's mobile frame does, because the phone's bottom-left
// is the walking stick and the phone button. The design's "· garage 4" after the distance is left out.
//
// SpawnIntroUI decides when there is an objective and calls Draw; this only lays it out and paints it, in the
// design's pixels through PixelGUI.Px so it lands on the same grid as the rest of the IMGUI HUD.
public static class ObjectiveWidget
{
    // The design's measurements, in kit pixels.
    const float Margin = 12f;          // from the screen edges (PC)
    const float BoxW = 24f, H = 37f;   // the arrow box; the plate beside it is as tall
    const float Gap = 6f;              // box to plate
    const float PlatePad = 8f;         // text inset inside the plate
    const float MinPlateW = 80f;
    const float Tracking = 2f;         // the title's letter spacing

    static Color Deep => DriveScoutStyle.DialoguePanel;
    static readonly Color DeepTranslucent = new Color32(0x10, 0x1a, 0x30, 0xdd);

    static GUIStyle _title, _distance;

    // Draws the widget pointing along `direction` (GUI space, y down; zero = no arrow) and returns the rect
    // it covers, for tap-testing.
    public static Rect Draw(string title, string distance, Vector2 direction)
    {
        EnsureStyles();
        float px = PixelGUI.Px(1f);
        title = string.IsNullOrEmpty(title) ? "" : title.ToUpperInvariant();

        float titleW = PixelGUI.TrackedWidth(title, _title, Tracking);
        float distW = string.IsNullOrEmpty(distance) ? 0f : _distance.CalcSize(new GUIContent(distance)).x;
        float plateW = Mathf.Max(PixelGUI.Px(MinPlateW), Mathf.Ceil(Mathf.Max(titleW, distW)) + PixelGUI.Px(PlatePad * 2f));
        float h = PixelGUI.Px(H), boxW = PixelGUI.Px(BoxW);

        Vector2 at = Corner(h);
        var box = new Rect(at.x, at.y, boxW, h);
        var plate = new Rect(box.xMax + PixelGUI.Px(Gap), at.y, plateW, h);

        // The arrow box: deep navy, a 2px cream border outside it.
        Border(box, PixelGUI.Px(2f), DriveScoutStyle.PanelFrame);
        PixelGUI.Fill(box, Deep);
        DrawArrow(box, direction, px);

        // The plate: the same navy a little see-through, a 2px slate border.
        Border(plate, PixelGUI.Px(2f), DriveScoutStyle.Edge);
        PixelGUI.Fill(plate, DeepTranslucent);

        float tx = plate.x + PixelGUI.Px(PlatePad);
        PixelGUI.LabelTracked(new Rect(tx, plate.y + PixelGUI.Px(4f), plateW, PixelGUI.Px(10f)), title, _title, Tracking);
        if (!string.IsNullOrEmpty(distance))
            GUI.Label(new Rect(tx, plate.y + PixelGUI.Px(16f), plateW, PixelGUI.Px(16f)), distance, _distance);

        float b = PixelGUI.Px(2f);
        return new Rect(box.x - b, box.y - b, plate.xMax - box.x + b * 2f, h + b * 2f);
    }

    // Top-left of the arrow box. PC: the bottom-left corner. Phone: the top, beside the pause button.
    static Vector2 Corner(float h)
    {
        if (PixelGUI.Handheld)
        {
            var pause = TouchWalkControls.CurrentLayout().pauseButton;
            return new Vector2(Mathf.Round(pause.x + pause.width + PixelGUI.Px(Margin)), Mathf.Round(pause.y));
        }
        float m = PixelGUI.Px(Margin);
        return new Vector2(m, Mathf.Round(Screen.height - m - h));
    }

    // A pixel arrow centred in the box, pointing any way round the full 360 degrees rather than snapped to the
    // eight compass points. It is rasterised in whole kit pixels at the current angle (a shaft and a head, the
    // design's gold) into a small point-filtered texture and drawn at the kit's whole-number scale, so a turned
    // arrow is as crisp as an upright one; the texture is only redrawn when the angle moves a whole degree.
    const int ArrowPx = 21;   // the arrow's square canvas, in kit pixels (fits the 24 px box)
    static Texture2D _arrowTex;
    static Color32[] _arrowPixels;
    static int _arrowDeg = int.MinValue;

    static void DrawArrow(Rect box, Vector2 dir, float px)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        // GUI space runs y down; the angle is counted anticlockwise from "right" with y up.
        int deg = Mathf.RoundToInt(Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg);
        deg = ((deg % 360) + 360) % 360;
        EnsureArrow(deg);

        float ox = box.x + Mathf.Floor((BoxW - ArrowPx) * 0.5f) * px;
        float oy = box.y + Mathf.Floor((H - ArrowPx) * 0.5f) * px;
        GUI.DrawTexture(new Rect(ox, oy, ArrowPx * px, ArrowPx * px), _arrowTex);
    }

    static void EnsureArrow(int deg)
    {
        if (_arrowTex != null && _arrowDeg == deg) return;
        _arrowDeg = deg;
        if (_arrowTex == null)
        {
            _arrowTex = new Texture2D(ArrowPx, ArrowPx, TextureFormat.RGBA32, false)
            {
                name = "ObjectiveArrow",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            _arrowPixels = new Color32[ArrowPx * ArrowPx];
        }

        // Direction with y up, the texture's own orientation (row 0 is the bottom).
        float a = deg * Mathf.Deg2Rad;
        float dx = Mathf.Cos(a), dy = Mathf.Sin(a);
        float c = ArrowPx * 0.5f;
        Color32 gold = DriveScoutStyle.Gold;
        var clear = new Color32(0, 0, 0, 0);

        // In the arrow's own frame (u along it, v across): a head from u = 1 to the tip at u = 9, 9.6 px across
        // its base, on a 3 px shaft back to u = -8.
        const float Tip = 9f, HeadBase = 1f, HeadSlope = 0.6f, Tail = -8f, ShaftHalf = 1.5f;
        for (int row = 0; row < ArrowPx; row++)
        {
            float py = row + 0.5f - c;
            for (int col = 0; col < ArrowPx; col++)
            {
                float pxl = col + 0.5f - c;
                float u = pxl * dx + py * dy;
                float v = Mathf.Abs(-pxl * dy + py * dx);
                bool head = u >= HeadBase && u <= Tip && v <= (Tip - u) * HeadSlope + 0.35f;
                bool shaft = u >= Tail && u < HeadBase && v <= ShaftHalf;
                _arrowPixels[row * ArrowPx + col] = head || shaft ? gold : clear;
            }
        }
        _arrowTex.SetPixels32(_arrowPixels);
        _arrowTex.Apply(false);
    }

    // A border `t` thick drawn just outside `r`.
    static void Border(Rect r, float t, Color c) =>
        PixelGUI.Fill(new Rect(r.x - t, r.y - t, r.width + t * 2f, r.height + t * 2f), c);

    static int _builtAt;

    static void EnsureStyles()
    {
        if (_title != null && _builtAt == PixelGUI.Scale) return;
        _builtAt = PixelGUI.Scale;
        _title = new GUIStyle(PixelGUI.HeadingSmall)
        {
            alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Overflow,
            padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0),
        };
        _title.normal.textColor = DriveScoutStyle.Gold;
        _distance = new GUIStyle(PixelGUI.Data)
        {
            alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Overflow,
            padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0),
        };
        _distance.normal.textColor = DriveScoutStyle.Text;
    }
}
