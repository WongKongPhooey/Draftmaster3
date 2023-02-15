using UnityEngine;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;

public class RaceHUD : MonoBehaviour {

	public float widthblock;
	public float heightblock;
	public static bool gamePaused;

	public GameObject raceCam;

	public static int fontScale;

	public static bool caution = false;
	public static bool goingGreen = false;
	public static bool racePreover = false;
	public static bool raceOver = false;
	public static bool challengeActive;
	
	public GUISkin eightBitSkin;
	public GUISkin redGUI;
	public GameObject thePlayer;
	public int player2Num;

	public float engineVol;
	public float crowdVol;
	public static int tutorialStage = 1;
	public static int tutorialSteeringCount;
	public static int tutorialBrakingCount;
	public static int tutorialDraftingCount;
	public static int tutorialBackdraftCount;
	public static int tutorialSteeringMax = 3;
	public static int tutorialBrakingMax = 1;
	public static int tutorialDraftingMax = 500;
	public static int tutorialBackdraftMax = 100;
	public static bool tutorialTopSpeed = false;
	public static bool tutorialBackingOut = false;
	
	
	void Start(){
		widthblock = Screen.width/20;
		heightblock = Screen.height/20;
		thePlayer = GameObject.Find("Player");
		tutorialSteeringCount = 0;
		tutorialBrakingCount = 0;
		tutorialDraftingCount = 0;
		tutorialBackdraftCount = 0;
		gamePaused = false;
		tutorialBackingOut = false;
		
		challengeActive = false;
		if(PlayerPrefs.HasKey("RaceMoment")){
			challengeActive = true;
		}
	}
	
	
	void Awake(){
		tutorialStage = 1;
		gamePaused = false;
		tutorialBackingOut = false;
		
		fontScale = FontScale.scale();
	}
	
	void FixedUpdate(){
		if (tutorialSteeringCount >= tutorialSteeringMax){
			tutorialStage++;
			tutorialSteeringCount = 0;
		}
		if (tutorialBrakingCount >= tutorialBrakingMax){
			tutorialStage++;
			tutorialBrakingCount = 0;
		}
		if (tutorialDraftingCount >= tutorialDraftingMax){
			tutorialStage++;
			tutorialDraftingCount = 0;
		}
		if (tutorialBackdraftCount >= tutorialBackdraftMax){
			tutorialStage++;
			tutorialBackdraftCount = 0;
		}
		if (tutorialStage == 5){
			Time.timeScale = 0.0f;
			raceCam.GetComponent<AudioListener>().enabled = false;
			PlayerPrefs.SetInt("Volume",0);
		}
	}
	
	void OnGUI() {

		if(fontScale == 0){
			fontScale = FontScale.scale();
			SceneManager.LoadScene("MainMenu");
		}
		
		GUI.skin = eightBitSkin;
		GUI.skin.label.fontSize = 80 / 2;
		GUI.skin.button.fontSize = 120 / 2;

		if((raceOver == false)&&(caution == false)){
			//Do nothing
		} else {
			if(challengeActive == false){
				if((raceOver == true)||(racePreover == true)){
					GUI.skin.label.fontSize = 512 / fontScale;
					GUI.skin.label.alignment = TextAnchor.LowerLeft;
					GUI.skin.label.normal.textColor = Color.black;
					GUI.Label(new Rect(widthblock + (8 / (fontScale * 2)), Screen.height - (heightblock * 9) + (8 / (fontScale * 2)), widthblock * 12, heightblock * 8), "P" + (Ticker.position + 1));
					GUI.Label(new Rect(widthblock - (8 / (fontScale * 2)), Screen.height - (heightblock * 9) + (8 / (fontScale * 2)), widthblock * 12, heightblock * 8), "P" + (Ticker.position + 1));
					GUI.Label(new Rect(widthblock + (8 / (fontScale * 2)), Screen.height - (heightblock * 9) - (8 / (fontScale * 2)), widthblock * 12, heightblock * 8), "P" + (Ticker.position + 1));
					GUI.Label(new Rect(widthblock - (8 / (fontScale * 2)), Screen.height - (heightblock * 9) - (8 / (fontScale * 2)), widthblock * 12, heightblock * 8), "P" + (Ticker.position + 1));
					GUI.skin.label.normal.textColor = Color.yellow;
					GUI.Label(new Rect(widthblock , Screen.height - (heightblock * 9), widthblock * 12, heightblock * 8), "P" + (Ticker.position + 1));
				}
				GUI.skin.label.fontSize = 80 / fontScale;
					
				GUI.skin = redGUI;
				GUI.skin.button.fontSize = 120 / fontScale;
				if (GUI.Button(new Rect(widthblock * 13, Screen.height - (heightblock * 7), widthblock * 6, heightblock * 4), "Results")){
					Time.timeScale = 1.0f;
					raceOver = false;
					SceneManager.LoadScene("Menus/RaceResults");
				}
			}
		}
	}
}
