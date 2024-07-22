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
	string currentSeriesIndex;
	
	string setPrize;
	bool altPaintReward;
	string raceType;
	
	bool championshipReward;
	int championshipFinish;
	int seriesLength;
	
	int momentComplete;
	
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
	
	public GameObject expTitle;
	public GameObject moneyTitle;
	public GameObject gearsTitle;
	public GameObject partsTitle;
	public GameObject partsCar;
	
	
    // Start is called before the first frame update
    void Awake()
    {
		PrizeMoney.setPrizeMoney();
		
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
		if(PlayerPrefs.HasKey("SeriesPrizeAmt")){
			if(PlayerPrefs.GetString("SeriesPrizeAmt") != ""){
				setPrize = PlayerPrefs.GetString("SeriesPrizeAmt");
			}
		}
		currentSeriesIndex = PlayerPrefs.GetString("CurrentSeriesIndex");
		raceMenu = PlayerPrefs.GetInt("CurrentSeries");
		raceSubMenu = PlayerPrefs.GetInt("CurrentSubseries");
		validDriver = null;
		if(seriesPrize != ""){
			validDriver = ListPrizeOptions(seriesPrize);
		} else {
			validDriver = ListPrizeOptions("");
		}
		//Debug.Log("Series Prize: " + seriesPrize);
		finishPos = PlayerPrefs.GetInt("PlayerFinishPosition");
		//Debug.Log("Finished: " + finishPos);
		rewardMultiplier = 1;
		
		rewardsTitle = GameObject.Find("Title");
		
		expTitle = GameObject.Find("ExpTitle");
		moneyTitle = GameObject.Find("MoneyTitle");
		gearsTitle = GameObject.Find("GearsTitle");
		partsTitle = GameObject.Find("PartsTitle");
		partsCar = GameObject.Find("PartsCar");
		
		expTitle.GetComponent<TMPro.TMP_Text>().text = PlayerPrefs.GetString("ExpInfo");
		expTitle.GetComponent<UIAnimate>().animOffset = 40;
		expTitle.GetComponent<UIAnimate>().scaleIn();
		
		championshipReward = false;
		championshipFinish = 40;
		if(PlayerPrefs.GetInt("ChampionshipReward") == 1){
			championshipReward = true;
			seriesLength = PlayerPrefs.GetInt("SeriesChampionship" + currentSeriesIndex + "Length");
			PlayerPrefs.SetInt("ChampionshipReward", 0);
		}
		
		if(championshipReward == true){
			rewardMultiplier = seriesLength;
			Debug.Log("Multiplier Set as " + rewardMultiplier);
			PlayerPrefs.DeleteKey("ChampionshipSubseries");
			PlayerPrefs.DeleteKey("SeriesChampionship" + currentSeriesIndex + "Round");
		}
		
		if(raceType == "Event"){
			//Nominal amount for an event win
			prizeMoney = 1000;
		} else {
			prizeMoney = PrizeMoney.getPrizeMoney(finishPos);
		}
		Debug.Log("Won: " + prizeMoney);
		playerMoney += prizeMoney * rewardMultiplier;
		Debug.Log("Multiplied Win: " + (prizeMoney * rewardMultiplier));
		PlayerPrefs.SetInt("PrizeMoney", playerMoney);
		
		moneyTitle.GetComponent<TMPro.TMP_Text>().text = " +$" + (prizeMoney * rewardMultiplier) + "";
		moneyTitle.GetComponent<UIAnimate>().animOffset = 80;
		moneyTitle.GetComponent<UIAnimate>().scaleIn();
		
		int raceEntries = PlayerPrefs.GetInt("RaceEntries");
		
		//Adjust based on size of field in the race
		if(raceEntries == 0){
			raceEntries = 40;
		}
		//Min of 60%, as not to scale the rewards too low
		if(raceEntries < 24){
			raceEntries = 24;
		}
		
		if(raceType != "Event"){
			//e.g. AI Level 14 = 14 / 3  = 4.x -> 4 + 2 = 6
			//e.g. AI Level 5 = 5 / 3 = 1.x -> 1 + 2 = 3
			float maxGearsFloat = Mathf.Floor(SeriesData.offlineAILevel[raceMenu,raceSubMenu]/3);
			maxRaceGears = (int)maxGearsFloat + 2;
			//If low strength AI Race (<+3)
			if(maxRaceGears <= 2){
				//Top 3 win gears
				maxRaceGears = 3;
			}
			if(maxRaceGears >= 7){
				//Max for a win is 7
				maxRaceGears = 7;
			}
			
			//Longer seasons are more generous with rewards
			if((raceType == "Championship")&&(rewardMultiplier >= 8)){
				//33 race series, 150% difficulty
				//e.g. 1st -> 7 - 0 = 7
				//e.g. 10th -> 7 - 2 = 5
				//e.g. 27th -> 7 - 6 = 1
				//e.g. 28th -> 7 - 7 = 0
				
				//20 race series, 60% difficulty
				//e.g. 1st -> 4 - 0 = 4
				//e.g. 10th -> 4 - 2 = 2
				//e.g. 15th -> 4 - 3 = 1
				//e.g. 16th -> 4 - 4 = 0
				float scaledFinish = (finishPos + 1)/4;
				rewardGears = maxRaceGears - Mathf.FloorToInt(scaledFinish);
				Debug.Log("Rewarded Gears: " + rewardGears + ", Max Race Gears: " + maxRaceGears + ", Finish Pos: " + finishPos + ", Pos/4(Floor): " + Mathf.FloorToInt((finishPos + 1)/4f) + "");

				//Adjust based on size of field in the race
				float scaledRewards = (rewardGears / 40f) * raceEntries;
				rewardGears = Mathf.FloorToInt(scaledRewards);
				
				Debug.Log("Rewarded Gears Per Race: " + rewardGears);
				int minimumGears = Mathf.CeilToInt(rewardMultiplier/2f);
				Debug.Log("Minimum Reward Gears: " + minimumGears);
				
				if(rewardGears >= 1){
					//33 race series, 150% difficulty
					//e.g. 1st -> 7 * 33 = 231
					//e.g. 10th -> 5 * 33 = 165
					//e.g. 27th -> 1 * 33 = 33
					//e.g. 28th -> 0 * 33 = 0
					
					//20 race series, 60% difficulty
					//e.g. 1st -> 4 * 20 = 80
					//e.g. 10th -> 2 * 20 = 40
					//e.g. 15th -> 1 * 20 = 20
					gears += rewardGears * rewardMultiplier;
					Debug.Log("Total Rewarded Gears: " + (rewardGears * rewardMultiplier));
					gearsTitle.GetComponent<TMPro.TMP_Text>().text = " +" + (rewardGears * rewardMultiplier) + " Gears (" + gears + ")";
				} else {
					//e.g. 40th -> 17
					gears+= minimumGears;
					gearsTitle.GetComponent<TMPro.TMP_Text>().text = " +" + minimumGears + " Gears (" + gears + ")";
				}
			} else {
				//e.g. 150% AI Strength = 7 Gears for a win, 1 gear for 24th
				rewardGears = maxRaceGears - Mathf.FloorToInt(finishPos/4);
				
				float scaledRewards = (rewardGears / 40f) * raceEntries;
				rewardGears = Mathf.FloorToInt(scaledRewards);

				//Debug.Log("Single Race Gears Rewarded: " + rewardGears + "Max Race Gears: " + maxRaceGears + ", Finish Pos: " + finishPos + ", Pos/4(Ceil): " + Mathf.FloorToInt(finishPos/4) + ", Factor: " + raceEntries + "/40, Scaled Rewards: " + scaledRewards);
				
				if(raceType == "Event"){
					rewardGears = 1;
				}
				
				if(rewardGears > 0){
					gears += rewardGears * rewardMultiplier;
					Debug.Log("You've got Gears");
					gearsTitle.GetComponent<TMPro.TMP_Text>().text = " +" + (rewardGears * rewardMultiplier) + " Gears (" + gears + ")";
				} else {
					rewardGears = 0;
					Debug.Log("No Gears");
					gearsTitle.GetComponent<TMPro.TMP_Text>().text = " +0 Gears (" + gears + ")";
				}
			}
		}
		PlayerPrefs.SetInt("Gears",gears);
		
		gearsTitle.GetComponent<UIAnimate>().animOffset = 120;
		gearsTitle.GetComponent<UIAnimate>().scaleIn();	
		
		//Debug.Log("Race Type: " + raceType);
		switch(raceType){
			case "Event":
				Debug.Log("Checking Event Rewards");
				//Int as bool
				momentComplete = PlayerPrefs.GetInt("MomentComplete");
				if((finishPos == 1)||(momentComplete == 1)){
					if(seriesPrize == "AltPaint"){
						Debug.Log("Unlocking Alt Paint: " + setPrize);
						UnlockAltPaint(setPrize);
					} else {
						//Populate event reward pool
						Debug.Log("Assign Event Prize");
						AssignPrizes(validDriver[Random.Range(0,validDriver.Count)], setPrize, 1);
					}
				} else {
					carReward = "";
				}
				break;
			case "Championship":
				//e.g. 33 race season / 50% / finished 10th = 7
				//e.g. 33 race season / 150% / finished 20th = 10
				//e.g. 33 race season / 150% / finished 1st = 99
				float maxRewardFloat = SeriesData.offlineAILevel[raceMenu,raceSubMenu]/5f;
				int maxRewardInt = Mathf.CeilToInt(maxRewardFloat);
				Debug.Log("Reward Multi: " + Mathf.CeilToInt(rewardMultiplier/2) + ", Max Reward: " + maxRewardInt + ", CeilFinish Pos/2: " + Mathf.CeilToInt(((finishPos + 1f)/2f)));
				int rewardRatio = Mathf.CeilToInt((Mathf.CeilToInt(rewardMultiplier/2f) * maxRewardInt) / Mathf.CeilToInt((finishPos + 1f)/2f));
				if(rewardRatio >= 1){
					rewardMultiplier = rewardRatio;
					Debug.Log("New reward multiplier: " + rewardMultiplier);
					AssignPrizes(validDriver[Random.Range(0,validDriver.Count)], setPrize, rewardMultiplier);
				} else {
					carReward = "";
				}
				break;
			default:
				if(rewardGears > 0){
					//If rnd is less or equal to your rewardGears, you get car parts.
					float rnd = Random.Range(0,8);
					//Top 3 = guaranteed reward
					//Rewarded Gears = each Gear acts like a raffle ticket
					if((rnd <= rewardGears)||
					   (finishPos <= 3)){
						if(validDriver.Count > 0){
							AssignPrizes(validDriver[Random.Range(0,validDriver.Count)], setPrize, rewardMultiplier);
						} else {
							//Double the gears if no prize car available
							gears += rewardGears;
							PlayerPrefs.SetInt("Gears",gears);
							gearsTitle.GetComponent<TMPro.TMP_Text>().text = " +" + (rewardGears * rewardMultiplier) + rewardGears + " Gears (" + gears + ")";
						}
					} else {
						carReward = "";
					}
				} else {
					carReward = "";
				}
				break;
		}
		
		//Update UI
		if(championshipReward == true){
			rewardsTitle.GetComponent<TMPro.TMP_Text>().text = "Championship Rewards - " + MiscScripts.PositionPostfix(finishPos) + " Place";
		} else {
			rewardsTitle.GetComponent<TMPro.TMP_Text>().text = "Race Rewards";
		}
		
		partsTitle.GetComponent<TMPro.TMP_Text>().text = "" + carReward + "\n(" + carCurrentGears + ")";
		partsTitle.GetComponent<UIAnimate>().animOffset = 160;
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
		PlayerPrefs.DeleteKey("MomentComplete");
	}

	void AssignPrizes(string carId, string setPrize, int multiplier){
		Debug.Log("Set Prize: " + setPrize);
		if(!PlayerPrefs.HasKey(carId + "Gears")){
			PlayerPrefs.SetInt(carId + "Gears", multiplier);
		}
		int carGears = PlayerPrefs.GetInt(carId + "Gears");
		string seriesPrefix = carId.Substring(0,5);
		int carNum = int.Parse(carId.Substring(5));
		if(int.Parse(setPrize) > 1){
			//Likely has a big fixed prize set e.g. 35 car parts
			PlayerPrefs.SetInt(carId + "Gears", carGears + int.Parse(setPrize));
			
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
		PlayerPrefs.DeleteKey("SeriesPrizeAmt");
	}
	
	void UnlockAltPaint(string setPrize){
		//Win an alt paint rather than car parts
		//setPrize format example: cup20livery48alt2
		
		string sanitisedAlt = setPrize.Replace("livery","");
		sanitisedAlt = sanitisedAlt.Replace("alt","Alt");
		
		//Sanitised example: cup2048Alt2Unlocked
		
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
	
	List<string> ListPrizeOptions(string category){
		List<string> prizeOptions = new List<string>();
		Debug.Log("Looping through reward options.. Type: " + category);
		switch(category){
			//Team Rewards
			case "cup20":
			case "cup22":
			case "cup23":
			case "cup24":
			case "irl23":
			case "irl24":
			case "dmc15":
			case "irc00":
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					if(seriesPrefix == category){
						for(int j=0;j<=99;j++){
							if(DriverNames.getName(seriesPrefix,j) != null){
								if(PlayerPrefs.GetInt(seriesPrefix + j + "Class") < 6){
									prizeOptions.Add("" + seriesPrefix + j + "");
								}
							}
						}
					}
				}
			break;
			case "Rookies":
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					Debug.Log("Searching " + seriesPrefix);
					for(int j=0;j<=99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getType(seriesPrefix,j) == "Rookie"){
								if(PlayerPrefs.GetInt(seriesPrefix + j + "Class") < 6){
									Debug.Log("Adding " + seriesPrefix + j + "");
									prizeOptions.Add("" + seriesPrefix + j + "");
								}
							}
						}
					}
				}
			break;
			case "Rarity1":
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<=99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getRarity(seriesPrefix,j) == 1){
								if(PlayerPrefs.GetInt(seriesPrefix + j + "Class") < 6){
									prizeOptions.Add("" + seriesPrefix + j + "");
								}
							}
						}
					}
				}
			break;
			case "Rarity2":
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<=99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getRarity(seriesPrefix,j) == 2){
								if(PlayerPrefs.GetInt(seriesPrefix + j + "Class") < 6){
									prizeOptions.Add("" + seriesPrefix + j + "");
								}
							}
						}
					}
				}
			break;
			case "Rarity3":
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<=99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getRarity(seriesPrefix,j) == 3){
								if(PlayerPrefs.GetInt(seriesPrefix + j + "Class") < 6){
									prizeOptions.Add("" + seriesPrefix + j + "");
								}
							}
						}
					}
				}
			break;
			case "Rarity4":
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<=99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getRarity(seriesPrefix,j) == 4){
								if(PlayerPrefs.GetInt(seriesPrefix + j + "Class") < 6){
									prizeOptions.Add("" + seriesPrefix + j + "");
								}
							}
						}
					}
				}
			break;
			
			//Manufacturer Rewards
			case "CHV":
			case "FRD":
			case "TYT":
			case "HON":
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<=99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getManufacturer(seriesPrefix,j) == category){
								if(PlayerPrefs.GetInt(seriesPrefix + j + "Class") < 6){
									prizeOptions.Add("" + seriesPrefix + j + "");
								}
							}
						}
					}
				}
			break;
			
			//Manufacturer Rewards (Capped at 1*)
			case "CHV1":
			case "FRD1":
			case "TYT1":
			case "HON1":
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<=99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getManufacturer(seriesPrefix,j) == category.Substring(0,3)){
								if(DriverNames.getRarity(seriesPrefix,j) == 1){
									if(PlayerPrefs.GetInt(seriesPrefix + j + "Class") < 6){
										prizeOptions.Add("" + seriesPrefix + j + "");
									}
								}
							}
						}
					}
				}
			break;
			
			//Team Rewards
			case "IND":
			case "SPI":
			case "RWR":
			case "FRM":
			case "RFR":
			case "RFK":
			case "TRK":
			case "RCR":
			case "CGR":
			case "SHR":
			case "PEN":
			case "JGR":
			case "HEN":
			case "KAU":
			case "23X":
			case "LMC":
			case "FOY":
			case "AND":
			case "ARR":
			case "DCR":
			case "DRE":
			case "EDC":
			case "JUN":
			case "MEY":
			case "RLL":
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<=99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getTeam(seriesPrefix,j) == category){
								if(PlayerPrefs.GetInt(seriesPrefix + j + "Class") < 6){
									prizeOptions.Add("" + seriesPrefix + j + "");
								}
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
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<=99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getType(seriesPrefix,j) == category){
								if(PlayerPrefs.GetInt(seriesPrefix + j + "Class") < 6){
									prizeOptions.Add("" + seriesPrefix + j + "");
								}
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
				prizeOptions.Add("cup2048");
				setPrize = PlayerPrefs.GetString("SeriesPrizeAmt");
			break;
			
			case "Earnhardt":
				prizeOptions.Add("cup203");
				setPrize = PlayerPrefs.GetString("SeriesPrizeAmt");
			break;
			
			case "Harvick":
				prizeOptions.Add("cup234");
			break;
			case "Logano":
				prizeOptions.Add("cup2222");
				setPrize = PlayerPrefs.GetString("SeriesPrizeAmt");
			break;
			
			case "Elliott":
				prizeOptions.Add("cup229");
				setPrize = PlayerPrefs.GetString("SeriesPrizeAmt");
			break;
			
			case "Bell":
				prizeOptions.Add("cup2220");
				setPrize = PlayerPrefs.GetString("SeriesPrizeAmt");
			break;
			
			case "Chastain":
				prizeOptions.Add("cup221");
				setPrize = PlayerPrefs.GetString("SeriesPrizeAmt");
			break;
			
			case "Suarez":
				prizeOptions.Add("cup2096");
				prizeOptions.Add("cup2299");
				prizeOptions.Add("cup2399");
				prizeOptions.Add("cup2499");
				setPrize = PlayerPrefs.GetString("SeriesPrizeAmt");
			break;
			
			default:
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<=99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(PlayerPrefs.GetInt(seriesPrefix + j + "Class") < 6){
								prizeOptions.Add("" + seriesPrefix + j + "");
							}
						}
					}
				}
			break;
		}
		return prizeOptions;
	}
	
	int getChampionshipPosition(){
		//Debug.Log("LOOKING FOR CHAMPIONSHIP POSITION");
		string playerCarNumber = PlayerPrefs.GetString("carTexture");
		string splitAfter = "livery";
		playerCarNumber = playerCarNumber.Substring(playerCarNumber.IndexOf(splitAfter) + splitAfter.Length);
		int carNumber = int.Parse(playerCarNumber);
		//Debug.Log("CAR NUMBER IS " + carNumber.ToString());
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
				//Debug.Log("CHAMPIONSHIP POSITION IS " + champPosition);
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
