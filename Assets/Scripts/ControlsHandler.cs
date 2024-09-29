using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ControlsHandler : MonoBehaviour {
	
	public static void turnLeft(){
		Movement.changeLaneLeft();
	}
	
	public static void turnRight(){
		Movement.changeLaneRight();
	}
	
	public static void releaseSteering(){
		Movement.releaseSteering();
	}
	
	public static void holdBrake(){
		Movement.holdBrake();
	}
	
	public static void releaseBrake(){
		Movement.releaseBrake();
	}
}
