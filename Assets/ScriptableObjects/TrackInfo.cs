using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "Racetrack/New Track", order = 1)]
public class TrackInfo : ScriptableObject {

    [Header("Track Info")]
    public string trackName;
    public int trackId, trackLaps, topSpeed;
    
    [Header("Track Measurements")]
    public int totalTurns;
	public int trackWidth;
    public int[] straightLengths, turnLengths, turnAngles, bankingAngles;

    [Header("Racing Line")]
    public bool variableIdealLine;
    public float[] lowestEntry, lowestMidpoint, lowestExit;
    public float[] idealEntry, idealMidpoint, idealExit;
    public float[] highestEntry, highestMidpoint, highestExit;
    public float[] longRunEntry, longRunMidpoint, longRunExit;
}