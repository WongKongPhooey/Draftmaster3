using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using System;
using System.Collections;
using System.Collections.Generic;
using Random=UnityEngine.Random;
using Unity.Entities.UniversalDelegates;

public class VehicleLogic : MonoBehaviour
{
	GameObject vehicle;
	bool isPlayer = false;
	static Camera mainCam;

    //Car info
    public string carName;
	public int carNum;
	public string carTeam;
	public string carManu;
    string seriesPrefix;

    //Track info
    public static TrackInfo currentTrackInfo;
	public AnimationCurve[] racingLines;
	public int[] turnEntries;
	public int[] turnExits;

	public int turn = 0;

    //Speed variables
    public float speed;
    static float speedMetres;
    public float locationOnTrack;
    float engineTemp;

	float tempLimit;

	bool blownEngine;

	bool coolEngine;
	int sparksCooldown;

    //Draft variables
    float draftStrengthRatio;
	float dragDecelMulti;
	float backdraftMulti;
	float bumpDraftDistTrigger;
	float draftAirCushion;
	float passDistMulti;
	bool tandemDrafting;
    static int maxTandem;
	static float coolOffSpace;
	static float coolOffInv;

    //Wreck variables
    public bool isWrecking;
	public bool wreckOver;
	public float wreckDamage;
	float baseDecel;
	float randDecel;
	float slideX;
	public float wreckDecel;
	float wreckAngle;
	float wreckMassRand;
	float wreckSlideRand;
	float wreckFlatRand;
	float sparksEndSpeed;
	float maxSparksRand;
	float targetForce;
	float windForce;
	float forceSmoothing;
	int wreckProbability;
	bool hitByPlayer;

    //Cached components
    NativeArray<RaycastCommand> raycastBatch;
	NativeArray<RaycastHit> raycastHits;
	JobHandle raycastHandler;
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

	//Misc
	public bool debugPlayer;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start(){
        
		vehicle = this.gameObject;
		mainCam = GameObject.Find("Main Camera").GetComponent<Camera>();

        currentTrackInfo = Resources.Load<TrackInfo>("Tracks/Phoenix");

        //Todo: This should be calculated/offset from where they spawn
		locationOnTrack = 0;
		turn = 0;

		initRacingLines();

        speed = 140;
		speedMetres = speed / 2.237f;
    }

	void initRacingLines(){
		racingLines = new AnimationCurve[currentTrackInfo.totalTurns];
		turnEntries = new int[currentTrackInfo.totalTurns];
		turnExits = new int[currentTrackInfo.totalTurns];

		for(int i=0;i<currentTrackInfo.totalTurns;i++){
			//Debug.Log("Turn: " + i + " - Entry:" + (currentTrackInfo.turnPositions[i] - currentTrackInfo.turnLeadIn[i]));
			turnEntries[i] = currentTrackInfo.turnPositions[i] - currentTrackInfo.turnLeadIn[i];
			turnExits[i] = currentTrackInfo.turnPositions[i] + currentTrackInfo.turnLengths[i] + currentTrackInfo.turnLeadOut[i];
			racingLines[i] = new AnimationCurve(new Keyframe(turnEntries[i], currentTrackInfo.idealEntry[i]), new Keyframe(currentTrackInfo.turnPositions[i] + (currentTrackInfo.turnLengths[i]/2f),currentTrackInfo.idealMidpoint[i]), new Keyframe(turnExits[i],currentTrackInfo.idealExit[i]));
		}
	}



    // Update is called once per frame
    void Update(){
        locationOnTrack+= (speedMetres) * Time.deltaTime;
		if(locationOnTrack > turnExits[turn]){
			turn = updateTurnCount(turn);
		}

		if(isPlayer == true){
 			float frameRotation = RaceManager.turnAngle[turn] / (RaceManager.turnLength[turn] / speedMetres) * Time.deltaTime;
       		mainCam.transform.Rotate(0,0,-frameRotation);
		}

		Debug.Log("xLine: " + racingLines[turn].Evaluate(locationOnTrack));
		float xLine = racingLines[turn].Evaluate(locationOnTrack);
		vehicle.transform.position = new Vector2(vehicle.transform.position.x, -6.5f + (13 * xLine));
	}

	int updateTurnCount(int turn){
		turn += 1;
		if(turn >= currentTrackInfo.totalTurns){
			turn = 0;
			locationOnTrack = 0;
		}

		#if UNITY_EDITOR
		if(debugPlayer == true){
			Debug.Log("Turn updated to: " + turn);
		}
		#endif

		return turn;
	}

	void startWreck(){
		
		//Bailout
		if((isWrecking == true)||(wreckOver == true)||(Movement.pacing == true)){
			return;
		}
		
		tireSmokeParticles.Play();
		
		if(Random.Range(1,10) <= 3){
			leftSparksParticles.Play();
			rightSparksParticles.Play();
		}
		
		Movement.incrTotalWreckers();
		
		isWrecking = true;
		wreckDamage+=1;
		RaceControl.isWrecking[carNum] = true;
		if(CameraRotate.cautionOut == false){
			CameraRotate.throwCaution();
		}
		sparksCooldown = 99999;

		//Debug.Log(this.name + " is wrecking");
		
		//Make the car light, more affected by physics
		wreckRigidbody.mass = 2 + wreckMassRand;
		
		//Remove constraints, allowing it to impact/spin using physics
		wreckRigidbody.constraints &= ~RigidbodyConstraints.FreezeRotationY;
		wreckRigidbody.constraints &= ~RigidbodyConstraints.FreezePositionX;
		
		//Remove forces, physics only
		wreckRigidbody.isKinematic = false;
		wreckRigidbody.useGravity = false;
		
		//Apply wind/drag
		sparksEndSpeed = Random.Range(-130,-180);
		maxSparksRand = Random.Range(5,30);
		targetForce = Random.Range(10f,-10f);
		windForce = targetForce;
		forceSmoothing = 0.5f;
		baseDecel = -0.35f;
		randDecel = Random.Range(0.01f,0.1f);
		slideX = 0;
		wreckDecel = 0;
		wreckForce.force = new Vector3(0f, 0f,windForce);
		wreckForce.torque = new Vector3(0f, Random.Range(-0.5f, 0.35f) * 10, 0f);

		if(Movement.momentChecks == true){
			MomentsCriteria.checkMomentsCriteria("CarWrecks",carNum.ToString());
			MomentsCriteria.checkMomentsCriteria("CarWrecksAlso",carNum.ToString());
		}
	}
	
	public void endWreck(){
		speed = 0;
		slideX = 0;
		isWrecking = false;
		wreckOver = true;
		RaceControl.isWrecking[carNum] = false;
		RaceControl.hasWrecked[carNum] = true;
		
		sparksCooldown = 0;

		tireSmokeParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
		leftSparksParticles.Stop();
		rightSparksParticles.Stop();
	}
	
	void wreckPhysics(){
		
		wreckAngle = this.gameObject.transform.rotation.y;
		float wreckSine = Mathf.Sin(wreckAngle);
		if(wreckSine < 0){
			wreckSine = -wreckSine;
		}
		if(wreckSine < 0.2f){
			wreckSine = 0.2f;
		}

		baseDecel-=(0.45f - randDecel);
		slideX = ((baseDecel + 1) / 5f) + wreckSlideRand;
		//Formula: -200f = -10x, -140f = 0x, 0f = 10x
		//         -200f = -20x, -100f = -10x, 0f = 0x
		//         -200f = -6x, -140f = 0x, 0f = 14f
		//slideX = ((baseDecel + 1) / 10f) + 14f
		//Reduce division factor to increase effect
		
		updateWindForce(wreckSine);

		//Standard relativity
		targetForce = wreckDecel;
		if(CameraRotate.onTurn == true){
			wreckForce.force = new Vector3(slideX, 0f,windForce);
		} else {
			wreckForce.force = new Vector3(wreckFlatRand, 0f,windForce);
		}

		wreckDecel = baseDecel - (60f * wreckSine);
		
		if(wreckDecel < -200){
			endWreck();
		}
		
		wreckRigidbody.mass = (-wreckDecel / 20) + 2 + wreckMassRand;
		wreckRigidbody.angularDamping += 0.001f;
		
		//Prevent landing in the crowd
		/*if(pos.x > 1.5f){
			this.gameObject.transform.position = new Vector3(1.5f,pos.y,pos.z);
		}*/
		
		//Debug.Log("Sparks End: " + sparksEndSpeed + " Wreck Decel: " + wreckDecel);
		if(sparksEndSpeed < wreckDecel){
			//Align particle system to global track direction
			leftSparks.rotation = Quaternion.Euler(0,180,0);
			rightSparks.rotation = Quaternion.Euler(0,180,0);
			leftSparksParticles.startSpeed = 100 + (wreckDecel / 2);
			rightSparksParticles.startSpeed = 100 + (wreckDecel / 2);
			leftSparksParticles.maxParticles = (int)Mathf.Floor(maxSparksRand + (wreckDecel / 12));
			rightSparksParticles.maxParticles = (int)Mathf.Floor(maxSparksRand + (wreckDecel / 12));
			leftSparksParticles.startLifetime = 0.2f + ((0-wreckDecel) / 50);
			rightSparksParticles.startLifetime = 0.2f + ((0-wreckDecel) / 50);
			leftSparksParticleRenderer.lengthScale = 0.5f + (wreckDecel / 200);
			rightSparksParticleRenderer.lengthScale = 0.5f + (wreckDecel / 200);
		} else {
			leftSparksParticles.Stop();
			rightSparksParticles.Stop();
		}
		
		//Flatten the smoke
		tireSmoke.rotation = Quaternion.Euler(0,180,0);
		float smokeMultiplier = wreckSine;
		smokeMultiplier = (smokeMultiplier * 60) + 5;
		smokeMultiplier = Mathf.Round(smokeMultiplier);
		tireSmokeParticles.startColor = new Color32(200,200,200,(byte)smokeMultiplier);
		tireSmokeParticles.startSpeed = 40 + (wreckDecel / 5);
		tireSmokeParticles.startSize = 12 + (wreckDecel / 30); //Max 12, Min 4.5
		tireSmokeParticles.maxParticles = (int)(70 + Mathf.Round(wreckDecel / 2)); //Max 70, Hits 0 at -140 decel
	}
	
	void updateWindForce(float angleSin){
		if(windForce < targetForce - (forceSmoothing * (angleSin * 2))){
			windForce += forceSmoothing * (angleSin * 2);
		}
		if(windForce > targetForce + (forceSmoothing * (angleSin * 2))){
			windForce -= forceSmoothing * (angleSin * 2);
		}
	}

    void OnCollisionEnter(Collision carHit) {
		
        if ((carHit.gameObject.tag == "Opponent") || 
		    (carHit.gameObject.tag == "Wall")) {
			
			if((blownEngine == true)&&(isWrecking == false)){
				Debug.Log("Blown Engine Wreck");
				startWreck();
			}
			
			//Join wreck
			if(carHit.gameObject.tag == "Opponent"){
				if(isWrecking == true){
					//Share some wreck inertia
					float opponentWreckDecel = carHit.gameObject.GetComponent<AIMovement>().wreckDecel;
					wreckDamage += ((opponentWreckDecel - wreckDecel) / 2);
					wreckDecel += ((opponentWreckDecel - wreckDecel) / 2);
					RaceControl.wreckDamage[carNum] = wreckDamage;
				} else {
					bool joinWreck = carHit.gameObject.GetComponent<AIMovement>().isWrecking;
					if(joinWreck == true){
						//Debug.Log("Wreck: Joining In");
						startWreck();
					}
				}
			}

			//No need to check the other stuff if wrecking
			if(isWrecking == true){
				return;
			}

			/*if (laneticker != 0){
				if (laneticker > 0){
					bool rightSideHit = checkRaycast("RightCorners", 0.51f);
					if(rightSideHit == true){
						backingOut = true;
						laneticker = -laneChangeDuration + laneticker;
					}
				}
				if (laneticker < 0){
					bool leftSideHit = checkRaycast("LeftCorners", 0.51f);
					if(leftSideHit == true){
						backingOut = true;
						laneticker = laneChangeDuration + laneticker;
					}
				}
			} else {
				int dooredStrength = 0;
				if(carHit.gameObject.tag == "AICar"){
					dooredStrength = carHit.gameObject.GetComponent<VehicleLogic>().GetStat("AGG");
				}
				if(doored("Left",dooredStrength) == true){
					changeLane("Right");
				}
				if(doored("Right",dooredStrength) == true){
					changeLane("Left");
				}
			}
			speed -= Random.Range(0.5f,5f);*/
        }
				
        /*if(leftSideClear(0.51f) == false){
            leftSparksParticles.Play();
            sparksCooldown = Random.Range(5,20);
        }
        if(rightSideClear(0.51f) == false){
            rightSparksParticles.Play();
            sparksCooldown = Random.Range(5,20);
        }*/
    }

    void OnDestroy(){
		raycastBatch.Dispose();
		raycastHits.Dispose();
	}
}