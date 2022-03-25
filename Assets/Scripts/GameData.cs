using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameData : MonoBehaviour {
	
	public static int gameFuel;
	public static int maxFuel;
	public static int gears;
	public static int level;
	public static int transferTokens;
	public static int transfersLeft;
	public static string rewardName;
	
	public static DateTime originTime;
	public static int day;
	public static int week;
	public static double lastFuelCheck;
	public static double lastTimeCheck;
	public static double currentTimestamp;
	public static double fuelUpdate;
	public static int dayInterval;
	public static double weekInterval;
	public static double timeSinceLast;
	public static int lastSpareTime;
	public static float spareTime;
	public static double fuelToAdd;
	public static double daysToAdd;
	public static int daysToAddInt;
	public static double weeksToAdd;
	public static float spareFuel;
	
	public static string[,] levelUpRewards = new string[50,2];
	
	void Start(){
		
		originTime = new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
		currentTimestamp = (int)(System.DateTime.UtcNow - originTime).TotalSeconds;
		
		if(PlayerPrefs.HasKey("GameDay")){
			//Get the last known cycle day
			day = PlayerPrefs.GetInt("GameDay");
			//Debug.Log("Last known day: " + day);
		} else {
			//First login
			day = 1;
		}
		
		if(PlayerPrefs.HasKey("GameWeek")){
			//Get the last known cycle day
			week = PlayerPrefs.GetInt("GameWeek");
			//Debug.Log("Last known week: " + week);
		} else {
			//First login
			week = 1;
		}
		
		if(PlayerPrefs.HasKey("Level")){
			//Get the last known fuel value
			level = PlayerPrefs.GetInt("Level");
		} else {
			//First login
			PlayerPrefs.SetInt("Level",1);
			level = 1;
		}
		
		if(PlayerPrefs.HasKey("GameFuel")){
			//Get the last known fuel value
			gameFuel = PlayerPrefs.GetInt("GameFuel");
		} else {
			//First login
			gameFuel = 20;
		}
		
		if(PlayerPrefs.HasKey("MaxFuel")){
			//Get the maximum fuel
			maxFuel = 20 + (level * 2);
			PlayerPrefs.SetInt("MaxFuel", maxFuel);
		} else {
			//First login
			maxFuel = 20;
			PlayerPrefs.SetInt("MaxFuel", 20);
		}
		
		if(PlayerPrefs.HasKey("Gears")){
			//Get the last known value
			gears = PlayerPrefs.GetInt("Gears");
		} else {
			//First login
			gears = 0;
			PlayerPrefs.SetInt("Gears",0);
		}
		
		if(PlayerPrefs.HasKey("TransferTokens")){
			//Get the last known value
			transferTokens = PlayerPrefs.GetInt("TransferTokens");
		} else {
			//First login
			PlayerPrefs.SetInt("TransferTokens",1);
		}
		
		if(PlayerPrefs.HasKey("TransfersLeft")){
			//Get the last known value
			transfersLeft = PlayerPrefs.GetInt("TransfersLeft");
		} else {
			//First login
			PlayerPrefs.SetInt("TransfersLeft",1);
		}
		
		//last saved timestamp
		if(PlayerPrefs.HasKey("LastTimeCheck")){
			lastTimeCheck = System.Convert.ToDouble(PlayerPrefs.GetString("LastTimeCheck"));
			//Debug.Log("Last time check: " + lastTimeCheck);
		} else {
			lastTimeCheck = currentTimestamp;
		}
		//Spare fraction of fuel not added previously
		if(PlayerPrefs.HasKey("SpareFuel")){
			spareFuel = PlayerPrefs.GetFloat("SpareFuel");
			if(spareFuel < 0){
				spareFuel = 0;
			}
		} else {
			spareFuel = 0;
		}
		
		//5 Minute interval
		fuelUpdate = 300;
		
		dayInterval = 86400;
		
		timeSinceLast = currentTimestamp - lastTimeCheck;
		//Debug.Log("Seconds since last check: " + timeSinceLast);
		
		fuelToAdd = timeSinceLast / System.Convert.ToDouble(fuelUpdate);
		fuelToAdd+=spareFuel;
		
		//Spare time left from last check
		if(PlayerPrefs.HasKey("SpareTime")){
			lastSpareTime = PlayerPrefs.GetInt("SpareTime");
			if(lastSpareTime < 0){
				lastSpareTime = 0;
			}
			//Debug.Log("Previous spare time" + lastSpareTime);
		} else {
			lastSpareTime = 0;
		}
		
		daysToAdd = ((timeSinceLast + lastSpareTime) / dayInterval);
		//Debug.Log("Days to add: " + daysToAdd);
		
		daysToAddInt = (int)Mathf.Floor((float)daysToAdd);
		//Debug.Log("Days added: " + daysToAddInt);
		
		spareTime = (float)timeSinceLast - (daysToAddInt * dayInterval);
		//Debug.Log("Spare Time: " + spareTime);
		
		//Bugfix: Not sure why this happens?
		if(spareTime < 0){
			spareTime = 0;
		}
		
		//If no day cycle was completed, stack up the spare time
		if(daysToAddInt == 0){
			spareTime+=lastSpareTime;
		}
		//Debug.Log("Spare time: " + spareTime);
		
		//Save the new spare time
		PlayerPrefs.SetInt("SpareTime",(int)Mathf.Floor(spareTime));
		
		//20M represents a large timestamp from the default Jan 01 1970
		if(fuelToAdd > 200000000){
			//First time logging in
			lastTimeCheck = currentTimestamp;
			gameFuel = 10;
		}
		
		//Update the last known fuel check
		PlayerPrefs.SetString("LastTimeCheck",currentTimestamp.ToString());
		
		//Day of the week cycle
		if(daysToAdd > 1){
			resetDailies();
			Debug.Log("Reset the daily plays");
		} else {
			//For testing only
			//resetDailies();
		}
		while(daysToAdd > 1){
			//Add it
			day++;
			daysToAdd--;
			//Loop on day 7
			while(day > 7){
				//End of week
				day -= 7;
				week++;
			}
		}
		//Save the new day
		PlayerPrefs.SetInt("GameDay",day);
		
		//Week of the season cycle
		while(week > 13){
			//End of week
			week -= 13;
		}
		//Save the new day
		PlayerPrefs.SetInt("GameWeek",week);
		
		//If theres fuel to add from the last login
		while(fuelToAdd > 1){
			//If fuel isn't full
			if(gameFuel < maxFuel){
				//Add it
				gameFuel++;
				fuelToAdd--;
				//Debug.Log("Add Fuel +1");
			} else {
				//Break out if fuel is full already
				fuelToAdd = 0;
				break;
			}
		}
		//Save the new fuel value
		PlayerPrefs.SetInt("GameFuel",gameFuel);
		
		//Save the spare fuel to carry over to next time
		spareFuel = float.Parse(fuelToAdd.ToString());
		PlayerPrefs.SetFloat("SpareFuel",spareFuel);
		//Debug.Log("Spare Fuel: " + spareFuel);
		
		setRewards();
	}
	
	void resetDailies(){
		PlayerPrefs.SetInt("DailyGarage",0);
		PlayerPrefs.SetInt("DailySelects",0);
		PlayerPrefs.DeleteKey("DailyRandoms");
		PlayerPrefs.SetInt("DailyRandomsPicked",0);
		
		for(int i=0;i<10;i++){
			for(int j=0;j<10;j++){
				if(SeriesData.offlineDailyPlays[i,j] != null){
					int maxPlays = SeriesData.getMaxPlays(i,j);
					//int maxPlays = SeriesData.offlineDailyPlays[i,j];
					PlayerPrefs.SetInt("DailyPlays" + i + j + "", maxPlays);
					//Debug.Log(i + "," + j + " max plays: " + maxPlays);
				}
			}
		}
	}
	
	public static void setRewards(){
		levelUpRewards[2,0] = "Gears";
		levelUpRewards[2,1] = "5";
		levelUpRewards[3,0] = "Coins";
		levelUpRewards[3,1] = "5000";
		levelUpRewards[4,0] = "Gears";
		levelUpRewards[4,1] = "5";
		levelUpRewards[5,0] = "Transfer";
		levelUpRewards[5,1] = "1";
		levelUpRewards[6,0] = "Gears";
		levelUpRewards[6,1] = "5";
		levelUpRewards[7,0] = "Coins";
		levelUpRewards[7,1] = "5000";
		levelUpRewards[8,0] = "Gears";
		levelUpRewards[8,1] = "5";
		levelUpRewards[9,0] = "Coins";
		levelUpRewards[9,1] = "5000";
		levelUpRewards[10,0] = "Transfer";
		levelUpRewards[10,1] = "1";
		levelUpRewards[11,0] = "Coins";
		levelUpRewards[11,1] = "10000";
		levelUpRewards[12,0] = "Gears";
		levelUpRewards[12,1] = "10";
		levelUpRewards[13,0] = "Coins";
		levelUpRewards[13,1] = "10000";
		levelUpRewards[14,0] = "Gears";
		levelUpRewards[14,1] = "10";
		levelUpRewards[15,0] = "Transfer";
		levelUpRewards[15,1] = "1";
		levelUpRewards[16,0] = "Gears";
		levelUpRewards[16,1] = "10";
		levelUpRewards[17,0] = "Coins";
		levelUpRewards[17,1] = "10000";
		levelUpRewards[18,0] = "Gears";
		levelUpRewards[18,1] = "10";
		levelUpRewards[19,0] = "Coins";
		levelUpRewards[19,1] = "10000";
		levelUpRewards[20,0] = "Transfer";
		levelUpRewards[20,1] = "1";
		levelUpRewards[21,0] = "Coins";
		levelUpRewards[21,1] = "20000";
		levelUpRewards[22,0] = "Gears";
		levelUpRewards[22,1] = "15";
		levelUpRewards[23,0] = "Coins";
		levelUpRewards[23,1] = "20000";
		levelUpRewards[24,0] = "Gears";
		levelUpRewards[24,1] = "15";
		levelUpRewards[25,0] = "Transfer";
		levelUpRewards[25,1] = "1";
		levelUpRewards[26,0] = "Gears";
		levelUpRewards[26,1] = "15";
		levelUpRewards[27,0] = "Coins";
		levelUpRewards[27,1] = "20000";
		levelUpRewards[28,0] = "Gears";
		levelUpRewards[28,1] = "15";
		levelUpRewards[29,0] = "Coins";
		levelUpRewards[29,1] = "20000";
		levelUpRewards[30,0] = "Transfer";
		levelUpRewards[30,1] = "1";
		levelUpRewards[31,0] = "Coins";
		levelUpRewards[31,1] = "50000";
		levelUpRewards[32,0] = "Gears";
		levelUpRewards[32,1] = "20";
		levelUpRewards[33,0] = "Coins";
		levelUpRewards[33,1] = "50000";
		levelUpRewards[34,0] = "Gears";
		levelUpRewards[34,1] = "20";
		levelUpRewards[35,0] = "Transfer";
		levelUpRewards[35,1] = "1";
		levelUpRewards[36,0] = "Gears";
		levelUpRewards[36,1] = "20";
		levelUpRewards[37,0] = "Coins";
		levelUpRewards[37,1] = "50000";
		levelUpRewards[38,0] = "Gears";
		levelUpRewards[38,1] = "20";
		levelUpRewards[39,0] = "Coins";
		levelUpRewards[39,1] = "50000";
		levelUpRewards[40,0] = "Transfer";
		levelUpRewards[40,1] = "1";
	}
	
	public static string levelUpReward(int level){
		string rewardType = levelUpRewards[level,0];
		Debug.Log("Loaded level up: " + level);
		int rewardValue = int.Parse(levelUpRewards[level,1]);
		switch(rewardType){
			case "Coins":
				int coins = PlayerPrefs.GetInt("PrizeMoney"); 
				PlayerPrefs.SetInt("PrizeMoney", coins + rewardValue);
				rewardName = "Coins";
				break;
			case "Gears":
				int gears = PlayerPrefs.GetInt("Gears");
				PlayerPrefs.SetInt("Gears", gears + rewardValue);
				rewardName = "Gears";
				break;
			case "Transfer":
				int tokens = PlayerPrefs.GetInt("TransferTokens");
				int tokensleft = PlayerPrefs.GetInt("TransfersLeft");
				PlayerPrefs.SetInt("TransferTokens", tokens + rewardValue);
				PlayerPrefs.SetInt("TransfersLeft", tokensleft + rewardValue);
				rewardName = "Transfer Token";
				break;
		}
		string levelUpText = "+" + levelUpRewards[level,1] + " " + rewardName;
		return levelUpText;
	}
	
	public static int classMax(int carClass){
		switch(carClass){
			case 0:
				return 10;
				break;
			case 1:
				return 20;
				break;
		    case 2:
				return 35;
				break;
			case 3:
				return 50;
				break;
			case 4:
				return 70;
				break;
			case 5:
				return 100;
				break;
			case 6:
				return 150;
				break;
		    default:
				return 999;
				break;
		}
	}
	
	public static int unlockGears(int unlockClass){
		int unlockedAt;
		switch(unlockClass){
			case 0:
				return 0;
			case 1:
				return 10;
				break;
			case 2:
				return 30;
				break;
		    case 3:
				return 65;
				break;
			case 4:
				return 115;
				break;
			case 5:
				return 185;
				break;
			case 6:
				return 285;
				break;
		    default:
				return 999;
				break;
		}
	}
	
	public static int upgradeCost(int carClass){
		switch(carClass){
			case 0:
				return 0;
				break;
			case 1:
				return 10000;
				break;
		    case 2:
				return 50000;
				break;
			case 3:
				return 100000;
				break;
			case 4:
				return 250000;
				break;
			case 5:
				return 500000;
				break;
			case 6:
				return 1000000;
				break;
		    default:
				return 99999999;
				break;
		}
	}
	
	public static int levelExp(int level){
		switch(level){
			case 1:
				return 100;
				break;
			case 2:
				return 125;
				break;
			case 3:
				return 150;
				break;
			case 4:
				return 175;
				break;
			case 5:
				return 200;
				break;
			case 6:
				return 225;
				break;
			case 7:
				return 250;
				break;
			case 8:
				return 275;
				break;
			case 9:
				return 300;
				break;
			case 10:
				return 350;
				break;
			case 11:
				return 400;
				break;
			case 12:
				return 450;
				break;
			case 13:
				return 500;
				break;
			case 14:
				return 550;
				break;
			case 15:
				return 650;
				break;
			case 16:
				return 750;
				break;
			case 17:
				return 850;
				break;
			case 18:
				return 950;
				break;
			case 19:
				return 1050;
				break;
			case 20:
				return 1250;
				break;
			case 21:
				return 1450;
				break;
			case 22:
				return 1650;
				break;
			case 23:
				return 1850;
				break;
			case 24:
				return 2050;
				break;
			case 25:
				return 2300;
				break;
			case 26:
				return 2550;
				break;
			case 27:
				return 2800;
				break;
			case 28:
				return 3050;
				break;
			case 29:
				return 3300;
				break;
			case 30:
				return 3600;
				break;
			case 31:
				return 3900;
				break;
			case 32:
				return 4200;
				break;
			case 33:
				return 4500;
				break;
			case 34:
				return 4800;
				break;
			case 35:
				return 5300;
				break;
			case 36:
				return 6000;
				break;
			case 37:
				return 7000;
				break;
			case 38:
				return 8000;
				break;
			case 39:
				return 9000;
				break;
			case 40:
				return 10000;
				break;
			default:
				return 999999;
				break;
		}
	}
}