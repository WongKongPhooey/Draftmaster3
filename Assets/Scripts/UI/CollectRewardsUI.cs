using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CollectRewardsUI : MonoBehaviour
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
    	string prizeType;
	string prizeCarString;
	string prizeCarSeries;
	int prizeCarNumber;
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
        prizeType=PlayerPrefs.GetString("PrizeType");
        Debug.Log(prizeType);

        List<string> validRewards;
        switch(prizeType){
            case "MysteryGarage":
                validRewards = getValidRewards("");
                AssignPrizes(validRewards[Random.Range(0,validDriver.Count)], setPrize, rewardMultiplier);
                break;
            case "FreeDaily":
                validRewards = getValidRewards("");
                AssignPrizes(validRewards[Random.Range(0,validDriver.Count)], setPrize, rewardMultiplier);
                break;
            default:
                break;
        }

		rewardsTitle = GameObject.Find("Title");
		partsTitle = GameObject.Find("PartsTitle");
		partsCar = GameObject.Find("PartsCar");
		
		partsTitle.GetComponent<TMPro.TMP_Text>().text = "" + carReward + "\n(" + carCurrentGears + ")";
		partsTitle.GetComponent<UIAnimate>().animOffset = 160;
		if(carReward != ""){
			partsTitle.GetComponent<UIAnimate>().scaleIn();
			
			if(carPrizeNumAlt != ""){
				//partsCar.GetComponent<RawImage>().texture = Resources.Load<Texture2D>(carPrizeAlt);
			} else {
				//partsCar.GetComponent<RawImage>().texture = Resources.Load<Texture2D>(carPrize);
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
	
	List<string> getValidRewards(string category){
        List<string> validRewards = null;
		switch(category){
			//Team Rewards
			case "cup20":
			case "cup22":
			case "cup23":
			case "cup24":
			case "irl23":
			case "dmc15":
			case "irc00":
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					if(seriesPrefix == category){
						for(int j=0;j<99;j++){
							if(DriverNames.getName(seriesPrefix,j) != null){
								if(PlayerPrefs.GetInt(seriesPrefix + j + "Class") < 6){
                                    validRewards.Add("" + seriesPrefix + j + "");
                                }
								//Debug.Log(category + " Added: #" + i);
							}
						}
					}
				}
			break;
			case "Rookies":
				//for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getType(seriesPrefix,j) == "Rookie"){
								if(PlayerPrefs.GetInt(seriesPrefix + j + "Class") < 6){
                                    validRewards.Add("" + seriesPrefix + j + "");
                                }
							}
						}
					}
				}
			break;
			case "Rarity1":
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getRarity(seriesPrefix,j) == 1){
								if(PlayerPrefs.GetInt(seriesPrefix + j + "Class") < 6){
                                    validRewards.Add("" + seriesPrefix + j + "");
                                }
							}
						}
					}
				}
			break;
			case "Rarity2":
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getRarity(seriesPrefix,j) == 2){
								if(PlayerPrefs.GetInt(seriesPrefix + j + "Class") < 6){
                                    validRewards.Add("" + seriesPrefix + j + "");
                                }
							}
						}
					}
				}
			break;
			case "Rarity3":
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getRarity(seriesPrefix,j) == 3){
								if(PlayerPrefs.GetInt(seriesPrefix + j + "Class") < 6){
                                    validRewards.Add("" + seriesPrefix + j + "");
                                }
							}
						}
					}
				}
			break;
			case "Rarity4":
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getRarity(seriesPrefix,j) == 4){
								if(PlayerPrefs.GetInt(seriesPrefix + j + "Class") < 6){
                                    validRewards.Add("" + seriesPrefix + j + "");
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
					for(int j=0;j<99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getManufacturer(seriesPrefix,j) == category){
								if(PlayerPrefs.GetInt(seriesPrefix + j + "Class") < 6){
                                    validRewards.Add("" + seriesPrefix + j + "");
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
					for(int j=0;j<99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getManufacturer(seriesPrefix,j) == category.Substring(0,3)){
								if(DriverNames.getRarity(seriesPrefix,j) == 1){
									if(PlayerPrefs.GetInt(seriesPrefix + j + "Class") < 6){
                                        validRewards.Add("" + seriesPrefix + j + "");
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
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getTeam(seriesPrefix,j) == category){
								if(PlayerPrefs.GetInt(seriesPrefix + j + "Class") < 6){
                                    validRewards.Add("" + seriesPrefix + j + "");
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
					for(int j=0;j<99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(DriverNames.getType(seriesPrefix,j) == category){
								if(PlayerPrefs.GetInt(seriesPrefix + j + "Class") < 6){
                                    validRewards.Add("" + seriesPrefix + j + "");
                                }
							}
						}
					}
				}
			    break;			
			default:
				for(int i=0;i<DriverNames.allWinnableCarsets.Length;i++){
					string seriesPrefix = DriverNames.allWinnableCarsets[i];
					for(int j=0;j<99;j++){
						if(DriverNames.getName(seriesPrefix,j) != null){
							if(PlayerPrefs.GetInt(seriesPrefix + j + "Class") < 6){
                               validRewards.Add("" + seriesPrefix + j + "");
                            }    
						}
					}
				}
			break;
		}
        return validRewards;
	}
	
	public static int randCarPartsAmt(int maxParts){

        int randAmt = 0;
        if(maxParts > 15){
		    int[] randAmtSet = {10,10,10,12,12,12,15,15,15,18,18,20,25};
		    randAmt = randAmtSet[Random.Range(0,(randAmtSet.Length)-1)];
        } else {
            randAmt = Random.Range(3,13);
        }
        return randAmt;
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
