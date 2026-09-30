using System;
using System.Collections.Generic;

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

        // A qualifying lap counts only if one was set. Timing rows use -1 for "no time".
        public static bool HasTime(float bestLap) => bestLap > 0f;

        // Put a qualifying field in grid order (index 0 = pole): timed cars by best lap, untimed cars behind
        // them by laps run, and a player who set no time behind all of them.
        //
        // The player goes last deliberately. Untimed cars used to share one tie on laps run — nearly always
        // nought for everyone — and List.Sort is not stable, so a player who ended qualifying without a lap
        // could come out of the tie on pole and start the race from the front. No time is no earned slot.
        // The sort here is stable (original order breaks ties), so the same session always gives the same grid.
        public static List<T> OrderForGrid<T>(IList<T> cars, Func<T, float> bestLap, Func<T, int> laps,
                                              Func<T, bool> isPlayer)
        {
            var ordered = new List<T>(cars.Count);
            if (cars.Count == 0) return ordered;

            var keyed = new List<(T car, int i)>(cars.Count);
            for (int i = 0; i < cars.Count; i++) keyed.Add((cars[i], i));

            keyed.Sort((a, b) =>
            {
                bool aOut = isPlayer(a.car) && !HasTime(bestLap(a.car));
                bool bOut = isPlayer(b.car) && !HasTime(bestLap(b.car));
                if (aOut != bOut) return aOut ? 1 : -1;

                bool aHas = HasTime(bestLap(a.car)), bHas = HasTime(bestLap(b.car));
                if (aHas != bHas) return aHas ? -1 : 1;
                int c = aHas ? bestLap(a.car).CompareTo(bestLap(b.car)) : laps(b.car).CompareTo(laps(a.car));
                return c != 0 ? c : a.i.CompareTo(b.i);
            });

            for (int i = 0; i < keyed.Count; i++) ordered.Add(keyed[i].car);
            return ordered;
        }
    }
}
