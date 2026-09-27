namespace Draftmaster.Controls
{
    // One size of type for everything a phone player reads.
    //
    // On a desktop the screen is big enough that a little drift between surfaces goes unnoticed. On a phone
    // it does not: the objective strip at the top of the screen read right, the quest tracker under it read
    // tiny and the speech bubbles read huge, because each was sized by a different rule — the IMGUI panels
    // on the kit's whole-number scale, the world-space bubble in metres (so it grew and shrank with the
    // camera's zoom), and the tracker's title in the kit's smallest label face.
    //
    // The objective strip is the reference, because it is the one that felt right in the hand: its detail
    // line is the data face at one cell (16 UI px on the 640x360 grid), its title the display face at two
    // cells. Everything else on a handheld is brought to those two sizes. The arithmetic lives here, away
    // from the engine, so the tests can pin it.
    public static class HandheldType
    {
        // The kit's authored grid height, as PixelGUI.DesignHeight.
        public const float DesignHeight = 360f;

        // How tall, in world metres, a line of world-space text has to be to come out `linePx` screen
        // pixels tall through an orthographic camera of half-height `orthoSize` on a screen `screenHeightPx`
        // high. This is what ties a speech bubble to the HUD: the bubble is sized from the camera it is seen
        // through, so zooming indoors or out on the paddock no longer changes how big the words read.
        // Returns 0 when there is nothing to measure against, so the caller can keep its own default.
        public static float WorldLineMetres(float linePx, float screenHeightPx, float orthoSize)
        {
            if (linePx <= 0f || screenHeightPx <= 0f || orthoSize <= 0f) return 0f;
            return linePx / screenHeightPx * 2f * orthoSize;
        }
    }
}
