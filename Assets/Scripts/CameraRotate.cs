using UnityEngine;
using System.Collections;

public class CameraRotate : MonoBehaviour {

	public GameObject TDCamera;
	public GameObject cornerKerb;
	public GameObject apron;
	public GameObject finishLine;
	
	public AudioSource carEngine;
	public AudioSource crowdNoise;
	public static int audioOn;

	public static bool cautionCleared;

	public static int[] straightLength = new int[6];
	public static int[] turnLength = new int[6];
	public static int[] turnAngle = new int[6];
	public static int[] turnDir = new int[6];
	public static int[] brakingPoint = new int[6];
	public static int[] brakingSpeed = new int[6];
	public static int[] accelPoint = new int[6];
	public static float turnSpeed;
	public static int trackLength;
	public static int totalTurns;
	public static int straightcounter;
	public static int cornercounter;
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
	int speedOffset;
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
		brakingPoint[0] = 20;
		brakingPoint[1] = 20;
		brakingPoint[2] = 20;
		brakingPoint[3] = 20;
		brakingPoint[4] = 20;
		brakingPoint[5] = 20;
		brakingSpeed[0] = 20;
		brakingSpeed[1] = 20;
		brakingSpeed[2] = 20;
		brakingSpeed[3] = 20;
		brakingSpeed[4] = 20;
		brakingSpeed[5] = 20;
		accelPoint[0] = 20;
		accelPoint[1] = 20;
		accelPoint[2] = 20;
		accelPoint[3] = 20;
		accelPoint[4] = 20;
		accelPoint[5] = 20;
		turnSpeed = 0;
		trackLength = 0;
		totalTurns = PlayerPrefs.GetInt("TotalTurns");
		for(int i=0;i<totalTurns;i++){
			trackLength += straightLength[i];
			//Debug.Log("Track Length: " + trackLength);
			trackLength += (turnLength[i] * turnAngle[i]);
			//Debug.Log("Track Length: " + trackLength);
		}
		straight = 1;
		turn = 1;
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
		cameraRotate = PlayerPrefs.GetInt("CameraRotate");

		speedOffset = PlayerPrefs.GetInt("SpeedOffset");

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
			carEngine.volume = 0.5f;
			crowdNoise.volume = 0.10f;
		} else {
			carEngine.volume = 0.0f;
			crowdNoise.volume = 0.0f;
		}
		
	}

	// Update is called once per frame
	void FixedUpdate () {
		
		//finishLine.renderer.enabled = false;
		
		averageSpeedTotal += Movement.playerSpeed;
		averageSpeedCount++;
		averageSpeed = averageSpeedTotal / averageSpeedCount;
		
		straightcounter++;
		
		if ((straightcounter == PlayerPrefs.GetInt("StartLine"))&&(straight == 1)){
			//finishLine.renderer.enabled = true;
			//Scoreboard.checkPositions();
			Scoreboard.updateScoreboard();
			lap++;
			PlayerPrefs.SetInt("TotalLaps",PlayerPrefs.GetInt("TotalLaps") + 1);

			if((ChallengeSelectGUI.challengeMode == true)&&(PlayerPrefs.GetString("ChallengeType")=="LastToFirstLaps")&&(Scoreboard.position == 1)){
				lap--;
				PlayerPrefs.SetInt("TotalLaps",PlayerPrefs.GetInt("TotalLaps") - 1);
				Time.timeScale = 0.0f;
				finishLine.GetComponent<Renderer>().enabled = true;
				//carEngine = GetComponent<AudioSource>();
				carEngine.volume = 0;
				crowdNoise.volume = 0;
				RaceHUD.raceOver = true;
				//Scoreboard.championshipPointsOrder();
				Scoreboard.checkFinishPositions();
				if((lap - 187) < PlayerPrefs.GetInt("LastToFirstRecord")){
					PlayerPrefs.SetInt("LastToFirstRecord",(lap - 187));
				}
			}

			if((ChallengeSelectGUI.challengeMode == true)&&(PlayerPrefs.GetString("ChallengeType")=="TrafficJam")&&(Scoreboard.position == 1)){
				lap--;
				PlayerPrefs.SetInt("TotalLaps",PlayerPrefs.GetInt("TotalLaps") - 1);
				Time.timeScale = 0.0f;
				finishLine.GetComponent<Renderer>().enabled = true;
				//carEngine = GetComponent<AudioSource>();
				carEngine.volume = 0;
				crowdNoise.volume = 0;
				RaceHUD.raceOver = true;
				//Scoreboard.championshipPointsOrder();
				Scoreboard.checkFinishPositions();
				if((lap - 150) < PlayerPrefs.GetInt("TrafficJamRecord")){
					PlayerPrefs.SetInt("TrafficJamRecord",(lap - 150));
				}
			}

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
				if(crowdNoise.volume < 0.5f){
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
			carEngine.volume = 0;
			crowdNoise.volume = 0;
			RaceHUD.raceOver = true;
			Scoreboard.checkFinishPositions();
			if(PlayerPrefs.HasKey("FastestLap" + circuit)){
				currentLapRecord = PlayerPrefs.GetInt("FastestLap" + circuit);
				//if(lapRecord > currentLapRecord){
					lapRecord -= speedOffset;
					Debug.Log(lapRecord);
					lapRecordInt = (int)Mathf.Round(lapRecord * 1000);
					PlayerPrefs.SetInt("FastestLap" + circuit, lapRecordInt);
					Debug.Log(lapRecordInt);
					PlayFabManager.SendLeaderboard(lapRecordInt, circuit);
				//}
			}
			if((ChallengeSelectGUI.challengeMode == true)&&(PlayerPrefs.GetString("ChallengeType")=="TeamPlayer")){
				if((Movement.draftPercent) > PlayerPrefs.GetInt("TeamPlayerRecord")){
					PlayerPrefs.SetInt("TeamPlayerRecord",int.Parse(Mathf.Round(Movement.draftPercent).ToString()));
				}
				PlayerPrefs.SetInt("DraftPercent",int.Parse(Mathf.Round(Movement.draftPercent).ToString()));
			}
		}

		if((straightcounter + brakingPoint[straight-1]) > straightLength[straight-1]){
			turnSpeed += 0.01f;
		}

		if( straightcounter > straightLength[straight-1]){
			Movement.onTurn = true;
			AIMovement.onTurn = true;
			if(cameraRotate == 1){
				if(turnDir[turn-1] == 1){
					TDCamera.transform.Rotate(0,0,(1.0f/turnAngle[turn-1]));
				} else {
					TDCamera.transform.Rotate(0,0,(-1.0f/turnAngle[turn-1]));
				}
			}
			cornerKerb.GetComponent<Renderer>().enabled = true;
			apron.GetComponent<Renderer>().enabled = true;
			//kerbBlur+=kerbBlur;
			cornerKerb.GetComponent<Renderer>().material.mainTextureOffset = new Vector2(kerbBlur,0);
			cornercounter++;
		}

		if(cornercounter + accelPoint[turn-1] >= (turnLength[turn-1] * turnAngle[turn-1])){
			turnSpeed-=0.01f;
		}

		if(cornercounter >= (turnLength[turn-1] * turnAngle[turn-1])){
			//Scoreboard.checkPositions();
			Scoreboard.updateScoreboard();
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
}
