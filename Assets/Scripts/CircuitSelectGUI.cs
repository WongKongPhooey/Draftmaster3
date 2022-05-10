using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.SceneManagement;

/*----------------------
01 Daytona Beach, FL
02 Atlanta, GA
03 Las Vegas, NV
04 Phoenix, AZ
05 Fontana, CA 
06 Martinsville, VA
07 Fort Worth, TX
08 Bristol, TN
09 Richmond, VA
10 Talladega, AL 
11 Dover, DE
12 Kansas City, KS
13 Charlotte, NC
14 Long Pond, PA
15 Michigan - Brooklyn, MI
16 Joliet, IL
17 Kentucky - Sparta, KY
18 New Hampshire - Loudon, NH
19 Darlington, SC
20 Indianapolis, IN
21 Homestead, FL
22 WWT Gateway - Madison, IL
23 Nashville, TN

24 Rockingham, England
25 Rockingham, NC
26 North Wilkesboro, NC
27 Nazareth, PN
28 Iowa, IA
30 LA Coliseum, CA
----------------------*/

public class CircuitSelectGUI : MonoBehaviour {

	public GUISkin eightBitSkin;
	public GUISkin redGUI;
	public GameObject circuit;
	
	static float widthblock = Mathf.Round(Screen.width/20);
	static float heightblock = Mathf.Round(Screen.height/20);

	public static string seriesPrefix;
	public int carNumber;

	string currentSeriesName;
	string currentSeriesIndex;
	string seriesTrackList;
	string[] seriesTracks;
	int seriesLength;
	
	static int currentSubseries;
	string currentTrack;
	int championshipRound;	
	static Dictionary<int, int> championshipPoints = new Dictionary<int, int>();
	
	string activeMenu;
	
	int bestFinishPos;

	public static int gameDifficulty;
	public static int speedFactor;
	public static string circuitChoice;
	public static int seriesFuel;
	
	int maxDailyPlays;
	int dailyPlays;
	
	public static string[] circuitNames = new string[50];
	
	public Vector2 scrollPosition = Vector2.zero;
	
	void Awake(){
		
		widthblock = Mathf.Round(Screen.width/20);
		heightblock = Mathf.Round(Screen.height/20);
		
		carNumber = PlayerPrefs.GetInt("CarChoice");
		seriesPrefix = "cup20";
		
		loadCircuitNames();
		
		activeMenu = "Points";
		
		currentSeriesName = PlayerPrefs.GetString("CurrentSeriesName");
		currentSeriesIndex = PlayerPrefs.GetString("CurrentSeriesIndex");
		currentSubseries = PlayerPrefs.GetInt("CurrentSubseries");
		
		maxDailyPlays = PlayerPrefs.GetInt("SubseriesDailyPlays");
		
		seriesTrackList = PlayerPrefs.GetString("SeriesTrackList");
		
		//If there's a track list loaded
		if(seriesTrackList != ""){
			seriesTracks = seriesTrackList.Split(',');
		} else {
			seriesTrackList = "1,2,3,4,5";
			seriesTracks = seriesTrackList.Split(',');
		}
		seriesLength = seriesTracks.Length;
		
		if(seriesLength == 1){
			//One race series = Challenge or Event
			//Skip this screen
			loadTrack(seriesTracks[0], 0);
			setRaceLaps();
			startRace();
		} else {
			PlayerPrefs.SetString("RaceType","Single");
		}
		
		PlayerPrefs.SetInt("ChampionshipLength",seriesLength);
		
		circuit.GetComponent<Renderer>().material.mainTexture = null;
		
		loadTrack(seriesTracks[0], 0);
		
		//Check for an active championship
		if(PlayerPrefs.HasKey("ChampionshipSubseries")){
			if(PlayerPrefs.GetString("ChampionshipSubseries") == currentSeriesIndex){
				PlayerPrefs.SetString("RaceType","Championship");
				seriesPrefix = PlayerPrefs.GetString("ChampionshipCarSeries");
				PlayerPrefs.SetString("carSeries", seriesPrefix);
				Debug.Log("ChampionshipCarSeries loaded as " + PlayerPrefs.GetString("ChampionshipCarSeries") + ". CarSeries is " + PlayerPrefs.GetString("carSeries"));
				
				//Found a championship
				championshipRound = PlayerPrefs.GetInt("ChampionshipRound");
				//Debug.Log("Current Round: " + championshipRound);
				if(championshipRound >= seriesLength){
					//Championship is over, reset
					PlayerPrefs.DeleteKey("ChampionshipSubseries");
					PlayerPrefs.SetString("RaceType","Single");
					Debug.Log("End of season, round reset.");
				} else {
					loadTrack(seriesTracks[championshipRound].ToString(), 0);
					PlayerPrefs.SetString("CurrentTrack","" + championshipRound);
					loadPoints();
				}
			}
		}
		
		if(PlayerPrefs.HasKey("DailyPlays" + currentSeriesIndex + "")){
			dailyPlays = PlayerPrefs.GetInt("DailyPlays" + currentSeriesIndex + "");
			//Debug.Log("DailyPlays" + currentSeriesIndex + " = " + dailyPlays);
		} else {
			PlayerPrefs.SetInt("DailyPlays" + currentSeriesIndex + "", maxDailyPlays);
			dailyPlays = maxDailyPlays;
		}
		
		gameDifficulty = PlayerPrefs.GetInt("Difficulty");
		//Resources.Load("numblank") as Texture;

		seriesFuel = 10;
		seriesFuel = PlayerPrefs.GetInt("SeriesFuel");

		PlayerPrefs.SetInt("TurnDir1",0);
		PlayerPrefs.SetInt("TurnDir2",0);
		PlayerPrefs.SetInt("TurnDir3",0);
		PlayerPrefs.SetInt("TurnDir4",0);
		PlayerPrefs.SetInt("TurnDir5",0);
		PlayerPrefs.SetInt("TurnDir6",0);

		if(PlayerPrefs.GetString("raceSeries") == "IndyCar"){
			speedFactor = 24;
		} else {
			speedFactor = 0;
		}
	}
	
	void FixedUpdate(){
		circuit.transform.Rotate(0,1.0f,0);
	}

	void getTrack(string track, int order){
		switch(track){
			case "1":
				showBestFinish(currentSeriesIndex, order);
				if (GUI.Button(new Rect(widthblock * 2, heightblock * ((order*3) + 7), widthblock * 5.5f, heightblock * 2), "Daytona Beach, FL")){
					Daytona();
					PlayerPrefs.SetString("CurrentTrack","" + order);
					circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("SuperTriOval") as Texture;
				}
				break;
			case "2":
				showBestFinish(currentSeriesIndex, order);
				if (GUI.Button(new Rect(widthblock * 2, heightblock * ((order*3) + 7), widthblock * 5.5f, heightblock * 2), "Atlanta, GA")){
					Atlanta();
					PlayerPrefs.SetString("CurrentTrack","" + order);
					circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("AngledTriOval") as Texture;
				}
				break;
			case "3":
				showBestFinish(currentSeriesIndex, order);
				if (GUI.Button(new Rect(widthblock * 2, heightblock * ((order*3) + 7), widthblock * 5.5f, heightblock * 2), "Las Vegas, NV")){
					LasVegas();
					PlayerPrefs.SetString("CurrentTrack","" + order);
					circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("TriOval") as Texture;
				}
				break;
			case "4":
				showBestFinish(currentSeriesIndex, order);
				if (GUI.Button(new Rect(widthblock * 2, heightblock * ((order*3) + 7), widthblock * 5.5f, heightblock * 2), "Phoenix, AZ")){
					Phoenix();
					PlayerPrefs.SetString("CurrentTrack","" + order);
					circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("Phoenix") as Texture;
				}
				break;
			case "5":
				showBestFinish(currentSeriesIndex, order);
				if (GUI.Button(new Rect(widthblock * 2, heightblock * ((order*3) + 7), widthblock * 5.5f, heightblock * 2), "Fontana, CA")){
					Fontana();
					PlayerPrefs.SetString("CurrentTrack","" + order);
					circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("SuperTriOval") as Texture;
				}
				break;
			case "6":
				showBestFinish(currentSeriesIndex, order);
				if (GUI.Button(new Rect(widthblock * 2, heightblock * ((order*3) + 7), widthblock * 5.5f, heightblock * 2), "Martinsville, VA")){
					Martinsville();
					PlayerPrefs.SetString("CurrentTrack","" + order);
					circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("LongOval") as Texture;
				}
				break;
			case "7":
				showBestFinish(currentSeriesIndex, order);
				if (GUI.Button(new Rect(widthblock * 2, heightblock * ((order*3) + 7), widthblock * 5.5f, heightblock * 2), "Fort Worth, TX")){
					FortWorth();
					PlayerPrefs.SetString("CurrentTrack","" + order);
					circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("AngledTriOval") as Texture;
				}
				break;
			case "8":
				showBestFinish(currentSeriesIndex, order);
				if (GUI.Button(new Rect(widthblock * 2, heightblock * ((order*3) + 7), widthblock * 5.5f, heightblock * 2), "Bristol, TN")){
					Bristol();
					PlayerPrefs.SetString("CurrentTrack","" + order);
					circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("TinyOval") as Texture;
				}
				break;
			case "9":
				showBestFinish(currentSeriesIndex, order);
				if (GUI.Button(new Rect(widthblock * 2, heightblock * ((order*3) + 7), widthblock * 5.5f, heightblock * 2), "Richmond, VA")){
					Richmond();
					PlayerPrefs.SetString("CurrentTrack","" + order);
					circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("TriOval") as Texture;
				}
				break;
			case "10":
				showBestFinish(currentSeriesIndex, order);
				if (GUI.Button(new Rect(widthblock * 2, heightblock * ((order*3) + 7), widthblock * 5.5f, heightblock * 2), "Talladega, AL")){
					Talladega();
					PlayerPrefs.SetString("CurrentTrack","" + order);
					circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("Talladega") as Texture;
				}
				break;
			case "11":
				showBestFinish(currentSeriesIndex, order);
				if (GUI.Button(new Rect(widthblock * 2, heightblock * ((order*3) + 7), widthblock * 5.5f, heightblock * 2), "Dover, DE")){
					Dover();
					PlayerPrefs.SetString("CurrentTrack","" + order);
					circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("SmallOval") as Texture;
				}
				break;
			case "12":
				showBestFinish(currentSeriesIndex, order);
				if (GUI.Button(new Rect(widthblock * 2, heightblock * ((order*3) + 7), widthblock * 5.5f, heightblock * 2), "Kansas City, KS")){
					Kansas();
					PlayerPrefs.SetString("CurrentTrack","" + order);
					circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("TriOval") as Texture;
				}
				break;
			case "13":
				showBestFinish(currentSeriesIndex, order);
				if (GUI.Button(new Rect(widthblock * 2, heightblock * ((order*3) + 7), widthblock * 5.5f, heightblock * 2), "Charlotte, NC")){
					Charlotte();
					PlayerPrefs.SetString("CurrentTrack","" + order);
					circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("AngledTriOval") as Texture;
				}
				break;
			case "14":
				showBestFinish(currentSeriesIndex, order);
				if (GUI.Button(new Rect(widthblock * 2, heightblock * ((order*3) + 7), widthblock * 5.5f, heightblock * 2), "Long Pond, PA")){
					LongPond();
					PlayerPrefs.SetString("CurrentTrack","" + order);
					circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("LongPond") as Texture;
				}
				break;
			case "15":
				showBestFinish(currentSeriesIndex, order);
				if (GUI.Button(new Rect(widthblock * 2, heightblock * ((order*3) + 7), widthblock * 5.5f, heightblock * 2), "Brooklyn, MI")){
					Michigan();
					PlayerPrefs.SetString("CurrentTrack","" + order);
					circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("SuperTriOval") as Texture;
				}
				break;
			case "16":
				showBestFinish(currentSeriesIndex, order);
				if (GUI.Button(new Rect(widthblock * 2, heightblock * ((order*3) + 7), widthblock * 5.5f, heightblock * 2), "Joliet, IL")){
					Joliet();
					PlayerPrefs.SetString("CurrentTrack","" + order);
					circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("TriOval") as Texture;
				}
				break;
			case "17":
				showBestFinish(currentSeriesIndex, order);
				if (GUI.Button(new Rect(widthblock * 2, heightblock * ((order*3) + 7), widthblock * 5.5f, heightblock * 2), "Sparta, KY")){
					Kentucky();
					PlayerPrefs.SetString("CurrentTrack","" + order);
					circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("TriOval") as Texture;
				}
				break;
			case "18":
				showBestFinish(currentSeriesIndex, order);
				if (GUI.Button(new Rect(widthblock * 2, heightblock * ((order*3) + 7), widthblock * 5.5f, heightblock * 2), "Loudon, NH")){
					NewHampshire();
					PlayerPrefs.SetString("CurrentTrack","" + order);
					circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("LongOval") as Texture;
				}
				break;
			case "19":
				showBestFinish(currentSeriesIndex, order);
				if (GUI.Button(new Rect(widthblock * 2, heightblock * ((order*3) + 7), widthblock * 5.5f, heightblock * 2), "Darlington, SC")){
					Darlington();
					PlayerPrefs.SetString("CurrentTrack","" + order);
					circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("Darlington") as Texture;
				}
				break;
			case "20":
				showBestFinish(currentSeriesIndex, order);
				if (GUI.Button(new Rect(widthblock * 2, heightblock * ((order*3) + 7), widthblock * 5.5f, heightblock * 2), "Indianapolis, IN")){
					Indianapolis();
					PlayerPrefs.SetString("CurrentTrack","" + order);
					circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("Indianapolis") as Texture;
				}
				break;
			case "21":
				showBestFinish(currentSeriesIndex, order);
				if (GUI.Button(new Rect(widthblock * 2, heightblock * ((order*3) + 7), widthblock * 5.5f, heightblock * 2), "Miami, FL")){
					Miami();
					PlayerPrefs.SetString("CurrentTrack","" + order);
					circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("BigOval") as Texture;
				}
				break;
			case "22":
				showBestFinish(currentSeriesIndex, order);
				if (GUI.Button(new Rect(widthblock * 2, heightblock * ((order*3) + 7), widthblock * 5.5f, heightblock * 2), "Madison, IL")){
					Madison();
					PlayerPrefs.SetString("CurrentTrack","" + order);
					circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("Madison") as Texture;
				}
				break;
			case "23":
				showBestFinish(currentSeriesIndex, order);
				if (GUI.Button(new Rect(widthblock * 2, heightblock * ((order*3) + 7), widthblock * 5.5f, heightblock * 2), "Nashville, TN")){
					Nashville();
					PlayerPrefs.SetString("CurrentTrack","" + order);
					circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("TriOval") as Texture;
				}
				break;
			case "30":
				showBestFinish(currentSeriesIndex, order);
				if (GUI.Button(new Rect(widthblock * 2, heightblock * ((order*3) + 7), widthblock * 5.5f, heightblock * 2), "Los Angeles, CA")){
					LosAngeles();
					PlayerPrefs.SetString("CurrentTrack","" + order);
					circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("TinyOval") as Texture;
				}
				break;
			default:
				break;
		}
	}

	void OnGUI() {
		
		GUI.skin = eightBitSkin;

		GUI.skin.label.fontSize = 72 / FontScale.fontScale;
		GUI.skin.button.fontSize = 72 / FontScale.fontScale;
		GUI.skin.verticalScrollbar.fixedWidth = Screen.width / 20;
		GUI.skin.verticalScrollbarThumb.fixedWidth = Screen.width / 20;
		
		scrollPosition = GUI.BeginScrollView(new Rect(0, 0, widthblock * 9, Screen.height), scrollPosition, new Rect(0, 0, widthblock, Screen.height * 4.0f));

		GUI.Label(new Rect(widthblock, heightblock / 2, widthblock * 7, heightblock * 2), currentSeriesName);
		GUI.skin.label.fontSize = 48 / FontScale.fontScale;
		//GUI.Label(new Rect(widthblock, heightblock * 2, widthblock * 7, heightblock * 2), "Daily Attempts: " + dailyPlays + "/" + maxDailyPlays);

		if((PlayerPrefs.HasKey("ChampionshipSubseries"))&&(PlayerPrefs.GetString("ChampionshipSubseries") == currentSeriesIndex)){
			GUI.skin.button.fontSize = 48 / FontScale.fontScale;
			GUI.skin.button.alignment = TextAnchor.MiddleCenter;
			if (GUI.Button(new Rect(widthblock, heightblock * 4, widthblock * 2.75f, heightblock * 1.5f), "Points")){
				activeMenu = "Points";
			}
			if (GUI.Button(new Rect(widthblock * 4.25f, heightblock * 4, widthblock * 2.75f, heightblock * 1.5f), "Schedule")){
				activeMenu = "Schedule";
			}
			GUI.skin.button.fontSize = 72 / FontScale.fontScale;
			if(activeMenu != "Schedule"){
				showPoints(widthblock / 2, 6, carNumber);
			} else {
				showSchedule(widthblock / 2, 6, championshipRound);
			}
		} else {
			GUI.skin = redGUI;
			GUI.skin.button.fontSize = 72 / FontScale.fontScale;
			if (GUI.Button(new Rect(widthblock / 2, heightblock * 4, widthblock * 7f, heightblock * 2), "Run As Championship")){
				activeMenu = "Schedule";
				PlayerPrefs.SetString("ChampionshipSubseries",currentSeriesIndex);
				PlayerPrefs.SetString("RaceType","Championship");
				loadTrack(seriesTracks[0], 0);
				PlayerPrefs.SetInt("ChampionshipRound",0);
				resetChampionshipPoints();
				PlayerPrefs.SetString("ChampionshipCarTexture", PlayerPrefs.GetString("carTexture"));
				PlayerPrefs.SetString("ChampionshipCarSeries", PlayerPrefs.GetString("carSeries"));
				//Debug.Log("Championship Carset Series set as " + PlayerPrefs.GetString("ChampionshipCarSeries"));
				PlayerPrefs.SetInt("ChampionshipCarChoice", PlayerPrefs.GetInt("CarChoice"));
				
			}
			GUI.skin = eightBitSkin;
			GUI.skin.button.fontSize = 72 / FontScale.fontScale;
			GUI.skin.label.fontSize = 48 / FontScale.fontScale;
			
			int trackCount = 0;
			foreach (string track in seriesTracks){
				getTrack(track, trackCount);
				trackCount++;
			}
		}
		
		GUI.EndScrollView();
		
		//GUI.skin.label.fontSize = 24 / FontScale.fontScale;
		GUI.skin.label.normal.textColor = Color.black;
		GUI.skin.label.alignment = TextAnchor.UpperRight;
		int maxSpeed = PlayerPrefs.GetInt("SpeedOffset");
		GUI.Label(new Rect((widthblock * 12)-20, 0, widthblock * 8, heightblock * 1.5f), "Avg Speed: " + (202 - maxSpeed) + "MpH");
		GUI.Label(new Rect((widthblock * 15)-20, heightblock * 1.5f, widthblock * 5, heightblock * 1.5f), "Laps: " + PlayerPrefs.GetInt("RaceLaps"));
		GUI.Label(new Rect((widthblock * 15)-20, heightblock * 3, widthblock * 5, heightblock * 1.5f), "Lanes: " + PlayerPrefs.GetInt("CircuitLanes"));
		GUI.skin.label.alignment = TextAnchor.MiddleLeft;
		//GUI.skin.label.fontSize = 72 / FontScale.fontScale;
		
		if (Input.GetKeyDown(KeyCode.Escape)){
			SceneManager.LoadScene("CarSelect");
		}
		
		if (GUI.Button(new Rect(widthblock * 15, heightblock * 17, widthblock * 3, heightblock * 2), "Race")){
			startRace();
		}
		
		if (GUI.Button(new Rect(widthblock * 11, heightblock * 17, widthblock * 3, heightblock * 2), "Back")){
			SceneManager.LoadScene("SeriesSelect");
		}
	}

	void setRaceLaps(){
		//Adjust laps based on AI difficulty
		int AIDiff = PlayerPrefs.GetInt("AIDifficulty");
		int baseLaps = PlayerPrefs.GetInt("RaceLaps");
		int raceLapsMultiplier = (AIDiff / 8) + 1;
		PlayerPrefs.SetInt("RaceLaps", Mathf.FloorToInt(baseLaps * raceLapsMultiplier));
		Debug.Log("Race Laps: " + baseLaps + " * " + raceLapsMultiplier);
	}

	void startRace(){
		
		PlayerPrefs.SetString("CurrentCircuit",circuitChoice);
		if(GameData.gameFuel >= seriesFuel){
			GameData.gameFuel-=seriesFuel;
			
			PlayerPrefs.SetInt("TotalStarts",PlayerPrefs.GetInt("TotalStarts") + 1);
			if(PlayerPrefs.HasKey("TotalStarts" + seriesPrefix + carNumber)){
				PlayerPrefs.SetInt("TotalStarts" + seriesPrefix + carNumber,PlayerPrefs.GetInt("TotalStarts" + seriesPrefix + carNumber) + 1);
			} else {
				PlayerPrefs.SetInt("TotalStarts" + seriesPrefix + carNumber, 1);
			}
			
			//Debug.Log("-" + seriesFuel + " Fuel, now " + GameData.gameFuel);
			PlayerPrefs.SetInt("GameFuel",GameData.gameFuel);
			//Debug.Log("Track chosen: " + PlayerPrefs.GetString("CurrentTrack"));
			//dailyPlays--;
			//PlayerPrefs.SetInt("DailyPlays" + currentSeriesIndex + "", dailyPlays);
			Debug.Log("Race Laps: " + PlayerPrefs.GetInt("RaceLaps"));
			SceneManager.LoadScene(circuitChoice);
		} else {
			//Roll back and bail
			PlayerPrefs.SetString("StoreFocus","Fuel");
			SceneManager.LoadScene("Store");
		}
	}

	void loadTrack(string track, int order){
		switch(track){
			case "1":
				Daytona();
				circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("SuperTriOval") as Texture;
				break;
			case "2":
				Atlanta();
				circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("AngledTriOval") as Texture;
				break;
			case "3":
				LasVegas();
				circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("TriOval") as Texture;
				break;
			case "4":
				Phoenix();
				circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("Phoenix") as Texture;
				break;
			case "5":
				Fontana();
				circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("SuperTriOval") as Texture;
				break;
			case "6":
				Martinsville();
				circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("LongOval") as Texture;
				break;
			case "7":
				FortWorth();
				circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("AngledTriOval") as Texture;
				break;
			case "8":
				Bristol();
				circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("TinyOval") as Texture;
				break;
			case "9":
				Richmond();
				circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("TriOval") as Texture;
				break;
			case "10":
				Talladega();
				circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("Talladega") as Texture;
				break;
			case "11":
				Dover();
				circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("SmallOval") as Texture;
				break;
			case "12":
				Kansas();
				circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("TriOval") as Texture;
				break;
			case "13":
				Charlotte();
				circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("AngledTriOval") as Texture;
				break;
			case "14":
				LongPond();
				circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("LongPond") as Texture;
				break;
			case "15":
				Michigan();
				circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("SuperTriOval") as Texture;
				break;
			case "16":
				Joliet();
				circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("TriOval") as Texture;
				break;
			case "17":
				Kentucky();
				circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("TriOval") as Texture;
				break;
			case "18":
				NewHampshire();
				circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("LongOval") as Texture;
				break;
			case "19":
				Darlington();
				circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("Darlington") as Texture;
				break;
			case "20":
				Indianapolis();
				circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("Indianapolis") as Texture;
				break;
			case "21":
				Miami();
				circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("BigOval") as Texture;
				break;
			case "22":
				Madison();
				circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("Madison") as Texture;
				break;
			case "23":
				Nashville();
				circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("TriOval") as Texture;
				break;
			case "30":
				LosAngeles();
				circuit.GetComponent<Renderer>().material.mainTexture = Resources.Load("TinyOval") as Texture;
				break;
			default:
				break;
		}
		Debug.Log(circuitChoice + " Loaded");
		setRaceLaps();
		
		//Testing only
		//PlayerPrefs.SetInt("RaceLaps",2);
		
		PlayerPrefs.SetString("CurrentTrack","" + order);
	}

	static void loadPoints(){
		championshipPoints.Clear();
		int pointsTableInd = 0;
		for(int i=0;i<100;i++){
			if(PlayerPrefs.HasKey("ChampionshipPoints" + i)){
				championshipPoints.Add(i, PlayerPrefs.GetInt("ChampionshipPoints" + i));
				//Debug.Log("# " + i + " has " + PlayerPrefs.GetInt("ChampionshipPoints" + i) + " points.");
				pointsTableInd++;
			}
		}
	}

	static void showPoints(float posX, float posY, int carNumber){
		
		List<KeyValuePair<int, int>> pointsTable = new List<KeyValuePair<int, int>>(championshipPoints);
		
		pointsTable.Sort(
			delegate(KeyValuePair<int, int> firstPair,
			KeyValuePair<int, int> nextPair)
			{
				return nextPair.Value.CompareTo(firstPair.Value);
			}
		);
		
		//Table header
		GUI.Label(new Rect(widthblock, heightblock * posY, widthblock * 1.5f, heightblock * 2), "Pos");	
		GUI.Label(new Rect(widthblock * 3f, heightblock * posY, widthblock * 2.5f, heightblock * 2), "Driver");	
		GUI.Label(new Rect(widthblock * 6.5f, heightblock * posY, widthblock * 1.5f, heightblock * 2), "Pts");	
			
		
		int pointsTableInd = 0;
		foreach(var pointsRow in pointsTable){
			
			if(pointsRow.Key == carNumber){
				GUI.skin.label.normal.textColor = Color.red;
			}
			if(pointsRow.Value > 0){
				GUI.Label(new Rect(widthblock, heightblock * (((pointsTableInd + 1)*1.5f) + posY), widthblock * 1.5f, heightblock * 2), "" + (pointsTableInd + 1));
				if(pointsRow.Key == carNumber){
					if(PlayerPrefs.HasKey(seriesPrefix + carNumber + "AltDriver")){
						//Debug.Log("Using alt driver");
						GUI.Label(new Rect(widthblock * 3f, heightblock * (((pointsTableInd + 1)*1.5f) + posY), widthblock * 2.5f, heightblock * 2), "" + AltPaints.getAltPaintDriver(seriesPrefix,carNumber,PlayerPrefs.GetInt(seriesPrefix + carNumber + "AltPaint")));	
					} else {
						GUI.Label(new Rect(widthblock * 3f, heightblock * (((pointsTableInd + 1)*1.5f) + posY), widthblock * 2.5f, heightblock * 2), "" + DriverNames.getName(seriesPrefix, pointsRow.Key));	
					}
				} else {
					GUI.Label(new Rect(widthblock * 3f, heightblock * (((pointsTableInd + 1)*1.5f) + posY), widthblock * 2.5f, heightblock * 2), "" + DriverNames.getName(seriesPrefix, pointsRow.Key));	
				}
				GUI.Label(new Rect(widthblock * 6.5f, heightblock * (((pointsTableInd + 1)*1.5f) + posY), widthblock * 1.5f, heightblock * 2), "" + pointsRow.Value);	
			}
			if(pointsRow.Key == carNumber){
				GUI.skin.label.normal.textColor = Color.black;
			}
			pointsTableInd++;
		}
	}

	void showSchedule(float posX, float posY, int championshipRound){
		
		//Table header
		GUI.Label(new Rect(widthblock, heightblock * posY, widthblock * 1.5f, heightblock * 2), "Round");	
		GUI.Label(new Rect(widthblock * 3f, heightblock * posY, widthblock * 2.5f, heightblock * 2), "Location");	
		
		int scheduleInd = 0;
		foreach(var track in seriesTracks){
			
			if(scheduleInd == championshipRound){
				GUI.skin.label.normal.textColor = Color.red;
			}
			GUI.Label(new Rect(widthblock, heightblock * (((scheduleInd + 1)*1.5f) + posY), widthblock * 1.5f, heightblock * 2), "R" + (scheduleInd + 1));	
			GUI.Label(new Rect(widthblock * 3f, heightblock * (((scheduleInd + 1)*1.5f) + posY), widthblock * 5f, heightblock * 2), "" + circuitNames[int.Parse(track)]);	
			if(scheduleInd == championshipRound){
				GUI.skin.label.normal.textColor = Color.black;
			}
			scheduleInd++;
		}
	}

	static void showBestFinish(string currentSeriesIndex, int order){
		
		if(PlayerPrefs.HasKey("BestFinishPosition" + currentSeriesIndex + order) == true){
			int bestFinishPos = PlayerPrefs.GetInt("BestFinishPosition" + currentSeriesIndex + order);
			GUI.skin.label.alignment = TextAnchor.MiddleCenter;
			GUI.Label(new Rect(widthblock/2, heightblock * ((order*3) + 7), widthblock * 1.5f, heightblock * 2), "" + bestFinishPos);
			//Debug.Log("Track " + order + " has a best finish of " + bestFinishPos);
		} else {
			//Debug.Log("No finishes on Track " + currentSubseries + order);
		}
	}
	
	static void resetChampionshipPoints(){
		championshipPoints.Clear();
		for(int i=0;i<100;i++){
			if(DriverNames.cup2020Names[i] != null){
				PlayerPrefs.SetInt("ChampionshipPoints" + i,0);
			}
			if(PlayerPrefs.HasKey("ChampionshipPoints" + i)){
				PlayerPrefs.SetInt("ChampionshipPoints" + i,0);
			} else {
				//Debug.Log("No points found for " + ("ChampionshipPoints" + i));
			}
		}
		loadPoints();
	}

	static void loadCircuitNames(){
		circuitNames[1] = "Daytona Beach, FL";
		circuitNames[2] = "Atlanta, GA";
		circuitNames[3] = "Las Vegas, NV";
		circuitNames[4] = "Phoenix, AZ";
		circuitNames[5] = "Fontana, CA";
		circuitNames[6] = "Martinsville, VA";
		circuitNames[7] = "Fort Worth, TX";
		circuitNames[8] = "Bristol, TN";
		circuitNames[9] = "Richmond, VA";
		circuitNames[10] = "Talladega, AL";
		circuitNames[11] = "Dover, DE";
		circuitNames[12] = "Kansas City, KS";
		circuitNames[13] = "Charlotte, NC";
		circuitNames[14] = "Long Pond, PA";
		circuitNames[15] = "Brooklyn, MI";
		circuitNames[16] = "Joliet, IL";
		circuitNames[17] = "Sparta, KY";
		circuitNames[18] = "Loudon, NH";
		circuitNames[19] = "Darlington, SC";
		circuitNames[20] = "Indianapolis, IN";
		circuitNames[21] = "Homestead, FL";
		circuitNames[22] = "Madison, IL";
		circuitNames[23] = "Nashville, TN";
		circuitNames[30] = "Los Angeles, CA";
		
		circuitNames[24] = "Rockingham, England";
		
		circuitNames[25] = "Rockingham, NC";
		circuitNames[26] = "North Wilkesboro, NC";
		circuitNames[27] = "Nazareth, PN";
		circuitNames[28] = "Newton, IA";
		circuitNames[29] = "Nashville, TN";
	}

	static void Talladega(){
		circuitChoice = "Talladega";
		PlayerPrefs.SetInt("RaceLaps",4);
		PlayerPrefs.SetInt("CircuitLanes",4);
		PlayerPrefs.SetInt("StraightLength1",200);
		PlayerPrefs.SetInt("StraightLength2",500);
		PlayerPrefs.SetInt("StraightLength3",200);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",150);
		PlayerPrefs.SetInt("TurnLength2",150);
		PlayerPrefs.SetInt("TurnLength3",60);
		PlayerPrefs.SetInt("TurnLength4",0);
		PlayerPrefs.SetInt("TurnAngle1",4);
		PlayerPrefs.SetInt("TurnAngle2",4);
		PlayerPrefs.SetInt("TurnAngle3",8);
		PlayerPrefs.SetInt("TurnAngle4",0);
		PlayerPrefs.SetInt("StartLine",50);
		PlayerPrefs.SetInt("SpeedOffset",2 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",3);
		PlayerPrefs.SetString("TrackType","Plate");
	}

	static void Joliet(){
		circuitChoice = "Joliet";
		PlayerPrefs.SetInt("RaceLaps",6);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",1);
		PlayerPrefs.SetInt("StraightLength2",1);
		PlayerPrefs.SetInt("StraightLength3",200);
		PlayerPrefs.SetInt("StraightLength4",10);
		PlayerPrefs.SetInt("TurnLength1",35);
		PlayerPrefs.SetInt("TurnLength2",145);
		PlayerPrefs.SetInt("TurnLength3",145);
		PlayerPrefs.SetInt("TurnLength4",35);
		PlayerPrefs.SetInt("TurnAngle1",4);
		PlayerPrefs.SetInt("TurnAngle2",1);
		PlayerPrefs.SetInt("TurnAngle3",1);
		PlayerPrefs.SetInt("TurnAngle4",4);
		PlayerPrefs.SetInt("StartLine",5);
		PlayerPrefs.SetInt("SpeedOffset",24 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Mid");
	}
	
	static void LasVegas(){
		circuitChoice = "LasVegas";
		PlayerPrefs.SetInt("RaceLaps",6);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",1);
		PlayerPrefs.SetInt("StraightLength2",1);
		PlayerPrefs.SetInt("StraightLength3",200);
		PlayerPrefs.SetInt("StraightLength4",10);
		PlayerPrefs.SetInt("TurnLength1",35);
		PlayerPrefs.SetInt("TurnLength2",145);
		PlayerPrefs.SetInt("TurnLength3",145);
		PlayerPrefs.SetInt("TurnLength4",35);
		PlayerPrefs.SetInt("TurnAngle1",4);
		PlayerPrefs.SetInt("TurnAngle2",1);
		PlayerPrefs.SetInt("TurnAngle3",1);
		PlayerPrefs.SetInt("TurnAngle4",4);
		PlayerPrefs.SetInt("StartLine",5);
		PlayerPrefs.SetInt("SpeedOffset",18 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Mid");
	}

	static void Bristol(){
		circuitChoice = "Bristol";
		PlayerPrefs.SetInt("RaceLaps",10);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",100);
		PlayerPrefs.SetInt("StraightLength2",100);
		PlayerPrefs.SetInt("StraightLength3",0);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",180);
		PlayerPrefs.SetInt("TurnLength2",180);
		PlayerPrefs.SetInt("TurnLength3",0);
		PlayerPrefs.SetInt("TurnLength4",0);
		PlayerPrefs.SetInt("TurnAngle1",1);
		PlayerPrefs.SetInt("TurnAngle2",1);
		PlayerPrefs.SetInt("TurnAngle3",0);
		PlayerPrefs.SetInt("TurnAngle4",0);
		PlayerPrefs.SetInt("StartLine",50);
		PlayerPrefs.SetInt("SpeedOffset",65 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",2);
		PlayerPrefs.SetString("TrackType","Short");
	}
	
	static void Richmond(){
		circuitChoice = "Richmond";
		PlayerPrefs.SetInt("RaceLaps",9);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",1);
		PlayerPrefs.SetInt("StraightLength2",1);
		PlayerPrefs.SetInt("StraightLength3",150);
		PlayerPrefs.SetInt("StraightLength4",10);
		PlayerPrefs.SetInt("TurnLength1",15);
		PlayerPrefs.SetInt("TurnLength2",165);
		PlayerPrefs.SetInt("TurnLength3",165);
		PlayerPrefs.SetInt("TurnLength4",15);
		PlayerPrefs.SetInt("TurnAngle1",4);
		PlayerPrefs.SetInt("TurnAngle2",1);
		PlayerPrefs.SetInt("TurnAngle3",1);
		PlayerPrefs.SetInt("TurnAngle4",4);
		PlayerPrefs.SetInt("StartLine",5);
		PlayerPrefs.SetInt("SpeedOffset",58 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Short");
	}
	
	static void NewHampshire(){
		circuitChoice = "NewHampshire";
		PlayerPrefs.SetInt("RaceLaps",6);
		PlayerPrefs.SetInt("CircuitLanes",4);
		PlayerPrefs.SetInt("StraightLength1",250);
		PlayerPrefs.SetInt("StraightLength2",250);
		PlayerPrefs.SetInt("StraightLength3",0);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",180);
		PlayerPrefs.SetInt("TurnLength2",180);
		PlayerPrefs.SetInt("TurnLength3",0);
		PlayerPrefs.SetInt("TurnLength4",0);
		PlayerPrefs.SetInt("TurnAngle1",2);
		PlayerPrefs.SetInt("TurnAngle2",2);
		PlayerPrefs.SetInt("TurnAngle3",0);
		PlayerPrefs.SetInt("TurnAngle4",0);
		PlayerPrefs.SetInt("StartLine",100);
		PlayerPrefs.SetInt("SpeedOffset",43 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",2);
		PlayerPrefs.SetString("TrackType","Short");
	}

	static void Indianapolis(){
		circuitChoice = "Indianapolis";
		PlayerPrefs.SetInt("RaceLaps",4);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",400);
		PlayerPrefs.SetInt("StraightLength2",100);
		PlayerPrefs.SetInt("StraightLength3",400);
		PlayerPrefs.SetInt("StraightLength4",100);
		PlayerPrefs.SetInt("TurnLength1",90);
		PlayerPrefs.SetInt("TurnLength2",90);
		PlayerPrefs.SetInt("TurnLength3",90);
		PlayerPrefs.SetInt("TurnLength4",90);
		PlayerPrefs.SetInt("TurnAngle1",2);
		PlayerPrefs.SetInt("TurnAngle2",2);
		PlayerPrefs.SetInt("TurnAngle3",2);
		PlayerPrefs.SetInt("TurnAngle4",2);
		PlayerPrefs.SetInt("StartLine",250);
		PlayerPrefs.SetInt("SpeedOffset",0 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Large");
	}

	static void Atlanta(){
		circuitChoice = "Atlanta";
		PlayerPrefs.SetInt("RaceLaps",5);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",150);
		PlayerPrefs.SetInt("StraightLength2",50);
		PlayerPrefs.SetInt("StraightLength3",350);
		PlayerPrefs.SetInt("StraightLength4",50);
		PlayerPrefs.SetInt("TurnLength1",20);
		PlayerPrefs.SetInt("TurnLength2",160);
		PlayerPrefs.SetInt("TurnLength3",160);
		PlayerPrefs.SetInt("TurnLength4",20);
		PlayerPrefs.SetInt("TurnAngle1",1);
		PlayerPrefs.SetInt("TurnAngle2",2);
		PlayerPrefs.SetInt("TurnAngle3",2);
		PlayerPrefs.SetInt("TurnAngle4",1);
		PlayerPrefs.SetInt("StartLine",40);
		PlayerPrefs.SetInt("SpeedOffset",16 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Mid");
	}
	
	static void Phoenix(){
		circuitChoice = "Phoenix";
		PlayerPrefs.SetInt("RaceLaps",8);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",50);
		PlayerPrefs.SetInt("StraightLength2",100);
		PlayerPrefs.SetInt("StraightLength3",150);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",50);
		PlayerPrefs.SetInt("TurnLength2",170);
		PlayerPrefs.SetInt("TurnLength3",140);
		PlayerPrefs.SetInt("TurnLength4",0);
		PlayerPrefs.SetInt("TurnAngle1",1);
		PlayerPrefs.SetInt("TurnAngle2",2);
		PlayerPrefs.SetInt("TurnAngle3",2);
		PlayerPrefs.SetInt("TurnAngle4",1);
		PlayerPrefs.SetInt("StartLine",20);
		PlayerPrefs.SetInt("SpeedOffset",55 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",3);
		PlayerPrefs.SetString("TrackType","Short");
	}

	static void Motegi(){
		circuitChoice = "Motegi";
		PlayerPrefs.SetInt("RaceLaps",6);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",300);
		PlayerPrefs.SetInt("StraightLength2",300);
		PlayerPrefs.SetInt("StraightLength3",0);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",200);
		PlayerPrefs.SetInt("TurnLength2",160);
		PlayerPrefs.SetInt("TurnLength3",0);
		PlayerPrefs.SetInt("TurnLength4",0);
		PlayerPrefs.SetInt("TurnAngle1",2);
		PlayerPrefs.SetInt("TurnAngle2",1);
		PlayerPrefs.SetInt("TurnAngle3",0);
		PlayerPrefs.SetInt("TurnAngle4",0);
		PlayerPrefs.SetInt("StartLine",200);
		PlayerPrefs.SetInt("SpeedOffset",25 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",2);
		PlayerPrefs.SetString("TrackType","Alt");
	}

	static void LongPond(){
		circuitChoice = "LongPond";
		PlayerPrefs.SetInt("RaceLaps",4);
		PlayerPrefs.SetInt("CircuitLanes",4);
		PlayerPrefs.SetInt("StraightLength1",350);
		PlayerPrefs.SetInt("StraightLength2",250);
		PlayerPrefs.SetInt("StraightLength3",300);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",110);
		PlayerPrefs.SetInt("TurnLength2",100);
		PlayerPrefs.SetInt("TurnLength3",150);
		PlayerPrefs.SetInt("TurnLength4",0);
		PlayerPrefs.SetInt("TurnAngle1",2);
		PlayerPrefs.SetInt("TurnAngle2",2);
		PlayerPrefs.SetInt("TurnAngle3",2);
		PlayerPrefs.SetInt("TurnAngle4",0);
		PlayerPrefs.SetInt("StartLine",200);
		PlayerPrefs.SetInt("SpeedOffset",8 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",3);
		PlayerPrefs.SetString("TrackType","Large");
	}

	static void Fontana(){
		circuitChoice = "Fontana";
		PlayerPrefs.SetInt("RaceLaps",5);
		PlayerPrefs.SetInt("CircuitLanes",4);
		PlayerPrefs.SetInt("StraightLength1",1);
		PlayerPrefs.SetInt("StraightLength2",1);
		PlayerPrefs.SetInt("StraightLength3",250);
		PlayerPrefs.SetInt("StraightLength4",20);
		PlayerPrefs.SetInt("TurnLength1",30);
		PlayerPrefs.SetInt("TurnLength2",150);
		PlayerPrefs.SetInt("TurnLength3",150);
		PlayerPrefs.SetInt("TurnLength4",30);
		PlayerPrefs.SetInt("TurnAngle1",8);
		PlayerPrefs.SetInt("TurnAngle2",2);
		PlayerPrefs.SetInt("TurnAngle3",2);
		PlayerPrefs.SetInt("TurnAngle4",8);
		PlayerPrefs.SetInt("StartLine",10);
		PlayerPrefs.SetInt("SpeedOffset",3 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Large");
	}
	
	static void Michigan(){
		circuitChoice = "Michigan";
		PlayerPrefs.SetInt("RaceLaps",4);
		PlayerPrefs.SetInt("CircuitLanes",4);
		PlayerPrefs.SetInt("StraightLength1",1);
		PlayerPrefs.SetInt("StraightLength2",1);
		PlayerPrefs.SetInt("StraightLength3",250);
		PlayerPrefs.SetInt("StraightLength4",20);
		PlayerPrefs.SetInt("TurnLength1",10);
		PlayerPrefs.SetInt("TurnLength2",170);
		PlayerPrefs.SetInt("TurnLength3",170);
		PlayerPrefs.SetInt("TurnLength4",10);
		PlayerPrefs.SetInt("TurnAngle1",16);
		PlayerPrefs.SetInt("TurnAngle2",2);
		PlayerPrefs.SetInt("TurnAngle3",2);
		PlayerPrefs.SetInt("TurnAngle4",16);
		PlayerPrefs.SetInt("StartLine",10);
		PlayerPrefs.SetInt("SpeedOffset",3 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Large");
	}

	static void Charlotte(){
		circuitChoice = "Charlotte";
		PlayerPrefs.SetInt("RaceLaps",6);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",100);
		PlayerPrefs.SetInt("StraightLength2",50);
		PlayerPrefs.SetInt("StraightLength3",200);
		PlayerPrefs.SetInt("StraightLength4",50);
		PlayerPrefs.SetInt("TurnLength1",25);
		PlayerPrefs.SetInt("TurnLength2",155);
		PlayerPrefs.SetInt("TurnLength3",155);
		PlayerPrefs.SetInt("TurnLength4",25);
		PlayerPrefs.SetInt("TurnAngle1",1);
		PlayerPrefs.SetInt("TurnAngle2",2);
		PlayerPrefs.SetInt("TurnAngle3",2);
		PlayerPrefs.SetInt("TurnAngle4",1);
		PlayerPrefs.SetInt("StartLine",40);
		PlayerPrefs.SetInt("SpeedOffset",19 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Mid");
	}

	static void FortWorth(){
		circuitChoice = "FortWorth";
		PlayerPrefs.SetInt("RaceLaps",6);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",80);
		PlayerPrefs.SetInt("StraightLength2",75);
		PlayerPrefs.SetInt("StraightLength3",150);
		PlayerPrefs.SetInt("StraightLength4",75);
		PlayerPrefs.SetInt("TurnLength1",35);
		PlayerPrefs.SetInt("TurnLength2",145);
		PlayerPrefs.SetInt("TurnLength3",145);
		PlayerPrefs.SetInt("TurnLength4",35);
		PlayerPrefs.SetInt("TurnAngle1",1);
		PlayerPrefs.SetInt("TurnAngle2",1);
		PlayerPrefs.SetInt("TurnAngle3",1);
		PlayerPrefs.SetInt("TurnAngle4",1);
		PlayerPrefs.SetInt("StartLine",40);
		PlayerPrefs.SetInt("SpeedOffset",22 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Mid");
	}

	static void Darlington(){
		circuitChoice = "Darlington";
		PlayerPrefs.SetInt("RaceLaps",6);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",250);
		PlayerPrefs.SetInt("StraightLength2",0);
		PlayerPrefs.SetInt("StraightLength3",0);
		PlayerPrefs.SetInt("StraightLength4",250);
		PlayerPrefs.SetInt("TurnLength1",45);
		PlayerPrefs.SetInt("TurnLength2",110);
		PlayerPrefs.SetInt("TurnLength3",45);
		PlayerPrefs.SetInt("TurnLength4",160);
		PlayerPrefs.SetInt("TurnAngle1",1);
		PlayerPrefs.SetInt("TurnAngle2",4);
		PlayerPrefs.SetInt("TurnAngle3",1);
		PlayerPrefs.SetInt("TurnAngle4",2);
		PlayerPrefs.SetInt("StartLine",150);
		PlayerPrefs.SetInt("SpeedOffset",22 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Alt");
	}

	static void Miami(){
		circuitChoice = "Miami";
		PlayerPrefs.SetInt("RaceLaps",6);
		PlayerPrefs.SetInt("CircuitLanes",4);
		PlayerPrefs.SetInt("StraightLength1",300);
		PlayerPrefs.SetInt("StraightLength2",300);
		PlayerPrefs.SetInt("StraightLength3",0);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",180);
		PlayerPrefs.SetInt("TurnLength2",180);
		PlayerPrefs.SetInt("TurnLength3",0);
		PlayerPrefs.SetInt("TurnLength4",0);
		PlayerPrefs.SetInt("TurnAngle1",2);
		PlayerPrefs.SetInt("TurnAngle2",2);
		PlayerPrefs.SetInt("TurnAngle3",1);
		PlayerPrefs.SetInt("TurnAngle4",1);
		PlayerPrefs.SetInt("StartLine",150);
		PlayerPrefs.SetInt("SpeedOffset",19 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",2);
		PlayerPrefs.SetString("TrackType","Mid");
	}

	static void Dover(){
		circuitChoice = "Dover";
		PlayerPrefs.SetInt("RaceLaps",8);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",150);
		PlayerPrefs.SetInt("StraightLength2",150);
		PlayerPrefs.SetInt("StraightLength3",0);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",180);
		PlayerPrefs.SetInt("TurnLength2",180);
		PlayerPrefs.SetInt("TurnLength3",0);
		PlayerPrefs.SetInt("TurnLength4",0);
		PlayerPrefs.SetInt("TurnAngle1",1);
		PlayerPrefs.SetInt("TurnAngle2",1);
		PlayerPrefs.SetInt("TurnAngle3",1);
		PlayerPrefs.SetInt("TurnAngle4",1);
		PlayerPrefs.SetInt("StartLine",75);
		PlayerPrefs.SetInt("SpeedOffset",47 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",2);
		PlayerPrefs.SetString("TrackType","Short");
	}
	
	static void Kansas(){
		circuitChoice = "Kansas";
		PlayerPrefs.SetInt("RaceLaps",5);
		PlayerPrefs.SetInt("CircuitLanes",4);
		PlayerPrefs.SetInt("StraightLength1",1);
		PlayerPrefs.SetInt("StraightLength2",50);
		PlayerPrefs.SetInt("StraightLength3",200);
		PlayerPrefs.SetInt("StraightLength4",50);
		PlayerPrefs.SetInt("TurnLength1",15);
		PlayerPrefs.SetInt("TurnLength2",165);
		PlayerPrefs.SetInt("TurnLength3",165);
		PlayerPrefs.SetInt("TurnLength4",15);
		PlayerPrefs.SetInt("TurnAngle1",4);
		PlayerPrefs.SetInt("TurnAngle2",4);
		PlayerPrefs.SetInt("TurnAngle3",4);
		PlayerPrefs.SetInt("TurnAngle4",4);
		PlayerPrefs.SetInt("StartLine",2);
		PlayerPrefs.SetInt("SpeedOffset",17 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Large");
	}
	
	static void Kentucky(){
		circuitChoice = "Kentucky";
		PlayerPrefs.SetInt("RaceLaps",6);
		PlayerPrefs.SetInt("CircuitLanes",4);
		PlayerPrefs.SetInt("StraightLength1",1);
		PlayerPrefs.SetInt("StraightLength2",1);
		PlayerPrefs.SetInt("StraightLength3",200);
		PlayerPrefs.SetInt("StraightLength4",10);
		PlayerPrefs.SetInt("TurnLength1",35);
		PlayerPrefs.SetInt("TurnLength2",145);
		PlayerPrefs.SetInt("TurnLength3",145);
		PlayerPrefs.SetInt("TurnLength4",35);
		PlayerPrefs.SetInt("TurnAngle1",4);
		PlayerPrefs.SetInt("TurnAngle2",1);
		PlayerPrefs.SetInt("TurnAngle3",1);
		PlayerPrefs.SetInt("TurnAngle4",4);
		PlayerPrefs.SetInt("StartLine",5);
		PlayerPrefs.SetInt("SpeedOffset",31 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Mid");
	}

	static void Daytona(){
		circuitChoice = "Daytona";
		PlayerPrefs.SetInt("RaceLaps",4);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",2);
		PlayerPrefs.SetInt("StraightLength2",200);
		PlayerPrefs.SetInt("StraightLength3",500);
		PlayerPrefs.SetInt("StraightLength4",200);
		PlayerPrefs.SetInt("TurnLength1",30);
		PlayerPrefs.SetInt("TurnLength2",150);
		PlayerPrefs.SetInt("TurnLength3",150);
		PlayerPrefs.SetInt("TurnLength4",30);
		PlayerPrefs.SetInt("TurnAngle1",4);
		PlayerPrefs.SetInt("TurnAngle2",4);
		PlayerPrefs.SetInt("TurnAngle3",4);
		PlayerPrefs.SetInt("TurnAngle4",4);
		PlayerPrefs.SetInt("StartLine",1);
		PlayerPrefs.SetInt("SpeedOffset",8 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Plate");
	}

	static void Martinsville(){
		circuitChoice = "Martinsville";
		PlayerPrefs.SetInt("RaceLaps",9);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",150);
		PlayerPrefs.SetInt("StraightLength2",150);
		PlayerPrefs.SetInt("StraightLength3",0);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",180);
		PlayerPrefs.SetInt("TurnLength2",180);
		PlayerPrefs.SetInt("TurnLength3",0);
		PlayerPrefs.SetInt("TurnLength4",0);
		PlayerPrefs.SetInt("TurnAngle1",1);
		PlayerPrefs.SetInt("TurnAngle2",1);
		PlayerPrefs.SetInt("TurnAngle3",0);
		PlayerPrefs.SetInt("TurnAngle4",0);
		PlayerPrefs.SetInt("StartLine",50);
		PlayerPrefs.SetInt("SpeedOffset",75 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",2);
		PlayerPrefs.SetString("TrackType","Short");
	}
	
	static void LosAngeles(){
		circuitChoice = "LosAngeles";
		PlayerPrefs.SetInt("RaceLaps",10);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",20);
		PlayerPrefs.SetInt("StraightLength2",0);
		PlayerPrefs.SetInt("StraightLength3",0);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",5);
		PlayerPrefs.SetInt("TurnLength2",170);
		PlayerPrefs.SetInt("TurnLength3",10);
		PlayerPrefs.SetInt("TurnLength4",170);
		PlayerPrefs.SetInt("TurnLength5",5);
		PlayerPrefs.SetInt("TurnAngle1",8);
		PlayerPrefs.SetInt("TurnAngle2",1);
		PlayerPrefs.SetInt("TurnAngle3",8);
		PlayerPrefs.SetInt("TurnAngle4",1);
		PlayerPrefs.SetInt("TurnAngle5",8);
		PlayerPrefs.SetInt("StartLine",10);
		PlayerPrefs.SetInt("SpeedOffset",85 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",5);
		PlayerPrefs.SetString("TrackType","Short");
	}

	static void Madison(){
		circuitChoice = "Madison";
		PlayerPrefs.SetInt("RaceLaps",7);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",250);
		PlayerPrefs.SetInt("StraightLength2",250);
		PlayerPrefs.SetInt("StraightLength3",0);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",170);
		PlayerPrefs.SetInt("TurnLength2",190);
		PlayerPrefs.SetInt("TurnLength3",0);
		PlayerPrefs.SetInt("TurnLength4",0);
		PlayerPrefs.SetInt("TurnAngle1",2);
		PlayerPrefs.SetInt("TurnAngle2",2);
		PlayerPrefs.SetInt("TurnAngle3",1);
		PlayerPrefs.SetInt("TurnAngle4",1);
		PlayerPrefs.SetInt("StartLine",150);
		PlayerPrefs.SetInt("SpeedOffset",36 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",2);
		PlayerPrefs.SetString("TrackType","Mid");
	}
	
	static void Nashville(){
		circuitChoice = "Nashville";
		PlayerPrefs.SetInt("RaceLaps",6);
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",1);
		PlayerPrefs.SetInt("StraightLength2",1);
		PlayerPrefs.SetInt("StraightLength3",200);
		PlayerPrefs.SetInt("StraightLength4",10);
		PlayerPrefs.SetInt("TurnLength1",35);
		PlayerPrefs.SetInt("TurnLength2",145);
		PlayerPrefs.SetInt("TurnLength3",145);
		PlayerPrefs.SetInt("TurnLength4",35);
		PlayerPrefs.SetInt("TurnAngle1",4);
		PlayerPrefs.SetInt("TurnAngle2",1);
		PlayerPrefs.SetInt("TurnAngle3",1);
		PlayerPrefs.SetInt("TurnAngle4",4);
		PlayerPrefs.SetInt("StartLine",5);
		PlayerPrefs.SetInt("SpeedOffset",17 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",4);
		PlayerPrefs.SetString("TrackType","Mid");
	}

	static void TestTrack(){
		circuitChoice = "TestTrack";
		PlayerPrefs.SetInt("RaceLaps",1 * PlayerPrefs.GetInt("RaceLapsMultiplier"));
		PlayerPrefs.SetInt("CircuitLanes",3);
		PlayerPrefs.SetInt("StraightLength1",50);
		PlayerPrefs.SetInt("StraightLength2",50);
		PlayerPrefs.SetInt("StraightLength3",0);
		PlayerPrefs.SetInt("StraightLength4",0);
		PlayerPrefs.SetInt("TurnLength1",180);
		PlayerPrefs.SetInt("TurnLength2",180);
		PlayerPrefs.SetInt("TurnLength3",0);
		PlayerPrefs.SetInt("TurnLength4",0);
		PlayerPrefs.SetInt("TurnAngle1",1);
		PlayerPrefs.SetInt("TurnAngle2",1);
		PlayerPrefs.SetInt("TurnAngle3",1);
		PlayerPrefs.SetInt("TurnAngle4",1);
		PlayerPrefs.SetInt("StartLine",25);
		PlayerPrefs.SetInt("SpeedOffset",77 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",2);
	}

	static void CustomTrack(){
		circuitChoice = PlayerPrefs.GetString("CustomCircuitGround") + PlayerPrefs.GetInt("CustomCircuitLanes").ToString() + "Lanes";
		PlayerPrefs.SetInt("RaceLaps",PlayerPrefs.GetInt("CustomCircuitLaps") * PlayerPrefs.GetInt("RaceLapsMultiplier"));
		PlayerPrefs.SetInt("CircuitLanes",PlayerPrefs.GetInt("CustomCircuitLanes"));
		PlayerPrefs.SetInt("StraightLength1",PlayerPrefs.GetInt("CustomStraightLength1"));
		PlayerPrefs.SetInt("StraightLength2",PlayerPrefs.GetInt("CustomStraightLength2"));
		PlayerPrefs.SetInt("StraightLength3",PlayerPrefs.GetInt("CustomStraightLength3"));
		PlayerPrefs.SetInt("StraightLength4",PlayerPrefs.GetInt("CustomStraightLength4"));
		PlayerPrefs.SetInt("TurnLength1",PlayerPrefs.GetInt("CustomTurnAngle1"));
		PlayerPrefs.SetInt("TurnLength2",PlayerPrefs.GetInt("CustomTurnAngle2"));
		PlayerPrefs.SetInt("TurnLength3",PlayerPrefs.GetInt("CustomTurnAngle3"));
		PlayerPrefs.SetInt("TurnLength4",PlayerPrefs.GetInt("CustomTurnAngle4"));
		PlayerPrefs.SetInt("TurnAngle1",PlayerPrefs.GetInt("CustomTurnLength1"));
		PlayerPrefs.SetInt("TurnAngle2",PlayerPrefs.GetInt("CustomTurnLength2"));
		PlayerPrefs.SetInt("TurnAngle3",PlayerPrefs.GetInt("CustomTurnLength3"));
		PlayerPrefs.SetInt("TurnAngle4",PlayerPrefs.GetInt("CustomTurnLength4"));
		PlayerPrefs.SetInt("StartLine", Mathf.FloorToInt(PlayerPrefs.GetInt("CustomStraightLength1")/2));
		PlayerPrefs.SetInt("SpeedOffset",25 - speedFactor);
		PlayerPrefs.SetInt("TotalTurns",PlayerPrefs.GetInt("CustomCircuitTurns"));
	}
}
