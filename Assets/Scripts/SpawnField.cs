using UnityEngine;
using System.Collections;
using System.Text.RegularExpressions;

public class SpawnField : MonoBehaviour {

	public Transform AICarPrefab;

	string carNum;

	public static ArrayList fastCars = new ArrayList();
	public static ArrayList midCars = new ArrayList();
	public static ArrayList slowCars = new ArrayList();

	public float paceDistance;

	int gridRows;
	int gridLanes;
	int circuitLanes;
	int AILevel;
	
	int fieldSize;
	
	int currentSeries;
	int currentSubseries;
	
	string seriesPrefix;
	
	bool cautionRestart;
	public static int startLane;

	// Use this for initialization
	void Start () {

		int playerRow;
		
		seriesPrefix = PlayerPrefs.GetString("carSeries");

		gridLanes = 2;
		
		circuitLanes = PlayerPrefs.GetInt("CircuitLanes");
		
		currentSeries = PlayerPrefs.GetInt("CurrentSeries");
		currentSubseries = PlayerPrefs.GetInt("CurrentSubseries");
		
		startLane = Random.Range(1,3); //1 or 2

		AILevel = SeriesData.offlineAILevel[int.Parse(currentSeries.ToString()),int.Parse(currentSubseries.ToString())];
		if(AILevel>15){
			AILevel = 15;
		}

		spawnCars(seriesPrefix);
		//spawnCup2020Scenario();
		paceDistance = 2.5f;
		string carNumber = PlayerPrefs.GetString("carTexture");
		carNumber = PlayerPrefs.GetString("carTexture");			
		string splitAfter = "livery";
		carNumber = carNumber.Substring(carNumber.IndexOf(splitAfter) + splitAfter.Length);
		
		fastCars.Remove(carNumber);
		midCars.Remove(carNumber);
		slowCars.Remove(carNumber);
		
		fieldSize = fastCars.Count + midCars.Count + slowCars.Count;
		
		gridRows = Mathf.CeilToInt(fieldSize / 2);
		
		playerRow = Random.Range(AILevel,AILevel+5);
		//gridRows = 2;
		//playerRow = 2;

		string challengeName = PlayerPrefs.GetString("ChallengeType");

		switch(challengeName){
		case "NoFuel":
		case "CleanBreak":
		case "PhotoFinish":
			playerRow = 1;
			break;
		case "TeamPlayer":
		case "LatePush":
			playerRow = 5;
			break;
		case "LastToFirstLaps":
		case "TrafficJam":
			playerRow = gridRows - 1;
			break;
		default:
			break;
		}

		int carChoice;

		Object AICarInstance;

		//Cars In Front
		for (int i = playerRow - 1; i >= 1; i--) {
			for(int j=1;j<=gridLanes;j++){
				AICarInstance = Instantiate(AICarPrefab, new Vector3(0-(1.2f * (j-1)), 0.4f, i * paceDistance), Quaternion.identity);
				if(fastCars.Count > 0){
					carChoice = Random.Range(0,fastCars.Count);
					carNum = fastCars[carChoice].ToString();
					AICarInstance.name = ("AICar0" + carNum);
					GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = j+1;
					fastCars.RemoveAt(carChoice);
				} else {
					if(midCars.Count > 0){
						carChoice = Random.Range(0,midCars.Count);
						carNum = midCars[carChoice].ToString();
						AICarInstance.name = ("AICar0" + carNum);
						GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = j+1;
						midCars.RemoveAt(carChoice);
					} else {
						if(slowCars.Count > 0){
							carChoice = Random.Range(0,slowCars.Count);
							carNum = slowCars[carChoice].ToString();
							AICarInstance.name = ("AICar0" + carNum);
							GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = j+1;
							slowCars.RemoveAt(carChoice);
						}
					}
				}
			}
		}

		//Position the player car in it's start lane
		GameObject.Find("Player").transform.Translate(0-(1.2f * startLane),0f,0f);

		for(int j=1;j<=gridLanes;j++){
			if(j != startLane){
				AICarInstance = Instantiate(AICarPrefab, new Vector3(0-(1.2f * (j-1)), 0.4f, 0f), Quaternion.identity);
				if(fastCars.Count > 0){
					carChoice = Random.Range(0,fastCars.Count);
					carNum = fastCars[carChoice].ToString();
					AICarInstance.name = ("AICar0" + carNum);
					GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = j+1;
					fastCars.RemoveAt(carChoice);
				} else {
					if(midCars.Count > 0){
						carChoice = Random.Range(0,midCars.Count);
						carNum = midCars[carChoice].ToString();
						AICarInstance.name = ("AICar0" + carNum);
						GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = j+1;
						midCars.RemoveAt(carChoice);
					} else {
						if(slowCars.Count > 0){
							carChoice = Random.Range(0,slowCars.Count);
							carNum = slowCars[carChoice].ToString();
							AICarInstance.name = ("AICar0" + carNum);
							GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = j+1;
							slowCars.RemoveAt(carChoice);
						}
					}
				}
			}
		}

		//Cars Behind
		for (int i = 1; i < (gridRows - playerRow); i++) {
			for(int j=1;j<=gridLanes;j++){
				AICarInstance = Instantiate(AICarPrefab, new Vector3(0-(1.2f * (j-1)), 0.4f, i * -paceDistance), Quaternion.identity);
				if(fastCars.Count > 0){
					carChoice = Random.Range(0,fastCars.Count);
					carNum = fastCars[carChoice].ToString();
					AICarInstance.name = ("AICar0" + carNum);
					GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = j+1;
					fastCars.RemoveAt(carChoice);
				} else {
					if(midCars.Count > 0){
						carChoice = Random.Range(0,midCars.Count);
						carNum = midCars[carChoice].ToString();
						AICarInstance.name = ("AICar0" + carNum);
						GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = j+1;
						midCars.RemoveAt(carChoice);
					} else {
						if(slowCars.Count > 0){
							carChoice = Random.Range(0,slowCars.Count);
							carNum = slowCars[carChoice].ToString();
							AICarInstance.name = ("AICar0" + carNum);
							GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = j+1;
							slowCars.RemoveAt(carChoice);
						}
					}
				}
			}
		}
			
			//Lane 1
			/*if((PlayerPrefs.GetString("CurrentCircuit")=="Legends500")&&(PlayerPrefs.GetString("raceSeries")=="IndyCar")){
				AICarInstance = Instantiate(AICarPrefab, new Vector3(1.2f, 0.4f, i * -paceDistance), Quaternion.identity);
				if(fastCars.Count > 0){
					carChoice = Random.Range(0,fastCars.Count);
					carNum = fastCars[carChoice].ToString();
					AICarInstance.name = ("AICar0" + carNum);
					GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 1;
					fastCars.RemoveAt(carChoice);
				} else {
					carChoice = Random.Range(0,slowCars.Count);
					carNum = slowCars[carChoice].ToString();
					AICarInstance.name = ("AICar0" + carNum);
					GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 1;
					slowCars.RemoveAt(carChoice);
				}
			}
			
			//Lane 2
			if(PlayerPrefs.GetInt("CircuitLanes") == 3){
				AICarInstance = Instantiate(AICarPrefab, new Vector3(0, 0.4f, i * -paceDistance), Quaternion.identity);
				if(fastCars.Count > 0){
					carChoice = Random.Range(0,fastCars.Count);
					carNum = fastCars[carChoice].ToString();
					AICarInstance.name = ("AICar0" + carNum);
					GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 2;
					fastCars.RemoveAt(carChoice);
				} else {
					carChoice = Random.Range(0,slowCars.Count);
					carNum = slowCars[carChoice].ToString();
					AICarInstance.name = ("AICar0" + carNum);
					GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 2;
					slowCars.RemoveAt(carChoice);
				}
			}
			
			//Lane 3
			AICarInstance = Instantiate(AICarPrefab, new Vector3(-1.2f, 0.4f, i * -paceDistance), Quaternion.identity);
			if(fastCars.Count > 0){
				carChoice = Random.Range(0,fastCars.Count);
				carNum = fastCars[carChoice].ToString();
				AICarInstance.name = ("AICar0" + carNum);
				GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 3;
				fastCars.RemoveAt(carChoice);
			} else {
				carChoice = Random.Range(0,slowCars.Count);
				carNum = slowCars[carChoice].ToString();
				AICarInstance.name = ("AICar0" + carNum);
				GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 3;
				slowCars.RemoveAt(carChoice);
			}

			//Lane 4
			if(PlayerPrefs.GetInt("CircuitLanes") == 4){
				AICarInstance = Instantiate(AICarPrefab, new Vector3(-2.4f, 0.4f, i * -paceDistance), Quaternion.identity);
				if(fastCars.Count > 0){
					carChoice = Random.Range(0,fastCars.Count);
					carNum = fastCars[carChoice].ToString();
					AICarInstance.name = ("AICar0" + carNum);
					GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 4;
					fastCars.RemoveAt(carChoice);
				} else {
					if(slowCars.Count > 0){
						carChoice = Random.Range(0,slowCars.Count);
						carNum = slowCars[carChoice].ToString();
						AICarInstance.name = ("AICar0" + carNum);
						GameObject.Find("AICar0" + carNum).GetComponent<AIMovement>().lane = 4;
						slowCars.RemoveAt(carChoice);
					}
				}
			}
		}*/
		//Scoreboard.checkPositions();
		Scoreboard.updateScoreboard();
	}

	public static void spawnIndycars(){

		fastCars.Clear();
		midCars.Clear();
		slowCars.Clear();
	}

	public static void spawnCars(string seriesPref){
		fastCars.Clear();
		midCars.Clear();
		slowCars.Clear();
		
		for(int i=0;i<100;i++){
			switch(DriverNames.getRarity(seriesPref, i)){
				case 4:
					fastCars.Add("" + i + "");
					break;
				case 3:
					fastCars.Add("" + i + "");
					break;
				case 2:
					midCars.Add("" + i + "");
					break;
				case 1:
					slowCars.Add("" + i + "");
					break;
				default:
					break;
			}
		}
		
		/*fastCars.Add("1");
		fastCars.Add("2");
		fastCars.Add("3");
		fastCars.Add("4");
		fastCars.Add("5");
		fastCars.Add("6");
		fastCars.Add("8");
		fastCars.Add("9");
		fastCars.Add("10");
		fastCars.Add("11");
		fastCars.Add("12");
		fastCars.Add("13");
		fastCars.Add("14");
		fastCars.Add("18");
		fastCars.Add("19");
		fastCars.Add("20");
		fastCars.Add("21");
		fastCars.Add("22");
		fastCars.Add("24");
		fastCars.Add("42");
		fastCars.Add("43");
		fastCars.Add("48");
		fastCars.Add("88");
		fastCars.Add("95");
		slowCars.Add("0");
		slowCars.Add("7");
		slowCars.Add("15");
		slowCars.Add("16");
		slowCars.Add("17");
		slowCars.Add("27");
		slowCars.Add("32");
		slowCars.Add("34");
		slowCars.Add("36");
		slowCars.Add("37");
		slowCars.Add("38");
		slowCars.Add("41");
		slowCars.Add("47");
		slowCars.Add("49");
		slowCars.Add("51");
		slowCars.Add("52");
		slowCars.Add("53");
		slowCars.Add("54");
		slowCars.Add("62");
		slowCars.Add("66");
		slowCars.Add("74");
		slowCars.Add("77");
		slowCars.Add("78");
		slowCars.Add("96");*/
	}
	
	public static void spawnCup2020Scenario(){
		fastCars.Clear();
		midCars.Clear();
		slowCars.Clear();
		
		fastCars.Add("1");
		fastCars.Add("18");
		fastCars.Add("2");
		//fastCars.Add("9");
	}

	public static void spawnStockcars(){

		fastCars.Clear();
		midCars.Clear();
		slowCars.Clear();

		fastCars.Add("3");
		fastCars.Add("24");
		fastCars.Add("30");
		fastCars.Add("43");
		fastCars.Add("48");
		fastCars.Add("52");
		fastCars.Add("88");
		
		slowCars.Add("0");
		slowCars.Add("1");
		slowCars.Add("2");
		slowCars.Add("4");
		slowCars.Add("5");
		slowCars.Add("6");
		slowCars.Add("7");
		slowCars.Add("8");
		slowCars.Add("9");
		slowCars.Add("10");
		slowCars.Add("11");
		slowCars.Add("12");
		slowCars.Add("13");
		slowCars.Add("14");
		slowCars.Add("15");
		slowCars.Add("16");
		slowCars.Add("17");
		slowCars.Add("18");
		slowCars.Add("19");
		slowCars.Add("20");
		slowCars.Add("21");
		slowCars.Add("22");
		slowCars.Add("23");
		slowCars.Add("25");
		slowCars.Add("27");
		slowCars.Add("28");
		slowCars.Add("29");
		slowCars.Add("33");
		slowCars.Add("37");
		slowCars.Add("41");
		slowCars.Add("44");
		slowCars.Add("46");
		slowCars.Add("50");
		slowCars.Add("51");
		slowCars.Add("55");
		slowCars.Add("66");
		slowCars.Add("67");
		
		if(RacePoints.championshipMode == true){
			slowCars.Add("71");
			slowCars.Add("77");
			slowCars.Add("80");
			slowCars.Add("89");
			slowCars.Add("91");
			slowCars.Add("92");
			slowCars.Add("94");
			slowCars.Add("99");
		}
	}
}