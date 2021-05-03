using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class StatsMenuGUI : MonoBehaviour {

	public GUISkin eightBitSkin;
	
	float widthblock; 
	float heightblock;

	public Vector2 scrollPosition = Vector2.zero;
	public float hSliderValue = 0.0F;

	void Awake(){
		widthblock = Screen.width/20;
		heightblock = Screen.height/20;
	}
	
	void OnGUI() {

		GUI.skin = eightBitSkin;

		GUI.skin.button.fontSize = 64 / FontScale.fontScale;

		GUI.skin.label.fontSize = 88 / FontScale.fontScale;

		GUI.skin.textField.fontSize = 88 / FontScale.fontScale;

		GUI.skin.label.normal.textColor = Color.black;

		GUI.skin.verticalScrollbar.fixedWidth = Screen.width / 20;
		GUI.skin.verticalScrollbarThumb.fixedWidth = Screen.width / 20;
		
		scrollPosition = GUI.BeginScrollView(new Rect(0, 0, Screen.width, Screen.height), scrollPosition, new Rect(0, 0, Screen.width - widthblock-10, Screen.height * 2.2f));

		GUI.skin.label.alignment = TextAnchor.UpperCenter;
		
		GUI.Label(new Rect(widthblock * 4, heightblock/2, widthblock * 12, heightblock * 2), "My Statistics");
		
		if (GUI.Button(new Rect(widthblock * 16.5f, heightblock * 0.5f, widthblock * 1.5f, heightblock * 1.5f), "Back")){
			SceneManager.LoadScene("MainMenu");
		}
		
		GUI.skin.label.alignment = TextAnchor.UpperLeft;

		GUI.Label(new Rect(widthblock * 1, heightblock * 2, widthblock * 12, heightblock * 3), "Racing:");

		GUI.Label(new Rect(widthblock * 2, heightblock * 4, widthblock * 12, heightblock * 3), "Starts:");
		GUI.Label(new Rect(widthblock * 14, heightblock * 4, widthblock * 12, heightblock * 3), PlayerPrefs.GetInt("TotalStarts").ToString());

		GUI.Label(new Rect(widthblock * 2, heightblock * 6, widthblock * 12, heightblock * 3), "Championships:");
		GUI.Label(new Rect(widthblock * 14, heightblock * 6, widthblock * 12, heightblock * 3), PlayerPrefs.GetInt("TotalChampionships").ToString());

		GUI.Label(new Rect(widthblock * 2, heightblock * 8, widthblock * 12, heightblock * 3), "Wins:");
		GUI.Label(new Rect(widthblock * 14, heightblock * 8, widthblock * 12, heightblock * 3), PlayerPrefs.GetInt("TotalWins").ToString());

		GUI.Label(new Rect(widthblock * 2, heightblock * 10, widthblock * 12, heightblock * 3), "Top 5s:");
		GUI.Label(new Rect(widthblock * 14, heightblock * 10, widthblock * 12, heightblock * 3), PlayerPrefs.GetInt("TotalTop5s").ToString());

		GUI.Label(new Rect(widthblock * 2, heightblock * 12, widthblock * 12, heightblock * 3), "Laps Completed:");
		GUI.Label(new Rect(widthblock * 14, heightblock * 12, widthblock * 12, heightblock * 3), PlayerPrefs.GetInt("TotalLaps").ToString());

		GUI.Label(new Rect(widthblock * 1, heightblock * 15, widthblock * 12, heightblock * 3), "Challenges:");
		
		GUI.Label(new Rect(widthblock * 2, heightblock * 17, widthblock * 12, heightblock * 3), "Last To First Record:");
		GUI.Label(new Rect(widthblock * 14, heightblock * 17, widthblock * 12, heightblock * 3), PlayerPrefs.GetInt("LastToFirstRecord").ToString() + " Laps");

		GUI.Label(new Rect(widthblock * 2, heightblock * 19, widthblock * 12, heightblock * 3), "No Fuel Record:");
		GUI.Label(new Rect(widthblock * 14, heightblock * 19, widthblock * 12, heightblock * 3), PlayerPrefs.GetInt("NoFuelRecord").ToString() + MiscScripts.PositionPostfix(PlayerPrefs.GetInt("NoFuelRecord")));

		GUI.Label(new Rect(widthblock * 2, heightblock * 21, widthblock * 12, heightblock * 3), "Late Push Record:");
		GUI.Label(new Rect(widthblock * 14, heightblock * 21, widthblock * 12, heightblock * 3), PlayerPrefs.GetInt("LatePushRecord").ToString() + MiscScripts.PositionPostfix(PlayerPrefs.GetInt("LatePushRecord")));

		GUI.Label(new Rect(widthblock * 2, heightblock * 23, widthblock * 12, heightblock * 3), "Team Player Record:");
		GUI.Label(new Rect(widthblock * 14, heightblock * 23, widthblock * 12, heightblock * 3), PlayerPrefs.GetInt("TeamPlayerRecord").ToString() + "%");

		GUI.Label(new Rect(widthblock * 2, heightblock * 25, widthblock * 12, heightblock * 3), "Clean Break Record:");
		GUI.Label(new Rect(widthblock * 14, heightblock * 25, widthblock * 12, heightblock * 3), PlayerPrefs.GetInt("CleanBreakRecord").ToString() + "ms");
		
		GUI.Label(new Rect(widthblock * 2, heightblock * 27, widthblock * 12, heightblock * 3), "Traffic Jam Record:");
		GUI.Label(new Rect(widthblock * 14, heightblock * 27, widthblock * 12, heightblock * 3), PlayerPrefs.GetInt("TrafficJamRecord").ToString() + " Laps");

		GUI.Label(new Rect(widthblock * 2, heightblock * 29, widthblock * 12, heightblock * 3), "Photo Finish Record:");
		GUI.Label(new Rect(widthblock * 14, heightblock * 29, widthblock * 12, heightblock * 3), PlayerPrefs.GetInt("PhotoFinishRecord").ToString() + "ms");
		
		GUI.Label(new Rect(widthblock * 2, heightblock * 31, widthblock * 12, heightblock * 3), "All Wound Up Record:");
		GUI.Label(new Rect(widthblock * 14, heightblock * 31, widthblock * 12, heightblock * 3), PlayerPrefs.GetInt("AllWoundUpRecord").ToString() + " Rivals");

		GUI.Label(new Rect(widthblock * 1, heightblock * 34, widthblock * 12, heightblock * 3), "Vehicles:");

		GUI.EndScrollView();

		if (Input.GetKeyDown(KeyCode.Escape)){
			SceneManager.LoadScene("MainMenu");
		}
	}
}
