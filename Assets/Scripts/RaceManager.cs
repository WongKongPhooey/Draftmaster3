using System.Collections;
using System.Collections.Generic;
using PlayFab.ClientModels;
using Unity.Cinemachine;
using UnityEngine;

public class RaceManager : MonoBehaviour
{
	private static CinemachineCamera actionedCamera;

	public static GameObject thePlayer;
    public static TrackInfo currentTrackInfo;
    public int[] straightLength, turnLength, turnAngle, turnPositions, turnStartAngle;

	public static int trackLength;
	public static int totalTurns;
	public float playerLocation;
	private float trackRotation;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start(){
        trackInit();
    }

	void FixedUpdate(){

    	int playerTurn = thePlayer.GetComponent<VehicleLogic>().turn;
	   	playerLocation = thePlayer.GetComponent<VehicleLogic>().locationOnTrack;
       	if(turnPositions[playerTurn] > playerLocation){
			//On a straight
			trackRotation = turnStartAngle[playerTurn];
		} else {
			//Somewhere in a turn
			float percentInTurn = (playerLocation - turnPositions[playerTurn]) / turnLength[playerTurn];
			if(percentInTurn < 1){
				trackRotation = turnStartAngle[playerTurn] + (turnAngle[playerTurn] * percentInTurn);
			} else {
				trackRotation = turnStartAngle[playerTurn] + turnAngle[playerTurn];
			}
		}
	   	CameraManager.setRotation(trackRotation);
    }

    void trackInit(){
        currentTrackInfo = Resources.Load<TrackInfo>("Tracks/Phoenix");

        totalTurns = currentTrackInfo.totalTurns;
		straightLength = new int[totalTurns];
		turnLength = new int[totalTurns];
		turnAngle = new int[totalTurns];
		turnStartAngle = new int[totalTurns];
		turnPositions = new int[totalTurns];
		int cumulativeTurnAngle = 0;
		for(int i=0;i<totalTurns;i++){
			straightLength[i] = currentTrackInfo.straightLengths[i];
			turnLength[i] = currentTrackInfo.turnLengths[i];
			turnAngle[i] = currentTrackInfo.turnAngles[i];
			turnPositions[i] = currentTrackInfo.turnPositions[i];
			turnStartAngle[i] = cumulativeTurnAngle;
			cumulativeTurnAngle += turnAngle[i];
			trackLength += straightLength[i];
			trackLength += turnLength[i];
		}
    }

	public static void setPlayer(GameObject playerVehicle){
		thePlayer = playerVehicle;
		CameraManager.setPlayer(thePlayer);
		actionedCamera = GameObject.Find("FollowCamera").GetComponent<CinemachineCamera>();
		actionedCamera.Follow = thePlayer.transform;
	}
}