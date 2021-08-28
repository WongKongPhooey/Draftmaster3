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
	int prizeCarNumber;
	
	public bool firstCar;
	
	public static string carReward;
	public static int carCurrentGears;
	public static int carClassMax;
	
	public List<int> validDriver = new List<int>();

	void Awake(){

		moneyCount = 0;
		playerMoney = PlayerPrefs.GetInt("PrizeMoney");
		raceWinnings = PlayerPrefs.GetInt("raceWinnings");
		firstCar = false;
		if(PlayerPrefs.HasKey("NewUser")){
			PlayerPrefs.SetInt("NewUser",1);
			prizeType=PlayerPrefs.GetString("PrizeType");
			Debug.Log("Prize: " + prizeType);
			switch(prizeType){
				case "MysteryGarage":
					ListPrizeOptions("");
					prizeCarNumber = validDriver[Random.Range(0,validDriver.Count)];
					PremiumGarage("cup20",prizeCarNumber);
					break;
				case "3RareGarage":
					ListPrizeOptions("Specific");
					prizeCarNumber = validDriver[Random.Range(0,validDriver.Count)];
					PremiumGarage("cup20",prizeCarNumber);
					break;
				case "FreeDaily":
					ListPrizeOptions("");
					prizeCarNumber = validDriver[Random.Range(0,validDriver.Count)];
					DailyGarage("cup20",prizeCarNumber);
					break;
				default:
					break;
			}
		} else {
			//First time user, Rookie unlock
			firstCar = true;
			ListPrizeOptions("Rookies");
			prizeCarNumber = validDriver[Random.Range(0,validDriver.Count)];
			StarterCar("cup20",prizeCarNumber);
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
		
		GUI.skin.label.alignment = TextAnchor.MiddleRight;
		GUI.Label(new Rect(widthblock * 9, heightblock * 6, widthblock * 5, heightblock * 2), "" + carReward + "");
		GUI.DrawTexture(new Rect(widthblock * 6, heightblock * 6, widthblock * 2, widthblock * 1), Resources.Load("cup20livery" + prizeCarNumber) as Texture);

		GUI.skin.button.alignment = TextAnchor.MiddleCenter;
		
		if (GUI.Button(new Rect(widthblock * 7, heightblock * 17, widthblock * 6, heightblock * 2), "Continue")){
			if(prizeType == "Dailies"){
				Application.LoadLevel("Store");
			} else {
				if(firstCar == true){
					PlayerPrefs.SetInt("CarFocus",prizeCarNumber);
					PlayerPrefs.SetString("SeriesFocus","cup20");
					Application.LoadLevel("SingleCar");
				} else {
					Application.LoadLevel("MainMenu");
				}
			}
		}
	}
	
	void ListPrizeOptions(string category){
		switch(category){
			case "Everyone":
				for(int i=0;i<99;i++){
					if(DriverNames.cup2020Names[i] != null){
						validDriver.Add(i);
					}
				}
			break;
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
			case "Specific":
				validDriver.Add(18);
			break;
			default:
				//All Drivers
				for(int i=0;i<99;i++){
					if(DriverNames.cup2020Names[i] != null){
						validDriver.Add(i);
					}
				}
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
		int randAmt = Random.Range(3,10);
		if(PlayerPrefs.HasKey(seriesPrefix + carNumber + "Gears")){
			carGears = PlayerPrefs.GetInt(seriesPrefix + carNumber + "Gears");
			PlayerPrefs.SetInt(seriesPrefix + carNumber + "Gears", carGears + randAmt);
		} else {
			PlayerPrefs.SetInt(seriesPrefix + carNumber + "Gears", randAmt);
			PlayerPrefs.SetInt(seriesPrefix + carNumber + "Class", 0);
		}
		
		int carClass = 0;
			
		carCurrentGears = carGears;
		carClassMax = GameData.classMax(carClass);
		carReward = "" + DriverNames.cup2020Names[carNumber] + " +" + randAmt;
	}
	
	void PremiumGarage(string seriesPrefix, int carNumber){

    int[] randAmtSet = {10,10,12,12,15,15,18,18,20,25,35,40,50};
		Debug.Log("Prize: #" + carNumber);
		int randAmt = randAmtSet[Random.Range(0,12)];
		int carGears = 0;
		if(PlayerPrefs.HasKey(seriesPrefix + carNumber + "Gears")){
			carGears = PlayerPrefs.GetInt(seriesPrefix + carNumber + "Gears");
			PlayerPrefs.SetInt(seriesPrefix + carNumber + "Gears", carGears + randAmt);
		} else {
			PlayerPrefs.SetInt(seriesPrefix + carNumber + "Gears", randAmt);
			PlayerPrefs.SetInt(seriesPrefix + carNumber + "Class", 0);
		}
		
		int carClass = 0;
			
		carCurrentGears = carGears;
		carClassMax = GameData.classMax(carClass);
		carReward = "" + DriverNames.cup2020Names[carNumber] + " +" + ramdAmt;
	}
}