namespace Draftmaster.Sim
{
    // Where a driver who has not qualified lines up.
    //
    // A career race is the end of a weekend: the grid is the qualifying order, and a driver with no time
    // starts from the back — no lap, no earned slot. That rule is right for a championship and wrong for a
    // SINGLE RACE, which has no qualifying in front of it to have missed. Starting every exhibition dead
    // last out of forty-four would make the one thing the screen exists for — pick a track, pick a car, go
    // racing — the same forty-minute drive through the field every time.
    //
    // So an unqualified single race draws its slot out of the hat instead: pole one time, mid-pack the
    // next. Kept here, free of MonoBehaviour state, so the rule can be unit-tested in EditMode — the race
    // itself can only be judged in Play Mode, which is not always available.
    public static class StartingGrid
    {
        // A draw shared by every caller that does not bring its own. Seeded from the clock, so two races
        // started in the same session do not line up in the same place.
        static System.Random _draw;

        // The slot (0 = pole) for a driver with no qualifying time, in a field of `fieldSize` cars.
        //
        // drawFromTheHat = the session had no qualifying to miss (a single race), so the slot is random.
        // Otherwise it is the back of the field, which is what a missed qualifying session earns.
        // rng is for tests; production passes null and takes the shared draw.
        public static int UnqualifiedSlot(int fieldSize, bool drawFromTheHat, System.Random rng = null)
        {
            if (fieldSize <= 1) return 0;
            if (!drawFromTheHat) return BackOfTheField(fieldSize);

            var draw = rng ?? (_draw ??= new System.Random());
            return draw.Next(0, fieldSize);
        }

        public static int BackOfTheField(int fieldSize) => fieldSize <= 1 ? 0 : fieldSize - 1;
    }
}
