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

		currentSeriesIndex = PlayerPrefs.GetString("CurrentSeriesIndex");

		PlayerPrefs.SetInt("FinishPos",(Ticker.position + 1));

		exp = PlayerPrefs.GetInt("Exp");
		level = PlayerPrefs.GetInt("Level");

		//Add XP and Increment Championship Round
		if(PlayerPrefs.GetInt("ExpAdded") == 0){
			//Example 1st = 110, 5th = 30, 10th = 20, 30th = 13
			raceExp = (100 / (Ticker.position + 1)) + 10;
			
			//Example 1st = 100, 10th = 70, 20th = 40, 30th = 10
			//raceExp = ((30 - Ticker.position) * 3) + 10;
			exp += Mathf.RoundToInt(raceExp);
			PlayerPrefs.SetInt("Exp",exp);
			Debug.Log("Exp: " + exp);
			
			//Is this a championship round?
			if(PlayerPrefs.HasKey("ChampionshipSubseries")){
				//Debug.Log("Yeah.. there's a championship around");
				if(PlayerPrefs.GetString("ChampionshipSubseries") == currentSeriesIndex){
					//Debug.Log("This is the active championship");
					//Re-route to avoid the Race Rewards mid-season
					GameObject.Find("NextButton").GetComponent<NavButton>().sceneName = "Menus/ChampionshipHub";
					
					//Increment Championship Round
					int championshipRound = PlayerPrefs.GetInt("ChampionshipRound");
					PlayerPrefs.SetInt("ChampionshipRound",championshipRound+1);
					
					fieldSize = PlayerPrefs.GetInt("FieldSize");
					
					//Debug.Log("Add Championship Points. Next Round Is " + (championshipRound+1));
					//Debug.Log("Field Size Loop: " + fieldSize);
					RacePoints.setCupPoints();
					for( int i=0; i < fieldSize; i++){
						if(Ticker.carNames[i] == null){
							//Exit loop
							//Debug.Log("No name here, skip loop #" + i);
							continue;
						}
						if(i == (Ticker.position)){
							//Debug.Log("We finished P" + i);
							carNumber = playerCarNumber;
						} else {
							carNumber = Ticker.carNames[i].Remove(0,6);
						}
						//Debug.Log("Add " + RacePoints.placePoints[i] + " points");
						addChampionshipPoints(carNumber, RacePoints.placePoints[i]);
					}
				} else {
					//Debug.Log("This isn't an active championship race.");
				}
			} else {
				//Debug.Log("No Active Championship Exists");
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
		
		for(int i=0;i<fieldSize;i++){
			
			int carNum = 999;
			float carDist = 999.999f;
			if(!PlayerPrefs.HasKey("FinishPosition" + i + "")){
				break;
			} else {
				carNum = PlayerPrefs.GetInt("FinishPosition" + i + "");
				carDist = PlayerPrefs.GetInt("FinishTime" + i + "");
			}
			
			GameObject resultInst = Instantiate(resultRow, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
			RectTransform resultObj = resultInst.GetComponent<RectTransform>();
			resultInst.transform.SetParent(resultsFrame, false);
			resultInst.GetComponent<UIAnimate>().animOffset = i+1;
			resultInst.GetComponent<UIAnimate>().scaleIn();
			
			TMPro.TMP_Text resultPos = resultInst.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
			RawImage resultNumber = resultInst.transform.GetChild(1).GetComponent<RawImage>();
			TMPro.TMP_Text resultDriver = resultInst.transform.GetChild(2).GetComponent<TMPro.TMP_Text>();
			RawImage resultManu = resultInst.transform.GetChild(3).GetComponent<RawImage>();
			TMPro.TMP_Text resultTime = resultInst.transform.GetChild(4).GetComponent<TMPro.TMP_Text>();
			
			resultPos.text = (i+1).ToString();
			resultNumber.texture = Resources.Load<Texture2D>("cup20num" + carNum);
			resultDriver.text = DriverNames.getName(seriesPrefix,carNum);
			resultManu.texture = Resources.Load<Texture2D>("Icons/manu-" + DriverNames.getManufacturer(seriesPrefix, carNum));
			if(i==0){
				resultTime.text = "";
			} else {
				resultTime.text = "+" + (carDist / 1000f).ToString("f3");
			}
		}
	}

	void addChampionshipPoints(string carNumber, int points){
		int currentPoints = PlayerPrefs.GetInt("ChampionshipPoints" + carNumber + "");
		PlayerPrefs.SetInt("ChampionshipPoints" + carNumber + "", currentPoints + points);
		Debug.Log("Car #" + carNumber + " - Points:" + (currentPoints + " + " + points));
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
