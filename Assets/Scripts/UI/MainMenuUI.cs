using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour {
	
	string versionLabel;

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
	
	public GameObject alertPopup;
	public GameObject popupFrame;
	
    // Start is called before the first frame update
    void Start()
    {
		
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
		
		//Delete Pre-Race Stored Variables
		if(PlayerPrefs.HasKey("ActivePath")){
			PlayerPrefs.DeleteKey("ActivePath");
		}
		PlayerPrefs.DeleteKey("RaceModifier");
		
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
			AlertManager.showPopup("Level Up","You've leveled up matey!","dm2logo");
		}
		
		money = PlayerPrefs.GetInt("PrizeMoney");
		
		levelLabel = GameObject.Find("LevelLabel");
		levelLabel.GetComponent<TMPro.TMP_Text>().text = "Level " + level;
		
		weekDayLabel = GameObject.Find("WeekDayLabel");
		weekDayLabel.GetComponent<TMPro.TMP_Text>().text = "Week " + week + " / Day " + day;
		
		gearsLabel = GameObject.Find("GearsLabel");
		gearsLabel.GetComponent<TMPro.TMP_Text>().text = gears.ToString();
		
		moneyLabel = GameObject.Find("MoneyLabel");
		moneyLabel.GetComponent<TMPro.TMP_Text>().text = money.ToString();
		
		if(PlayerPrefs.HasKey("PlayerUsername")){
			PlayFabManager.LoginFromPrefs();
		}
		
		//Reset the game to imitate new users
		//PlayerPrefs.DeleteAll();
		
		if(!PlayerPrefs.HasKey("NewUser")){
			PlayerPrefs.SetInt("PrizeMoney",10000);
			firstTimeInit();
		}
		
		//Delete Post-Race Vars
		PlayerPrefs.DeleteKey("FinishPos");
		PlayerPrefs.DeleteKey("FixedSeries");
		PlayerPrefs.DeleteKey("CustomCar");
		PlayerPrefs.DeleteKey("CustomField");
		PlayerPrefs.SetInt("TutorialActive",0);
		PlayerPrefs.SetInt("CautionHasBeen",0);
		PlayerPrefs.SetInt("ExpAdded",0);
		PlayerPrefs.SetInt("ActiveCaution",0);

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
		}
		if(audioOn == 1){
			crowdNoise.volume = 0.25f;
		} else {
			crowdNoise.volume = 0.0f;
		}
		
		if(newGiftAlert == true){
			AlertManager.showPopup("","","");
		}
		
        loadCurrentChampionshipInfo();
    }

	void loadCurrentChampionshipInfo(){
		
		string subSeriesId;
		string subSeriesName = "No Active Championship";
		
		if((PlayerPrefs.HasKey("ChampionshipSubseries"))&&(PlayerPrefs.GetString("ChampionshipSubseries").Length > 0)){
			subSeriesId = PlayerPrefs.GetString("ChampionshipSubseries");
			Debug.Log(subSeriesId);
			int seriesInt = int.Parse(subSeriesId.Substring(0,1));
			string subSeries = subSeriesId.Substring(subSeriesId.Length-1);
			int subSeriesInt = int.Parse(subSeries);
			subSeriesName = SeriesData.offlineMenu[seriesInt] + " - " + SeriesData.offlineSeries[seriesInt,subSeriesInt];
		}
		
		GameObject championshipUI = GameObject.Find("ActiveChampionshipName");
		TMPro.TMP_Text championshipUILabel = championshipUI.GetComponent<TMPro.TMP_Text>();
		championshipUILabel.text = subSeriesName;
	}

	void loadCurrentChampionship(){
		if(PlayerPrefs.HasKey("ChampionshipSubseries")){
			PlayerPrefs.SetString("carTexture", PlayerPrefs.GetString("ChampionshipCarTexture"));
			PlayerPrefs.SetInt("CarChoice", PlayerPrefs.GetInt("ChampionshipCarChoice"));
			PlayerPrefs.SetString("carSeries", PlayerPrefs.GetString("ChampionshipCarSeries"));
			
			PlayerPrefs.SetString("ActivePath","ChampionshipRace");
			//Debug.Log("Championship Car Series is " + PlayerPrefs.GetString("ChampionshipCarSeries"));
			SceneManager.LoadScene("Menus/ChampionshipHub");
		}
	}

	void firstTimeInit(){
		PlayerPrefs.SetInt("CameraRotate", 1);
		PlayerPrefs.SetInt("TransferTokens", 1);
		PlayerPrefs.SetInt("TransfersLeft", 1);
		PlayerPrefs.SetString("TargetVersion", Application.version);
		AlertManager.showPopup("Hey Rookie! You Need A Ride?", "A driver has given you access to their car for your debut.", "");
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
