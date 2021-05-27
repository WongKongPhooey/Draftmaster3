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

		challengeComplete = false;

		if(Scoreboard.position == 0){
			SceneManager.LoadScene("MainMenu");
		}

		exp = PlayerPrefs.GetInt("Exp");
		level = PlayerPrefs.GetInt("Level");
		
		//Add XP
		raceExp = 100 / Scoreboard.position;
		exp += Mathf.RoundToInt(raceExp);
		PlayerPrefs.SetInt("Exp",exp);
		Debug.Log("Exp: " + exp);

		winningsMultiplier = 1;

		string currentSubseries = PlayerPrefs.GetString("CurrentSubseries");
		string currentTrack = PlayerPrefs.GetString("CurrentTrack");

		if(PlayerPrefs.HasKey("BestFinishPosition" + currentSubseries + currentTrack) == true){
			int bestFinishPos = PlayerPrefs.GetInt("BestFinishPosition" + currentSubseries + currentTrack);
			if((Scoreboard.position < bestFinishPos)&&(Scoreboard.position > 0)){
				PlayerPrefs.SetInt("BestFinishPosition" + currentSubseries + currentTrack + "", Scoreboard.position);
			}
			Debug.Log("Prev best finish: " + bestFinishPos + "Track: " + currentSubseries + currentTrack);
		} else {
			PlayerPrefs.SetInt("BestFinishPosition" + currentSubseries + currentTrack + "", Scoreboard.position);
			Debug.Log("New best finish: " + Scoreboard.position + ". Track: " + currentSubseries + currentTrack);
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
		raceWinnings = PrizeMoney.cashAmount[Scoreboard.position - 1] * winningsMultiplier;

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
	
	/*void AssignPrizes(string seriesPrefix, int carNumber){
		if(PlayerPrefs.HasKey(seriesPrefix + carNumber + "Gears")){
			int carGears = PlayerPrefs.GetInt(seriesPrefix + carNumber + "Gears");
			int carClass = PlayerPrefs.GetInt(seriesPrefix + carNumber + "Class");
			PlayerPrefs.SetInt(seriesPrefix + carNumber + "Gears", carGears + 10);
			Debug.Log("You got new parts for car #" + carNumber + "!");
			
			int classMax = GameData.classMax(carClass);
			//If gears hits max for class
			if(carGears > classMax){
				//Increment class
				Debug.Log("Upgrade!");
				PlayerPrefs.SetInt(seriesPrefix + carNumber + "Gears", carGears - classMax);
				PlayerPrefs.SetInt(seriesPrefix + carNumber + "Class", carClass + 1);
			} else {
				Debug.Log("You have " + carGears + " of " + classMax + " needed");
			}
		} else {
			PlayerPrefs.SetInt(seriesPrefix + carNumber + "Gears", 1);
		}
	}*/

	void OnGUI() {

		GUI.skin = eightBitSkin;
		string carNumber;
		string carDriver;
		string liveryName;
		int resultsRows;
		float windowscroll;

		liveryName = "cup20livery";
		resultsRows = 18;
		windowscroll = 1.5f;

		GUI.skin.verticalScrollbar.fixedWidth = Screen.width / 20;
		GUI.skin.verticalScrollbarThumb.fixedWidth = Screen.width / 20;

		GUI.skin.label.fontSize = 72 / FontScale.fontScale;

		GUI.skin.label.normal.textColor = Color.black;

		scrollPosition = GUI.BeginScrollView(new Rect(0, 0, Screen.width, Screen.height), scrollPosition, new Rect(0, 0, Screen.width - widthblock, Screen.height * windowscroll));

		GUI.skin.label.alignment = TextAnchor.UpperLeft;

		for( int i=0; i < resultsRows; i++){
			if(i == (Scoreboard.position - 1)){
				GUI.DrawTexture(new Rect(widthblock * 2, (heightblock * (i * 2)) + (heightblock * 2), heightblock * 3f, heightblock * 1.5f), Resources.Load(PlayerPrefs.GetString("carTexture")) as Texture);
				carNumber = PlayerPrefs.GetString("carTexture");			
				string splitAfter = "livery";
				carNumber = carNumber.Substring(carNumber.IndexOf(splitAfter) + splitAfter.Length);
			} else {
				carNumber = Scoreboard.carNames[i].Remove(0,6);
				GUI.DrawTexture(new Rect(widthblock * 2, (heightblock * (i * 2)) + (heightblock * 2), heightblock * 3f, heightblock * 1.5f), Resources.Load(liveryName + carNumber) as Texture);
			}

			if(i == (Scoreboard.position - 1)){
				GUI.skin.label.normal.textColor = Color.red;
			}
			if((PlayerPrefs.GetInt("Local2Player") == 1)&&(int.Parse(carNumber) == PlayerPrefs.GetInt("Player2Num"))){
				GUI.skin.label.normal.textColor = Color.blue;
			}
			GUI.Label(new Rect(widthblock * 4.5f, (heightblock * (i * 2)) + (heightblock * 2), widthblock * 2, heightblock * 2), "P" + (i + 1));
			if(i == (Scoreboard.position - 1)){
				//carDriver = PlayerPrefs.GetString("RacerName");
				carDriver = DriverNames.cup2020Names[int.Parse(carNumber)];
			} else {
				if((PlayerPrefs.GetInt("Local2Player") == 1)&&(int.Parse(carNumber) == PlayerPrefs.GetInt("Player2Num"))){
					carDriver = "Player.2";
				} else {
					switch (PlayerPrefs.GetString("raceSeries")){
					case "StockCar":
						carDriver = DriverNames.cup2020Names[int.Parse(carNumber)];
						break;
					default:
						carDriver = DriverNames.cup2020Names[int.Parse(carNumber)];
						break;
					}
				}
			}
			GUI.Label(new Rect(widthblock * 6.5f, (heightblock * (i * 2)) + (heightblock * 2), widthblock * 7, heightblock * 2), "" + carDriver + "(" + carNumber + ")");
			GUI.Label(new Rect(widthblock * 12, (heightblock * (i * 2)) + (heightblock * 2), widthblock * 3, heightblock * 2), "+" + ((Scoreboard.carPositions[0] - Scoreboard.carPositions[i]) / 25).ToString("F3"));
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
		GUI.skin.button.alignment = TextAnchor.MiddleLeft;

		if(GUI.Button(new Rect(widthblock * 2, Screen.height - (heightblock * 3), widthblock * 3, heightblock * 2), "Next")){

			if(((RacePoints.championshipMode == false)&&(ChallengeSelectGUI.challengeMode == false))||((ChallengeSelectGUI.challengeMode == true)&&(challengeComplete == true))){
				PlayerPrefs.SetInt("PrizeMoney",PlayerPrefs.GetInt("PrizeMoney") + (PrizeMoney.cashAmount[Scoreboard.position - 1] * winningsMultiplier * lapsBonus));

			}
			ChallengeSelectGUI.challengeMode = false;
			Application.LoadLevel("RaceRewards");
		}
	}
}
