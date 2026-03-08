using System.Collections;
using System.Collections.Generic;
using PlayFab.ClientModels;
using Unity.Cinemachine;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class RaceManager : MonoBehaviour
{
	private static CinemachineCamera actionedCamera;

	public static GameObject thePlayer;
	public static float playerSpeed;
	public static float playerSpeedMetres;
	public static float motionSpeed;
	public static float motionOffset;
	public static float frameMotion;
	public static float playerXShift;
    public static TrackInfo currentTrackInfo;
    public static int[] straightLength, turnLength, turnAngle, turnPositions, turnStartAngle;
	public static int trackLength;
	public static int totalTurns;
	public static float playerLocation;
	public float playerLocationPublic;
	private float trackRotation;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start(){
        trackInit();

		playerXShift = 0;

		//If no player is set, fallback to the player on foot
		if(thePlayer == null){
			thePlayer = GameObject.Find("PlayerOnFoot");
			setPlayer(thePlayer, 3f);
		}
    }

	void FixedUpdate(){

		if(thePlayer.tag != "Vehicle"){

			//Players on foot only walk around the pit/infield/crowd fixed at 0x
			if (thePlayer.tag == "PlayerOnFoot"){
				playerLocation = 0;
				trackRotation = 0;
				CameraManager.setRotation(trackRotation);
			}
			return;
		}

		motionSpeed = playerSpeedMetres * Time.deltaTime;

    	int playerTurn = thePlayer.GetComponent<VehicleLogic>().turn;
	   	playerLocation = thePlayer.GetComponent<VehicleLogic>().locationOnTrack;
		playerLocationPublic = playerLocation;
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

	void LateUpdate()
	{
		//Player has now been zeroed
		//Hopefully by this point everything else has acknowledged the shift
		if(thePlayer.transform.position.x == 0){
			playerXShift = 0;
		}
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

	public static GameObject getPlayer(){
		
		//If no player is set, fallback to the player on foot
		if(thePlayer == null){
			thePlayer = GameObject.Find("PlayerOnFoot");
			setPlayer(thePlayer, 3f);
		}
		return thePlayer;
	}

	public static void setPlayer(GameObject playerVehicle, float zoom = 18f){
		thePlayer = playerVehicle;
		playerXShift = -thePlayer.transform.position.x;
		Debug.Log("New X shift: " + playerXShift);
		CameraManager.setPlayer(thePlayer, zoom);
		DialogueManager.setPlayerCanvas(thePlayer);
		actionedCamera = GameObject.Find("FollowCamera").GetComponent<CinemachineCamera>();
		actionedCamera.Follow = thePlayer.transform;
	}
}