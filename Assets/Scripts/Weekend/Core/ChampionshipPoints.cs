namespace Draftmaster.Weekend
{
    // What a race result is worth in a championship.
    //
    // The scale is the modern stock-car one, because that is the sport this game is: the winner takes 40,
    // second takes 35, and from third it steps down one point at a time to a single point for 36th. The
    // five-point gap between winning and finishing second is the whole reason a driver will wreck somebody
    // on the last lap, so it stays.
    //
    // Pure arithmetic on purpose. The simulated championships (SeasonChampionships) and the player's own
    // result go through the same function, so the table cannot end up scoring the player on a different
    // scale from the field they are being compared to.
    public static class ChampionshipPoints
    {
        // A win.
        public const int Win = 40;
        // Second place. Third is one below this, and it steps down from there.
        public const int Second = 35;
        // The last position that scores more than the minimum.
        public const int LastScoring = 36;
        // Pole position. Small on purpose - qualifying decides where you start, not what you are paid.
        public const int Pole = 1;

        // Points for a classified finishing position, 1-based. A car that never started scores nothing;
        // a car that retired still scores whatever its classified position is worth, the same as the sport.
        public static int ForFinish(int position)
        {
            if (position <= 0) return 0;
            if (position == 1) return Win;

            int points = (LastScoring + 1) - position;      // P2 = 35, P3 = 34 ... P36 = 1
            return points < 1 ? 1 : points;
        }

        // Everything one driver banks from one round.
        public static int ForRound(int finishPosition, bool tookPole)
        {
            int points = ForFinish(finishPosition);
            if (points <= 0) return 0;                       // a non-starter's pole is not a thing
            return points + (tookPole ? Pole : 0);
        }
    }
}
