using UnityEngine;
using System.Collections;

public class DriverPoints : MonoBehaviour {
	
	public static int[] pointsTotal = new int[100];
	
	// Use this for initialization
	void Start () {		
	}

	public static void resetPoints() {
		
		for(int i = 0; i < 100;i++){
			PlayerPrefs.SetInt("ChampionshipPoints"  + i,0);
			pointsTotal[i] = 0;
		}
	}
}
