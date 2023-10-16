﻿using UnityEngine;
using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using TMPro;

using Random=UnityEngine.Random;

public class Movement : MonoBehaviour {

	public GameObject vehicle;
	public static float playerSpeed;
	public static float affectedPlayerSpeed;
	public static float speedoSpeed;
	public float gettableSpeed;
	public float topSpeed;
	float variTopSpeed;
	public static float speedRand;
	public static float randTopend;
	static float overspeed;
	float speed;
	float draftDist;

	float challengeSpeedBoost;
	float dominatorDrag;

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
	bool officialSeries;
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

	//These can adjust per race series (e.g. Indy)
	int seriesSpeedDiff;
	float draftStrengthRatio;
	float dragDecelMulti;
	float backdraftMulti;
	float bumpDraftDistTrigger;
	float draftAirCushion;
	float passDistMulti;
	float revLimiterBoost;

	public static bool isWrecking;
	public static bool wreckOver;
	float baseDecel;
	float slideX;
	public static float playerWreckDecel;
	float wreckAngle;
	float wreckTorque;
	int wreckHits;
	public static int totalWreckers;
	public static float wreckDamage;
	
	int wreckFreq;
	
	float targetForce;
	float windForce;
	float forceSmoothing;
	
	int sparksCooldown;
	
	public GameObject mainCam;
	public GameObject audioHolder;
	public static bool gamePausedLate;
	
	AudioSource carEngine;
	float engineRevs;
	int revbarOffset;
	
	public static int speedOffset;
	
	public static int currentGear;
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
	
	public Texture2D modTex;
	
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
	
	public GameObject pauseMenu;
	public GameObject cautionSummaryMenu;
	public GameObject cautionSummaryTotalWreckers;
	public GameObject cautionSummaryDamage;
	
	public static bool delicateMod;
	public static bool invincibleMod;
	public static bool suddenshowerMod;
	public static bool bulldozerMod;
	public static bool wallrideMod;
	
	public static bool momentChecks;
	public static bool fastestLapSaved;

	// Use this for initialization
	void Start () {
		playerSpeed = 203;
		gettableSpeed = playerSpeed;
		speedRand = Random.Range(0,50);
		speedRand = speedRand / 100;
		topSpeed = 208f + speedRand;
		randTopend = Random.Range(0,99);
		randTopend = randTopend / 1000;
		laneticker = 0;
		onTurn = false;
		lane = SpawnField.startLane;
		laneBias = 0;
		challengeSpeedBoost = 0;
		tandemPosition = 1;
		
		speedOffset = PlayerPrefs.GetInt("SpeedOffset");
		
		gamePausedLate = false;
		Time.timeScale = 1.0f;
		//safePause = false;
		
		isWrecking = false;
		wreckOver = false;
		playerWreckDecel = 0;
		sparksCooldown = 0;
		totalWreckers = 0;
		wreckDamage = 0;
		
		wreckFreq = PlayerPrefs.GetInt("WreckFreq");
		
		pacing = true;
		
		carEngine = audioHolder.GetComponent<AudioSource>();
		carEngine.volume = 0.15f;
		
		HUD = GameObject.Find("HUD");
		HUDControls = GameObject.Find("Controls");
		pauseMenu = GameObject.Find("PauseMenu");
		cautionSummaryMenu = GameObject.Find("CautionMenu");
		cautionSummaryTotalWreckers = GameObject.Find("CarsInvolved");
		cautionSummaryDamage = GameObject.Find("WreckDamage");
		mainCam = GameObject.Find("Main Camera");
		HUD.SetActive(false);
		HUDControls.SetActive(false);
		cautionSummaryMenu.SetActive(false);
		fastestLapSaved = false;
		
		revbarOffset = 10;
		
		seriesPrefix = PlayerPrefs.GetString("carSeries");
		
		if(DriverNames.isOfficialSeries(seriesPrefix) == true){
			officialSeries = true;
		} else {
			officialSeries = false;
		}
		
		setCarPhysics(seriesPrefix);
		
		carName = PlayerPrefs.GetString("carTexture");
		
		string splitAfter = "livery";
		string carNumStr = carName.Substring(carName.IndexOf(splitAfter) + splitAfter.Length);
		
		bool findCarNum = int.TryParse(Regex.Replace(carNumStr, "[^0-9]", ""), out carNum);
		if(findCarNum == true){
			PlayerPrefs.SetInt("carNumber",carNum);
			carClass = PlayerPrefs.GetInt(seriesPrefix + carNum + "Class");
			if(officialSeries == true){
				carTeam = DriverNames.getTeam(seriesPrefix,carNum);
				carManu = DriverNames.getManufacturer(seriesPrefix,carNum);
				carRarity = DriverNames.getRarity(seriesPrefix,carNum);
			} else {
				carTeam = ModData.getTeam(seriesPrefix,carNum);
				carManu = ModData.getManufacturer(seriesPrefix,carNum);
				carRarity = ModData.getRarity(seriesPrefix,carNum);
			}
		} else {
			carRarity = 0;
			//Debug.Log("Invalid Car #");
		}
		
		Renderer liveryRend = this.transform.Find("Livery").GetComponent<Renderer>();
		Renderer numRend = this.transform.Find("Number").GetComponent<Renderer>();
		
		if(PlayerPrefs.HasKey(seriesPrefix + carNum + "AltPaint")){
			liveryRend.material.mainTexture = Resources.Load(seriesPrefix + "livery" + carNum + "alt" + PlayerPrefs.GetInt(seriesPrefix + carNum + "AltPaint")) as Texture;
		} else {
			if(officialSeries == true){
				liveryRend.material.mainTexture = Resources.Load(PlayerPrefs.GetString("carTexture")) as Texture;
			} else {
				Debug.Log("Non official series, get mod texture");
				modTex = ModData.getTexture(seriesPrefix,carNum);
				liveryRend.material.mainTexture = ModData.getTexture(seriesPrefix,carNum);
			}
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
		
		switch(seriesPrefix){
			case "cup22":
				this.transform.Find("Number").Translate(0.1f,0f,0f);
				break;
			case "cup23":
				//Debug.Log("Cup '23 Number Shift");
				this.transform.Find("Number").Translate(0.1f,0f,0f);
				break;
			default:
				break;
		}
				
		circuitLanes = PlayerPrefs.GetInt("CircuitLanes");
		
		apronLineX = -2.7f;
		apronLineX = 1.2f - ((circuitLanes - 1) * 1.2f) - 0.3f;

		brakesOn = false;
		draftCounter = 0;
		raceCounter = 0;

		if(officialSeries == true){
			if(DriverNames.getType(seriesPrefix,carNum) == "Strategist"){
				if(seriesPrefix == "irl23"){
					carClass+=4;
				}
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
					case 9:
						laneChangeDuration = 25;
						laneChangeSpeed = 0.048f;
						laneChangeBackout = 10;
						break;
					case 10:
						laneChangeDuration = 20;
						laneChangeSpeed = 0.06f;
						laneChangeBackout = 8;
						break;
					default:
						laneChangeDuration = 80;
						laneChangeSpeed = 0.015f;
						laneChangeBackout = 32;
						break;
				}
			} else {
				if(seriesPrefix == "irl23"){
					laneChangeDuration = 48;
					laneChangeSpeed = 0.025f;
					laneChangeBackout = 16;
				} else {
					laneChangeDuration = 80;
					laneChangeSpeed = 0.015f;
					laneChangeBackout = 32;
				}
			}
		} else {
			laneChangeDuration = 80;
			laneChangeSpeed = 0.015f;
			laneChangeBackout = 32;
		}

		dooredStrength = 40;
		if(officialSeries == true){
			if (DriverNames.getType(seriesPrefix,carNum) == "Intimidator"){
				dooredStrength = 50 + (carRarity * 5) + (carClass * 5);
				if(dooredStrength > 95){
					dooredStrength = 95;
				}
			}
		}

		dominatorDrag = 0;
		if(officialSeries == true){
			if(DriverNames.getType(seriesPrefix,carNum) == "Dominator"){
				//e.g. Class 6(S) reduces drag from 0.004f to 0.002f
				dominatorDrag = carClass / 3000f;
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
		suddenshowerMod = false;
		bulldozerMod = false;
		wallrideMod = false;
		
		momentChecks = false;
		if(PlayerPrefs.HasKey("RaceMoment")){
			momentChecks = true;
		}
		
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
				//mainCam.GetComponent<UIAnimate>().shakeCamZ(mainCam.transform.position, wreckDamage / 100f);
			} else {
				wreckDamage += (200 - playerWreckDecel) / 100;
				//mainCam.GetComponent<UIAnimate>().shakeCamX(mainCam.transform.position, wreckDamage / 50f);
			}
		}
		
		//Debug.Log("Hit " + carHit.gameObject.name);
		if ((carHit.gameObject.tag == "AICar") || 
			(carHit.gameObject.tag == "Barrier") || 
			(carHit.gameObject.name == "OuterWall") ||
			(carHit.gameObject.name == "SaferBarrier") ||
			(carHit.gameObject.name == "TrackLimit") ||
			(carHit.gameObject.name == "FixedKerb")) {
			
			if((isWrecking == false)&&(wreckOver == false)&&(delicateMod == true)){
				startWreck();
				this.transform.Find("TireSmoke").GetComponent<ParticleSystem>().Play();
			}
			if((isWrecking == false)&&(wreckOver == false)&&(wallrideMod == true)){
				if((carHit.gameObject.tag == "Barrier") || 
				  (carHit.gameObject.name == "OuterWall") ||
				  (carHit.gameObject.name == "SaferBarrier")){
					startWreck();
					this.transform.Find("TireSmoke").GetComponent<ParticleSystem>().Play();
					GameObject.Find("Main Camera").GetComponent<AudioManager>().playSfx("Scrape");
				}
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
						laneticker = -laneChangeDuration + laneticker;
						lane--;
						//GameObject.Find("Main Camera").GetComponent<AudioManager>().playSfx("Impact");
					}
				} else {
					if(laneticker < 0){
						bool rightSideHit = checkRaycast("RightCorners", 0.51f);
						if(rightSideHit == true){
							backingOut = true;
							laneticker = laneChangeDuration + laneticker;
							lane++;
							//GameObject.Find("Main Camera").GetComponent<AudioManager>().playSfx("Impact");
						}
					}
				}
			}
		}
		playerSpeed-=0.2f;
	}
	
	void OnCollisionExit (Collision carHit){
		if(isWrecking == true){
			playerWreckDecel+= Random.Range(2f,5f);
		}
	}
	
	void ReceivePush(float bumpSpeed){
		if(isWrecking == false){
			//Debug.Log("Thanks for the push! Hit me at " + bumpSpeed + " while I was going " + playerSpeed);
			if(initialContact == false){
				float midSpeed = bumpSpeed - playerSpeed;
				playerSpeed += midSpeed/4;
				initialContact = true;
				//Debug.Log("Impact levels out Player");
			} else {
				//Speed up
				playerSpeed += backdraftMulti;
			}
			//Send it back
			RaycastHit DraftCheckBackward;
			bool HitBackward = Physics.Raycast(transform.position - new Vector3(0.48f,0,0), transform.forward * -1, out DraftCheckBackward, 1.01f);
			if(HitBackward == false){
				HitBackward = Physics.Raycast(transform.position - new Vector3(-0.48f,0,0), transform.forward * -1, out DraftCheckBackward, 1.01f);
			}
			
			if(HitBackward == true){
				DraftCheckBackward.transform.gameObject.SendMessage("UpdateTandemPosition",tandemPosition);
				DraftCheckBackward.transform.gameObject.SendMessage("GivePush",playerSpeed);
				//Debug.Log("Send to pusher: " + playerSpeed);
			}
		}
	}
	
	void GivePush(float bumpSpeed){
		//Debug.Log("Return to player speed " + bumpSpeed + " (was " + playerSpeed + ")");
		if(tandemPosition > 2){
			playerSpeed-= 0.25f;
			//Debug.Log("Player speed reduced by " + (bumpSpeed - playerSpeed) + "");
		}
		affectedPlayerSpeed = bumpSpeed;
	}
	
	void UpdateTandemPosition(int tandemPosInFront){
		tandemPosition = tandemPosInFront + 1;
		//Debug.Log("Player is in tandem position " + tandemPosition + "");
	}
	
	// Update is called once per frame
	void FixedUpdate () {
			
		updateHUD();
		
		if(gamePausedLate == true){
			if(cautionSummaryMenu.activeSelf == true){
				Debug.Log("Time Paused (Movement)");
				Time.timeScale = 0.0f;
			} else {
				Debug.Log("Pause wasn't ready..");
			}
		}
		
		if(pacing == true){
			playerSpeed = 202;
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
				//this.transform.Find("TireSmoke").GetComponent<ParticleSystem>().Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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

		bool HitForward = Physics.Raycast(transform.position, transform.forward, out DraftCheckForward, 25);
        bool HitBackward = Physics.Raycast(transform.position, transform.forward * -1, out DraftCheckBackward, 25);

		RaycastHit DraftCheck;

		laneBias = 0;

		//Speed up if under min non-draft speed
		if(playerSpeed < 199){
			playerSpeed=199f;
		}
		
		//The top speed factoring in car spec and track lane
		variTopSpeed = topSpeed + (carRarity / 4f) + (laneInv / 4f) + randTopend;
		
		//If in draft of car in front
		if (Physics.Raycast(transform.position,transform.forward, out DraftCheck, 10 + customDistF)){
			draftDist = DraftCheck.distance;
			//Speed up
			if(playerSpeed < variTopSpeed){
				
				float draftStrength = ((10 - DraftCheck.distance)/draftStrengthRatio) + (carRarity / 1000f);
				
				float diffToMax = variTopSpeed - playerSpeed;
				//If approaching max speed, taper off
				if((diffToMax) < 2){
					//If speed above max, it zeros the draft out (rev limiter)
					if(diffToMax < 0){
						diffToMax = 0;
					}
					draftStrength *= (diffToMax / 2) + 0.01f;
					//Debug.Log("Draft: " + draftStrength + " Multi: " + (diffToMax / 2));
				}
				playerSpeed += draftStrength;
				
			} else {
				playerSpeed-=((playerSpeed - topSpeed)/200);
				
				//Over the top speed and still in the draft
				overspeed += 0.0001f;
				playerSpeed = variTopSpeed + overspeed;
			}
		} else {
			//Slow down if not in any draft
			if(playerSpeed > 200){
				float diffToMax = variTopSpeed - playerSpeed;
				if((diffToMax) < 2){
					if(diffToMax < 0){
						diffToMax = 0;
					}
					playerSpeed-=(dragDecelMulti * (2 - (diffToMax / 2)));
				} else {
					playerSpeed-=(dragDecelMulti + dominatorDrag);
				}
			}
			
			//Overspeed disappears
			if(overspeed > 0){
				overspeed -= 0.01f;
			} else {
				overspeed = 0;
			}
					
			//Empty the Draft Bar
			draftDist= 10 + customDistF;
		}
		
		// If recieving backdraft of car behind
		if (Physics.Raycast(transform.position,transform.forward * -1, out DraftCheck, draftAirCushion)){
			//Speed up
			if(playerSpeed <= (variTopSpeed - 2f)){
				playerSpeed+=(backdraftMulti);
			}
		}

		// If being bump-drafted from behind
		if (Physics.Raycast(transform.position,transform.forward * -1, out DraftCheck, bumpDraftDistTrigger)){
			//Speed up
			if(playerSpeed <= (variTopSpeed - 2f)){
				playerSpeed+=(backdraftMulti / 5f);
			}
			//playerSpeed+=0.0045f;
			DraftCheckBackward.transform.gameObject.SendMessage("GivePush",playerSpeed);
			tandemDraft = true;
		} else {
			tandemDraft = false;
			initialContact = false;
		}

		// If bump-drafting the car in front
		if (Physics.Raycast(transform.position,transform.forward, out DraftCheck, bumpDraftDistTrigger)){
			if(isWrecking == false){
				//Bump drafting speeds both up
				if(playerSpeed <= (variTopSpeed - 2f)){
					//Debug.Log("AI " + DraftCheck.transform.gameObject.name + " is bumped to take speed of " + playerSpeed);
					playerSpeed+=(backdraftMulti + customAccelF);
				}
				if(DraftCheck.transform.gameObject.tag == "AICar"){
					//Debug.Log("AI " + DraftCheck.transform.gameObject.name + " is bumped to take speed of " + playerSpeed);
					DraftCheck.transform.gameObject.SendMessage("ReceivePush",playerSpeed);
				}
			}
		} else {
			tandemDraft = false;
			tandemPosition = 1;
		}
		
		if(brakesOn == true){
			if(playerSpeed > 195){
				playerSpeed-=0.025f;
			}
		}

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

		if(affectedPlayerSpeed != 0){
			//Debug.Log("Player speed equalised to " + affectedPlayerSpeed + ". Was " + playerSpeed);
			playerSpeed = affectedPlayerSpeed;
			affectedPlayerSpeed = 0;
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
	
	void LateUpdate(){
		if(gamePausedLate == true){
			if((cautionSummaryMenu.activeSelf == true)&&
			   (Time.timeScale != 0.0f)){
				Debug.Log("Time Paused (Movement)");
				Time.timeScale = 0.0f;
			}
		}
	}
	
	void setCarPhysics(string seriesPrefix){
		switch(seriesPrefix){
			case "irl23":
				seriesSpeedDiff = 30;
				draftStrengthRatio = 600f;
				dragDecelMulti = 0.0025f;
				backdraftMulti = 0.015f;
				bumpDraftDistTrigger = 1.2f;
				draftAirCushion = 1.8f;
				revLimiterBoost = 1.0f;
				break;
			case "Cushion":
				seriesSpeedDiff = 0;
				draftStrengthRatio = 1200f;
				dragDecelMulti = 0.0035f;
				backdraftMulti = 0.004f;
				bumpDraftDistTrigger = 1.11f;
				draftAirCushion = 1.2f;
				revLimiterBoost = 0f;
				break;
			case "V3Weaker":
				seriesSpeedDiff = 0;
				draftStrengthRatio = 900f;
				dragDecelMulti = 0.0035f;
				backdraftMulti = 0.005f;
				bumpDraftDistTrigger = 1.11f;
				draftAirCushion = 1.1f;
				revLimiterBoost = 0f;
				break;
			default:
				seriesSpeedDiff = 0;
				draftStrengthRatio = 1050f;
				dragDecelMulti = 0.0035f;
				backdraftMulti = 0.004f;
				bumpDraftDistTrigger = 1.11f;
				draftAirCushion = 1.2f;
				revLimiterBoost = 0f;
				break;
		}
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

        if (laneticker == 0){
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
			if (backingOut == false) {
				backingOut = true;
				if((CameraRotate.onTurn == true)||(wallrideMod == true)){
					startWreck();
				} else {
					GameObject.Find("Main Camera").GetComponent<CommentaryManager>().commentate("Wallhit");
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
			
			carEngine.pitch = 0.7f + revLimiterBoost + (engineRevs / 10000f) - ((CameraRotate.carSpeedOffset - playerWreckDecel) / 200);
			if(currentGear != 4){
				currentGear = 4;
				//SFX
				//GameObject.Find("Main Camera").GetComponent<AudioManager>().playSfx("GearShift");
			}
			HUDGear.GetComponent<TMPro.TMP_Text>().text = "GEAR 4";
			HUDRevs.GetComponent<TMPro.TMP_Text>().text = "" + engineRevs.ToString("F0") + " RPM";
			HUDRevBarMask.GetComponent<RectTransform>().sizeDelta = new Vector2(((10000 - engineRevs) / 25) + revbarOffset, 40);
		} else {
			if((CameraRotate.carSpeedOffset - playerWreckDecel) < gearSpeeds[3]){
				//3rd GEAR
				engineRevs = 5000;
				engineRevs+=((gearSpeeds[3] - CameraRotate.carSpeedOffset) * 100);
				engineRevs+=(playerSpeed - 195) * 100;
				
				carEngine.pitch = 1.4f + revLimiterBoost + ((playerSpeed - 200) / 10) - ((CameraRotate.carSpeedOffset - playerWreckDecel) / 200);
				if(currentGear != 3){
					currentGear = 3;
					//SFX
					//GameObject.Find("Main Camera").GetComponent<AudioManager>().playSfx("GearShift");
				}
				HUDGear.GetComponent<TMPro.TMP_Text>().text = "GEAR 3";
				HUDRevs.GetComponent<TMPro.TMP_Text>().text = "" + engineRevs.ToString("F0") + " RPM";
				HUDRevBarMask.GetComponent<RectTransform>().sizeDelta = new Vector2(((10000 - engineRevs) / 25) + revbarOffset, 40);
			} else {
				//2nd GEAR 
				if((CameraRotate.carSpeedOffset - playerWreckDecel) < gearSpeeds[2]){
					engineRevs = 2000;
					engineRevs+=((gearSpeeds[2] - CameraRotate.carSpeedOffset) * 100);
					engineRevs+=(playerSpeed - 195) * 100;
					
					carEngine.pitch = 1.6f + revLimiterBoost + ((playerSpeed - 200) / 5) - ((CameraRotate.carSpeedOffset - playerWreckDecel) / 200);
					if(currentGear != 2){
						currentGear = 2;
						//SFX
						//GameObject.Find("Main Camera").GetComponent<AudioManager>().playSfx("GearShift");
					}
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
					
					carEngine.pitch = 1.6f + revLimiterBoost + ((playerSpeed - 200) / 5) - ((CameraRotate.carSpeedOffset - playerWreckDecel) / 200);
					if(currentGear != 1){
						currentGear = 1;
						//SFX
						//GameObject.Find("Main Camera").GetComponent<AudioManager>().playSfx("GearShift");
					}
					HUDGear.GetComponent<TMPro.TMP_Text>().text = "GEAR 2";
					HUDRevs.GetComponent<TMPro.TMP_Text>().text = "" + engineRevs.ToString("F0") + " RPM";
					HUDRevBarMask.GetComponent<RectTransform>().sizeDelta = new Vector2(((10000 - engineRevs) / 25) + revbarOffset, 40);
				}
			}
		}
		
		//The speed shown on the HUD
		speedoSpeed = (playerSpeed + seriesSpeedDiff - speedOffset - CameraRotate.carSpeedOffset) + playerWreckDecel;
		
		//When debugging the real speed
		#if UNITY_EDITOR
		//speedoSpeed = playerSpeed + playerWreckDecel;
		#endif
		
		if(speedoSpeed > 1){
			HUDSpeed.GetComponent<TMPro.TMP_Text>().text = "SPD " + (speedoSpeed).ToString("F2");
			HUDAccSpeed.GetComponent<TMPro.TMP_Text>().text = "SPD " + (speedoSpeed).ToString("F2");
		} else {
			//Update the final position GUI slightly before the Time.timeScale freezes to 0
			if(speedoSpeed > 0){
				RaceHUD.racePreover = true;
			} else {
				endWreck();
			}
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
		HUDBestLap.GetComponent<TMPro.TMP_Text>().text = "BEST " + (CameraRotate.raceLapRecord - speedOffset).ToString("F2");
		
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
			totalWreckers++;
			wreckHits++;
			wreckDamage+=1;
			GameObject.Find("Main Camera").GetComponent<CommentaryManager>().commentate("Crash");
		}
		if(CameraRotate.cautionOut == false){
			CameraRotate.throwCaution();
		}
		sparksCooldown = 99999;
		//Debug.Log(this.name + " is wrecking");
		
		//Make the car light, more affected by physics
		this.GetComponent<Rigidbody>().mass = 2;
		
		//Remove constraints, allowing it to impact/spin using physics
		this.GetComponent<Rigidbody>().constraints &= ~RigidbodyConstraints.FreezeRotationY;
		this.GetComponent<Rigidbody>().constraints &= ~RigidbodyConstraints.FreezePositionX;
		
		//Remove forces, physics only
		this.GetComponent<Rigidbody>().isKinematic = false;
		this.GetComponent<Rigidbody>().useGravity = false;
		
		//Apply wind/drag
		baseDecel = -0.25f;
		slideX = 0;
		playerWreckDecel = 0;
		targetForce = 0;
		forceSmoothing = 0.2f;
		windForce = targetForce;
		this.GetComponent<ConstantForce>().force = new Vector3(0f, 0f,targetForce);
		float wreckTorque;
		if(wreckHits > 1){
			//Subsequent hits based on rotation angle
			float spinAngle = this.transform.localRotation.eulerAngles.y;
			//Debug.Log("Car rotation: " + spinAngle);
			wreckTorque = Random.Range(-0.25f, 0.25f) * 10;
		} else {
			//First impact, car will start straight
			wreckTorque = Random.Range(-0.25f, 0.25f) * 10;
		}
		this.GetComponent<ConstantForce>().torque = new Vector3(0f, wreckTorque, 0f);
		
		this.transform.Find("TireSmoke").GetComponent<ParticleSystem>().Play();
		
		if(momentChecks == true){
			MomentsCriteria.checkMomentsCriteria("WreckStartLocationStraight",CameraRotate.straight.ToString(), onTurn.ToString());
			MomentsCriteria.checkMomentsCriteria("WreckStartPositionHigherThan",Ticker.position.ToString());
		}
	}
	
	public void endWreck(){
		playerSpeed = 0;
		baseDecel = -0.25f;
		slideX = 0;
		playerWreckDecel = 0;
		targetForce = 0;
		updateWindForce();
		isWrecking = false;
		wreckOver = true;
		hideHUD();
		//Ticker.saveCautionPositions();
		//Debug.Log("WRECK OVER");
		this.GetComponent<Rigidbody>().mass = 5;
		this.GetComponent<Rigidbody>().isKinematic = true;
		//this.GetComponent<Rigidbody>().useGravity = true;
		this.GetComponent<ConstantForce>().force = new Vector3(0f, 0f,windForce);
		this.GetComponent<ConstantForce>().torque = new Vector3(0f, 0f, 0f);
		this.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezePositionZ;
		
		//this.transform.Find("TireSmoke").GetComponent<ParticleSystem>().Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
		
		if(momentChecks == true){
			MomentsCriteria.checkMomentsCriteria("WreckEndLocationLessThanX",vehicle.transform.position.x.ToString());
			MomentsCriteria.checkMomentsCriteria("WreckEndLocationCorner", CameraRotate.turn.ToString(), onTurn.ToString());
			MomentsCriteria.checkMomentsCriteria("WreckTotalCars",totalWreckers.ToString());
			MomentsCriteria.checkMomentsCriteria("CarWrecks","");
			MomentsCriteria.checkMomentsCriteria("CarAvoidsWreck","");
			MomentsCriteria.checkMomentsCriteria("PlayerWrecks","");
			MomentsCriteria.checkMomentsCriteria("PlayerAvoidsWreck","");
		}
		
		cautionSummaryTotalWreckers.GetComponent<TMPro.TMP_Text>().text = totalWreckers + " Cars Involved";
		cautionSummaryDamage.GetComponent<TMPro.TMP_Text>().text = "Damage? " + calculateDamageGrade(wreckDamage);
		//If damage is above 1000, considered to be heavy, 2000+ is unrepairable
		
		//No cautions on the last lap, race is over
		if(CameraRotate.lap < CameraRotate.raceEnd){
			if(momentChecks == false){
				//Open the caution menu
				cautionSummaryMenu.SetActive(true);
			} else {
				//Moment status replaces caution stats
			}
			if(fastestLapSaved == false){
				mainCam.GetComponent<CameraRotate>().saveRaceFastestLap();
				fastestLapSaved = true;
			}
		}
		gamePausedLate = true;
		Debug.Log("Pause the game!");
		mainCam.GetComponent<AudioListener>().enabled = false;
		PlayerPrefs.SetInt("Volume",0);
		
		if(momentChecks == true){
			MomentsCriteria.checkEndCriteria();
		}
	}
	
	void wreckPhysics(){
		wreckAngle = this.gameObject.transform.rotation.y;
		float wreckSine = Mathf.Sin(wreckAngle);
		if(wreckSine < 0){
			wreckSine = -wreckSine;
		}
		baseDecel-=0.27f;
		slideX = ((baseDecel + 1) / 4f) + 20f;
		//Formula: -200f = -10x, -140f = 0x, 0f = 10x
		//         -200f = -20x, -100f = -10x, 0f = 0x
		//         -200f = -6x, -140f = 0x, 0f = 14f
		//slideX = ((baseDecel + 1) / 10f) + 14f
		//Reduce division factor to increase effect
		
		if(wallrideMod == false){
			targetForce = playerWreckDecel;
		}
		updateWindForce();
		if(CameraRotate.onTurn == true){
			//baseDecel-=0.02f * CameraRotate.currentTurnSharpness();
			//Debug.Log("Extra decel: " + (0.02f * CameraRotate.currentTurnSharpness()));
			if(wallrideMod == true){
				baseDecel+=0.15f;
				this.GetComponent<ConstantForce>().force = new Vector3(10f,0f,40f);
			} else {
				this.GetComponent<ConstantForce>().force = new Vector3(slideX,0f,windForce);
				//Debug.Log("Side Force: " + slideX);
			}
			//Debug.Log("Apply side force to wreck on turn");
		} else {
			this.GetComponent<ConstantForce>().force = new Vector3(-3f, 0f,windForce);
		}
		playerWreckDecel = baseDecel - (50f * wreckSine);
		
		//Debug.Log("Wreck Decel: " + playerWreckDecel);
		if((playerSpeed - speedOffset - CameraRotate.carSpeedOffset) + windForce < 0){
			endWreck();
		}
		
		if(wallrideMod == true){
			this.GetComponent<Rigidbody>().mass = 2;
		} else {
			this.GetComponent<Rigidbody>().mass = (-playerWreckDecel / 20) + 2;
		}
		this.GetComponent<Rigidbody>().angularDrag += 0.001f;
		
		//Prevent landing in the crowd
		if(this.gameObject.transform.position.x > 2f){
			this.gameObject.transform.position = new Vector3(2f,this.gameObject.transform.position.y,this.gameObject.transform.position.z);
		}
		
		this.transform.Find("SparksL").rotation = Quaternion.Euler(0,180,0);
		this.transform.Find("SparksR").rotation = Quaternion.Euler(0,180,0);
		this.transform.Find("SparksL").GetComponent<ParticleSystem>().startSpeed = 50 + (playerWreckDecel / 4);
		this.transform.Find("SparksR").GetComponent<ParticleSystem>().startSpeed = 50 + (playerWreckDecel / 4);
		
		Transform tireSmoke = this.transform.Find("TireSmoke");
		tireSmoke.rotation = Quaternion.Euler(0,180,0);
		float smokeMultiplier = Mathf.Sin(wreckAngle);
		if(smokeMultiplier < 0){
			smokeMultiplier = -smokeMultiplier;
		}
		smokeMultiplier = (smokeMultiplier * 60) + 0;
		smokeMultiplier = Mathf.Round(smokeMultiplier);
		tireSmoke.GetComponent<ParticleSystem>().startColor = new Color32(200,200,200,60);
		tireSmoke.GetComponent<ParticleSystem>().startSpeed = 40 + (playerWreckDecel / 5);
		tireSmoke.GetComponent<ParticleSystem>().startSize = 12 + (playerWreckDecel / 30);
		
		if(Mathf.Round(playerWreckDecel) == -90){
			GameObject theCamera = GameObject.Find("Main Camera");
			theCamera.GetComponent<CommentaryManager>().commentate("Caution");
		}
		
		if((playerSpeed - speedOffset - CameraRotate.carSpeedOffset) + windForce > 30){
			//this.transform.Find("TireSmoke").GetComponent<ParticleSystem>().Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
		}
	}
	
	void updateWindForce(){
		if(windForce < targetForce - forceSmoothing){
			windForce += forceSmoothing;
		}
		if(windForce > targetForce + forceSmoothing){
			windForce -= forceSmoothing;
		}
	}
	
	public static void incrTotalWreckers(){
		totalWreckers++;
		if(totalWreckers == 20){
			//Debug.Log("BIG CRASH");
			GameObject theCamera = GameObject.Find("Main Camera");
			theCamera.GetComponent<CommentaryManager>().commentate("BigCrash");
		}
	}
		
	public string calculateDamageGrade(float damage){
		if(damage > 1500){
			return "DVP Clock (100%)";
		}
		if(damage > 500){
			return "Heavy (" + Mathf.Round(damage / 15) + "%)";
		}
		if(damage > 200){
			return "Minor (" + Mathf.Round(damage / 15) + "%)";
		}
		if(damage > 20){
			return "Cosmetic (" + Mathf.Round(damage / 15) + "%)";
		}
		return "None";
	}
	
	public void hideHUD(){
		HUD.SetActive(false);
		HUDControls.SetActive(false);
	}
}