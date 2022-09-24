using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class OptionsMenuGUI : MonoBehaviour {

	public GUISkin eightBitSkin;
	
	float widthblock; 
	float heightblock;

	public static int difficulty;
	public static string difficultyName;
	public static string difficultyDistance;
	public static int cameraRotate;
	public static int local2Player;
	public static int audioOn;
	public static int laneChange;
	public static int laneChangeSpeed;
	public static int laneChangeDuration;
	public static int raceLaps;
	public static int racePrize;
	public static int averageLaps;
	public static string audioOnName;
	public static string cameraRotateName;
	public static string local2PlayerName;
	public static string laneChangeName;
	public Vector2 scrollPosition = Vector2.zero;
	public float hSliderValue = 0.0F;

	public string cheatCode = "";

	void Awake(){
		
		FontScale.scale();
		
		widthblock = Screen.width/20;
		heightblock = Screen.height/20;
		difficulty = PlayerPrefs.GetInt("Difficulty");
		if(difficulty == 1){
			difficultyName = "Standard";
		} else {
			difficultyName = "Hard";
		}
		raceLaps = PlayerPrefs.GetInt("RaceLapsMultiplier");
		racePrize = PlayerPrefs.GetInt("RaceLapsMultiplier");
		averageLaps = PlayerPrefs.GetInt("RaceLapsMultiplier") * 8;
		laneChange = PlayerPrefs.GetInt("LaneChange");
		switch(laneChange){
		case 0:
			laneChangeName = "Slow";
			laneChangeDuration = 120;
			laneChangeSpeed = 10;
			break;
		case 1:
			laneChangeName = "Regular";
			laneChangeDuration = 80;
			laneChangeSpeed = 15;
			break;
		case 2:
			laneChangeName = "Fast";
			laneChangeDuration = 60;
			laneChangeSpeed = 20;
			break;
		case 3:
			laneChangeName = "Fastest";
			laneChangeDuration = 40;
			laneChangeSpeed = 30;
			break;
		}
		cameraRotate = PlayerPrefs.GetInt("CameraRotate");
		if(cameraRotate == 1){
			cameraRotateName = "Yes";
		} else {
			cameraRotate = 0;
			cameraRotateName = "No";
		}
		if(PlayerPrefs.HasKey("AudioOn")){
			audioOn = PlayerPrefs.GetInt("AudioOn");
		} else {
			audioOn = 1;
			PlayerPrefs.SetInt("AudioOn", audioOn);
		}
		if(audioOn == 1){
			audioOnName = "On";
		} else {
			audioOn = 0;
			audioOnName = "Off (Muted)";
		}
	}
	
	void OnGUI() {

		GUI.skin = eightBitSkin;

		GUI.skin.button.fontSize = 64 / FontScale.fontScale;

		GUI.skin.label.fontSize = 72 / FontScale.fontScale;

		GUI.skin.textField.fontSize = 72 / FontScale.fontScale;

		GUI.skin.label.normal.textColor = Color.black;

		GUI.skin.verticalScrollbar.fixedWidth = Screen.width / 20;
		GUI.skin.verticalScrollbarThumb.fixedWidth = Screen.width / 20;
		
		scrollPosition = GUI.BeginScrollView(new Rect(0, 0, Screen.width, Screen.height), scrollPosition, new Rect(0, 0, Screen.width - widthblock-10, Screen.height * 1.8f));

		GUI.skin.label.alignment = TextAnchor.UpperCenter;
		
		GUI.Label(new Rect(widthblock * 4, heightblock/2, widthblock * 12, heightblock * 2), "General Settings");

		GUI.skin.label.alignment = TextAnchor.UpperLeft;
		
		CommonGUI.BackButton("MainMenu");

		if (GUI.Button(new Rect(widthblock * 13, heightblock * 4, widthblock * 1, heightblock * 2), "<")){
			switch(audioOn){
			case 0:
				audioOn = 1;
				audioOnName = "On";
				break;
			case 1:
				audioOn = 0;
				audioOnName = "Off (Muted)";
				break;
			}
			PlayerPrefs.SetInt("AudioOn", audioOn);
		}
		if (GUI.Button(new Rect(widthblock * 14, heightblock * 4, widthblock * 1, heightblock * 2), ">")){
			switch(audioOn){
			case 0:
				audioOn = 1;
				audioOnName = "On";
				break;
			case 1:
				audioOn = 0;
				audioOnName = "Off (Muted)";
				break;
			}
			PlayerPrefs.SetInt("AudioOn", audioOn);
		}
		
		GUI.Label(new Rect(widthblock * 2, heightblock * 4, widthblock * 9, heightblock * 3), "Audio/SFX: " + audioOnName);
		

		if (GUI.Button(new Rect(widthblock * 13, heightblock * 8, widthblock * 1, heightblock * 2), "<")){
			switch(cameraRotate){
			case 0:
				cameraRotate = 1;
				cameraRotateName = "Yes";
				break;
			case 1:
				cameraRotate = 0;
				cameraRotateName = "No";
				break;
			}
			PlayerPrefs.SetInt("CameraRotate", cameraRotate);
		}
		if (GUI.Button(new Rect(widthblock * 14, heightblock * 8, widthblock * 1, heightblock * 2), ">")){
			switch(cameraRotate){
			case 0:
				cameraRotate = 1;
				cameraRotateName = "Yes";
				break;
			case 1:
				cameraRotate = 0;
				cameraRotateName = "No";
				break;
			}
			PlayerPrefs.SetInt("CameraRotate", cameraRotate);
		}
		GUI.Label(new Rect(widthblock * 2, heightblock * 8, widthblock * 9, heightblock * 3), "Camera Rotation: " + cameraRotateName);

		GUI.EndScrollView();

		if (Input.GetKeyDown(KeyCode.Escape)){
			SceneManager.LoadScene("MainMenu");
		}
	}
}
