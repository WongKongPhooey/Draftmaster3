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
         }
    }
}
