using UnityEngine;

// The formation lap's out-of-position warning, drawn as the Draftmaster3_UI_Unity design has it (RaceHUD_PC /
// RaceHUD_Mobile, 2026-11-11): a dark red plate centred 12px under the top of the screen with a 2px red border
// just outside it, "OUT OF POSITION" in red Silkscreen capitals tracked 2px across the top, and the instruction
// ("Drop in behind car #21") in cream monogram under it. The box is as wide as the instruction plus 9px each
// side - the design's 172px is that for its sample line - so a longer name never runs out of it.
//
// It flashes: the border and the heading step between full red and a dull red, the kit's 0.45s beat, while the
// instruction stays lit so it can always be read.
//
// PaceLapAssist decides when the player is out of position and calls Draw from its OnGUI; the other formation
// and caution prompts keep their plain line.
public static class OutOfPositionBox
{
    // The design's measurements, in kit pixels.
    const float Top = 12f;          // from the top of the screen to the plate
    const float H = 39f;            // plate height (38.91 in the export)
    const float MinW = 172f;        // the design's plate, for its sample line
    const float PadX = 9f;          // instruction inset either side
    const float Border = 2f;        // the red frame, outside the plate
    const float Tracking = 2f;      // the heading's letter spacing
    const float BlinkSeconds = 0.45f;

    const string Heading = "OUT OF POSITION";

    static Color Hex(uint rgba) => new Color32((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba);

    static readonly Color Plate = Hex(0x2a1210ee);
    static readonly Color Alarm = DriveScoutStyle.Bad;      // #e0604a, the design's red
    static readonly Color AlarmDim = Hex(0x6a2a20ff);       // the off half of the flash: still a frame, just dull

    static GUIStyle _heading, _body;
    static int _builtAt;

    // Draws the box and returns the rect it covers, border included.
    public static Rect Draw(string instruction)
    {
        EnsureStyles();
        instruction ??= "";

        float headingW = PixelGUI.TrackedWidth(Heading, _heading, Tracking);
        float bodyW = _body.CalcSize(new GUIContent(instruction)).x;
        float w = Mathf.Max(PixelGUI.Px(MinW), Mathf.Ceil(Mathf.Max(headingW, bodyW)) + PixelGUI.Px(PadX * 2f));
        w = PixelGUI.Px(Mathf.Ceil(w / PixelGUI.Px(1f)));   // whole kit pixels, so the centring stays on the grid
        float h = PixelGUI.Px(H);

        var plate = new Rect(Mathf.Round((Screen.width - w) * 0.5f), PixelGUI.Px(Top), w, h);
        bool lit = Mathf.Repeat(Time.unscaledTime, BlinkSeconds * 2f) < BlinkSeconds;
        Color frame = lit ? Alarm : AlarmDim;

        float b = PixelGUI.Px(Border);
        var outer = new Rect(plate.x - b, plate.y - b, plate.width + b * 2f, plate.height + b * 2f);
        // The frame as four strips: the plate is see-through, so a solid fill under it would tint it red.
        PixelGUI.Fill(new Rect(outer.x, outer.y, outer.width, b), frame);
        PixelGUI.Fill(new Rect(outer.x, plate.yMax, outer.width, b), frame);
        PixelGUI.Fill(new Rect(outer.x, plate.y, b, plate.height), frame);
        PixelGUI.Fill(new Rect(plate.xMax, plate.y, b, plate.height), frame);
        PixelGUI.Fill(plate, Plate);

        _heading.normal.textColor = frame;
        float hx = Mathf.Round(plate.x + (plate.width - headingW) * 0.5f);
        PixelGUI.LabelTracked(new Rect(hx, plate.y + PixelGUI.Px(4f), headingW, PixelGUI.Px(10f)), Heading, _heading, Tracking);
        GUI.Label(new Rect(plate.x, plate.y + PixelGUI.Px(16f), plate.width, PixelGUI.Px(16f)), instruction, _body);

        return outer;
    }

    static void EnsureStyles()
    {
        if (_heading != null && _builtAt == PixelGUI.Scale) return;
        _builtAt = PixelGUI.Scale;
        _heading = new GUIStyle(PixelGUI.HeadingSmall)
        {
            alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Overflow, richText = false,
            padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0),
        };
        _heading.normal.textColor = Alarm;
        _body = new GUIStyle(PixelGUI.Data)
        {
            alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow, richText = false,
            padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0),
        };
        _body.normal.textColor = DriveScoutStyle.Text;
    }
}
