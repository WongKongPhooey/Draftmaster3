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
    int laneticker;
    int laneChangeDuration;
    int laneChangeBackout;
    float laneChangeSpeed;
	bool changingLanes;
	int holdLane;
	int laneRest;
	bool movingLane;
	bool forcedMove;
    bool backingOut;
    bool laneSettled;
    int laneSettling;
	int laneStagnant;
	int stagnantMax;
	
	float bumpChanceFactor;
	
	int antiGlitch;
	
	int circuitLanes;
	float apronLineX;

	public string carName;
	public int carNum;
	public string carTeam;
	public string carManu;
	public string carType;

    public static bool onTurn;
	public bool tandemDraft;

    string carNumber;
	string AICarNum;
	
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
		
		antiGlitch = 0;
		
        //enginetemp = 210;
		thePlayer = GameObject.Find("Player");
		seriesPrefix = "cup2020";
		
		string splitAfter = "AICar0";
		carNumber = this.name.Substring(this.name.IndexOf(splitAfter) + splitAfter.Length);
		
		//Debug.Log(carNumber);
		carNum = int.Parse(carNumber);
		
		carTeam = DriverNames.cup2020Teams[carNum];
		carManu = DriverNames.cup2020Manufacturer[carNum];
		carType = DriverNames.cup2020Types[carNum];
		
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
		
		Debug.Log("Apron Line: " + apronLineX + "(" + circuitLanes + " lanes)");

        speedRand = Random.Range(-150, 150);
        speedRand = speedRand / 100;
        accelRand = Random.Range(-30, 60);
        accelRand = accelRand / 5000;

        if (DriverNames.cup2020Types[carNum] == "Strategist"){
            laneChangeDuration = 40;
            laneChangeSpeed = 0.030f;
            laneChangeBackout = 16;
        } else {
            laneChangeDuration = 80;
            laneChangeSpeed = 0.015f;
            laneChangeBackout = 32;
        }
		
		if (DriverNames.cup2020Types[carNum] == "Intimidator"){
			bumpChanceFactor = 10 + (2.5f * PlayerPrefs.GetInt(seriesPrefix + carNum + "Class"));
			//Debug.Log("Extra bump chance of " + bumpChanceFactor + " - #" + carNum);
		} else {
			bumpChanceFactor = 10;
		}

		forcedMove = false;
        movingLane = false;
        backingOut = false;

        crashTime = 50;

        wobbleCount = 1;
        wobblePos = 0;
        wobbleTarget = 0;
        wobbleRand = Random.Range(100, 500);
    }

    void OnCollisionEnter(Collision carHit) {
		
		if(forcedMove == true){
			return;
		}
		
		if(antiGlitch > 0){
			antiGlitch = 0;
		}
		
        if ((carHit.gameObject.tag == "AICar") || 
		    (carHit.gameObject.tag == "Player") || 
			(carHit.gameObject.tag == "Barrier") || 
			(carHit.gameObject.name == "OuterWall") ||
			(carHit.gameObject.name == "TrackLimit") ||
			(carHit.gameObject.name == "FixedKerb")) {
				
            //if ((laneticker != 0) && (backingOut == false) && (movingLane == true)){
			if ((laneticker != 0) && (movingLane == true) && (forcedMove == false)){
				if (laneticker > 0){
					//backingOut = true;
					laneticker = -laneChangeDuration + laneticker;
					lane--;
				}
				if (laneticker < 0){
					//backingOut = true;
					laneticker = laneChangeDuration + laneticker;
					lane++;
				}
            } else {
				if(doored("Left",25) == true){
					changeLane("Right");
				} else {
					if(doored("Right",25) == true){
						changeLane("Left");
					} else {
						RaycastHit glitchLeftF;
						RaycastHit glitchLeftB;
						bool overlapLeftF = Physics.Raycast(transform.position + new Vector3(0.49f,0,0), transform.forward + transform.right * -1, out glitchLeftF, 2);
						bool overlapLeftB = Physics.Raycast(transform.position + new Vector3(-0.49f,0,0), transform.forward + transform.right * -1, out glitchLeftB, 2);
					}
				}
			}
        }
        AISpeed -= 0.5f;
    }
	
	void OnCollisionStay(Collision carHit) {
		antiGlitch++;
		
		if(forcedMove == true){
			return;
		}
		
		//Fallback forced backout
		if (((carHit.gameObject.tag == "AICar") || 
			(carHit.gameObject.tag == "Player")) && 
			(antiGlitch > 2)){
				
			//Debug.Log("ANTI GLITCH - " + carHit.gameObject.name + ": " + antiGlitch);
				
			if (laneticker != 0){
				if (laneticker > 0){
					backingOut = true;
					laneticker = -laneChangeDuration + laneticker;
					lane--;
				}
				if (laneticker < 0){
					backingOut = true;
					laneticker = laneChangeDuration + laneticker;
					lane++;
				}
			} else {
				if(doored("Left",25) == true){
					changeLane("Right");
				} else {
					if(doored("Right",25) == true){
						changeLane("Left");
					} else {
						RaycastHit glitchLeftF;
						RaycastHit glitchLeftB;
						bool overlapLeftF = Physics.Raycast(transform.position + new Vector3(0.49f,0,0), transform.forward + transform.right * -1, out glitchLeftF, 2);
						bool overlapLeftB = Physics.Raycast(transform.position + new Vector3(-0.49f,0,0), transform.forward + transform.right * -1, out glitchLeftB, 2);
					}
				}
			}	
		}
	}
	
	void OnCollisionExit(Collision carHit) {
		antiGlitch = 0;
		Debug.Log("Left Collision with" + carHit.gameObject.name);
	}
	
	void ReceivePush(float bumpSpeed){
		
		if(tandemDraft == false){
			float midSpeed = bumpSpeed - AISpeed;
			int bumpChance = (int)Mathf.Round(midSpeed * bumpChanceFactor);
			if(bumpChance > 100){
				bumpChance = 100;
			}
			AISpeed += midSpeed/4;
			tandemDraft = true;
			
			//Hard bump -> Push to pass
			float randChance = Random.Range(0,100);
			if (randChance > bumpChance){
				changeLane("Right");
			}
			//Debug.Log("Impact levels out " + AICar.name);
		}
		//Send it back
		RaycastHit DraftCheckBackward;
		bool HitBackward = Physics.Raycast(transform.position, transform.forward * -1, out DraftCheckBackward, 1.1f);
		if(HitBackward ){
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
				if((distFromPlayer <= 10)&&(distFromPlayer >= -10)){
					speedLogic();
				} else {
					//Improve FPS by removing logic from far away opponents
					//If player is 10+ cars ahead
					if(distFromPlayer > 10){
						dumbSpeed("+");
					}
					//If player is 10+ cars behind
					if(distFromPlayer < -10){
						dumbSpeed("-");
					}
				}
			}
			draftLogic();
			updateMovement();
		} else {
			//Pacing speed
			AISpeed = 200;
		}
	}

	void speedLogic(){
		//Speed tops out
        if (AISpeed > (204 + laneInv)){
			//Reduce speed, proportionate to the amount 'over'
            AISpeed -= ((AISpeed - 204) / 100);
		}
		//Minimum speed
		if (AISpeed < (200)){
			AISpeed = 200;
        }
		
		RaycastHit DraftCheckForward;
        RaycastHit DraftCheckBackward;
        bool HitForward = Physics.Raycast(transform.position, transform.forward, out DraftCheckForward, 100);
        bool HitBackward = Physics.Raycast(transform.position, transform.forward * -1, out DraftCheckBackward, 100);
		
		//If gaining draft of car in front
		if (HitForward && DraftCheckForward.distance <= 10){
			//Speed up
			if (AISpeed < (205)){
				//Draft gets stronger as you get closer
				AISpeed += ((10 - DraftCheckForward.distance)/1000);
			}
		} else {
			//Slow down
			if (AISpeed > 200){
				//No draft, slow with drag
				AISpeed -= 0.003f;
			}
		}
		
		//If recieving backdraft from car behind
		if (HitBackward && DraftCheckBackward.distance <= 1.5f){
			AISpeed += (0.004f);
		}
		
		// If being bump-drafted from behind
		if (HitBackward && DraftCheckBackward.distance <= 1.05f){
			AISpeed += 0.006f;
			tandemDraft = true;
		} else {
			tandemDraft = false;
		}

		//If bump-drafting the car in front
		if (HitForward && DraftCheckForward.distance <= 1.01f)
		{
			if(DraftCheckForward.transform.gameObject.name != null){
				DraftCheckForward.transform.gameObject.SendMessage("ReceivePush",AISpeed);
			}
		} else {
			tandemDraft = false;
		}
		
		//Speed difference between the player and the AI
		speed = AISpeed - Movement.playerSpeed;
		speed = speed / 100;
		AICar.transform.Translate(0, 0, speed);
	}
	
	void dumbSpeed(string symbol){
		
		if(symbol == "+"){
		AISpeed += 0.002f;
		}
		if(symbol == "-"){
		AISpeed -= 0.002f;
		}
		
		//Speed difference between the player and the AI
		speed = AISpeed - Movement.playerSpeed;
		speed = speed / 100;
		AICar.transform.Translate(0, 0, speed);
	}

	void updateMovement() {
		
		//How fast can you switch lanes
        if (laneticker > 0)
        {
            AICar.transform.Translate(-laneChangeSpeed, 0, 0);
            laneticker--;
        }

        if (laneticker < 0)
        {
            AICar.transform.Translate(laneChangeSpeed, 0, 0);
            laneticker++;
        }

        if (laneticker == 0)
        {
            movingLane = false;
			forcedMove = false;
            backingOut = false;
        }
		
		if(AICar.transform.position.x <= apronLineX){
			Debug.Log("Track Limits!");
			backingOut = true;
			forcedMove = true;
			laneticker = -laneChangeDuration + laneticker;
			lane--;
		}
		
		if(AICar.transform.position.x >= 1.275f){
			//Debug.Log("Wall!");
			forcedMove = true;
			backingOut = true;
			laneticker = laneChangeDuration + laneticker;
			//Debug.Log("Now: " + laneticker + " Dur:" + laneChangeDuration);
			lane++;
			//Wall hit decel
			AISpeed -= 1f;
		}
		
		wobbleCount++;
		
		if(wobbleCount >= wobbleRand){
			wobbleRand = Random.Range(5,25);
			wobbleTarget = Random.Range(-100,100);
			wobbleCount = 1;
		}
		
		//General wobble while in lane
		if(wobbleTarget > wobblePos){
			AICar.transform.Translate(-0.0015f,0,0);
			wobblePos++;
		}
		
		if(wobbleTarget < wobblePos){
			AICar.transform.Translate(0.0015f,0,0);
			wobblePos--;
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
			if(holdLane >= laneRest){
				findDraft();
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
		//Debug.DrawRay(transform.position + new Vector3(-1.2f,0,1.1f), transform.forward * 5, Color.cyan);
		//Debug.DrawRay(transform.position + new Vector3(1.2f,0,1.1f), transform.forward * 5, Color.cyan);
		
		if (HitLaneLeft){
			if(leftSideClear()){
				if(DraftCheckLaneLeft.distance >= 2f){
					//Go for it, regardless of closing speed
					direction = "Left";
				} else {
					//Only seek a close draft if faster than you
					float opponentSpeed = getOpponentSpeed(DraftCheckLaneLeft.collider.gameObject);
					if(AISpeed <= opponentSpeed){
						direction = "Left";
					}
				}
			}
		}
		
		if (HitLaneRight){
			if(rightSideClear()){
				if(DraftCheckLaneRight.distance >= 2f){
					if(direction == "Left"){
						direction = "Both";
					} else {
						direction = "Right";
					}
				} else {
					float opponentSpeed = getOpponentSpeed(DraftCheckLaneRight.collider.gameObject);
					if(AISpeed <= (opponentSpeed)){
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
		
		//DISABLE DOORED
		return false;
		
		//Randomness
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
		
		if((laneticker == 0)&&(movingLane == false)){
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
			backingOut = false;
			holdLane = 0;
		}
	}
	
	public void setLaneRest(){
		if(lap >= 3){
			laneRest = Random.Range(0, 50);
			//Debug.Log("Get Lairy");
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
	
	float getOpponentSpeed(GameObject opponent){
		if(opponent.tag == "AICar"){
			return opponent.GetComponent<AIMovement>().AISpeed;
		}
		if(opponent.tag == "Player"){
			return Movement.playerSpeed;
		}
		return 0;
	}
	
	bool checkRaycast(string rayDirection, float rayLength){
		bool rayHit;
		RaycastHit DraftCheck;
		if(rayLength != null){
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
			case "LeftCorners":
				rayHit = Physics.Raycast(transform.position, transform.forward - transform.right, out DraftCheck, rayLength);
				rayHit = Physics.Raycast(transform.position, (transform.forward * -1) - transform.right, out DraftCheck, rayLength);
				break;
			case "RightCorners":
				rayHit = Physics.Raycast(transform.position, transform.forward + transform.right, out DraftCheck, rayLength);
				rayHit = Physics.Raycast(transform.position, (transform.forward * -1) + transform.right, out DraftCheck, rayLength);
				break;
			case "LeftEdge":
				rayHit = Physics.Raycast(transform.position + new Vector3(-1f,0,-1f), transform.forward, out DraftCheck, rayLength);
				Debug.DrawRay(transform.position + new Vector3(-1f,0,-1f), Vector3.forward * 2, Color.red);
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
