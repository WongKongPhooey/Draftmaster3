using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "Racetrack/New Track", order = 1)]
public class TrackInfo : ScriptableObject {

    [Header("Track Info")]
    public string trackName;
    public int trackId, trackLaps, topSpeed;
    
    [Header("Track Measurements")]
    public int totalTurns;
	public int trackLength;
    public int trackWidth;
    public int finishLinePosition;
    public int[] straightPositions, turnPositions, straightLengths, turnLengths, turnAngles, turnLeadIn, turnLeadOut, bankingAngles;

    [Header("Track Speed")]
    public int[] turnMaxSpeeds;
    public AnimationCurve[] lowTurnDecel;
    public AnimationCurve[] highTurnDecel;

    [Header("Racing Line")]
    public bool variableIdealLine;
    public float[] lowestEntry, lowestMidpoint, lowestExit;
    public float[] idealEntry, idealMidpoint, idealExit;
    public float[] highestEntry, highestMidpoint, highestExit;
    public float[] longRunEntry, longRunMidpoint, longRunExit;
}