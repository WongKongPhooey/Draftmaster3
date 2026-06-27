using UnityEngine;

// Per-car pit history, written by whichever service runs the stop (PitStopController for AI, PlayerPitService
// for the human) and read by the crew-chief data HUD. Kept on the car so it's found by transform lookup.
public class PitHistory : MonoBehaviour
{
    [Tooltip("Lap the car was on when it last entered its pit box. -1 = hasn't pitted yet.")]
    public int lastPitLap = -1;
    [Tooltip("Total stops made.")]
    public int stops = 0;

    public bool HasPitted => stops > 0;

    public void Record(int lap)
    {
        lastPitLap = lap;
        stops++;
    }
}
