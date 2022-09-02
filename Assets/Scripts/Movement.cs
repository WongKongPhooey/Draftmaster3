using UnityEngine;
using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using TMPro;

using Random=UnityEngine.Random;

public class Movement : MonoBehaviour {

	public GameObject vehicle;
	public static float playerSpeed;
	public float gettableSpeed;
	public float topSpeed;
	public static float speedRand;
	public static float randTopend;
	float speed;
	float draftDist;

	float challengeSpeedBoost;

	int customAccel;
	int customSpeed;
	int customDist;
	int customStrength;

	float customAccelF;
	float customSpeedF;
	float customDistF;
	float customStrengthF;

	float customAccelFactor;
	float customSpeedFactor;
	float customDistFactor;
	float customStrengthFactor;

	int circuitLanes;
	float apronLineX;

	string seriesPrefix;
	int customNum;

	public static int lane = 2;
	public static int laneInv;
	public static int laneticker;
	public static int laneBias;
	public static int laneChangeDuration;
	public static int laneChangeBackout;
	public int dooredStrength;
	float laneChangeSpeed;
	float vStrongLane;
	float strongLane;
	float weakLane;
	float vWeakLane;
	float weakestLane;
	float laneFactor;

	public static bool onTurn;
	public static bool brakesOn;

	public static bool isWrecking;
	public static bool wreckOver;
	float baseDecel;
	public static float playerWreckDecel;
	float wreckAngle;
	float wreckTorque;
	int wreckHits;
	float wreckDamage;
	
	float targetForce;
	float windForce;
	float forceSmoothing;
	
	int sparksCooldown;
	
	public GameObject audioHolder;
	
	AudioSource carEngine;
	float engineRevs;
	int revbarOffset;
	
	public static int speedOffset;
	
	int[] gearSpeeds = {999, 190, 110, 50, 25};
	
	public static GameObject HUD;
	public static GameObject HUDControls;
	
	public GameObject HUDGear;
	public GameObject HUDRevs;
	public GameObject HUDRevBarMask;
	public GameObject HUDDraftBarMask;
	public GameObject HUDSpeed;
	public GameObject HUDAccSpeed;
	public GameObject HUDLastLap;
	public GameObject HUDBestLap;
	public GameObject HUDLapDelta;
	public float calcLapDelta;
	public float HUDDraftStrength;
	
	public static bool carInside;
	public static bool carOutside;
	public static bool almostInside;
	public static bool almostOutside;
	public static string spotterCall;
	
	public string carName;
	public int carNum;
	public int carClass;
	public string carTeam;
	public string carManu;
	public int carRarity;
	public int carNumber;
	
	public bool backingOut = false;
	public bool tandemDraft = false;
	public int tandemPosition;
	public bool initialContact = false;

	int wobbleCount;
	int wobblePos;
	int wobbleTarget;
	int wobbleRand;

	bool draftChallenge;
	public static string draftBuddy;
	public static string draftBuddyF;
	public static string draftBuddyR;
	public static int buddyInFront;
	public static int buddyBehind;
	public static bool canBuddy = false;
	public static bool buddyUp = false;
	public static int buddiedTime;
	public static int buddyMax;
	public static bool pushingPartner;
	public static float draftCounter;
	public static int raceCounter;
	public static float draftPercent;

	public Dictionary<string, int> incidents = new Dictionary<string, int>();
	public List<string> rivals = new List<string>();
	public static int mostHits;
	public static string currentRival;
	
	public static bool pacing;
	
	public GameObject cautionSummaryMenu;
	
	public static bool delicateMod;
	public static bool invincibleMod;

	// Use this for initialization
	void Start () {
		playerSpeed = 203;
		gettableSpeed = playerSpeed;
		topSpeed = 208f + speedRand;
		speedRand = Random.Range(0,50);
		speedRand = speedRand / 100;
		randTopend = Random.Range(0,99);
		randTopend = randTopend / 1000;
		laneticker = 0;
		onTurn = false;
		lane = SpawnField.startLane;
		laneBias = 0;
		challengeSpeedBoost = 0;
		tandemPosition = 1;
		
		speedOffset = PlayerPrefs.GetInt("SpeedOffset");
		
		isWrecking = false;
		wreckOver = false;
		playerWreckDecel = 0;
		sparksCooldown = 0;
		
		pacing = true;
		
		HUD = GameObject.Find("HUD");
		HUDControls = GameObject.Find("Controls");
		cautionSummaryMenu = GameObject.Find("CautionMenu");
		HUD.SetActive(false);
		HUDControls.SetActive(false);
		cautionSummaryMenu.SetActive(false);
		
		revbarOffset = 10;
		
		seriesPrefix = PlayerPrefs.GetString("carSeries");
		
		carName = PlayerPrefs.GetString("carTexture");
		
		string splitAfter = "livery";
		string carNumStr = carName.Substring(carName.IndexOf(splitAfter) + splitAfter.Length);
		
		bool findCarNum = int.TryParse(Regex.Replace(carNumStr, "[^0-9]", ""), out carNum);
		if(findCarNum == true){
		    carClass = PlayerPrefs.GetInt(seriesPrefix + carNum + "Class");
			carTeam = DriverNames.cup2020Teams[carNum];
			carManu = DriverNames.cup2020Manufacturer[carNum];
			carRarity = DriverNames.cup2020Rarity[carNum];
		} else {
			carRarity = 0;
			//Debug.Log("Invalid Car #");
		}
		
		Renderer liveryRend = this.transform.Find("Livery").GetComponent<Renderer>();
		Renderer numRend = this.transform.Find("Number").GetComponent<Renderer>();
		
		if(PlayerPrefs.HasKey(seriesPrefix + carNum + "AltPaint")){
			liveryRend.material.mainTexture = Resources.Load(seriesPrefix + "livery" + carNum + "alt" + PlayerPrefs.GetInt(seriesPrefix + carNum + "AltPaint")) as Texture;
		} else {
			liveryRend.material.mainTexture = Resources.Load(PlayerPrefs.GetString("carTexture")) as Texture;
		}
		
		if(PlayerPrefs.HasKey("CustomNumber" + seriesPrefix + carNum)){
			if(PlayerPrefs.HasKey(seriesPrefix + carNum + "AltPaint")){
				liveryRend.material.mainTexture = Resources.Load(seriesPrefix + "livery" + carNum + "blankalt" + PlayerPrefs.GetInt(seriesPrefix + carNum + "AltPaint")) as Texture;
			} else {
				liveryRend.material.mainTexture = Resources.Load(seriesPrefix + "livery" + carNum + "blank") as Texture;
			}
			customNum = PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + carNum);
			numRend.material.mainTexture = Resources.Load("cup20num" + customNum) as Texture;
		
			//Debug.Log("Player #" + customNum + " applied Var: " + seriesPrefix + "num" + customNum);
			numRend.enabled = true;
		} else {
			numRend.enabled = false;
		}
			
		if(seriesPrefix == "cup22"){
			this.transform.Find("Number").Translate(0.1f,0f,0f);
		}
				
		circuitLanes = PlayerPrefs.GetInt("CircuitLanes");
		
		apronLineX = -2.7f;
		apronLineX = 1.2f - ((circuitLanes - 1) * 1.2f) - 0.3f;

		brakesOn = false;
		draftBuddy = "";
		buddiedTime = 0;
		buddyMax = 10000;
		incidents.Clear();
		currentRival = "";
		mostHits = 0;
		rivals = new List<string>();
		if(PlayerPrefs.GetString("ChallengeType")=="TeamPlayer"){
			draftChallenge = true;
		}
		draftCounter = 0;
		raceCounter = 0;

		if(PlayerPrefs.GetString("ChallengeType")=="CleanBreak"){
			challengeSpeedBoost = 1.0f;
		}

		if (DriverNames.cup2020Types[carNum] == "Strategist"){
			switch(carClass){
				case 1:
					laneChangeDuration = 75;
					laneChangeSpeed = 0.016f;
					laneChangeBackout = 30;
					break;
				case 2:
					laneChangeDuration = 64;
					laneChangeSpeed = 0.01875f;
					laneChangeBackout = 28;
					break;
				case 3:
					laneChangeDuration = 60;
					laneChangeSpeed = 0.02f;
					laneChangeBackout = 24;
					break;
				case 4:
					laneChangeDuration = 50;
					laneChangeSpeed = 0.024f;
					laneChangeBackout = 20;
					break;
				case 5:
					laneChangeDuration = 48;
					laneChangeSpeed = 0.025f;
					laneChangeBackout = 16;
					break;
				case 6:
					laneChangeDuration = 40;
					laneChangeSpeed = 0.030f;
					laneChangeBackout = 14;
					break;
				case 7:
					laneChangeDuration = 36;
					laneChangeSpeed = 0.0333333f;
					laneChangeBackout = 12;
					break;
				case 8:
					laneChangeDuration = 32;
					laneChangeSpeed = 0.0375f;
					laneChangeBackout = 12;
					break;
				default:
					laneChangeDuration = 80;
					laneChangeSpeed = 0.015f;
					laneChangeBackout = 32;
					break;
			}
		} else {
			laneChangeDuration = 80;
			laneChangeSpeed = 0.015f;
			laneChangeBackout = 32;
		}

		dooredStrength = 40;
		if (DriverNames.cup2020Types[carNum] == "Intimidator"){
			dooredStrength = 50 + (carRarity * 5) + (carClass * 5);
			if(dooredStrength > 95){
				dooredStrength = 95;
			}
		}

		if(PlayerPrefs.HasKey("CustomAcceleration")){
			customAccel = PlayerPrefs.GetInt("CustomAcceleration");
			customAccelFactor = 50000;
			customAccelF = (customAccel - 50)/customAccelFactor;
		} else {
			customAccel = 0;
			customAccelF = 0;
		}
		
		if(PlayerPrefs.HasKey("CustomMaxSpeed")){
			customSpeed = PlayerPrefs.GetInt("CustomMaxSpeed");
			customSpeedFactor = 10;
			customSpeedF = (customSpeed - 50)/customSpeedFactor;
		} else {
			customSpeed = 0;
			customSpeedF = 0;
		}

		if(PlayerPrefs.HasKey("CustomDraftStrength")){
			customStrength = PlayerPrefs.GetInt("CustomDraftStrength");
			customStrengthFactor = 5000;
			customStrengthF = (customStrength - 50)/customStrengthFactor;
		} else {
			customStrength = 0;
			customStrengthFactor = 0;
		}

		if(PlayerPrefs.HasKey("CustomDraftDistance")){
			customDist = PlayerPrefs.GetInt("CustomDraftDistance");
			customDistFactor = 10;
			customDistF = (customDist - 50)/customDistFactor;
		} else {
			customDist = 0;
			customDistFactor = 0;
		}

		wobbleCount = 1;
		wobblePos = 0;
		wobbleTarget = 0;
		wobbleRand = Random.Range(100,500);
		
		delicateMod = false;
		invincibleMod = false;
		
		RaceModifiers.checkModifiers();
	}
	
	void OnCollisionEnter (Collision carHit){
		
		if(isWrecking == true){
			wreckHits++;
			if(carHit.gameObject.tag == "AICar"){
				float hitSpeed = carHit.gameObject.GetComponent<AIMovement>().wreckDecel;
				float netSpeed;
				if(playerWreckDecel != 0){
					netSpeed = hitSpeed - playerWreckDecel;
				} else {
					netSpeed = hitSpeed - 200;
				}
				if(netSpeed < 0){
					netSpeed = -netSpeed;
				}
				wreckDamage += netSpeed;
			} else {
				wreckDamage += (200 - playerWreckDecel) / 100;
			}
		}
		
		//Debug.Log("Hit " + carHit.gameObject.name);
		if((carHit.gameObject.tag == "AICar") || 
		   (carHit.gameObject.tag == "Barrier") || 
		   (carHit.gameObject.name == "OuterWall")){
			
			if((isWrecking == false)&&(wreckOver == false)&&(delicateMod == true)){
				startWreck();
			}
			   
			//Join wreck
			if(carHit.gameObject.tag == "AICar"){
				bool joinWreck = carHit.gameObject.GetComponent<AIMovement>().isWrecking;
				if(joinWreck == true){
					if(isWrecking == false){
						if(wreckOver == false){
							startWreck();
							this.transform.Find("TireSmoke").GetComponent<ParticleSystem>().Play();
						}
					} else {
						//Share some wreck inertia
						float opponentWreckDecel = carHit.gameObject.GetComponent<AIMovement>().wreckDecel;
						playerWreckDecel += ((opponentWreckDecel - playerWreckDecel) / 2);
					}
				}
			}
			   
			if((laneticker != 0)&&(backingOut == false)){
				if(laneticker > 0){
					bool leftSideHit = checkRaycast("LeftCorners", 0.51f);
					if(leftSideHit == true){
						backingOut = true;
						RaceHUD.tutorialBackingOut = true;
						laneticker = -laneChangeDuration + laneticker;
						lane--;
					}
				} else {
					if(laneticker < 0){
						bool rightSideHit = checkRaycast("RightCorners", 0.51f);
						if(rightSideHit == true){
							backingOut = true;
							RaceHUD.tutorialBackingOut = true;
							laneticker = laneChangeDuration + laneticker;
							lane++;
						}
					}
				}
			}
			if(carHit.gameObject.name == "OuterWall"){
				playerSpeed-=0.5f;
			}
		}
		playerSpeed-=0.5f;
	}
	
	void OnCollisionExit (Collision carHit){
		if(isWrecking == true){
			playerWreckDecel+= Random.Range(2f,5f);
		}
	}
	
	void ReceivePush(float bumpSpeed){
		if(isWrecking == false){
			//Debug.Log("Thanks for the push! Hit me at " + bumpSpeed + "while I was going " + playerSpeed);
			if(initialContact == false){
				float midSpeed = bumpSpeed - playerSpeed;
				playerSpeed += midSpeed/4;
				initialContact = true;
				//Debug.Log("Impact levels out Player");
			} else {
				//Send it back
				RaycastHit DraftCheckBackward;
				bool HitBackward = Physics.Raycast(transform.position, transform.forward * -1, out DraftCheckBackward, 1.1f);
				DraftCheckBackward.transform.gameObject.SendMessage("UpdateTandemPosition",tandemPosition);
				DraftCheckBackward.transform.gameObject.SendMessage("GivePush",playerSpeed);
			}
		}
	}
	
	void GivePush(float bumpSpeed){
		playerSpeed = bumpSpeed;
		if(tandemPosition > 2){
			playerSpeed-= 0.25f;
			//Debug.Log("Player speed reduced by " + (bumpSpeed - playerSpeed) + "");
		}
	}
	
	void UpdateTandemPosition(int tandemPosInFront){
		tandemPosition = tandemPosInFront + 1;
		//Debug.Log("Player is in tandem position " + tandemPosition + "");
	}
	
	// Update is called once per frame
	void FixedUpdate () {
			
		updateHUD();
		
		if(pacing == true){
			playerSpeed = 200;
			HUD.SetActive(false);
			HUDControls.SetActive(false);
			return;
		}
		
		if((isWrecking == true)||(wreckOver == true)){
			//Bail, drop all Movement logic
			if(wreckOver == false){
				wreckPhysics();
				if(playerSpeed > 200){
					playerSpeed-=0.005f;
				}
			} else {
				playerSpeed = 0;
				targetForce = 0;
				updateWindForce();
				this.GetComponent<ConstantForce>().force = new Vector3(0f,0f,windForce);
				this.transform.Find("TireSmoke").GetComponent<ParticleSystem>().Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
			}
			return;
		}
				
		laneInv = 4 - lane;
		laneFactor = 10000;
		weakestLane = laneInv/laneFactor;
		laneFactor = 200;
		vWeakLane = laneInv/laneFactor;
		laneFactor = 10;
		weakLane = laneInv/laneFactor;
		laneFactor = 5;
		strongLane = laneInv/laneFactor;
		laneFactor = 2;
		vStrongLane = laneInv/laneFactor;
		
		RaycastHit DraftCheckForward;
        RaycastHit DraftCheckBackward;

		bool HitForward = Physics.Raycast(transform.position, transform.forward, out DraftCheckForward, 100);
        bool HitBackward = Physics.Raycast(transform.position, transform.forward * -1, out DraftCheckBackward, 100);

		//Frontward Draft
		Debug.DrawRay (transform.position + new Vector3(0.0f, 0.0f, 1f), Vector3.forward * 8, Color.green);
		
		//Leftward Cast
		Debug.DrawRay (transform.position, Vector3.left * 1, Color.yellow);
		
		//Rightward Cast
		Debug.DrawRay (transform.position, Vector3.right * 1, Color.yellow);
		
		//Backdraft
		Debug.DrawRay (transform.position, Vector3.back * 2, Color.red);
		
		RaycastHit DraftCheck;

		laneBias = 0;

		//Speed up if under max non-draft speed
		if(playerSpeed < 199){
			playerSpeed=199f;
		}
		
		//If in draft of car in front
		if (Physics.Raycast(transform.position,transform.forward, out DraftCheck, 10 + customDistF)){
			draftDist = DraftCheck.distance;
			//Speed up
			if(playerSpeed <= topSpeed + (carRarity/5f) + randTopend){
				playerSpeed+=((10 - DraftCheck.distance)/1500);
			} else {
				playerSpeed-=((playerSpeed - topSpeed)/200);
			}
			if(PlayerPrefs.GetInt("TutorialActive") == 1){
				if(RaceHUD.tutorialStage == 3){
					RaceHUD.tutorialDraftingCount++;
				}
			}
		} else {
			//Slow down if not in any draft
			if(playerSpeed >= 200){
				playerSpeed-=0.0035f;
			}
			//Empty the Draft Bar
			draftDist= 10 + customDistF;
		}
		
		// If recieving backdraft of car behind
		if (Physics.Raycast(transform.position,transform.forward * -1, out DraftCheck, 1.5f)){
			//Speed up
			if(playerSpeed <= (205f + (speedRand + customSpeedF) + challengeSpeedBoost + laneInv + carRarity + randTopend)){
				playerSpeed+=(0.004f + customAccelF);
				if(RaceHUD.tutorialStage == 4){
					RaceHUD.tutorialBackdraftCount++;
				}
			}
		}

		// If being bump-drafted from behind
		if (Physics.Raycast(transform.position,transform.forward * -1, out DraftCheck, 1.01f)){
			//Speed up
			playerSpeed+=0.003f;
			if(RaceHUD.tutorialStage == 4){
				RaceHUD.tutorialBackdraftCount++;
			}
			//DraftCheckBackward.transform.gameObject.SendMessage("GivePush",playerSpeed);
			tandemDraft = true;
		} else {
			tandemDraft = false;
			initialContact = false;
		}

		// If bump-drafting the car in front
		if (Physics.Raycast(transform.position,transform.forward, out DraftCheck, 1.01f)){
			if(isWrecking == false){
				//Debug.Log("AI " + DraftCheck.transform.gameObject.name + " is bumped to match speed of " + playerSpeed);
				if(DraftCheck.transform.gameObject.tag == "AICar"){
					DraftCheckForward.transform.gameObject.SendMessage("ReceivePush",playerSpeed);
				}
			}
			//Bump drafting speeds both up
			playerSpeed+=0.004f;

		} else {
			tandemDraft = false;
			tandemPosition = 1;
		}
		
		// Draft Behind A Buddy
		if (Physics.Raycast(transform.position,transform.forward, out DraftCheck, 2.8f)){
			
			string frontBuddy = DraftCheck.collider.gameObject.name;
			if(draftBuddyF != frontBuddy){
				draftBuddyF = frontBuddy;
				buddyInFront = 0;
			} else {
				buddyInFront++;
				if((buddyInFront >= 500)||(buddyBehind >= 500)){
					canBuddy = true;
					buddiedTime = 0;
				}
			}
		} else {
			buddyInFront = 0;
		}
		
		if(brakesOn == true){
			if(playerSpeed > 195){
				playerSpeed-=0.025f;
			}
		}
		
		// Receive A Buddy Backdraft
		if (Physics.Raycast(transform.position,transform.forward * -1, out DraftCheck, 2.8f)){
			string rearBuddy = DraftCheck.collider.gameObject.name;
			if(draftBuddyR != rearBuddy){
				draftBuddyR = rearBuddy;
				buddyBehind = 0;
			} else {
				buddyBehind++;
				
				//Look for teammates
				RaycastHit DraftBehind;
				string draftTeam = "NAN";
				string draftManu = "NAN";
				
				if(DraftCheck.transform.GetComponent<AIMovement>()){
					draftTeam = DraftCheck.transform.GetComponent<AIMovement>().carTeam;
					draftManu = DraftCheck.transform.GetComponent<AIMovement>().carManu;
				}
				
				if(carTeam == draftTeam){
					canBuddy = true;
					//buddyCrit = "Team Draft #" + draftNum + "";
					//Debug.Log("Draft from a " + draftTeam + " team mate to my " + carTeam + " car");
				} else {
					//Debug.Log("No draft from a " + draftTeam + " car to your " + carTeam + " car");
				}
				
				if(carManu == draftManu){
					canBuddy = true;
					buddiedTime = 0;
					buddyBehind = 999;
					//buddyCrit = "Team Draft #" + draftNum + "";
					//Debug.Log("Draft from a " + draftManu + " team mate to my " + carManu + " car");
				} else {
					//Debug.Log("No draft from a " + draftManu + " car to your " + carManu + " car");
				}
					
				//Resetter
				if((buddyInFront >= 500)||(buddyBehind >= 500)){
					canBuddy = true;
					buddiedTime = 0;
				}
			}
		} else {
			buddyBehind = 0;
		}
		
		if((buddyInFront < 500)&&(buddyBehind < 500)){
			canBuddy = false;
			buddyUp = false;
		}
		if((buddyInFront >= 500)||(buddyBehind >= 500)){
			if(buddyInFront >= buddyBehind){
				draftBuddy = draftBuddyF;
			} else {
				draftBuddy = draftBuddyR;
			}
		}
		
		if(buddyUp == true){
			buddiedTime++;
		}
		
		//Buddies will not help you forever
		if(buddiedTime >= buddyMax){
			canBuddy = false;
			buddyUp = false;
		}
		
		//Buddies won't help you if you're the leader
		if(Scoreboard.position == 1){
			canBuddy = false;
			buddyUp = false;
		}
		
		//Last lap
		if(CameraRotate.lap == PlayerPrefs.GetInt("RaceLaps")){
			canBuddy = false;
			buddyUp = false;
		}

		if(draftChallenge == true){
			if(pushingPartner == true){
				draftCounter++;
			}
			raceCounter++;
			draftPercent = (draftCounter / raceCounter) * 100;
		}
		
		//GUI.Label(new Rect(Screen.width - (widthblock * 3.5f),heightblock * 3.25f, widthblock * 3f, heightblock * 1f), "Spd:" + (Movement.playerSpeed - speedOffset - CameraRotate.carSpeedOffset).ToString("F2") + "MpH");
		//GUI.Label(new Rect(Screen.width - (widthblock * 3.5f),heightblock * 4.25f, widthblock * 3f, heightblock * 1f), "This:" + (CameraRotate.averageSpeed - speedOffset).ToString("F2") + "MpH");
		//GUI.Label(new Rect(Screen.width - (widthblock * 3.5f),heightblock * 5.25f, widthblock * 3f, heightblock * 1f), "Best:" + (CameraRotate.lapRecord - speedOffset).ToString("F2") + "MpH");
		

		wobbleCount++;
		
		if(wobbleCount >= wobbleRand){
			wobbleRand = Random.Range(10,60);
			wobbleTarget = Random.Range(-110,110);
			wobbleCount = 1;
		}
		
		//General wobble while in lane
		if(wobbleTarget > wobblePos){
			vehicle.transform.Translate(-0.001f,0,0);
			wobblePos++;
		}
		if(wobbleTarget < wobblePos){
			vehicle.transform.Translate(0.001f,0,0);
			wobblePos--;
		}

		updateMovement();
		
		if(((laneticker == 2)||(laneticker == -2))&&(RaceHUD.tutorialBackingOut == false)){
			if(RaceHUD.tutorialStage == 1){
				RaceHUD.tutorialSteeringCount++;
			}
		}
		
		//Speed tops out
				//Speed tops out
        if (playerSpeed > (205 + carRarity + laneInv)){
			//Reduce speed, proportionate to the amount 'over'
            playerSpeed -= ((playerSpeed - 205) / 200);
		}
		if(playerSpeed > 210){
			playerSpeed=210f;
		}

		//Caution decel
		if(RaceHUD.caution == true){
			if(playerSpeed > 200){
				playerSpeed-=0.02f;
			} else {
				playerSpeed = 200;
			}
		}
		
		gettableSpeed = playerSpeed;
		
		//Speed difference between the Player and the Control Car
		//speed = (AISpeed + wreckDecel) - (Movement.playerSpeed + Movement.playerWreckDecel);
		speed = (playerSpeed + playerWreckDecel) - ControlCarMovement.controlSpeed;
		speed = speed / 100;
		vehicle.transform.Translate(0, 0, speed);
	}
	
	public static void pacingEnds(){
		pacing = false;
		HUD.SetActive(true);
		HUDControls.SetActive(true);
	}
	
	void wreckSpeed(){
		//Speed difference between the Player and the Control Car
		speed = (playerSpeed + playerWreckDecel) - ControlCarMovement.controlSpeed;
		speed = speed / 100;
		vehicle.transform.Translate(new Vector3(0, 0, speed),Space.World);
	}
	
	void updateMovement() {
		
		//How fast can you switch lanes
        if (laneticker > 0){
			bool leftCastHit = checkRaycast("LeftCorners", 0.51f);
			if(leftCastHit == true){
				backingOut = true;
				laneticker = -laneChangeDuration + laneticker;
				lane--;
			} else {
				vehicle.transform.Translate(-laneChangeSpeed, 0, 0);
				laneticker--;
			}
        }

        if (laneticker < 0){
			bool rightCastHit = checkRaycast("RightCorners", 0.51f);
			if(rightCastHit == true){
				backingOut = true;
				laneticker = laneChangeDuration + laneticker;
				lane++;
			} else {
				vehicle.transform.Translate(laneChangeSpeed, 0, 0);
				laneticker++;
			}
        }

        if (laneticker == 0)
        {
			//movingLane = false;
            backingOut = false;
        }
		
		if(vehicle.transform.position.x <= apronLineX){
			//Debug.Log("Track Limits!");
			if (backingOut == false) {
				backingOut = true;
			}
			laneticker = -laneChangeDuration + laneticker;
			lane--;
		}
		
		if(vehicle.transform.position.x >= 1.35f){
			//Debug.Log("Wall!");
			if (backingOut == false) {
				backingOut = true;
				if(CameraRotate.onTurn == true){
					startWreck();
				}
			}
			laneticker = laneChangeDuration + laneticker;
			lane++;
			//Wall hit decel
			playerSpeed -= 1f;
		}
	}
	
	public static void changeLaneLeft(){
		if(laneticker == 0){
			lane++;
			laneticker = laneChangeDuration;
			//Debug.Log("Goin' Left!");
		}
	}
	
	public static void changeLaneRight(){
		if(laneticker == 0){
			lane--;
			laneticker = -laneChangeDuration;
			//Debug.Log("Goin' Right!");
		}
	}
	
	public static void holdBrake(){
		brakesOn = true;
	}
	
	public static void releaseBrake(){
		brakesOn = false;
	}
	
	void updateHUD(){
		
		carEngine = audioHolder.GetComponent<AudioSource>();
		
		//4th GEAR
		if((CameraRotate.carSpeedOffset - playerWreckDecel) < gearSpeeds[4]){
			//Calculate revs
			engineRevs = 5000;
			engineRevs+=((gearSpeeds[4] - CameraRotate.carSpeedOffset) * 100);
			engineRevs+=(playerSpeed - 195) * 200;
			
			carEngine.pitch = 0.7f + (engineRevs / 10000f);
			carEngine.pitch = 0.7f + (engineRevs / 10000f) - ((CameraRotate.carSpeedOffset - playerWreckDecel) / 200);
			HUDGear.GetComponent<TMPro.TMP_Text>().text = "GEAR 4";
			HUDRevs.GetComponent<TMPro.TMP_Text>().text = "" + engineRevs.ToString("F0") + " RPM";
			HUDRevBarMask.GetComponent<RectTransform>().sizeDelta = new Vector2(((10000 - engineRevs) / 25) + revbarOffset, 40);
		} else {
			if((CameraRotate.carSpeedOffset - playerWreckDecel) < gearSpeeds[3]){
				//3rd GEAR
				engineRevs = 5000;
				engineRevs+=((gearSpeeds[3] - CameraRotate.carSpeedOffset) * 100);
				engineRevs+=(playerSpeed - 195) * 100;
				
				carEngine.pitch = 1.4f + ((playerSpeed - 200) / 10) - ((CameraRotate.carSpeedOffset - playerWreckDecel) / 200);
				HUDGear.GetComponent<TMPro.TMP_Text>().text = "GEAR 3";
				HUDRevs.GetComponent<TMPro.TMP_Text>().text = "" + engineRevs.ToString("F0") + " RPM";
				HUDRevBarMask.GetComponent<RectTransform>().sizeDelta = new Vector2(((10000 - engineRevs) / 25) + revbarOffset, 40);
			} else {
				//2nd GEAR 
				if((CameraRotate.carSpeedOffset - playerWreckDecel) < gearSpeeds[2]){
					engineRevs = 2000;
					engineRevs+=((gearSpeeds[2] - CameraRotate.carSpeedOffset) * 100);
					engineRevs+=(playerSpeed - 195) * 100;
					
					carEngine.pitch = 1.6f + ((playerSpeed - 200) / 5) - ((CameraRotate.carSpeedOffset - playerWreckDecel) / 200);
					HUDGear.GetComponent<TMPro.TMP_Text>().text = "GEAR 2";
					HUDRevs.GetComponent<TMPro.TMP_Text>().text = "" + engineRevs.ToString("F0") + " RPM";
					HUDRevBarMask.GetComponent<RectTransform>().sizeDelta = new Vector2(((10000 - engineRevs) / 25) + revbarOffset, 40);
				} else {
					//1st GEAR (wrecks only)
					engineRevs = 0;
					engineRevs+=((gearSpeeds[1] - (CameraRotate.carSpeedOffset - playerWreckDecel)) * 100);
					engineRevs+=(playerSpeed - 195) * 100;
					if(engineRevs < 0){
						engineRevs = 0;
					}
					
					carEngine.pitch = 1.6f + ((playerSpeed - 200) / 5) - ((CameraRotate.carSpeedOffset - playerWreckDecel) / 200);
					HUDGear.GetComponent<TMPro.TMP_Text>().text = "GEAR 2";
					HUDRevs.GetComponent<TMPro.TMP_Text>().text = "" + engineRevs.ToString("F0") + " RPM";
					HUDRevBarMask.GetComponent<RectTransform>().sizeDelta = new Vector2(((10000 - engineRevs) / 25) + revbarOffset, 40);
				}
			}
		}
		
		
		if(((playerSpeed - speedOffset - CameraRotate.carSpeedOffset) + playerWreckDecel) > 0){
			HUDSpeed.GetComponent<TMPro.TMP_Text>().text = "SPD " + ((playerSpeed - speedOffset - CameraRotate.carSpeedOffset) + playerWreckDecel).ToString("F0");
			HUDAccSpeed.GetComponent<TMPro.TMP_Text>().text = "SPD " + ((playerSpeed - speedOffset - CameraRotate.carSpeedOffset) + playerWreckDecel).ToString("F2");
		}
		
		if(wreckOver == true){
			carEngine.pitch = 0f;
			HUDGear.GetComponent<TMPro.TMP_Text>().text = "NO GEAR";
			HUDRevs.GetComponent<TMPro.TMP_Text>().text = "0 RPM";
			HUDRevBarMask.GetComponent<RectTransform>().sizeDelta = new Vector2(430, 40);
			HUDSpeed.GetComponent<TMPro.TMP_Text>().text = "SPD 0";
			HUDAccSpeed.GetComponent<TMPro.TMP_Text>().text = "SPD 0";
		}

		if(draftDist > HUDDraftStrength){
			HUDDraftStrength+=0.5f;
		}
		if(draftDist < HUDDraftStrength){
			HUDDraftStrength-=0.5f;
		}
		
		if((isWrecking == false)&&(wreckOver == false)){
			HUDDraftBarMask.GetComponent<RectTransform>().sizeDelta = new Vector2(80, (120f / (10f + customDistF)) * HUDDraftStrength);
		} else {
			//Cover it completely
			HUDDraftBarMask.GetComponent<RectTransform>().sizeDelta = new Vector2(80, 120f);
		}

		HUDLastLap.GetComponent<TMPro.TMP_Text>().text = "LAP " + (CameraRotate.averageSpeed - speedOffset).ToString("F2");
		HUDBestLap.GetComponent<TMPro.TMP_Text>().text = "BEST " + (CameraRotate.lapRecord - speedOffset).ToString("F2");
		
		calcLapDelta = (CameraRotate.lapRecord - speedOffset)-(CameraRotate.averageSpeed - speedOffset);
		if(calcLapDelta<0){
			HUDLapDelta.GetComponent<TMPro.TMP_Text>().text = "DLT (" + calcLapDelta.ToString("F2") + ")";
		} else {
			HUDLapDelta.GetComponent<TMPro.TMP_Text>().text = "DLT (+" + calcLapDelta.ToString("F2") + ")";
		}
	}
	
	bool checkRaycast(string rayDirection, float rayLength){
		bool rayHit;
		RaycastHit DraftCheck;
		if(rayLength == null){
			rayLength = 100;
		}
		switch(rayDirection){
			case "Front":
				rayHit = Physics.Raycast(transform.position, transform.forward, out DraftCheck, rayLength);
				break;
			case "Rear":
				rayHit = Physics.Raycast(transform.position, transform.forward * -1, out DraftCheck, rayLength);
				break;
			case "Left":
				rayHit = Physics.Raycast(transform.position, transform.right * -1, out DraftCheck, rayLength);
				break;
			case "Right":
				rayHit = Physics.Raycast(transform.position, transform.right, out DraftCheck, rayLength);
				break;
			case "LeftDiags":
				rayHit = Physics.Raycast(transform.position, transform.forward - transform.right, out DraftCheck, rayLength);
				rayHit = Physics.Raycast(transform.position, (transform.forward * -1) - transform.right, out DraftCheck, rayLength);
				break;
			case "RightDiags":
				rayHit = Physics.Raycast(transform.position, transform.forward + transform.right, out DraftCheck, rayLength);
				rayHit = Physics.Raycast(transform.position, (transform.forward * -1) + transform.right, out DraftCheck, rayLength);
				break;
			case "LeftCorners":
				rayHit = Physics.Raycast(transform.position + new Vector3(0,0,0.98f), transform.right * -1, out DraftCheck, rayLength);
				Debug.DrawRay(transform.position + new Vector3(0,0,0.98f), transform.right * -0.52f, Color.yellow);
				if(rayHit == false){
					rayHit = Physics.Raycast(transform.position + new Vector3(0,0,-0.98f), transform.right * -1, out DraftCheck, rayLength);
					Debug.DrawRay(transform.position + new Vector3(0,0,0.98f), transform.right * -0.52f, Color.yellow);
				}
				break;
			case "RightCorners":
				rayHit = Physics.Raycast(transform.position + new Vector3(0,0,0.98f), transform.right, out DraftCheck, rayLength);
				Debug.DrawRay(transform.position + new Vector3(0,0,0.98f), transform.right * 0.52f, Color.yellow);
				if(rayHit == false){
					rayHit = Physics.Raycast(transform.position + new Vector3(0,0,-0.98f), transform.right, out DraftCheck, rayLength);
					Debug.DrawRay(transform.position + new Vector3(0,0,-0.98f), transform.right * 0.52f, Color.yellow);
				}
				break;
			case "LeftEdge":
				rayHit = Physics.Raycast(transform.position + new Vector3(-1f,0,-1f), transform.forward, out DraftCheck, rayLength);
				Debug.DrawRay(transform.position + new Vector3(-1f,0,-1f), Vector3.forward * 2, Color.red);
				break;
			case "RightEdge":
				rayHit = Physics.Raycast(transform.position + new Vector3(1f,0,-1f), transform.forward, out DraftCheck, rayLength);
				Debug.DrawRay(transform.position + new Vector3(1f,0,-1f), Vector3.forward * 2, Color.red);
				break;
			default:
				Debug.Log("Invalid Raycast Direction");
				rayHit = false;
				break;
		}
		return rayHit;
	}	
	
	void startWreck(){
		if(invincibleMod == true){
			return;
		}
		if(wreckOver == false){
			isWrecking = true;
		}
		if(CameraRotate.cautionOut == false){
			CameraRotate.cautionOut = true;
		}
		sparksCooldown = 99999;
		//Debug.Log(this.name + " is wrecking");
		
		//Make the car light, more affected by physics
		this.GetComponent<Rigidbody>().mass = 5;
		
		//Remove constraints, allowing it to impact/spin using physics
		this.GetComponent<Rigidbody>().constraints &= ~RigidbodyConstraints.FreezeRotationY;
		this.GetComponent<Rigidbody>().constraints &= ~RigidbodyConstraints.FreezePositionX;
		
		//Remove forces, physics only
		this.GetComponent<Rigidbody>().isKinematic = false;
		this.GetComponent<Rigidbody>().useGravity = false;
		
		//Apply wind/drag
		//baseDecel = -1f * (float)(1 + CameraRotate.carSpeedOffset / 10f);
		baseDecel = -0.2f;
		playerWreckDecel = 0;
		targetForce = 0;
		forceSmoothing = 0.2f;
		windForce = targetForce;
		this.GetComponent<ConstantForce>().force = new Vector3(0f, 0f,targetForce);
		float wreckTorque = Random.Range(-0.1f, 0.1f) * 10;
		this.GetComponent<ConstantForce>().torque = new Vector3(0f, wreckTorque, 0f);
	}
	
	public void endWreck(){
		playerSpeed = 0;
		baseDecel = -0.2f;
		playerWreckDecel = 0;
		targetForce = 0;
		updateWindForce();
		isWrecking = false;
		wreckOver = true;
		//Debug.Log("WRECK OVER");
		this.GetComponent<Rigidbody>().mass = 25;
		this.GetComponent<Rigidbody>().isKinematic = true;
		//this.GetComponent<Rigidbody>().useGravity = true;
		this.GetComponent<ConstantForce>().force = new Vector3(0f, 0f,windForce);
		this.GetComponent<ConstantForce>().torque = new Vector3(0f, 0f, 0f);
		this.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezePositionZ;
		
		this.transform.Find("TireSmoke").GetComponent<ParticleSystem>().Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
		
		cautionSummaryMenu.SetActive(true);
	}
	
	void wreckPhysics(){
		wreckAngle = this.gameObject.transform.rotation.y;
		float wreckSine = Mathf.Sin(wreckAngle);
		if(wreckSine < 0){
			wreckSine = -wreckSine;
		}
		baseDecel-=0.2f;
		targetForce = playerWreckDecel;
		updateWindForce();
		if(CameraRotate.onTurn == true){
			//baseDecel-=0.02f * CameraRotate.currentTurnSharpness();
			//Debug.Log("Extra decel: " + (0.02f * CameraRotate.currentTurnSharpness()));
			this.GetComponent<ConstantForce>().force = new Vector3(10f, 0f,windForce);
			//Debug.Log("Apply side force to wreck on turn");
		} else {
			this.GetComponent<ConstantForce>().force = new Vector3(-2f, 0f,windForce);
		}
		playerWreckDecel = baseDecel - (50f * wreckSine);
		//Debug.Log("Wreck Decel: " + playerWreckDecel);
		if((playerSpeed - speedOffset - CameraRotate.carSpeedOffset) + windForce < 0){
			endWreck();
		}
		
		this.GetComponent<Rigidbody>().mass = (-playerWreckDecel / 10) + 5;
		this.GetComponent<Rigidbody>().angularDrag += 0.001f;
		
		this.transform.Find("SparksL").rotation = Quaternion.Euler(0,180,0);
		this.transform.Find("SparksR").rotation = Quaternion.Euler(0,180,0);
		Transform tireSmoke = this.transform.Find("TireSmoke");
		tireSmoke.rotation = Quaternion.Euler(0,180,0);
		float smokeMultiplier = Mathf.Sin(wreckAngle);
		if(smokeMultiplier < 0){
			smokeMultiplier = -smokeMultiplier;
		}
		smokeMultiplier = (smokeMultiplier * 60) + 0;
		smokeMultiplier = Mathf.Round(smokeMultiplier);
		tireSmoke.GetComponent<ParticleSystem>().startColor = new Color32(255,255,255,(byte)smokeMultiplier);
	}
	
	void updateWindForce(){
		if(windForce < targetForce - forceSmoothing){
			windForce += forceSmoothing;
		}
		if(windForce > targetForce + forceSmoothing){
			windForce -= forceSmoothing;
		}
	}
}