using UnityEngine;
using System.Collections;
using Random=UnityEngine.Random;

public class CameraRotate : MonoBehaviour {

	public GameObject TDCamera;
	public GameObject cornerKerb;
	public GameObject apron;
	public GameObject finishLine;
	public GameObject tropicono;
	
	public AudioSource carEngine;
	public AudioSource crowdNoise;
	public static int audioOn;

	public static bool cautionCleared;

	public static int[] straightLength = new int[6];
	public static int[] turnLength = new int[6];
	public static int[] turnAngle = new int[6];
	public static int[] turnDir = new int[6];
	public static float turnSpeed;
	public static int trackLength;
	public static int totalTurns;
	public static int straightcounter;
	public static int cornercounter;
	public static float carSpeedOffset;
	public static int cornerSpeed;
	public static int cornerMidpoint;
	public static float gearedAccel;
	
	public static bool onTurn;
	
	public static int lap;
	public static int cautionLap;
	public static int cautionProb;
	public static int restartLap;
	public static int raceEnd;
	public static int straight;
	public static int turn;
	public static int cameraRotate;
	public static float averageSpeed;
	public static float averageSpeedTotal;
	public static int averageSpeedCount;
	public static float lapRecord;
	public static int lapRecordInt;
	public static int currentLapRecord;
	public static int trackSpeedOffset;
	string circuit;
	float kerbBlur;
	
	void Awake(){
		kerbBlur = 0.5f;
		straightcounter = 0;
		straightLength[0] = PlayerPrefs.GetInt("StraightLength1");
		straightLength[1] = PlayerPrefs.GetInt("StraightLength2");
		straightLength[2] = PlayerPrefs.GetInt("StraightLength3");
		straightLength[3] = PlayerPrefs.GetInt("StraightLength4");
		straightLength[4] = PlayerPrefs.GetInt("StraightLength5");
		straightLength[5] = PlayerPrefs.GetInt("StraightLength6");
		turnLength[0] = PlayerPrefs.GetInt("TurnLength1");
		turnLength[1] = PlayerPrefs.GetInt("TurnLength2");
		turnLength[2] = PlayerPrefs.GetInt("TurnLength3");
		turnLength[3] = PlayerPrefs.GetInt("TurnLength4");
		turnLength[4] = PlayerPrefs.GetInt("TurnLength5");
		turnLength[5] = PlayerPrefs.GetInt("TurnLength6");
		turnAngle[0] = PlayerPrefs.GetInt("TurnAngle1");
		turnAngle[1] = PlayerPrefs.GetInt("TurnAngle2");
		turnAngle[2] = PlayerPrefs.GetInt("TurnAngle3");
		turnAngle[3] = PlayerPrefs.GetInt("TurnAngle4");
		turnAngle[4] = PlayerPrefs.GetInt("TurnAngle5");
		turnAngle[5] = PlayerPrefs.GetInt("TurnAngle6");
		turnDir[0] = 0;
		turnDir[1] = 0;
		turnDir[2] = 0;
		turnDir[3] = 0;
		turnDir[4] = 0;
		turnDir[5] = 0;
		onTurn = false;
		turnSpeed = 0;
		trackLength = 0;
		totalTurns = PlayerPrefs.GetInt("TotalTurns");
		
		gearedAccel = calcCircuitGearing();
		
		cameraRotate = PlayerPrefs.GetInt("CameraRotate");
		
		for(int i=0;i<totalTurns;i++){
			trackLength += straightLength[i];
			//Debug.Log("Track Length: " + trackLength);
			trackLength += (turnLength[i] * turnAngle[i]);
			//Debug.Log("Track Length: " + trackLength);
		}
		straight = totalTurns;
		turn = totalTurns;
		if(cameraRotate == 1){
			TDCamera.transform.Rotate(0,0,turnLength[turn-1]);
		}
		
		lap = 0;
		circuit = PlayerPrefs.GetString("CurrentCircuit");
		lapRecord = 0;
		if(PlayerPrefs.HasKey("FastestLap" + circuit)){
			lapRecord = PlayerPrefs.GetInt("FastestLap" + circuit);
			lapRecord = lapRecord / 1000;
		} else {
			PlayerPrefs.SetInt("FastestLap" + circuit, 0);
		}
		cornercounter = 0;
		carSpeedOffset = 80;
		cornerSpeed = 0;

		trackSpeedOffset = PlayerPrefs.GetInt("SpeedOffset");

		if((ChallengeSelectGUI.challengeMode == true)||(PlayerPrefs.GetInt("ActiveCaution") == 1)){
			lap = PlayerPrefs.GetInt("StartingLap");
			raceEnd = PlayerPrefs.GetInt("RaceLaps");
			RaceHUD.goingGreen = true;
			restartLap = lap + 1;
			PlayerPrefs.SetInt("ActiveCaution",0);
		} else {
			//cautionProb = Random.Range(1,100);
			cautionProb = 1; //NO CAUTIONS
			if((PlayerPrefs.GetInt("RaceLaps") > 4)&&(cautionProb > 1)){
				cautionLap = Random.Range(2,PlayerPrefs.GetInt("RaceLaps")-3); //Random Caution
				//Debug.Log("Caution will be on lap " + cautionLap);
			} else {
				cautionLap = 999;
				//Debug.Log("No Cautions");
			}
		}

		audioOn = PlayerPrefs.GetInt("AudioOn");
		if(audioOn == 1){
			carEngine.volume = 0.25f;
			crowdNoise.volume = 0.05f;
		} else {
			carEngine.volume = 0.0f;
			crowdNoise.volume = 0.0f;
		}
		
	}

	// Update is called once per frame
	void FixedUpdate () {
		
		//finishLine.renderer.enabled = false;
		
		averageSpeedTotal += (Movement.playerSpeed - carSpeedOffset);
		averageSpeedCount++;
		averageSpeed = averageSpeedTotal / averageSpeedCount;
		
		straightcounter++;
		
		if ((straightcounter == PlayerPrefs.GetInt("StartLine"))&&(straight == 1)){
			Scoreboard.updateScoreboard();
			lap++;
			PlayerPrefs.SetInt("TotalLaps",PlayerPrefs.GetInt("TotalLaps") + 1);

			if((lap == cautionLap)&&(cautionCleared == false)){
				PlayerPrefs.SetInt("ActiveCaution",1);
				//Debug.Log("Caution is out - lap " + lap);
				//Scoreboard.checkCautionPositions();
				RaceHUD.caution = true;
				cautionCleared = true;
			}

			if(lap == cautionLap + 1){
				Time.timeScale = 0.0f;
				finishLine.GetComponent<Renderer>().enabled = true;
				carEngine.volume = 0;
				crowdNoise.volume = 0;
				RaceHUD.caution = true;
				PlayerPrefs.SetInt("StartingLap",lap);
				PlayerPrefs.SetInt("CautionHasBeen",1);
			}

			if(lap == restartLap){
				RaceHUD.goingGreen = false;
			}

			if((averageSpeed > lapRecord)&&(lap > 1)){
				lapRecord = averageSpeed;
			}

			averageSpeed = 0;
			averageSpeedCount = 0;
			averageSpeedTotal = 0;
		}

		if ((straightcounter % 100) == 1){
			//Scoreboard.checkPositions();
			Scoreboard.updateScoreboard();
		}

		if(audioOn == 1){
			if(lap == (PlayerPrefs.GetInt("RaceLaps"))){
				if(crowdNoise.volume < 0.3f){
					crowdNoise.volume += 0.005f;
				}
			}
		} else {
			carEngine.volume = 0.0f;
			crowdNoise.volume = 0.0f;
		}

		if(lap == (PlayerPrefs.GetInt("RaceLaps") + 1)){
			lap--;
			Time.timeScale = 0.0f;
			finishLine.GetComponent<Renderer>().enabled = true;
			if(PlayerPrefs.GetString("CurrentCircuit") == "Joliet"){
				int rand = Random.Range(1,100);
				//Lucky day
				if((rand > 26)&&(rand < 28)){
					tropicono.GetComponent<Renderer>().enabled = true;
				}
			}
			carEngine.volume = 0;
			crowdNoise.volume = 0;
			RaceHUD.raceOver = true;
			Scoreboard.checkFinishPositions();
			PlayerPrefs.SetInt("ExpAdded",0);
			if(PlayerPrefs.HasKey("FastestLap" + circuit)){
				currentLapRecord = PlayerPrefs.GetInt("FastestLap" + circuit);
				lapRecord -= trackSpeedOffset;
				Debug.Log(lapRecord);
				lapRecordInt = (int)Mathf.Round(lapRecord * 1000);
				PlayerPrefs.SetInt("FastestLap" + circuit, lapRecordInt);
				Debug.Log("Send to leaderboard - " + lapRecordInt + ": " + circuit);
				PlayFabManager.SendLeaderboard(lapRecordInt, circuit, "FastestLap");
				if(PlayerPrefs.GetString("LiveTimeTrial") == circuit){
					PlayFabManager.CheckLiveTimeTrial();
					//Double checked
					if(PlayerPrefs.GetString("LiveTimeTrial") == circuit){
						PlayFabManager.SendLeaderboard(lapRecordInt, "LiveTimeTrial","");
					}
				}
				
			}
		}

		//Turning
		if(straightcounter > straightLength[straight-1]){
			if(onTurn == false){
				onTurn = true;
				Movement.onTurn = true;
				AIMovement.onTurn = true;
				cornerSpeed = calcCornerSpeed(straight-1);
				cornerMidpoint = (turnLength[straight-1] * turnAngle[straight-1]) / 2;
				//Debug.Log("Corner " + straight + " speed: " + cornerSpeed);
			}
			if(cameraRotate == 1){
				if(turnDir[turn-1] == 1){
					TDCamera.transform.Rotate(0,0,(1.0f/turnAngle[turn-1]));
				} else {
					TDCamera.transform.Rotate(0,0,(-1.0f/turnAngle[turn-1]));
				}
			}
			cornerKerb.GetComponent<Renderer>().enabled = true;
			if(apron != null){
				apron.GetComponent<Renderer>().enabled = true;
			}
			cornerKerb.GetComponent<Renderer>().material.mainTextureOffset = new Vector2(kerbBlur,0);
			cornercounter++;
			
			
			if(lap > 0){
				//Corner Decel
				if(cornercounter < cornerMidpoint){
					if(carSpeedOffset < cornerSpeed){
						carSpeedOffset+=0.010f * cornerSpeed;
					}
				} else {
					//Corner Accel
					if(carSpeedOffset > 0){
						//carSpeedOffset-=0.0025f * cornerSpeed;
						Debug.Log("Accel off corner");
						carSpeedOffset-= gearedAccel;
					} else {
						carSpeedOffset=0;
					}
				}
				//Allows acceleration from a slow corner whilst on a following fast corner
				if(carSpeedOffset > cornerSpeed){
					//carSpeedOffset-=0.0025f * cornerSpeed;
					Debug.Log("Accel on following corner");
					carSpeedOffset-= gearedAccel;
				}
			}
		} else {
			if(lap > 0){
				//Post turn accel
				if(carSpeedOffset > 0){
					carSpeedOffset-= gearedAccel;
				} else {
					carSpeedOffset=0;
				}
			}
		}

		//End of turn
		if(cornercounter >= (turnLength[turn-1] * turnAngle[turn-1])){
			Scoreboard.updateScoreboard();
			onTurn = false;
			Movement.onTurn = false;
			AIMovement.onTurn = false;
			straightcounter = 0;
			cornercounter = 0;
			straight++;
			turn++;
			if(straight > PlayerPrefs.GetInt("TotalTurns")){
				straight = 1;
				turn = 1;
			}
			if(cornerKerb.name != "FixedKerb"){
				cornerKerb.GetComponent<Renderer>().enabled = false;
			}
			if(apron.name != "FixedApron"){
				apron.GetComponent<Renderer>().enabled = false;
			}
		}
	}
	
	void OnGUI(){
		GUI.Label(new Rect(200, 200, 300, 200), "Offset: " + carSpeedOffset);
	}
	
	float calcCircuitGearing(){
		int longestStraight = 1;
		int slowestTurn = 1;
		int slowestTurnLength = 1;
		int turnSpeed = 0;
		int straightDist = 0;
		float calcdGear;
		for(int i=0;i<6;i++){
			turnSpeed = calcCornerSpeed(i);
			
			if(turnSpeed > slowestTurn){
				slowestTurn = turnSpeed;
				slowestTurnLength = (turnLength[i] * turnAngle[i]);
			}
		}
		Debug.Log("Slowest Turn - " + slowestTurn + "MpH");
		
		for(int i=0;i<6;i++){
			straightDist = straightLength[i];
			if(straightDist > longestStraight){
				longestStraight = straightDist;
			}
		}
		Debug.Log("Longest Straight - " + longestStraight + "m");
		
		calcdGear = (float)slowestTurn / (float)(longestStraight + (float)(slowestTurnLength / 2));
		Debug.Log("Calculated Gearing - " + calcdGear.ToString("f3") + " (" + (float)slowestTurn + " / " + (float)(longestStraight + (float)(slowestTurnLength / 2)) + ")");
		return calcdGear;
	}
	
	int calcCornerSpeed(int corner){
		int length = turnLength[corner];
		if(PlayerPrefs.GetString("TrackType") == "Plate"){
			if(turnAngle[corner] >= 4){
				return 0;
			}
		}
		switch(turnAngle[corner]){
			case 1:
				if(length > 160){
					return 60;
				} else {
					if(length > 45){
						return 30;
					} else {
						return 15;
					}
				}
				break;
			case 2:
				if(length > 45){
					return 20;
				} else {
					return 0;
				}
				break;
			case 4:
				if(length > 90){
					return 10;
				} else {
					return 0;
				}
				break;
			case 8:
				return 0;
				break;
			default:
				return 0;
				break;
		}
		return 0;
	}
}