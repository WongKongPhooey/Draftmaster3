using UnityEngine;
using System.Collections;

public class EnviroMovement : MonoBehaviour {

	public GameObject EnviroObject;

	void Awake(){
	}

	// Update is called once per frame
	void FixedUpdate () {
		EnviroObject.transform.Translate(0,0,-3);
		
		if (EnviroObject.transform.position.z < -40){
			EnviroObject.transform.Translate(0,0,80);
		}
	}
}
