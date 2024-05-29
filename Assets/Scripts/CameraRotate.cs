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

	public static int[] straightLength = new int[6];
	public static int[] turnLength = new int[6];
	public static int[] turnAngle = new int[6];
	public static int[] turnDir = new int[6];
	public static float turnSpeed;
	public static int trackLength;
	public static int totalTurns;
	public static int currentLapLength;
	public static int lengthcounter;
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
	float kerbBlur;
	
	public GameObject cautionSummaryMenu;
	public GameObject pauseMenu;
	public GameObject challengeLost;
	public static bool isLiveTimeTrial;
	public static bool gamePausedLate;
	
	public static bool momentChecks;
	
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
		MainCam.orthographicSize = 7.0f;
		if(PlayerPrefs.HasKey("CameraZoom")){
			MainCam.orthographicSize = 8.0f - PlayerPrefs.GetInt("CameraZoom");
		}
		
		cornerKerbRenderer = cornerKerb.GetComponent<Renderer>();
		
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
		
		pacing = true;
		gamePausedLate = false;
		Time.timeScale = 1.0f;
		
		gearedAccel = calcCircuitGearing();
		cameraRotate = PlayerPrefs.GetInt("CameraRotate");
		cautionOut = false;
		acknowledgeWreck = false;
		overtime = false;
		
		for(int i=0;i<totalTurns;i++){
			trackLength += straightLength[i];
			trackLength += (turnLength[i] * turnAngle[i]);
		}
		straight = totalTurns;
		turn = totalTurns;
		if(cameraRotate == 1){
			TDCamera.transform.Rotate(0,0,turnLength[turn-1]);
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
		if(PlayerPrefs.HasKey("CustomRaceLaps")){
			if(PlayerPrefs.GetString("RaceType") != "Event"){
				//This should not be here..
				PlayerPrefs.DeleteKey("CustomRaceLaps");
			} else {
				//Set custom race length for events
				if(PlayerPrefs.GetInt("CustomRaceLaps") > 0){
					raceEnd = PlayerPrefs.GetInt("CustomRaceLaps") - 1;
				}
			}
		}
		
		circuit = PlayerPrefs.GetString("CurrentCircuit");
		liveTimeTrial = PlayerPrefs.GetString("LiveTimeTrial");
		isLiveTimeTrial = false;
		if(circuit == liveTimeTrial){
			isLiveTimeTrial = true;
		}
		
		
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
			raceEnd = PlayerPrefs.GetInt("RaceLaps");
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
				Debug.Log("Time Paused (Camera Rotate)");
				Time.timeScale = 0.0f;
				return;
			}
			try {
				pauseMenu = GameObject.Find("PauseMenu");
				if(pauseMenu.activeSelf == true){
					Debug.Log("Time Paused (Camera Rotate)");
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
			pauseMenu.SetActive(false);
		}
		
		//Commentary hasn't mentioned the wreck yet..
		if((cautionOut == true)&&(acknowledgeWreck == false)){
			acknowledgeWreck = true;
			//To be replaced with turn-based commentary
			this.gameObject.GetComponent<CommentaryManager>().commentate("Crash");
		}
		
		//Prevent the counters running when game is pausing
		if(gamePausedLate == false){
			lengthcounter++;
			straightcounter++;
		}
		
		if((Movement.wreckOver == true)&&(Movement.isWrecking == false)){
			//If last lap, no restart, it's over!
			if(lap >= raceEnd){
				endRace();
			}
			//Terminal Engine Damage
			if(Movement.blownEngine == true){
				endRace();
			}
			return;
		}
		
		//Keep the camera and environment following the player on the z-axis
		TDCamera.transform.position = new Vector3(TDCamera.transform.position.x,TDCamera.transform.position.y,thePlayer.transform.position.z);
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
		if ((straightcounter == PlayerPrefs.GetInt("StartLine"))&&(straight == 1)){
			Ticker.updateTicker();
			//Freeze lap count at a caution
			if(cautionOut == false){
				lap++;
			}
			
			//Final Lap
			if(CameraRotate.lap == CameraRotate.raceEnd){
				//Debug.Log("LAST LAP! Pos:" + Ticker.position);
				if(Ticker.position == 0){
					this.gameObject.GetComponent<CommentaryManager>().commentate("LastLapLeader");
				} else {
					this.gameObject.GetComponent<CommentaryManager>().commentate("LastLap");
				}
			}
			//Debug.Log("Add on a lap, now on lap " + lap);
			//Starts/Restarts
			if(pacing == true){
				Movement.pacingEnds();
				this.gameObject.GetComponent<CommentaryManager>().commentate("Start");
				pacing = false;
			} else {			
				if(lengthcounter >= 100){		
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
						//Debug.Log("New Fastest Lap: " + fastestRaceLap);
					}
				}
			}
			
			currentLapLength = 0;
			lengthcounter=0;

			averageSpeed = 0;
			averageSpeedCount = 0;
			averageSpeedTotal = 0;
			
			lapTime = 0;
			frameTime = 0;
			
			//Caution, reset scene
			if(cautionOut == true){
				//Only save at the line if not currently wrecking
				if(Movement.isWrecking == false){
					saveRaceFastestLap();
					cautionSummaryMenu.SetActive(true);
					finishLine.GetComponent<Renderer>().enabled = true;
					carEngine.volume = 0.0f;
					crowdNoise.volume = 0.0f;
					gamePausedLate = true;
				}
			}
			PlayerPrefs.SetInt("TotalLaps",PlayerPrefs.GetInt("TotalLaps") + 1);
			
			//Race End
			if(lap >= (raceEnd + 1)){
				gamePausedLate = true;
				endRace();
			}
		}

		if ((straightcounter % 20) == 1){
			Ticker.updateTicker();
		}

		if(audioOn != 0){
			if(lap == (PlayerPrefs.GetInt("RaceLaps"))){
				if(crowdNoise.volume < 0.15f){
					crowdNoise.volume += 0.002f;
				}
			}
		}
		currentLapLength++;

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
					//Debug.Log("Corner Angle Factor:" + cornerAngleFactor + " - maxFactor:" + maxCornerFactor);
					TDCamera.transform.Rotate(0,0,(-1.0f/turnAngle[turn-1]) * cornerAngleFactor);
					wreckingCornerCounter += cornerAngleFactor;
					//Debug.Log(wreckingCornerCounter + " so far, out of " + (turnLength[turn-1] * turnAngle[turn-1]));
				} else {
					wreckingCornerCounter++;
					if(turnDir[turn-1] == 1){
						TDCamera.transform.Rotate(0,0,(1.0f/turnAngle[turn-1]));
					} else {
						TDCamera.transform.Rotate(0,0,(-1.0f/turnAngle[turn-1]));
					}
				}
			}
			cornerKerbRenderer.enabled = true;
			cornerKerbRenderer.material.mainTextureOffset = new Vector2(kerbBlur,0);
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
			if(onTurn == true){
				//Debug.Log("WreckCornCount:" + wreckingCornerCounter + " - TurnLength:" + turnLength[turn-1] + " - Turn Angle:" + turnAngle[turn-1] + " - Turn Factor:" + (turnLength[turn-1] * turnAngle[turn-1]));
				if(wreckingCornerCounter >= (turnLength[turn-1] * turnAngle[turn-1])){
					//Debug.Log("WreckCornCount:" + wreckingCornerCounter + " > Turn Factor:" + (turnLength[turn-1] * turnAngle[turn-1]));
					Ticker.updateTicker();
					onTurn = false;
					Movement.onTurn = false;
					AIMovement.onTurn = false;
					straightcounter = 0;
					cornercounter = 0;
					wreckingCornerCounter = 0;
					straight++;
					turn++;
					
					if(straight > PlayerPrefs.GetInt("TotalTurns")){
						straight = 1;
						turn = 1;
					}
					//Debug.Log("Straight:" + straight + " - Turn:" + turn);
					if(cornerKerb.name != "FixedKerb"){
						cornerKerbRenderer.enabled = false;
					}
				}
			}
		} else {
			if(onTurn == true){
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
					
					if(cautionOut == true){
						this.gameObject.GetComponent<CommentaryManager>().commentate("Caution");
					}
					
					//Debug.Log("Straight:" + straight + " - Turn:" + turn);
					if(cornerKerb.name != "FixedKerb"){
						cornerKerbRenderer.enabled = false;
					}
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
		if(lap >= (raceEnd + 1)){
			//Bug catch, again no idea on this one
			if((straight == 1)||(turn == 1)){
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
			if(momentChecks == true){
				MomentsCriteria.updateCriteriaCompletion("CrossTheLine",true);
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
		Ticker.checkFinishPositions();
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
			Debug.Log("No complete lap set.. bail");
			raceLapRecordInt = 0;
			return;
		}
		if(fastestRaceLapInt < 0){
			Debug.Log("No fastest lap set.. bail");
			fastestRaceLapInt = 0;
			return;
		}
		Debug.Log("Fastest Lap Save Call (Reusable)");
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
				PlayFabManager.SendLeaderboard(raceLapRecordInt, "LiveTimeTrialR184","");
				PlayFabManager.SendLeaderboard(fastestRaceLapInt, "FastestLapChallenge","");
				//This seems to be working
				Debug.Log("Sent Leaderboards (Reusable)");
			}
		} else {
			Debug.Log("Not The Live Circuit");
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
			slowestTurn = 20;
			slowestTurnLength = 200;
		}
		
		calcdGear = (float)(slowestTurn * 1.5f) / (float)(longestStraight + (float)(slowestTurnLength / 2));
		if(calcdGear > 0.25f){
			calcdGear = 0.25f;
		}
		Debug.Log("Calculated Gearing - " + calcdGear.ToString("f3") + " (" + (float)slowestTurn + " / " + (float)longestStraight + " + " + (float)(slowestTurnLength / 2) + ")");
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