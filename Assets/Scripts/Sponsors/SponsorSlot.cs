namespace Draftmaster.Sponsors
{
    // The places on a stock car a sponsor's decal can be sold. The livery art is 64x32 with the nose at
    // -X, so these read front-to-back: hood, then the two rear quarter panels, then the decklid.
    // None means "signed but not on the car" — a deal in that state earns nothing, which is the whole
    // point of making the player place them.
    public enum SponsorSlot
    {
        None = 0,
        Hood = 1,
        Tail = 2,
        QuarterLeft = 3,
        QuarterRight = 4,
    }

    public static class SponsorSlots
    {
        // Every slot a decal can actually go in (excludes None), in the order the UI lists them.
        public static readonly SponsorSlot[] All =
        {
            SponsorSlot.Hood, SponsorSlot.Tail, SponsorSlot.QuarterLeft, SponsorSlot.QuarterRight,
        };

        // What a slot is worth as a fraction of the deal's per-race value. The hood is the money panel —
        // it's what the broadcast camera sees on a straight — and the quarters are the cheap seats. A car
        // with every panel sold earns 2.6x a hood-only car, so filling the car matters without making the
        // small slots pointless.
        public static float PayMultiplier(SponsorSlot slot) => slot switch
        {
            SponsorSlot.Hood => 1.00f,
            SponsorSlot.Tail => 0.70f,
            SponsorSlot.QuarterLeft => 0.45f,
            SponsorSlot.QuarterRight => 0.45f,
            _ => 0f,
        };

        public static string DisplayName(SponsorSlot slot) => slot switch
        {
            SponsorSlot.Hood => "HOOD",
            SponsorSlot.Tail => "TAIL",
            SponsorSlot.QuarterLeft => "LEFT QUARTER",
            SponsorSlot.QuarterRight => "RIGHT QUARTER",
            _ => "NOT ON THE CAR",
        };
    }
}
