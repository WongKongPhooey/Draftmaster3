using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class ChampionshipHubUI : MonoBehaviour
{

	public GameObject championshipRow;
	public Transform standingsFrame;
	
	public GameObject hubTitle;
	public GameObject hubRound;
	public GameObject nextTrackLabel;
	public GameObject hubTrackImage;
	
	public GameObject hubTrackName;
	public static int[] tracksArray;
	
	public GameObject hubCarImage;
	
	public GameObject nextRound;
	
	public GameObject goRaceBtn;
	public GameObject endSeasonBtn;

	public bool championshipOver;
	
	public static string seriesPrefix;
	public static bool modSeries;
	public int carNumber;

	string currentSeriesIndex;
	string currentModSeries;
	int currentSeries;
	int currentSubseries;
	string championshipSubseries;
	string currentSeriesName;
	string currentSubseriesName;
	string currentTrack;
	string championshipTracklist;
	int championshipRound;
	int championshipLength;
	static Dictionary<int, int> championshipPoints = new Dictionary<int, int>();
	
	public int championshipPosition;
	
	public static string circuitChoice;
	
    // Start is called before the first frame update
    void Start(){
		
		if(PlayerPrefs.HasKey("MidRaceLoading")){
			PlayerPrefs.DeleteKey("MidRaceLoading");
		}
		
		//Delete Pre-Race Stored Variables
		if(PlayerPrefs.HasKey("ActivePath")){
			PlayerPrefs.DeleteKey("ActivePath");
		}
		if(PlayerPrefs.HasKey("RaceMoment")){
			PlayerPrefs.DeleteKey("RaceMoment");
		}
		if(PlayerPrefs.HasKey("StartingLap")){
			PlayerPrefs.DeleteKey("StartingLap");
		}
		if(PlayerPrefs.HasKey("CustomRaceLaps")){
			PlayerPrefs.DeleteKey("CustomRaceLaps");
		}
		if(PlayerPrefs.HasKey("RaceModifier")){
			PlayerPrefs.DeleteKey("RaceModifier");
		}
		if(PlayerPrefs.HasKey("CustomFieldOrder")){
			PlayerPrefs.DeleteKey("CustomFieldOrder");
		}
		if(PlayerPrefs.HasKey("MomentComplete")){
			PlayerPrefs.DeleteKey("MomentComplete");
		}
		if(PlayerPrefs.HasKey("RaceAILevel")){
			PlayerPrefs.DeleteKey("RaceAILevel");
		}
		if(PlayerPrefs.HasKey("SpawnFromCaution")){
			PlayerPrefs.DeleteKey("SpawnFromCaution");
		}
		if(PlayerPrefs.HasKey("RaceAltPaintsChosen")){
			PlayerPrefs.DeleteKey("RaceAltPaintsChosen");
		}
		if(PlayerPrefs.HasKey("SeriesPrizeAmt")){
			PlayerPrefs.DeleteKey("SeriesPrizeAmt");
		}

		for(int i=0;i<100;i++){
			if(PlayerPrefs.HasKey("RaceAltPaint" + i)){
				PlayerPrefs.DeleteKey("RaceAltPaint" + i);
				//Debug.Log("Reset Alt Paints");
			}
			PlayerPrefs.DeleteKey("CautionPosition" + i + "");
			PlayerPrefs.DeleteKey("DNFPosition" + i + "");
			PlayerPrefs.DeleteKey("DNFLap" + i + "");
		}
		
		currentSeriesIndex = PlayerPrefs.GetString("CurrentSeriesIndex");
		currentModSeries = "";
		//Sneak custom modded series IDs in here (otherwise blank)
		if(PlayerPrefs.HasKey("CurrentModSeries")){
			currentModSeries = PlayerPrefs.GetString("CurrentModSeries");
		}
		//Debug.Log("Index: " + currentSeriesIndex);
		carNumber = PlayerPrefs.GetInt("CarChoice");
		currentSeries = int.Parse(currentSeriesIndex.Substring(0,1));
		currentSubseries = int.Parse(currentSeriesIndex.Substring(1));
		championshipTracklist = PlayerPrefs.GetString("SeriesChampionship" + currentModSeries + currentSeriesIndex + "Tracklist");
		
		//Testing Long Championships Fast
		#if UNITY_EDITOR
		//PlayerPrefs.SetInt("SeriesChampionship" + currentSeriesIndex + "Round",4);
		//Debug.Log("Championship Round hacked to R4.");
		#endif
		
		championshipRound = PlayerPrefs.GetInt("SeriesChampionship" + currentModSeries + currentSeriesIndex + "Round");
		championshipLength = PlayerPrefs.GetInt("SeriesChampionship" + currentModSeries + currentSeriesIndex + "Length");
		seriesPrefix = PlayerPrefs.GetString("SeriesChampionship" + currentModSeries + currentSeriesIndex + "CarSeries");
		PlayerPrefs.SetString("carSeries",seriesPrefix);
		Debug.Log("Champ Carset: " + seriesPrefix);
		PlayerPrefs.SetString("RaceType","Championship");
		
		modSeries = false;
		if(ModData.isModSeries(seriesPrefix) == true){
			modSeries = true;
		}
		
		SeriesData.loadSeries();
		loadPoints(currentSeriesIndex);
		
		hubTitle = GameObject.Find("Title");
		hubTitle.GetComponent<TMPro.TMP_Text>().text = SeriesData.offlineSeries[currentSeries, currentSubseries];
    
		nextRound = GameObject.Find("NextRound");
		nextRound.GetComponent<TMPro.TMP_Text>().text = "Round " + (championshipRound + 1) + "/" + championshipLength;
		
		//Debug.Log("Tracklist: " + championshipTracklist);
		tracksArray = championshipTracklist.Split(',').Select(int.Parse).ToArray();
		
		hubCarImage = GameObject.Find("NextCar");
		if(ModData.isModSeries(seriesPrefix) == true){
			hubCarImage.GetComponent<RawImage>().texture = ModData.getTexture(seriesPrefix,carNumber);
		} else {
			if(PlayerPrefs.HasKey(seriesPrefix + carNumber + "AltPaint")){
				hubCarImage.GetComponent<RawImage>().texture = Resources.Load<Texture2D>(seriesPrefix + "livery" + carNumber + "alt" + PlayerPrefs.GetInt(seriesPrefix + carNumber + "AltPaint"));
			} else {
				hubCarImage.GetComponent<RawImage>().texture = Resources.Load<Texture2D>(PlayerPrefs.GetString("carTexture"));
			}
		}
		nextTrackLabel = GameObject.Find("NextTrackLabel");
		
		if((championshipRound + 1) > championshipLength){
			if(championshipLength <= 1){
				//Bugged? Stop the infinite rewards glitch
				PlayerPrefs.DeleteKey("ChampionshipSubseries");
				SceneManager.LoadScene("Menus/MainMenu");
			}
			hubTitle.GetComponent<TMPro.TMP_Text>().text = "Championship Finished";
			nextRound.GetComponent<TMPro.TMP_Text>().text = "Championship Over";
			nextTrackLabel.GetComponent<TMPro.TMP_Text>().text = MiscScripts.PositionPostfix(championshipPosition) + " Place";
			championshipOver = true;
			championshipRound = 0;
		} else {
			nextTrackLabel.GetComponent<TMPro.TMP_Text>().text = TrackData.getTrackName(tracksArray[championshipRound]);
		}
		
		hubTrackImage = GameObject.Find("NextTrack");
		hubTrackImage.GetComponent<RawImage>().texture = Resources.Load<Texture2D>(TrackData.getTrackImage(tracksArray[championshipRound]));

		if(championshipOver == true){
			goRaceBtn = GameObject.Find("GoRace");
			goRaceBtn.SetActive(false);
			PlayerPrefs.SetInt("PlayerFinishPosition", championshipPosition);
		} else {
			endSeasonBtn = GameObject.Find("EndSeason");
			endSeasonBtn.SetActive(false);
		}
	}

	public void loadPoints(string currentSeriesIndex){
		championshipPoints.Clear();
		int pointsTableInd = 0;
		for(int i=0;i<100;i++){
			if(modSeries == true){
				if(ModData.getCarNum(seriesPrefix,i) == 999){
					//Non-valid car number
					continue;
				}
				int carNo = ModData.getCarNum(seriesPrefix,i);
				if(!PlayerPrefs.HasKey("SeriesChampionship" + currentSeriesIndex + "Points"  + carNo)){
					//Debug.Log("Set points for: " + carNo);
					PlayerPrefs.SetInt("SeriesChampionship" + currentSeriesIndex + "Points"  + carNo,0);
				}
				//Debug.Log("Index:" + i + " - Num:" + carNo);
				championshipPoints.Add(carNo,PlayerPrefs.GetInt("SeriesChampionship" + currentSeriesIndex + "Points"  + carNo));
			} else {
				if(DriverNames.getName(seriesPrefix,i) == null){
					continue;
				}
				if(!PlayerPrefs.HasKey("SeriesChampionship" + currentSeriesIndex + "Points" + i)){
					PlayerPrefs.SetInt("SeriesChampionship" + currentSeriesIndex + "Points" + i,0);
				}
				championshipPoints.Add(i,PlayerPrefs.GetInt("SeriesChampionship" + currentSeriesIndex + "Points" + i));
			}
			pointsTableInd++;
		}
		showPoints();
	}

	public void showPoints(){
		
		//Reset standings to blank
		foreach (Transform child in standingsFrame){
			Destroy(child.gameObject);
		}
		
		List<KeyValuePair<int, int>> pointsTable = new List<KeyValuePair<int, int>>(championshipPoints);
		
		pointsTable.Sort(
			delegate(KeyValuePair<int, int> firstPair,
			KeyValuePair<int, int> nextPair)
			{
				return nextPair.Value.CompareTo(firstPair.Value);
			}
		);
			
		int pointsInd = 0;
		foreach(var pointsRow in pointsTable){
			
			int carInd = 999;
			if(modSeries == true){
				carInd = ModData.getJsonIndexFromCarNum(seriesPrefix, pointsRow.Key);
				if(ModData.getName(seriesPrefix, carInd) == null){
					continue;
				}
			}
			
			GameObject standingsInst = Instantiate(championshipRow, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
			RectTransform resultObj = standingsInst.GetComponent<RectTransform>();
			standingsInst.transform.SetParent(standingsFrame, false);
			standingsInst.GetComponent<UIAnimate>().animOffset = pointsInd+1;
			standingsInst.GetComponent<UIAnimate>().scaleIn();
			
			TMPro.TMP_Text champPos = standingsInst.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
			RawImage champNum = standingsInst.transform.GetChild(1).GetComponent<RawImage>();
			TMPro.TMP_Text champFallbackNum = standingsInst.transform.GetChild(2).GetComponent<TMPro.TMP_Text>();
			TMPro.TMP_Text champDriver = standingsInst.transform.GetChild(3).GetComponent<TMPro.TMP_Text>();
			RawImage champManu = standingsInst.transform.GetChild(4).GetComponent<RawImage>();
			TMPro.TMP_Text champPoints = standingsInst.transform.GetChild(5).GetComponent<TMPro.TMP_Text>();
			
			if(pointsRow.Key == carNumber){
				championshipPosition = pointsInd+1;
			}
			
			champPos.text = (pointsInd+1).ToString();
			
			if(modSeries == true){
				//Debug.Log("# " + pointsRow.Key + " has " + pointsRow.Value.ToString() + " points.");
				int carNum = pointsRow.Key;
				if(PlayerPrefs.HasKey("CustomNumber" + seriesPrefix + ModData.getJsonIndexFromCarNum(seriesPrefix,pointsRow.Key))){
					carNum = PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + ModData.getJsonIndexFromCarNum(seriesPrefix,pointsRow.Key));
				}
				if (Resources.Load<Texture2D>("cup20num" + carNum) != null) {
					champNum.texture = Resources.Load<Texture2D>("cup20num" + carNum);
					champFallbackNum.enabled = false;
				} else {
					champNum.enabled = false;
					champFallbackNum.text = carNum.ToString();
				}
				champDriver.text = ModData.getName(seriesPrefix, carInd);
				champManu.texture = Resources.Load<Texture2D>("Icons/manu-" + ModData.getManufacturer(seriesPrefix, carInd));
			} else {
				champNum.texture = Resources.Load<Texture2D>("cup20num" + pointsRow.Key);
				champDriver.text = DriverNames.getName(seriesPrefix, pointsRow.Key);
				champManu.texture = Resources.Load<Texture2D>("Icons/manu-" + DriverNames.getManufacturer(seriesPrefix, pointsRow.Key));
				champFallbackNum.enabled = false;
			}
			champPoints.text = pointsRow.Value.ToString();
			pointsInd++;
		}
	}
}