using UnityEngine;
using System.Collections;

public class EnviroMovement : MonoBehaviour {

	public GameObject EnviroObject;
	public bool staticScroll;
	public bool scrollOnce;
	float scrollSpeedScaler;
	public float carSpeedOffset;
	public int trackSpeedOffset;
	static float enviroSpeed;
	public float enviroSpeedViewer;
	static float minScrollSpeed;
	static float scrollPos;
	Renderer rend;

	void Awake(){
		if(staticScroll == true){
			rend = GetComponent<Renderer>();
		}
		minScrollSpeed = 0.175f;
		carSpeedOffset = 0;
		trackSpeedOffset = PlayerPrefs.GetInt("SpeedOffset");
		enviroSpeed = -3.8f;
		scrollPos = 1;
	}

	// Update is called once per frame
	void FixedUpdate () {
		
		if(Movement.wreckOver == true){
			return;
		}
		
		carSpeedOffset = CameraRotate.carSpeedOffset - Movement.playerWreckDecel;

		if(staticScroll == true){
			//Slowest game speed is car:80, track:105 (LA start), Scaler = 0.066 + 0.131 = 0.197f		
			float scrollCalc = (carSpeedOffset / 1200f) + (trackSpeedOffset / 800f);
			float scrollVX = ((minScrollSpeed - scrollCalc) / 2);
			if(scrollVX >= 0){
				scrollPos -= scrollVX;
			} else {
				//Can't go backwards..
				scrollVX = 0;
			}
			if(scrollPos < 0){
				scrollPos+=1;
			}
			
			rend.material.mainTextureOffset = new Vector2(0, scrollPos);
		} else {
			enviroSpeed = -3.6f + (carSpeedOffset / 60f) + (trackSpeedOffset / 40f);
			
			//Can't go backwards..
			if(enviroSpeed <= 0){
				EnviroObject.transform.Translate(0,0,enviroSpeed);
			}
			
			if (EnviroObject.transform.position.z <= -30){
				EnviroObject.transform.Translate(0,0,60);
			}
		}
		enviroSpeedViewer = enviroSpeed;
	}
}
