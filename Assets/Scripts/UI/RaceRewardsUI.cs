using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class RaceRewardsUI : MonoBehaviour
{
	
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
	
	string setPrize;
	bool altPaintReward;
	string raceType;
	
	bool championshipReward;
	int championshipFinish;
	int seriesLength;
	
	public static string carPrize;
	public static int carPrizeNum;
	public static string carPrizeAlt;
	public static string carPrizeNumAlt;
	public static string carReward;
	public static int rewardMultiplier;
	public static int carCurrentGears;
	public static int carClassMax;

	public List<string> validDriver = new List<string>();
	
	public GameObject rewardsTitle;
	
	public GameObject moneyTitle;
	public GameObject gearsTitle;
	public GameObject partsTitle;
	public GameObject partsCar;
	
	
    // Start is called before the first frame update
    void Start()
    {
        gears = PlayerPrefs.GetInt("Gears");
        offsetGears = 0;
        rewardGears = 0;

		raceType = PlayerPrefs.GetString("RaceType");

		carPrizeNumAlt = "";
		
		setPrize = "0";
		altPaintReward = false;

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
		Debug.Log("Series Prize: " + seriesPrize);
		finishPos = PlayerPrefs.GetInt("FinishPos");
		rewardMultiplier = 1;
		
		rewardsTitle = GameObject.Find("Title");
		
		moneyTitle = GameObject.Find("MoneyTitle");
		gearsTitle = GameObject.Find("GearsTitle");
		partsTitle = GameObject.Find("PartsTitle");
		partsCar = GameObject.Find("PartsCar");
		
		championshipReward = false;
		championshipFinish = 40;
		if(PlayerPrefs.GetInt("ChampionshipReward") == 1){
			championshipReward = true;
			championshipFinish = getChampionshipPosition();
			seriesLength = PlayerPrefs.GetInt("ChampionshipLength");
			PlayerPrefs.SetInt("ChampionshipReward", 0);
		}
		
		if(championshipReward == true){
			finishPos = championshipFinish;
			rewardMultiplier = seriesLength;
			//Debug.Log("Multiplier Set as " + rewardMultiplier);
			PlayerPrefs.DeleteKey("ChampionshipSubseries");
		}
		
		prizeMoney = PrizeMoney.getPrizeMoney(finishPos-1);
		playerMoney += prizeMoney * rewardMultiplier;
		PlayerPrefs.SetInt("PrizeMoney", playerMoney);
		
		Debug.Log("Race Type: " + raceType);
		switch(raceType){
			case "Event":
			//Must win
				Debug.Log("Checking Event Rewards");
				if(finishPos == 1){
					Debug.Log("You won!");
					if(seriesPrize == "AltPaint"){
						Debug.Log("Unlocking Alt Paint: " + setPrize);
						UnlockAltPaint(setPrize);
					} else {
						//Populate event reward pool
						AssignPrizes(validDriver[Random.Range(0,validDriver.Count)], setPrize, rewardMultiplier);
					}
				} else {
					carReward = "";
				}
				break;
			default:
				//If top 10 finish..
				if(finishPos < 11){
					//Inverted chance of reward (10th = 10%, 1st = 100%)
					float chance = 11 - finishPos;
					float rnd = Random.Range(0,10);
					if(rnd <= chance){
						Debug.Log("Top 10 finish, add rewards");
						AssignPrizes(validDriver[Random.Range(0,validDriver.Count)], setPrize, rewardMultiplier);
					} else {
						carReward = "";
					}
				} else {
					carReward = "";
				}
				break;
		}
		
		if(raceType != "Event"){
			//e.g. AI Level 14 = 14 / 5  = 2.x -> 2 + 3 = 5
			float maxGearsFloat = Mathf.Floor(SeriesData.offlineAILevel[raceMenu,raceSubMenu]/5);
			maxRaceGears = (int)maxGearsFloat + 3;
			//If low strength AI Race (<+3)
			if(maxRaceGears <= 2){
				//Top 3 win gears
				maxRaceGears = 3;
			}
			if(maxRaceGears >= 6){
				//Max for a win is 8
				maxRaceGears = 5;
			}
			
			//e.g. +8 AI Strength = 5 Gears for a win, 1 gear for 5th
			rewardGears = (maxRaceGears - finishPos) + 1;
			if(rewardGears > 0){
				gears += rewardGears * rewardMultiplier;
			} else {
				rewardGears = 0;
			}
		}
		PlayerPrefs.SetInt("Gears",gears);
		
		//Update UI
		if(championshipReward == true){
			rewardsTitle.GetComponent<TMPro.TMP_Text>().text = "Championship Rewards - " + finishPos + MiscScripts.PositionPostfix(finishPos) + " Place";
		} else {
			rewardsTitle.GetComponent<TMPro.TMP_Text>().text = "Race Rewards";
		}
		
		moneyTitle.GetComponent<TMPro.TMP_Text>().text = " +$" + (prizeMoney * rewardMultiplier) + "";
		moneyTitle.GetComponent<UIAnimate>().animOffset = 40;
		moneyTitle.GetComponent<UIAnimate>().scaleIn();
		
		gearsTitle.GetComponent<TMPro.TMP_Text>().text = " +" + (rewardGears * rewardMultiplier) + " Gears (" + gears + ")";
		gearsTitle.GetComponent<UIAnimate>().animOffset = 80;
		gearsTitle.GetComponent<UIAnimate>().scaleIn();	
		
		partsTitle.GetComponent<TMPro.TMP_Text>().text = "" + carReward + "\n(" + carCurrentGears + ")";
		partsTitle.GetComponent<UIAnimate>().animOffset = 120;
		if(carReward != ""){
			partsTitle.GetComponent<UIAnimate>().scaleIn();
			
			if(carPrizeNumAlt != ""){
				partsCar.GetComponent<RawImage>().texture = Resources.Load<Texture2D>(carPrizeAlt);
			} else {
				partsCar.GetComponent<RawImage>().texture = Resources.Load<Texture2D>(carPrize);
			}
		}
		
		//Tidyup at the end
		PlayerPrefs.DeleteKey("SeriesPrize");
		PlayerPrefs.DeleteKey("RaceType");
	}

	void AssignPrizes(string carId, string setPrize, int multiplier){
		if(!PlayerPrefs.HasKey(carId + "Gears")){
			PlayerPrefs.SetInt(carId + "Gears", multiplier);
		}
		int carGears = PlayerPrefs.GetInt(carId + "Gears");
		string seriesPrefix = carId.Substring(0,5);
		int carNum = int.Parse(carId.Substring(5));
		if(int.Parse(setPrize) > 1){
			//Likely has a big fixed prize set e.g. 35 car parts
			PlayerPrefs.SetInt(carId + "Gears", carGears + int.Parse(setPrize));
			
			//Todo: make this take any seriesPrefix
			carReward = "(" + DriverNames.getSeriesNiceName(seriesPrefix) + ") " + DriverNames.getName(seriesPrefix, carNum) + " +" + int.Parse(setPrize);			
			carCurrentGears = carGears + int.Parse(setPrize);
		} else {
			PlayerPrefs.SetInt(carId + "Gears", carGears + multiplier);
			carReward = "(" + DriverNames.getSeriesNiceName(seriesPrefix) + ") " + DriverNames.getName(seriesPrefix, carNum) + " +" + multiplier;			
			carCurrentGears = carGears + multiplier;
		}
		carPrize = seriesPrefix + "livery" + carNum;
		carPrizeNum = carNum;
		
		//Reset Prizes
		PlayerPrefs.SetString("SeriesPrize","");
	}
	
	void UnlockAltPaint(string setPrize){
		//Win an alt paint rather than car parts
		//setPrize format example: cup20livery48alt2
		
		string sanitisedAlt = setPrize.Replace("livery","");
		sanitisedAlt = sanitisedAlt.Replace("alt","Alt");
		
		string extractedCarNum = setPrize.Split('y').Last();
		string extractedAltNum = setPrize.Split('t').Last();
		carPrizeAlt = setPrize;
		carPrizeNumAlt = extractedCarNum;
		
		//Debug.Log("Extracted Alt: " + extractedCarNum);
		
		extractedCarNum = extractedCarNum.Substring(0, extractedCarNum.IndexOf("alt")).Trim();
		int parsedNum = int.Parse(extractedCarNum);
		int parsedAlt = int.Parse(extractedAltNum);
		
		//Debug.Log("Extracted car number: #" + extractedCarNum);
		//Debug.Log("Extracted alt number: #" + extractedAltNum);
		
		PlayerPrefs.SetInt(sanitisedAlt + "Unlocked",1);
		if(AltPaints.cup2020AltPaintDriver[parsedNum,parsedAlt] != null){
			carReward = "New " + AltPaints.cup2020AltPaintDriver[parsedNum,parsedAlt] + " Alt Unlocked!";
		} else {
			carReward = "New " + DriverNames.cup2020Names[parsedNum] + " Alt Unlocked!";
		}
	}
	
	void ListPrizeOptions(string category){
		switch(category){
			case "Rookies":
				//for(int i=0;i<DriverNames.allCarsets.Length;i++){
				for(int i=0;i<DriverNames.allCarsets.Length;i++){
					string seriesPrefix = DriverNames.allCarsets[i];
					for(int j=0;j<99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getType(seriesPrefix,j) == "Rookie"){
								validDriver.Add("" + seriesPrefix + j + "");
								//Debug.Log("Rookie Added: #" + i);
							}
						}
					}
				}
			break;
			case "Rarity1":
				for(int i=0;i<DriverNames.allCarsets.Length;i++){
					string seriesPrefix = DriverNames.allCarsets[i];
					for(int j=0;j<99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getRarity(seriesPrefix,j) == 1){
								validDriver.Add("" + seriesPrefix + j + "");
								//Debug.Log("1* Rarity Added: #" + i);
							}
						}
					}
				}
			break;
			case "Rarity2":
				for(int i=0;i<DriverNames.allCarsets.Length;i++){
					string seriesPrefix = DriverNames.allCarsets[i];
					for(int j=0;j<99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getRarity(seriesPrefix,j) == 2){
								validDriver.Add("" + seriesPrefix + j + "");
								//Debug.Log("2* Rarity Added: #" + i);
							}
						}
					}
				}
			break;
			case "Rarity3":
				for(int i=0;i<DriverNames.allCarsets.Length;i++){
					string seriesPrefix = DriverNames.allCarsets[i];
					for(int j=0;j<99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getRarity(seriesPrefix,j) == 3){
								validDriver.Add("" + seriesPrefix + j + "");
								//Debug.Log("3* Rarity Added: #" + i);
							}
						}
					}
				}
			break;
			case "Rarity4":
				for(int i=0;i<DriverNames.allCarsets.Length;i++){
					string seriesPrefix = DriverNames.allCarsets[i];
					for(int j=0;j<99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getRarity(seriesPrefix,j) == 4){
								validDriver.Add("" + seriesPrefix + j + "");
								//Debug.Log("4* Rarity Added: #" + i);
							}
						}
					}
				}
			break;
			
			//Manufacturer Rewards
			case "CHV":
			case "FRD":
			case "TYT":
				for(int i=0;i<DriverNames.allCarsets.Length;i++){
					string seriesPrefix = DriverNames.allCarsets[i];
					for(int j=0;j<99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getManufacturer(seriesPrefix,j) == category){
								validDriver.Add("" + seriesPrefix + j + "");
								//Debug.Log(category + " Added: #" + i);
							}
						}
					}
				}
			break;
			
			//Manufacturer Rewards (Capped at 1*)
			case "CHV1":
			case "FRD1":
			case "TYT1":
				for(int i=0;i<DriverNames.allCarsets.Length;i++){
					string seriesPrefix = DriverNames.allCarsets[i];
					for(int j=0;j<99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getManufacturer(seriesPrefix,j) == category){
								if(DriverNames.getRarity(seriesPrefix,j) == 1){
									validDriver.Add("" + seriesPrefix + j + "");
									//Debug.Log(category + " Added: #" + i);
								}
							}
						}
					}
				}
			break;
			
			//Team Rewards
			case "IND":
			case "RWR":
			case "FRM":
			case "RFR":
			case "RCR":
			case "CGR":
			case "SHR":
			case "PEN":
			case "JGR":
			case "HEN":
				for(int i=0;i<DriverNames.allCarsets.Length;i++){
					string seriesPrefix = DriverNames.allCarsets[i];
					for(int j=0;j<99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getTeam(seriesPrefix,j) == category){
								validDriver.Add("" + seriesPrefix + j + "");
								//Debug.Log(category + " Added: #" + i);
							}
						}
					}
				}
			break;
			
			//Driver Type Rewards
			case "Strategist":
			case "Closer":
			case "Intimidator":
			case "Blocker":
			case "Dominator":
			case "Legend":
				for(int i=0;i<DriverNames.allCarsets.Length;i++){
					string seriesPrefix = DriverNames.allCarsets[i];
					for(int j=0;j<99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getType(seriesPrefix,j) == category){
								validDriver.Add("" + seriesPrefix + j + "");
								//Debug.Log(category + " Added: #" + i);
							}
						}
					}
				}
			break;

			case "AltPaint":
				altPaintReward = true;
				setPrize = PlayerPrefs.GetString("SeriesPrizeAmt");
			break;
			
			//Event Specific
			case "Johnson":
				validDriver.Add("cup2048");
				setPrize = PlayerPrefs.GetString("SeriesPrizeAmt");
			break;
			
			case "Earnhardt":
				validDriver.Add("cup203");
				setPrize = PlayerPrefs.GetString("SeriesPrizeAmt");
			break;
			
			default:
				for(int i=0;i<DriverNames.allCarsets.Length;i++){
					string seriesPrefix = DriverNames.allCarsets[i];
					for(int j=0;j<99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							validDriver.Add("" + seriesPrefix + j + "");
							//Debug.Log("Added: #" + i);
						}
					}
				}
			break;
		}
	}
	
	int getChampionshipPosition(){
		Debug.Log("LOOKING FOR CHAMPIONSHIP POSITION");
		string playerCarNumber = PlayerPrefs.GetString("carTexture");
		string splitAfter = "livery";
		playerCarNumber = playerCarNumber.Substring(playerCarNumber.IndexOf(splitAfter) + splitAfter.Length);
		int carNumber = int.Parse(playerCarNumber);
		Debug.Log("CAR NUMBER IS " + carNumber.ToString());
		Dictionary<int, int> championshipPoints = new Dictionary<int, int>();
		
		championshipPoints.Clear();
		int pointsTableInd = 0;
		for(int i=0;i<100;i++){
			if(PlayerPrefs.HasKey("ChampionshipPoints" + i)){
				championshipPoints.Add(i, PlayerPrefs.GetInt("ChampionshipPoints" + i));
				pointsTableInd++;
			}
		}
		
		List<KeyValuePair<int, int>> pointsTable = new List<KeyValuePair<int, int>>(championshipPoints);
		
		pointsTable.Sort(
			delegate(KeyValuePair<int, int> firstPair,
			KeyValuePair<int, int> nextPair)
			{
				return nextPair.Value.CompareTo(firstPair.Value);
			}
		);
		
		int champPosition = 0;
		pointsTableInd = 1;
		foreach(var pointsRow in pointsTable){
			if(pointsRow.Key == carNumber){
				champPosition = pointsTableInd;
				Debug.Log("CHAMPIONSHIP POSITION IS " + champPosition);
			}
			pointsTableInd++;
		}
		return champPosition;
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
