using System.Collections;
using System.Collections.Generic;
using PlayFab.ClientModels;
using Unity.Cinemachine;
using UnityEngine;

public class RaceManager : MonoBehaviour
{
	private static CinemachineCamera actionedCamera;

	public static GameObject thePlayer;
	public Transform carPrefab;
	public static Transform carPrefabStatic;
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

	void Awake(){
		// Track must be loaded before any Start() runs so on-foot spawn / env objects can read it.
		trackInit();
		carPrefabStatic = carPrefab;
		spawnField();

	}

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start(){
		playerXShift = 0;

		//If no player is set, fallback to the player on foot
		if(thePlayer == null){
			thePlayer = GameObject.FindGameObjectWithTag("PlayerOnFoot");
			setPlayer(thePlayer, 3f);
		}

		spawnField();
    }

	void FixedUpdate(){

		if(thePlayer == null) return;

		if(thePlayer.tag != "Vehicle"){

			//Players on foot stand at the track's infield scene position so the env shows the right area
			if (thePlayer.GetComponent<MovementOnFoot>() != null){
				playerLocation = currentTrackInfo != null ? currentTrackInfo.infieldScenePositionX : 0;
				playerLocationPublic = playerLocation;
				trackRotation = getCumulativeTurnAngle(playerLocation);
				CameraManager.setRotation(trackRotation);
				EnvironmentManager.circuitRotation = trackRotation;
			}
			return;
		}

		motionSpeed = playerSpeedMetres * Time.deltaTime;

	   	playerLocation = thePlayer.GetComponent<VehicleLogic>().locationOnTrack;
		playerLocationPublic = playerLocation;
		trackRotation = getCumulativeTurnAngle(playerLocation);
	   	CameraManager.setRotation(trackRotation);
		EnvironmentManager.circuitRotation = trackRotation;
    }

	void LateUpdate()
	{
		playerXShift = 0;
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

	public static float getCumulativeTurnAngle(float location){
		float angle = 0f;
		if(turnPositions == null) return 0f;
		for(int i=0;i<totalTurns;i++){
			if(location >= turnPositions[i] + turnLength[i]){
				angle += turnAngle[i];
			} else if(location >= turnPositions[i]){
				float pct = (location - turnPositions[i]) / (float)turnLength[i];
				angle += turnAngle[i] * pct;
				break;
			} else {
				break;
			}
		}
		return angle;
	}

	public static GameObject getPlayer(){
		
		//If no player is set, fallback to the player on foot
		if(thePlayer == null){
			thePlayer = GameObject.FindGameObjectWithTag("PlayerOnFoot");
			setPlayer(thePlayer, 3f);
		}
		return thePlayer;
	}

	public static void setPlayer(GameObject playerVehicle, float zoom = 18f){
		thePlayer = playerVehicle;

		// Recentre the incoming player on world x=0 (screen centre). The game keeps the
		// player fixed at centre and scrolls the world past it, so on every player switch
		// we cancel out the player's current X by shifting by its negative. Every vehicle
		// and on-foot character is translated by the same amount so relative spacing is
		// preserved. Recentring is done by moving the cars, NOT by offsetting the
		// environment root, so playerXShift stays 0 (see below).
		float shift = -thePlayer.transform.position.x;

		VehicleLogic[] vehicles = Object.FindObjectsByType<VehicleLogic>(FindObjectsSortMode.None);
		foreach(VehicleLogic v in vehicles){
			v.transform.Translate(shift, 0, 0);
		}
		MovementOnFoot[] footChars = Object.FindObjectsByType<MovementOnFoot>(FindObjectsSortMode.None);
		foreach(MovementOnFoot f in footChars){
			f.transform.Translate(shift, 0, 0);
		}

		// EnvironmentManager reads playerXShift to slide the environment root on X. Since
		// recentring above already moved the cars to x=0, the env root needs no offset, so
		// zero it here. LateUpdate also forces playerXShift back to 0 every frame.
		playerXShift = 0;

		if(thePlayer.TryGetComponent<VehicleLogic>(out VehicleLogic vl)){
			playerLocation = vl.locationOnTrack;
		} else if (thePlayer.GetComponent<MovementOnFoot>() != null && currentTrackInfo != null) {
			playerLocation = currentTrackInfo.infieldScenePositionX;
		} else {
			playerLocation = 0;
		}

		CameraManager.setPlayer(thePlayer, zoom);
		float spawnRotation = getCumulativeTurnAngle(playerLocation);
		Debug.Log("Spawn Rotation: " + spawnRotation);
		CameraManager.setRotation(spawnRotation);
		EnvironmentManager.circuitRotation = spawnRotation;
		DialogueManager.setPlayerCanvas(thePlayer);
		actionedCamera = GameObject.Find("FollowCamera").GetComponent<CinemachineCamera>();
		actionedCamera.Follow = thePlayer.transform;

		EnvironmentObjectV2[] envObjects = Object.FindObjectsByType<EnvironmentObjectV2>(FindObjectsSortMode.None);
		foreach(EnvironmentObjectV2 obj in envObjects){
			obj.resetState();
		}
	}

	public static void spawnField(){
		Object carInst;
		for (int i = 1; i < 43; i++) {
			carInst = Instantiate(carPrefabStatic, new Vector2(53f - (i * 10.24f), -21.5f), Quaternion.identity);
			carInst.name = ("AICar0" + i);
			//carInst.GetComponent<SpriteRenderer>().Sprite = Resources.Load("cup26livery" + i);			
		}
	}
}