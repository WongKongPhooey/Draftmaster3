using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class RaceResultsExit : MonoBehaviour {
	
	float heightblock = Screen.height/20;

	public GUISkin eightBitSkin;

	int loadCounter;

	void OnEnable(){
		loadCounter = 0;
		GetComponent<RaceResultsGUI>().enabled = false;
		if(RacePoints.championshipMode == true){
			SceneManager.LoadScene("ChampionshipResults");
		}
	}

	void Update(){
		loadCounter+=2;
		if(loadCounter >= 100){
			SceneManager.LoadScene("AdvertDoubleMoney");
		}
	}

	void OnGUI(){
		GUI.skin = eightBitSkin;
		GUI.skin.label.normal.textColor = Color.black;
		GUI.skin.label.fontSize = 128 / FontScale.fontScale;
		GUI.skin.label.alignment = TextAnchor.MiddleCenter;
		GUI.Label(new Rect(0, heightblock * 8,Screen.width, heightblock * 4), "Loading.. " + loadCounter + "%");
	}
}
