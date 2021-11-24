using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PointsTableGUI : MonoBehaviour{
	
	public GUISkin eightBitSkin;
	public GUISkin redGUI;
	
	static float widthblock = Mathf.Round(Screen.width/20);
	static float heightblock = Mathf.Round(Screen.height/20);

	public string seriesPrefix;
	public int carNumber;

	int currentSeries;
	int currentSubseries;
	string currentSeriesName;
	string currentSubseriesName;
	string currentTrack;
	int championshipRound;	
	static Dictionary<int, int> championshipPoints = new Dictionary<int, int>();
	
	public static string circuitChoice;
	
	public Vector2 scrollPosition = Vector2.zero;
	
    // Start is called before the first frame update
    void Start(){
		carNumber = PlayerPrefs.GetInt("CarChoice");
		
		currentSeries = PlayerPrefs.GetInt("CurrentSeries");
		currentSubseries = PlayerPrefs.GetInt("CurrentSubseries");
		championshipRound = PlayerPrefs.GetInt("ChampionshipRound");
        SeriesData.setData();
		loadPoints(); 
    }

    // Update is called once per frame
    void Update()
    {
        
    }
	
	void OnGUI() {
		
		GUI.skin = eightBitSkin;

		GUI.skin.label.fontSize = 72 / FontScale.fontScale;
		GUI.skin.button.fontSize = 72 / FontScale.fontScale;
		GUI.skin.verticalScrollbar.fixedWidth = Screen.width / 20;
		GUI.skin.verticalScrollbarThumb.fixedWidth = Screen.width / 20;
		
		GUI.Label(new Rect(widthblock * 4, heightblock / 2, widthblock * 15, heightblock * 2), SeriesData.offlineMenu[currentSeries] + " - " + SeriesData.offlineSeries[currentSeries,currentSubseries] + " - After All " + championshipRound + " Rounds");
		
		scrollPosition = GUI.BeginScrollView(new Rect(0, heightblock*3, widthblock*20, heightblock*17), scrollPosition, new Rect(0, heightblock*3, widthblock, Screen.height * 4.0f));

		GUI.skin.label.fontSize = 48 / FontScale.fontScale;
		GUI.skin.button.alignment = TextAnchor.MiddleLeft;
		showPoints(widthblock / 2, 3, carNumber);
		
		GUI.EndScrollView();
		
		GUI.skin = redGUI;
		CommonGUI.BackButton("MainMenu");
		GUI.skin = eightBitSkin;
	}
	
	static void loadPoints(){
		championshipPoints.Clear();
		int pointsTableInd = 0;
		for(int i=0;i<100;i++){
			if(PlayerPrefs.HasKey("ChampionshipPoints" + i)){
				championshipPoints.Add(i, PlayerPrefs.GetInt("ChampionshipPoints" + i));
				Debug.Log("# " + i + " has " + PlayerPrefs.GetInt("ChampionshipPoints" + i) + " points.");
				pointsTableInd++;
			} else {
				Debug.Log("No points found for #" + i);
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
		GUI.Label(new Rect(widthblock * 2.5f, heightblock * posY, widthblock * 3f, heightblock * 2), "Pos");	
		GUI.Label(new Rect(widthblock * 6.5f, heightblock * posY, widthblock * 4.5f, heightblock * 2), "Driver");
		GUI.Label(new Rect(widthblock * 12f, heightblock * posY, widthblock * 2.5f, heightblock * 2), "Team");
		GUI.Label(new Rect(widthblock * 15.5f, heightblock * posY, widthblock * 2.5f, heightblock * 2), "Pts");	
			
		
		int pointsTableInd = 0;
		foreach(var pointsRow in pointsTable){
			
			if(pointsRow.Key == carNumber){
				GUI.skin.label.normal.textColor = Color.red;
			}
			GUI.Label(new Rect(widthblock * 2.5f, heightblock * (((pointsTableInd + 1)*1.5f) + posY), widthblock * 3f, heightblock * 2), "" + (pointsTableInd + 1));	
			GUI.Label(new Rect(widthblock * 6.5f, heightblock * (((pointsTableInd + 1)*1.5f) + posY), widthblock * 4.5f, heightblock * 2), "" + DriverNames.cup2020Names[pointsRow.Key]);	
			GUI.Label(new Rect(widthblock * 12f, heightblock * (((pointsTableInd + 1)*1.5f) + posY), widthblock * 2.5f, heightblock * 2), "" + DriverNames.cup2020Teams[pointsRow.Key]);	
			GUI.Label(new Rect(widthblock * 15.5f, heightblock * (((pointsTableInd + 1)*1.5f) + posY), widthblock * 2.5f, heightblock * 2), "" + pointsRow.Value);	
			if(pointsRow.Key == carNumber){
				GUI.skin.label.normal.textColor = Color.black;
			}
			pointsTableInd++;
		}
	}
}
