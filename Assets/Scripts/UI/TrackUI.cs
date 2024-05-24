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
	
	public GameObject alertPopup;
	
    // Start is called before the first frame update
    void Start()
    {
		trackList = PlayerPrefs.GetString("SeriesTrackList");
		currentSeriesIndex = PlayerPrefs.GetString("CurrentSeriesIndex");
        loadAllTracks(trackList);
		
		if(PlayerPrefs.GetString("RaceType") == "Championship"){
			if(PlayerPrefs.HasKey("SeriesChampionship" + currentSeriesIndex + "Round")){
				seriesLength = PlayerPrefs.GetInt("SeriesChampionship" + currentSeriesIndex + "Length", seriesLength);
				PlayerPrefs.SetString("SeriesChampionship" + currentSeriesIndex + "Tracklist",PlayerPrefs.GetString("SeriesTrackList"));
				PlayerPrefs.SetInt("SeriesChampionship" + currentSeriesIndex + "Length", seriesLength);
			}
		}
		
		//Reset any left over prefs from previous races
		for(int i=0;i<99;i++){
			PlayerPrefs.DeleteKey("CautionPosition" + i + "");
			PlayerPrefs.DeleteKey("FinishPosition" + i + "");
			PlayerPrefs.DeleteKey("DNFPosition" + i + "");
			PlayerPrefs.DeleteKey("DNFLap" + i + "");
		}
		
		championshipSelector = GameObject.Find("ChampionshipSelector");
		championshipSelector.transform.GetChild(0).GetComponent<TMPro.TMP_Text>().text = "All " + seriesLength + " Rounds";
		championshipSelector.transform.GetChild(1).GetComponent<TMPro.TMP_Text>().text = "x" + seriesLength + " race rewards received at the end of the season.";
		
		carNumber = PlayerPrefs.GetInt("CarChoice");
		seriesPrefix = "cup20";
		seriesPrefix = PlayerPrefs.GetString("carSeries");
		
		seriesFuel = PlayerPrefs.GetInt("SeriesFuel");
		
		alertPopup = GameObject.Find("AlertPopup");
		
		//If race type is Championship (Set in the Championship Hub screen)
		if(PlayerPrefs.GetString("RaceType") == "Championship"){
			Debug.Log("There's an active championship here.. " + currentSeriesIndex);
			seriesPrefix = PlayerPrefs.GetString("SeriesChampionship" + currentSeriesIndex + "CarSeries");
			PlayerPrefs.SetString("carSeries", seriesPrefix);
			//Debug.Log("SeriesChampionship" + currentSeriesIndex + "Carset loaded as " + seriesPrefix);
			
			//Found a championship round set
			championshipRound = PlayerPrefs.GetInt("SeriesChampionship" + currentSeriesIndex + "Round");
			Debug.Log("Current Round: " + championshipRound);
			if(championshipRound >= seriesLength){
				//Championship is over, reset
				PlayerPrefs.DeleteKey("ChampionshipSubseries");
				PlayerPrefs.SetInt("SeriesChampionship" + currentSeriesIndex + "Round", 0);
				PlayerPrefs.SetString("RaceType","Single");
			} else {
				//Debug.Log("Championship - Round " + championshipRound + "/" + seriesLength);
				loadTrack(trackList, championshipRound);
				PlayerPrefs.SetString("CurrentTrack","" + championshipRound);
			}
		} else {
			championshipRound = 0;
			//Debug.Log("No active championship for this series " + currentSeriesIndex);
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
			championshipRound = 0;
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
			raceLaps.text = "Laps " + calcRaceLaps(TrackData.getTrackLaps(trackId));
			trackImage.texture = Resources.Load<Texture2D>(TrackData.getTrackImage(tracksArray[i]));
		}
	}

	public void loadTrack(string tracks, int seriesIndex){
		
		TrackData.loadTrackNames();
		
		foreach (Transform child in tileFrame){
			Destroy(child.gameObject);
		}
		
		Debug.Log("Championship Round: " + championshipRound);

		//If there's a track list loaded
		if(tracks != ""){
			tracksArray = tracks.Split(',');
		} else {
			tracks = "1,2,3,4,5";
			tracksArray = tracks.Split(',');
		}
		seriesLength = tracksArray.Length;
		//Debug.Log("Track ID:" + tracksArray[championshipRound] );
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
		trackImage.texture = Resources.Load<Texture2D>(TrackData.getTrackImage(tracksArray[seriesIndex]));
		if(PlayerPrefs.GetString("RaceType") == "Championship"){
			startRace(trackCodeName);
		}
	}
	
	static string getTrackName(string trackId){
		string trackName = "";
		TrackData.loadTrackNames();
		TrackData.loadTrackCodeNames();
		trackName = TrackData.trackNames[int.Parse(trackId)];
		return trackName;
	}

	public static int calcRaceLaps(int baseLaps){
		int AIDiff = PlayerPrefs.GetInt("AIDifficulty");
		int raceLapsMultiplier = (AIDiff / 4) + 1;
		//Debug.Log("Diff Adjusted Race Laps: " + baseLaps + " * " + raceLapsMultiplier);
		return Mathf.FloorToInt(baseLaps * raceLapsMultiplier);
	}

	public static void setRaceLaps(){
		//Adjust laps based on AI difficulty
		int AIDiff = PlayerPrefs.GetInt("AIDifficulty");
		int baseLaps = PlayerPrefs.GetInt("RaceLaps");
		int raceLapsMultiplier = (AIDiff / 4) + 1;
		PlayerPrefs.SetInt("RaceLaps", Mathf.FloorToInt(baseLaps * raceLapsMultiplier));
		//Debug.Log("Diff Adjusted Race Laps: " + baseLaps + " * " + raceLapsMultiplier);
	}

	public static void startRace(string track){
		
		//This loads the track data
		GameObject.Find("Main").GetComponent<TrackData>().loadTrackData(track);
		GameObject alertPopup = GameObject.Find("AlertPopup");
		
		setRaceLaps();
		
		PlayerPrefs.SetString("TrackLocation",track);
		PlayerPrefs.SetString("CurrentCircuit", track);
		//if(GameData.gameFuel >= seriesFuel){
			//GameData.gameFuel-=seriesFuel;
		
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
		#if UNITY_EDITOR
		//PlayerPrefs.SetInt("RaceLaps",4);
		#endif
		SceneManager.LoadScene(track);
	}

	public void startChampionship(){
		DriverPoints.resetPoints(currentSeriesIndex,seriesPrefix);
		PlayerPrefs.SetString("RaceType","Championship");
		PlayerPrefs.SetInt("SeriesChampionship" + currentSeriesIndex + "Round", 0);
		PlayerPrefs.SetString("SeriesChampionship" + currentSeriesIndex + "Tracklist",PlayerPrefs.GetString("SeriesTrackList"));
		PlayerPrefs.SetInt("SeriesChampionship" + currentSeriesIndex + "Length", seriesLength);
		PlayerPrefs.SetString("SeriesChampionship" + currentSeriesIndex + "CarTexture", PlayerPrefs.GetString("carTexture"));
		PlayerPrefs.SetString("SeriesChampionship" + currentSeriesIndex + "CarSeries", PlayerPrefs.GetString("carSeries"));
		PlayerPrefs.SetInt("SeriesChampionship" + currentSeriesIndex + "CarChoice", PlayerPrefs.GetInt("CarChoice"));
		loadTrack(trackList, 0);
		championshipRound = 0;
		resetChampionshipPoints(currentSeriesIndex);
		SceneManager.LoadScene("Menus/ChampionshipHub");
	}

	public void resetChampionshipPoints(string seriesIndex){
		for(int i=0;i<100;i++){
			//Empty them all out
			PlayerPrefs.DeleteKey("SeriesChampionship" + seriesIndex + "Points" + i);
		}
	}

	public void dynamicBackButton(){
		SceneManager.LoadScene("Menus/Garage");
	}

    // Update is called once per frame
    void Update(){
    }
}
