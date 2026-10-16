using Draftmaster.Sim;
using NUnit.Framework;

// The running position and the classification at the flag have to count laps the same way.
//
// Watkins Glen's line is 250 m down the pit straight, so the pit boxes the field starts from sit either side
// of it. Every car used to start on lap 0, so the ones short of the line went a lap up the moment they crossed
// it: the position readout ranked them all ahead of the cars that started past it, while the race director
// (which baselined each car on its own lap at the green) classified the real order. Seen as P11 on track,
// P5 in the results.
public class RaceLapsTests
{
    const float Lap = 3900f;
    const float LineAt = 250f;   // Watkins Glen's startFinishDistance

    static float FromLine(float trackDistance) => UnityEngine.Mathf.Repeat(trackDistance - LineAt, Lap);

    // Plays a car through the tracker's lap counter: seeded on first sight, +1 on each forward wrap.
    static int LapAfter(params float[] trackDistances)
    {
        int lap = 0;
        float prev = 0f;
        for (int i = 0; i < trackDistances.Length; i++)
        {
            float d = FromLine(trackDistances[i]);
            if (i == 0) lap = RaceLaps.FirstLap(d, Lap);
            else if (prev - d > Lap * 0.5f) lap++;
            else if (d - prev > Lap * 0.5f) lap--;
            prev = d;
        }
        return lap;
    }

    [Test]
    public void ACarPastTheLineStartsOnLapZero()
    {
        Assert.AreEqual(0, RaceLaps.FirstLap(10f, Lap));
        Assert.AreEqual(0, RaceLaps.FirstLap(Lap * 0.5f, Lap));
    }

    [Test]
    public void ACarShortOfTheLineHasNotStartedTheLap()
    {
        Assert.AreEqual(-1, RaceLaps.FirstLap(Lap - 10f, Lap));
    }

    [Test]
    public void PitBoxesEitherSideOfTheLineAreOnTheSameLapAtTheGreen()
    {
        // One box 100 m short of the line, one 200 m past it. Both run out of the pits and round the formation
        // lap, and are queued up 40 m and 60 m short of the line when the green waves.
        int shortBox = LapAfter(150f, 260f, 1000f, 2000f, 3000f, 3800f, LineAt - 40f);
        int pastBox = LapAfter(450f, 1000f, 2000f, 3000f, 3800f, LineAt - 60f);
        Assert.AreEqual(pastBox, shortBox);
    }

    [Test]
    public void TheStartCrossingIsLapOneWhenTheLeaderIsStillShortOfTheLine()
    {
        int leaderLap = 0;
        int baseLap = RaceLaps.StartBaseline(leaderLap, Lap - 37f, Lap);
        // The leader takes the green 37 m short of the line and crosses it: lap one.
        Assert.AreEqual(1, (leaderLap + 1) - baseLap);
    }

    [Test]
    public void ACarShortOfTheLineGetsNoFreeLapWhenTheLeaderIsAlreadyOverIt()
    {
        // The green waves with the leader 20 m over the line; the car behind is 30 m short of it, a lap lower.
        int leaderLap = 1, behindLap = 0;
        int baseLap = RaceLaps.StartBaseline(leaderLap, 20f, Lap);
        int leaderRacing = leaderLap - baseLap;
        int behindAfterCrossing = (behindLap + 1) - baseLap;
        Assert.AreEqual(leaderRacing, behindAfterCrossing, "both on the same racing lap once the field is over the line");
        Assert.AreEqual(1, leaderRacing);
    }
}
