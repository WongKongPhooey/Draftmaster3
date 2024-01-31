using UnityEngine;
using System.Collections;

public class EnviroMovement : MonoBehaviour {

	public GameObject EnviroObject;
	public bool staticScroll;
	public bool scrollOnce;
	float scrollSpeedScaler;
	public float carSpeedOffset;
	public int trackSpeedOffset;
	public float wreckOffsetMulti;
	static float enviroSpeed;
	public float enviroSpeedViewer;
	public float maxScrollSpeed;
	static float scrollPos;
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
		sizeMulti = EnviroObject.transform.localScale.z / 32f;
		//This is for manually matching up static scrolling to non-static
		hackScaler = 0.2f;
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
			
			rend.material.mainTextureOffset = new Vector2(0, scrollPos);
		} else {
			//Slowest game speed is car:80, track:105 (LA start), Scaler = 1.33 + 2.63 = 0.36f
			enviroSpeed = (-3.6f + (carSpeedOffset / 60f) + (trackSpeedOffset / 40f)) * wreckOffsetMulti;
			
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
