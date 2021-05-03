using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class DraftSettingsMenuGUI : MonoBehaviour {

	public GUISkin eightBitSkin;
	
	float widthblock; 
	float heightblock;

	public static int difficulty;
	public static string difficultyName;
	public static string difficultyDistance;
	public static int cameraRotate;
	public static int raceLaps;
	public static int racePrize;
	public static int averageLaps;
	public static string cameraRotateName;
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
		cameraRotate = PlayerPrefs.GetInt("CameraRotate");
		if(cameraRotate == 1){
			cameraRotateName = "Yes";
		} else {
			cameraRotate = 0;
			cameraRotateName = "No";
		}
		if(!PlayerPrefs.HasKey("CustomDraftDistance")){
			PlayerPrefs.SetInt("CustomDraftDistance",50);
		}
		if(!PlayerPrefs.HasKey("CustomDraftStrength")){
			PlayerPrefs.SetInt("CustomDraftStrength",50);
		}
		if(!PlayerPrefs.HasKey("CustomMaxSpeed")){
			PlayerPrefs.SetInt("CustomMaxSpeed",50);
		}
		if(!PlayerPrefs.HasKey("CustomAcceleration")){
			PlayerPrefs.SetInt("CustomAcceleration",50);
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
		
		scrollPosition = GUI.BeginScrollView(new Rect(0, 0, Screen.width, Screen.height), scrollPosition, new Rect(0, 0, Screen.width - widthblock-10, Screen.height * 1.2f));

		GUI.skin.label.alignment = TextAnchor.UpperCenter;
		
		GUI.Label(new Rect(widthblock * 4, heightblock/2, widthblock * 12, heightblock * 2), "Draft Settings");

		GUI.skin.label.alignment = TextAnchor.UpperLeft;

		if (GUI.Button(new Rect(widthblock * 16.5f, heightblock * 0.5f, widthblock * 1.5f, heightblock * 1.5f), "Back")){
			SceneManager.LoadScene("MainMenu");
		}
		
		//---------------DRAFT DISTANCE------------------//

		GUI.Label(new Rect(widthblock * 11, heightblock * 4, widthblock * 4, heightblock * 2), PlayerPrefs.GetInt("CustomDraftDistance").ToString());
		if(PlayerPrefs.GetInt("CustomDraftDistance") < 99){
			if (GUI.Button(new Rect(widthblock * 13, heightblock * 4, widthblock * 1, heightblock * 2), "+")){
				PlayerPrefs.SetInt("CustomDraftDistance",PlayerPrefs.GetInt("CustomDraftDistance") + 1);
			}
		}
		if(PlayerPrefs.GetInt("CustomDraftDistance") > 1){
			if (GUI.Button(new Rect(widthblock * 14, heightblock * 4, widthblock * 1, heightblock * 2), "-")){
				PlayerPrefs.SetInt("CustomDraftDistance",PlayerPrefs.GetInt("CustomDraftDistance") - 1);
			}
		}
		GUI.Label(new Rect(widthblock * 2, heightblock * 4, widthblock * 8, heightblock * 3), "Draft Distance");

		//---------------DRAFT STRENGTH------------------//

		GUI.Label(new Rect(widthblock * 11, heightblock * 8, widthblock * 4, heightblock * 2), PlayerPrefs.GetInt("CustomDraftStrength").ToString());
		if(PlayerPrefs.GetInt("CustomDraftStrength") < 99){
			if (GUI.Button(new Rect(widthblock * 13, heightblock * 8, widthblock * 1, heightblock * 2), "+")){
				PlayerPrefs.SetInt("CustomDraftStrength",PlayerPrefs.GetInt("CustomDraftStrength") + 1);
			}
		}
		if(PlayerPrefs.GetInt("CustomDraftStrength") > 1){
			if (GUI.Button(new Rect(widthblock * 14, heightblock * 8, widthblock * 1, heightblock * 2), "-")){
				PlayerPrefs.SetInt("CustomDraftStrength",PlayerPrefs.GetInt("CustomDraftStrength") - 1);
			}
		}
		GUI.Label(new Rect(widthblock * 2, heightblock * 8, widthblock * 8, heightblock * 3), "Draft Strength");

		//---------------TOP SPEED------------------//
		
		GUI.Label(new Rect(widthblock * 11, heightblock * 12, widthblock * 4, heightblock * 2), PlayerPrefs.GetInt("CustomMaxSpeed").ToString());
		if(PlayerPrefs.GetInt("CustomMaxSpeed") < 99){
			if (GUI.Button(new Rect(widthblock * 13, heightblock * 12, widthblock * 1, heightblock * 2), "+")){
				PlayerPrefs.SetInt("CustomMaxSpeed",PlayerPrefs.GetInt("CustomMaxSpeed") + 1);
			}
		}
		if(PlayerPrefs.GetInt("CustomMaxSpeed") > 1){
			if (GUI.Button(new Rect(widthblock * 14, heightblock * 12, widthblock * 1, heightblock * 2), "-")){
				PlayerPrefs.SetInt("CustomMaxSpeed",PlayerPrefs.GetInt("CustomMaxSpeed") - 1);
			}
		}
		GUI.Label(new Rect(widthblock * 2, heightblock * 12, widthblock * 8, heightblock * 3), "Max Speed");

		//---------------ACCELERATION------------------//
		
		GUI.Label(new Rect(widthblock * 11, heightblock * 16, widthblock * 4, heightblock * 2), PlayerPrefs.GetInt("CustomAcceleration").ToString());
		if(PlayerPrefs.GetInt("CustomAcceleration") < 99){
			if (GUI.Button(new Rect(widthblock * 13, heightblock * 16, widthblock * 1, heightblock * 2), "+")){
				PlayerPrefs.SetInt("CustomAcceleration",PlayerPrefs.GetInt("CustomAcceleration") + 1);
			}
		}
		if(PlayerPrefs.GetInt("CustomAcceleration") > 1){
			if (GUI.Button(new Rect(widthblock * 14, heightblock * 16, widthblock * 1, heightblock * 2), "-")){
				PlayerPrefs.SetInt("CustomAcceleration",PlayerPrefs.GetInt("CustomAcceleration") - 1);
			}
		}
		GUI.Label(new Rect(widthblock * 2, heightblock * 16, widthblock * 8, heightblock * 3), "Acceleration");

		//

		if (GUI.Button(new Rect(widthblock * 2, heightblock * 20, widthblock * 5, heightblock * 2), "Reset All")){
			PlayerPrefs.SetInt("CustomDraftDistance",50);
			PlayerPrefs.SetInt("CustomDraftStrength",50);
			PlayerPrefs.SetInt("CustomMaxSpeed",50);
			PlayerPrefs.SetInt("CustomAcceleration",50);
		}

		GUI.EndScrollView();

		if (Input.GetKeyDown(KeyCode.Escape)){
			SceneManager.LoadScene("MainMenu");
		}
	}
}
