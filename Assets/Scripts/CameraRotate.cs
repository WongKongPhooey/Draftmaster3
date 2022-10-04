﻿using UnityEngine;
using System.Collections;
using Random=UnityEngine.Random;

public class CameraRotate : MonoBehaviour {

	public GameObject thePlayer;
	public GameObject TDCamera;
	public GameObject trackEnviro;
	public GameObject cornerKerb;
	public GameObject apron;
	public GameObject finishLine;
	public GameObject tropicono;
	
	public AudioSource carEngine;
	public AudioSource crowdNoise;
	public static int audioOn;

	public static bool cautionOut;
	public static bool cautionCleared;
	public static bool overtime;

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
	
	public static float maxCornerFactor;
	public static float wreckingCornerCounter;
	
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
	
	public GameObject cautionSummaryMenu;
	public GameObject pauseMenu;
	
	void Awake(){
		
		Time.timeScale = 1.0f;
		
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
		
		thePlayer = GameObject.Find("Player");
		trackEnviro = GameObject.Find("Environment");
		
		cautionSummaryMenu = GameObject.Find("CautionMenu");
		pauseMenu = GameObject.Find("PauseMenu");
		pauseMenu.SetActive(false);
		
		gearedAccel = calcCircuitGearing();
		
		cameraRotate = PlayerPrefs.GetInt("CameraRotate");
		
		cautionOut = false;
		overtime = false;
		
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
		raceEnd = PlayerPrefs.GetInt("RaceLaps");
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

		if(PlayerPrefs.HasKey("SpawnFromCaution")){
			//Debug.Log("Caution Ending");
			lap = PlayerPrefs.GetInt("CautionLap") + 1;
			Debug.Log("Restarting on lap " + lap);
			raceEnd = PlayerPrefs.GetInt("RaceLaps");
			if(lap >= (raceEnd - 1)){
				//Unlimited Overtime
				//Debug.Log("Overtime");
				raceEnd = lap+2;
				PlayerPrefs.SetInt("RaceLaps", raceEnd);
				Debug.Log("Restarting Lap " + lap + " of " + raceEnd);
				overtime = true;
			}
			PlayerPrefs.DeleteKey("CautionLap");
		} else {
			Debug.Log("No Active Caution");
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
		
		if(Movement.wreckOver == true){
			//If last lap, no restart, it's over!
			if(lap >= raceEnd){
				endRace();
			}
			return;
		}
		
		//finishLine.renderer.enabled = false;
		
		//Keep the camera and environment following the player on the z-axis
		TDCamera.transform.position = new Vector3(TDCamera.transform.position.x,TDCamera.transform.position.y,thePlayer.transform.position.z);
		trackEnviro.transform.position = new Vector3(trackEnviro.transform.position.x,trackEnviro.transform.position.y,thePlayer.transform.position.z);
		
		averageSpeedTotal += (Movement.playerSpeed - carSpeedOffset);
		averageSpeedCount++;
		averageSpeed = averageSpeedTotal / averageSpeedCount;
		
		straightcounter++;
		
		//Increment Lap
		if ((straightcounter == PlayerPrefs.GetInt("StartLine"))&&(straight == 1)){
			Ticker.updateTicker();
			lap++;
			//Starts/Restarts
			if(Movement.pacing == true){
				Movement.pacingEnds();
			}
			
			//Caution, reset scene
			if(cautionOut == true){
				//Only save at the line if not currently wrecking
				if((Movement.isWrecking == false)||(lap >= (raceEnd + 1))){
					cautionSummaryMenu.SetActive(true);
					Time.timeScale = 0.0f;
					finishLine.GetComponent<Renderer>().enabled = true;
					carEngine.volume = 0;
					crowdNoise.volume = 0;
				}
			}
			PlayerPrefs.SetInt("TotalLaps",PlayerPrefs.GetInt("TotalLaps") + 1);

			if((averageSpeed > lapRecord)&&(lap > 1)){
				lapRecord = averageSpeed;
			}

			averageSpeed = 0;
			averageSpeedCount = 0;
			averageSpeedTotal = 0;
			
			//Race End
			if(lap >= (raceEnd + 1)){
				endRace();
			}
		}

		if ((straightcounter % 100) == 1){
			Ticker.updateTicker();
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

		//Turning
		if(straightcounter > straightLength[straight-1]){
			if(onTurn == false){
				onTurn = true;
				Movement.onTurn = true;
				AIMovement.onTurn = true;
				cornerSpeed = calcCornerSpeed(straight-1);
				cornerMidpoint = (turnLength[straight-1] * turnAngle[straight-1]) / 2;
				wreckingCornerCounter = 0;
				//Debug.Log("Corner " + straight + " speed: " + cornerSpeed);
			}
			if(cameraRotate == 1){
				//If wrecking and not on last lap (the finish trigger screws up with this enabled)
				if((Movement.isWrecking == true)&&(lap < raceEnd)){
					maxCornerFactor = Movement.playerSpeed - Movement.speedOffset;
					float cornerAngleFactor = (1 / maxCornerFactor) * (maxCornerFactor + Movement.playerWreckDecel);
					Debug.Log("Corner Angle Factor:" + cornerAngleFactor + " - maxFactor:" + maxCornerFactor);
					TDCamera.transform.Rotate(0,0,(-1.0f/turnAngle[turn-1]) * cornerAngleFactor);
					wreckingCornerCounter += cornerAngleFactor;
					Debug.Log(wreckingCornerCounter + " so far, out of " + (turnLength[turn-1] * turnAngle[turn-1]));
					//if(wreckingCornerCounter > (turnLength[turn-1] * turnAngle[turn-1])){
						//float cornerFinalDiff = wreckingCornerCounter - (turnLength[turn-1] * turnAngle[turn-1]);
						//TDCamera.transform.Rotate(0,0,(-1.0f/cornerFinalDiff));
					//}
				} else {
					wreckingCornerCounter++;
					if(turnDir[turn-1] == 1){
						TDCamera.transform.Rotate(0,0,(1.0f/turnAngle[turn-1]));
					} else {
						TDCamera.transform.Rotate(0,0,(-1.0f/turnAngle[turn-1]));
					}
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
						//Debug.Log("Accel off corner");
						carSpeedOffset-= gearedAccel;
					} else {
						carSpeedOffset=0;
					}
				}
				//Allows acceleration from a slow corner whilst on a following fast corner
				if(carSpeedOffset > cornerSpeed){
					//carSpeedOffset-=0.0025f * cornerSpeed;
					//Debug.Log("Accel on following corner");
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
		if(Movement.isWrecking == true){
			if(wreckingCornerCounter >= (turnLength[turn-1] * turnAngle[turn-1])){
				Ticker.updateTicker();
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
				if((apron)&&(apron.name != "FixedApron")){
					apron.GetComponent<Renderer>().enabled = false;
				}
			}
		} else {
			if(cornercounter >= (turnLength[turn-1] * turnAngle[turn-1])){
				Ticker.updateTicker();
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
				if((apron)&&(apron.name != "FixedApron")){
					apron.GetComponent<Renderer>().enabled = false;
				}
			}
		}
	}
	
	public void endRace(){
		if(lap > raceEnd){
			lap--;
		}
		Time.timeScale = 0.0f;
		if(Movement.wreckOver == false){
			finishLine.GetComponent<Renderer>().enabled = true;
		}
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
		GameObject.Find("Player").GetComponent<Movement>().hideHUD();
		Ticker.checkFinishPositions();
		PlayerPrefs.SetInt("ExpAdded",0);
		if(PlayerPrefs.HasKey("FastestLap" + circuit)){
			currentLapRecord = PlayerPrefs.GetInt("FastestLap" + circuit);
			lapRecord -= trackSpeedOffset;
			//Debug.Log(lapRecord);
			lapRecordInt = (int)Mathf.Round(lapRecord * 1000);
			PlayerPrefs.SetInt("FastestLap" + circuit, lapRecordInt);
			//Debug.Log("Send to leaderboard - " + lapRecordInt + ": " + circuit);
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
	
	public static void throwCaution(){
		//No caution on the last lap
		if(lap < raceEnd){
			cautionOut = true;
			PlayerPrefs.SetInt("SpawnFromCaution",1);
			PlayerPrefs.SetInt("CautionLap", lap);
		}
	}
	
	public void pauseGame(){
		if(RaceHUD.raceOver == false){
			RaceHUD.gamePaused = true;
			Time.timeScale = 0.0f;
			TDCamera.gameObject.GetComponent<AudioListener>().enabled = false;
			PlayerPrefs.SetInt("Volume",0);
			PlayerPrefs.SetInt("MidRaceLoading", 1);
			pauseMenu.SetActive(true);
		}
	}
	public void unpauseGame(){
		if(RaceHUD.raceOver == false){
			RaceHUD.gamePaused = false;
			Time.timeScale = 1.0f;
			TDCamera.gameObject.GetComponent<AudioListener>().enabled = true;
			PlayerPrefs.SetInt("Volume",1);
			pauseMenu.SetActive(false);
		}
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
		//Debug.Log("Slowest Turn - " + slowestTurn + "MpH");
		
		for(int i=0;i<6;i++){
			straightDist = straightLength[i];
			if(straightDist > longestStraight){
				longestStraight = straightDist;
			}
		}
		//Debug.Log("Longest Straight - " + longestStraight + "m");
		
		//Default fallback for plate tracks (where slowest turn = 0)
		if(slowestTurn == 1){
			slowestTurn = 40;
			slowestTurnLength = 200;
		}
		
		calcdGear = (float)slowestTurn / (float)(longestStraight + (float)(slowestTurnLength / 2));
		if(calcdGear > 0.25f){
			calcdGear = 0.25f;
		}
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
	
	public static float currentTurnSharpness(){
		return 8 / turnAngle[turn-1];
	}
}