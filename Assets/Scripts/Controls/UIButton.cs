namespace Draftmaster.Controls
{
    // The game's one button. Every screen's buttons are the same height with the same caption, taken from
    // the pause menu (RacePauseMenu): 26 UI px on the kit's 640x360 grid — 78px at 1080p — with the caption
    // in the data face (Fixedsys) at the Data size, regular weight, no extra tracking. Fixedsys because the
    // captions are Title Case and Silkscreen, the face buttons used to be set in, has no lower case.
    //
    // IMGUI panels get the height from PixelGUI.MenuButtonH and the caption from PixelGUI.ButtonStyle /
    // TabStyle; canvases from IronOvalUI.Button / TabButton, or CanvasPx for one authored by hand.
    // Draftmaster > UI > Audit Buttons lists anything on a canvas that has drifted off it.
    public static class UIButton
    {
        // Plate height in kit pixels: one line of the caption face (Fixedsys 16 draws an 18px line) plus the
        // plate's 2px padding top and bottom, plus 2px more above and below so a column of them doesn't read
        // cramped — what PixelGUI.MenuButtonH measures on a desktop.
        public const int KitPx = 26;

        // What the caption is set in.
        public const TypeRole CaptionRole = TypeRole.Data;

        // The plate's height on a canvas authored at `referenceHeight` (1080 or 360).
        public static float CanvasPx(float referenceHeight) => KitPx * referenceHeight / UIType.KitHeight;

        // The plate's height on a screen `screenHeightPx` tall.
        public static float ScreenPx(float screenHeightPx) => KitPx * screenHeightPx / UIType.KitHeight;

        // The caption's size on a screen `screenHeightPx` tall.
        public static float CaptionScreenPx(float screenHeightPx) =>
            UIType.KitPx(CaptionRole) * screenHeightPx / UIType.KitHeight;
    }
}
