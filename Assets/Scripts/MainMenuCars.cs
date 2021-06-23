using UnityEngine;
using System.Collections;

public class MainMenuCars : MonoBehaviour {

	public GameObject EnviroObject;

	// Update is called once per frame
	void FixedUpdate () {
		EnviroObject.transform.Translate(0.1f,0,0);
		
		if (EnviroObject.transform.position.z > 50){
			EnviroObject.transform.Translate(-100,0,0);
		}
	}
}
