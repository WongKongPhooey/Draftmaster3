using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour {

	int exp;
	int level;
	int levelExp;
	
	int day;
	int week;
	int dayInterval;
	
	int gears;
	
	string rewardString;

	int money;

	public bool levelUpMenu;
	public static bool newMessageAlert;
	public static string messageAlert;
	
	public static bool newGiftAlert;
	public static string giftAlert;
	
	public static bool showInfoBox;
	public static string infoBox;
	
	public AudioSource crowdNoise;
	public static int audioOn;
	
	public GameObject levelLabel;
	public GameObject gearsLabel;
	public GameObject weekDayLabel;
	public GameObject moneyLabel;
	public GameObject versionLabel;
	
	public GameObject loginBtn;
	public GameObject loginBtnLabel;
	
	public GameObject timeTrialDescUI;	
	TMPro.TMP_Text timeTrialDescUILabel;
	
	public GameObject liveChallengeDescUI;	
	TMPro.TMP_Text liveChallengeDescUILabel;
	
	public GameObject latestNews;
	
	GameObject alertTitle;
	GameObject alertImage;
	GameObject alertText;
	
	public GameObject alertPopup;
	public GameObject popupFrame;
	
	void Awake(){
		
		#if UNITY_EDITOR
			//PlayerPrefs.SetInt("Gears",500);
		#endif
		
		int fpsCap = PlayerPrefs.GetInt("FPSLimit");
		switch(fpsCap){
			case 1:
				Application.targetFrameRate = 30;
				break;
			case 2:
				Application.targetFrameRate = 60;
				break;
			case 3:
				Application.targetFrameRate = 120;
				#if UNITY_EDITOR
					Application.targetFrameRate = -1;
				#endif
				break;
			default:
				Application.targetFrameRate = 60;
				break;
		}
	}
	
    // Start is called before the first frame update
    void Start()
    {
		
		#if UNITY_EDITOR
				//Testing
		//PlayerPrefs.SetInt("Exp",7250);
		//PlayerPrefs.SetInt("Level",34);
		
		//PlayerPrefs.SetInt("cup231" + "Unlocked",0);
		//PlayerPrefs.SetInt("cup231" + "Gears",66);
		//PlayerPrefs.SetInt("cup231" + "Class",0);
		#endif
		
		Time.timeScale = 1.0f;
		
		exp = PlayerPrefs.GetInt("Exp");
		//exp = 10000;
		level = PlayerPrefs.GetInt("Level");
		//Debug.Log("Level " + level);
		levelExp = GameData.levelExp(level);
		
		day = PlayerPrefs.GetInt("GameDay");
		week = PlayerPrefs.GetInt("GameWeek");
		gears = PlayerPrefs.GetInt("Gears");
		
		dayInterval = 86400;
		
		rewardString = "";
		
		newMessageAlert = false;
		newGiftAlert = false;
		showInfoBox = false;
		
		if(!PlayerPrefs.HasKey("FPSLimit")){
			PlayerPrefs.SetInt("FPSLimit",2);
		}
		
		if(PlayerPrefs.HasKey("MidRaceLoading")){
			PlayerPrefs.DeleteKey("MidRaceLoading");
		}
		
		//Delete Pre-Race Stored Variables
		if(PlayerPrefs.HasKey("ActivePath")){
			PlayerPrefs.DeleteKey("ActivePath");
		}
        if(PlayerPrefs.HasKey("RaceType")){
			PlayerPrefs.DeleteKey("RaceType");
		}
		if(PlayerPrefs.HasKey("RaceMoment")){
			PlayerPrefs.DeleteKey("RaceMoment");
		}
		if(PlayerPrefs.HasKey("StartingLap")){
			PlayerPrefs.DeleteKey("StartingLap");
		}
		if(PlayerPrefs.HasKey("RaceModifier")){
			PlayerPrefs.DeleteKey("RaceModifier");
		}
		if(PlayerPrefs.HasKey("CustomFieldOrder")){
			PlayerPrefs.DeleteKey("CustomFieldOrder");
		}
		if(PlayerPrefs.HasKey("MomentComplete")){
			PlayerPrefs.DeleteKey("MomentComplete");
		}
		if(PlayerPrefs.HasKey("RaceAILevel")){
			PlayerPrefs.DeleteKey("RaceAILevel");
		}
		if(PlayerPrefs.HasKey("SpawnFromCaution")){
			PlayerPrefs.DeleteKey("SpawnFromCaution");
		}
		if(PlayerPrefs.HasKey("RaceAltPaintsChosen")){
			PlayerPrefs.DeleteKey("RaceAltPaintsChosen");
		}
		if(PlayerPrefs.HasKey("SeriesPrizeAmt")){
			PlayerPrefs.DeleteKey("SeriesPrizeAmt");
		}

		for(int i=0;i<100;i++){
			if(PlayerPrefs.HasKey("RaceAltPaint" + i)){
				PlayerPrefs.DeleteKey("RaceAltPaint" + i);
				//Debug.Log("Reset Alt Paints");
			}
		}
		PlayerPrefs.DeleteKey("RaceAltPaintsChosen");
		
		//Level Up?
		if(exp > levelExp){
			exp-= levelExp;
			level++;
			PlayerPrefs.SetInt("Exp", exp);
			PlayerPrefs.SetInt("Level", level);
			
			GameData.setRewards();
			rewardString = GameData.levelUpReward(level);
			
			Debug.Log("Level Up -> " + level);
			levelUpMenu = true;
			alertPopup.GetComponent<AlertManager>().showPopup("Level Up","You've reached level " + level + ", and have been rewarded " + rewardString + "!","dm2logo");
		}
		
		money = PlayerPrefs.GetInt("PrizeMoney");
		
		levelLabel = GameObject.Find("LevelLabel");
		levelLabel.GetComponent<TMPro.TMP_Text>().text = "Level " + level;
		
		RectTransform carGearsProgressUI = GameObject.Find("LevelProgress").GetComponent<RectTransform>();
		float gearsProgressUIWidth = Mathf.Round((100 / (float)levelExp) * (float)exp) + 1;
		//Debug.Log(exp + " / " + levelExp);
		carGearsProgressUI.sizeDelta = new Vector2(gearsProgressUIWidth, 12);
		
		weekDayLabel = GameObject.Find("WeekDayLabel");
		weekDayLabel.GetComponent<TMPro.TMP_Text>().text = "Week " + week + " / Day " + day;
		
		gearsLabel = GameObject.Find("GearsLabel");
		gearsLabel.GetComponent<TMPro.TMP_Text>().text = gears.ToString();
		
		moneyLabel = GameObject.Find("MoneyLabel");
		moneyLabel.GetComponent<TMPro.TMP_Text>().text = money.ToString();
		
		loginBtn = GameObject.Find("LoginButton");
		loginBtnLabel = GameObject.Find("LoginButtonLabel");
		
		versionLabel = GameObject.Find("VersionLabel");
		versionLabel.GetComponent<TMPro.TMP_Text>().text = "v" + Application.version;
		
		timeTrialDescUI = GameObject.Find("TimeTrialDesc");
		timeTrialDescUILabel = timeTrialDescUI.GetComponent<TMPro.TMP_Text>();
			
		liveChallengeDescUI = GameObject.Find("LiveChallenge");
		liveChallengeDescUILabel = liveChallengeDescUI.GetComponent<TMPro.TMP_Text>();
		
		if(PlayerPrefs.GetString("MessageAlert") != ""){
			latestNews = GameObject.Find("LatestNews");
			latestNews.GetComponent<TMPro.TMP_Text>().text = PlayerPrefs.GetString("MessageAlert");
		}
		
		if(PlayerPrefs.HasKey("PlayerUsername")){
			PlayFabManager.LoginFromPrefs();
			loginBtn.GetComponent<NavButton>().sceneName = "Levels/MyAccount";
			loginBtnLabel.GetComponent<TMPro.TMP_Text>().text = PlayerPrefs.GetString("PlayerUsername");
		} else {
			clearOnlineData();
			loginBtnLabel.GetComponent<TMPro.TMP_Text>().text = "Login/Register";
		}
		
		//Reset the game to imitate new users
		#if UNITY_EDITOR
		//PlayerPrefs.DeleteAll();
		PlayerPrefs.SetInt("Gears",1000);
		//PlayerPrefs.SetInt("Level", 45);
		#endif
		
		if(!PlayerPrefs.HasKey("NewUser")){
			PlayerPrefs.SetInt("PrizeMoney",10000);
			PlayerPrefs.SetInt("Gears",10);
			firstTimeInit();
		}
		
		//Delete Post-Race Vars
		PlayerPrefs.DeleteKey("FinishPos");
		PlayerPrefs.DeleteKey("FixedSeries");
		PlayerPrefs.DeleteKey("CustomCar");
		PlayerPrefs.DeleteKey("CustomField");
		PlayerPrefs.DeleteKey("EventReplay");
		PlayerPrefs.SetInt("TutorialActive",0);
		PlayerPrefs.SetInt("CautionHasBeen",0);
		PlayerPrefs.SetInt("ExpAdded",0);
		PlayerPrefs.SetInt("ActiveCaution",0);
		PlayerPrefs.SetInt("Volume",1);

		//First Time Init
		if(!PlayerPrefs.HasKey("TotalStarts")){
			PlayerPrefs.SetInt("TotalStarts", 0);
			PlayerPrefs.SetInt("TotalWins", 0);
			PlayerPrefs.SetInt("TotalLaps", 0);
			PlayerPrefs.SetInt("TotalChampionships", 0);
			PlayerPrefs.SetInt("TotalTop5s", 0);
		}
		
		//Menu Sounds
		if(!PlayerPrefs.HasKey("AudioOn")){
			PlayerPrefs.SetInt("AudioOn",1);
			audioOn = 1;
		} else {
			audioOn = PlayerPrefs.GetInt("AudioOn");
			//Possible muted games fix
			if((audioOn != 0)&&(audioOn != 1)){
				PlayerPrefs.SetInt("AudioOn",1);
				audioOn = 1;
			}
		}
		if(audioOn == 1){
			crowdNoise.volume = 0.15f;
		} else {
			crowdNoise.volume = 0.0f;
		}
		
        //loadCurrentChampionshipInfo();
		checkForPlayfabUpdates();
    }

	void checkForPlayfabUpdates(){
		if(PlayerPrefs.GetString("LiveTimeTrial") == ""){
			if(!PlayerPrefs.HasKey("PlayerUsername")){
				timeTrialDescUILabel.text = "Register a free account to use the online features of the game. This includes the weekly Time Trial leaderboard, and live Challenges!";
			} else {
				timeTrialDescUILabel.text = "There is No Active Time Trial";
			}
		} else {
			timeTrialDescUILabel.text = "Set your fastest lap in any race series with an official Cup car at " + PlayerPrefs.GetString("LiveTimeTrial") + " this week to enter. Top 5 win prizes!";
		}
		
		if(PlayerPrefs.GetString("MomentName") == ""){
			liveChallengeDescUILabel.text = "";
		} else {
			liveChallengeDescUILabel.text = "Live Moments Challenge\n" + PlayerPrefs.GetString("MomentName") + "";
		}
	}

	void loadCurrentChampionshipInfo(){
		
	}

	void loadCurrentChampionship(){
		if(PlayerPrefs.HasKey("ChampionshipSubseries")){
			PlayerPrefs.SetString("carTexture", PlayerPrefs.GetString("ChampionshipCarTexture"));
			PlayerPrefs.SetInt("CarChoice", PlayerPrefs.GetInt("ChampionshipCarChoice"));
			PlayerPrefs.SetString("carSeries", PlayerPrefs.GetString("ChampionshipCarSeries"));
			PlayerPrefs.SetString("ActivePath","ChampionshipRace");
			SceneManager.LoadScene("Menus/ChampionshipHub");
		}
	}

	void firstTimeInit(){
		Debug.Log("New User Setup");
		PlayerPrefs.SetInt("AudioOn", 1);
		PlayerPrefs.SetInt("CommsOn", 1);
		PlayerPrefs.SetInt("CameraRotate", 1);
		PlayerPrefs.SetInt("CameraZoom", 1);
		PlayerPrefs.SetInt("WreckFreq", 2);
		PlayerPrefs.SetInt("FPSLimit", 2);
		PlayerPrefs.SetInt("TransferTokens", 1);
		PlayerPrefs.SetInt("TransfersLeft", 1);
		PlayerPrefs.SetString("TargetVersion", Application.version);
		alertPopup.GetComponent<AlertManager>().showPopup("Hey Rookie! You Need A Ride?", "Some of the drivers have let you use their cars to get you started!", "cup22livery78");
		Debug.Log("Open popup");
		PlayerPrefs.SetInt("NewUser",1);
	}
	
	void clearOnlineData(){
		PlayerPrefs.SetString("StoreDailySelects","");
		PlayerPrefs.SetString("LiveTimeTrial","");
	}

    // Update is called once per frame
    void Update(){
        checkForPlayfabUpdates();
    }
}
