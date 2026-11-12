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

    // One of eight pixel arrows, centred in the box: the design's ◀ and its seven turns. Drawn in whole kit
    // pixels rather than rotated, so a diagonal stays as crisp as the four straight ones.
    static void DrawArrow(Rect box, Vector2 dir, float px)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        // GUI space runs y down; the octant is counted anticlockwise from "right" with y up.
        float angle = Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg;
        int octant = ((Mathf.RoundToInt(angle / 45f) % 8) + 8) % 8;
        var c = DriveScoutStyle.Gold;

        bool straight = octant % 2 == 0;
        float gw = straight ? (octant == 0 || octant == 4 ? 4f : 7f) : 5f;
        float gh = straight ? (octant == 0 || octant == 4 ? 7f : 4f) : 5f;
        float ox = box.x + Mathf.Floor((BoxW - gw) * 0.5f) * px;
        float oy = box.y + Mathf.Floor((H - gh) * 0.5f) * px;

        void P(float x, float y, float w, float h) => PixelGUI.Fill(new Rect(ox + x * px, oy + y * px, w * px, h * px), c);

        switch (octant)
        {
            case 4: for (int i = 0; i < 4; i++) P(i, 3 - i, 1, 1 + 2 * i); break;          // left: tip on the left
            case 0: for (int i = 0; i < 4; i++) P(3 - i, 3 - i, 1, 1 + 2 * i); break;      // right
            case 2: for (int j = 0; j < 4; j++) P(3 - j, j, 1 + 2 * j, 1); break;          // up: tip on top
            case 6: for (int j = 0; j < 4; j++) P(3 - j, 3 - j, 1 + 2 * j, 1); break;      // down
            case 3: for (int j = 0; j < 5; j++) P(0, j, 5 - j, 1); break;                  // up-left: corner top-left
            case 1: for (int j = 0; j < 5; j++) P(j, j, 5 - j, 1); break;                  // up-right
            case 5: for (int j = 0; j < 5; j++) P(0, j, j + 1, 1); break;                  // down-left
            case 7: for (int j = 0; j < 5; j++) P(4 - j, j, j + 1, 1); break;              // down-right
        }
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
