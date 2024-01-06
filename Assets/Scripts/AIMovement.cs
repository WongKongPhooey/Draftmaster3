﻿using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Random=UnityEngine.Random;

public class AIMovement : MonoBehaviour
{
    public GameObject AICar;
	public GameObject thePlayer;
	public GameObject controlCar;
	public Camera player2Cam;
	public Vector3 pos;
    public float AISpeed;
	public float relativeZToPlayer;
	float affectedAISpeed;
    float speed;
	public float AITopSpeed;
	float AIVariTopSpeed;
    float speedRand;
    float accelRand;
	int AILevel;
    int laneticker;
    int laneChangeDuration;
    int laneChangeBackout;
    float laneChangeSpeed;
	bool changingLanes;
	int holdLane;
	int laneRest;
	int dooredStrength;
	bool movingLane;
    bool backingOut;
    bool laneSettled;
    int laneSettling;
	int laneStagnant;
	int stagnantMax;
	
	//These can adjust per race series (e.g. Indy)
	float draftStrengthRatio;
	float dragDecelMulti;
	float backdraftMulti;
	float bumpDraftDistTrigger;
	float draftAirCushion;
	float passDistMulti;
	bool tandemDrafting;
	
	public bool isWrecking;
	public bool wreckOver;
	float baseDecel;
	float randDecel;
	float slideX;
	public float wreckDecel;
	float speedDiffPadding;
	float wreckAngle;
	float wreckSlideRand;
	float wreckFlatRand;
	float wreckMassRand;
	int antiGlitch;
	public bool playerWrecked;
	float targetForce;
	float windForce;
	float forceSmoothing;
	int wreckProbability;
	bool hitByPlayer;
	
	int wreckFreq;
	
	static int maxTandem;
	static float coolOffSpace;
	static float coolOffInv;
	int circuitLanes;
	float apronLineX;

	public string carName;
	public int carNum;
	public string carTeam;
	public string carManu;
	public int carRarity;
	public string carType;
	public int AICarClass;

	List<string> altPaints;

    public static bool onTurn;
	public bool tandemDraft;
	public int tandemPosition;
	public bool initialContact = false;

    string carNumber;
	string AICarNum;
	
	string currentSeries;
	string currentSubseries;
	string seriesPrefix;
	bool officialSeries;
	int customNum;

	bool coolEngine;
	int sparksCooldown;

	int maxDraftDistance;

	bool dominator;

	int tick;
	int logicCycle;
	int particleDisableDelay;

	public int lap;
    public int lane;
    public int laneInv;
    float laneFactor;
    public int laneBias;

    int wobbleCount;
    int wobblePos;
    int wobbleTarget;
    int wobbleRand;
	
	int distFromPlayer;
	
	bool caution = false;
	static bool prePause = false;
    public static bool crashActive;
    public static int crashTime;

	//Debug tool
	public bool debugPlayer;

	NativeArray<RaycastCommand> raycastBatch;
	NativeArray<RaycastHit> raycastHits;
	JobHandle raycastHandler;

    // Use this for initialization
    void Start(){
		
		tick=0;
		logicCycle = 20;
		
		pos = transform.position;
		
		AISpeed = 203f;
        speedRand = Random.Range(-150, 150);
        speedRand = speedRand / 100;
        accelRand = Random.Range(-30, 60);
        accelRand = accelRand / 5000;	
		AITopSpeed = 206f + speedRand;
		relativeZToPlayer = 0;
		
		holdLane = 0;
		laneRest = Random.Range(100, 1000);
		isWrecking = false;
		wreckOver = false;
		wreckSlideRand = Random.Range(5f,15f);
		wreckFlatRand = Random.Range(0f,-3f);
		wreckMassRand = Random.Range(-0.5f,0.5f);
		hitByPlayer = false;
		speedDiffPadding = 0.2f;
		
		wreckFreq = PlayerPrefs.GetInt("WreckFreq");
		wreckProbability = (wreckFreq * 2) + 1;
		
        onTurn = false;
		tandemDraft = false;
		tandemPosition = 1;
		maxTandem = 2;
		coolOffSpace = 2.0f;
		coolOffInv = 75;
		if(PlayerPrefs.GetString("TrackType") == "Short"){
			//maxTandem = 1;
			coolOffSpace = 2.5f;
			coolOffInv = 50;
		}
		if(PlayerPrefs.GetString("TrackType") == "Plate"){
			maxTandem = 3;
			coolOffSpace = 1.5f;
		}
        laneticker = 0;
		
		currentSeries = PlayerPrefs.GetInt("CurrentSeries").ToString();
		currentSubseries = PlayerPrefs.GetInt("CurrentSubseries").ToString();
		
		if(PlayerPrefs.GetString("RaceType") == "Event"){
			AILevel = EventData.offlineAILevel[int.Parse(currentSeries.ToString()),int.Parse(currentSubseries.ToString())];
		} else {
			AILevel = SeriesData.offlineAILevel[int.Parse(currentSeries.ToString()),int.Parse(currentSubseries.ToString())];
		}
		PlayerPrefs.SetInt("RaceAILevel", AILevel);
		
		raycastBatch = new NativeArray<RaycastCommand>(8, Allocator.Persistent);
		raycastHits = new NativeArray<RaycastHit>(8, Allocator.Persistent);
		
		antiGlitch = 0;
		
		coolEngine = false;
		sparksCooldown = 0;
		particleDisableDelay = 0;
		
		thePlayer = GameObject.Find("Player");
		controlCar = GameObject.Find("ControlCar");
		
		
		if(PlayerPrefs.HasKey("FixedSeries")){
			seriesPrefix = PlayerPrefs.GetString("FixedSeries");
		} else {
			seriesPrefix = PlayerPrefs.GetString("carSeries");
		}
		
		string splitAfter = "AICar0";
		carNumber = this.name.Substring(this.name.IndexOf(splitAfter) + splitAfter.Length);
		
		//Debug.Log(carNumber);
		carName = AICar.name;
		carNum = int.Parse(carNumber);
		
		if(DriverNames.isOfficialSeries(seriesPrefix) == true){
			officialSeries = true;
		}
		
		if(officialSeries == true){
			setCarPhysics(seriesPrefix);
		} else {
			setCarPhysics(ModData.getPhysicsModel(seriesPrefix));
		}
		
		if(officialSeries == true){
			carTeam = DriverNames.getTeam(seriesPrefix,carNum);
			carManu = DriverNames.getManufacturer(seriesPrefix,carNum);
			carType = DriverNames.getType(seriesPrefix,carNum);
			carRarity = DriverNames.getRarity(seriesPrefix,carNum);
		} else {
			carTeam = ModData.getTeam(seriesPrefix,carNum);
			carManu = ModData.getManufacturer(seriesPrefix,carNum);
			carType = ModData.getType(seriesPrefix,carNum);
			carRarity = ModData.getRarity(seriesPrefix,carNum);
		}
		
		AICarClass = PlayerPrefs.GetInt("SubseriesMinClass");
		
		Renderer liveryRend = this.transform.Find("Plane").GetComponent<Renderer>();
		Renderer numRend = this.transform.Find("Number").GetComponent<Renderer>();
		
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
		
		string chosenAlt;
		if(PlayerPrefs.HasKey("RaceAltPaint" + carNumber)){
			//Load the pre-picked alt paint
			chosenAlt = PlayerPrefs.GetString("RaceAltPaint" + carNumber);
			//Debug.Log("Remembered to show the #" + carNumber + " Alt");
		} else {
			if(PlayerPrefs.HasKey("RaceAltPaintsChosen")){
				chosenAlt = "0";
			} else {
				altPaints = new List<string>();
				//altPaints.Add("0");
				altPaints.Add("0");
				altPaints.Add("0");
				altPaints.Add("0");
				altPaints.Add("0");
				if(officialSeries == true){
					AltPaints.loadAlts();
					for(int i=1;i<10;i++){
						if(AltPaints.getAltPaintName(seriesPrefix,carNum,i) != null){
							//Debug.Log("Alt Paint #" + carNum + " Alt " + i + " could spawn");
							if(AltPaints.getAltPaintAISpawning(seriesPrefix,carNum,i) != true){
								altPaints.Add(i.ToString());
								//Debug.Log("Added #" + carNum + " Alt " + i + " to the spawn list");
							}
						}
					}
				} else {
					for(int i=1;i<10;i++){
						if(ModData.getAltTexture(seriesPrefix,carNum,i) != null){
							Debug.Log("Mod Alt Paint #" + carNum + " Alt " + i + " could spawn");
							altPaints.Add(i.ToString());
						}
					}
				}
				int altIndex = Random.Range(0,altPaints.Count);
				chosenAlt = altPaints[altIndex];
				if(chosenAlt != "0"){
					PlayerPrefs.SetInt("RaceAltPaintsChosen",1);
					PlayerPrefs.SetString("RaceAltPaint" + carNumber,chosenAlt);
					//Debug.Log("Set the #" + carNumber + " Alt");
				}
			}
		}
		
		if(PlayerPrefs.HasKey("CustomNumber" + seriesPrefix + carNumber)){
			if(chosenAlt != "0"){
				liveryRend.material.mainTexture = Resources.Load(seriesPrefix + "livery" + carNumber + "blankalt" + chosenAlt) as Texture;
			} else {
				liveryRend.material.mainTexture = Resources.Load(seriesPrefix + "livery" + carNumber + "blank") as Texture;
			}
			customNum = PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + carNumber);
			numRend.material.mainTexture = Resources.Load("cup20num" + customNum) as Texture;
			//Debug.Log("Custom number #" + customNum + " applied to car " + carNum + "Var: " + seriesPrefix + "num" + customNum);
		} else {
			if(chosenAlt != "0"){
				Debug.Log("Custom alt spawned - Car #" + carNumber);
				if(officialSeries == true){
					//Debug.Log("Stock Series - Spawn car paint");
					liveryRend.material.mainTexture = Resources.Load(seriesPrefix + "livery" + carNumber + "alt" + chosenAlt) as Texture;
				} else {
					//Debug.Log("Mod Series - Spawn car paint");
					liveryRend.material.mainTexture = ModData.getAltTexture(seriesPrefix,int.Parse(carNumber),int.Parse(chosenAlt));
				}
			} else {
				if(officialSeries == true){
					//Debug.Log("Stock Series - Spawn car paint");
					liveryRend.material.mainTexture = Resources.Load(seriesPrefix + "livery" + carNumber) as Texture;
				} else {
					//Debug.Log("Non official series, get mod texture");
					liveryRend.material.mainTexture = ModData.getTexture(seriesPrefix,carNum);
				}
			}
			numRend.enabled = false;
			//Debug.Log("No custom number saved");
		}
		
		circuitLanes = PlayerPrefs.GetInt("CircuitLanes");
		
		apronLineX = -2.7f;
		apronLineX = 1.2f - ((circuitLanes - 1) * 1.2f) - 0.3f;

		AIVariTopSpeed = AITopSpeed + (carRarity / 4f) + (AILevel / 4f) + (laneInv / 4f);

		dominator = false;
		
		if(officialSeries == true){
			if (DriverNames.getType(seriesPrefix,carNum) == "Dominator"){
				dominator = true;
			}
		}

		if(officialSeries == true){
			if (DriverNames.getType(seriesPrefix,carNum) == "Strategist"){
				if(seriesPrefix == "irl23"){
					AICarClass+=4;
				}
				switch(AICarClass){
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
				dooredStrength = 40 + (carRarity * 15);
				if(dooredStrength > 95){
					dooredStrength = 95;
				}
				wreckProbability = (wreckFreq * 3) + 1;
			}
		}

		maxDraftDistance = 9 + carRarity;
		if(officialSeries == true){
			if (DriverNames.getType(seriesPrefix,carNum) == "Closer"){
				maxDraftDistance = 9 + carRarity + AICarClass;		
			}
		}
		
		if(officialSeries == true){
			if (DriverNames.getType(seriesPrefix,carNum) == "Rookie"){
				wreckProbability = (wreckFreq * 3) + 1;
			}
		}

        movingLane = false;
        backingOut = false;

        crashTime = 50;

        wobbleCount = 1;
        wobblePos = 0;
        wobbleTarget = 0;
        wobbleRand = Random.Range(100, 500);
    }

	void OnDestroy(){
		raycastBatch.Dispose();
		raycastHits.Dispose();
	}

    void OnCollisionEnter(Collision carHit) {
		
        if ((carHit.gameObject.tag == "AICar") || 
		    (carHit.gameObject.tag == "Player") || 
			(carHit.gameObject.tag == "Barrier") || 
			(carHit.gameObject.name == "OuterWall") ||
			(carHit.gameObject.name == "SaferBarrier") ||
			(carHit.gameObject.name == "TrackLimit") ||
			(carHit.gameObject.name == "FixedKerb")) {
			
			//Delicate mod - Everybody wrecks
			if((isWrecking == false)&&(Movement.delicateMod == true)){
				//Debug.Log("Wreck: Delicate Mod");
				startWreck();
			}
			
			//Join wreck
			if(carHit.gameObject.tag == "AICar"){
				bool joinWreck = carHit.gameObject.GetComponent<AIMovement>().isWrecking;
				if(joinWreck == true){
					if(isWrecking == false){
						//Debug.Log("Wreck: Joining In");
						startWreck();
						this.transform.Find("TireSmoke").GetComponent<ParticleSystem>().Play();
					} else {
						//Share some wreck inertia
						float opponentWreckDecel = carHit.gameObject.GetComponent<AIMovement>().wreckDecel;
						wreckDecel += ((opponentWreckDecel - wreckDecel) / 2);
					}
				}
			}
			if(carHit.gameObject.tag == "Player"){
				bool joinWreck = Movement.isWrecking;
				if(joinWreck == true){
					if(isWrecking == false){
						//Debug.Log("Wreck: Joining Player");
						startWreck();
						this.transform.Find("TireSmoke").GetComponent<ParticleSystem>().Play();
					} else {
						//Share some wreck inertia
						wreckDecel += ((Movement.playerWreckDecel - wreckDecel) / 2);
					}
				}
			}
						
			if (laneticker != 0){
				if (laneticker > 0){
					bool rightSideHit = checkRaycast("RightCorners", 0.51f);
					if(rightSideHit == true){
						backingOut = true;
						laneticker = -laneChangeDuration + laneticker;
						lane--;
					}
				}
				if (laneticker < 0){
					bool leftSideHit = checkRaycast("LeftCorners", 0.51f);
					if(leftSideHit == true){
						backingOut = true;
						laneticker = laneChangeDuration + laneticker;
						lane++;
					}
				}
			} else {
				int dooredStrength = 25;
				if(carHit.gameObject.tag == "Player"){
					hitByPlayer = true;
					dooredStrength = carHit.gameObject.GetComponent<Movement>().dooredStrength;
				} else {
					if(carHit.gameObject.tag == "AICar"){
						dooredStrength = carHit.gameObject.GetComponent<AIMovement>().dooredStrength;
					}
				}
				if(doored("Left",dooredStrength) == true){
					changeLane("Right");
				}
				if(doored("Right",dooredStrength) == true){
					changeLane("Left");
				}
			}
			AISpeed -= 0.5f;
        }
		
		if(carHit.gameObject.name == "OuterWall"){
			this.transform.Find("SparksR").GetComponent<ParticleSystem>().Play();
			sparksCooldown = Random.Range(5,20);
		}
		
		if ((carHit.gameObject.tag == "AICar") || 
		    (carHit.gameObject.tag == "Player") || 
			(carHit.gameObject.tag == "Barrier")){
				
			if(leftSideClear(0.51f) == false){
				this.transform.Find("SparksL").GetComponent<ParticleSystem>().Play();
				sparksCooldown = Random.Range(5,20);
			}
			if(rightSideClear(0.51f) == false){
				this.transform.Find("SparksR").GetComponent<ParticleSystem>().Play();
				sparksCooldown = Random.Range(5,20);
			}
		}
    }
	
	void OnCollisionStay(Collision carHit) {
		if(laneticker != 0){
			antiGlitch++;
		}
		
		//Join wreck
		if(carHit.gameObject.tag == "AICar"){
			bool joinWreck = carHit.gameObject.GetComponent<AIMovement>().isWrecking;
			if(joinWreck == true){
				if(isWrecking == false){
					//Debug.Log("Wreck: Joining In");
					startWreck();
					this.transform.Find("TireSmoke").GetComponent<ParticleSystem>().Play();
				} else {
					//Share some wreck inertia
					float opponentWreckDecel = carHit.gameObject.GetComponent<AIMovement>().wreckDecel;
					wreckDecel += ((opponentWreckDecel - wreckDecel) / 2);
				}
			}
		}
		if(carHit.gameObject.tag == "Player"){
			bool joinWreck = Movement.isWrecking;
			if(joinWreck == true){
				if(isWrecking == false){
					//Debug.Log("Wreck: Joining Player");
					startWreck();
					this.transform.Find("TireSmoke").GetComponent<ParticleSystem>().Play();
				} else {
					//Share some wreck inertia
					wreckDecel += ((Movement.playerWreckDecel - wreckDecel) / 2);
				}
			}
		}
    }
	
	void OnCollisionExit(Collision carHit) {
		antiGlitch = 0;
		particleDisableDelay = 20;
    }
	
	void ReceivePush(GameObject pushedBy){
		//Debug.Log(AICar.name + " gets pushed at speed of " + AISpeed);
		if(initialContact == false){
			float bumpSpeed;
			if(pushedBy.tag == "AICar"){
				bumpSpeed = pushedBy.GetComponent<AIMovement>().AISpeed;
			} else {
				bumpSpeed = Movement.playerSpeed;
			}	
			float midSpeed = bumpSpeed - AISpeed;
			if((midSpeed > 4f)||(midSpeed < -4f)){
				//Debug.Log("Wreck: Strong Push");
				float rng = Random.Range(0,500);
				//Debug.Log("Wreck Rng: " + rng + " < " + wreckProbability);
				if(wreckProbability >= rng){
					startWreck();
				}
			}
			//For some reason changing this makes the player bump-draft mega fast!
			AISpeed += midSpeed/4;
			//Debug.Log(AICar.name + " is now at speed of " + AISpeed);
			initialContact = true;
			tandemDraft = true;
		} else {
			AISpeed += backdraftMulti;
		}
		if(isWrecking == false){			
			pushedBy.SendMessage("UpdateTandemPosition",tandemPosition);
			//Debug.Log(AICar.name + " sends push back to " + pushedBy.name);
			pushedBy.SendMessage("GivePush",AISpeed);
		}
	}
	
	void GivePush(float bumpSpeed){
		//if(carRarity == 4){
			//Debug.Log("Return to #" + carNum + " speed " + bumpSpeed + " (was " + AISpeed + ")");
		//}
		affectedAISpeed = bumpSpeed;
		//Discourage long draft trains
		if(tandemPosition > maxTandem){
			affectedAISpeed-=0.25f;
			coolEngine=true;
		}
	}
	
	void UpdateTandemPosition(int tandemPosInFront){
		tandemPosition = tandemPosInFront + 1;
		//Debug.Log("" + AICar.name + " is in tandem position" + tandemPosition);
	}

    // Update is called once per frame
    void FixedUpdate(){

		logicCycle = 20;
		tick++;
		if(tick>=logicCycle){
			tick=0;
		}

		pos = transform.position;

		if(isWrecking == false){
			if(sparksCooldown > 0){
				sparksCooldown--;
			}
		}
		if(sparksCooldown == 1){
			sparksCooldown--;
			this.transform.Find("SparksL").GetComponent<ParticleSystem>().Stop();
			this.transform.Find("SparksR").GetComponent<ParticleSystem>().Stop();	
		}

		lap = CameraRotate.lap;
		
		//Handle the time between pausing and Time setting to 0
		if(CameraRotate.gamePausedLate == true){
			prePause = true;
		} else {
			prePause = false;
		}
		
		if(CameraRotate.overtime == true){
			wreckProbability = wreckFreq + 1;
			
			if(CameraRotate.lap == CameraRotate.raceEnd){
				wreckProbability = (wreckFreq * 5) + 1;
			}
		}
		
        laneInv = 4 - lane;
		
		AIVariTopSpeed = AITopSpeed + (carRarity / 4f) + (AILevel / 5f) + (laneInv / 4f);
		
		if(Movement.pacing == false){
			if((isWrecking == true)||(wreckOver == true)){
				//Bail, drop all Movement logic
				if(wreckOver == false){
					wreckPhysics();
					if(AISpeed > 200){
						AISpeed -=0.005f;
					}
				} else {
					AISpeed = 0;
					if(Movement.wreckOver == true){
						targetForce = 0;
						//Make the crashed cars stay still if the player is also still
						this.GetComponent<Rigidbody>().mass = 25;
						this.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezePositionZ;
					} else {
						targetForce = 0 - Movement.speedoSpeed;
					}
					updateWindForce();
						
					this.GetComponent<ConstantForce>().force = new Vector3(0f,0f,windForce);
					this.transform.Find("TireSmoke").GetComponent<ParticleSystem>().Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
				}
				return;
			} else {
				holdLane++;
				speedLogic();
			}
			
			//Experimental, for CPU saves
			if(carNum%logicCycle == tick){
				//Debug.Log("Draft Logic cycle save, frame: " + carNum);
				draftLogic();
			}
			carWobble();
			updateMovement();
			this.gameObject.GetComponent<Rigidbody>().velocity = Vector3.zero;
			this.gameObject.GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
		} else {
			//Pacing speed
			AISpeed = 202;
		}
		
		/*if(debugPlayer == true){
			Debug.Log(AICar.name + " found draft");
		}*/
		
		//Schedule the required raycasts (that run every frame) to run during this frame in a batch job
		raycastBatch[0] = new RaycastCommand(pos, transform.forward, maxDraftDistance); //0. Forward centered
		raycastBatch[1] = new RaycastCommand(pos, transform.forward * -1, draftAirCushion); //1. Backward centered
		raycastBatch[2] = new RaycastCommand(pos + new Vector3(-1f,0,-1f), transform.forward, 1.5f); //2. Car Left Of Player
		raycastBatch[3] = new RaycastCommand(pos + new Vector3(1f,0,-1f), transform.forward, 1.5f); //3. Car Right Of Player
		
		raycastHandler = RaycastCommand.ScheduleBatch(raycastBatch, raycastHits, 4);
	}

	void speedLogic(){
		
		//Complete the raycasting that was scheduled during the previous frame
		raycastHandler.Complete();
		
		RaycastHit DraftCheckForward = raycastHits[0];
        RaycastHit DraftCheckBackward = raycastHits[1];
        bool HitForward = DraftCheckForward.distance > 0;
        bool HitBackward = DraftCheckBackward.distance > 0;
		
		if(debugPlayer == true){
			Debug.Log(AICar.name + " speedLogic start - " + AISpeed);
		}
		
		//If gaining draft of car in front
		if((HitForward && DraftCheckForward.distance <= maxDraftDistance)&&(coolEngine == false)){
			
			/*if(debugPlayer == true){
				Debug.Log(AICar.name + " found draft");
			}*/
			
			//Speed up
			if (AISpeed < AIVariTopSpeed){
				//Draft gets stronger as you get closer
				float draftStrength = ((maxDraftDistance - DraftCheckForward.distance)/draftStrengthRatio) + (AILevel / 2500);
				//If approaching max speed, taper off
				float diffToMax = AIVariTopSpeed - AISpeed;
				if((diffToMax) < 2){
					//If speed above max, it zeros the draft out (rev limiter)
					if(diffToMax < 0){
						diffToMax = 0;
					}
					draftStrength *= (diffToMax / 2) + 0.01f;
				}
				//AISpeed += draftStrength;
				AISpeed += draftStrength;
				
				/*
				if(debugPlayer == true){
					Debug.Log(AICar.name + " speeding up - speed " + AISpeed + " , variTopSpeed " + AIVariTopSpeed);
				}
				*/
			}
		} else {
			//Slow down
			if (AISpeed > 200){
				
				if(debugPlayer == true){
					Debug.Log(AICar.name + " slowing down, no forward draft");
				}
				//No draft, slow with drag
				if(dominator == true){
					AISpeed -= ((dragDecelMulti - (AILevel / 6000f)) / 2);
					
					#if UNITY_EDITOR
					if(debugPlayer == true){
						Debug.Log(AICar.name + " dominator slowing down by " + ((dragDecelMulti - (AILevel / 6000f)) / 2) + " , variTopSpeed " + AIVariTopSpeed);
					}
					#endif
				} else {
					float diffToMax = AIVariTopSpeed - AISpeed;
					if((diffToMax) < 2){
						if(diffToMax < 0){
							diffToMax = 0;
						}
						AISpeed -= (dragDecelMulti - (AILevel / 6000f)) * (2 - (diffToMax / 2));
						
						#if UNITY_EDITOR
						if(debugPlayer == true){
							Debug.Log(AICar.name + " close to max speed, slowing down by " + (dragDecelMulti - (AILevel / 6000f)) * (2 - (diffToMax / 2)));
						}
						#endif
					} else {
						AISpeed -= (dragDecelMulti - (AILevel / 6000f));
						
						#if UNITY_EDITOR
						if(debugPlayer == true){
							Debug.Log(AICar.name + " slowing down by " + (dragDecelMulti - (AILevel / 6000f)));
						}
						#endif
					}
				}
			}
		}
		
		#if UNITY_EDITOR
		if(debugPlayer == true){
			Debug.Log(AICar.name + " speedLogic front draft end - " + AISpeed);
		}
		#endif
		
		//If recieving backdraft from car behind
		if (HitBackward && DraftCheckBackward.distance <= draftAirCushion){
			
			#if UNITY_EDITOR
			if(debugPlayer == true){
				Debug.Log(AICar.name + " has backdraft");
			}
			#endif
			
			//Bump draft can't exceed slingshot speed (for balance)
			if(AISpeed <= (AIVariTopSpeed - 2f)){
				
				AISpeed += (backdraftMulti + (AILevel / 2000));
				
				#if UNITY_EDITOR
				if(debugPlayer == true){
					Debug.Log(AICar.name + " backdraft speed up by " + (backdraftMulti + (AILevel / 2000)));
				}
				#endif
			}
		}
		
		// If being bump-drafted from behind
		if (HitBackward && DraftCheckBackward.distance <= bumpDraftDistTrigger){
			if(AISpeed <= (AIVariTopSpeed - 2f)){
				AISpeed+=(backdraftMulti / 5f);
			}
			tandemDraft = true;
			int currentPos = Ticker.checkSingleCarPosition(AICar);
			if(currentPos == 0){
				if (AISpeed > (AIVariTopSpeed - 3f)){
					//Debug.Log("Leader is #" + carNum);
					evadeDraft();
				}
			}
		} else {
			tandemDraft = false;
		}

		//If bump-drafting the car in front
		if (HitForward && DraftCheckForward.distance <= bumpDraftDistTrigger){
			//Frontward draft
			if(DraftCheckForward.transform.gameObject.name != null){

				if (DraftCheckForward.distance <= bumpDraftDistTrigger){
					DraftCheckForward.transform.gameObject.SendMessage("ReceivePush",AICar);
				}
				if (AISpeed > (AIVariTopSpeed - 3f)){
					coolEngine = true;
					//Debug.Log("Hot Bump Draft. Cool Engine #" + carNum);
					//if(HitForward && DraftCheckForward.transform.gameObject.name == "Player"){
						//Debug.Log("Cooling down behind player #" + carNum);
					//}
				}
			}
			if(seriesPrefix == "irl23"){
				coolEngine = true;
			}
		} else {
			tandemDraft = false;
			tandemPosition = 1;
		}
		
		//If engine is too hot, stall out
		if(coolEngine == true){
			if (HitForward && DraftCheckForward.distance <= coolOffSpace){
				//Overheat weakens as you back away
				AISpeed -= (coolOffSpace - DraftCheckForward.distance)/coolOffInv;
				if(affectedAISpeed != 0){
					affectedAISpeed -= (coolOffSpace - DraftCheckForward.distance)/coolOffInv;
				}
			} else {
				coolEngine = false;
			}
		}
		
		//Speed tops out
        if (AISpeed > AIVariTopSpeed){
			//Hard limiter
            AISpeed = AIVariTopSpeed - 0.1f;
		}
		
		//Minimum speed
		if (AISpeed < (200 + speedRand + (carRarity / 8f) + (laneInv / 8f))){
			AISpeed = (200 + speedRand + (carRarity / 8f) + (laneInv / 8f));
		}
		
		if(affectedAISpeed != 0){
			AISpeed = affectedAISpeed;
			affectedAISpeed = 0;
		}
		
		//Speed difference between the player and the AI
		//speed = (AISpeed + wreckDecel) - (Movement.playerSpeed + Movement.playerWreckDecel);
		if(Movement.isWrecking == true){
			speed = (AISpeed + wreckDecel) - ControlCarMovement.controlSpeed - (Movement.playerWreckDecel * speedDiffPadding);
		} else {
			speed = (AISpeed + wreckDecel) - ControlCarMovement.controlSpeed;
		}
		speed = speed / 100;
		if(prePause != true){
			AICar.transform.Translate(0, 0, speed);
			relativeZToPlayer = speed;
		}
	}
	
	void dumbSpeed(int direction){
		AISpeed += ((0.001f + (AILevel / 5000)) * direction);
		
		//Speed difference between the player and the AI
		speed = (AISpeed + wreckDecel) - ControlCarMovement.controlSpeed;
		speed = speed / 100;
		if(prePause != true){
			AICar.transform.Translate(0, 0, speed);
		}
	}
	
	void setCarPhysics(string seriesPrefix){
		switch(seriesPrefix){
			case "irl23":
			case "indy":
			case "indycar":
			case "openwheel":
				draftStrengthRatio = 450f;
				dragDecelMulti = 0.003f;
				backdraftMulti = 0.015f;
				bumpDraftDistTrigger = 1.2f;
				draftAirCushion = 1.8f;
				passDistMulti = 1.8f;
				coolOffSpace = 1.5f;
				coolOffInv = 2.4f;
				tandemDrafting = false;
				break;
			case "cushion":
				draftStrengthRatio = 900f;
				dragDecelMulti = 0.004f;
				backdraftMulti = 0.004f;
				bumpDraftDistTrigger = 1.1f;
				passDistMulti = 1f;
				draftAirCushion = 1.2f;
				coolOffSpace = 1.4f;
				coolOffInv = 3f;
				tandemDrafting = true;
				break;
			case "v3weaker":
				draftStrengthRatio = 700f;
				dragDecelMulti = 0.004f;
				backdraftMulti = 0.005f;
				bumpDraftDistTrigger = 1.1f;
				passDistMulti = 1f;
				draftAirCushion = 1.1f;
				coolOffSpace = 1.2f;
				coolOffInv = 4f;
				tandemDrafting = true;
				break;
			default:
				draftStrengthRatio = 900f;
				dragDecelMulti = 0.0035f;
				backdraftMulti = 0.004f;
				bumpDraftDistTrigger = 1.1f;
				passDistMulti = 1f;
				draftAirCushion = 1.2f;
				coolOffSpace = 1.4f;
				coolOffInv = 5f;
				tandemDrafting = true;
				break;
		}
	}
	
	void wreckSpeed(){		
		//Speed difference between the player and the AI
		speed = (AISpeed + wreckDecel) - ControlCarMovement.controlSpeed;
		speed = speed / 100;
		if(prePause != true){
			AICar.transform.Translate(new Vector3(0, 0, speed),Space.World);
		}
	}

	void updateMovement() {
		
		//How fast can you switch lanes
        if (laneticker > 0){
			
			RaycastHit leftCastCheck;
			bool leftCastHit = Physics.Raycast(pos + new Vector3(0,0,0.98f), transform.right * -1, out leftCastCheck, 0.51f);
			if(leftCastHit == false){
				leftCastHit = Physics.Raycast(pos + new Vector3(0,0,-0.98f), transform.right * -1, out leftCastCheck, 0.51f);
			}
			
			if(leftCastHit == true){
				backingOut = true;
				laneticker = -laneChangeDuration + laneticker;
				lane--;
			} else {
				AICar.transform.Translate(-laneChangeSpeed, 0, 0);
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
				AICar.transform.Translate(laneChangeSpeed, 0, 0);
				laneticker++;
			}
        }

        if (laneticker == 0)
        {
            movingLane = false;
            backingOut = false;
        }
		
		if(pos.x <= apronLineX){
			//Debug.Log("Track Limits!");
			if (backingOut == false) {
				backingOut = true;
			}
			laneticker = -laneChangeDuration + laneticker;
			lane--;
		}
		
		if(pos.x >= 1.35f){
			//Debug.Log("Wall!");
			if (backingOut == false) {
				backingOut = true;
				float rng = Random.Range(0,500);
				//Debug.Log("Wreck Rng: " + rng + " < " + wreckProbability);
				if((wreckProbability >= rng)||
				(Movement.delicateMod == true)||
				((hitByPlayer == true)&&(CameraRotate.lap == CameraRotate.raceEnd))){
					startWreck();
					//Debug.Log("Wreck: Random Wall");
					this.transform.Find("TireSmoke").GetComponent<ParticleSystem>().Play();
				}
			}
			laneticker = laneChangeDuration + laneticker;
			lane++;
			//Wall hit decel
			AISpeed -= 1f;
		}
	}

	void draftLogic(){
		
		RaycastHit DraftCheckForwardZOffset;
        bool HitForwardZOffset = Physics.Raycast(transform.position + new Vector3(0.0f, 0.0f, 1.05f), transform.forward, out DraftCheckForwardZOffset, 5);
		
        //Debug.DrawRay(pos  + new Vector3(0.0f, 0.0f, 1.2f), Vector3.forward * 10, Color.green);
		//Debug.Log("Checking Draft Logic.. ");
		if(HitForwardZOffset == true){
			float carDist = DraftCheckForwardZOffset.distance;
			float opponentSpeed = getOpponentSpeed(DraftCheckForwardZOffset);
			bool tryTimedPass = timedPass(carDist, opponentSpeed);
			if(CameraRotate.cautionOut == true){
				//Debug.Log("Caution Weighted Draft Logic");
				GameObject oppCar = DraftCheckForwardZOffset.transform.gameObject;
				if(oppCar.GetComponent<AIMovement>() != null){
					if(oppCar.GetComponent<AIMovement>().isWrecking == true){
						avoidWreck();
						//Debug.Log(carName + " Avoids Wreck");
					}
				} else {
					if(oppCar.GetComponent<Movement>() != null){
						if(Movement.isWrecking == true){
							avoidWreck();
							//Debug.Log(carName + " Avoids Wrecking Player");
						}
					}
				}
			}
			
			if((holdLane >= laneRest)&&(tryTimedPass == false)){
				string opponentName = DraftCheckForwardZOffset.collider.gameObject.name;
				int opponentNum = 9999;
				string opponentTeam = "";
				if(opponentName != "Player") {
					opponentNum = getCarNumFromName(opponentName);
					if(opponentNum != 9999){
						if(officialSeries == true){
							opponentTeam = DriverNames.getTeam(seriesPrefix,opponentNum);
						} else {
							opponentTeam = ModData.getTeam(seriesPrefix,opponentNum);
						}
					}
				}

				//Pass them
				if(movingLane == false){
					tryPass(50, false);
				}
			}
			
			//Lift if about to bump draft with too much closing speed
			if((carDist < draftAirCushion)&&((AISpeed - opponentSpeed) > 1f)){
				slowUp(carDist);
			}
		} else {
			//Check further away
			RaycastHit DraftCheckForwardLong = raycastHits[0];
			bool HitForwardLong = DraftCheckForwardLong.distance > 0;
			
			if(HitForwardLong == true){
				float opponentSpeed = getOpponentSpeed(DraftCheckForwardLong);
				//Debug.Log("Something in front.. dist:" + DraftCheckForwardLong.distance);
				//Avoid slow moving draft
				if(CameraRotate.cautionOut == true){
					//Debug.Log("Caution Weighted Draft Logic");
					GameObject oppCar = DraftCheckForwardLong.transform.gameObject;
					if(oppCar.GetComponent<AIMovement>() != null){
						if(oppCar.GetComponent<AIMovement>().isWrecking == true){
							avoidWreck();
							//Debug.Log(carName + " Avoids Wreck");
						}
					} else {
						if(oppCar.GetComponent<Movement>() != null){
							if(Movement.isWrecking == true){
								avoidWreck();
								//Debug.Log(carName + " Avoids Wrecking Player");
							}
						}
					}
				} else {
					//Debug.Log("Nothing in front..");
					if(opponentSpeed > (AISpeed + 1.5f)){
						findClearLane();
					}
				}
			} else {
				findDraft();
			}
			//raycastBatch[3] = new RaycastCommand(pos + new Vector3(0.0f, 0.0f, 1.2f), transform.forward, maxDraftDistance); //Forward Long Dist
		}
	}
	
	void slowUp(float carDist){
		AISpeed -= (draftAirCushion - carDist)/2;
		//Debug.Log("BRAKE! #" + carNumber);
	}
	
	void carWobble(){
		wobbleCount++;
		
		if(wobbleCount >= wobbleRand){
			wobbleRand = Random.Range(10,60);
			wobbleTarget = Random.Range(-110,110);
			wobbleCount = 1;
		}
		
		//General wobble while in lane
		if(wobbleTarget > wobblePos){
			AICar.transform.Translate(-0.001f,0,0);
			wobblePos++;
		}
		if(wobbleTarget < wobblePos){
			AICar.transform.Translate(0.001f,0,0);
			wobblePos--;
		}
	}

	void evadeDraft(){
		//Trying to pass nobody = Weaving to break the draft from behind
		//tryPass(100, false);
		
		bool leftSideClr = leftSideClear();
		bool rightSideClr = rightSideClear();
		
		//Can go either way
		if((leftSideClr == true)&&(rightSideClr == true)){
			//Rand pick
			string direction = "";
			float rng = Random.Range(0,1.99999f);
			if(rng > 1f){
				direction = "Right";
			} else {
				direction = "Left";
			}
			changeLane(direction);
		} else {
			if(rightSideClr == true) {
				changeLane("Right");
			} else {
				if(leftSideClr == true) {
					changeLane("Left");
				}
			}
		}
	}

	void tryPass(int chance, bool forced) {
		//Check Right Corners Clear
		float rng = Random.Range(0,100);
		if(chance >= rng) {
			bool rightSideClr = rightSideClear();
			if((rightSideClr == true)||(forced == true)) {
				if(Movement.wallrideMod == true){
					//This hack keeps the top lane empty for a wallride
				} else {
					changeLane("Right");
				}
			} else {
				bool leftSideClr = leftSideClear();
				if(leftSideClr == true) {
					changeLane("Left");
				}
			}
		}
	}
	
	bool timedPass(float distance, float opponentSpeed) {
		
		if((AISpeed - opponentSpeed) > (2.5f * passDistMulti)){
			tryPass(50, false);
			//Debug.Log("Car #" + carNum + " attempts large pass. Speed diff:" + (AISpeed - opponentSpeed) + " Distance:" + distance);
			return true;
		}
		if(distance < (2.5f * passDistMulti)){
			if((AISpeed - opponentSpeed) > (1.5f * passDistMulti)){
				//Debug.Log("Car #" + carNum + " attempts long pass. Speed diff:" + (AISpeed - opponentSpeed) + " Distance:" + distance);
				tryPass(50, false);
				return true;
			}
		}
		if(distance < (1.5f * passDistMulti)){
			if((AISpeed - opponentSpeed) > (1f * passDistMulti)){
				//Debug.Log("Car #" + carNum + " attempts ideal pass. Speed diff:" + (AISpeed - opponentSpeed) + " Distance:" + distance);
				tryPass(50, false);
				return true;
			}
		}
		if(distance < (1.25f * passDistMulti)){
			if((AISpeed - opponentSpeed) > (0.3f * passDistMulti)){
				//Debug.Log("Car #" + carNum + " attempts close pass. Speed diff:" + (AISpeed - opponentSpeed) + " Distance:" + distance);
				tryPass(50, false);
				return true;
			}
			if(tandemDrafting == false){
				if((AISpeed - opponentSpeed) >= 0){
					tryPass(50, false);
					return true;
				}
			}
		}
		return false;
	}
	
	public void findDraft(){
		RaycastHit DraftCheckLaneLeft;
		RaycastHit DraftCheckLaneRight;
		bool HitLaneLeft = Physics.Raycast(pos + new Vector3(-1.2f,0,1.1f), transform.forward, out DraftCheckLaneLeft, 10);
		bool HitLaneRight = Physics.Raycast(pos + new Vector3(1.2f,0,1.1f), transform.forward, out DraftCheckLaneRight, 10);
		string direction = "";
		
		if(HitLaneLeft){
			if(leftSideClear()){
				float opponentSpeed = getOpponentSpeed(DraftCheckLaneLeft);
				if(DraftCheckLaneLeft.distance >= 3.5f){
					if((opponentSpeed >= (AISpeed - 0.1f))||(AISpeed < 201)){
						//Go for it if slight speed diff, just for the draft
						direction = "Left";
					}
				} else {
					//Only seek a close draft if faster than you
					if((opponentSpeed >= (AISpeed + 0.1f))||(AISpeed < 201)){
						direction = "Left";
						//Debug.Log("Opponent Speed " + opponentSpeed + " ahead of AISpeed " + AISpeed);
					}
				}
			}
		}
		
		if(HitLaneRight){
			if(rightSideClear()){
				float opponentSpeed = getOpponentSpeed(DraftCheckLaneRight);
				if(DraftCheckLaneRight.distance >= 3.5f){
					if((opponentSpeed >= (AISpeed - 0.1f))||(AISpeed < 201)){
						if(direction == "Left"){
							direction = "Both";
						} else {
							direction = "Right";
						}
					}
				} else {
					if((opponentSpeed >= (AISpeed + 0.1f))||(AISpeed < 201)){
						if(direction == "Left"){
							direction = "Both";
						} else {
							direction = "Right";
						}
					}
				}
			}
		}
		
		//If an option exists..
		if(direction != ""){
			if(direction == "Both"){
				//Random choose one
				float rng = Random.Range(0,2);
				Debug.Log("Rnd /2 = " + rng);
				if(rng > 1f){
					direction = "Right";
				} else {
					direction = "Left";
				}
			}
			changeLane(direction);
		}
	}
	
	public void findClearLane(bool wrecking = false){
		RaycastHit DraftCheckLaneLeft;
		RaycastHit DraftCheckLaneRight;
		bool HitLaneLeft = Physics.Raycast(pos + new Vector3(-1.2f,0,1.1f), transform.forward, out DraftCheckLaneLeft, 10);
		bool HitLaneRight = Physics.Raycast(pos + new Vector3(1.2f,0,1.1f), transform.forward, out DraftCheckLaneRight, 10);
		string direction = "";
		
		if(leftSideClear()){
			if(HitLaneLeft == false){
				//Left lane is clear
				direction = "Left";
			} else {
				//Left lane isn't clear, but is moving faster than yourself
				//Or, simply aiming to follow non-wrecking cars
				if(wrecking == true){
					if(DraftCheckLaneLeft.transform.gameObject.GetComponent<AIMovement>() != null){
						if(DraftCheckLaneLeft.transform.gameObject.GetComponent<AIMovement>().isWrecking == false){
							direction = "Left";
						}
					}
				} else {
					float opponentSpeed = getOpponentSpeed(DraftCheckLaneLeft);
					if(opponentSpeed > (AISpeed + 0.1f)){
						direction = "Left";
					}
				}
			}
		}
		
		if(rightSideClear()){
			if(HitLaneRight == false){
				if(direction == "Left"){
					direction = "Both";
				} else {
					direction = "Right";
				}
			} else {
				if(wrecking == true){
					if(DraftCheckLaneRight.transform.gameObject.GetComponent<AIMovement>() != null){
						if(DraftCheckLaneRight.transform.gameObject.GetComponent<AIMovement>().isWrecking == false){
							if(direction == "Left"){
								direction = "Both";
							} else {
								direction = "Right";
							}
						}
					}
				} else {
					float opponentSpeed = getOpponentSpeed(DraftCheckLaneRight);
					if(opponentSpeed > (AISpeed + 0.1f)){
						if(direction == "Left"){
							direction = "Both";
						} else {
							direction = "Right";
						}
					}
				}
			}
		}
		
		//If an option exists..
		if(direction != ""){
			if(direction == "Both"){
				//Random choose one
				float rng = Random.Range(0,2);
				Debug.Log("Rnd /2 = " + rng);
				if(rng > 1f){
					direction = "Right";
				} else {
					direction = "Left";
				}
			}
			changeLane(direction);
		}
	}
	
	public void avoidWreck(){
		RaycastHit DraftCheckLaneLeft;
		RaycastHit DraftCheckLaneRight;
		bool HitLaneLeft = Physics.Raycast(pos + new Vector3(-1.2f,0,1.1f), transform.forward, out DraftCheckLaneLeft, 20);
		bool HitLaneRight = Physics.Raycast(pos + new Vector3(1.2f,0,1.1f), transform.forward, out DraftCheckLaneRight, 20);
		string direction = "";
		
		if(leftSideClear()){
			if(HitLaneLeft == false){
				//Left lane is clear
				direction = "Left";
			} else {
				//Left lane isn't clear, but is moving faster than yourself
				//Or, simply aiming to follow non-wrecking cars
				if(DraftCheckLaneLeft.transform.gameObject.GetComponent<AIMovement>() != null){
					if(DraftCheckLaneLeft.transform.gameObject.GetComponent<AIMovement>().isWrecking == false){
						direction = "Left";
					}
				}
			}
		}
		
		if(rightSideClear()){
			if(HitLaneRight == false){
				if(direction == "Left"){
					direction = "Both";
				} else {
					direction = "Right";
				}
			} else {
				if(DraftCheckLaneRight.transform.gameObject.GetComponent<AIMovement>() != null){
					if(DraftCheckLaneRight.transform.gameObject.GetComponent<AIMovement>().isWrecking == false){
						if(direction == "Left"){
							direction = "Both";
						} else {
							direction = "Right";
						}
					}
				}
			}
		}
		
		//If an option exists..
		if(direction != ""){
			if(direction == "Both"){
				//Random choose one
				float rng = Random.Range(0,2);
				//Debug.Log("Rnd /2 = " + rng);
				if(rng > 1f){
					direction = "Right";
				} else {
					direction = "Left";
				}
			}
			changeLane(direction);
		}
	}
	
	public bool leftSideClear(float checkDist = 1.5f){
		
		RaycastHit checkLaneLeft = raycastHits[2];
        bool hitLaneLeft = checkLaneLeft.distance > 0;
		
		//Check Left Lane Clear
		if (!(hitLaneLeft)){
			return true;
		} else {
			return false;
		}	
	}
	
	public bool rightSideClear(float checkDist = 1.5f){
		
		RaycastHit checkLaneRight = raycastHits[3];		
        bool hitLaneRight = checkLaneRight.distance > 0;
		
		//Check Right Lane Clear
		if (!(hitLaneRight)){
			return true;
		} else {
			return false;
		}
	}
	
	public bool doored(string side, float chance){
		
		float randChance = Random.Range(0,100);
		
		if (randChance > chance){
			return false;
		}
		
		if(side == "Left"){
			
			RaycastHit collisionLeftF;
			RaycastHit collisionLeftB;
			bool dooredLeftF = Physics.Raycast(pos + new Vector3(-0.52f,0,-1f), transform.forward, out collisionLeftF, 2);
			bool dooredLeftB = Physics.Raycast(pos + new Vector3(-0.52f,0,1f), transform.forward * -1, out collisionLeftB, 2);
			
			if (((dooredLeftF) && (collisionLeftF.distance <= 2)) ||
			   ((dooredLeftB) && (collisionLeftB.distance <= 2))) {
				//Debug.Log("Doored!");
				return true;
			} else {
				return false;
			}
		} else {
			if(side == "Right"){
				
				RaycastHit collisionRightF;
				RaycastHit collisionRightB;
				bool dooredRightF = Physics.Raycast(pos + new Vector3(0.52f,0,-1f), transform.forward, out collisionRightF, 2);
				bool dooredRightB = Physics.Raycast(pos + new Vector3(0.52f,0,1f), transform.forward * -1, out collisionRightB, 2);
				
				if (((dooredRightF) && (collisionRightF.distance <= 2)) ||
				   ((dooredRightB) && (collisionRightB.distance <= 2))) {
					//Debug.Log("Doored!");
					return true;
				} else {
					return false;
				}
			} else {
				//Debug.Log("Invalid doored value");
				return false;
			}
		}
	}
	
	public void changeLane(string direction){
		
		if(laneticker == 0){
			//Debug.Log("Lane change!");
			if(direction == "Left"){
				laneticker = laneChangeDuration;
				lane++;
			} else {
				if(direction == "Right"){
					laneticker = -laneChangeDuration;
					lane--;
				} else {
					if(direction == "Find"){
						//Debug.Log("Not sure where to move");
					} else {
						//Debug.Log("Invalid lane change");
					}
				}
			}
			laneRest = Random.Range(100, 1000);
			movingLane = true;
			holdLane = 0;
		}
	}
	
	public void setLaneRest(){
		if(lap >= 4){
			laneRest = Random.Range(0, 1);
		}
	}
	
	int getCarNumFromName(string theName){
		string splitAfter = "AICar0";
		carNumber = theName.Substring(theName.IndexOf(splitAfter) + splitAfter.Length);
		int parsedNum;
		bool isNum = int.TryParse(carNumber, out parsedNum);
		if(isNum == true) {
			return parsedNum;
		} else {
			return 9999;
		}
	}
	
	float getOpponentSpeed(RaycastHit opponent){
		if(opponent.transform.gameObject.name != null){
			if(opponent.transform.gameObject.name == "Player"){
				return opponent.transform.gameObject.GetComponent<Movement>().gettableSpeed;
			} else {
				if(opponent.transform.gameObject.tag == "AICar"){
					//Debug.Log(opponent.transform.gameObject.name);
					return opponent.transform.gameObject.GetComponent<AIMovement>().AISpeed;
				}
			}
		}
		return 9999;
	}
	
	bool checkRaycast(string rayDirection, float rayLength){
		bool rayHit;
		RaycastHit DraftCheck;
		if(rayLength == null){
			rayLength = 100;
		}
		switch(rayDirection){
			case "Front":
				rayHit = Physics.Raycast(pos, transform.forward, out DraftCheck, rayLength);
				break;
			case "Rear":
				rayHit = Physics.Raycast(pos, transform.forward * -1, out DraftCheck, rayLength);
				break;
			case "Left":
				rayHit = Physics.Raycast(pos, transform.right * -1, out DraftCheck, rayLength);
				break;
			case "Right":
				rayHit = Physics.Raycast(pos, transform.right, out DraftCheck, rayLength);
				break;
			case "FrontCorners":
				rayHit = Physics.Raycast(pos + new Vector3(0.48f,0,0), transform.forward, out DraftCheck, rayLength);
				if(rayHit == false){
					rayHit = Physics.Raycast(pos + new Vector3(-0.48f,0,0), transform.forward, out DraftCheck, rayLength);
				}
				break;
			case "RearCorners":
				rayHit = Physics.Raycast(pos - new Vector3(0.48f,0,0), transform.forward * -1, out DraftCheck, rayLength);
				if(rayHit == false){
					rayHit = Physics.Raycast(pos - new Vector3(-0.48f,0,0), transform.forward * -1, out DraftCheck, rayLength);
				}
				break;
			case "LeftDiags":
				rayHit = Physics.Raycast(pos, transform.forward - transform.right, out DraftCheck, rayLength);
				rayHit = Physics.Raycast(pos, (transform.forward * -1) - transform.right, out DraftCheck, rayLength);
				break;
			case "RightDiags":
				rayHit = Physics.Raycast(pos, transform.forward + transform.right, out DraftCheck, rayLength);
				rayHit = Physics.Raycast(pos, (transform.forward * -1) + transform.right, out DraftCheck, rayLength);
				break;
			case "LeftCorners":
				rayHit = Physics.Raycast(pos + new Vector3(0,0,0.98f), transform.right * -1, out DraftCheck, rayLength);
				//Debug.DrawRay(pos + new Vector3(0,0,0.98f), transform.right * -0.52f, Color.yellow);
				if(rayHit == false){
					rayHit = Physics.Raycast(pos + new Vector3(0,0,-0.98f), transform.right * -1, out DraftCheck, rayLength);
					//Debug.DrawRay(pos + new Vector3(0,0,-0.98f), transform.right * -0.52f, Color.yellow);
				}
				break;
			case "RightCorners":
				rayHit = Physics.Raycast(pos + new Vector3(0,0,0.98f), transform.right, out DraftCheck, rayLength);
				//Debug.DrawRay(pos + new Vector3(0,0,0.98f), transform.right * 0.52f, Color.yellow);
				if(rayHit == false){
					rayHit = Physics.Raycast(pos + new Vector3(0,0,-0.98f), transform.right, out DraftCheck, rayLength);
					//Debug.DrawRay(pos + new Vector3(0,0,-0.98f), transform.right * 0.52f, Color.yellow);
				}
				break;
			case "LeftEdge":
				rayHit = Physics.Raycast(pos + new Vector3(-1f,0,-1f), transform.forward, out DraftCheck, rayLength);
				//Debug.DrawRay(pos + new Vector3(-1f,0,-1f), Vector3.forward * 2, Color.red);
				break;
			case "RightEdge":
				rayHit = Physics.Raycast(pos + new Vector3(1f,0,-1f), transform.forward, out DraftCheck, rayLength);
				//Debug.DrawRay(pos + new Vector3(1f,0,-1f), Vector3.forward * 2, Color.red);
				break;
			default:
				//Debug.Log("Invalid Raycast Direction");
				rayHit = false;
				break;
		}
		return rayHit;
	}
	
	void startWreck(){
		
		//Bailout
		if((isWrecking == true)||(wreckOver == true)){
			return;
		}
		
		Movement.incrTotalWreckers();
		
		isWrecking = true;
		if(CameraRotate.cautionOut == false){
			CameraRotate.throwCaution();
		}
		sparksCooldown = 99999;
		//Debug.Log(this.name + " is wrecking");
		
		//Make the car light, more affected by physics
		this.GetComponent<Rigidbody>().mass = 2 + wreckMassRand;
		
		//Remove constraints, allowing it to impact/spin using physics
		this.GetComponent<Rigidbody>().constraints &= ~RigidbodyConstraints.FreezeRotationY;
		this.GetComponent<Rigidbody>().constraints &= ~RigidbodyConstraints.FreezePositionX;
		
		//Remove forces, physics only
		this.GetComponent<Rigidbody>().isKinematic = false;
		this.GetComponent<Rigidbody>().useGravity = false;
		
		//Apply wind/drag
		targetForce = 0;
		windForce = targetForce;
		forceSmoothing = 0.2f;
		baseDecel = -0.25f;
		randDecel = Random.Range(0.01f,0.1f);
		slideX = 0;
		wreckDecel = 0;
		this.GetComponent<ConstantForce>().force = new Vector3(0f, 0f,windForce);
		this.GetComponent<ConstantForce>().torque = new Vector3(0f, Random.Range(-0.5f, 0.35f) * 10, 0f);
	}
	
	public void endWreck(){
		//Debug.Log(this.name + " WRECKED");
		AISpeed = 0;
		baseDecel = -0.25f;
		slideX = 0;
		wreckDecel = 0;
		targetForce = 0;
		isWrecking = false;
		wreckOver = true;
		
		//Skip force smoothing updateWindforce() on this transition frame
		targetForce = 0 - Movement.speedoSpeed;
		windForce = 0 - Movement.speedoSpeed;
		
		sparksCooldown = 0;
		this.GetComponent<Rigidbody>().mass = 25;
		//this.GetComponent<Rigidbody>().isKinematic = true;
		//this.GetComponent<Rigidbody>().useGravity = true;
		this.GetComponent<ConstantForce>().force = new Vector3(0f,0f,windForce);
		this.GetComponent<ConstantForce>().torque = new Vector3(0f,0f,0f);
		if(Movement.wreckOver == true){
			this.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezePositionZ;
		}
		this.transform.Find("SparksL").GetComponent<ParticleSystem>().Stop();
		this.transform.Find("SparksR").GetComponent<ParticleSystem>().Stop();
		this.transform.Find("TireSmoke").GetComponent<ParticleSystem>().Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
	}
	
	void wreckPhysics(){
		wreckAngle = this.gameObject.transform.rotation.y;
		float wreckSine = Mathf.Sin(wreckAngle);
		if(wreckSine < 0){
			wreckSine = -wreckSine;
		}
		baseDecel-=(0.4f - randDecel);
		slideX = ((baseDecel + 1) / 5f) + wreckSlideRand;
		//Formula: -200f = -10x, -140f = 0x, 0f = 10x
		//         -200f = -20x, -100f = -10x, 0f = 0x
		//         -200f = -6x, -140f = 0x, 0f = 14f
		//slideX = ((baseDecel + 1) / 10f) + 14f
		//Reduce division factor to increase effect
		
		updateWindForce();
		
		//baseDecel-=0.02f * 1;//CameraRotate.currentTurnSharpness();
		
		// Move relative to stopped player
		if(Movement.wreckOver == true){
			/*if(playerWrecked == false){
				//Acknowledge physics change when player is stopped, reset momentum this frame
				playerWrecked = true;
				//Make them skate to a stop - amplified for effect
				this.GetComponent<Rigidbody>().mass = 2.5f;
			}
			targetForce = wreckDecel + 200f;
			
			//Can't move backwards if the player is stopped
			if(windForce < 0){
				windForce = -windForce;
			}
			if(CameraRotate.onTurn == true){
				this.GetComponent<ConstantForce>().force = new Vector3(slideX, 0f,windForce);
			} else {
				this.GetComponent<ConstantForce>().force = new Vector3(wreckFlatRand, 0f,windForce);
			}
			this.GetComponent<Rigidbody>().velocity = new Vector3(slideX, 0f,windForce/10f);*/
		} else {
			//Standard relativity
			targetForce = wreckDecel;
			//this.GetComponent<ConstantForce>().force = new Vector3(slideX, 0f,windForce);
			if(CameraRotate.onTurn == true){
				this.GetComponent<ConstantForce>().force = new Vector3(slideX, 0f,windForce);
			} else {
				this.GetComponent<ConstantForce>().force = new Vector3(wreckFlatRand, 0f,windForce);
			}
			//this.GetComponent<Rigidbody>().velocity = new Vector3(slideX, 0f,windForce);
		}

		wreckDecel = baseDecel - (50f * wreckSine);
		
		if(wreckDecel < -200){
			endWreck();
		}
		
		this.GetComponent<Rigidbody>().mass = (-wreckDecel / 20) + 2 + wreckMassRand;
		this.GetComponent<Rigidbody>().angularDrag += 0.001f;
		
		//Prevent landing in the crowd
		if(pos.x > 2f){
			this.gameObject.transform.position = new Vector3(2f,pos.y,pos.z);
		}
		
		//Align particle system to global track direction
		this.transform.Find("SparksL").rotation = Quaternion.Euler(0,180,0);
		this.transform.Find("SparksR").rotation = Quaternion.Euler(0,180,0);
		this.transform.Find("SparksL").GetComponent<ParticleSystem>().startSpeed = 50 + (wreckDecel / 4);
		this.transform.Find("SparksR").GetComponent<ParticleSystem>().startSpeed = 50 + (wreckDecel / 4);
		
		//Flatten the smoke
		Transform tireSmoke = this.transform.Find("TireSmoke");
		tireSmoke.rotation = Quaternion.Euler(0,180,0);
		float smokeMultiplier = Mathf.Sin(wreckAngle);
		if(smokeMultiplier < 0){
			smokeMultiplier = -smokeMultiplier;
		}
		smokeMultiplier = (smokeMultiplier * 60) + 5;
		smokeMultiplier = Mathf.Round(smokeMultiplier);
		tireSmoke.GetComponent<ParticleSystem>().startColor = new Color32(200,200,200,(byte)smokeMultiplier);
		tireSmoke.GetComponent<ParticleSystem>().startSpeed = 40 + (wreckDecel / 5);
		tireSmoke.GetComponent<ParticleSystem>().startSize = 12 + (wreckDecel / 30); //Max 12, Min 4.5
		tireSmoke.GetComponent<ParticleSystem>().maxParticles = (int)(70 + Mathf.Round(wreckDecel / 2)); //Max 70, Hits 0 at -140 decel
	}
	
	void updateWindForce(){
		if(windForce < targetForce - forceSmoothing){
			windForce += forceSmoothing;
		}
		if(windForce > targetForce + forceSmoothing){
			windForce -= forceSmoothing;
		}
	}
	
	void drawRaycasts(){

	}
}
