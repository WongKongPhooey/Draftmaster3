namespace Draftmaster.Sim
{
    // Lap counting rules shared by the live running order (RacePositionTracker) and the race finish
    // (RaceDirector). Both have to agree on which lap every car is on, or the position on screen and the
    // classification at the flag are two different races.
    //
    // Distances here are measured from the painted start/finish line, 0..lapLength.
    //
    // Kept free of MonoBehaviour state so the rules can be unit-tested in EditMode.
    public static class RaceLaps
    {
        // The lap a car is on the first time it is measured. A car in the back half of the lap is sat
        // BEHIND the line — the back of a grid, a pit box short of it — and has not started the lap the cars
        // ahead of the line are on; crossing it will bring it level with them. Counting both as lap 0 put
        // every car that started short of the line a whole lap up on the cars that started past it.
        //
        // That is the Watkins Glen case: its line is 250 m down the pit straight, so the pit boxes the field
        // starts from sit either side of it.
        public static int FirstLap(float distFromLine, float lapLength)
            => lapLength > 0f && distFromLine > lapLength * 0.5f ? -1 : 0;

        // The lap the race is counted from, taken off the leader at the green. The crossing the field makes
        // on the start counts as lap one: a leader still approaching the line has that crossing to come, a
        // leader already over it has made it. The same baseline is used for every car, so a car's racing laps
        // only ever differ from another's by the line crossings between them — the same order the running
        // positions show. (Each car taking its own lap as the baseline gave every car short of the line a
        // free lap whenever the leader was already over it at the green.)
        public static int StartBaseline(int leaderLap, float leaderDistFromLine, float lapLength)
            => lapLength > 0f && leaderDistFromLine > lapLength * 0.5f ? leaderLap : leaderLap - 1;
    }
}
