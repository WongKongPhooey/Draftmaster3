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
	
	public Texture2D spannerTex;
	
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
	
	int dayInterval;
	
	string rewardString;

	int totalMoney;

	public bool levelUpMenu;
	public static bool newMessageAlert;
	public static string messageAlert;
	
	public static bool newGiftAlert;
	public static string giftAlert;
	
	public static bool showInfoBox;
	public static string infoBox;
	
	public AudioSource crowdNoise;
	public static int audioOn;

	public Texture2D gasCanTex;
	public Texture2D gearTex;
	
	void Awake(){

		//Hard Redirect Away Fallback
		SceneManager.LoadScene("Menus/MainMenu");

		exp = PlayerPrefs.GetInt("Exp");
		//exp = 10000;
		level = PlayerPrefs.GetInt("Level");
		//Debug.Log("Level " + level);
		levelExp = GameData.levelExp(level);
		
		dayInterval = 86400;
		
		rewardString = "";
		
		newMessageAlert = false;
		newGiftAlert = false;
		showInfoBox = false;
		
		if(PlayerPrefs.HasKey("ActivePath")){
			PlayerPrefs.DeleteKey("ActivePath");
		}
		PlayerPrefs.DeleteKey("RaceModifier");
		
		if(exp > levelExp){
			exp-= levelExp;
			level++;
			PlayerPrefs.SetInt("Exp", exp);
			PlayerPrefs.SetInt("Level", level);
			
			GameData.setRewards();
			rewardString = GameData.levelUpReward(level);
			
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
		
		//PlayerPrefs.SetInt("cup2018Gears", 100);
		
		if(PlayerPrefs.HasKey("PlayerUsername")){
			PlayFabManager.LoginFromPrefs();
		}
		
		//Reset the game to imitate new users
		//PlayerPrefs.DeleteAll();
		
		if(!PlayerPrefs.HasKey("NewUser")){
			PlayerPrefs.SetInt("PrizeMoney",10000);
			firstTimeInit();
		}
		
		PlayerPrefs.DeleteKey("FinishPos");
		PlayerPrefs.DeleteKey("FixedSeries");
		PlayerPrefs.DeleteKey("CustomCar");
		PlayerPrefs.DeleteKey("CustomField");
		PlayerPrefs.SetInt("TutorialActive",0);
		PlayerPrefs.SetInt("CautionHasBeen",0);
		PlayerPrefs.SetInt("TotalRivals",0);
		PlayerPrefs.SetInt("ExpAdded",0);

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
			crowdNoise.volume = 0.25f;
		} else {
			crowdNoise.volume = 0.0f;
		}
	}

	void Update() {
	}
	
	void firstTimeInit(){
		PlayerPrefs.SetInt("CameraRotate", 1);
		PlayerPrefs.SetInt("TransferTokens", 1);
		PlayerPrefs.SetInt("TransfersLeft", 1);
		PlayerPrefs.SetString("TargetVersion", Application.version);
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
		
		CommonGUI.SocialBar();
		
		//Level Progress Bar Box
		GUI.Box(new Rect(widthblock * 0.5f, (heightblock * 1.5f) + 15, widthblock * 2f, 5), "");
		//Progress Bar
		GUI.skin = blueGUI;
		if((((widthblock * 2f)/levelExp) * exp) >= (widthblock * 2f)){
			GUI.Box(new Rect(widthblock * 0.5f, (heightblock * 1.5f) + 15, widthblock * 2f, 5), "");
		} else {
			GUI.Box(new Rect(widthblock * 0.5f, (heightblock * 1.5f) + 15, (((widthblock * 2f)/levelExp) * exp) + 1, 5), "");
		}
		GUI.skin = eightBitSkin;
		
		//Day Cycle Bar Box
		GUI.Box(new Rect(widthblock * 3f, (heightblock * 1.5f) + 15, widthblock * 3f, 5), "");
		//Progress Bar
		GUI.skin = blueGUI;
		GUI.Box(new Rect(widthblock * 3f, (heightblock * 1.5f) + 15, Mathf.Floor((((widthblock * 3f)/dayInterval) * PlayerPrefs.GetInt("SpareTime"))) + 1, 5), "");
		GUI.skin = eightBitSkin;
		
		GUI.skin.button.alignment = TextAnchor.MiddleCenter;
		
		if(PlayerPrefs.HasKey("PlayerUsername")){
			
			if (GUI.Button(new Rect(widthblock * 0.5f, Screen.height - (heightblock * 1.5f) - 20, widthblock * 4.5f, heightblock * 1.5f), PlayerPrefs.GetString("PlayerUsername"))){
				SceneManager.LoadScene("MyAccount");
			}
		} else {
			if (GUI.Button(new Rect(widthblock * 0.5f, Screen.height - (heightblock * 1.5f) - 20, widthblock * 2, heightblock * 1.5f), "Login")){
				SceneManager.LoadScene("LoginRegister");
			}
		}
		
		if(levelUpMenu == true){
			GUI.Box(new Rect(0, 0, Screen.width, Screen.height),"");
			GUI.skin = whiteGUI;
			GUI.Box(new Rect(widthblock * 3f, heightblock * 3f, widthblock * 14f, heightblock * 14f),"");
			
			GUI.skin = buttonSkin;
			GUI.skin.label.alignment = TextAnchor.UpperCenter;
			GUI.skin.label.fontSize = 64 / FontScale.fontScale;
			GUI.Label(new Rect(widthblock * 3f, heightblock * 4f, widthblock * 14f, heightblock * 2f), "You've Reached Level " + level + "!");
			GUI.skin.label.fontSize = 64 / FontScale.fontScale;
			GUI.Label(new Rect(widthblock * 3f, heightblock * 9f, widthblock * 14f, heightblock * 2f), rewardString);
			
			GUI.skin = redGUI;
			if (GUI.Button(new Rect(widthblock * 8.75f, heightblock * 14f, widthblock * 2.5f, heightblock * 2f), "Continue")){
				levelUpMenu = false;
			}
		}
		
		if(newMessageAlert == true){
			GUI.Box(new Rect(0, 0, Screen.width, Screen.height),"");
			GUI.skin = whiteGUI;
			GUI.Box(new Rect(widthblock * 3f, heightblock * 3f, widthblock * 14f, heightblock * 14f),"");
			
			GUI.skin = buttonSkin;
			GUI.skin.label.alignment = TextAnchor.MiddleCenter;
			GUI.skin.label.fontSize = 64 / FontScale.fontScale;
			GUI.Label(new Rect(widthblock * 4f, heightblock * 4f, widthblock * 12f, heightblock * 9f), messageAlert);
			
			GUI.skin = redGUI;
			if (GUI.Button(new Rect(widthblock * 8.75f, heightblock * 14f, widthblock * 2.5f, heightblock * 2f), "Continue")){
				newMessageAlert = false;
				messageAlert = "";
			}
		}
		
		if(newGiftAlert == true){
			GUI.Box(new Rect(0, 0, Screen.width, Screen.height),"");
			GUI.skin = whiteGUI;
			GUI.Box(new Rect(widthblock * 3f, heightblock * 3f, widthblock * 14f, heightblock * 14f),"");
			
			GUI.skin = buttonSkin;
			GUI.skin.label.alignment = TextAnchor.MiddleCenter;
			GUI.skin.label.fontSize = 64 / FontScale.fontScale;
			GUI.Label(new Rect(widthblock * 4f, heightblock * 4f, widthblock * 12f, heightblock * 9f), giftAlert);
			
			GUI.skin = redGUI;
			if (GUI.Button(new Rect(widthblock * 8.75f, heightblock * 14f, widthblock * 2.5f, heightblock * 2f), "Continue")){
				newGiftAlert = false;
				giftAlert = "";
			}
		}
		
		if(showInfoBox == true){
			GUI.Box(new Rect(0, 0, Screen.width, Screen.height),"");
			GUI.skin = whiteGUI;
			GUI.Box(new Rect(widthblock * 4f, heightblock * 4f, widthblock * 12f, heightblock * 12f),"");
			
			GUI.skin = buttonSkin;
			GUI.skin.label.alignment = TextAnchor.UpperCenter;
			GUI.Label(new Rect(widthblock * 4.5f, heightblock * 5f, widthblock * 11f, heightblock * 2f), "F.A.Qs");
			GUI.skin.label.alignment = TextAnchor.MiddleCenter;
			GUI.skin.label.fontSize = 64 / FontScale.fontScale;
			GUI.Label(new Rect(widthblock * 4.5f, heightblock * 6f, widthblock * 11f, heightblock * 7f), infoBox);
			
			GUI.skin = redGUI;
			if (GUI.Button(new Rect(widthblock * 8.75f, heightblock * 13.5f, widthblock * 2.5f, heightblock * 2f), "Continue")){
				showInfoBox = false;
			}
		}
		
		if (Input.GetKeyDown(KeyCode.Escape)){
			Application.Quit();
		}
	}
}