using UnityEngine;
using System;
using System.Collections;
using System.Text.RegularExpressions;

using Random=UnityEngine.Random;

public class AIMovement : MonoBehaviour
{

    public GameObject AICar;
	public GameObject thePlayer;
	public Camera player2Cam;
    public float AISpeed;
    float speed;
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
	bool movingLane;
    bool backingOut;
    bool laneSettled;
    int laneSettling;
	int laneStagnant;
	int stagnantMax;
	
	int antiGlitch;
	
	int circuitLanes;
	float apronLineX;

	public string carName;
	public int carNum;
	public string carTeam;
	public string carManu;
	public int carRarity;
	public string carType;
	public int AICarClass;

    public static bool onTurn;
	public bool tandemDraft;

    string carNumber;
	string AICarNum;
	
	string currentSeries;
	string currentSubseries;
	
	string seriesPrefix;
	int customNum;

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

    public static bool crashActive;
    public static int crashTime;

    // Use this for initialization
    void Start(){
		
		holdLane = 0;
		laneRest = Random.Range(100, 1000);
		
        onTurn = false;
		tandemDraft = false;
        AISpeed = 200;
        laneticker = 0;
		
		currentSeries = PlayerPrefs.GetInt("CurrentSeries").ToString();
		currentSubseries = PlayerPrefs.GetInt("CurrentSubseries").ToString();
		
		AILevel = SeriesData.offlineAILevel[int.Parse(currentSeries.ToString()),int.Parse(currentSubseries.ToString())];
		
		Debug.Log("AILevel: " + AILevel);
		
		antiGlitch = 0;
		
		thePlayer = GameObject.Find("Player");
		seriesPrefix = "cup2020";
		
		string splitAfter = "AICar0";
		carNumber = this.name.Substring(this.name.IndexOf(splitAfter) + splitAfter.Length);
		
		//Debug.Log(carNumber);
		carNum = int.Parse(carNumber);
		
		carTeam = DriverNames.cup2020Teams[carNum];
		carManu = DriverNames.cup2020Manufacturer[carNum];
		carType = DriverNames.cup2020Types[carNum];
		
		AICarClass = PlayerPrefs.GetInt("SubseriesMinClass");
		
		seriesPrefix = "cup20";
		
		Renderer liveryRend = this.transform.Find("Plane").GetComponent<Renderer>();
		Renderer numRend = this.transform.Find("Number").GetComponent<Renderer>();
		
		if(PlayerPrefs.HasKey("CustomNumber" + seriesPrefix + carNumber)){
			liveryRend.material.mainTexture = Resources.Load(seriesPrefix + "livery" + carNumber + "blank") as Texture;
			customNum = PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + carNumber);
			numRend.material.mainTexture = Resources.Load("cup20num" + customNum) as Texture;
			//Debug.Log("Custom number #" + customNum + " applied to car " + carNum + "Var: " + seriesPrefix + "num" + customNum);
		} else {
			liveryRend.material.mainTexture = Resources.Load(seriesPrefix + "livery" + carNumber) as Texture;
			numRend.enabled = false;
			//Debug.Log("No custom number saved");
		}
		
		circuitLanes = PlayerPrefs.GetInt("CircuitLanes");
		
		apronLineX = -2.7f;
		apronLineX = 1.2f - ((circuitLanes - 1) * 1.2f) - 0.3f;
		
		//Debug.Log("Apron Line: " + apronLineX);

        speedRand = Random.Range(-150, 150);
        speedRand = speedRand / 100;
        accelRand = Random.Range(-30, 60);
        accelRand = accelRand / 5000;

        if (DriverNames.cup2020Types[carNum] == "Strategist"){
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

        movingLane = false;
        backingOut = false;

        crashTime = 50;

        wobbleCount = 1;
        wobblePos = 0;
        wobbleTarget = 0;
        wobbleRand = Random.Range(100, 500);
    }

    void OnCollisionEnter(Collision carHit) {
		
        if ((carHit.gameObject.tag == "AICar") || 
		    (carHit.gameObject.tag == "Player") || 
			(carHit.gameObject.tag == "Barrier") || 
			(carHit.gameObject.name == "OuterWall") ||
			(carHit.gameObject.name == "TrackLimit") ||
			(carHit.gameObject.name == "FixedKerb")) {
							
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
				if(doored("Left",25) == true){
					changeLane("Right");
				}
				if(doored("Right",25) == true){
					changeLane("Left");
				}
			}
			AISpeed -= 0.5f;
        }
    }
	
	void OnCollisionStay(Collision carHit) {
		if(laneticker != 0){
			antiGlitch++;
		}
    }
	
	void OnCollisionExit(Collision carHit) {
		antiGlitch = 0;
    }
	
	void ReceivePush(float bumpSpeed){
		
		if(tandemDraft == false){
			float midSpeed = bumpSpeed - AISpeed;
			//For some reason changing this makes the player bump-draft mega fast!
			AISpeed += midSpeed/4;
			tandemDraft = true;
			//Debug.Log("Impact levels out " + AICar.name);
		}
		//Send it back
		RaycastHit DraftCheckBackward;
		//bool HitBackward = Physics.Raycast(transform.position, transform.forward * -1, out DraftCheckBackward, 1.1f);
		bool HitBackward = Physics.Raycast(transform.position - new Vector3(0.48f,0,0), transform.forward * -1, out DraftCheckBackward, 1.01f);
		if(HitBackward == false){
			HitBackward = Physics.Raycast(transform.position - new Vector3(-0.48f,0,0), transform.forward * -1, out DraftCheckBackward, 1.01f);
		}
		
		if(HitBackward == true){
			DraftCheckBackward.transform.gameObject.SendMessage("GivePush",AISpeed);
		}
	}
	
	void GivePush(float bumpSpeed){
		AISpeed = bumpSpeed;
		//Debug.Log("" + AICar.name + " is being speed matched");
	}

    // Update is called once per frame
    void FixedUpdate(){

		//Debug.Log(this.name + " Check");

		lap = CameraRotate.lap;
		
        laneInv = 4 - lane;
		
		if(lap > 0){
			if(caution){
				if (AISpeed > 200.5f){
					AISpeed -= 0.02f;
				} else {
					AISpeed = 200;
				}
			} else {
				holdLane++;
				distFromPlayer = Scoreboard.checkSingleCarPosition(carName) - Scoreboard.checkPlayerPosition();
				if((distFromPlayer<=20)&&(distFromPlayer>=-20)){
					speedLogic();
				} else {
					//Improve FPS by removing logic from far away opponents
					if(distFromPlayer>20){
						dumbSpeed(1);
					} else {
						if(distFromPlayer<-20){
							dumbSpeed(-1);
						}
					}
				}
			}
			draftLogic();
			carWobble();
			updateMovement();
			this.gameObject.GetComponent<Rigidbody>().velocity = Vector3.zero;
			this.gameObject.GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
		} else {
			//Pacing speed
			AISpeed = 200;
		}
	}

	void speedLogic(){
		
		RaycastHit DraftCheckForward;
        RaycastHit DraftCheckBackward;
        bool HitForward = Physics.Raycast(transform.position, transform.forward, out DraftCheckForward, 100);
        bool HitBackward = Physics.Raycast(transform.position, transform.forward * -1, out DraftCheckBackward, 100);
		
		//If gaining draft of car in front
		if (HitForward && DraftCheckForward.distance <= 10){
			//Speed up
			if (AISpeed < (205 + (AILevel / 5))){
				//Draft gets stronger as you get closer
				AISpeed += ((10 - DraftCheckForward.distance)/ (1000 - (AILevel * 10)));
			}
		} else {
			//Slow down
			if (AISpeed > 200){
				//No draft, slow with drag
				AISpeed -= 0.003f + (AILevel / 10000);
			}
		}
		
		//If recieving backdraft from car behind
		if (HitBackward && DraftCheckBackward.distance <= 1.5f){
			//if (AISpeed > (206 + laneInv + (AILevel / 5))){
				AISpeed += (0.004f) + (AILevel / 2000);
			//}
		}
		
		// If being bump-drafted from behind
		if (HitBackward && DraftCheckBackward.distance <= 1.01f){
			AISpeed += 0.004f;
			tandemDraft = true;
			int currentPos = Scoreboard.checkSingleCarPosition("AICar0" + carNum + "");
			if(currentPos == 0){
				//Debug.Log("Leader is #" + carNum);
				evadeDraft();
			}
		} else {
			tandemDraft = false;
		}

		//If bump-drafting the car in front
		if (HitForward && DraftCheckForward.distance <= 1.01f){
			if(DraftCheckForward.transform.gameObject.name != null){
				DraftCheckForward.transform.gameObject.SendMessage("ReceivePush",AISpeed);
			}
		} else {
			tandemDraft = false;
		}
		
		//Speed tops out
        if (AISpeed > (204 + laneInv + (AILevel / 5))){
			//Reduce speed, proportionate to the amount 'over'
            AISpeed -= ((AISpeed - 204) / 100);
		}
		if (AISpeed > 210){
			//Hard limiter as a fallback
            AISpeed = 210;
		}
		
		//Minimum speed
		if (AISpeed < (200)){
			AISpeed = 200;
		}
		
		//Speed difference between the player and the AI
		speed = AISpeed - Movement.playerSpeed;
		speed = speed / 100;
		AICar.transform.Translate(0, 0, speed);
	}
	
	void dumbSpeed(int direction){
		AISpeed += ((0.001f + (AILevel / 5000)) * direction);
		
		//Speed difference between the player and the AI
		speed = AISpeed - Movement.playerSpeed;
		speed = speed / 100;
		AICar.transform.Translate(0, 0, speed);
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
		
		if(AICar.transform.position.x <= apronLineX){
			//Debug.Log("Track Limits!");
			if (backingOut == false) {
				backingOut = true;
			}
			laneticker = -laneChangeDuration + laneticker;
			lane--;
		}
		
		if(AICar.transform.position.x >= 1.35f){
			//Debug.Log("Wall!");
			if (backingOut == false) {
				backingOut = true;
			}
			laneticker = laneChangeDuration + laneticker;
			lane++;
			//Wall hit decel
			AISpeed -= 1f;
		}
	}

	void draftLogic(){
		RaycastHit DraftCheckForward;
        bool HitForward = Physics.Raycast(transform.position + new Vector3(0.0f, 0.0f, 1.2f), transform.forward, out DraftCheckForward, 5);
		//Debug.DrawRay(transform.position  + new Vector3(0.0f, 0.0f, 1.2f), Vector3.forward * 10, Color.green);
		
		if(HitForward == true){
			if(holdLane >= laneRest){
				carName = DraftCheckForward.collider.gameObject.name;
				float carDist = DraftCheckForward.distance;
				int opponentNum = 9999;
				string opponentTeam = "";
				if(carName != "Player") {
					opponentNum = getCarNumFromName(carName);
					if(opponentNum != 9999){
						opponentTeam = DriverNames.cup2020Teams[opponentNum];
					}
				}
				//If teammate
				if(opponentTeam == carTeam){
					//Stay with unless near front
				} else {
					//Pass them
					if(movingLane == false){
						tryPass(50, false);
					}
				}
			}
		} else {
			findDraft();
		}
	}
	
	void carWobble(){
		wobbleCount++;
		
		if(wobbleCount >= wobbleRand){
			wobbleRand = Random.Range(20,50);
			wobbleTarget = Random.Range(-100,100);
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
				changeLane("Right");
			} else {
				bool leftSideClr = leftSideClear();
				if(leftSideClr == true) {
					changeLane("Left");
				}
			}
		}
	}
	
	public void findDraft(){
		RaycastHit DraftCheckLaneLeft;
		RaycastHit DraftCheckLaneRight;
		bool HitLaneLeft = Physics.Raycast(transform.position + new Vector3(-1.2f,0,1.1f), transform.forward, out DraftCheckLaneLeft, 5);
		bool HitLaneRight = Physics.Raycast(transform.position + new Vector3(1.2f,0,1.1f), transform.forward, out DraftCheckLaneRight, 5);
		string direction = "";
		
		if (HitLaneLeft){
			if(leftSideClear()){
				if(DraftCheckLaneLeft.distance >= 2.5f){
					//Go for it, regardless of closing speed
					direction = "Left";
				} else {
					//Only seek a close draft if faster than you
					float opponentSpeed = getOpponentSpeed(DraftCheckLaneLeft);
					if(opponentSpeed > (AISpeed + 0.1f)){
						direction = "Left";
						//Debug.Log("Opponent Speed " + opponentSpeed + " ahead of AISpeed " + AISpeed);
					}
				}
			}
		}
		
		if (HitLaneRight){
			if(rightSideClear()){
				if(DraftCheckLaneRight.distance >= 2.5f){
					if(direction == "Left"){
						direction = "Both";
					} else {
						direction = "Right";
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
	
	public bool leftSideClear(){
		
        RaycastHit DraftCheckLeft;
        RaycastHit DraftCheckLeftForward;
        RaycastHit DraftCheckLeftBackward;
        bool HitLeft = Physics.Raycast(transform.position, transform.right * -1, out DraftCheckLeft, 100);
        bool HitLeftForward = Physics.Raycast(transform.position, transform.forward - transform.right, out DraftCheckLeftForward, 100);
        bool HitLeftBackward = Physics.Raycast(transform.position, (transform.forward * -1) - transform.right, out DraftCheckLeftBackward, 100);
		
		//Check Left Corners Clear
		if (!(HitLeftForward && DraftCheckLeftForward.distance <= 1.5f)){
			if (!(HitLeftBackward && DraftCheckLeftBackward.distance <= 1.5f)){
				if (!(HitLeft && DraftCheckLeft.distance <= 1)){
					return true;
				} else {
					return false;
				}
			} else {
				return false;
			}
		} else {
			return false;
		}
	}
	
	public bool rightSideClear(){
		
        RaycastHit DraftCheckRight;
        RaycastHit DraftCheckRightForward;
        RaycastHit DraftCheckRightBackward;
        bool HitRight = Physics.Raycast(transform.position, transform.right, out DraftCheckRight, 100);
        bool HitRightForward = Physics.Raycast(transform.position, transform.forward + transform.right, out DraftCheckRightForward, 100);
        bool HitRightBackward = Physics.Raycast(transform.position, (transform.forward * -1) + transform.right, out DraftCheckRightBackward, 100);
		
		//Check Right Corners Clear
		if (!(HitRightForward && DraftCheckRightForward.distance <= 1.5f)){
			if (!(HitRightBackward && DraftCheckRightBackward.distance <= 1.5f)){
				//Check Right Side Clear
				if (!(HitRight && DraftCheckRight.distance <= 1)){
					return true;
				} else {
					return false;
				}
			} else {
				return false;
			}
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
			bool dooredLeftF = Physics.Raycast(transform.position + new Vector3(-0.52f,0,-1f), transform.forward, out collisionLeftF, 2);
			bool dooredLeftB = Physics.Raycast(transform.position + new Vector3(-0.52f,0,1f), transform.forward * -1, out collisionLeftB, 2);
			
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
				bool dooredRightF = Physics.Raycast(transform.position + new Vector3(0.52f,0,-1f), transform.forward, out collisionRightF, 2);
				bool dooredRightB = Physics.Raycast(transform.position + new Vector3(0.52f,0,1f), transform.forward * -1, out collisionRightB, 2);
				
				if (((dooredRightF) && (collisionRightF.distance <= 2)) ||
				   ((dooredRightB) && (collisionRightB.distance <= 2))) {
					//Debug.Log("Doored!");
					return true;
				} else {
					return false;
				}
			} else {
				Debug.Log("Invalid doored value");
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
						Debug.Log("Not sure where to move");
					} else {
						Debug.Log("Invalid lane change");
					}
				}
			}
			laneRest = Random.Range(100, 1000);
			movingLane = true;
			holdLane = 0;
		}
	}
	
	public void setLaneRest(){
		if(lap >= 3){
			laneRest = Random.Range(0, 1);
			Debug.Log("Get Lairy");
		}
	}
	
	int getCarNumFromName(string carName){
		string splitAfter = "AICar0";
		carNumber = carName.Substring(carName.IndexOf(splitAfter) + splitAfter.Length);
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
			case "FrontCorners":
				rayHit = Physics.Raycast(transform.position + new Vector3(0.48f,0,0), transform.forward, out DraftCheck, rayLength);
				if(rayHit == false){
					rayHit = Physics.Raycast(transform.position + new Vector3(-0.48f,0,0), transform.forward, out DraftCheck, rayLength);
				}
				break;
			case "RearCorners":
				rayHit = Physics.Raycast(transform.position - new Vector3(0.48f,0,0), transform.forward * -1, out DraftCheck, rayLength);
				if(rayHit == false){
					rayHit = Physics.Raycast(transform.position - new Vector3(-0.48f,0,0), transform.forward * -1, out DraftCheck, rayLength);
				}
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
					Debug.DrawRay(transform.position + new Vector3(0,0,-0.98f), transform.right * -0.52f, Color.yellow);
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
	
	void drawRaycasts(){
		//Frontward draft
        Debug.DrawRay(transform.position + new Vector3(0.0f, 0.0f, 1.2f), Vector3.forward * 10, Color.green);
        //Backdraft
        Debug.DrawRay(transform.position, Vector3.back * 2, Color.red);
        //Leftdraft
        Debug.DrawRay(transform.position, Vector3.left * 1, Color.yellow);
        //Rightdraft
        Debug.DrawRay(transform.position, Vector3.right * 1, Color.yellow);
        //FrontLeftdraft
        Debug.DrawRay(transform.position, (Vector3.left + Vector3.forward) * 1.5f, Color.magenta);
        //FrontRightdraft
        Debug.DrawRay(transform.position, (Vector3.right + Vector3.forward) * 1.5f, Color.magenta);
        //RearLeftdraft
        Debug.DrawRay(transform.position, (Vector3.left + Vector3.back) * 1.5f, Color.magenta);
        //RearRightdraft
        Debug.DrawRay(transform.position, (Vector3.right + Vector3.back) * 1.5f, Color.magenta);
	}
}
