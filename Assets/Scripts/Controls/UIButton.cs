namespace Draftmaster.Controls
{
    // The game's one button. Every screen's buttons are the same height with the same caption, taken from
    // the pause menu (RacePauseMenu): 20 UI px on the kit's 640x360 grid — 60px at 1080p — with the caption in
    // the display face (Silkscreen) at the Label size, regular weight, no extra tracking.
    //
    // IMGUI panels get the height from PixelGUI.MenuButtonH and the caption from PixelGUI.ButtonStyle /
    // TabStyle; canvases from IronOvalUI.Button / TabButton, or CanvasPx for one authored by hand. On a
    // handheld the IMGUI plate grows to hold the bigger body face (PixelGUI.ButtonH) — this is the desktop size.
    // Draftmaster > UI > Audit Buttons lists anything on a canvas that has drifted off it.
    public static class UIButton
    {
        // Plate height in kit pixels: one Label line (10) plus the plate's 3px border padding top and bottom,
        // plus 2px more above and below so a column of them doesn't read cramped.
        public const int KitPx = 20;

        // What the caption is set in.
        public const TypeRole CaptionRole = TypeRole.Label;

        // The plate's height on a canvas authored at `referenceHeight` (1080 or 360).
        public static float CanvasPx(float referenceHeight) => KitPx * referenceHeight / UIType.KitHeight;

        // The plate's height on a screen `screenHeightPx` tall.
        public static float ScreenPx(float screenHeightPx) => KitPx * screenHeightPx / UIType.KitHeight;

        // The caption's size on a screen `screenHeightPx` tall.
        public static float CaptionScreenPx(float screenHeightPx) =>
            UIType.KitPx(CaptionRole) * screenHeightPx / UIType.KitHeight;
    }
}
