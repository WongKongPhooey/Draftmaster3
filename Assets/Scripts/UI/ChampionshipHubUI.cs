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
	public GameObject hubTrackImage;
	public GameObject hubTrackName;
	
	public GameObject nextRound;
	
	public static string seriesPrefix;
	public int carNumber;

	int currentSeries;
	int currentSubseries;
	string championshipSubseries;
	string currentSeriesName;
	string currentSubseriesName;
	string currentTrack;
	int championshipRound;
	int championshipLength;
	static Dictionary<int, int> championshipPoints = new Dictionary<int, int>();
	
	public static string circuitChoice;
	
    // Start is called before the first frame update
    void Start()
    {
		carNumber = PlayerPrefs.GetInt("CarChoice");
		
		championshipSubseries = PlayerPrefs.GetString("ChampionshipSubseries");
		currentSeries = int.Parse(championshipSubseries.Substring(0,1));
		currentSubseries = int.Parse(championshipSubseries.Substring(championshipSubseries.Length-1));
		championshipRound = PlayerPrefs.GetInt("ChampionshipRound");
		championshipLength = PlayerPrefs.GetInt("ChampionshipLength");
		seriesPrefix = PlayerPrefs.GetString("carSeries");
        
		SeriesData.loadSeries();
		loadPoints();
		
		hubTitle = GameObject.Find("Title");
		hubTitle.GetComponent<TMPro.TMP_Text>().text = SeriesData.offlineSeries[currentSeries, currentSubseries];
    
		nextRound = GameObject.Find("NextRound");
		nextRound.GetComponent<TMPro.TMP_Text>().text = "Round " + (championshipRound + 1) + "/" + championshipLength;
    
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
			
			//if(pointsRow.Key == carNumber){
			//	GUI.skin.label.normal.textColor = Color.red;
			//}
			
			champPos.text = (pointsInd+1).ToString();
			champNum.texture = Resources.Load<Texture2D>("cup20num" + pointsRow.Key);
			champDriver.text = DriverNames.getName(seriesPrefix, pointsRow.Key);
			champManu.texture = Resources.Load<Texture2D>("Icons/manu-" + DriverNames.getManufacturer(seriesPrefix, pointsRow.Key));
			champPoints.text = pointsRow.Value.ToString();
			pointsInd++;
		}
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
