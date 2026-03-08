using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using System;
using System.Collections;
using System.Collections.Generic;
using Random=UnityEngine.Random;
using Unity.Entities.UniversalDelegates;
using UnityEditor.Experimental.GraphView;
using UnityEditor.Callbacks;

public class VehicleLogic : MonoBehaviour
{
	GameObject vehicle;
	public bool isPlayer = false;
	static Camera mainCam;
	public Vector2 pos;
	private Vector2 direction;
	private float worldDirection;
	private float diffToWorldRads;
	private int sensitivity;
	private bool autoTurn;

	//Global info
	public static float playerSpeedMetres;
	public Material motionShader;
	public Material motionShader2x;
	public Material motionShader4x;

	public float frameMotion;
	public float motionOffset;
	public float motionOffset2x;
	public float motionOffset4x;
	public float lastXOffset;
	public static float xShift;
	public float yRatio;
	float prevYRatio;

    //Vehicle info
	public int topSpeed;
	public float zeroToSixty;
	public float gearRatio;
	public AnimationCurve accelerationCurve;
    public string carName;
	public int carNum;
	public string carTeam;
	public string carManu;
    string seriesPrefix;

    //Track/Location info
	public static VehicleInfo currentVehicleInfo;
    public static TrackInfo currentTrackInfo;
	public AnimationCurve[] racingLines;
	public AnimationCurve[] turnSpeeds;
	public int[] turnEntries;
	public int[] turnExits;
	public float[] steeringAngles;
	public float trackWidth;

	public int turn = 0;
	public bool inArc = false;
	public bool onTurn = false;
	public bool pitting = false;

	public float yDiff;
	public float yLine;

    //Speed variables
    public float speed;
    public float speedMetres;
    public float locationOnTrack;
    
	float targetSpeed, pointAccel, pointDecel;
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

	//AI Logic variables
	bool arcAdjusted = false;

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
	Rigidbody2D rb;
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
	void Start()
	{

		vehicle = this.gameObject;
		pos = transform.position;
		mainCam = GameObject.Find("MainCamera").GetComponent<Camera>();
		sensitivity = InputManager.inputSensitivity;
		autoTurn = InputManager.autoTurn;
		xShift = 0;
		worldDirection = 0;

		trackWidth = 13f;
		yRatio = (vehicle.transform.position.y + (trackWidth / 2)) / trackWidth;

		if (isPlayer == true)
		{
			RaceManager.setPlayer(vehicle);
		}

		currentVehicleInfo = Resources.Load<VehicleInfo>("Vehicles/Cup24");
		//currentVehicleInfo = Resources.Load<VehicleInfo>("Vehicles/PushCart");
		currentTrackInfo = Resources.Load<TrackInfo>("Tracks/Phoenix");

		zeroToSixty = currentVehicleInfo.zeroToSixty;

		//Debug.Log("Top Speed: " + currentVehicleInfo.topSpeed);
		accelerationCurve = currentVehicleInfo.accelerationCurve;
		//accelerationCurve.keys[2] = new Keyframe(15,currentTrackInfo.topSpeed);

		//Todo: This should be calculated/offset from where they spawn
		locationOnTrack = 0 - vehicle.transform.position.x;
		turn = 0;

		initRacingLines();

		//speed = 55;
		speed = 0;
		speedMetres = speed / 2.237f;
		
		rb = this.gameObject.GetComponent<Rigidbody2D>();
    }

	void initRacingLines(){
		racingLines = new AnimationCurve[currentTrackInfo.totalTurns];
		turnEntries = new int[currentTrackInfo.totalTurns];
		turnExits = new int[currentTrackInfo.totalTurns];
		steeringAngles = new float[currentTrackInfo.totalTurns];
		turnSpeeds = new AnimationCurve[currentTrackInfo.totalTurns];

		for(int i=0;i<currentTrackInfo.totalTurns;i++){
			//Debug.Log("Turn: " + i + " - Entry:" + (currentTrackInfo.turnPositions[i] - currentTrackInfo.turnLeadIn[i]));
			turnEntries[i] = currentTrackInfo.turnPositions[i] - currentTrackInfo.turnLeadIn[i];
			turnExits[i] = currentTrackInfo.turnPositions[i] + currentTrackInfo.turnLengths[i] + currentTrackInfo.turnLeadOut[i];
			steeringAngles[i] = currentTrackInfo.steeringAngles[i];
			racingLines[i] = new AnimationCurve(new Keyframe(turnEntries[i], currentTrackInfo.idealEntry[i]), new Keyframe(currentTrackInfo.turnPositions[i] + (currentTrackInfo.turnLengths[i]/2f),currentTrackInfo.idealMidpoint[i]), new Keyframe(turnExits[i],currentTrackInfo.idealExit[i]));
		}
	}

	public void setAsPlayer(){
		RaceManager.setPlayer(this.gameObject);
		InputManager.ChangeInputMap("InCar");
	}

    // Update is called once per frame
    void FixedUpdate(){

		xShift = RaceManager.playerXShift;

		speed = updateSpeed(targetSpeed, onTurn, pitting);

		calculateDraft();

		updateTurnLogic();
		
		updateLocation();

		//Check who the active player is
		if (RaceManager.thePlayer == this.gameObject) {
			isPlayer = true;
			playerLogic();
		} else {
			isPlayer = false;
			opponentLogic();
		}

		//Check for a pit entry
		if(((currentTrackInfo.pitEntryTriggerX - 1) < locationOnTrack)
		&&((currentTrackInfo.pitEntryTriggerX + 1) > locationOnTrack)
		&&((currentTrackInfo.pitEntryTriggerY) > yRatio)
		&&((currentTrackInfo.pitEntryTriggerY - 1f) < yRatio)){
			pitting = true;
		}
	}

	void playerLogic(){
		
		//Send the new motion speed to the environment objects
		if (isWrecking == true) {
			wreckMotion();
		} else {
			updateMotion();
		}
		HUDManager.updateHUD(speed);

		//playerSpeedMetres is used to sync up all scrolling motion in the scene
		playerSpeedMetres = speedMetres;

		//If autoturn is not enabled
		if (autoTurn == false){
			
			//Update the car's rotation based on steering input
			worldDirection += direction.x * sensitivity * Time.deltaTime;
			//Debug.Log("Player rotation: " + worldDirection);

			//Compare the rotation of the car to the circuit, and convert to radians
			diffToWorldRads = ((worldDirection - EnvironmentManager.circuitRotation) * 3.14159f) / 180f;
        	//Debug.Log("Player rotation rads: " + diffToWorldRads);

			//Manual steering control
			if (onTurn == true){
				//Car pulls naturally wide in the turns (so we add -steeringAngle)
				direction.Set(InputManager.direction.x + (steeringAngles[turn] / 25), 0);
			} else {
				direction.Set(InputManager.direction.x, 0);
			}

			//Trigonometry fun time
			vehicle.transform.Translate(0, playerSpeedMetres * Mathf.Sin(diffToWorldRads) * Time.deltaTime, 0);
		} else
		{
			InputManager.playerYRatio = yRatio;
		}
		vehicle.transform.Translate(xShift,0,0);
	}

	void opponentLogic(){
		
		//Move the other cars relative to the player
		vehicle.transform.Translate(new Vector2((playerSpeedMetres - speedMetres) * Time.deltaTime, 0));

		#if UNITY_EDITOR
			if(debugPlayer == true){
				Debug.Log("Distance opponent moved X: " + playerSpeedMetres + " - " + speedMetres);
			}
		#endif

		if (RaceManager.thePlayer.tag != "Vehicle"){
			
			//Not in a car, so make the cars 'loop' to simulate laps
			if (vehicle.transform.position.x < (-RaceManager.trackLength / 2)){
				vehicle.transform.Translate(RaceManager.trackLength, 0, 0);
			}
		}
		vehicle.transform.Translate(xShift,0,0);
	}

	void automatedPitControl(){
		yLine = currentTrackInfo.pitLane.Evaluate(locationOnTrack);
		vehicle.transform.position = new Vector2(vehicle.transform.position.x, (yLine * trackWidth) - (trackWidth / 2));
			
		//Check for a pit exit
		if(((currentTrackInfo.pitExitTriggerX - 1) < locationOnTrack)
		&&((currentTrackInfo.pitExitTriggerX + 1) > locationOnTrack)
		&&((currentTrackInfo.pitExitTriggerY) > yRatio)
		&&((currentTrackInfo.pitExitTriggerY - 1f) < yRatio)){
			pitting = false;
		}
	}

	void assistedLineControl(bool inArc){
		if(inArc == true){
			yLine = racingLines[turn].Evaluate(locationOnTrack);
			vehicle.transform.position = new Vector2(vehicle.transform.position.x, (yLine * trackWidth) - (trackWidth / 2));
		}
	}

	void updateLocation() {

		pos = vehicle.transform.position;

		//Update Y location relative to the track
		prevYRatio = yRatio;
		yRatio = (vehicle.transform.position.y + (trackWidth / 2)) / trackWidth;

		//Leans the car inwards/outwards in the turns
		//vehicle.transform.rotation = Quaternion.Euler(0, 0, (prevYRatio - yRatio) * 500);
		vehicle.transform.rotation = Quaternion.Euler(0, 0, (EnvironmentManager.circuitRotation - worldDirection));

		locationOnTrack+= (speedMetres) * Time.deltaTime;
	}

	float updateSpeed(float targetSpeed, bool onTurn, bool pitting = false){

		pointAccel = currentVehicleInfo.accelerationCurve.Evaluate(speed) * Time.deltaTime;
		pointDecel = currentVehicleInfo.decelerationCurve.Evaluate(speed) * Time.deltaTime;

		/*
		*  If speed difference is beyond the max acceleration of the car
		*/
		if ((targetSpeed - speed) > pointAccel)
		{
			//Just accelerate instead
			speed += pointAccel;
		}
		else
		{
			if ((targetSpeed - speed) < pointDecel)
			{
				//Decelerate (Brake) as hard as possible
				speed += pointDecel;
			}
			else
			{
				//Follow the calculated speed curve
				return targetSpeed;
			}
		}

		//Convert from MpH to m/s
		speedMetres = speed / 2.237f;

		return speed;
	}

	void updateTurnLogic() {

		//Update turn and arc triggers
		onTurn = checkTurnStatus(turn, locationOnTrack, onTurn);
		inArc = checkArcStatus(turn, locationOnTrack, inArc, yRatio);
		if (pitting == true)
		{
			automatedPitControl();
			targetSpeed = currentTrackInfo.pitSpeed.Evaluate(locationOnTrack);
		}
		else
		{
			if (inArc == true)
			{
				if ((autoTurn == true) || (isPlayer == false))
				{
					assistedLineControl(inArc);
				}
				targetSpeed = turnSpeeds[turn].Evaluate(locationOnTrack);
			}
			else
			{
				targetSpeed = 999;
			}
		}
		
		//On turn logic
		if(onTurn == true){
			if(isPlayer == true){
				float frameRotation = currentTrackInfo.turnAngles[turn] / (currentTrackInfo.turnLengths[turn] / speedMetres) * Time.deltaTime;
				mainCam.transform.Rotate(0,0,-frameRotation);
				EnvironmentManager.circuitRotation = mainCam.transform.rotation.x;
			} else {
				int awarenessVal = 20;
				checkQuarters(awarenessVal);
			}
		}
	}

	bool checkTurnStatus(int turn, float location, bool isOnTurn){
		if(location > (currentTrackInfo.turnPositions[turn] + currentTrackInfo.turnLengths[turn])){
			return false;
		} else {
			if(location >= currentTrackInfo.turnPositions[turn]){
				if(isOnTurn == false){
					return true;
				}
			}
		}
		return isOnTurn;
	}

	bool checkArcStatus(int turn, float location, bool isInArc, float yRatio){
		//Are we in a turn (incl. the arc)?
		Debug.Log(turn);
		if(location > turnExits[turn]){
			updateTurnCount(turn);
			return false;
		} else {
			if(location >= turnEntries[turn]){
				if(isInArc == false){
					calcNextTurnArc(turn, yRatio);
					return true;
				}
			}
		}
		return isInArc;
	}

	void calcNextTurnArc(int turn, float yRatio){
		
		float midpointRatio;
		float rnd = Random.Range(4,6)/10f;
		if(rnd >= (currentTrackInfo.highestEntry[turn] - 0.2f)){
			//Go high, rip the wall
			midpointRatio = currentTrackInfo.highestMidpoint[turn];
			racingLines[turn] = new AnimationCurve(new Keyframe(turnEntries[turn], yRatio), new Keyframe(currentTrackInfo.turnPositions[turn] + (currentTrackInfo.turnLengths[turn]/2f),randLine(currentTrackInfo.highestMidpoint[turn])), new Keyframe(turnExits[turn],randLine(currentTrackInfo.highestExit[turn])));
		
			#if UNITY_EDITOR
			if(debugPlayer == true){
				Debug.Log("High turn " + turn + " arc calculated: " + yRatio);
			}
			#endif

		} else {
			if(rnd <= (currentTrackInfo.lowestEntry[turn] + 0.2f)){
				//Dive down low
				midpointRatio = currentTrackInfo.lowestMidpoint[turn];
				racingLines[turn] = new AnimationCurve(new Keyframe(turnEntries[turn], yRatio), new Keyframe(currentTrackInfo.turnPositions[turn] + (currentTrackInfo.turnLengths[turn]/2f),randLine(currentTrackInfo.lowestMidpoint[turn])), new Keyframe(turnExits[turn],randLine(currentTrackInfo.lowestExit[turn])));
			
				#if UNITY_EDITOR
				if(debugPlayer == true){
					Debug.Log("Low turn " + turn + " arc calculated: " + yRatio);
				}
				#endif

			} else {
				midpointRatio = currentTrackInfo.idealMidpoint[turn];
				racingLines[turn] = new AnimationCurve(new Keyframe(turnEntries[turn], yRatio), new Keyframe(currentTrackInfo.turnPositions[turn] + (currentTrackInfo.turnLengths[turn]/2f),randLine(currentTrackInfo.idealMidpoint[turn])), new Keyframe(turnExits[turn],randLine(currentTrackInfo.idealExit[turn])));
			
				#if UNITY_EDITOR
				if(debugPlayer == true){
					Debug.Log("Mid turn " + turn + " arc calculated: " + yRatio);
				}
				#endif
			}
		}

		//Random for now
		//float midpointRatio = Random.Range(0,10) / 10f;
		//racingLines[turn] = new AnimationCurve(new Keyframe(turnEntries[turn], yRatio), new Keyframe(currentTrackInfo.turnPositions[turn] + (currentTrackInfo.turnLengths[turn]/2f), midpointRatio), new Keyframe(turnExits[turn], Random.Range(80,100) / 100f));
	
		bool flatOut = false;
		if(currentTrackInfo.turnMaxSpeeds[turn] >= currentTrackInfo.topSpeed){
			flatOut = true;
		}

		calcNextTurnSpeed(turn, speed, midpointRatio, flatOut);
	}

	void recalcTurnArc(int turn, bool carInside, bool carOutside){

		float currentYRatio = racingLines[turn].Evaluate(locationOnTrack);
		if(carInside == true){
			racingLines[turn] = new AnimationCurve(new Keyframe(locationOnTrack, yRatio), new Keyframe(turnExits[turn], 1f));
		}
		if(carOutside == true){
			racingLines[turn] = new AnimationCurve(new Keyframe(locationOnTrack, yRatio), new Keyframe(turnExits[turn], 0.6f));
		}
		arcAdjusted = true;
	}

	void calcNextTurnSpeed(int turn, float speed, float yRatio, bool flatOut){

		//Speed based on arc (and banking)
		float minTurnY = currentTrackInfo.lowestEntry[turn];
		float maxTurnY = currentTrackInfo.highestEntry[turn];
		float ySpread = maxTurnY - minTurnY;

		//Predetermined speed curves, based on shape of turn and banking
		AnimationCurve minTurnArc = currentTrackInfo.lowTurnDecel[turn];
		AnimationCurve maxTurnArc = currentTrackInfo.highTurnDecel[turn];

		turnSpeeds[turn] = new AnimationCurve();
		for(int i=0;i<currentTrackInfo.turnLengths[turn];i+=20){

			if(flatOut == true){
				turnSpeeds[turn].AddKey(new Keyframe(currentTrackInfo.turnPositions[turn],currentTrackInfo.topSpeed));
				continue;
			}

			//First keyframe is just entry speed
			if(i==0){
				turnSpeeds[turn].AddKey(new Keyframe(currentTrackInfo.turnPositions[turn],speed));
				continue;
			}

			//arcRatio = yRatio zero'd (by subtracting the min) divided by the possible spread
			//This returns what % of the arc is being run 
			//e.g. if min is 0.2, max is 0.9, spread is 0.7, actual is 0.35
			//then 0.35 - 0.2 is 0.15, divided by 0.7 = 21% 
			//So the car is 21% up from the lowest arc, compared to the highest arc.
			//float arcRatio = (yRatio - minTurnY) / ySpread;

			float arcRatio = (racingLines[turn].Evaluate(currentTrackInfo.turnPositions[turn] + i) - minTurnY) / ySpread;
			float highLineSpeed = maxTurnArc.Evaluate(currentTrackInfo.turnPositions[turn] + i);
			float lowLineSpeed = minTurnArc.Evaluate(currentTrackInfo.turnPositions[turn] + i);
			float speedSpread = 0.5f;
			float speedRatio = 0.5f;
			bool highLineFast = false;
			if(highLineSpeed < lowLineSpeed){
				highLineFast = true;

				//Amount of possible variation between low line and high line speeds
				speedSpread = lowLineSpeed - highLineSpeed;
				speedRatio = (speedSpread * arcRatio) + maxTurnArc.Evaluate(currentTrackInfo.turnPositions[turn] + i);
			} else {
				speedSpread = highLineSpeed - lowLineSpeed;
				speedRatio = (speedSpread * arcRatio) + minTurnArc.Evaluate(currentTrackInfo.turnPositions[turn] + i);
			}
			turnSpeeds[turn].AddKey(new Keyframe(currentTrackInfo.turnPositions[turn] + i,speedRatio));

			#if UNITY_EDITOR
			if(debugPlayer == true){
				Debug.Log("High Line Speed: " + highLineSpeed + " - Low Line Speed: " + lowLineSpeed + " - Speed Ratio: " + speedRatio + " - Speed Spread: " + speedSpread + " - Arc Ratio: " + arcRatio + " - High Line Fast?: " + highLineFast);
			}
			#endif
		}

		for(int j=0;j<turnSpeeds[turn].keys.Length;j++){
			turnSpeeds[turn].SmoothTangents(j,0);
		}

		//Fixed Speed (for now)
		//turnSpeeds[turn] = new AnimationCurve(new Keyframe(turnEntries[turn], speed), new Keyframe(currentTrackInfo.turnPositions[turn] + (currentTrackInfo.turnLengths[turn]/2f),currentTrackInfo.turnMaxSpeeds[turn]), new Keyframe(turnExits[turn],speed));
	}

	float randLine(float yPos){
		int consistency = 15;
		float variability=(21-consistency)/50f;

		if(yPos < variability){
			return Random.Range(0,yPos + variability);
		}
		if((yPos + variability) > 1){
			return Random.Range(yPos - variability,1);
		}

		return Random.Range(yPos - variability,yPos + variability);
	}

	void updateTurnCount(int t){
		t++;
		if(t >= currentTrackInfo.totalTurns){
			t = 0;
			locationOnTrack = 0;
		}
		turn = t;
		arcAdjusted = false;

		#if UNITY_EDITOR
		if(debugPlayer == true){
			Debug.Log("Turn updated to: " + turn);
		}
		#endif
	}

	public void updateMotion(){
		RaceManager.playerSpeedMetres = speedMetres;
		frameMotion = (playerSpeedMetres / 10.24f) * Time.deltaTime;
		motionOffset -= frameMotion;
		motionOffset2x -= (playerSpeedMetres / 20.48f) * Time.deltaTime;
		motionOffset4x -= (playerSpeedMetres / 40.96f) * Time.deltaTime;
		if(motionOffset <= 0){
			motionOffset++;
		}
		if(motionOffset2x <= 0){
			motionOffset2x++;
		}
		if(motionOffset4x <= 0){
			motionOffset4x++;
		}
		RaceManager.motionOffset = motionOffset;
		RaceManager.frameMotion = frameMotion;
		//motionShader.SetFloat("_MotionOffset", motionOffset);
		//motionShader2x.SetFloat("_MotionOffset", motionOffset2x);
		//motionShader4x.SetFloat("_MotionOffset", motionOffset4x);
	}

	public void wreckMotion()
	{
		//Convert from MpH to m/s
		playerSpeedMetres = rb.linearVelocity.x;
		RaceManager.playerSpeedMetres = playerSpeedMetres;

		frameMotion = (playerSpeedMetres / 10.24f) * Time.deltaTime;
		motionOffset -= frameMotion;
		motionOffset2x -= (playerSpeedMetres / 20.48f) * Time.deltaTime;
		motionOffset4x -= (playerSpeedMetres / 40.96f) * Time.deltaTime;
		if (motionOffset <= 0)
		{
			motionOffset++;
		}
		if (motionOffset2x <= 0)
		{
			motionOffset2x++;
		}
		if (motionOffset4x <= 0)
		{
			motionOffset4x++;
		}
		RaceManager.motionOffset = motionOffset;
		RaceManager.frameMotion = frameMotion;
		
		speed = rb.linearVelocity.x;

		if(speed < 5){
			rb.linearVelocity = new Vector2(0,0);
			rb.angularDamping = 500f;
			speed = 0;
		}
	}

	void calculateDraft(){
		RaycastHit2D checkFront = Physics2D.Raycast(pos + new Vector2(-2.56f,0), Vector2.left,10,LayerMask.GetMask("Vehicles"));
		Debug.DrawRay(pos + new Vector2(-2.56f,0), Vector2.left * 10, Color.green);

		bool hitDraft = (checkFront.distance < 10);
		if(hitDraft == true){
			//speed+=0.1f;
		}
	}

	void checkQuarters(int awareness){

		float awarenessDist = 0.25f * awareness;

		RaycastHit2D checkFrontLeft = Physics2D.Raycast(pos + new Vector2(-2.56f,-1.28f), Vector2.down,awarenessDist,LayerMask.GetMask("Vehicles"));
		RaycastHit2D checkRearLeft = Physics2D.Raycast(pos + new Vector2(2.56f,-1.28f), Vector2.down,awarenessDist,LayerMask.GetMask("Vehicles"));
		RaycastHit2D checkFrontRight = Physics2D.Raycast(pos + new Vector2(-2.56f,1.28f), Vector2.up,awarenessDist,LayerMask.GetMask("Vehicles"));
		RaycastHit2D checkRearRight = Physics2D.Raycast(pos + new Vector2(2.56f,1.28f), Vector2.up,awarenessDist,LayerMask.GetMask("Vehicles"));
		//Debug.DrawRay(pos + new Vector2(-2.56f,-1.28f), Vector2.down * awarenessDist, Color.green);
		//Debug.DrawRay(pos + new Vector2(2.56f,-1.28f), Vector2.down * awarenessDist, Color.green);
		//Debug.DrawRay(pos + new Vector2(-2.56f,1.28f), Vector2.up * awarenessDist, Color.green);
		//Debug.DrawRay(pos + new Vector2(2.56f,1.28f), Vector2.up * awarenessDist, Color.green);


		bool hitLaneLeft = ((checkFrontLeft.distance > 0)&&(checkFrontLeft.distance < awarenessDist))||((checkRearLeft.distance > 0)&&(checkRearLeft.distance < awarenessDist));
		bool hitLaneRight = ((checkFrontRight.distance > 0)&&(checkFrontRight.distance < awarenessDist))||((checkRearRight.distance > 0)&&(checkRearRight.distance < awarenessDist));

		if(arcAdjusted == false){

			if(hitLaneLeft == true){

				#if UNITY_EDITOR
				//Debug.Log("#" + this.gameObject.name + " - Adjusted arc to avoid car inside");
				#endif

				recalcTurnArc(turn, true, false);
			} else {
				if(hitLaneRight == true){

					#if UNITY_EDITOR
					//Debug.Log("#" + this.gameObject.name + " - Adjusted arc to avoid car outside");
					#endif

					recalcTurnArc(turn, false, true);
				}
			}
		}
	}

	private void OnCollisionEnter2D(Collision2D collision)
	{
		if(collision.gameObject.layer == 3){return;}

		Debug.Log(this.gameObject.name + " collides with " + collision.gameObject.name + "");
		startWreck();
	}

	void startWreck()
	{

		//Bailout
		if (isWrecking == false)
		{
			//Debug.Log("Initial wrecking force: " + speedMetres);
		} else {
			return;
		}
		isWrecking = true;

		/*tireSmokeParticles.Play();
		
		if(Random.Range(1,10) <= 3){
			leftSparksParticles.Play();
			rightSparksParticles.Play();
		}
		
		Movement.incrTotalWreckers();
		
		isWrecking = true;
		wreckDamage+=1;
		RaceControl.isWrecking[carNum] = true;
		if(CameraRotate.cautionOut == false){No 
			CameraRotate.throwCaution();
		}
		sparksCooldown = 99999;
		*/

		Debug.Log("Max wall force: " + speedMetres + ", Actual: " + (speedMetres * (1 - Mathf.Sin(diffToWorldRads))));
		rb.AddForce(new Vector3(speedMetres * (1 - Mathf.Sin(diffToWorldRads)),0,0),ForceMode2D.Impulse);
		//rb.AddForce(new Vector3(speedMetres,0,0) ,ForceMode2D.Impulse);
		
		//rb.AddTorque(Random.Range(-50f,50f));

		/*
		//Apply wind/drag
		sparksEndSpeed = Random.Range(-130,-180);
		maxSparksRand = Random.Range(5,30);

		if(Movement.momentChecks == true){
			MomentsCriteria.checkMomentsCriteria("CarWrecks",carNum.ToString());
			MomentsCriteria.checkMomentsCriteria("CarWrecksAlso",carNum.ToString());
		}*/
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

	void wreckPhysics()
	{

		/*wreckAngle = this.gameObject.transform.rotation.y;
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
		}

		//Debug.Log("Sparks End: " + sparksEndSpeed + " Wreck Decel: " + wreckDecel);
		if (sparksEndSpeed < wreckDecel)
		{
			//Align particle system to global track direction
			leftSparks.rotation = Quaternion.Euler(0, 180, 0);
			rightSparks.rotation = Quaternion.Euler(0, 180, 0);
			leftSparksParticles.startSpeed = 100 + (wreckDecel / 2);
			rightSparksParticles.startSpeed = 100 + (wreckDecel / 2);
			leftSparksParticles.maxParticles = (int)Mathf.Floor(maxSparksRand + (wreckDecel / 12));
			rightSparksParticles.maxParticles = (int)Mathf.Floor(maxSparksRand + (wreckDecel / 12));
			leftSparksParticles.startLifetime = 0.2f + ((0 - wreckDecel) / 50);
			rightSparksParticles.startLifetime = 0.2f + ((0 - wreckDecel) / 50);
			leftSparksParticleRenderer.lengthScale = 0.5f + (wreckDecel / 200);
			rightSparksParticleRenderer.lengthScale = 0.5f + (wreckDecel / 200);
		}
		else
		{
			leftSparksParticles.Stop();
			rightSparksParticles.Stop();
		}

		//Flatten the smoke
		tireSmoke.rotation = Quaternion.Euler(0, 180, 0);
		float smokeMultiplier = wreckSine;
		smokeMultiplier = (smokeMultiplier * 60) + 5;
		smokeMultiplier = Mathf.Round(smokeMultiplier);
		tireSmokeParticles.startColor = new Color32(200, 200, 200, (byte)smokeMultiplier);
		tireSmokeParticles.startSpeed = 40 + (wreckDecel / 5);
		tireSmokeParticles.startSize = 12 + (wreckDecel / 30); //Max 12, Min 4.5
		tireSmokeParticles.maxParticles = (int)(70 + Mathf.Round(wreckDecel / 2)); //Max 70, Hits 0 at -140 decel
		*/
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
			
			startWreck();

			//No need to check the other stuff if wrecking
			if(isWrecking == true){
				return;
			}
        }
    }
}