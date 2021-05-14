using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class MainMenuGUI : MonoBehaviour {

	public GUISkin eightBitSkin;
	public GUISkin buttonSkin;
	public GUISkin whiteGUI;
	public GUISkin redGUI;
	public GUISkin blueGUI;

	public Texture2D polyforgeLogo;
	
	float widthblock; 
	float heightblock;
	bool mainMenu;
	bool goRaceMenu;
	bool moreRaceMenu;
	bool settingsMenu;
	bool editorsMenu;
	string editorLabel;
	string versionLabel;

	int exp;
	int level;
	int levelExp;

	int totalMoney;

	public bool levelUpMenu;

	public bool proVersion;
	
	public AudioSource crowdNoise;
	public static int audioOn;

	public Texture2D gasCanTex;
	public Texture2D gearTex;
	
	void Awake(){

		proVersion = MiscScripts.proVersion;

		exp = PlayerPrefs.GetInt("Exp");
		level = PlayerPrefs.GetInt("Level");
		Debug.Log("Level " + level);
		levelExp = GameData.levelExp(level);
		
		if(exp > levelExp){
			exp-= levelExp;
			level++;
			PlayerPrefs.SetInt("Exp", exp);
			PlayerPrefs.SetInt("Level", level);
			Debug.Log("Level Up -> " + level);
			levelUpMenu = true;
		}
		
		totalMoney = PlayerPrefs.GetInt("PrizeMoney");
		
		widthblock = Mathf.Round(Screen.width/20);
		heightblock = Mathf.Round(Screen.height/20);
		RacePoints.championshipMode = false;

		mainMenu = true;
		goRaceMenu = false;
		moreRaceMenu = false;
		
		//Reset the game to imitate new users
		//PlayerPrefs.DeleteAll();
		
		if(!PlayerPrefs.HasKey("NewUser")){
			PlayerPrefs.SetInt("PrizeMoney",10000);
		}
		
		PlayerPrefs.SetInt("TutorialActive",0);
		PlayerPrefs.SetInt("CautionHasBeen",0);
		PlayerPrefs.SetInt("TotalRivals",0);

		ChallengeSelectGUI.challengeMode = false;
		PlayerPrefs.SetString("ChallengeType","");

		PlayerPrefs.SetInt("ActiveCaution",0);

		if(!PlayerPrefs.HasKey("TotalStarts")){
			PlayerPrefs.SetInt("TotalStarts", 0);
			PlayerPrefs.SetInt("TotalWins", 0);
			PlayerPrefs.SetInt("TotalLaps", 0);
			PlayerPrefs.SetInt("TotalChampionships", 0);
			PlayerPrefs.SetInt("TotalTop5s", 0);
		}
		
		if(!PlayerPrefs.HasKey("AudioOn")){
			PlayerPrefs.SetInt("AudioOn",1);
			audioOn = 1;
		} else {
			audioOn = PlayerPrefs.GetInt("AudioOn");
		}
		if(audioOn == 1){
			crowdNoise.volume = 0.10f;
		} else {
			crowdNoise.volume = 0.0f;
		}
		
	}

	void Update() {
	}

	void OnGUI() {
		
		GUI.skin = eightBitSkin;

		GUI.skin.label.fontSize = 108 / FontScale.fontScale;
		GUI.skin.label.alignment = TextAnchor.MiddleLeft;
		GUI.skin.label.normal.textColor = Color.black;
		GUI.skin.button.normal.textColor = Color.black;
		
		GUI.skin.button.fontSize = 64 / FontScale.fontScale;
		GUI.skin.button.alignment = TextAnchor.MiddleRight;
		GUI.skin.label.normal.textColor = Color.black;
		
		CommonGUI.TopBar();
		
		//All Cars
		GUI.skin.button.alignment = TextAnchor.MiddleCenter;
		if (GUI.Button(new Rect(Screen.width - (widthblock * 2.5f), Screen.height - (heightblock * 2.5f), widthblock * 2, heightblock * 2), "Cars")){
			SceneManager.LoadScene("AllCars");
		}
		
		if(levelUpMenu == true){
			GUI.Box(new Rect(0, 0, Screen.width, Screen.height),"");
			GUI.skin = whiteGUI;
			GUI.Box(new Rect(widthblock * 3f, heightblock * 3f, widthblock * 14f, heightblock * 14f),"");
			
			GUI.skin = buttonSkin;
			GUI.skin.label.alignment = TextAnchor.UpperCenter;
			GUI.skin.label.fontSize = 64 / FontScale.fontScale;
			GUI.Label(new Rect(widthblock * 3f, heightblock * 4f, widthblock * 14f, heightblock * 2f), "Level Up!");
			GUI.Label(new Rect(widthblock * 3f, heightblock * 7f, widthblock * 14f, heightblock * 2f), "+10 Gears");
			
			GUI.skin = blueGUI;
			if (GUI.Button(new Rect(widthblock * 8.75f, heightblock * 14f, widthblock * 2.5f, heightblock * 2f), "Continue")){
				levelUpMenu = false;
			}
		}
		
		if (Input.GetKeyDown(KeyCode.Escape)){
			Application.Quit();
		}
	}
}