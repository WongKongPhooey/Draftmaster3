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
	
	public static string carReward;
	public static int carCurrentGears;
	public static int carClassMax;
	
	public List<int> validDriver = new List<int>();

	void Awake(){

		moneyCount = 0;
		playerMoney = PlayerPrefs.GetInt("PrizeMoney");
		raceWinnings = PlayerPrefs.GetInt("raceWinnings");
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
		int finishPos = Scoreboard.position;
		AssignPrizes("cup20",validDriver[Random.Range(0,validDriver.Count)]);
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

		GUI.Label(new Rect(widthblock * 3, heightblock * 6, widthblock * 14, heightblock * 2), "" + carReward + " (" + carCurrentGears + ")");

		GUI.Label(new Rect(widthblock * 3, heightblock * 8, widthblock * 14, heightblock * 2), " +10 Gears (" + gears + ")");

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
			carCurrentGears = carGears + 1;
			carClassMax = GameData.classMax(carClass);
			
			gears = PlayerPrefs.GetInt("Gears");
			gears += 1;
			PlayerPrefs.SetInt("Gears",gears);
		} else {
			PlayerPrefs.SetInt(seriesPrefix + carNumber + "Gears", 1);
			carReward = "" + DriverNames.cup2020Names[carNumber] + " +1";
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
}
