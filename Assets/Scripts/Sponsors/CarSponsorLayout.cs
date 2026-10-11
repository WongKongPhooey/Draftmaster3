using UnityEngine;

namespace Draftmaster.Sponsors
{
    // Where the sponsor panels sit on a carset's livery, in TEXTURE PIXELS of the 64x32 paint (origin
    // bottom-left, Unity's texture convention). One asset per carset, because a different car shape puts
    // its hood and decklid in different places.
    //
    // The cup26 art runs nose at -X, tail at +X: hood x5-19, windscreen x20-28, roof number x30-45, rear
    // window x46-57, decklid strip x58-62. The defaults below are cut from that (cup26livery89).
    //
    // Decals are never scaled — a logo is blitted 1:1 and centred in its rect, clipped if it overflows —
    // because resampling pixel art off the 12.8 px/m standard is exactly what PixelArt exists to prevent.
    //
    // A rect may run past the bodywork (or the texture) on purpose: the baker only paints where the livery
    // is opaque, so the car's own silhouette crops whatever hangs off it — a quarter-panel logo can wrap off
    // the rear corner.
    [CreateAssetMenu(fileName = "CarSponsorLayout", menuName = "Draftmaster/Car Sponsor Layout")]
    public class CarSponsorLayout : ScriptableObject
    {
        [Tooltip("Carset this layout describes, e.g. cup26. Informational — the painter is handed the layout directly.")]
        public string carsetPrefix = "cup26";

        [Tooltip("Livery size in pixels the rects below were authored against. Rects are clamped to the real sprite at bake time.")]
        public Vector2Int spriteSize = new Vector2Int(64, 32);

        [Header("Panels (texture pixels, origin bottom-left)")]
        [Tooltip("Bonnet between the front wheel arches — the money panel. Takes the brand's hood.png.")]
        public RectInt hood = new RectInt(5, 4, 14, 24);
        [Tooltip("Decklid strip behind the rear window. Takes small.png, turned to read across the car.")]
        public RectInt tail = new RectInt(58, 8, 4, 16);
        [Tooltip("Rear quarter panel on the +Y side of the art. Takes small.png. May run off the body — the silhouette crops it.")]
        public RectInt quarterLeft = new RectInt(48, 26, 16, 4);
        [Tooltip("Rear quarter panel on the -Y side of the art. Takes small.png. May run off the body — the silhouette crops it.")]
        public RectInt quarterRight = new RectInt(48, 2, 16, 4);

        [Header("Turn applied to the art before painting (as seen in an image viewer)")]
        public DecalTurn hoodTurn = DecalTurn.None;
        public DecalTurn tailTurn = DecalTurn.Clockwise;
        [Tooltip("Far side: half a turn, so the lettering faces the other way to the near side.")]
        public DecalTurn quarterLeftTurn = DecalTurn.Half;
        public DecalTurn quarterRightTurn = DecalTurn.None;

        public RectInt RectFor(SponsorSlot slot) => slot switch
        {
            SponsorSlot.Hood => hood,
            SponsorSlot.Tail => tail,
            SponsorSlot.QuarterLeft => quarterLeft,
            SponsorSlot.QuarterRight => quarterRight,
            _ => new RectInt(0, 0, 0, 0),
        };

        public DecalTurn TurnFor(SponsorSlot slot) => slot switch
        {
            SponsorSlot.Hood => hoodTurn,
            SponsorSlot.Tail => tailTurn,
            SponsorSlot.QuarterLeft => quarterLeftTurn,
            SponsorSlot.QuarterRight => quarterRightTurn,
            _ => DecalTurn.None,
        };

        // The panel's size in the art's own orientation — what a decal for this slot should be drawn at.
        public Vector2Int ArtSize(SponsorSlot slot)
        {
            RectInt r = RectFor(slot);
            return DecalTurns.Size(TurnFor(slot), r.width, r.height);
        }

        // Bottom-left pixel a decal footprint of (w,h) — already turned — lands on when centred in a slot.
        // Returned even when the decal is bigger than the panel — the baker clips, so an oversized logo
        // bleeds evenly rather than hanging off one edge.
        public Vector2Int Anchor(SponsorSlot slot, int decalWidth, int decalHeight)
        {
            RectInt r = RectFor(slot);
            return new Vector2Int(
                r.x + Mathf.RoundToInt((r.width - decalWidth) * 0.5f),
                r.y + Mathf.RoundToInt((r.height - decalHeight) * 0.5f));
        }
    }
}
