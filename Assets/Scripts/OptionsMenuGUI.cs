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
		local2Player = PlayerPrefs.GetInt("Local2Player");
		if(local2Player == 1){
			local2PlayerName = "Enabled";
		} else {
			local2Player = 0;
			local2PlayerName = "Disabled";
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
		
		if (GUI.Button(new Rect(widthblock * 16.5f, heightblock * 0.5f, widthblock * 1.5f, heightblock * 1.5f), "Back")){
			SceneManager.LoadScene("MainMenu");
		}

		if (GUI.Button(new Rect(widthblock * 11, heightblock * 4, widthblock * 5, heightblock * 2), "Change Name")){
			SceneManager.LoadScene("ChooseName");
		}

		GUI.Label(new Rect(widthblock * 2, heightblock * 4, widthblock * 9, heightblock * 3), "Driver: " + PlayerPrefs.GetString("RacerName"));

		if (GUI.Button(new Rect(widthblock * 11, heightblock * 8, widthblock * 1, heightblock * 2), "<")){
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
		if (GUI.Button(new Rect(widthblock * 12, heightblock * 8, widthblock * 1, heightblock * 2), ">")){
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
		
		GUI.Label(new Rect(widthblock * 2, heightblock * 8, widthblock * 9, heightblock * 3), "Audio/SFX: " + audioOnName);

		if (GUI.Button(new Rect(widthblock * 11, heightblock * 12, widthblock * 1, heightblock * 2), "<")){
			switch(difficultyName){
			case "Standard":
				difficulty = 2;
				difficultyName = "Hard";
				break;
			case "Hard":
				difficulty = 1;
				difficultyName = "Standard";
				break;
			}
			PlayerPrefs.SetInt("Difficulty", difficulty);
		}
		
		if (GUI.Button(new Rect(widthblock * 12, heightblock * 12, widthblock * 1, heightblock * 2), ">")){
			switch(difficultyName){
			case "Standard":
				difficulty = 2;
				difficultyName = "Hard";
				break;
			case "Hard":
				difficulty = 1;
				difficultyName = "Standard";
				break;
			}
			PlayerPrefs.SetInt("Difficulty", difficulty);
		}

		GUI.Label(new Rect(widthblock * 2, heightblock * 12, widthblock * 9, heightblock * 3), "Difficulty: " + difficultyName);

		if (GUI.Button(new Rect(widthblock * 11, heightblock * 16, widthblock * 1, heightblock * 2), "<")){
			if(raceLaps > 1){
				raceLaps /= 2;
				racePrize /= 2;
				averageLaps /= 2;
			} else {
				raceLaps = 8;
				racePrize = 8;
				averageLaps = 64;
			}
			PlayerPrefs.SetInt("RaceLapsMultiplier", raceLaps);
		}
		if (GUI.Button(new Rect(widthblock * 12, heightblock * 16, widthblock * 1, heightblock * 2), ">")){
			if(raceLaps < 5){
				raceLaps *= 2;
				racePrize *= 2;
				averageLaps *= 2;
			} else {
				raceLaps = 1;
				racePrize = 1;
				averageLaps = 8;
			}
			PlayerPrefs.SetInt("RaceLapsMultiplier", raceLaps);
		}
		
		GUI.Label(new Rect(widthblock * 2, heightblock * 16, widthblock * 9, heightblock * 3), "Laps: X" + raceLaps.ToString() + " (Avg. " + averageLaps.ToString() + ")");

		if (GUI.Button(new Rect(widthblock * 11, heightblock * 20, widthblock * 1, heightblock * 2), "<")){
			switch(laneChange){
			case 0:
				laneChange = 3;
				laneChangeName = "Fastest";
				laneChangeDuration = 40;
				laneChangeSpeed = 30;
				break;
			case 1:
				laneChange = 0;
				laneChangeName = "Slow";
				laneChangeDuration = 120;
				laneChangeSpeed = 10;
				break;
			case 2:
				laneChange = 1;
				laneChangeName = "Regular";
				laneChangeDuration = 80;
				laneChangeSpeed = 15;
				break;
			case 3:
				laneChange = 2;
				laneChangeName = "Fast";
				laneChangeDuration = 60;
				laneChangeSpeed = 20;
				break;
			}
			PlayerPrefs.SetInt("LaneChange", laneChange);
			PlayerPrefs.SetInt("LaneChangeSpeed", laneChangeSpeed);
			PlayerPrefs.SetInt("LaneChangeDuration", laneChangeDuration);
		}
		if (GUI.Button(new Rect(widthblock * 12, heightblock * 20, widthblock * 1, heightblock * 2), ">")){
			switch(laneChange){
			case 0:
				laneChange = 1;
				laneChangeName = "Regular";
				laneChangeDuration = 80;
				laneChangeSpeed = 15;
				break;
			case 1:
				laneChange = 2;
				laneChangeName = "Fast";
				laneChangeDuration = 60;
				laneChangeSpeed = 20;
				break;
			case 2:
				laneChange = 3;
				laneChangeName = "Fastest";
				laneChangeDuration = 40;
				laneChangeSpeed = 30;
				break;
			case 3:
				laneChange = 0;
				laneChangeName = "Slow";
				laneChangeDuration = 120;
				laneChangeSpeed = 10;
				break;
			}
			PlayerPrefs.SetInt("LaneChange", laneChange);
			PlayerPrefs.SetInt("LaneChangeSpeed", laneChangeSpeed);
			PlayerPrefs.SetInt("LaneChangeDuration", laneChangeDuration);
		}
		
		GUI.Label(new Rect(widthblock * 2, heightblock * 20, widthblock * 9, heightblock * 3), "Lane Changes: " + laneChangeName);

		if (GUI.Button(new Rect(widthblock * 11, heightblock * 24, widthblock * 1, heightblock * 2), "<")){
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
		if (GUI.Button(new Rect(widthblock * 12, heightblock * 24, widthblock * 1, heightblock * 2), ">")){
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
		
		GUI.Label(new Rect(widthblock * 2, heightblock * 24, widthblock * 9, heightblock * 3), "Camera Rotation: " + cameraRotateName);
		
		if (GUI.Button(new Rect(widthblock * 11, heightblock * 28, widthblock * 1, heightblock * 2), "<")){
			switch(local2Player){
			case 0:
				local2Player = 1;
				local2PlayerName = "Enabled";
				break;
			case 1:
				local2Player = 0;
				local2PlayerName = "Disabled";
				break;
			}
			PlayerPrefs.SetInt("Local2Player", local2Player);
		}
		if (GUI.Button(new Rect(widthblock * 12, heightblock * 28, widthblock * 1, heightblock * 2), ">")){
			switch(local2Player){
			case 0:
				local2Player = 1;
				local2PlayerName = "Enabled";
				break;
			case 1:
				local2Player = 0;
				local2PlayerName = "Disabled";
				break;
			}
			PlayerPrefs.SetInt("Local2Player", local2Player);
		}
		
		GUI.Label(new Rect(widthblock * 2, heightblock * 28, widthblock * 9, heightblock * 3), "Local 2 Player: " + local2PlayerName);

		cheatCode = GUI.TextField(new Rect(widthblock * 11, heightblock * 32, widthblock * 3.5f, heightblock * 2), cheatCode, 30);

		if (GUI.Button(new Rect(widthblock * 14.5f, heightblock * 32, widthblock * 1.5f, heightblock * 2), "Go!")){
			MiscScripts.CheatCodes(cheatCode);
		}

		GUI.Label(new Rect(widthblock * 2, heightblock * 32, widthblock * 9, heightblock * 3), "Test Codes");


		GUI.EndScrollView();

		if (Input.GetKeyDown(KeyCode.Escape)){
			SceneManager.LoadScene("MainMenu");
		}
	}
}
