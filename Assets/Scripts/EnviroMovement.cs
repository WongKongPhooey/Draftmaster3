using UnityEngine;
using System.Collections;

public class EnviroMovement : MonoBehaviour {

	public GameObject EnviroObject;
	float carSpeedOffset;
	int trackSpeedOffset;
	float enviroSpeed;

	void Awake(){
		carSpeedOffset = 0;
		trackSpeedOffset = PlayerPrefs.GetInt("SpeedOffset");
		Debug.Log("Track Speed Offset: " + trackSpeedOffset);
		enviroSpeed = -3;
	}

	// Update is called once per frame
	void FixedUpdate () {
		
		carSpeedOffset = CameraRotate.carSpeedOffset;
		enviroSpeed = -4 + (carSpeedOffset / 40) + (trackSpeedOffset / 40);
		Debug.Log("Enviro Speed: " + enviroSpeed);
		
		EnviroObject.transform.Translate(0,0,enviroSpeed);
		
		if (EnviroObject.transform.position.z < -40){
			EnviroObject.transform.Translate(0,0,80);
		}
	}
}
