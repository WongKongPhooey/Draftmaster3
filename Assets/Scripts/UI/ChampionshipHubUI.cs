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
    void Start()
    {
		carNumber = PlayerPrefs.GetInt("CarChoice");
		
		championshipSubseries = PlayerPrefs.GetString("ChampionshipSubseries");
		PlayerPrefs.SetString("CurrentSeriesIndex",championshipSubseries);
		currentSeries = int.Parse(championshipSubseries.Substring(0,1));
		currentSubseries = int.Parse(championshipSubseries.Substring(1));
		championshipTracklist = PlayerPrefs.GetString("ChampionshipTracklist");
		championshipRound = PlayerPrefs.GetInt("ChampionshipRound");
		championshipLength = PlayerPrefs.GetInt("ChampionshipLength");
		//Debug.Log("Season Length: " + championshipLength);
		seriesPrefix = PlayerPrefs.GetString("ChampionshipCarSeries");
		PlayerPrefs.SetString("carSeries",PlayerPrefs.GetString("ChampionshipCarSeries"));
		
		PlayerPrefs.SetString("RaceType","Championship");
        
		modSeries = false;
		if(ModData.isModSeries(seriesPrefix) == true){
			modSeries = true;
		}
		
		SeriesData.loadSeries();
		loadPoints();
		
		hubTitle = GameObject.Find("Title");
		hubTitle.GetComponent<TMPro.TMP_Text>().text = SeriesData.offlineSeries[currentSeries, currentSubseries];
    
		nextRound = GameObject.Find("NextRound");
		nextRound.GetComponent<TMPro.TMP_Text>().text = "Round " + (championshipRound + 1) + "/" + championshipLength;
		
		Debug.Log("Tracklist: " + championshipTracklist);
		tracksArray = championshipTracklist.Split(',');
		
		hubCarImage = GameObject.Find("NextCar");
		hubCarImage.GetComponent<RawImage>().texture = Resources.Load<Texture2D>(PlayerPrefs.GetString("carTexture"));
		
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

	public void loadPoints(){
		championshipPoints.Clear();
		int pointsTableInd = 0;
		for(int i=0;i<100;i++){
			if(PlayerPrefs.HasKey("ChampionshipPoints" + i)){
				championshipPoints.Add(i, PlayerPrefs.GetInt("ChampionshipPoints" + i));
				//Debug.Log("# " + i + " has " + PlayerPrefs.GetInt("ChampionshipPoints" + i) + " points.");
				pointsTableInd++;
			} else {
				//Debug.Log("No points found for #" + i);
			}
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
			
			if(pointsRow.Value == 0){
				//continue;
			}
			
			GameObject standingsInst = Instantiate(championshipRow, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
			RectTransform resultObj = standingsInst.GetComponent<RectTransform>();
			standingsInst.transform.SetParent(standingsFrame, false);
			standingsInst.GetComponent<UIAnimate>().animOffset = pointsInd+1;
			standingsInst.GetComponent<UIAnimate>().scaleIn();
			
			TMPro.TMP_Text champPos = standingsInst.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
			RawImage champNum = standingsInst.transform.GetChild(1).GetComponent<RawImage>();
			TMPro.TMP_Text champDriver = standingsInst.transform.GetChild(2).GetComponent<TMPro.TMP_Text>();
			RawImage champManu = standingsInst.transform.GetChild(3).GetComponent<RawImage>();
			TMPro.TMP_Text champPoints = standingsInst.transform.GetChild(4).GetComponent<TMPro.TMP_Text>();
			
			if(pointsRow.Key == carNumber){
				championshipPosition = pointsInd+1;
			}
			
			champPos.text = (pointsInd+1).ToString();
			champNum.texture = Resources.Load<Texture2D>("cup20num" + pointsRow.Key);
			if(modSeries == true){
				champDriver.text = ModData.getName(seriesPrefix, pointsRow.Key);
				champManu.texture = Resources.Load<Texture2D>("Icons/manu-" + ModData.getManufacturer(seriesPrefix, pointsRow.Key));
			} else {
				champDriver.text = DriverNames.getName(seriesPrefix, pointsRow.Key);
				champManu.texture = Resources.Load<Texture2D>("Icons/manu-" + DriverNames.getManufacturer(seriesPrefix, pointsRow.Key));
			
			}
			champPoints.text = pointsRow.Value.ToString();
			pointsInd++;
		}
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
