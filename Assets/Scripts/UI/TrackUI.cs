using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TrackUI : MonoBehaviour
{
	
	public GameObject trackTile;
	public Transform tileFrame;
	public static string[] tracksArray;
	int seriesLength;
	public static int seriesFuel;
	public static string trackCodeName;
	
	public static string seriesPrefix;
	public static int carNumber;
	
	public static string currentSeriesIndex;
	
	public static string trackList;
	
	public static int championshipRound;
	public GameObject championshipSelector;
	
    // Start is called before the first frame update
    void Start()
    {
		trackList = PlayerPrefs.GetString("SeriesTrackList");
		currentSeriesIndex = PlayerPrefs.GetString("CurrentSeriesIndex");
        loadAllTracks(trackList);
		championshipSelector = GameObject.Find("ChampionshipSelector");
		championshipSelector.transform.GetChild(0).GetComponent<TMPro.TMP_Text>().text = "All " + seriesLength + " Rounds";
		championshipSelector.transform.GetChild(1).GetComponent<TMPro.TMP_Text>().text = "x" + seriesLength + " race rewards received at the end of the season.";
		
		carNumber = PlayerPrefs.GetInt("CarChoice");
		seriesPrefix = "cup20";
		seriesPrefix = PlayerPrefs.GetString("carSeries");
		
		seriesFuel = PlayerPrefs.GetInt("SeriesFuel");
		
		//Check for an active championship
		if(PlayerPrefs.HasKey("ChampionshipSubseries")){
			Debug.Log("There's an active championship..");
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
					loadTrack(trackList, championshipRound);
					PlayerPrefs.SetString("CurrentTrack","" + championshipRound);
					//loadPoints();
				}
			}
		} else {
			Debug.Log("No active championship");
		}
    }

	public void loadAllTracks(string tracks){
		
		TrackData.loadTrackNames();
		TrackData.loadTrackCodeNames();
		
		foreach (Transform child in tileFrame){
			//Destroy(child.gameObject);
		}
		
		//If there's a track list loaded
		if(tracks != ""){
			tracksArray = tracks.Split(',');
		} else {
			tracks = "1,2,3,4,5";
			tracksArray = tracks.Split(',');
		}
		seriesLength = tracksArray.Length;
		
		if(seriesLength == 1){
			//If there's only 1 race in this Event/Series
			//Jump straight into it
			loadTrack(tracks,0);
			startRace(TrackData.getTrackCodeName(int.Parse(tracksArray[0])));
		}
		
		for(int i=0;i<seriesLength;i++){
			GameObject tileInst = Instantiate(trackTile, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
			int trackId = int.Parse(tracksArray[i]);
			tileInst.GetComponent<TrackUIFunctions>().trackId = trackId;
			tileInst.GetComponent<TrackUIFunctions>().trackCodeName = TrackData.getTrackCodeName(trackId);
			tileInst.transform.SetParent(tileFrame, false);
			tileInst.GetComponent<UIAnimate>().animOffset = i+1;
			tileInst.GetComponent<UIAnimate>().scaleIn();
			
			TMPro.TMP_Text trackName = tileInst.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
			TMPro.TMP_Text raceLaps = tileInst.transform.GetChild(1).GetComponent<TMPro.TMP_Text>();
			TMPro.TMP_Text bestFinish = tileInst.transform.GetChild(3).GetComponent<TMPro.TMP_Text>();
			RawImage trackImage = tileInst.transform.GetChild(4).GetComponent<RawImage>();
			if(PlayerPrefs.HasKey("BestFinishPosition" + currentSeriesIndex + i) == true){
				bestFinish.text = PlayerPrefs.GetInt("BestFinishPosition" + currentSeriesIndex + i).ToString();
			} else {
				bestFinish.text = "N/A";
			}
			trackName.text = getTrackName(tracksArray[i]);
			raceLaps.text = "Laps " + PlayerPrefs.GetInt("RaceLaps");
			trackImage.texture = Resources.Load<Texture2D>(getTrackImage(tracksArray[i]));
		}
	}

	public void loadTrack(string tracks, int seriesIndex){
		
		TrackData.loadTrackNames();
		
		foreach (Transform child in tileFrame){
			Destroy(child.gameObject);
		}
		
		//If there's a track list loaded
		if(tracks != ""){
			tracksArray = tracks.Split(',');
		} else {
			tracks = "1,2,3,4,5";
			tracksArray = tracks.Split(',');
		}
		seriesLength = tracksArray.Length;
		int trackId = int.Parse(tracksArray[championshipRound]);
		string trackCodeName = TrackData.trackCodeNames[trackId];
		
		GameObject tileInst = Instantiate(trackTile, new Vector3(transform.position.x,transform.position.y, transform.position.z), Quaternion.identity);
		tileInst.GetComponent<TrackUIFunctions>().trackId = trackId;
		tileInst.GetComponent<TrackUIFunctions>().trackCodeName = TrackData.getTrackCodeName(trackId);
		tileInst.transform.SetParent(tileFrame, false);
		tileInst.GetComponent<UIAnimate>().animOffset = 1;
		tileInst.GetComponent<UIAnimate>().scaleIn();
		
		TMPro.TMP_Text trackName = tileInst.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
		TMPro.TMP_Text raceLaps = tileInst.transform.GetChild(1).GetComponent<TMPro.TMP_Text>();
		TMPro.TMP_Text bestFinish = tileInst.transform.GetChild(3).GetComponent<TMPro.TMP_Text>();
		RawImage trackImage = tileInst.transform.GetChild(4).GetComponent<RawImage>();
		if(PlayerPrefs.HasKey("BestFinishPosition" + currentSeriesIndex + seriesIndex) == true){
			bestFinish.text = PlayerPrefs.GetInt("BestFinishPosition" + currentSeriesIndex + seriesIndex).ToString();
		} else {
			bestFinish.text = "N/A";
		}
		trackName.text = getTrackName(tracksArray[seriesIndex]);
		raceLaps.text = "Laps " + PlayerPrefs.GetInt("RaceLaps");
		trackImage.texture = Resources.Load<Texture2D>(getTrackImage(tracksArray[seriesIndex]));
		if(PlayerPrefs.GetString("RaceType") == "Championship"){
			startRace(trackCodeName);
		}
	}
	
	string getTrackImage(string trackId){
		string trackImageName = "";
		switch(trackId){
			case "1":
				trackImageName = "SuperTriOval";
				break;
			case "2":
				trackImageName = "AngledTriOval";
				break;
			case "3":
				trackImageName = "TriOval";
				break;
			case "4":
				trackImageName = "Phoenix";
				break;
			case "5":
				trackImageName = "SuperTriOval";
				break;
			case "6":
				trackImageName = "LongOval";
				break;
			case "7":
				trackImageName = "AngledTriOval";
				break;
			case "8":
				trackImageName = "TinyOval";
				break;
			case "9":
				trackImageName = "TriOval";
				break;
			case "10":
				trackImageName = "Talladega";
				break;
			case "11":
				trackImageName = "SmallOval";
				break;
			case "12":
				trackImageName = "TriOval";
				break;
			case "13":
				trackImageName = "AngledTriOval";
				break;
			case "14":
				trackImageName = "LongPond";
				break;
			case "15":
				trackImageName = "SuperTriOval";
				break;
			case "16":
				trackImageName = "TriOval";
				break;
			case "17":
				trackImageName = "TriOval";
				break;
			case "18":
				trackImageName = "LongOval";
				break;
			case "19":
				trackImageName = "Darlington";
				break;
			case "20":
				trackImageName = "Indianapolis";
				break;
			case "21":
				trackImageName = "BigOval";
				break;
			case "22":
				trackImageName = "Madison";
				break;
			case "23":
				trackImageName = "TriOval";
				break;
			case "30":
				trackImageName = "TinyOval";
				break;
			default:
				break;
		}
		return trackImageName;
	}
	
	static string getTrackName(string trackId){
		string trackName = "";
		TrackData.loadTrackNames();
		TrackData.loadTrackCodeNames();
		trackName = TrackData.trackNames[int.Parse(trackId)];
		return trackName;
	}

	void setRaceLaps(){
		//Adjust laps based on AI difficulty
		int AIDiff = PlayerPrefs.GetInt("AIDifficulty");
		int baseLaps = PlayerPrefs.GetInt("RaceLaps");
		int raceLapsMultiplier = (AIDiff / 8) + 1;
		PlayerPrefs.SetInt("RaceLaps", Mathf.FloorToInt(baseLaps * raceLapsMultiplier));
		//Debug.Log("Race Laps: " + baseLaps + " * " + raceLapsMultiplier);
	}

	public static void startRace(string track){
		
		//This loads the track data
		GameObject.Find("Main").GetComponent<TrackData>().loadTrackData(track);
		
		PlayerPrefs.SetString("TrackLocation",track);
		PlayerPrefs.SetString("CurrentCircuit", track);
		if(GameData.gameFuel >= seriesFuel){
			GameData.gameFuel-=seriesFuel;
			
			PlayerPrefs.SetInt("TotalStarts",PlayerPrefs.GetInt("TotalStarts") + 1);
			if(PlayerPrefs.HasKey("TotalStarts" + seriesPrefix + carNumber)){
				PlayerPrefs.SetInt("TotalStarts" + seriesPrefix + carNumber,PlayerPrefs.GetInt("TotalStarts" + seriesPrefix + carNumber) + 1);
				//Debug.Log("Increment Total Starts: " + seriesPrefix + ", " + carNumber);
			} else {
				//Debug.Log("First Start: " + seriesPrefix + ", " + carNumber);
				PlayerPrefs.SetInt("TotalStarts" + seriesPrefix + carNumber, 1);
			}
			
			//Legacy fix for incorrect start count
			if(PlayerPrefs.GetInt("TotalStarts" + seriesPrefix + carNumber) < PlayerPrefs.GetInt("TotalTop5s" + seriesPrefix + carNumber)){
				PlayerPrefs.SetInt("TotalStarts" + seriesPrefix + carNumber , PlayerPrefs.GetInt("TotalTop5s" + seriesPrefix + carNumber));
			}
			
			//Testing
			//PlayerPrefs.SetInt("RaceLaps",1);
			
			//Debug.Log("-" + seriesFuel + " Fuel, now " + GameData.gameFuel);
			PlayerPrefs.SetInt("GameFuel",GameData.gameFuel);
			SceneManager.LoadScene(track);
		} else {
			//Roll back and bail
			//PlayerPrefs.SetString("StoreFocus","Fuel");
			//SceneManager.LoadScene("Menus/StoreUI");
			AlertManager.showPopup("Not Enough Fuel", "You need more fuel to race. Watch an ad or buy fuel in the Store, or simply wait for the fuel to refill.", "Icons/fuel");
		}
	}

	public void startChampionship(){
		DriverPoints.resetPoints(seriesPrefix);
		PlayerPrefs.SetString("ChampionshipSubseries",currentSeriesIndex);
		PlayerPrefs.SetString("ChampionshipTracklist",PlayerPrefs.GetString("SeriesTrackList"));
		PlayerPrefs.SetString("RaceType","Championship");
		loadTrack(trackList, 0);
		PlayerPrefs.SetInt("ChampionshipRound",0);
		championshipRound = 0;
		PlayerPrefs.SetInt("ChampionshipLength", seriesLength);
		//resetChampionshipPoints();
		PlayerPrefs.SetString("ChampionshipCarTexture", PlayerPrefs.GetString("carTexture"));
		PlayerPrefs.SetString("ChampionshipCarSeries", PlayerPrefs.GetString("carSeries"));
		//Debug.Log("Championship Carset Series set as " + PlayerPrefs.GetString("ChampionshipCarSeries"));
		PlayerPrefs.SetInt("ChampionshipCarChoice", PlayerPrefs.GetInt("CarChoice"));
		SceneManager.LoadScene("Menus/ChampionshipHub");
	}

	public void dynamicBackButton(){
		SceneManager.LoadScene("Menus/Garage");
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
