namespace Draftmaster.Controls
{
    // What a piece of text is, which decides how big it is drawn. Every surface picks one of these rather
    // than a number of its own — see UIType.
    public enum TypeRole
    {
        Title,     // the name of a whole screen ("THE ROAD TO DAYTONA")
        Heading,   // a window or section's name, a place name, a big readout like cash
        Body,      // prose: blurbs, descriptions, tutorial text
        Dialogue,  // what somebody says: speech bubbles, conversation windows
        Data,      // rows, columns, numbers, part lists, status lines
        Label,     // buttons, small caps captions, keys, footers — the smallest anything may be
    }

    // The game's one type scale.
    //
    // Sizes are given in UI pixels on the kit's 640x360 grid (PixelGUI.DesignHeight), the same unit the
    // IMGUI panels (PixelGUI) and the 640x360 Iron Oval canvases (IronOvalUI) already lay out in. Those two
    // agreed with each other and with the objective strip that reads right on a phone (HandheldType); what
    // drifted were surfaces authored on a 1920x1080 canvas with sizes picked by eye, which came out at a
    // third of the size of everything else — the satnav's side panel set its part list at 16px on a
    // 1080-tall canvas, where the race panels draw the same kind of row at 48.
    //
    // So a size is asked for by role, and converted to whatever canvas it lands on (CanvasPx), then snapped
    // onto the face's own pixel cell so a bitmap font stays crisp. Label is the floor: no text in the game
    // should come out smaller than it (MinimumScreenPx), and Draftmaster > UI > Audit Text Sizes lists
    // anything that does.
    public static class UIType
    {
        // The kit's authored grid height, as PixelGUI.DesignHeight.
        public const float KitHeight = 360f;

        // The floor, in kit pixels: one cell of the display face (Silkscreen 8). 24px on a 1080p screen.
        public const int MinimumKitPx = 8;

        // A role's size in kit pixels.
        public static int KitPx(TypeRole role)
        {
            switch (role)
            {
                case TypeRole.Title:    return 24;
                case TypeRole.Heading:  return 16;
                case TypeRole.Body:     return 16;
                case TypeRole.Dialogue: return 16;
                case TypeRole.Data:     return 16;
                default:                return MinimumKitPx; // Label
            }
        }

        // A role's size on a canvas authored at `referenceHeight` (1080 for the 1920x1080 canvases, 360 for
        // the kit's own), snapped onto a face drawn on a `faceCell`-pixel cell (Silkscreen 8, VT323 and
        // fixedsys 16, Pixelify Sans 20). Pass 0 for a vector face that can take any size.
        public static int CanvasPx(TypeRole role, float referenceHeight, int faceCell)
        {
            float px = KitPx(role) * referenceHeight / KitHeight;
            float floor = MinimumKitPx * referenceHeight / KitHeight;
            return Snap(px, faceCell, floor);
        }

        // The nearest whole multiple of `cell` to `px`, never less than one cell and never under `floorPx`
        // (rounding Pixelify's 20-cell to the nearest step would otherwise take a 24px label down to 20).
        public static int Snap(float px, int cell, float floorPx = 0f)
        {
            if (cell <= 0)
            {
                int whole = (int)System.MathF.Round(px);
                int min = (int)System.MathF.Ceiling(floorPx);
                return whole > min ? whole : min;
            }
            int steps = (int)System.MathF.Round(px / cell);
            if (steps < 1) steps = 1;
            while (steps * cell < floorPx) steps++;
            return steps * cell;
        }

        // The smallest text may be on a screen `screenHeightPx` tall (its short side).
        public static float MinimumScreenPx(float screenHeightPx) => MinimumKitPx * screenHeightPx / KitHeight;

        // Whether text drawn `canvasPx` tall on a canvas authored at `referenceHeight` clears the floor.
        // Independent of the real screen: a canvas scales its contents with the screen, so it is either
        // big enough everywhere or nowhere.
        public static bool Readable(float canvasPx, float referenceHeight) =>
            canvasPx + 0.001f >= MinimumKitPx * referenceHeight / KitHeight;
    }
}
