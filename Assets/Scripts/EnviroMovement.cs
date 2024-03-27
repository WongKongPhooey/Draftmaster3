using UnityEngine;
using System.Collections;

public class EnviroMovement : MonoBehaviour {

	public GameObject EnviroObject;
	public bool staticScroll;
	public bool syncedStaticScroll;
	public bool syncedScrollMaster;
	public bool scrollOnce;
	float scrollSpeedScaler;
	public float carSpeedOffset;
	public int trackSpeedOffset;
	public float wreckOffsetMulti;
	static float enviroSpeed;
	public float enviroSpeedViewer;
	public float maxScrollSpeed;
	public float scrollPos;
	static float syncedScroll;
	public float scrollVX;
	public float sizeMulti;
	//public float fixedScroll;
	public static float hackScaler;
	Renderer rend;
	int tick;
	float playerZ;

	void Awake(){
		tick=0;
		if(staticScroll == true){
			rend = GetComponent<Renderer>();
		}
		playerZ = 0;
		maxScrollSpeed = -0.15f;
		carSpeedOffset = 0;
		trackSpeedOffset = PlayerPrefs.GetInt("SpeedOffset");
		enviroSpeed = -3.8f;
		scrollPos = 1;
		sizeMulti = EnviroObject.transform.localScale.z / 256f;
		//This is for manually matching up static scrolling to non-static
		hackScaler = 0.25f;
	}

	// Update is called once per frame
	void FixedUpdate () {
		
		tick++;
		if(tick%60 == 0){
			tick = 0;
			playerZ = Movement.playerZ;
		}

		if(Movement.wreckOver == true){
			return;
		}
		wreckOffsetMulti = (200 - trackSpeedOffset + Movement.playerWreckDecel) / 200;
		carSpeedOffset = CameraRotate.carSpeedOffset;

		if(staticScroll == true){
			//If the scroll should be synced to a master object (e.g. the ground)..
			//And this object is not the master object
			//Skip the calc logic and just take the synced scroll value
			if((syncedStaticScroll == true) && (syncedScrollMaster == false)){
				rend.material.mainTextureOffset = new Vector2(0, syncedScroll);
			} else {
				//Max offsets are car:80, track:105 (LA start)
				//Slowest pacing = 0.02f, Stock pacing = 0.05f, Fastest scroll = 0.15f	
				float scrollCalc = (carSpeedOffset / 900f) + (trackSpeedOffset / 3500f);
				//maxScroll will always be negative
				//scrollCalc should generally not exceed about 1.2f to look realistic
				//trackSpeedOffset should aim to shift the calc by about 0.3f at most
				scrollVX = (((maxScrollSpeed + scrollCalc) / sizeMulti) * wreckOffsetMulti) * hackScaler;
				#if UNITY_EDITOR
				//Debug.Log("Max Scroll:" + maxScrollSpeed + ", Scroll Calc:" + scrollCalc + ", Size Multi:" + sizeMulti + " , Wreck Offset Multi:" + wreckOffsetMulti);
				#endif
				if(scrollVX <= 0){
					scrollPos += scrollVX;
					//Swap to this for testing
					//scrollPos -= fixedScroll;
				} else {
					//Can't go backwards..
					scrollVX = 0;
				}
				if(scrollPos < 0){
					scrollPos+=10;
				}
				if(scrollPos > 10){
					scrollPos-=10;
				}

				//If this is the master sync object..
				//save this scroll value to the static variable..
				//for all other synced objects to refer to.
				if(syncedScrollMaster == true){
					syncedScroll = scrollPos;
				}
				rend.material.mainTextureOffset = new Vector2(0, scrollPos);
			}
		} else {
			//Slowest game speed is car:80, track:105 (LA start), Scaler = 1.33 + 2.63 = 0.36f
			//enviroSpeed = (-3.6f + (carSpeedOffset / 60f) + (trackSpeedOffset / 40f)) * wreckOffsetMulti;

			float scrollCalc = (carSpeedOffset / 900f) + (trackSpeedOffset / 3500f);
			enviroSpeed = (((maxScrollSpeed + scrollCalc) / sizeMulti) * wreckOffsetMulti) * hackScaler;

			//Can't go backwards..
			if(enviroSpeed <= 0){
				EnviroObject.transform.Translate(0,0,enviroSpeed);
			}
			
			if (EnviroObject.transform.position.z <= (playerZ-30)){
				if(scrollOnce == false){
					EnviroObject.transform.Translate(0,0,60);
				}
			}
		}
		enviroSpeedViewer = enviroSpeed;
	}
}
