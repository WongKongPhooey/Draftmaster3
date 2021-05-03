using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class ChampionshipHubExit : MonoBehaviour {

	float widthblock = Screen.width/20;
	float heightblock = Screen.height/20;

	public GUISkin eightBitSkin;

	public GameObject circuit;

	int loadCounter;

	string nextCircuit;

	void OnEnable(){
		loadCounter = 0;
		GetComponent<ChampionshipHubGUI>().enabled = false;
		circuit.GetComponent<Renderer>().enabled = false;
	}

	void Update(){
		loadCounter+=2;
		if(loadCounter >= 100){
			nextCircuit = PlayerPrefs.GetString("nextCircuit");
			SceneManager.LoadScene(nextCircuit);
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
