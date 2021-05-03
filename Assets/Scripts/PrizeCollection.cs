using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PrizeCollection : MonoBehaviour
{
    public GUISkin eightBitSkin;
		
	float widthblock = Mathf.Round(Screen.width/20);
	float heightblock = Mathf.Round(Screen.height/20);

	int playerMoney;
	int moneyCount;
	int raceWinnings;
	
	string prizeType;
	
	public static string carReward;
	public static int carCurrentGears;
	public static int carClassMax;
	
	public List<int> validDriver = new List<int>();

	void Awake(){

		moneyCount = 0;
		playerMoney = PlayerPrefs.GetInt("PrizeMoney");
		raceWinnings = PlayerPrefs.GetInt("raceWinnings");
		if(PlayerPrefs.HasKey("NewUser")){
			PlayerPrefs.SetInt("NewUser",1);
			prizeType=PlayerPrefs.GetString("PrizeType");
			switch(prizeType){
				case "PremiumBag":
					ListPrizeOptions("Rarity3");
					PremiumGarage("cup20",validDriver[Random.Range(0,validDriver.Count)]);
					break;
				case "FreeDaily":
					ListPrizeOptions("");
					DailyGarage("cup20",validDriver[Random.Range(0,validDriver.Count)]);
					break;
				default:
					break;
			}
		} else {
			ListPrizeOptions("Rookies");
			StarterCar("cup20",validDriver[Random.Range(0,validDriver.Count)]);
			PlayerPrefs.SetInt("NewUser",1);
			prizeType="Rookies";
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
		
		GUI.Label(new Rect(widthblock * 3, heightblock * 2, widthblock * 14, heightblock * 4), "Collect Your Prize!");

		GUI.skin.label.fontSize = 48 / FontScale.fontScale;

		GUI.Label(new Rect(widthblock * 3, heightblock * 6, widthblock * 14, heightblock * 8), "" + carReward + "");

		GUI.skin.button.alignment = TextAnchor.MiddleCenter;
		
		if (GUI.Button(new Rect(widthblock * 7, heightblock * 17, widthblock * 6, heightblock * 2), "Continue")){
			if(prizeType == "Dailies"){
				Application.LoadLevel("Store");
			} else {
				Application.LoadLevel("MainMenu");
			}
		}
	}
	
	void ListPrizeOptions(string category){
		switch(category){
			case "Rookies":
				validDriver.Add(0); //Houff
				validDriver.Add(41); //Custer
				//Nemechek
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
	
	void StarterCar(string seriesPrefix, int carNumber){

		int carGears = 10;
		
		PlayerPrefs.SetInt(seriesPrefix + carNumber + "Gears", 10);
		PlayerPrefs.SetInt(seriesPrefix + carNumber + "Class", 0);
		
		int carClass = 0;
			
		carCurrentGears = carGears;
		carClassMax = GameData.classMax(carClass);
		carReward = "" + DriverNames.cup2020Names[carNumber] + " +10";
	}
	
	void DailyGarage(string seriesPrefix, int carNumber){

		int carGears = 0;
		if(PlayerPrefs.HasKey(seriesPrefix + carNumber + "Gears")){
			carGears = PlayerPrefs.GetInt(seriesPrefix + carNumber + "Gears");
			PlayerPrefs.SetInt(seriesPrefix + carNumber + "Gears", carGears + 10);
		} else {
			PlayerPrefs.SetInt(seriesPrefix + carNumber + "Gears", 10);
			PlayerPrefs.SetInt(seriesPrefix + carNumber + "Class", 0);
		}
		
		int carClass = 0;
			
		carCurrentGears = carGears;
		carClassMax = GameData.classMax(carClass);
		carReward = "" + DriverNames.cup2020Names[carNumber] + " +10";
	}
	
	void PremiumGarage(string seriesPrefix, int carNumber){

		int carGears = 0;
		if(PlayerPrefs.HasKey(seriesPrefix + carNumber + "Gears")){
			carGears = PlayerPrefs.GetInt(seriesPrefix + carNumber + "Gears");
			PlayerPrefs.SetInt(seriesPrefix + carNumber + "Gears", carGears + 50);
		} else {
			PlayerPrefs.SetInt(seriesPrefix + carNumber + "Gears", 50);
			PlayerPrefs.SetInt(seriesPrefix + carNumber + "Class", 0);
		}
		
		int carClass = 0;
			
		carCurrentGears = carGears;
		carClassMax = GameData.classMax(carClass);
		carReward = "" + DriverNames.cup2020Names[carNumber] + " +50";
	}
}