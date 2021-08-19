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
            //Top 3 win gears
			maxRaceGears = 3;
		}
		//e.g. +8 AI Strength = 5 Gears for a win, 1 gear for 5th
		rewardGears = (maxRaceGears - finishPos) + 1;
		if(rewardGears > 0){
			gears += rewardGears;
		} else {
			rewardGears = 0;
		}
		PlayerPrefs.SetInt("Gears",gears);
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
				for(int i=0;i<99;i++){
					if(DriverNames.cup2020Names[i] != null){
						if(DriverNames.cup2020Types[i] == "Rookie"){
							validDriver.Add(i);
							//Debug.Log("Rookie Added: #" + i);
						}
					}
				}
			break;
			
			case "Rarity1":
				for(int i=0;i<99;i++){
					if(DriverNames.cup2020Names[i] != null){
						if(DriverNames.cup2020Rarity[i] == 1){
							validDriver.Add(i);
							Debug.Log("1* Rarity Added: #" + i);
						}
					}
				}
			break;
			case "Rarity2":
				for(int i=0;i<99;i++){
					if(DriverNames.cup2020Names[i] != null){
						if(DriverNames.cup2020Rarity[i] == 2){
							validDriver.Add(i);
							Debug.Log("2* Rarity Added: #" + i);
						}
					}
				}
			break;
			case "Rarity3":
				for(int i=0;i<99;i++){
					if(DriverNames.cup2020Names[i] != null){
						if(DriverNames.cup2020Rarity[i] == 3){
							validDriver.Add(i);
							Debug.Log("3* Rarity Added: #" + i);
						}
					}
				}
			break;
			
			case "CHV":
				for(int i=0;i<99;i++){
					if(DriverNames.cup2020Names[i] != null){
						if(DriverNames.cup2020Manufacturer[i] == "CHV"){
							validDriver.Add(i);
							Debug.Log("CHV Added: #" + i);
						}
					}
				}
			break;
			case "FRD":
				for(int i=0;i<99;i++){
					if(DriverNames.cup2020Names[i] != null){
						if(DriverNames.cup2020Manufacturer[i] == "FRD"){
							validDriver.Add(i);
							Debug.Log("FRD Added: #" + i);
						}
					}
				}
			break;
			case "TYT":
				for(int i=0;i<99;i++){
					if(DriverNames.cup2020Names[i] != null){
						if(DriverNames.cup2020Manufacturer[i] == "TYT"){
							validDriver.Add(i);
							Debug.Log("TYT Added: #" + i);
						}
					}
				}
			break;
			
			case "IND":
				for(int i=0;i<99;i++){
					if(DriverNames.cup2020Names[i] != null){
						if(DriverNames.cup2020Teams[i] == "IND"){
							validDriver.Add(i);
							Debug.Log("IND Added: #" + i);
						}
					}
				}
			break;
			case "RWR":
				for(int i=0;i<99;i++){
					if(DriverNames.cup2020Names[i] != null){
						if(DriverNames.cup2020Teams[i] == "RWR"){
							validDriver.Add(i);
							Debug.Log("RWR Added: #" + i);
						}
					}
				}
			break;
			case "FRM":
				for(int i=0;i<99;i++){
					if(DriverNames.cup2020Names[i] != null){
						if(DriverNames.cup2020Teams[i] == "FRM"){
							validDriver.Add(i);
							Debug.Log("FRM Added: #" + i);
						}
					}
				}
			break;
			case "RFR":
				for(int i=0;i<99;i++){
					if(DriverNames.cup2020Names[i] != null){
						if(DriverNames.cup2020Teams[i] == "RFR"){
							validDriver.Add(i);
							Debug.Log("RFR Added: #" + i);
						}
					}
				}
			break;
			case "RCR":
				for(int i=0;i<99;i++){
					if(DriverNames.cup2020Names[i] != null){
						if(DriverNames.cup2020Teams[i] == "RCR"){
							validDriver.Add(i);
							Debug.Log("RCR Added: #" + i);
						}
					}
				}
			break;
			case "CGR":
				for(int i=0;i<99;i++){
					if(DriverNames.cup2020Names[i] != null){
						if(DriverNames.cup2020Teams[i] == "CGR"){
							validDriver.Add(i);
							Debug.Log("CGR Added: #" + i);
						}
					}
				}
			break;
			case "SHR":
				for(int i=0;i<99;i++){
					if(DriverNames.cup2020Names[i] != null){
						if(DriverNames.cup2020Teams[i] == "SHR"){
							validDriver.Add(i);
							Debug.Log("SHR Added: #" + i);
						}
					}
				}
			break;
			case "PEN":
				for(int i=0;i<99;i++){
					if(DriverNames.cup2020Names[i] != null){
						if(DriverNames.cup2020Teams[i] == "PEN"){
							validDriver.Add(i);
							Debug.Log("PEN Added: #" + i);
						}
					}
				}
			break;
			case "JGR":
				for(int i=0;i<99;i++){
					if(DriverNames.cup2020Names[i] != null){
						if(DriverNames.cup2020Teams[i] == "JGR"){
							validDriver.Add(i);
							Debug.Log("JGR Added: #" + i);
						}
					}
				}
			break;
			case "HEN":
				for(int i=0;i<99;i++){
					if(DriverNames.cup2020Names[i] != null){
						if(DriverNames.cup2020Teams[i] == "HEN"){
							validDriver.Add(i);
							Debug.Log("HEN Added: #" + i);
						}
					}
				}
			break;
			
			default:
				for(int i=0;i<99;i++){
					if(DriverNames.cup2020Names[i] != null){
						validDriver.Add(i);
						Debug.Log("All Car Added: #" + i);
					}
				}
			break;
			
			case "Strategist":
				for(int i=0;i<99;i++){
					if(DriverNames.cup2020Names[i] != null){
						if(DriverNames.cup2020Types[i] == "Strategist"){
							validDriver.Add(i);
							Debug.Log("Strategist Added: #" + i);
						}
					}
				}
			break;
			case "Closer":
				for(int i=0;i<99;i++){
					if(DriverNames.cup2020Names[i] != null){
						if(DriverNames.cup2020Types[i] == "Closer"){
							validDriver.Add(i);
							Debug.Log("Closer Added: #" + i);
						}
					}
				}
			break;
			case "Intimidator":
				for(int i=0;i<99;i++){
					if(DriverNames.cup2020Names[i] != null){
						if(DriverNames.cup2020Types[i] == "Intimidator"){
							validDriver.Add(i);
							Debug.Log("Intimidator Added: #" + i);
						}
					}
				}
			break;
			case "Gentleman":
				for(int i=0;i<99;i++){
					if(DriverNames.cup2020Names[i] != null){
						if(DriverNames.cup2020Types[i] == "Gentleman"){
							validDriver.Add(i);
							Debug.Log("Gentleman Added: #" + i);
						}
					}
				}
			break;
			case "Legend":
				for(int i=0;i<99;i++){
					if(DriverNames.cup2020Names[i] != null){
						if(DriverNames.cup2020Types[i] == "Legend"){
							validDriver.Add(i);
							Debug.Log("Legend Added: #" + i);
						}
					}
				}
			break;
		}
	}
}
