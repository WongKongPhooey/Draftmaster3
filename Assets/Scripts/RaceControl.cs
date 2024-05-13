using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RaceControl : MonoBehaviour
{
	
	public static bool[] isWrecking = new bool[100];
	public static bool[] hasWrecked = new bool[100];
	public static bool[] hasBlownEngine = new bool[100];
	public static float[] carSpeed = new float[100];
	public static GameObject[] carTandem = new GameObject[100];
	public static int[] tandemPosition = new int[100];
	public static float[] receivedSpeed = new float[100];
	public static float[] givenSpeed = new float[100];
    
	// Start is called before the first frame update
    void Start(){
		for(int i=0;i<100;i++){
			isWrecking[i] = false;
			hasWrecked[i] = false;
			hasBlownEngine[i] = false;
		}
    }

    // Update is called once per frame
    void Update(){  
    }
	
	public static bool isCarDamaged(int carNum){
		if((isWrecking[carNum] == true)||
		  (hasWrecked[carNum] == true)||
		  (hasBlownEngine[carNum] == true)){
			//Debug.Log("Car #" + carNum + " is damaged");
			return true;
		}
		return false;
	}
	
	public static bool isCarTerminalDamaged(int carNum){
		if(hasBlownEngine[carNum] == true){
			//Debug.Log("Car #" + carNum + " is damaged");
			return true;
		}
		return false;
	}
	
	public static float getOpponentSpeed(int carNum){
		//Debug.Log("Opponent #" + carNum + " - " + carSpeed[carNum] + " (Race Control)");
		return carSpeed[carNum];
	}
}
