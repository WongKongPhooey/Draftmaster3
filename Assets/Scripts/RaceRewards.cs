using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RaceRewards : MonoBehaviour
{
    public GUISkin eightBitSkin;
		
	float widthblock = Mathf.Round(Screen.width/20);
	float heightblock = Mathf.Round(Screen.height/20);

	int playerMoney;
	int moneyCount;
	int raceWinnings;
	int gears;
	int position;
	int finishPos;
	
	int prizeMoney;
	int maxRaceGears;
	
	string seriesPrize;
	int raceMenu;
	int raceSubMenu;
	int offsetGears;
    int rewardGears;
	
	public static int carPrizeNum;
	public static string carReward;
	public static int carCurrentGears;
	public static int carClassMax;
	
	public Texture2D moneyTex;
	public Texture2D gasCanTex;
	public Texture2D gearTex;
	
	public static Texture2D moneyTexInst;
	public static Texture2D gasCanTexInst;
	public static Texture2D gearTexInst;
	
	public List<int> validDriver = new List<int>();

	void Awake(){

		widthblock = Mathf.Round(Screen.width/20);
		heightblock = Mathf.Round(Screen.height/20);

		moneyTexInst = moneyTex;
		gasCanTexInst = gasCanTex;
		gearTexInst = gearTex;

        gears = PlayerPrefs.GetInt("Gears");
        offsetGears = 0;
        rewardGears = 0;


		moneyCount = 0;
		playerMoney = PlayerPrefs.GetInt("PrizeMoney");
		raceWinnings = PlayerPrefs.GetInt("raceWinnings");
		seriesPrize = PlayerPrefs.GetString("SeriesPrize");
		raceMenu = PlayerPrefs.GetInt("CurrentSeries");
		raceSubMenu = PlayerPrefs.GetInt("CurrentSubseries");
		if(seriesPrize != ""){
			ListPrizeOptions(seriesPrize);
		} else {
			ListPrizeOptions("");
		}
		finishPos = PlayerPrefs.GetInt("FinishPos");
		
		prizeMoney = PrizeMoney.getPrizeMoney(finishPos);
		playerMoney += prizeMoney;
		PlayerPrefs.SetInt("PrizeMoney", playerMoney);
		
        //If top 10 finish..
		if(finishPos < 11){
            //Inverted chance of reward (10th = 10%, 1st = 100%)
			float chance = 11 - finishPos;
			float rnd = Random.Range(0,10);
			if(rnd <= chance){
				AssignPrizes("cup20",validDriver[Random.Range(0,validDriver.Count)]);
			} else {
				carReward = "";
			}
		} else {
			carReward = "";
		}
		
		maxRaceGears = SeriesData.offlineAILevel[raceMenu,raceSubMenu] - 3;
		//If low strength AI Race (<+3)
        if(maxRaceGears <= 0){
            //Only reward the winner with gears
			if(finishPos == 1){
                rewardGears = 1;
				gears += rewardGears;
				PlayerPrefs.SetInt("Gears",gears);
			} else {
                rewardGears = 0;
            }
        //If stronger AI Race
		} else {
            //e.g. +8 AI Strength = +5 Max, - 3rd place finish, = 2, +1 = 3
			rewardGears = (maxRaceGears - finishPos) + 1;
			if(rewardGears > 0){
				gears += rewardGears;
			} else {
                rewardGears = 0;
            }
            PlayerPrefs.SetInt("Gears",gears);
		}
	}

	void FixedUpdate(){
	}

	void OnGUI() {
		
		GUI.skin = eightBitSkin;

		GUI.skin.button.fontSize = 64 / FontScale.fontScale;

		GUI.skin.label.fontSize = 64 / FontScale.fontScale;

		GUI.skin.label.normal.textColor = Color.black;

		GUI.skin.label.alignment = TextAnchor.MiddleCenter;
		
		GUI.Label(new Rect(widthblock * 3, heightblock * 2, widthblock * 14, heightblock * 4), "Race Rewards");

		GUI.skin.label.fontSize = 48 / FontScale.fontScale;

		if(carReward != ""){
			GUI.skin.label.alignment = TextAnchor.MiddleRight;
			GUI.Label(new Rect(widthblock * 9, heightblock * 6, widthblock * 5, heightblock * 2), "" + carReward + " (" + carCurrentGears + ")");
			GUI.DrawTexture(new Rect(widthblock * 6, heightblock * 6, widthblock * 2, widthblock * 1), Resources.Load("cup20livery" + carPrizeNum) as Texture);
		}
		
		GUI.DrawTexture(new Rect(widthblock * 7, heightblock * 9, widthblock * 1, widthblock * 1), gearTexInst);
		GUI.Label(new Rect(widthblock * 9, heightblock * 9, widthblock * 5, heightblock * 2), " +" + rewardGears + " Gears (" + gears + ")");
		
		if(prizeMoney != 0){
			GUI.DrawTexture(new Rect(widthblock * 7, heightblock * 12, widthblock * 1, widthblock * 1), moneyTexInst);
			GUI.Label(new Rect(widthblock * 9, heightblock * 12, widthblock * 5, heightblock * 2), " +$" + prizeMoney + "");
		}

		GUI.skin.label.alignment = TextAnchor.MiddleCenter;
		GUI.skin.button.alignment = TextAnchor.MiddleCenter;
		
		if (GUI.Button(new Rect(widthblock * 7, heightblock * 17, widthblock * 6, heightblock * 2), "Continue")){
			Application.LoadLevel("MainMenu");
		}
	}
	
	void AssignPrizes(string seriesPrefix, int carNumber){
		if(PlayerPrefs.HasKey(seriesPrefix + carNumber + "Gears")){
			int carGears = PlayerPrefs.GetInt(seriesPrefix + carNumber + "Gears");
			int carClass = PlayerPrefs.GetInt(seriesPrefix + carNumber + "Class");
			PlayerPrefs.SetInt(seriesPrefix + carNumber + "Gears", carGears + 1);
			carReward = "" + DriverNames.cup2020Names[carNumber] + " +1";
			carPrizeNum = carNumber;			
			carCurrentGears = carGears + 1;
			carClassMax = GameData.classMax(carClass);
		} else {
			PlayerPrefs.SetInt(seriesPrefix + carNumber + "Gears", 1);
			carPrizeNum = carNumber;
			carReward = "" + DriverNames.cup2020Names[carNumber] + " +1";
		}
		//Reset Prizes
		PlayerPrefs.SetString("SeriesPrize","");
	}
	
	void ListPrizeOptions(string category){
		switch(category){
			case "Rookies":
				validDriver.Add(0); //Houff
				validDriver.Add(41); //Custer
				validDriver.Add(38); //Nemechek
				validDriver.Add(95); //Bell
				validDriver.Add(8); //Reddick
				validDriver.Add(15); //Poole
			break;
			case "Playoffs":
				validDriver.Add(2);
				validDriver.Add(9);
				validDriver.Add(11);
				validDriver.Add(22);
			break;
			case "Testing":
				validDriver.Add(9);
				validDriver.Add(18);
				validDriver.Add(48);
			break;
			case "Rarity1":
				validDriver.Add(0);
				validDriver.Add(7);
				validDriver.Add(8);
				validDriver.Add(13);
				validDriver.Add(15);
				validDriver.Add(16);
				validDriver.Add(17);
				validDriver.Add(20);
				validDriver.Add(21);
				validDriver.Add(27);
				validDriver.Add(32);
				validDriver.Add(34);
				validDriver.Add(37);
				validDriver.Add(38);
				validDriver.Add(41);
				validDriver.Add(47);
				validDriver.Add(49);
				validDriver.Add(51);
				validDriver.Add(52);
				validDriver.Add(53);
				validDriver.Add(54);
				validDriver.Add(62);
				validDriver.Add(66);
				validDriver.Add(77);
				validDriver.Add(78);
				validDriver.Add(95);
				validDriver.Add(96);
			break;
			case "Rarity2":
				validDriver.Add(1);
				validDriver.Add(3);
				validDriver.Add(6);
				validDriver.Add(10);
				validDriver.Add(12);
				validDriver.Add(14);
				validDriver.Add(24);
				validDriver.Add(42);
				validDriver.Add(43);
				validDriver.Add(88);
			break;
			case "Rarity3":
				validDriver.Add(2);
				validDriver.Add(4);
				validDriver.Add(9);
				validDriver.Add(11);
				validDriver.Add(18);
				validDriver.Add(19);
				validDriver.Add(22);
				validDriver.Add(48);
			break;
			case "Plate1":
				validDriver.Add(16);
				validDriver.Add(20);
				validDriver.Add(34);
				validDriver.Add(47);
			break;
			case "Plate2":
				validDriver.Add(1);
				validDriver.Add(3);
				validDriver.Add(6);
				validDriver.Add(10);
				validDriver.Add(12);
				validDriver.Add(24);
			break;
			case "Plate3":
				validDriver.Add(2);
				validDriver.Add(11);
				validDriver.Add(22);
			break;
			default:
				//All Drivers
				validDriver.Add(0);
				validDriver.Add(1);
				validDriver.Add(2);
				validDriver.Add(3);
				validDriver.Add(4);
				validDriver.Add(6);
				validDriver.Add(7);
				validDriver.Add(8);
				validDriver.Add(9);
				validDriver.Add(10);
				validDriver.Add(11);
				validDriver.Add(12);
				validDriver.Add(13);
				validDriver.Add(14);
				validDriver.Add(15);
				validDriver.Add(16);
				validDriver.Add(17);
				validDriver.Add(18);
				validDriver.Add(19);
				validDriver.Add(20);
				validDriver.Add(21);
				validDriver.Add(22);
				validDriver.Add(24);
				validDriver.Add(41);
				validDriver.Add(42);
				validDriver.Add(43);
				validDriver.Add(47);
				validDriver.Add(48);
				validDriver.Add(95);
			break;
		}
	}
}
