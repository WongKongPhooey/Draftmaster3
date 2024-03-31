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
	public float enviroSpeed;
	public float enviroSpeedNewZ;
	public float enviroSpeedCurrentZ;
	static float enviroSpeedPercentage;
	public float enviroSpeedViewer;
	static float maxScrollSpeed;
	public float scrollPos;
	static float syncedScroll;
	public float scrollVX;
	public float sizeMulti;
	//public float fixedScroll;
	public float hackScaler;
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
		enviroSpeedCurrentZ = EnviroObject.transform.position.z;
		minPacingSpeed = 0.01f;
		scrollPos = 1;
		if(staticScroll == true){
			//Between 0.0039 and 0.025 (1 -> 64)
			sizeMulti = EnviroObject.transform.localScale.z / 256f;
		} else {
			//Non-static objects loop at a base of 64
			sizeMulti = 0.025f;
		}
		//This is for manually tuning the 'feeling of speed'
		hackScaler = 0.5f;
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
		wreckOffsetMulti = (200f - trackSpeedOffset + Movement.playerWreckDecel) / 200f;
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
				
				#if UNITY_EDITOR
				if(debugObject == true){
					Debug.Log("Name:" + this.gameObject.name + ", Synced Scroll:" + syncedScroll);
				}
				#endif
				
			} else {
				//maxScroll will always be negative
				//scrollCalc should generally not exceed about 1.2f to look realistic
				//trackSpeedOffset should aim to shift the calc by about 0.3f at most
				scrollVX = ((((maxScrollSpeed + scrollCalc) / sizeMulti) * wreckOffsetMulti) * hackScaler) - minPacingSpeed;
				#if UNITY_EDITOR
				if(debugObject == true){
					Debug.Log("Name:" + this.gameObject.name + ", ScrollVX:" + scrollVX + " , Max Scroll:" + maxScrollSpeed + ", Scroll Calc:" + scrollCalc + ", Size Multi:" + sizeMulti + ", Hack Scaler:" + hackScaler + " , Wreck Offset Multi:" + wreckOffsetMulti + ", Min Pacing Speed:" + minPacingSpeed);
				}
				#endif
				//Condenses to a decimal between 0 and 1
				scrollVX = scrollVX % 1;
				if(scrollVX <= 0f){
					scrollPos += scrollVX;
				} else {
					//Can't go backwards..
					scrollVX = 0f;
				}
				if(scrollPos < 0f){
					scrollPos+=1f;
				}
				if(scrollPos > 1f){
					scrollPos-=1f;
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
			//Update what the current value is
			enviroSpeedCurrentZ = enviroSpeedNewZ;

			//Returns a value between 0 and 1. Values outside of that should ignore the integer
			//enviroSpeedPercentage = ((((maxScrollSpeed + scrollCalc) / sizeMulti) * wreckOffsetMulti) * hackScaler) - minPacingSpeed;
			enviroSpeedPercentage = syncedScroll;
			enviroSpeedNewZ = 64f * enviroSpeedPercentage;
			enviroSpeedNewZ = enviroSpeedNewZ / 8f;
			
			//Convert this percentage to a distance along the 64 length track
			//e.g. 0.08 = 8% of 64 = 5.12
			if(enviroSpeedCurrentZ != enviroSpeedNewZ){
				enviroSpeed = enviroSpeedCurrentZ - enviroSpeedNewZ;
			}

			#if UNITY_EDITOR
			if(debugObject == true){
				Debug.Log("Name:" + this.gameObject.name + ", SyncedScroll:" + syncedScroll + ", EnviroSpeedNewZ:" + enviroSpeedNewZ + ", EnviroSpeedCurrentZ:" + enviroSpeedCurrentZ + ", EnviroSpeed:" + enviroSpeed);
			}
			#endif

			//Can't go backwards..
			if(enviroSpeed >= 0){
				EnviroObject.transform.Translate(0,0,-enviroSpeed);
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
