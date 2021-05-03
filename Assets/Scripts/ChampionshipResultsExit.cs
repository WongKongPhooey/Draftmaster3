using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class ChampionshipResultsExit : MonoBehaviour {

	float widthblock = Screen.width/20;
	float heightblock = Screen.height/20;

	public GUISkin eightBitSkin;

	int loadCounter;

	void OnEnable(){
		loadCounter = 0;
		GetComponent<ChampionshipResultsGUI>().enabled = false;
	}

	void Update(){
		loadCounter+=2;
		if(loadCounter >= 100){
			SceneManager.LoadScene("MainMenu");
		}
	}

	void OnGUI(){
		GUI.skin = eightBitSkin;
		GUI.skin.label.normal.textColor = Color.black;
		GUI.skin.label.fontSize = 128 / FontScale.fontScale;
		GUI.skin.label.alignment = TextAnchor.MiddleCenter;
		GUI.Label(new Rect(widthblock, heightblock * 8, widthblock * 20, heightblock * 4), "Loading.. " + loadCounter + "%");
	}
}
