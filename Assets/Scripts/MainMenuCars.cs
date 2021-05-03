using UnityEngine;
using System.Collections;

public class MainMenuCars : MonoBehaviour {

	public GameObject EnviroObject;

	// Update is called once per frame
	void FixedUpdate () {
		EnviroObject.transform.Translate(0,0,0.2f);
		
		if (EnviroObject.transform.position.z > 100){
			EnviroObject.transform.Translate(0,0,-200);
		}
	}
}
