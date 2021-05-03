using UnityEngine;
using System.Collections;

public class SceneryMovement : MonoBehaviour {

	public GameObject SceneryObject;

	public bool straightsOnly;
	public bool cornersOnly;
	public bool oneStraight;
	public bool insideOfCircuit;
	public int straight;

	// Update is called once per frame
	void FixedUpdate () {

		if(straightsOnly == true){
			if(CameraRotate.cornercounter != 0){
				this.GetComponent<Renderer>().enabled = false;
			} else {
				this.GetComponent<Renderer>().enabled = true;
			}
		}

		if(oneStraight == true){
			if((CameraRotate.straight != straight)||(CameraRotate.cornercounter != 0)){
				this.GetComponent<Renderer>().enabled = false;
			} else {
				this.GetComponent<Renderer>().enabled = true;
			}
		}

		SceneryObject.transform.Translate(0,0,-3);
		
		if (SceneryObject.transform.position.z < -40){
			SceneryObject.transform.Translate(0,0,80);
		}
	}
}
