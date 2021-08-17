using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraPan : MonoBehaviour {
	
	float speed = 0.8f;
	
    // Start is called before the first frame update
    void Start(){}

    void Update() {
         if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Moved) {
             Vector2 touchDeltaPosition = Input.GetTouch(0).deltaPosition;
              transform.Translate(-touchDeltaPosition.x * speed * Time.deltaTime, -touchDeltaPosition.y * speed * Time.deltaTime, 0);
			  if(transform.position.z > 9f){
				  transform.position = new Vector3(transform.position.x, transform.position.y, 9f);
			  }
			  if(transform.position.z < -6f){
				  transform.position = new Vector3(transform.position.x, transform.position.y, -6f);
			  }
			  if(transform.position.x > 3f){
				  transform.position = new Vector3(3f, transform.position.y, transform.position.z);
			  }
			  if(transform.position.x < -9f){
				  transform.position = new Vector3(-9, transform.position.y, transform.position.z);
			  }
         }
    }
}
