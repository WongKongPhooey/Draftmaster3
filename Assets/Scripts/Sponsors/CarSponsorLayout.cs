using UnityEngine;

namespace Draftmaster.Sponsors
{
    // Where the sponsor panels sit on a carset's livery, in TEXTURE PIXELS of the 64x32 paint (origin
    // bottom-left, Unity's texture convention). One asset per carset, because a different car shape puts
    // its hood and decklid in different places.
    //
    // The cup26 art runs nose at -X, tail at +X: front bumper x0-6, hood x7-26, greenhouse x27-44,
    // decklid x45-58, rear bumper x59-63. The defaults below are cut from that.
    //
    // Decals are never scaled — a logo is blitted 1:1 and centred in its rect, clipped if it overflows —
    // because resampling pixel art off the 12.8 px/m standard is exactly what PixelArt exists to prevent.
    [CreateAssetMenu(fileName = "CarSponsorLayout", menuName = "Draftmaster/Car Sponsor Layout")]
    public class CarSponsorLayout : ScriptableObject
    {
        [Tooltip("Carset this layout describes, e.g. cup26. Informational — the painter is handed the layout directly.")]
        public string carsetPrefix = "cup26";

        [Tooltip("Livery size in pixels the rects below were authored against. Rects are clamped to the real sprite at bake time.")]
        public Vector2Int spriteSize = new Vector2Int(64, 32);

        [Header("Panels (texture pixels, origin bottom-left)")]
        [Tooltip("Bonnet between the front wheel arches — the money panel.")]
        public RectInt hood = new RectInt(9, 8, 16, 16);
        [Tooltip("Decklid behind the rear window.")]
        public RectInt tail = new RectInt(45, 9, 12, 14);
        [Tooltip("Rear quarter panel on the +Y side of the art.")]
        public RectInt quarterLeft = new RectInt(44, 24, 12, 7);
        [Tooltip("Rear quarter panel on the -Y side of the art.")]
        public RectInt quarterRight = new RectInt(44, 1, 12, 7);

        public RectInt RectFor(SponsorSlot slot) => slot switch
        {
            SponsorSlot.Hood => hood,
            SponsorSlot.Tail => tail,
            SponsorSlot.QuarterLeft => quarterLeft,
            SponsorSlot.QuarterRight => quarterRight,
            _ => new RectInt(0, 0, 0, 0),
        };

        // Bottom-left pixel a decal of (w,h) lands on when centred in a slot. Returned even when the decal
        // is bigger than the panel — the baker clips, so an oversized logo bleeds evenly rather than
        // hanging off one edge.
        public Vector2Int Anchor(SponsorSlot slot, int decalWidth, int decalHeight)
        {
            RectInt r = RectFor(slot);
            return new Vector2Int(
                r.x + Mathf.RoundToInt((r.width - decalWidth) * 0.5f),
                r.y + Mathf.RoundToInt((r.height - decalHeight) * 0.5f));
        }

        // Largest decal this layout can show without clipping — what the art generator draws to.
        public Vector2Int SmallestPanel()
        {
            var min = new Vector2Int(int.MaxValue, int.MaxValue);
            foreach (var slot in SponsorSlots.All)
            {
                RectInt r = RectFor(slot);
                if (r.width <= 0 || r.height <= 0) continue;
                min.x = Mathf.Min(min.x, r.width);
                min.y = Mathf.Min(min.y, r.height);
            }
            if (min.x == int.MaxValue) min = new Vector2Int(12, 6);
            return min;
        }
    }
}
