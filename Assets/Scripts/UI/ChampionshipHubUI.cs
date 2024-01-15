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
	public static string[] tracksArray;
	
	public GameObject hubCarImage;
	
	public GameObject nextRound;
	
	public GameObject goRaceBtn;
	public GameObject endSeasonBtn;

	public bool championshipOver;
	
	public static string seriesPrefix;
	public static bool modSeries;
	public int carNumber;

	string currentSeriesIndex;
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
		currentSeriesIndex = PlayerPrefs.GetString("CurrentSeriesIndex");
		carNumber = PlayerPrefs.GetInt("CarChoice");
		Debug.Log("Current Series Index: " + currentSeriesIndex);
		currentSeries = int.Parse(currentSeriesIndex.Substring(0,1));
		currentSubseries = int.Parse(currentSeriesIndex.Substring(1));
		championshipTracklist = PlayerPrefs.GetString("SeriesChampionship" + currentSeriesIndex + "Tracklist");
		championshipRound = PlayerPrefs.GetInt("SeriesChampionship" + currentSeriesIndex + "Round");
		championshipLength = PlayerPrefs.GetInt("SeriesChampionship" + currentSeriesIndex + "Length");
		seriesPrefix = PlayerPrefs.GetString("SeriesChampionship" + currentSeriesIndex + "CarSeries");
		PlayerPrefs.SetString("carSeries",seriesPrefix);
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
		tracksArray = championshipTracklist.Split(',');
		
		hubCarImage = GameObject.Find("NextCar");
		if(ModData.isModSeries(seriesPrefix) == true){
			hubCarImage.GetComponent<RawImage>().texture = ModData.getTexture(seriesPrefix,carNumber);
		} else {
			hubCarImage.GetComponent<RawImage>().texture = Resources.Load<Texture2D>(PlayerPrefs.GetString("carTexture"));
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
			nextTrackLabel.GetComponent<TMPro.TMP_Text>().text = TrackData.getTrackName(int.Parse(tracksArray[championshipRound]));
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
				if(!PlayerPrefs.HasKey("ChampionshipPoints" + carNo)){
					//Debug.Log("Set points for: " + carNo);
					PlayerPrefs.SetInt("ChampionshipPoints" + carNo,0);
				}
				//Debug.Log("Index:" + i + " - Num:" + carNo);
				championshipPoints.Add(carNo,PlayerPrefs.GetInt("ChampionshipPoints" + carNo));
			} else {
				if(DriverNames.getName(seriesPrefix,i) == null){
					continue;
				}
				if(!PlayerPrefs.HasKey("SeriesChampionship" + currentSeriesIndex + "Points" + i)){
					PlayerPrefs.SetInt("SeriesChampionship" + currentSeriesIndex + "Points" + i,0);
				}
				championshipPoints.Add(i,PlayerPrefs.GetInt("ChampionshipPoints" + i));
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
			
			//Debug.Log("Points Row, Key:" + pointsRow.Key + " - Value:" + pointsRow.Value);
			
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
				if (Resources.Load<Texture2D>("cup20num" + pointsRow.Key) != null) {
					champNum.texture = Resources.Load<Texture2D>("cup20num" + pointsRow.Key);
					champFallbackNum.enabled = false;
				} else {
					champNum.enabled = false;
					champFallbackNum.text = pointsRow.Key.ToString();
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