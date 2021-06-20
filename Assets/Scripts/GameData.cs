using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameData : MonoBehaviour {
	
	public static int gameFuel;
	public static int maxFuel;
	public static int gears;
	public static int level;
	
	public static DateTime originTime;
	public static int day;
	public static int week;
	public static double lastFuelCheck;
	public static double lastTimeCheck;
	public static double currentTimestamp;
	public static double fuelUpdate;
	public static double dayInterval;
	public static double weekInterval;
	public static double timeSinceLast;
	public static double fuelToAdd;
	public static double daysToAdd;
	public static double weeksToAdd;
	public static float spareFuel;
	
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
			//Get the last known fuel value
			gears = PlayerPrefs.GetInt("Gears");
		} else {
			//First login
			gears = 0;
		}
		
		//last saved timestamp
		if(PlayerPrefs.HasKey("LastTimeCheck")){
			lastTimeCheck = System.Convert.ToDouble(PlayerPrefs.GetString("LastTimeCheck"));
		}
		//Spare fraction of fuel not added previously
		if(PlayerPrefs.HasKey("SpareFuel")){
			spareFuel = PlayerPrefs.GetFloat("SpareFuel");
		} else {
			spareFuel = 0;
		}
		
		//5 Minute interval
		fuelUpdate = 300;
		
		dayInterval = 60 * 60 * 24;
		
		timeSinceLast = currentTimestamp - lastTimeCheck;
		fuelToAdd = timeSinceLast / System.Convert.ToDouble(fuelUpdate);
		fuelToAdd+=spareFuel;
		
		daysToAdd = timeSinceLast / dayInterval;
		//Debug.Log("Days to add: " + daysToAdd);
		
		//Debug.Log("Fuel To Add: " + fuelToAdd);
		
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
	}
	
	void resetDailies(){
		PlayerPrefs.SetInt("DailyGarage",0);
		PlayerPrefs.SetInt("DailySelects",0);
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
				return 150;
				break;
			case 3:
				return 200;
				break;
			case 4:
				return 250;
				break;
			case 5:
				return 300;
				break;
			case 6:
				return 350;
				break;
			case 7:
				return 400;
				break;
			case 8:
				return 450;
				break;
			case 9:
				return 500;
				break;
			case 10:
				return 600;
				break;
			case 11:
				return 700;
				break;
			case 12:
				return 800;
				break;
			case 13:
				return 900;
				break;
			case 14:
				return 1000;
				break;
			case 15:
				return 1200;
				break;
			case 16:
				return 1400;
				break;
			case 17:
				return 1600;
				break;
			case 18:
				return 1800;
				break;
			case 19:
				return 2000;
				break;
			case 20:
				return 2250;
				break;
			case 21:
				return 2500;
				break;
			case 22:
				return 2750;
				break;
			case 23:
				return 3000;
				break;
			case 24:
				return 3250;
				break;
			case 25:
				return 3600;
				break;
			case 26:
				return 4000;
				break;
			case 27:
				return 4450;
				break;
			case 28:
				return 4950;
				break;
			case 29:
				return 5500;
				break;
			case 30:
				return 6100;
				break;
			default:
				return 999999;
				break;
		}
	}
}