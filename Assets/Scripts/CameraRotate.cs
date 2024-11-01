using UnityEngine;
using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Random=UnityEngine.Random;

public class CameraRotate : MonoBehaviour {

	public GameObject thePlayer;
	public GameObject TDCamera;
	public GameObject trackEnviro;
	public GameObject cornerKerb;
	public GameObject finishLine;
	public GameObject tropicono;
	
	public int customFrameRate;
	
	Renderer cornerKerbRenderer;
	
	public static string seriesPrefix;
	public static bool officialSeries;
	
	public Camera MainCam;
	public int cameraZoom;
	
	public AudioSource carEngine;
	public AudioSource crowdNoise;
	public static int audioOn;

	public static bool pacing;
	public static bool cautionOut;
	public static bool cautionCleared;
	public static bool acknowledgeWreck;
	public static bool overtime;

	public static int[] straightLength, turnLength, turnAngle;

	public static float turnSpeed;
	public static int trackLength;
	public static int totalTurns;
	public static int currentLapLength;
	public static float straightCounter;
	public static float turnCounter;
	public static float frameRotation;
	public static float carSpeedOffset;
	public static int cornerSpeed;
	public static int cornerMidpoint;
	public static float gearedAccel;
	
	public static float maxCornerFactor;
	public static float wreckingturnCounter;
	
	public static bool onTurn;
	public static bool crossedLine = false;
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
	
	public static float lapTime;
	public static float frameTime;
	public static float fastestRaceLap;
	public static int fastestRaceLapInt;
	
	public static float calcLapDelta;
	public static float raceLapRecord;
	public static int raceLapRecordInt;
	public static float lapRecord;
	public static int lapRecordInt;
	public static int currentLapRecord;
	public static int trackSpeedOffset;
	static string circuit;
	static string liveTimeTrial;
	static int liveTimeTrialActive;
	float kerbBlur;
	
	public GameObject cautionSummaryMenu;
	public GameObject pauseMenu;
	public GameObject challengeLost;
	public static bool isLiveTimeTrial;
	public static bool gamePausedLate;
	
	public static bool momentChecks;

	public static TrackInfo currentTrackInfo;
	
	void Awake(){
		
		int fpsCap = PlayerPrefs.GetInt("FPSLimit");
		switch(fpsCap){
			case 1:
				Application.targetFrameRate = 30;
				break;
			case 2:
				Application.targetFrameRate = 60;
				break;
			case 3:
				Application.targetFrameRate = 120;
				#if UNITY_EDITOR
				Application.targetFrameRate = -1;
				#endif
				break;
			default:
				Application.targetFrameRate = 60;
				break;
		}
		customFrameRate = -1;

		MainCam = GameObject.Find("Main Camera").GetComponent<Camera>();
		MainCam.orthographicSize = 8.0f;
		if(PlayerPrefs.HasKey("CameraZoom")){
			MainCam.orthographicSize = 10.0f - (PlayerPrefs.GetInt("CameraZoom") * 2);
		}
		
		currentTrackInfo = Resources.Load<TrackInfo>("Tracks/Phoenix");
		
		kerbBlur = 0.5f;
		straightCounter = 0;

		totalTurns = currentTrackInfo.totalTurns;
		straightLength = new int[totalTurns];
		turnLength = new int[totalTurns];
		turnAngle = new int[totalTurns];
		for(int i=0;i<totalTurns;i++){
			Debug.Log("Straight Length " + i + ": " + currentTrackInfo.straightLengths[i]);
			straightLength[i] = currentTrackInfo.straightLengths[i];
			turnLength[i] = currentTrackInfo.turnLengths[i];
			turnAngle[i] = currentTrackInfo.turnAngles[i];
			trackLength += straightLength[i] + turnLength[i];
		}

		onTurn = false;
		turnSpeed = 0;
		trackLength = 0;
		
		thePlayer = GameObject.Find("Player");
		trackEnviro = GameObject.Find("Environment");
		cautionSummaryMenu = GameObject.Find("CautionMenu");
		pauseMenu = GameObject.Find("PauseMenu");
		
		pacing = true;
		gamePausedLate = false;
		Time.timeScale = 1.0f;
		
		gearedAccel = calcCircuitGearing();
		cameraRotate = PlayerPrefs.GetInt("CameraRotate");
		cautionOut = false;
		acknowledgeWreck = false;
		overtime = false;
		
		//Start from the previous straight before the final turn of the pace lap
		straight = totalTurns - 1;
		turn = totalTurns - 1;

		if(cameraRotate == 1){
			TDCamera.transform.Rotate(0,0,turnAngle[turn]);
		}
		
		lap = 0;
		if(PlayerPrefs.HasKey("StartingLap")){
			lap = PlayerPrefs.GetInt("StartingLap") - 1;
			if(PlayerPrefs.GetString("RaceType") != "Event"){
				PlayerPrefs.DeleteKey("StartingLap");
			}
		}
		if(lap < 0){
			lap = 0;
		}
		
		raceEnd = PlayerPrefs.GetInt("RaceLaps");
		Debug.Log("Race Laps: " + raceEnd);
		if(PlayerPrefs.HasKey("CustomRaceLaps")){
			if(PlayerPrefs.GetString("RaceType") != "Event"){
				//This should not be here..
				PlayerPrefs.DeleteKey("CustomRaceLaps");
				//Debug.Log("Not An Event.. Delete");
			} else {
				//Set custom race length for events
				if(PlayerPrefs.GetInt("CustomRaceLaps") > 0){
					//Debug.Log("Race Laps: " + PlayerPrefs.GetInt("CustomRaceLaps"));
					raceEnd = PlayerPrefs.GetInt("CustomRaceLaps") - 1;
				}
			}
		}
		Debug.Log("Lap " + lap + " of " + raceEnd);
		
		circuit = PlayerPrefs.GetString("CurrentCircuit");
		liveTimeTrial = PlayerPrefs.GetString("LiveTimeTrial");
		liveTimeTrialActive = PlayerPrefs.GetInt("LiveTimeTrialActive");
		isLiveTimeTrial = false;
		if((circuit == liveTimeTrial)&&(liveTimeTrialActive == 1)){
			isLiveTimeTrial = true;
		}
		
		
		lapRecord = 0;
		if(PlayerPrefs.HasKey("FastestLap" + circuit)){
			lapRecord = PlayerPrefs.GetInt("FastestLap" + circuit);
			lapRecord = lapRecord / 1000;
		} else {
			PlayerPrefs.SetInt("FastestLap" + circuit, 0);
		}
		turnCounter = 0;
		carSpeedOffset = 80;
		cornerSpeed = 0;

		trackSpeedOffset = PlayerPrefs.GetInt("SpeedOffset");

		if(PlayerPrefs.HasKey("FixedSeries")){
			seriesPrefix = PlayerPrefs.GetString("FixedSeries");
		} else {
			seriesPrefix = PlayerPrefs.GetString("carSeries");
		}

		if(DriverNames.isOfficialSeries(seriesPrefix) == true){
			officialSeries = true;
		} else {
			officialSeries = false;
		}

		fastestRaceLap = 99.999f;
		raceLapRecord = 0;
		if(PlayerPrefs.HasKey("SpawnFromCaution")){
			lap = PlayerPrefs.GetInt("CautionLap") + 1;
			//Debug.Log("Restarting on lap " + lap);
			if(lap >= (raceEnd - 1)){
				//Overtime
				raceEnd = lap+2;
				PlayerPrefs.SetInt("RaceLaps", raceEnd);
				Debug.Log("Restarting Lap " + lap + " of " + raceEnd);
				overtime = true;
			}
			PlayerPrefs.DeleteKey("CautionLap");
			if(PlayerPrefs.HasKey("RaceFastestLap" + circuit)){
				raceLapRecord = PlayerPrefs.GetInt("RaceFastestLap" + circuit);
				raceLapRecord = raceLapRecord / 1000;
				//Debug.Log("Pulled existing fastest lap of " + raceLapRecord);
				PlayerPrefs.DeleteKey("RaceFastestLap" + circuit);
			}
		
		} else {
			//Delete any Race Fastest Lap time if not a caution restart
			PlayerPrefs.DeleteKey("RaceFastestLap" + circuit);
		}
		
		momentChecks = false;
		if(PlayerPrefs.HasKey("RaceMoment")){
			momentChecks = true;
		}
		
		audioOn = PlayerPrefs.GetInt("AudioOn");
		if(audioOn != 0){
			carEngine.volume = 0.15f;
			crowdNoise.volume = 0.05f;
			//Debug.Log("Audio is on: " + audioOn);
		} else {
			carEngine.volume = 0.0f;
			crowdNoise.volume = 0.0f;
			//Debug.Log("Audio is off: " + audioOn);
		}
		
	}

	void Start(){

		if(PlayerPrefs.HasKey("SpawnFromCaution")){
			
			//Race Restart Comms (1st pos = 0)
			if(Ticker.position < 10){
				this.gameObject.GetComponent<CommentaryManager>().commentate("RestartFront");
			} else {
				if(Ticker.position > 35){
					this.gameObject.GetComponent<CommentaryManager>().commentate("RestartBack");
				} else {
					if((Ticker.position > 16)&&(Ticker.position < 26)){
						this.gameObject.GetComponent<CommentaryManager>().commentate("RestartMiddle");
					} else {
						this.gameObject.GetComponent<CommentaryManager>().commentate("GreenFlag");
					}
				}
			}
		} else {
			//Initial Race Start
			this.gameObject.GetComponent<CommentaryManager>().commentate("GreenFlag");
		}
	}

	// Update is called once per frame
	void FixedUpdate() {

		//LateUpdate was unreliable..
		//..so run this at next frame start instead.
		if(gamePausedLate == true){
			if((cautionSummaryMenu.activeSelf == true)||
			   (RaceHUD.raceOver == true)){
				//Debug.Log("Time Paused For Caution (Camera Rotate)");
				Time.timeScale = 0.0f;
				return;
			}
			try {
				pauseMenu = GameObject.Find("PauseMenu");
				if(pauseMenu.activeSelf == true){
					Debug.Log("Time Paused By Player (Camera Rotate)");
					Time.timeScale = 0.0f;
				}
			}
			catch (Exception e){
				Debug.Log("Failed To Pause: " + e.Message);
			}
			try {
				challengeLost = GameObject.Find("ChallengeLost");
				if(challengeLost.activeSelf == true){
					Debug.Log("Time Paused (Moment Failed)");
					Time.timeScale = 0.0f;
				}
			}
			catch (Exception e){
				Debug.Log("Failed To End Moment Challenge: " + e.Message);
			}
			if(RaceHUD.raceOver == true){
				Debug.Log("Time Paused (Race Over)");
				Time.timeScale = 0.0f;
			}
		} else {
			straightCounter += Movement.playerSpeedMetres * Time.deltaTime;
			pauseMenu.SetActive(false);
		}
		
		//Commentary hasn't mentioned the wreck yet..
		if((cautionOut == true)&&(acknowledgeWreck == false)){
			acknowledgeWreck = true;
			//To be replaced with turn-based commentary
			this.gameObject.GetComponent<CommentaryManager>().commentate("Crash");
		}
		
		if((Movement.wreckOver == true)&&(Movement.isWrecking == false)){
			//If last lap, no restart. It's over!
			//Terminal Engine Damage? It's over!
			if((lap >= raceEnd)||(Movement.blownEngine == true)){
				endRace();
			}
			return;
		}
		
		//Keep the camera and environment following the player on the z-axis
		TDCamera.transform.position = new Vector3(thePlayer.transform.position.x,TDCamera.transform.position.y,thePlayer.transform.position.z);
		trackEnviro.transform.position = new Vector3(trackEnviro.transform.position.x,trackEnviro.transform.position.y,thePlayer.transform.position.z);
		
		if(pacing == false){
			averageSpeedTotal += (Movement.playerSpeed - carSpeedOffset);
			averageSpeedCount++;
			averageSpeed = averageSpeedTotal / averageSpeedCount;
			
			frameTime = (1/(Movement.playerSpeed / 200f));
			lapTime += (frameTime * Time.deltaTime);

			if(fastestRaceLap < 99f){
				calcLapDelta = lapTime-((fastestRaceLap / trackLength) * currentLapLength);
			} else {
				calcLapDelta = 99.999f;
			}
			//Debug.Log("Delta:" + calcLapDelta + " - Lap Time:" + lapTime + " - Fastest Lap:" + fastestRaceLap + " - Track Length:" + trackLength + " - Current Lap Length:" + currentLapLength);
		}
		
		//Increment Lap
		if ((straight == 0)&&(straightCounter >= PlayerPrefs.GetInt("StartLine"))&&(crossedLine == false)){
			Ticker.updateTicker();
			//Freeze lap count at a caution
			crossedLine = true;
			if(cautionOut == false){
				lap++;
			}
			
			//Final Lap
			if(CameraRotate.lap == CameraRotate.raceEnd){
				if(Ticker.position == 0){
					this.gameObject.GetComponent<CommentaryManager>().commentate("LastLapLeader");
				} else {
					this.gameObject.GetComponent<CommentaryManager>().commentate("LastLap");
				}
			}
			//Starts/Restarts
			if(pacing == true){
				Movement.pacingEnds();
				this.gameObject.GetComponent<CommentaryManager>().commentate("Start");
				pacing = false;
			} else {					
				if((averageSpeed > raceLapRecord)&&(lap > 1)){
					raceLapRecord = averageSpeed;
				}
				if((averageSpeed > lapRecord)&&(lap > 1)){
					lapRecord = averageSpeed;
				}
				if(raceLapRecord > lapRecord){
					lapRecord = raceLapRecord;
				}
				if((lapTime < fastestRaceLap)&&(lap > 1)&&(lapTime != 0)){
					fastestRaceLap = lapTime;
				}
			}
			
			currentLapLength = 0;
			averageSpeed = 0;
			averageSpeedCount = 0;
			averageSpeedTotal = 0;
			lapTime = 0;
			frameTime = 0;
			
			//Race End
			if(lap >= (raceEnd + 1)){
				gamePausedLate = true;
				endRace();
			} else {
			
				//Caution, reset scene
				if(cautionOut == true){
					//Only save at the line if not currently wrecking
					if(Movement.isWrecking == false){
						//Blown engine
						if(Movement.blownEngine == true){
							Debug.Log("Engine Blown!");
							Ticker.checkFinishPositions();
						} else {
							Ticker.saveCautionPositions(true);
						}
						saveRaceFastestLap();
						cautionSummaryMenu.SetActive(true);
						finishLine.GetComponent<Renderer>().enabled = true;
						carEngine.volume = 0.0f;
						crowdNoise.volume = 0.0f;
						gamePausedLate = true;
					}
				}
				PlayerPrefs.SetInt("TotalLaps",PlayerPrefs.GetInt("TotalLaps") + 1);
			}
		}

		if ((straightCounter % 20) < 1){
			Ticker.updateTicker();
		}

		//Final Lap, Crowd Noise increases
		if(audioOn != 0){
			if(lap == raceEnd){
				if(crowdNoise.volume < 0.15f){
					crowdNoise.volume += 0.002f;
				}
			}
		}
		currentLapLength++;

		//Turning
		if(straightCounter > straightLength[straight]){
			if(onTurn == false){
				onTurn = true;
				Movement.onTurn = true;
				AIMovement.onTurn = true;
				cornerSpeed = calcCornerSpeed(straight);
				cornerMidpoint = (turnLength[straight] / 2);
				//Debug.Log("Corner " + straight + " speed: " + cornerSpeed);
			}

			frameRotation = turnAngle[turn] / (turnLength[turn] / Movement.playerSpeedMetres) * Time.deltaTime;
			if(cameraRotate == 1){
				TDCamera.transform.Rotate(0,0,-frameRotation);
			}
			turnCounter+=(Movement.playerSpeedMetres * Time.deltaTime);
			
			
			if(lap > 0){
				//Corner Decel
				if(turnCounter < cornerMidpoint){
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
			if(pacing == false){
				//Post turn accel
				if(carSpeedOffset > 0){
					carSpeedOffset-= gearedAccel;
				} else {
					carSpeedOffset=0;
				}
			}
		}

		//End of turn
		if(onTurn == true){
			if(turnCounter >= turnLength[turn]){
				Ticker.updateTicker();
				onTurn = false;
				Movement.onTurn = false;
				AIMovement.onTurn = false;
				straightCounter = 0;
				turnCounter = 0;
				straight++;
				turn++;
				if(straight == totalTurns){
					straight = 0;
					turn = 0;
					crossedLine = false;
				}
				
				if(cautionOut == true){
					this.gameObject.GetComponent<CommentaryManager>().commentate("Caution");
				}
			}
		}
	}
	
	public void endRace(){
		if(RaceHUD.raceOver == true){
			//We've already ended the race.. waiting for logic elsewhere to finish
			return;
		} else {
			RaceHUD.raceOver = true;
		}
		//Debug.Log("Race Ended");
		if(lap >= (raceEnd + 1)){
			//Bug catch, again no idea on this one
			if((straight == 0)||(turn == 0)){
				//Do nothing
			} else {
				//Not on the finish straight, must have wrecked somewhere..
				if(Movement.wreckOver == false){
					//Wait! I haven't wrecked..
					if(lap <= raceEnd + 1){
						return;
					}
					//Don't end the race
				}
			}
			lap--;
			gamePausedLate = true;
			if(momentChecks == true){
				//MomentsCriteria.updateCriteriaCompletion("CrossTheLine",true);
				MomentsCriteria.checkMomentsCriteria("CrossTheLine","");
			}
			finishLine.transform.position = new Vector3(finishLine.transform.position.x,finishLine.transform.position.y,thePlayer.transform.position.z + 1f);
			finishLine.GetComponent<Renderer>().enabled = true;
		}
		if(lap == raceEnd){
			if(Movement.wreckOver == true){
				//Debug.Log("We've wrecked on the last lap!");
				gamePausedLate = true;
			}
		}
		//Giant Inflatable Tropicono Orange
		if(PlayerPrefs.GetString("CurrentCircuit") == "Joliet"){
			int rand = Random.Range(1,100);
			//Lucky day
			if((rand > 26)&&(rand < 28)){
				tropicono.GetComponent<Renderer>().enabled = true;
			}
		}
		carEngine.volume = 0;
		crowdNoise.volume = 0;
		GameObject.Find("Player").GetComponent<Movement>().hideHUD();
		if(Movement.wreckOver == true){
			Ticker.checkFinishPositions(false);
		} else {
			Ticker.checkFinishPositions();
		}
		PlayerPrefs.SetInt("ExpAdded",0);
		
		if(momentChecks == true){
			MomentsCriteria.checkMomentsCriteria("CarAvoidsWreck","");
			MomentsCriteria.checkMomentsCriteria("FinishPositionLowerThan",Ticker.position.ToString());
			MomentsCriteria.checkEndCriteria();
		}
		
		saveRaceFastestLap();
	}
	
	public static void saveRaceFastestLap(){
		//Invert the fastest times so they order right in the leaderboard
		fastestRaceLapInt = 100000 - (int)Mathf.Round(fastestRaceLap * 1000);
		raceLapRecordInt = (int)Mathf.Round((raceLapRecord - trackSpeedOffset) * 1000);
		if(raceLapRecordInt < 0){
			//Debug.Log("No complete lap set.. bail");
			raceLapRecordInt = 0;
			return;
		}
		if(fastestRaceLapInt < 0){
			//Debug.Log("No fastest lap set.. bail");
			fastestRaceLapInt = 0;
			return;
		}
		//Debug.Log("Fastest Lap Save Call (Reusable)");
		lapRecordInt = (int)Mathf.Round((lapRecord - trackSpeedOffset) * 1000);
		PlayerPrefs.SetInt("FastestLap" + circuit, lapRecordInt);
		if(PlayerPrefs.HasKey("FastestLap" + circuit)){
			currentLapRecord = PlayerPrefs.GetInt("FastestLap" + circuit);
			if(raceLapRecord > currentLapRecord){
				PlayerPrefs.SetInt("FastestLap" + circuit, raceLapRecordInt);
			}
		}
		PlayerPrefs.SetInt("RaceFastestLap" + circuit, raceLapRecordInt);
		if(isLiveTimeTrial == true){
			//Double checked
			if(officialSeries == true){
				PlayFabManager.SendLeaderboard(fastestRaceLapInt, "FastestLapChallenge","");
				//This seems to be working
				//Debug.Log("Sent Leaderboards (Reusable)");
			}
		} else {
			//Debug.Log("Not The Live Circuit");
		}
	}
	
	public static void throwCaution(){
		//No caution on the last lap
		if(lap < raceEnd){
			cautionOut = true;
			acknowledgeWreck = false;
			PlayerPrefs.SetInt("SpawnFromCaution",1);
			PlayerPrefs.SetInt("CautionLap", lap);
		}
	}
	
	public void pauseGame(){
		if(RaceHUD.raceOver == false){
			RaceHUD.gamePaused = true;
			gamePausedLate = true;
			TDCamera.gameObject.GetComponent<AudioListener>().enabled = false;
			AudioSource[] sounds = TDCamera.gameObject.GetComponents<AudioSource>();
			for(int i=0;i < sounds.Length;i++){
				sounds[i].mute = true;
			}
			PlayerPrefs.SetInt("Volume",0);
			PlayerPrefs.SetInt("MidRaceLoading", 1);
			pauseMenu.SetActive(true);
		}
	}
	public void unpauseGame(){
		if(RaceHUD.raceOver == false){
			RaceHUD.gamePaused = false;
			gamePausedLate = false;
			Time.timeScale = 1.0f;
			TDCamera.gameObject.GetComponent<AudioListener>().enabled = true;
			AudioSource[] sounds = TDCamera.gameObject.GetComponents<AudioSource>();
			for(int i=0;i < sounds.Length;i++){
				sounds[i].mute = false;
			}
			PlayerPrefs.SetInt("Volume",1);
			pauseMenu.SetActive(false);
			Debug.Log("Unpause the game");
		}
	}
	
	float calcCircuitGearing(){
		int longestStraight = 1;
		int slowestTurn = 1;
		int slowestTurnLength = 1;
		int turnSpeed = 0;
		int straightDist = 0;
		float calcdGear;
		for(int i=0;i<totalTurns;i++){
			turnSpeed = calcCornerSpeed(i);
			if(turnSpeed > slowestTurn){
				slowestTurn = turnSpeed;
				slowestTurnLength = (turnLength[i] * turnAngle[i]);
			}

			straightDist = straightLength[i];
			if(straightDist > longestStraight){
				longestStraight = straightDist;
			}
		}

		//Default fallback for plate tracks (where slowest turn = 0)
		if(slowestTurn == 1){
			slowestTurn = 20;
			slowestTurnLength = 200;
		}
		
		calcdGear = (float)(slowestTurn * 1.5f) / (float)(longestStraight + (float)(slowestTurnLength / 2));
		if(calcdGear > 0.25f){
			calcdGear = 0.25f;
		}
		//Debug.Log("Calculated Gearing - " + calcdGear.ToString("f3") + " (" + (float)slowestTurn + " / " + (float)longestStraight + " + " + (float)(slowestTurnLength / 2) + ")");
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