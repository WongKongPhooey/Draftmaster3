using UnityEngine;
using System.Collections;

public class EnviroMovement : MonoBehaviour {

	public GameObject EnviroObject;
	public bool staticScroll;
	public bool scrollOnce;
	float carSpeedOffset;
	int trackSpeedOffset;
	static float enviroSpeed;
	static float scrollPos;
	Renderer rend;

	void Awake(){
		if(staticScroll == true){
			rend = GetComponent<Renderer>();
		}
		carSpeedOffset = 0;
		trackSpeedOffset = PlayerPrefs.GetInt("SpeedOffset");
		//Debug.Log("Track Speed Offset: " + trackSpeedOffset);
		enviroSpeed = -3.8f;
		scrollPos = 1;
	}

	// Update is called once per frame
	void FixedUpdate () {
		
		carSpeedOffset = CameraRotate.carSpeedOffset;

		if(staticScroll == true){
			//scrollPos -= ((carSpeedOffset / 60) + (trackSpeedOffset / 40));
			scrollPos -= (0.15f - ((carSpeedOffset / 120) + (trackSpeedOffset / 80)));
			//Debug.Log(scrollPos);
			if(scrollPos < 0){
				scrollPos+=1;
			}
			
			rend.material.mainTextureOffset = new Vector2(0, scrollPos);
		} else {
			enviroSpeed = -3.8f + (carSpeedOffset / 60) + (trackSpeedOffset / 40);
			
			EnviroObject.transform.Translate(0,0,enviroSpeed);
			
			if (EnviroObject.transform.position.z <= -40){
				EnviroObject.transform.Translate(0,0,80);
			}
		}
	}
}
