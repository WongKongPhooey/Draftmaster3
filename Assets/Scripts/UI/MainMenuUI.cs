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
	
    // Start is called before the first frame update
    void Start()
    {
		
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
		}
		
		totalMoney = PlayerPrefs.GetInt("PrizeMoney");
		
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
		
        loadCurrentChampionshipInfo();
    }

	void loadCurrentChampionshipInfo(){
		
		string subSeriesId;
		string subSeriesName = "No Active Championship";
		
		if((PlayerPrefs.HasKey("ChampionshipSubseries"))&&(PlayerPrefs.GetString("ChampionshipSubseries").Length > 0)){
			subSeriesId = PlayerPrefs.GetString("ChampionshipSubseries");
			Debug.Log(subSeriesId);
			int seriesInt = int.Parse(subSeriesId.Substring(0,1));
			int subSeriesInt = int.Parse(subSeriesId.Substring(1,2));
			subSeriesName = SeriesData.offlineSeries[seriesInt,subSeriesInt];
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
			SceneManager.LoadScene("CircuitSelect");
		}
	}

	void firstTimeInit(){
		PlayerPrefs.SetInt("CameraRotate", 1);
		PlayerPrefs.SetInt("TransferTokens", 1);
		PlayerPrefs.SetInt("TransfersLeft", 1);
		PlayerPrefs.SetString("TargetVersion", Application.version);
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
