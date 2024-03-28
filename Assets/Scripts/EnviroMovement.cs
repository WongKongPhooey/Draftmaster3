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
	public float scrollCalc;
	public float minPacingSpeed;
	public float wreckOffsetMulti;
	static float enviroSpeed;
	public float enviroSpeedViewer;
	static float maxScrollSpeed;
	public float scrollPos;
	static float syncedScroll;
	public float scrollVX;
	public float sizeMulti;
	//public float fixedScroll;
	public static float hackScaler;
	public bool debugObject;
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
		minPacingSpeed = 1f/(trackSpeedOffset + 1f);
		scrollPos = 1;
		if(staticScroll == true){
			sizeMulti = EnviroObject.transform.localScale.z / 256f;
		} else {
			//Not sure why this value works, may need some thought
			sizeMulti = 0.19f;
		}
		//This is for manually tuning the 'feeling of speed'
		hackScaler = 0.25f;
		debugObject = false;
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
		//Max offsets are car:80, track:105 (LA Coliseum start)
		//Slowest pacing = 0.02f, Stock pacing = 0.05f, Fastest scroll = 0.15f	
		scrollCalc = (carSpeedOffset / 750f) + (trackSpeedOffset / 3500f);
		//e.g. 80/450 = 0.177, 105/3500 = 0.03 , total = 0.207
		//e.g. 80/900 = 0.088, 105/3500 = 0.03 , total = 0.0.118

		if(staticScroll == true){
			//If the scroll should be synced to a master object (e.g. the ground)..
			//And this object is not the master object
			//Skip the calc logic and just take the synced scroll value
			if((syncedStaticScroll == true) && (syncedScrollMaster == false)){
				rend.material.mainTextureOffset = new Vector2(0, syncedScroll);
			} else {
				//maxScroll will always be negative
				//scrollCalc should generally not exceed about 1.2f to look realistic
				//trackSpeedOffset should aim to shift the calc by about 0.3f at most
				scrollVX = ((((maxScrollSpeed + scrollCalc) / sizeMulti) * wreckOffsetMulti) * hackScaler) - minPacingSpeed;
				#if UNITY_EDITOR
				if(debugObject == true){
					Debug.Log("Name:" + this.gameObject.name + ", ScrollVX:" + scrollVX + " , Max Scroll:" + maxScrollSpeed + ", Scroll Calc:" + scrollCalc + ", Size Multi:" + sizeMulti + " , Wreck Offset Multi:" + wreckOffsetMulti + ", Min Pacing Speed:" + minPacingSpeed);
				}
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
			enviroSpeed = ((((maxScrollSpeed + scrollCalc) / 0.04f) * wreckOffsetMulti) * hackScaler) - minPacingSpeed;

			#if UNITY_EDITOR
			if(debugObject == true){
				Debug.Log("Name:" + this.gameObject.name + ", EnviroSpeed:" + enviroSpeed + " , Max Scroll:" + maxScrollSpeed + ", Scroll Calc:" + scrollCalc + ", Hack Scaler:" + hackScaler + " , Wreck Offset Multi:" + wreckOffsetMulti + ", Min Pacing Speed:" + minPacingSpeed);
			}
			#endif

			//Can't go backwards..
			if(enviroSpeed <= 0){
				EnviroObject.transform.Translate(0,0,enviroSpeed);
			}
			
			if (EnviroObject.transform.position.z <= (playerZ-32)){
				if(scrollOnce == false){
					EnviroObject.transform.Translate(0,0,64);
				}
			}
		}
		enviroSpeedViewer = enviroSpeed;
	}
}
