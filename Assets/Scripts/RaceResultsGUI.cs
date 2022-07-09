using UnityEngine;
//using UnityEngine.Advertisements;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;

public class RaceResultsGUI : MonoBehaviour {

	public GUISkin eightBitSkin;
	public Texture2D yellowBox;
	
	float widthblock = Screen.width/20;
	float heightblock = Screen.height/20;

	int exp;
	int level;
	int levelExp;
	float raceExp;

	string seriesPrefix;

	string playerCarNumber;
	string carNumber;

	int resultsRows;
	
	string currentSeriesIndex;

	int playerMoney;
	int moneyCount;
	int raceWinnings;
	int winningsMultiplier;
	int lapsBonus;

	public static bool challengeComplete;

	public Vector2 scrollPosition = Vector2.zero;
	
	void Awake(){

		widthblock = Screen.width/20;
		heightblock = Screen.height/20;

		resultsRows = PlayerPrefs.GetInt("FieldSize");
		if(resultsRows > 43){
			resultsRows = 43;
		}
		Debug.Log("Total finishers: " + resultsRows);

		playerCarNumber = PlayerPrefs.GetString("carTexture");
		string splitAfter = "livery";
		playerCarNumber = playerCarNumber.Substring(playerCarNumber.IndexOf(splitAfter) + splitAfter.Length);

		if(PlayerPrefs.HasKey("FixedSeries")){
			seriesPrefix = PlayerPrefs.GetString("FixedSeries");
		} else {
			seriesPrefix = PlayerPrefs.GetString("carSeries");
		}

		currentSeriesIndex = PlayerPrefs.GetString("CurrentSeriesIndex");

		challengeComplete = false;

		PlayerPrefs.SetInt("FinishPos",(Scoreboard.position + 1));

		exp = PlayerPrefs.GetInt("Exp");
		level = PlayerPrefs.GetInt("Level");
		
		//Add XP and Increment Championship Round
		if(PlayerPrefs.GetInt("ExpAdded") == 0){
			raceExp = 100 / (Scoreboard.position + 1);
			exp += Mathf.RoundToInt(raceExp);
			PlayerPrefs.SetInt("Exp",exp);
			Debug.Log("Exp: " + exp);
			
			//Is this a championship round?
			if(PlayerPrefs.HasKey("ChampionshipSubseries")){
				if(PlayerPrefs.GetString("ChampionshipSubseries") == currentSeriesIndex){
					
					//Increment Championship Round
					int championshipRound = PlayerPrefs.GetInt("ChampionshipRound");
					PlayerPrefs.SetInt("ChampionshipRound",championshipRound+1);
					
					Debug.Log("Add Championship Points. Next Round Is " + (championshipRound+1));
					RacePoints.setCupPoints();
					for( int i=0; i < resultsRows; i++){
						if(Scoreboard.carNames[i] == null){
							//Exit loop
							break;
						}
						if(i == (Scoreboard.position)){
							carNumber = playerCarNumber;
						} else {
							carNumber = Scoreboard.carNames[i].Remove(0,6);
						}
						//Debug.Log("Add " + RacePoints.placePoints[i] + " points");
						addChampionshipPoints(carNumber, RacePoints.placePoints[i]);
					}
				}
			}
			PlayerPrefs.SetInt("ExpAdded",1);
		}

		winningsMultiplier = 1;
				
		string currentTrack = PlayerPrefs.GetString("CurrentTrack");

		if(PlayerPrefs.HasKey("BestFinishPosition" + currentSeriesIndex+ currentTrack) == true){
			int bestFinishPos = PlayerPrefs.GetInt("BestFinishPosition" + currentSeriesIndex+ currentTrack);
			if(((Scoreboard.position + 1) < bestFinishPos)||(bestFinishPos == 0)){
				PlayerPrefs.SetInt("BestFinishPosition" + currentSeriesIndex + currentTrack + "", Scoreboard.position + 1);
			}
			Debug.Log("Prev best finish: " + bestFinishPos + "Track: " + currentSeriesIndex + currentTrack);
		} else {
			PlayerPrefs.SetInt("BestFinishPosition" + currentSeriesIndex + currentTrack + "", Scoreboard.position + 1);
			Debug.Log("New best finish: " + Scoreboard.position + ". Track: " + currentSeriesIndex + currentTrack);
		}

		switch(PlayerPrefs.GetString("ChallengeType")){
		case "LastToFirstLaps":
			if((Scoreboard.position) == 1){
				winningsMultiplier = 5;
				challengeComplete = true;
			}
			break;
		case "NoFuel":
			winningsMultiplier = 5;
			challengeComplete = true;
			if(Scoreboard.position < PlayerPrefs.GetInt("NoFuelRecord")){
				PlayerPrefs.SetInt("NoFuelRecord",Scoreboard.position);
			}
			break;
		case "LatePush":
			if((Scoreboard.position) <= 3){
				challengeComplete = true;
			}
			if(Scoreboard.position < PlayerPrefs.GetInt("LatePushRecord")){
				PlayerPrefs.SetInt("LatePushRecord",Scoreboard.position);
			}
			break;
		case "TeamPlayer":
			if(PlayerPrefs.GetInt("DraftPercent")>=30){
				winningsMultiplier = 5;
				challengeComplete = true;
			}
			break;
		case "CleanBreak":
			if((Scoreboard.position == 1)){
				if(((Scoreboard.carPositions[0] - Scoreboard.carPositions[1]) / 25) > 0.15){
					challengeComplete = true;
					if((((Scoreboard.carPositions[0] - Scoreboard.carPositions[1]) / 25)*1000) > PlayerPrefs.GetInt("CleanBreakRecord")){
						string gap = (((Scoreboard.carPositions[0] - Scoreboard.carPositions[1]) / 25)*1000).ToString("F0");
						PlayerPrefs.SetInt("CleanBreakRecord",int.Parse(gap));
					}
				}
			}
			break;
		case "TrafficJam":
			if((Scoreboard.position) == 1){
				winningsMultiplier = 5;
				challengeComplete = true;
			}
			break;
		case "PhotoFinish":
			if((Scoreboard.position == 1)){
				if(((Scoreboard.carPositions[0] - Scoreboard.carPositions[1]) / 25) < 0.1){
					challengeComplete = true;
					if((((Scoreboard.carPositions[0] - Scoreboard.carPositions[1]) / 25)*1000) < PlayerPrefs.GetInt("PhotoFinishRecord")){
						string gap = (((Scoreboard.carPositions[0] - Scoreboard.carPositions[1]) / 25)*1000).ToString("F0");
						PlayerPrefs.SetInt("PhotoFinishRecord",int.Parse(gap));
					}
				}
			}
			break;
		case "AllWoundUp":
			if((Scoreboard.position == 1)){
				if(PlayerPrefs.GetInt("TotalRivals") >= 0){
					challengeComplete = true;
					if(!PlayerPrefs.HasKey("AllWoundUpRecord")){
						PlayerPrefs.SetInt("AllWoundUpRecord",PlayerPrefs.GetInt("TotalRivals"));
					} else {
						if(PlayerPrefs.GetInt("TotalRivals") > PlayerPrefs.GetInt("AllWoundUpRecord")){
							PlayerPrefs.SetInt("AllWoundUpRecord",PlayerPrefs.GetInt("TotalRivals"));
						}
					}
				}
			}
			break;
		}
		moneyCount = 0;
		playerMoney = PlayerPrefs.GetInt("PrizeMoney");
		raceWinnings = PrizeMoney.cashAmount[Scoreboard.position] * winningsMultiplier;

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
	}

	void FixedUpdate(){
	}

	void OnGUI() {

		GUI.skin = eightBitSkin;
		string carDriver;
		float windowscroll;

		windowscroll = (resultsRows + 5) * 0.1f;
		//windowscroll = 4.5f;

		GUI.skin.verticalScrollbar.fixedWidth = Screen.width / 20;
		GUI.skin.verticalScrollbarThumb.fixedWidth = Screen.width / 20;

		GUI.skin.label.fontSize = 72 / FontScale.fontScale;

		GUI.skin.label.normal.textColor = Color.black;

		scrollPosition = GUI.BeginScrollView(new Rect(0, 0, Screen.width, Screen.height), scrollPosition, new Rect(0, 0, Screen.width - widthblock, Screen.height * windowscroll));

		GUI.skin.label.alignment = TextAnchor.MiddleLeft;

		for( int i=0; i < resultsRows; i++){
			
			if(Scoreboard.carNames[i] == null){
				//Exit loop
				break;
			}
			
			if(i == (Scoreboard.position)){
				carNumber = playerCarNumber;
				if(PlayerPrefs.HasKey("CustomNumber" + seriesPrefix + carNumber)){
					int customNum = PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + playerCarNumber);
					GUI.DrawTexture(new Rect(widthblock * 5, (heightblock * (i * 2)) + (heightblock * 2), heightblock * 3f, heightblock * 1.5f), Resources.Load(seriesPrefix + "livery" + playerCarNumber + "blank") as Texture);
					GUI.DrawTexture(new Rect((widthblock * 5) + (((heightblock * 3f)/64)*34), (heightblock * (i * 2)) + (heightblock * 2) + ((heightblock * 1.5f)/4), heightblock * 0.75f, heightblock * 0.75f), Resources.Load(seriesPrefix + "num" + customNum) as Texture);
				} else {
					GUI.DrawTexture(new Rect(widthblock * 5, (heightblock * (i * 2)) + (heightblock * 2), heightblock * 3f, heightblock * 1.5f), Resources.Load(seriesPrefix + "livery" + playerCarNumber) as Texture);
				}
			} else {
				//Debug.Log("#" + i + " - " + Scoreboard.carNames[i]);
				carNumber = Scoreboard.carNames[i].Remove(0,6);
				if(PlayerPrefs.HasKey("CustomNumber" + seriesPrefix + carNumber)){
					int customNum = PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + carNumber);
					GUI.DrawTexture(new Rect(widthblock * 5, (heightblock * (i * 2)) + (heightblock * 2), heightblock * 3f, heightblock * 1.5f), Resources.Load(seriesPrefix + "livery" + carNumber + "blank") as Texture);
					GUI.DrawTexture(new Rect((widthblock * 5) + (((heightblock * 3f)/64)*34), (heightblock * (i * 2)) + (heightblock * 2) + ((heightblock * 1.5f)/4), heightblock * 0.75f, heightblock * 0.75f), Resources.Load(seriesPrefix + "num" + customNum) as Texture);
				} else {
					GUI.DrawTexture(new Rect(widthblock * 5, (heightblock * (i * 2)) + (heightblock * 2), heightblock * 3f, heightblock * 1.5f), Resources.Load(seriesPrefix + "livery" + carNumber) as Texture);
				}
			}

			if(i == (Scoreboard.position)){
				GUI.skin.label.normal.textColor = Color.red;
			}
			GUI.Label(new Rect(widthblock * 3.5f, (heightblock * (i * 2)) + (heightblock * 2), widthblock * 2, heightblock * 2), "P" + (i + 1));
			carDriver = DriverNames.getName(seriesPrefix, int.Parse(carNumber));
			
			if(i == (Scoreboard.position)){
				if(PlayerPrefs.HasKey(seriesPrefix + carNumber + "AltDriver")){
					GUI.Label(new Rect(widthblock * 9f, (heightblock * (i * 2)) + (heightblock * 2), widthblock * 7, heightblock * 2), PlayerPrefs.GetString(seriesPrefix + carNumber + "AltDriver"));
				} else {
					GUI.Label(new Rect(widthblock * 9f, (heightblock * (i * 2)) + (heightblock * 2), widthblock * 7, heightblock * 2), "" + carDriver + "");
				}	
			} else {
				GUI.Label(new Rect(widthblock * 9f, (heightblock * (i * 2)) + (heightblock * 2), widthblock * 7, heightblock * 2), "" + carDriver + "");
			} 
			if(i == (Scoreboard.position)){
				GUI.Label(new Rect(widthblock * 14, (heightblock * (i * 2)) + (heightblock * 2), widthblock * 3, heightblock * 2), "+" + (Scoreboard.leaderDist).ToString("F3"));
			} else {
				GUI.Label(new Rect(widthblock * 14, (heightblock * (i * 2)) + (heightblock * 2), widthblock * 3, heightblock * 2), "+" + (Scoreboard.carDist[i]).ToString("F3"));
			}	
			if(RacePoints.championshipMode == true){
				DriverPoints.pointsTotal[int.Parse(carNumber)] = PlayerPrefs.GetInt("ChampionshipPoints" + carNumber);
			}
			GUI.skin.label.normal.textColor = Color.black;
		}

		GUI.EndScrollView();

		GUI.DrawTexture(new Rect(0, 0,Screen.width - widthblock, heightblock * 2), yellowBox, ScaleMode.StretchToFill);

		GUI.skin.label.alignment = TextAnchor.UpperCenter;

		if(ChallengeSelectGUI.challengeMode == true){
			if (challengeComplete == true){
				GUI.Label(new Rect(widthblock * 4, heightblock/2, widthblock * 12, heightblock * 2), "Challenge Complete");
			} else {
				GUI.Label(new Rect(widthblock * 4, heightblock/2, widthblock * 12, heightblock * 2), "Challenge Failed");
			}
		} else {
			GUI.Label(new Rect(widthblock * 7, heightblock/2, widthblock * 6, heightblock * 2), "Race Results");
		}

		GUI.DrawTexture(new Rect(0, Screen.height - (heightblock * 4),Screen.width - widthblock, heightblock * 4), yellowBox, ScaleMode.StretchToFill);
		
		GUI.skin.button.fontSize = 72 / FontScale.fontScale;
		GUI.skin.label.alignment = TextAnchor.UpperLeft;
		GUI.skin.button.alignment = TextAnchor.MiddleCenter;

		if(PlayerPrefs.HasKey("PlayerUsername")){
			if(GUI.Button(new Rect(widthblock * 12, Screen.height - (heightblock * 3), widthblock * 6, heightblock * 2), "View Leaderboard")){
				Application.LoadLevel("Leaderboard");
			}
		} else {
			GUI.skin.label.fontSize = 48 / FontScale.fontScale;
			GUI.skin.label.alignment = TextAnchor.MiddleRight;
			GUI.Label(new Rect(widthblock * 10, Screen.height - (heightblock * 3), widthblock * 8, heightblock * 2), "Login for online leaderboards");
			GUI.skin.label.alignment = TextAnchor.UpperLeft;
			GUI.skin.label.fontSize = 72 / FontScale.fontScale;
		}

		if(GUI.Button(new Rect(widthblock * 2, Screen.height - (heightblock * 3), widthblock * 3, heightblock * 2), "Next")){
			ChallengeSelectGUI.challengeMode = false;
			
			int seriesLength = PlayerPrefs.GetInt("ChampionshipLength");
			int championshipRound = PlayerPrefs.GetInt("ChampionshipRound");
			
			//Is this a championship round?
			if((PlayerPrefs.HasKey("ChampionshipSubseries"))&&(PlayerPrefs.GetString("RaceType") == "Championship")){
				if(PlayerPrefs.GetString("ChampionshipSubseries") == currentSeriesIndex){
					if(championshipRound >= seriesLength){
						//Season End
						//Set bool for rewards screen
						PlayerPrefs.SetInt("ChampionshipReward",1);
						Application.LoadLevel("RaceRewards");
					} else {
						Application.LoadLevel("CircuitSelect");
					}
				} else {
					Application.LoadLevel("RaceRewards");
				}
			} else {
				Application.LoadLevel("RaceRewards");	
			}
		}
	}
	
	void addChampionshipPoints(string carNumber, int points){
		int currentPoints = PlayerPrefs.GetInt("ChampionshipPoints" + carNumber + "");
		PlayerPrefs.SetInt("ChampionshipPoints" + carNumber + "", currentPoints + points);
		Debug.Log("Car #" + carNumber + " - Points:" + (currentPoints + " + " + points));
	}
}
