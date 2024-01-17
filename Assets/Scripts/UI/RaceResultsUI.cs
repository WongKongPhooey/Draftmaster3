using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class RaceResultsUI : MonoBehaviour
{
	
	int exp;
	int level;
	int levelExp;
	float raceExp;
	
	public GameObject resultRow;
	public Transform resultsFrame;
	public string seriesPrefix;
	bool officialSeries;
	
	string playerCarNumber;
	string carNumber;

	int fieldSize;
	
	string currentSeriesIndex;

	int playerMoney;
	int moneyCount;
	int raceWinnings;
	int winningsMultiplier;
	int lapsBonus;

	public static bool challengeComplete;
	
    // Start is called before the first frame update
    void Start(){
		
		PlayerPrefs.DeleteKey("ActivePath");
		PlayerPrefs.DeleteKey("MidRaceLoading");

		playerCarNumber = PlayerPrefs.GetString("carTexture");
		string splitAfter = "livery";
		playerCarNumber = playerCarNumber.Substring(playerCarNumber.IndexOf(splitAfter) + splitAfter.Length);

		if(PlayerPrefs.HasKey("FixedSeries")){
			seriesPrefix = PlayerPrefs.GetString("FixedSeries");
		} else {
			seriesPrefix = PlayerPrefs.GetString("carSeries");
		}

		if(DriverNames.isOfficialSeries(seriesPrefix) == true){
			officialSeries = true;
		} else {
			Debug.Log("Must be a mod.. " + seriesPrefix);
		}

		currentSeriesIndex = PlayerPrefs.GetString("CurrentSeriesIndex");

		PlayerPrefs.SetInt("FinishPos",(Ticker.position + 1));

		exp = PlayerPrefs.GetInt("Exp");
		level = PlayerPrefs.GetInt("Level");

		//Add XP and Increment Championship Round
		if(PlayerPrefs.GetInt("ExpAdded") == 0){
			
			int level = PlayerPrefs.GetInt("Level");
			//Debug.Log("Level " + level);
			int levelExp = levelUpExp(level);
			
			int AILevel = PlayerPrefs.GetInt("RaceAILevel");
			
			//Example 1st = 110, 5th = 30, 10th = 20, 30th = 13
			raceExp = Mathf.Round(((90 / (Ticker.position + 1)) + 10) * (float)(1 + (float)(AILevel / 10f)));
			//Debug.Log(Mathf.Round(((90 / (Ticker.position + 1)) + 10)));
			//Debug.Log((float)(1 + (float)(AILevel / 10f)));
			//Example 1st = 100, 10th = 70, 20th = 40, 30th = 10
			//raceExp = ((30 - Ticker.position) * 3) + 10;
			exp += Mathf.RoundToInt(raceExp);
			PlayerPrefs.SetInt("Exp",exp);
			//Debug.Log("Exp: " + exp);
			
			PlayerPrefs.SetString("ExpInfo","+" + raceExp + " (" + exp + "/" + levelExp + ")");

			if(ModData.isModSeries(seriesPrefix) == true){
				GameObject.Find("NextButton").GetComponent<NavButton>().sceneName = "Menus/MainMenu";
			}
			
			//Is this a championship round?
			if(PlayerPrefs.GetString("RaceType") == "Championship"){
				//Debug.Log("Yeah.. this is a championship event");
				//Re-route to avoid the Race Rewards mid-season
				GameObject.Find("NextButton").GetComponent<NavButton>().sceneName = "Menus/ChampionshipHub";
					
				//Increment Championship Round
				int championshipRound = PlayerPrefs.GetInt("SeriesChampionship" + currentSeriesIndex + "Round");
				PlayerPrefs.SetInt("SeriesChampionship" + currentSeriesIndex + "Round",championshipRound+1);
				Debug.Log("Next Round " + PlayerPrefs.GetInt("SeriesChampionship" + currentSeriesIndex + "Round"));
				
				fieldSize = PlayerPrefs.GetInt("FieldSize");
				
				//Debug.Log("Add Championship Points. Next Round Is " + (championshipRound+1));
				RacePoints.setCupPoints();
				for( int i=0; i < fieldSize; i++){
					if(Ticker.carNames[i] == null){
						//Exit loop
						Debug.Log("No name here, skip adding points #" + i);
						continue;
					}
					if(i == (Ticker.position)){
						carNumber = playerCarNumber;
					} else {
						carNumber = Ticker.carNames[i].Remove(0,6);
					}
					//Debug.Log("Add " + RacePoints.placePoints[i] + " points");
					addChampionshipPoints(carNumber, RacePoints.placePoints[i]);
				}
			}
			PlayerPrefs.SetInt("ExpAdded",1);
		}
		
		//If Event Reward already collected, cannot claim again
		if(PlayerPrefs.HasKey("EventReplay")){
			//Debug.Log("Event replay, no dupe rewards");
			GameObject.Find("NextButton").GetComponent<NavButton>().sceneName = "Menus/MainMenu";
		}

		winningsMultiplier = 1;
				
		string currentTrack = PlayerPrefs.GetString("CurrentTrack");

		if(PlayerPrefs.GetString("RaceType") == "Event"){
			Debug.Log("This is an Event Race (Challenge), set track index to 1");
			currentTrack = "1";
		}

		if(PlayerPrefs.HasKey("BestFinishPosition" + currentSeriesIndex+ currentTrack) == true){
			int bestFinishPos = PlayerPrefs.GetInt("BestFinishPosition" + currentSeriesIndex + currentTrack);
			//If better than previous (or 0 if it glitched a result)
			if(((Ticker.position + 1) < bestFinishPos)||(bestFinishPos == 0)){
				PlayerPrefs.SetInt("BestFinishPosition" + currentSeriesIndex + currentTrack + "", Ticker.position + 1);
				Debug.Log("New best finish: " + (Ticker.position + 1) + "Track: " + currentSeriesIndex + currentTrack);
			}
			Debug.Log("Prev best finish: " + bestFinishPos + "Track: " + currentSeriesIndex + currentTrack);
		} else {
			PlayerPrefs.SetInt("BestFinishPosition" + currentSeriesIndex + currentTrack + "", Ticker.position + 1);
			Debug.Log("New best finish: " + Ticker.position + ". Track: " + currentSeriesIndex + currentTrack);
		}

		moneyCount = 0;
		playerMoney = PlayerPrefs.GetInt("PrizeMoney");
		raceWinnings = PrizeMoney.cashAmount[Ticker.position] * winningsMultiplier;

		if(ChallengeSelectGUI.challengeMode == false){
			lapsBonus = PlayerPrefs.GetInt("RaceLapsMultiplier");
			winningsMultiplier *= lapsBonus;
		} else {
			lapsBonus = 1;
		}

		if ((ChallengeSelectGUI.challengeMode == false)||(challengeComplete == true)){
			PlayerPrefs.SetInt("raceWinnings",raceWinnings);
		} else {
			PlayerPrefs.SetInt("raceWinnings",0);
			raceWinnings = 0;
		}

		loadResults();
		
		//Update starts leaderboard
		PlayFabManager.SendLeaderboard(PlayerPrefs.GetInt("TotalStarts"), "AllTimeMostStarts","");
    }

	public void loadResults(){
		
		//Reset results to blank
		foreach (Transform child in resultsFrame){
			Destroy(child.gameObject);
		}
		
		if(PlayerPrefs.HasKey("FixedSeries")){
			seriesPrefix = PlayerPrefs.GetString("FixedSeries");
			Debug.Log("Series " + seriesPrefix);
		} else {
			seriesPrefix = PlayerPrefs.GetString("carSeries");
		}
		
		int fieldSize = PlayerPrefs.GetInt("FieldSize");
		//Debug.Log("Field Size: " + fieldSize);
		for(int i=0;i<fieldSize;i++){
			
			int carNum = 999;
			float carDist = 999.999f;
			if(!PlayerPrefs.HasKey("FinishPosition" + i + "")){
				break;
			} else {
				carNum = PlayerPrefs.GetInt("FinishPosition" + i + "");
				carDist = PlayerPrefs.GetInt("FinishTime" + i + "");
				//Debug.Log("Pos: " + i + " Num: " + carNum);
			}
			
			GameObject resultInst = Instantiate(resultRow, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
			RectTransform resultObj = resultInst.GetComponent<RectTransform>();
			resultInst.transform.SetParent(resultsFrame, false);
			resultInst.GetComponent<UIAnimate>().animOffset = i+1;
			resultInst.GetComponent<UIAnimate>().scaleIn();
			
			TMPro.TMP_Text resultPos = resultInst.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
			RawImage resultNumber = resultInst.transform.GetChild(1).GetComponent<RawImage>();
			TMPro.TMP_Text resultFallbackNumber = resultInst.transform.GetChild(2).GetComponent<TMPro.TMP_Text>();
			TMPro.TMP_Text resultDriver = resultInst.transform.GetChild(3).GetComponent<TMPro.TMP_Text>();
			RawImage resultManu = resultInst.transform.GetChild(4).GetComponent<RawImage>();
			TMPro.TMP_Text resultTime = resultInst.transform.GetChild(5).GetComponent<TMPro.TMP_Text>();
			
			resultPos.text = (i+1).ToString();
			if (Resources.Load<Texture2D>("cup20num" + carNum) != null) {
				resultNumber.texture = Resources.Load<Texture2D>("cup20num" + carNum);
				resultFallbackNumber.enabled = false;
			} else {
				resultNumber.enabled = false;
				resultFallbackNumber.text = carNum.ToString();
			}
			if(officialSeries == true){
				resultDriver.text = DriverNames.getName(seriesPrefix,carNum);
				resultManu.texture = Resources.Load<Texture2D>("Icons/manu-" + DriverNames.getManufacturer(seriesPrefix, carNum));
			} else {
				carNum = ModData.getJsonIndexFromCarNum(seriesPrefix,carNum);
				resultDriver.text = ModData.getName(seriesPrefix,carNum);
				string carManu = ModData.getManufacturer(seriesPrefix, carNum);
				if(DriverNames.isOfficialManu(carManu) == true){
					resultManu.texture = Resources.Load<Texture2D>("Icons/manu-" + carManu);
				} else {
					resultManu.texture = ModData.getManuTexture(seriesPrefix, carManu); 
				}
			}
			if(i==0){
				resultTime.text = "";
			} else {
				if((carDist > 32000f)||((i == (fieldSize - 1))&&(fieldSize > 16))){
					resultTime.text = "+1 LAP";
				} else {
					resultTime.text = "+" + (carDist / 1000f).ToString("f3");
				}
			}
		}
	}

	void addChampionshipPoints(string carNumber, int points){
		int currentPoints = PlayerPrefs.GetInt("SeriesChampionship" + currentSeriesIndex + "Points"  + carNumber);
		PlayerPrefs.SetInt("SeriesChampionship" + currentSeriesIndex + "Points"  + carNumber, currentPoints + points);
		//Debug.Log("Car #" + carNumber + " - Points:" + (currentPoints + " + " + points));
	}

	public static int levelUpExp(int level){
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
				return 5800;
				break;
			case 37:
				return 6500;
				break;
			case 38:
				return 7200;
				break;
			case 39:
				return 8000;
				break;
			case 40:
				return 9000;
				break;
			case 41:
				return 10000;
				break;
			case 42:
				return 11200;
				break;
			case 43:
				return 12500;
				break;
			case 44:
				return 14000;
				break;
			case 45:
				return 16000;
				break;
			default:
				return 9999999;
				break;
		}
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
