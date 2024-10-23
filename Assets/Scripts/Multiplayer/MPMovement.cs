using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Collections.Generic;

#if NEW_INPUT_SYSTEM_INSTALLED
using UnityEngine.InputSystem;
#endif

using Random=UnityEngine.Random;

public class MPMovement : NetworkBehaviour
{
	
	GameObject vehicle;
	float playerZ;
	float playerSpeed;
	float affectedPlayerSpeed;
	float gettableSpeed;
	float topSpeed;
	float variTopSpeed;
	float speedRand;
	float randTopend;
	float overspeed;
	public float speed;
	float draftDist;
	
	float engineTemp;
	float tempLimit;
	bool blownEngine;

	int sparksCooldown;

	float challengeSpeedBoost;
	float dominatorDrag;
	float maxDraftDistance;

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

	int lane = 2;
	int laneInv;
	int laneticker;
	int laneBias;
	int laneChangeDuration;
	int laneChangeBackout;
	int dooredStrength;
	float laneChangeSpeed;
	float laneFactor;

	bool onTurn;
	bool brakesOn;
	bool steering;

	//These can adjust per race series (e.g. Indy)
	int seriesSpeedDiff;
	float draftStrengthRatio;
	float dragDecelMulti;
	float backdraftMulti;
	float bumpDraftDistTrigger;
	float draftAirCushion;
	float passDistMulti;
	float revLimiterBoost;

	static bool isWrecking;
	static bool wreckOver;
	float baseDecel;
	float slideX;
	float playerWreckDecel;
	float sparksEndSpeed;
	float maxSparksRand;
	float wreckAngle;
	float wreckTorque;
	int wreckHits;
	static int totalWreckers;
	float wreckDamage;
	
	ConstantForce wreckForce;
	Rigidbody wreckRigidbody;
	Transform leftSparks;
	Transform rightSparks;
	ParticleSystem leftSparksParticles;
	ParticleSystem rightSparksParticles;
	ParticleSystemRenderer leftSparksParticleRenderer;
	ParticleSystemRenderer rightSparksParticleRenderer;
	Transform engineSmoke;
	ParticleSystem engineSmokeParticles;
	Transform tireSmoke;
	ParticleSystem tireSmokeParticles;
	
	int wreckFreq;
		
	float targetForce;
	float windForce;
	float forceSmoothing;
	
	AudioSource carEngine;
	float engineRevs;
	int revbarOffset;
	
	static int speedOffset;
	float carSpeedOffset;
	float draftFactor;
	
	static int currentGear;
	int[] gearSpeeds = {999, 190, 110, 50, 25};
	
	Texture2D modTex;
	
	public string carName;
	int carNum;
	int carClass;
	string carTeam;
	string carManu;
	int carRarity;
	int carNumber;
	
	bool backingOut = false;
	bool tandemDraft = false;
	int tandemPosition;
	bool initialContact = false;

	int wobbleCount;
	int wobblePos;
	int wobbleTarget;
	int wobbleRand;
	
	static bool pacing;

	bool fastestLapSaved;
	
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
		vehicle = this.gameObject;
        playerSpeed = 203;
		playerZ = 0;
		gettableSpeed = playerSpeed;
		speedRand = Random.Range(0,50);
		speedRand = speedRand / 100;
		topSpeed = 208f + speedRand;
		randTopend = Random.Range(0,99);
		randTopend = randTopend / 1000;
		
		//Low = 210, blows up at = ~260?
		engineTemp = 210f;
		tempLimit = 259 + Random.Range(0,4);
		
		blownEngine = false;
		
		sparksCooldown = 0;
		
		laneticker = 0;
		onTurn = false;
		lane = 2;
		laneBias = 0;
		challengeSpeedBoost = 0;
		tandemPosition = 1;
		
		speedOffset = PlayerPrefs.GetInt("SpeedOffset");
		carSpeedOffset = 80;
		
		Time.timeScale = 1.0f;
		//safePause = false;
		
		isWrecking = false;
		wreckOver = false;
		playerWreckDecel = 0;
		totalWreckers = 0;
		wreckDamage = 0;
		
		wreckFreq = PlayerPrefs.GetInt("WreckFreq");
		
		wreckForce = this.GetComponent<ConstantForce>();
		wreckRigidbody = this.GetComponent<Rigidbody>();
		leftSparks = this.transform.Find("SparksL");
		rightSparks = this.transform.Find("SparksR");
		leftSparksParticles = leftSparks.GetComponent<ParticleSystem>();
		rightSparksParticles = rightSparks.GetComponent<ParticleSystem>();
		leftSparksParticleRenderer = leftSparks.GetComponent<ParticleSystemRenderer>();
		rightSparksParticleRenderer = rightSparks.GetComponent<ParticleSystemRenderer>();
		engineSmoke = this.transform.Find("EngineSmoke");
		engineSmokeParticles = engineSmoke.GetComponent<ParticleSystem>();
		tireSmoke = this.transform.Find("TireSmoke");
		tireSmokeParticles = tireSmoke.GetComponent<ParticleSystem>();
		
		pacing = false;
		
		seriesPrefix = PlayerPrefs.GetString("carSeries");

		if(DriverNames.isOfficialSeries(seriesPrefix) == true){
			officialSeries = true;
		} else {
			officialSeries = false;
		}

		if(officialSeries == true){
			setCarPhysics(seriesPrefix);
		} else {
			setCarPhysics(ModData.getPhysicsModel(seriesPrefix));
		}
		
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
		GameObject numberObj = this.transform.Find("Number").transform.gameObject;
		Renderer numRend = this.transform.Find("Number").GetComponent<Renderer>();
		
		if(PlayerPrefs.HasKey(seriesPrefix + carNum + "AltPaint")){
			if(officialSeries == true){
				liveryRend.material.mainTexture = Resources.Load(seriesPrefix + "livery" + carNum + "alt" + PlayerPrefs.GetInt(seriesPrefix + carNum + "AltPaint")) as Texture;
			} else {
				modTex = ModData.getTexture(seriesPrefix,carNum);
				liveryRend.material.mainTexture = ModData.getTexture(seriesPrefix,carNum,false,"alt" + PlayerPrefs.GetInt(seriesPrefix + carNum + "AltPaint"));
			}
		} else {
			if(officialSeries == true){
				liveryRend.material.mainTexture = Resources.Load(PlayerPrefs.GetString("carTexture")) as Texture;
			} else {
				modTex = ModData.getTexture(seriesPrefix,carNum);
				liveryRend.material.mainTexture = ModData.getTexture(seriesPrefix,carNum);
			}
		}
		
		if(PlayerPrefs.HasKey("CustomNumber" + seriesPrefix + carNum)){
			Vector3 numXPos;
			Vector3 numScale;
			Vector3 numRotation;
			if(PlayerPrefs.HasKey(seriesPrefix + carNum + "AltPaint")){
				if(officialSeries == true){
					if(Resources.Load(seriesPrefix + "livery" + carNum + "blankalt" + PlayerPrefs.GetInt(seriesPrefix + carNum + "AltPaint")) != null){
						liveryRend.material.mainTexture = Resources.Load(seriesPrefix + "livery" + carNum + "blankalt" + PlayerPrefs.GetInt(seriesPrefix + carNum + "AltPaint")) as Texture;
					} else {
						liveryRend.material.mainTexture = Resources.Load(seriesPrefix + "livery" + carNum + "alt" + PlayerPrefs.GetInt(seriesPrefix + carNum + "AltPaint")) as Texture;
					}
				}
			} else {
				if(officialSeries == true){
					if(Resources.Load(seriesPrefix + "livery" + carNum + "blank") != null){
						liveryRend.material.mainTexture = Resources.Load(seriesPrefix + "livery" + carNum + "blank") as Texture;
					} else {
						liveryRend.material.mainTexture = Resources.Load(seriesPrefix + "livery" + carNum) as Texture;
					}
				}
			}
			if(officialSeries == true){
				numXPos = new Vector3(0,0.52f,0-DriverNames.getNumXPos(seriesPrefix)/55.75f);
				numScale = new Vector3(DriverNames.getNumScale(seriesPrefix) * 0.5f,0,DriverNames.getNumScale(seriesPrefix) * 0.5f);
				numRotation = new Vector3(0,-90f-DriverNames.getNumRotation(seriesPrefix),0);
			} else {
				numXPos = new Vector3(0,0.52f,0-ModData.getNumberPosition(seriesPrefix)/55.75f);
				numScale = new Vector3(ModData.getNumberScale(seriesPrefix) * 0.5f,0,ModData.getNumberScale(seriesPrefix) * 0.5f);
				numRotation = new Vector3(0,-90f-ModData.getNumberRotation(seriesPrefix),0);
			}
			customNum = PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + carNum);
			numRend.material.mainTexture = Resources.Load("cup20num" + customNum) as Texture;
			numberObj.GetComponent<Transform>().localPosition = numXPos;
			numberObj.GetComponent<Transform>().localScale = numScale;
			numberObj.GetComponent<Transform>().localRotation = Quaternion.Euler(numRotation.x, numRotation.y, numRotation.z);
		
			//Debug.Log("Player #" + customNum + " applied Var: " + seriesPrefix + "num" + customNum);
			numRend.enabled = true;
		} else {
			numRend.enabled = false;
		}
				
		circuitLanes = PlayerPrefs.GetInt("CircuitLanes");
		
		apronLineX = -2.7f;
		apronLineX = 1.2f - ((circuitLanes - 1) * 1.2f) - 0.3f;

		brakesOn = false;
		steering = false;

		if(officialSeries == true){
			if(DriverNames.getType(seriesPrefix,carNum) == "Strategist"){
				switch(seriesPrefix){
					case "irl23":
					case "irl24":
						carClass+=4;
						break;
					default:
						break;
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
					case 11:
						laneChangeDuration = 16;
						laneChangeSpeed = 0.075f;
						laneChangeBackout = 6;
						break;
					case 12:
					case 13:
					case 14:
					case 15:
					case 16:
						laneChangeDuration = 12;
						laneChangeSpeed = 0.1f;
						laneChangeBackout = 5;
						break;
					default:
						laneChangeDuration = 80;
						laneChangeSpeed = 0.015f;
						laneChangeBackout = 32;
						break;
				}	
			} else {
				switch(seriesPrefix){
					case "irl23":
					case "irl24":
						laneChangeDuration = 48;
						laneChangeSpeed = 0.025f;
						laneChangeBackout = 16;
						break;
					default:
						laneChangeDuration = 80;
						laneChangeSpeed = 0.015f;
						laneChangeBackout = 32;
						break;
				}
			}
		} else {
			laneChangeDuration = 80;
			laneChangeSpeed = 0.015f;
			laneChangeBackout = 32;
		}

		dooredStrength = 25;
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
				//e.g. Class 6(S) reduces drag by 0.0002f - 0.001f
				dominatorDrag = carClass / 25000f;
			}
		}

		maxDraftDistance = 9 + carRarity;
		if(officialSeries == true){
			if (DriverNames.getType(seriesPrefix,carNum) == "Closer"){
				//e.g. 4* Class 6(S) increases drafting range to 19
				maxDraftDistance = 9 + carRarity + carClass;		
			}
		}
		
		wobbleCount = 1;
		wobblePos = 0;
		wobbleTarget = 0;
		wobbleRand = Random.Range(100,500);
    }

	void setCarPhysics(string seriesPrefix){
		switch(seriesPrefix){
			case "irl23":
			case "irl24":
			case "indy":
			case "indycar":
			case "openwheel":
				seriesSpeedDiff = 30;
				draftStrengthRatio = 400f;
				dragDecelMulti = 0.0030f;
				backdraftMulti = 0.0015f;
				bumpDraftDistTrigger = 1.1f;
				draftAirCushion = 2f;
				revLimiterBoost = 1f;
				break;
			case "cushion":
				seriesSpeedDiff = 0;
				draftStrengthRatio = 1200f;
				dragDecelMulti = 0.0035f;
				backdraftMulti = 0.004f;
				bumpDraftDistTrigger = 1.05f;
				draftAirCushion = 1.2f;
				revLimiterBoost = 0f;
				break;
			case "v3weaker":
				seriesSpeedDiff = 0;
				draftStrengthRatio = 900f;
				dragDecelMulti = 0.0035f;
				backdraftMulti = 0.005f;
				bumpDraftDistTrigger = 1.05f;
				draftAirCushion = 1.1f;
				revLimiterBoost = 0f;
				break;
			case "v4cup":
				seriesSpeedDiff = 0;
				draftStrengthRatio = 1050f;
				dragDecelMulti = 0.003f;
				backdraftMulti = 0.005f;
				bumpDraftDistTrigger = 1.05f;
				draftAirCushion = 1.2f;
				revLimiterBoost = 0f;
				break;
			//v5
			default:
				seriesSpeedDiff = 0;
				draftStrengthRatio = 1000f;
				dragDecelMulti = 0.0025f;
				backdraftMulti = 0.0045f;
				bumpDraftDistTrigger = 1.05f;
				draftAirCushion = 1.2f;
				revLimiterBoost = 0f;
				break;
		}
	}
	
	// Update is called once per frame
	void Update () {
		
		// IsOwner will also work in a distributed-authoritative scenario as the owner 
        // has the Authority to update the object.
        if (!IsOwner || !IsSpawned) return;
		
		checkForInputs();
		updateMovement();
		
		//Speed returned from the car you just bumped
		float givenSpeed = RaceControl.givenSpeed[carNum];
		if(givenSpeed != 0){
			if(brakesOn == false){
				playerSpeed = givenSpeed;
				tandemPosition = RaceControl.tandemPosition[carNum];
				RaceControl.givenSpeed[carNum] = 0;
				#if UNITY_EDITOR
					//Debug.Log("The player has evened out at " + givenSpeed + " in the tandem.");
					//Debug.Log("Player is in a tandem of " + tandemPosition);
				#endif
			} else {
				//Helps to remove the 'stickiness' when lifting to detach from the tandem
				if(playerSpeed > (givenSpeed-0.25f)){
					playerSpeed = givenSpeed-0.25f;
				}
				RaceControl.givenSpeed[carNum] = 0;
			}
		} else {
			tandemPosition = 1;
			RaceControl.tandemPosition[carNum] = 1;
		}
		RaceControl.carSpeed[carNum] = playerSpeed;
		
		float bumpSpeed = RaceControl.receivedSpeed[carNum];
		if(bumpSpeed != 0){
			if(brakesOn == false){
				//You've been bumped!
				if(initialContact == false){
					float midSpeed = bumpSpeed - playerSpeed;
					if(((midSpeed > 2f)||(midSpeed < -2f))
						&&(blownEngine == true)){
						startWreck();
					}
					playerSpeed += midSpeed/4;
					initialContact = true;
				} else {
					//Speed up
					playerSpeed += backdraftMulti;
				}
			}
			//I have received some speed from being bumped from behind..
			//..so now I reset it to 0
			RaceControl.receivedSpeed[carNum] = 0;
			//Who bumped me?
			int pusherNum = RaceControl.carTandemNum[carNum];
			//Update their tandemPosition to 2+
			RaceControl.tandemPosition[pusherNum] = tandemPosition + 1;
			//Send them the speed of my car, so they then match it
			RaceControl.givenSpeed[pusherNum] = playerSpeed;
		}
		
		if(pacing == true){
			playerSpeed = 202;
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
				updateWindForce(1);
				this.GetComponent<ConstantForce>().force = new Vector3(0f,0f,windForce);
			}
			return;
		}
		//Immediately stop calculating
		if(RaceHUD.raceOver == true){
			return;
		}
			
		laneInv = 4 - lane;
		
		if(sparksCooldown > 0){
			sparksCooldown--;
			if(sparksCooldown == 1){
				sparksCooldown--;
				leftSparksParticles.Stop();
				rightSparksParticles.Stop();	
			}
		}
		
		carSpeedOffset = CameraRotate.carSpeedOffset;
		
		//Draft increases with track speed
		draftFactor = (200 - carSpeedOffset)/200;
		//Debug.Log("Draft Factor: " + draftFactor);
		
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
		if (Physics.Raycast(transform.position,transform.forward, out DraftCheck, maxDraftDistance)){
			draftDist = DraftCheck.distance;
			//Speed up
			if(playerSpeed < variTopSpeed){
				
				float minDraftStrength = (0.04f - (maxDraftDistance / 750f)) + (carRarity / 750f) + (carClass / 2500f);
				
				float oldDraftStrength = ((maxDraftDistance - DraftCheck.distance)/draftStrengthRatio) + (carRarity / 750f) + (carClass / 2500f) - dragDecelMulti;
				
				//e.g. dist 1 = strength 0.028
				//e.g. dist 2 = strength 0.026
				//e.g. dist 5 = strength 0.02
				float draftStrength = (0.04f - (DraftCheck.distance / 750f)) + (carRarity / 750f) + (carClass / 2500f) - minDraftStrength - dragDecelMulti;
				#if UNITY_EDITOR
				//Debug.Log("Player draft strength: " + oldDraftStrength + " - " + (maxDraftDistance - DraftCheck.distance) + " " + draftStrengthRatio + " " + (carRarity / 750f) + (carClass / 2500f));
				//Debug.Log("New player draft strength: " + draftStrength + " - " + (0.02f / DraftCheck.distance) + " " + (carRarity / 750f) + (carClass / 2500f));
				#endif
				
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
				playerSpeed += (draftStrength * draftFactor);
				
			} else {
				playerSpeed-=((playerSpeed - topSpeed)/200) * draftFactor;
				
				//Over the top speed and still in the draft
				overspeed += 0.0001f;
				playerSpeed = variTopSpeed + overspeed;
			}
			//e.g. bump draft (1.0) = Max 265f
			//e.g. close draft (2.0) = Max 255f
			//e.g. distant draft (5.0) = Max 225f
			if(engineTemp < (275f - (DraftCheck.distance * 10f))){
				//e.g. bump draft (1.0) = +0.02
				//e.g. closer draft (1.5) = +0.0066
				//e.g. breathing draft (2.0) = +0.004
				//e.g. distant draft (5.0) = +0.0012
				engineTemp+= (0.005f / (DraftCheck.distance - 0.75f));
			} else {
				//e.g. 260 temp & >1.5m away = -0.5
				//e.g. 250 temp & >2.5m away = -0.04
				//e.g. 225 temp & >5m away = -0.015
				engineTemp-= (engineTemp - 210f) / 1000f;
			}
			if(engineTemp >= tempLimit){
				blownEngine = true;
				engineSmokeParticles.Play();
				RaceControl.hasBlownEngine[carNum] = true;
			}
		} else {
			//Slow down if not in any draft
			if(playerSpeed > 200){
				float diffToMax = variTopSpeed - playerSpeed;
				if((diffToMax) < 2){
					if(diffToMax < 0){
						diffToMax = 0;
					}
					playerSpeed-=(dragDecelMulti * (2 - (diffToMax / 2))) * draftFactor;
				} else {
					playerSpeed-=(dragDecelMulti + dominatorDrag) * draftFactor;
				}
			}
			if(engineTemp > 210f){
				//e.g. 260 temp = -0.5
				//e.g. 250 temp = -0.04
				//e.g. 225 temp = -0.015
				engineTemp-= (engineTemp - 210f) / 1000f;
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
			
			//DraftCheckBackward.transform.gameObject.SendMessage("GivePush",playerSpeed);
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
					playerSpeed+=(backdraftMulti + customAccelF);
				}
				if(DraftCheck.transform.gameObject.tag == "AICar"){
					//Give the car you are pushing your speed value..
					//They will then acknowledge it, and return their speed 
					//back to you on the next frame
					int opponentNum = int.Parse(DraftCheck.transform.gameObject.name.Substring(5));
					RaceControl.carTandemNum[opponentNum] = carNum;
					RaceControl.receivedSpeed[opponentNum] = playerSpeed;
				}
				if(blownEngine == true){
					tandemDraft = false;
					tandemPosition = 1;
				}
			}
		} else {
			tandemDraft = false;
			tandemPosition = 1;
		}
		
		if((brakesOn == true)||(blownEngine == true)){
			brakesOn = true;
			if(playerSpeed > 195){
				playerSpeed-=0.05f;
			} else {
				if(blownEngine == true){
					startWreck();
					brakesOn = false;
				}
			}
		}
		
		if(steering == true){
			if(laneticker > 0){
				laneticker = 2;
			}
			if(laneticker < 0){
				laneticker = -2;
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
		//updateMovement();

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
		speed = (playerSpeed + playerWreckDecel) - ControlCarMovement.controlSpeed;
		speed = speed / 10000;
		playerZ = vehicle.transform.position.z;
		transform.position += new Vector3(0, 0, speed);
	}

	void checkForInputs() {
		//Debug.Log("Checking for inputs..");
		#if ENABLE_INPUT_SYSTEM && NEW_INPUT_SYSTEM_INSTALLED
		// New input system backends are enabled.
		if (Keyboard.current.aKey.isPressed)
		{
			changeLaneLeft();
		}
		else if (Keyboard.current.dKey.isPressed)
		{
			changeLaneRight();
		}
		#else
		// Old input backends are enabled.
		if (Input.GetKey(KeyCode.A))
		{
			//Debug.Log("Going left!");
			changeLaneLeft();
		}
		else if(Input.GetKey(KeyCode.D))
		{
			//Debug.Log("Going right!");
			changeLaneRight();
		}
		#endif
	}

	void updateMovement() {
		
		//Debug.Log("Update Movement..");
		
		float multiplier = 1 * Time.deltaTime;
		
		//How fast can you switch lanes
        if (laneticker > 0){
			transform.position += new Vector3(-laneChangeSpeed, 0, 0);
			//vehicle.transform.Translate(-laneChangeSpeed, 0, 0);
			laneticker--;
			Debug.Log("Position updated. Ticker: " + laneticker);
        }

        if (laneticker < 0){
			transform.position += new Vector3(laneChangeSpeed, 0, 0);
			//vehicle.transform.Translate(laneChangeSpeed, 0, 0);
			laneticker++;
			Debug.Log("Position updated. Ticker: " + laneticker);
        }

        if (laneticker == 0){
			steering = false;
			//movingLane = false;
            backingOut = false;
        }
		
		/*if(vehicle.transform.position.x <= apronLineX){
			//Debug.Log("Track Limits!");
			if (backingOut == false) {
				backingOut = true;
			}
			laneticker = -laneChangeDuration + laneticker;
			lane--;
		}*/
		
		/*if(vehicle.transform.position.x >= 1.35f){
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
		}*/
	}
	
	public void changeLaneLeft(){
		steering = true;
		if(laneticker == 0){
			lane++;
			laneticker = laneChangeDuration;
		}
		Debug.Log("Go Left");
	}
	
	public void changeLaneRight(){
		steering = true;
		if(laneticker == 0){
			lane--;
			laneticker = -laneChangeDuration;
		}
		Debug.Log("Go Right ");
	}
	
	public void holdBrake(){
		brakesOn = true;
	}
	
	public void releaseBrake(){
		brakesOn = false;
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
			
			//Blown engine, always wreck
			if(((isWrecking == false)&&(wreckOver == false))&&
			  (blownEngine == true)){
				startWreck();
			}
			if((isWrecking == false)&&(wreckOver == false)){
				if((carHit.gameObject.tag == "Barrier") || 
				  (carHit.gameObject.name == "OuterWall") ||
				  (carHit.gameObject.name == "SaferBarrier")){
					startWreck();
					//GameObject.Find("Main Camera").GetComponent<AudioManager>().playSfx("Scrape");
				}
			}
			   
			//Join wreck
			if(carHit.gameObject.tag == "AICar"){
				bool joinWreck = carHit.gameObject.GetComponent<AIMovement>().isWrecking;
				if(joinWreck == true){
					if(isWrecking == false){
						if(wreckOver == false){
							startWreck();
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
		
		if(carHit.gameObject.name == "OuterWall"){
			rightSparksParticles.Play();
			sparksCooldown = Random.Range(5,20);
		}
		
		if ((carHit.gameObject.tag == "AICar") ||
			(carHit.gameObject.tag == "Barrier")){
				
			if(checkRaycast("LeftCorners", 0.51f) == true){
				leftSparksParticles.Play();
				sparksCooldown = Random.Range(5,20);
			}
			if(checkRaycast("RightCorners", 0.51f) == true){
				rightSparksParticles.Play();
				sparksCooldown = Random.Range(5,20);
			}
		}
		
		playerSpeed-=0.2f;
	}
	
	void OnCollisionStay(Collision carHit) {
		if((blownEngine == true)&&(isWrecking == false)){
			startWreck();
		}
	}
	
	void OnCollisionExit (Collision carHit){
		
		if((blownEngine == true)&&(isWrecking == false)){
			startWreck();
		}
		
		if(isWrecking == true){
			playerWreckDecel+= Random.Range(2f,5f);
		}
	}
	
	void startWreck(){
		if(pacing == true){
			return;
		}
		
		tireSmokeParticles.Play();
		leftSparksParticles.Play();
		rightSparksParticles.Play();
		
		if(wreckOver == false){
			isWrecking = true;
			RaceControl.isWrecking[carNum] = true;
			totalWreckers++;
			wreckHits++;
			wreckDamage+=1;
		}
		if(CameraRotate.cautionOut == false){
			CameraRotate.throwCaution();
		}
		
		//Make the car light, more affected by physics
		wreckRigidbody.mass = 2;
		
		//Remove constraints, allowing it to impact/spin using physics
		wreckRigidbody.constraints &= ~RigidbodyConstraints.FreezeRotationY;
		wreckRigidbody.constraints &= ~RigidbodyConstraints.FreezePositionX;
		
		//Remove forces, physics only
		wreckRigidbody.isKinematic = false;
		wreckRigidbody.useGravity = false;
		
		//Apply wind/drag
		baseDecel = -0.35f;
		slideX = 0;
		playerWreckDecel = 0;
		sparksEndSpeed = Random.Range(-130,-180);
		maxSparksRand = Random.Range(5,30);
		targetForce = 0;
		forceSmoothing = 0.5f;
		windForce = targetForce;
		wreckForce.force = new Vector3(0f, 0f,targetForce);
		float wreckTorque;
		if(wreckHits > 1){
			//Subsequent hits based on rotation angle
			float spinAngle = this.transform.localRotation.eulerAngles.y;
			//Debug.Log("Car rotation: " + spinAngle);
			wreckTorque = Random.Range(-0.25f, 0.25f) * 10;
		} else {
			//First impact, car will start straight
			wreckTorque = Random.Range(-0.35f, 0.35f) * 10;
		}
		wreckForce.torque = new Vector3(0f, wreckTorque, 0f);
		
		if(fastestLapSaved == false){
			CameraRotate.saveRaceFastestLap();
			fastestLapSaved = true;
		}
	}
	
	public void endWreck(){
		playerSpeed = 0;
		baseDecel = 0;
		slideX = 0;
		playerWreckDecel = 0;
		targetForce = 0;
		updateWindForce(0);
		isWrecking = false;
		wreckOver = true;
		RaceControl.isWrecking[carNum] = false;
		RaceControl.hasWrecked[carNum] = true;
		
		//Debug.Log("WRECK OVER");
		wreckRigidbody.mass = 5;
		wreckRigidbody.isKinematic = true;
		wreckForce.force = new Vector3(0f, 0f,windForce);
		wreckForce.torque = new Vector3(0f, 0f, 0f);
		wreckRigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezePositionZ;
		
		if(blownEngine == true){
			Ticker.checkFinishPositions(false);
		} else {
			Ticker.saveCautionPositions(true);
		}
		
		//No cautions on the last lap, race is over
		if(CameraRotate.lap < CameraRotate.raceEnd){
			if(fastestLapSaved == false){
				//Debug.Log("Haven't saved fastest lap yet.. (Movement)");
				CameraRotate.saveRaceFastestLap();
				fastestLapSaved = true;
			}
		}
		//Debug.Log("Pause the game!");
		PlayerPrefs.SetInt("Volume",0);
	}
	
	void wreckPhysics(){
		
		wreckAngle = this.gameObject.transform.rotation.y;
		float wreckSine = Mathf.Sin(wreckAngle);
		if(wreckSine < 0){
			wreckSine = -wreckSine;
		}
		baseDecel-=0.42f;
		slideX = ((baseDecel + 1) / 4f) + 20f;
		//Formula: -200f = -10x, -140f = 0x, 0f = 10x
		//         -200f = -20x, -100f = -10x, 0f = 0x
		//         -200f = -6x, -140f = 0x, 0f = 14f
		//slideX = ((baseDecel + 1) / 10f) + 14f
		//Reduce division factor to increase effect
		
		targetForce = playerWreckDecel;
		updateWindForce(wreckSine);
		if(CameraRotate.onTurn == true){
			//baseDecel-=0.02f * CameraRotate.currentTurnSharpness();
			//Debug.Log("Extra decel: " + (0.02f * CameraRotate.currentTurnSharpness()));
			wreckForce.force = new Vector3(slideX,0f,windForce);
		} else {
			wreckForce.force = new Vector3(-3f, 0f,windForce);
			//Debug.Log("Windforce: " + windForce);
		}
		playerWreckDecel = baseDecel - (60f * wreckSine);
		//Debug.Log(playerWreckDecel);
		
		if(seriesSpeedDiff > 0){
			seriesSpeedDiff -= 1;
		}

		//Debug.Log("Wreck Decel: " + playerWreckDecel);
		if((playerSpeed - speedOffset - seriesSpeedDiff - CameraRotate.carSpeedOffset) + windForce <= 0){
			endWreck();
		}
		
		wreckRigidbody.mass = (-playerWreckDecel / 20) + 2;
		wreckRigidbody.angularDamping += 0.001f;
		
		//Prevent landing in the crowd
		if(this.gameObject.transform.position.x > 1.5f){
			this.gameObject.transform.position = new Vector3(1.5f,this.gameObject.transform.position.y,this.gameObject.transform.position.z);
		}
		
		//Debug.Log("Sparks End: " + sparksEndSpeed + " Wreck Decel: " + playerWreckDecel);
		if(sparksEndSpeed < playerWreckDecel){
			//Align particle system to global track direction
			leftSparks.rotation = Quaternion.Euler(0,180,0);
			rightSparks.rotation = Quaternion.Euler(0,180,0);
			leftSparksParticles.startSpeed = 100 + (playerWreckDecel / 2);
			rightSparksParticles.startSpeed = 100 + (playerWreckDecel / 2);
			leftSparksParticles.maxParticles = (int)Mathf.Floor(maxSparksRand + (playerWreckDecel / 12));
			rightSparksParticles.maxParticles = (int)Mathf.Floor(maxSparksRand + (playerWreckDecel / 12));
			leftSparksParticles.startLifetime = 0.2f + ((0-playerWreckDecel) / 50);
			rightSparksParticles.startLifetime = 0.2f + ((0-playerWreckDecel) / 50);
			leftSparksParticleRenderer.lengthScale = 0.5f + (playerWreckDecel / 200);
			rightSparksParticleRenderer.lengthScale = 0.5f + (playerWreckDecel / 200);
		} else {
			leftSparksParticles.Stop();
			rightSparksParticles.Stop();
		}
		
		//Flatten the smoke
		engineSmoke.rotation = Quaternion.Euler(0,180,0);
		tireSmoke.rotation = Quaternion.Euler(0,180,0);
		float smokeMultiplier = wreckSine;
		if(smokeMultiplier < 0){
			smokeMultiplier = -smokeMultiplier;
		}
		smokeMultiplier = (smokeMultiplier * 60) + 5;
		smokeMultiplier = Mathf.Round(smokeMultiplier);
		
		engineSmokeParticles.startColor = new Color32(135,135,155,(byte)smokeMultiplier);
		engineSmokeParticles.startSpeed = 40 + (playerWreckDecel / 5);
		
		tireSmokeParticles.startColor = new Color32(200,200,200,(byte)smokeMultiplier);
		tireSmokeParticles.startSpeed = 40 + (playerWreckDecel / 5);
		tireSmokeParticles.startSize = 12 + (playerWreckDecel / 30); //Max 12, Min 4.5
		tireSmokeParticles.maxParticles = (int)(70 + Mathf.Round(playerWreckDecel / 2)); //Max 70, Hits 0 at -140 decel
	}
	
	void updateWindForce(float angleSin){
		//Debug.Log("Wreck Angle Sin: " + (forceSmoothing * (angleSin * 2)));
		if(windForce < targetForce - (forceSmoothing * (angleSin * 2))){
			windForce += forceSmoothing * (angleSin * 2);
		}
		if(windForce > targetForce + (forceSmoothing * (angleSin * 2))){
			windForce -= forceSmoothing * (angleSin * 2);
		}
	}
	
	public static void pacingEnds(){
		pacing = false;
	}
	
	void wreckSpeed(){
		//Speed difference between the Player and the Control Car
		speed = (playerSpeed + playerWreckDecel) - ControlCarMovement.controlSpeed;
		speed = speed / 100;
		vehicle.transform.Translate(new Vector3(0, 0, speed),Space.World);
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
