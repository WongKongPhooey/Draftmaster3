﻿using UnityEngine;
using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Collections.Generic;

using Random=UnityEngine.Random;

public class Movement : MonoBehaviour {

	public GameObject vehicle;
	public static float playerSpeed;
	public float gettableSpeed;
	public float topSpeed;
	public static float enginetemp;
	public static float speedRand;

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
	float laneChangeSpeed;
	float vStrongLane;
	float strongLane;
	float weakLane;
	float vWeakLane;
	float weakestLane;
	float laneFactor;

	public static bool onTurn;
	
	public GameObject audioHolder;
	AudioSource carEngine;
	float engineRevs;
	
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
	public int carNumber;
	
	public bool backingOut = false;
	public bool tandemDraft = false;

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

	// Use this for initialization
	void Start () {
		enginetemp = 210;
		playerSpeed = 200;
		gettableSpeed = playerSpeed;
		topSpeed = 208f + speedRand;
		speedRand = Random.Range(0,50);
		speedRand = speedRand / 100;
		laneticker = 0;
		onTurn = false;
		lane = SpawnField.startLane;
		laneBias = 0;
		challengeSpeedBoost = 0;
		
		seriesPrefix = "cup20";
		
		carName = PlayerPrefs.GetString("carTexture");
		
		string splitAfter = "livery";
		string carNumStr = carName.Substring(carName.IndexOf(splitAfter) + splitAfter.Length);
		
		bool findCarNum = int.TryParse(Regex.Replace(carNumStr, "[^0-9]", ""), out carNum);
		if(findCarNum == true){
		  carClass = PlayerPrefs.GetInt(seriesPrefix + carNum + "Class");
			carTeam = DriverNames.cup2020Teams[carNum];
			carManu = DriverNames.cup2020Manufacturer[carNum];
		} else {
			Debug.Log("Invalid Car #");
		}
		
		Renderer liveryRend = this.transform.Find("Livery").GetComponent<Renderer>();
		Renderer numRend = this.transform.Find("Number").GetComponent<Renderer>();
				
		circuitLanes = PlayerPrefs.GetInt("CircuitLanes");
		
		apronLineX = -2.7f;
		apronLineX = 1.2f - ((circuitLanes - 1) * 1.2f) - 0.3f;

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
							laneChangeBackout = 16;
							break;
						default:
							break;
					}
		        } else {
		            laneChangeDuration = 80;
		            laneChangeSpeed = 0.015f;
		            laneChangeBackout = 32;
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
	}
	
	void OnCollisionEnter (Collision carHit){
		
		//Debug.Log("Hit " + carHit.gameObject.name);
		if((carHit.gameObject.tag == "AICar") || 
		   (carHit.gameObject.tag == "Barrier") || 
		   (carHit.gameObject.name == "OuterWall")){
			   
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
	
	void ReceivePush(float bumpSpeed){
		//Debug.Log("Thanks for the push! Hit me at " + bumpSpeed + "while I was going " + playerSpeed);
		if(tandemDraft == false){
			//if(bumpSpeed - playerSpeed > 1){
				float midSpeed = bumpSpeed - playerSpeed;
				playerSpeed += midSpeed/8;
				tandemDraft = true;
				//Debug.Log("Impact levels out Player");
			//}
		} else {
			//Send it back
			RaycastHit DraftCheckBackward;
			bool HitBackward = Physics.Raycast(transform.position, transform.forward * -1, out DraftCheckBackward, 1.1f);
			DraftCheckBackward.transform.gameObject.SendMessage("GivePush",playerSpeed);
		}
	}
	
	void GivePush(float bumpSpeed){
		playerSpeed = bumpSpeed;
	}
	
	// Update is called once per frame
	void FixedUpdate () {
				
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
			//Speed up
			if(playerSpeed <= topSpeed){
				playerSpeed+=((10 - DraftCheck.distance)/1000);
			} else {
				playerSpeed-=((playerSpeed - topSpeed)/100);
			}
			if(PlayerPrefs.GetInt("TutorialActive") == 1){
				if(RaceHUD.tutorialStage == 3){
					RaceHUD.tutorialDraftingCount++;
				}
			}
		} else {
			//Slow down if not in any draft
			if(playerSpeed >= 200){
				playerSpeed-=0.003f;
			}
		}
		
		// If recieving backdraft of car behind
		if (Physics.Raycast(transform.position,transform.forward * -1, out DraftCheck, 1.5f)){
			//Speed up
			if(playerSpeed <= (206f + (speedRand + customSpeedF) + challengeSpeedBoost + laneInv)){
				playerSpeed+=(0.004f + customAccelF);
				if(RaceHUD.tutorialStage == 4){
					RaceHUD.tutorialBackdraftCount++;
				}
			}
		}

		// If being bump-drafted from behind
		if (Physics.Raycast(transform.position,transform.forward * -1, out DraftCheck, 1.01f)){
			//Speed up
			playerSpeed+=0.004f;
			if(RaceHUD.tutorialStage == 4){
				RaceHUD.tutorialBackdraftCount++;
			}
			//DraftCheckBackward.transform.gameObject.SendMessage("GivePush",playerSpeed);
			tandemDraft = true;
		} else {
			tandemDraft = false;
		}

		// If bump-drafting the car in front
		if (Physics.Raycast(transform.position,transform.forward, out DraftCheck, 1.01f)){
			
			//Debug.Log("AI " + DraftCheck.transform.gameObject.name + " is bumped to match speed of " + playerSpeed);
			if(DraftCheck.transform.gameObject.tag == "AICar"){
				DraftCheckForward.transform.gameObject.SendMessage("ReceivePush",playerSpeed);
			}
			
			//Bump drafting speeds both up
			playerSpeed+=0.004f;	
		} else {
			tandemDraft = false;
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

		carEngine = audioHolder.GetComponent<AudioSource>();

		if(PlayerPrefs.GetString("raceSeries") == "IndyCar"){
			carEngine.pitch = 3f + ((playerSpeed - 200) / 10);
		} else {
			carEngine.pitch = 1.2f + ((playerSpeed - 200) / 20);
		}

		wobbleCount++;
		
		if(wobbleCount >= wobbleRand){
			wobbleRand = Random.Range(20,50);
			wobbleTarget = Random.Range(-100,100);
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
		
		if(CameraRotate.lap == 0){
			playerSpeed = 200;
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
		
		Renderer liveryRend = this.transform.Find("Livery").GetComponent<Renderer>();
		Renderer numRend = this.transform.Find("Number").GetComponent<Renderer>();
		
		liveryRend.material.mainTexture = Resources.Load(PlayerPrefs.GetString("carTexture")) as Texture;
		
		if(PlayerPrefs.HasKey("CustomNumber" + seriesPrefix + carNum)){
			liveryRend.material.mainTexture = Resources.Load(seriesPrefix + "livery" + carNum + "blank") as Texture;
			customNum = PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + carNum);
			numRend.material.mainTexture = Resources.Load("cup20num" + customNum) as Texture;
			//Debug.Log("Player #" + customNum + " applied Var: " + seriesPrefix + "num" + customNum);
			numRend.enabled = true;
		} else {
			//liveryRend.material.mainTexture = Resources.Load(seriesPrefix + "livery" + carNum) as Texture;
			numRend.enabled = false;
			//Debug.Log("No custom number saved");
		}
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
			}
			laneticker = laneChangeDuration + laneticker;
			lane++;
			//Wall hit decel
			playerSpeed -= 1f;
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
}