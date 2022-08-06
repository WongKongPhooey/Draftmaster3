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
	string currentSeriesIndex;
	public static int seriesFuel;
	public static string trackCodeName;
	
	public static string seriesPrefix;
	public static int carNumber;
	
    // Start is called before the first frame update
    void Start()
    {
		string trackList = PlayerPrefs.GetString("SeriesTrackList");
		string currentSeriesIndex = PlayerPrefs.GetString("CurrentSeriesIndex");
        loadAllTracks(trackList);
		
		carNumber = PlayerPrefs.GetInt("CarChoice");
		seriesPrefix = "cup20";
		seriesPrefix = PlayerPrefs.GetString("carSeries");
		
		seriesFuel = PlayerPrefs.GetInt("SeriesFuel");
    }

	public void loadAllTracks(string tracks){
		
		TrackData.loadTrackNames();
		
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
		//Debug.Log(circuitChoice + " Loaded");
		//setRaceLaps();
		
		//Testing only
		//PlayerPrefs.SetInt("RaceLaps",2);
		
		//PlayerPrefs.SetString("CurrentTrack","" + order);
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
		//Testing
		//PlayerPrefs.SetInt("RaceLaps", 2);
	}

	public static void startRace(string track){
		
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
			
			//Debug.Log("-" + seriesFuel + " Fuel, now " + GameData.gameFuel);
			PlayerPrefs.SetInt("GameFuel",GameData.gameFuel);
			SceneManager.LoadScene(track);
		} else {
			//Roll back and bail
			PlayerPrefs.SetString("StoreFocus","Fuel");
			SceneManager.LoadScene("Store");
		}
	}

	public void dynamicBackButton(){
		SceneManager.LoadScene("Menus/Garage");
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
