using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class TutorialGUI : MonoBehaviour {

	public GUISkin eightBitSkin;

	float widthblock = Mathf.Round(Screen.width/20);
	float heightblock = Mathf.Round(Screen.height/20);

	public string firstName = "New";
	public string lastName = "Racer";
	
	void Awake() {
		RaceHUD.tutorialStage = 1;
	}

	// Use this for initialization
	void OnGUI() {

		GUI.skin = eightBitSkin;

		GUI.skin.label.fontSize = 72 / FontScale.fontScale;
		
		GUI.skin.button.fontSize = 64 / FontScale.fontScale;

		GUI.skin.textField.fontSize = 72 / FontScale.fontScale;

		GUI.skin.label.normal.textColor = Color.black;

		GUI.skin.label.alignment = TextAnchor.MiddleCenter;

		GUI.Label(new Rect(widthblock * 4, heightblock, widthblock * 12, heightblock * 3), "First time on DraftMaster?");
		
		Vector2 pivotPoint = new Vector2(widthblock * 8, heightblock * 9);
		GUIUtility.RotateAroundPivot(-90, pivotPoint);
		GUI.DrawTexture(new Rect(widthblock * 8, heightblock * 9, widthblock * 2, widthblock * 4), Resources.Load("livery3") as Texture);
		GUIUtility.RotateAroundPivot(90, pivotPoint);
		
		GUI.Label(new Rect(widthblock * 2, heightblock * 11, widthblock * 16, heightblock * 3), "Learn the ropes at the wheel of the iconic Earnhardt #3 machine!");

		GUI.skin.label.alignment = TextAnchor.MiddleRight;

		if (GUI.Button(new Rect(widthblock * 3,heightblock * 16, widthblock * 6, heightblock * 2), "Play Tutorial")){
			PlayerPrefs.SetInt("TutorialActive",1);
			PlayerPrefs.SetInt("RaceLaps",200);
			PlayerPrefs.SetInt("StartingLap",1);
			PlayerPrefs.SetInt("CircuitLanes",4);
			PlayerPrefs.SetInt("StraightLength1",200);
			PlayerPrefs.SetInt("StraightLength2",500);
			PlayerPrefs.SetInt("StraightLength3",200);
			PlayerPrefs.SetInt("StraightLength4",0);
			PlayerPrefs.SetInt("TurnLength1",150);
			PlayerPrefs.SetInt("TurnLength2",150);
			PlayerPrefs.SetInt("TurnLength3",60);
			PlayerPrefs.SetInt("TurnLength4",0);
			PlayerPrefs.SetInt("TurnAngle1",4);
			PlayerPrefs.SetInt("TurnAngle2",4);
			PlayerPrefs.SetInt("TurnAngle3",8);
			PlayerPrefs.SetInt("TurnAngle4",0);
			PlayerPrefs.SetInt("StartLine",50);
			PlayerPrefs.SetInt("TotalTurns",3);
			PlayerPrefs.SetString("raceSeries", "StockCar");
			PlayerPrefs.SetString("carTexture", "livery3");
			PlayerPrefs.SetInt("CarChoice",3);
			SceneManager.LoadScene("ThunderAlley");
		}
		if (GUI.Button(new Rect(widthblock * 11,heightblock * 16, widthblock * 6, heightblock * 2), "Skip Tutorial")){
			PlayerPrefs.SetInt("TutorialActive",0);
			SceneManager.LoadScene("MainMenu");
		}
	}
}
