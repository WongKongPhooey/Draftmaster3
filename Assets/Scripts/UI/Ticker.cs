using UnityEngine;
using UnityEngine.UI;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class Ticker : MonoBehaviour
{
	
	public static string seriesPrefix;
	public static bool officialSeries;

	public static string circuitName;
	public static int raceLaps;

	public static bool gamePaused;

	public static int fieldSize;

	public static int tickerUpdateIndex;
	public static int frameRedraws;
	public static int maxFrameRedraws;

	public static GameObject playerCar;
	public static int playerCarNum;
	public static string playerCarName;
	public static string playerDisplayName;
	public static int playerDisplayNum;
	public static float playerPosition;
	public static int position;
	public static int championshipPosition;
	public static int playerPoints;
	
	public static bool carsTagged;
	public static int playerOneNum;
	public static string liveryName;
	public static float leaderDist;
	public static GameObject ticker;
	public static GameObject playerTicker;
	
	public static bool hasLed;

	public static string[] cachedCarNames = new string[100];
	public static int[] cachedCarNumbers = new int[100];
	public static int[] cachedCarIndexes = new int[100];

	public static GameObject[] carsArray = new GameObject[50];
	public static float[] carPositions = new float[50];
	public static string[] carNames = new string[50];
	public static string[] carNumber = new string[50];
	public static string[] driverNames = new string[50];
	public static float[] carDist = new float[50];
	public static GameObject[] wreckedCarsArray = new GameObject[50];
	public static float[] wreckedCarPositions = new float[50];
	public static string[] wreckedCarNames = new string[50];
	public static string[] wreckedCarNumber = new string[50];
	public static string[] wreckedDriverNames = new string[50];
	public static float[] wreckedCarDist = new float[50];
	public static List<GameObject> entrantList = new List<GameObject>();

	public static GameObject[] cautionCarsArray = new GameObject[50];
	public static float[] cautionCarPositions = new float[50];
	public static string[] cautionCarNames = new string[50];

	public static string[] carChampNames = new string[50];
	public static int[] carChampPoints = new int[50];
	
	public static float[] orderedPositions = new float[50];
	public static string[] orderedNames = new string[50];

	public static int totalCarWins;
	public static int totalCarTopFives;

	public GameObject tickerChild;
	public Transform tickerObj;
	
	public static GameObject tickerFlag;
	public static Image tickerFlagImg;
	public static GameObject tickerLaps;
	public static TMPro.TMP_Text tickerLapsLbl;
	public static GameObject tickerLocation;
	public static GameObject cautionSummaryMenu;
	public static GameObject mainCam;
	public static GameObject pauseMenu;
	
	public static TMPro.TMP_Text[] tickerPositions = new TMPro.TMP_Text[50];
	public static Image[] tickerNums = new Image[50];
	public static TMPro.TMP_Text[] tickerFallbackNums = new TMPro.TMP_Text[50];
	public static TMPro.TMP_Text[] tickerNames = new TMPro.TMP_Text[50];
	public static TMPro.TMP_Text[] tickerDists = new TMPro.TMP_Text[50];

    // Start is called before the first frame update
    void Awake(){
        
		cautionSummaryMenu = GameObject.Find("CautionMenu");
		mainCam = GameObject.Find("Main Camera");
		pauseMenu = GameObject.Find("PauseMenu");
		gamePaused = false;
		
		tickerUpdateIndex = 0;
		frameRedraws = 0;
		maxFrameRedraws = 2;
		
		if(PlayerPrefs.HasKey("FixedSeries")){
			seriesPrefix = PlayerPrefs.GetString("FixedSeries");
		} else {
			seriesPrefix = PlayerPrefs.GetString("carSeries");
		}
		
		if(DriverNames.isOfficialSeries(seriesPrefix) == true){
			officialSeries = true;
		} else {
			officialSeries = false;
		}
		
		circuitName = PlayerPrefs.GetString("CurrentCircuit");
		
		raceLaps = PlayerPrefs.GetInt("RaceLaps");
		
		ticker = GameObject.Find("Ticker");
		playerTicker = GameObject.Find("PlayerTicker");
		tickerFlag = GameObject.Find("RaceState");
		tickerFlagImg = tickerFlag.GetComponent<Image>();
		tickerLaps = GameObject.Find("RaceLaps");
		tickerLapsLbl = tickerLaps.GetComponent<TMPro.TMP_Text>();
		tickerLocation = GameObject.Find("RaceName");
		tickerLocation.GetComponent<TMPro.TMP_Text>().text = PlayerPrefs.GetString("TrackLocation");
		
		carsTagged = false;
		findAllCars();
		playerCarNum = PlayerPrefs.GetInt("CarChoice");
		if(officialSeries == true){
			//Grab any custom values
			if(PlayerPrefs.HasKey("CustomNumber" + seriesPrefix + playerCarNum)){
				playerDisplayNum = PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + playerCarNum);
			} else {
				playerDisplayNum = playerCarNum;
			}
			if(PlayerPrefs.HasKey("CustomDriver" + seriesPrefix + playerCarNum)){
				playerDisplayName = PlayerPrefs.GetString("CustomDriver" + seriesPrefix + playerCarNum);
			} else {
				if(AltPaints.getAltPaintDriver(seriesPrefix,playerCarNum,PlayerPrefs.GetInt(seriesPrefix + playerCarNum + "AltPaint")) != null){
					playerDisplayName = AltPaints.getAltPaintDriver(seriesPrefix,playerCarNum,PlayerPrefs.GetInt(seriesPrefix + playerCarNum + "AltPaint"));
				} else {
					playerDisplayName = DriverNames.getName(seriesPrefix,playerCarNum);
				}
			}
		} else {
			//Mod series
			int playerCarInd = ModData.getJsonIndexFromCarNum(seriesPrefix,playerCarNum);
			if(PlayerPrefs.HasKey("CustomNumber" + seriesPrefix + playerCarInd)){
				playerDisplayNum = PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + playerCarInd);
			} else {
				playerDisplayNum = playerCarNum;
			}
			if(PlayerPrefs.HasKey("CustomDriver" + seriesPrefix + playerCarInd)){
				playerDisplayName = PlayerPrefs.GetString("CustomDriver" + seriesPrefix + playerCarInd);
			} else {
				playerDisplayName = ModData.getName(seriesPrefix,playerCarInd);
			}
		}
		
		hasLed = false;
    }

	public void findAllCars(){
		playerCar = GameObject.FindGameObjectWithTag("Player");
		carsArray = GameObject.FindGameObjectsWithTag("AICar");
		fieldSize = carsArray.Length;
	}

	public void populateTickerData(){

		if((carsTagged == true)||(carsArray.Length == 0)){
			return;
		}
		
		if((carsTagged == false)&&(carsArray.Length > 0)){
			for(int t=0;t<carsArray.Length;t++){
				//Initiate ticker object
				GameObject tickerInst = Instantiate(tickerChild, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
				tickerInst.transform.SetParent(tickerObj, false);
			}
			
			int i=0;
			entrantList.Clear();
			foreach(GameObject car in carsArray){
				carPositions[i] = car.transform.position.z;
				carNames[i] = car.transform.name;
				carNumber[i] = carNames[i].Remove(0,6);
				if(DriverNames.isOfficialSeries(seriesPrefix) == true){
					cachedCarIndexes[int.Parse(carNumber[i])] = int.Parse(carNumber[i]);
					driverNames[i] = DriverNames.getName(seriesPrefix,int.Parse(carNumber[i]));
					if(PlayerPrefs.HasKey("CustomNumber" + seriesPrefix + carNumber[i])){
						//carNumber[i] = PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + carNumber[i]).ToString();
						cachedCarNumbers[int.Parse(carNumber[i])] = PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + carNumber[i]);
					} else {
						cachedCarNumbers[int.Parse(carNumber[i])] = int.Parse(carNumber[i]);
					}
					
					//Cache in an array to avoid calls during runtime
					cachedCarNames[int.Parse(carNumber[i])] = driverNames[i];
				} else {
					int carJsonIndex = ModData.getJsonIndexFromCarNum(seriesPrefix, int.Parse(carNumber[i]));
					cachedCarIndexes[int.Parse(carNumber[i])] = carJsonIndex;
					driverNames[i] = ModData.getName(seriesPrefix,carJsonIndex);
					
					if(PlayerPrefs.HasKey("CustomNumber" + seriesPrefix + carJsonIndex)){
						cachedCarNumbers[int.Parse(carNumber[i])] = PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + carJsonIndex);
					} else {
						cachedCarNumbers[int.Parse(carNumber[i])] = int.Parse(carNumber[i]);
					}
					
					//Cache in an array to avoid calls during runtime
					cachedCarNames[int.Parse(carNumber[i])] = driverNames[i];
				}

				carDist[i] = 0.000f;
				entrantList.Add(car);
				i++;
			}
			carsTagged = true;
			
			//Manually add the player
			entrantList.Add(playerCar);
			GameObject playerTick = Instantiate(tickerChild, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
			playerTick.transform.SetParent(tickerObj, false);
			updateTicker();
		}
	}

	public static void updateTickerDisplay(){
		//Start updating the ticker timer
		//from wherever it stopped on the previous frame
		frameRedraws=0;
		for(int i=tickerUpdateIndex;i<entrantList.Count;i++){
			//Only update a limited number per frame, for performance
			if(frameRedraws >= maxFrameRedraws){
				tickerUpdateIndex = i;
				return;
			}
			if(carNumber[i] == null){
				tickerUpdateIndex = 0;
				return;
			}

			//Fill the static arrays (heavy, on start frames)
			if(tickerPositions[i] == null){
				tickerPositions[i] = ticker.transform.GetChild(i).transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
				tickerNums[i] = ticker.transform.GetChild(i).transform.GetChild(1).GetComponent<Image>();
				tickerFallbackNums[i] = ticker.transform.GetChild(i).transform.GetChild(2).GetComponent<TMPro.TMP_Text>();
				tickerNames[i] = ticker.transform.GetChild(i).transform.GetChild(3).GetComponent<TMPro.TMP_Text>();
				tickerDists[i] = ticker.transform.GetChild(i).transform.GetChild(4).GetComponent<TMPro.TMP_Text>();
			}

			//Redraw the TMPText (heavy performance hit)
			tickerPositions[i].text = (i+1).ToString();

			int displayNumber = cachedCarNumbers[int.Parse(carNumber[i])];
			if(entrantList[i].name == playerCar.name){
				displayNumber = playerDisplayNum;
			}
			
			//Show the correct number icon, or a fallback number
			if(Resources.Load<Sprite>("cup20num" + displayNumber) != null){
				tickerNums[i].overrideSprite = Resources.Load<Sprite>("cup20num" + displayNumber);
				tickerNums[i].color = new Color32(255,255,225,255);
				tickerFallbackNums[i].text = "";
			} else {
				tickerNums[i].color = new Color32(255,255,225,0);
				tickerFallbackNums[i].text = carNumber[i];
			}
			
			tickerNames[i].text = cachedCarNames[int.Parse(carNumber[i])];
			
			tickerDists[i].text = "+" + carDist[i].ToString("f3");
			if(entrantList[i].name == playerCar.name){
				tickerNames[i].text = playerDisplayName;
				tickerDists[i].text = leaderDist.ToString("f3");
			}
			
			frameRedraws++;
		}
		//If it ever completes this loop, 
		//then it must have reached the end of the list.
		//So reset the start index
		tickerUpdateIndex = 0;
	}

	public static void updateTicker(){
		//Don't update the ticker until the game has loaded all the car data into it
		if(carsTagged == false){
			return;
		}
		
		playerPosition = playerCar.transform.position.z;
		
		int j=0;
		if(carsArray.Length == 0){
			playerCar = GameObject.FindGameObjectWithTag("Player");
			carsArray = GameObject.FindGameObjectsWithTag("AICar");
			foreach (GameObject car in carsArray) {
				entrantList.Add(car);
				j++;
			}
			entrantList.Add(playerCar);
		}
		if(entrantList.Count == 0){
			return;
		}
		
		//Sort by z axis position
        entrantList.Sort(delegate(GameObject a, GameObject b) {
		  return (a.transform.position.z).CompareTo(b.transform.position.z);
		});
		//Reverse the sort
		entrantList.Reverse();
		
		for(int i=0;i<entrantList.Count;i++){
			
			if(entrantList[i].name == playerCar.name){
				position = i;
				if(position == 0){
					if(hasLed == false){
						hasLed = true;
						GameObject.Find("Main Camera").GetComponent<CommentaryManager>().commentate("NewLeader");
					}
				}
				
				leaderDist = (entrantList[0].transform.position.z) - (entrantList[i].transform.position.z);
				leaderDist = leaderDist / 25;
				
				TMPro.TMP_Text playerTickerPos = playerTicker.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
				Image playerTickerNum = playerTicker.transform.GetChild(1).GetComponent<Image>();
				TMPro.TMP_Text playerTickerFallbackNum = playerTicker.transform.GetChild(2).GetComponent<TMPro.TMP_Text>();
				TMPro.TMP_Text playerTickerName = playerTicker.transform.GetChild(3).GetComponent<TMPro.TMP_Text>();
				TMPro.TMP_Text playerTickerDist = playerTicker.transform.GetChild(4).GetComponent<TMPro.TMP_Text>();
				playerTickerPos.text = (i+1).ToString();
				
				if(Resources.Load<Sprite>("cup20num" + playerDisplayNum) != null){
					playerTickerNum.overrideSprite = Resources.Load<Sprite>("cup20num" + playerDisplayNum);
					playerTickerNum.color = new Color32(255,255,225,255);
					playerTickerFallbackNum.text = "";
				} else {
					playerTickerNum.color = new Color32(255,255,225,0);
					playerTickerFallbackNum.text = playerDisplayNum.ToString();
				}
				playerTickerName.text = playerDisplayName;
				playerTickerDist.text = leaderDist.ToString("f3");
			} else {
				carNames[i] = "" + entrantList[i].name;
				carNumber[i] = carNames[i].Remove(0,6);
				driverNames[i] = cachedCarNames[int.Parse(carNumber[i])];
				
				carDist[i] = (entrantList[0].transform.position.z) - (entrantList[i].transform.position.z);
				carDist[i] = carDist[i] / 25;
			}
		}
		updateTickerDisplay();
	}

	public static int checkPlayerPosition(){
		for(int i=0;i<entrantList.Count;i++){
			if(entrantList[i].name == playerCar.name){
				return i;
			}
		}
		return 0;
	}

	public static GameObject getRaceLeader(){
		return entrantList[0];
	}

	public static int checkSingleCarPosition(GameObject thisCarName){
		int pos = entrantList.IndexOf(thisCarName); // Returns 1.
		if(pos == -1){
			return 9999;
		}
		return pos;
	}

	public static void saveCautionPositions(bool playerPitted = false){
		
		//Lock in any alt paints for the restart
		PlayerPrefs.SetInt("RaceAltPaintsChosen",1);
		
		entrantList.Clear();
		
		int j=0;
		playerCar = GameObject.FindGameObjectWithTag("Player");
		carsArray = GameObject.FindGameObjectsWithTag("AICar");
		playerPosition = playerCar.transform.position.z;
		
		if(carsArray.Length > 0){
			foreach (GameObject car in carsArray) {
				entrantList.Add(car);
				j++;
			}
			carsTagged = true;
		}
		if(playerPitted == false){
			entrantList.Add(playerCar);
		}
		
		//Sort by z axis position
        entrantList.Sort(delegate(GameObject a, GameObject b) {
		  return (a.transform.position.z).CompareTo(b.transform.position.z);
		});
		
		//Add after sort, tags onto back
		if(playerPitted == true){
			entrantList.Add(playerCar);
		}
		//Reverse the sort
		entrantList.Reverse(); 
		
		//Now we have our caution order..
		//Shuffle all wrecked cars to the back
		//Then shuffle heavily damaged cars to the back of that new set
		
		for(int i=0;i<entrantList.Count;i++){
			if(entrantList[i].name == playerCar.name){
				position = i;
				carNames[i] = playerCar.transform.name;
				carNumber[i] = "" + playerCarNum + "";
				if(PlayerPrefs.HasKey(seriesPrefix + carNumber[i] + "AltDriver")){
					driverNames[i] = PlayerPrefs.GetString(seriesPrefix + carNumber[i] + "AltDriver");
				} else {
					int carJsonIndex = 999;
					if(officialSeries == true){
						driverNames[i] = DriverNames.getName(seriesPrefix,int.Parse(carNumber[i]));
					} else {
						carJsonIndex = ModData.getJsonIndexFromCarNum(seriesPrefix, int.Parse(carNumber[i]));
						driverNames[i] = ModData.getName(seriesPrefix,carJsonIndex);
					}
				}
				leaderDist = (entrantList[0].transform.position.z) - (entrantList[i].transform.position.z);
				leaderDist = leaderDist / 25;
				PlayerPrefs.SetInt("PlayerCautionPosition", i);
				//Debug.Log("Player is in P" + i);
			} else {
				carNames[i] = "" + entrantList[i].name;
				carNumber[i] = carNames[i].Remove(0,6);
				int carJsonIndex = 999;
				if(officialSeries == true){
					driverNames[i] = DriverNames.getName(seriesPrefix,int.Parse(carNumber[i]));
				} else {
					carJsonIndex = ModData.getJsonIndexFromCarNum(seriesPrefix, int.Parse(carNumber[i]));
					driverNames[i] = ModData.getName(seriesPrefix,carJsonIndex);
				}
				carDist[i] = (entrantList[0].transform.position.z) - (entrantList[i].transform.position.z);
				carDist[i] = carDist[i] / 25;
			}
			PlayerPrefs.SetInt("CautionPosition" + i + "", int.Parse(carNumber[i]));
		}
		PlayerPrefs.SetInt("CautionLap", CameraRotate.lap);
		PlayerPrefs.SetInt("SpawnFromCaution", 1);
		cautionSummaryMenu.SetActive(true);
		SceneManager.LoadScene(SceneManager.GetActiveScene().name);
	}

	public static void checkFinishPositions(){
		playerPosition = playerCar.transform.position.z;
		
		entrantList.Clear();
		
		int j=0;
		playerCar = GameObject.FindGameObjectWithTag("Player");
		carsArray = GameObject.FindGameObjectsWithTag("AICar");
		if(carsArray.Length > 0){
			foreach (GameObject car in carsArray) {
				entrantList.Add(car);
				j++;
			}
			carsTagged = true;
		}
		entrantList.Add(playerCar);
		
		//Sort by z axis position
        entrantList.Sort(delegate(GameObject a, GameObject b) {
		  return (a.transform.position.z).CompareTo(b.transform.position.z);
		});
		//Reverse the sort
		entrantList.Reverse(); 
		
		//Debug.Log(entrantList.Count + " cars to sort.");
		
		for(int i=0;i<entrantList.Count;i++){
			if(entrantList[i].name == playerCar.name){
				position = i;
				carNames[i] = playerCar.transform.name;
				carNumber[i] = "" + playerCarNum + "";
				if(PlayerPrefs.HasKey(seriesPrefix + carNumber[i] + "AltDriver")){
					driverNames[i] = PlayerPrefs.GetString(seriesPrefix + carNumber[i] + "AltDriver");
				} else {
					int carJsonIndex = 999;
					if(officialSeries == true){
						driverNames[i] = DriverNames.getName(seriesPrefix,int.Parse(carNumber[i]));
					} else {
						carJsonIndex = ModData.getJsonIndexFromCarNum(seriesPrefix, int.Parse(carNumber[i]));
						driverNames[i] = ModData.getName(seriesPrefix,carJsonIndex);
					}
				}
				leaderDist = (entrantList[0].transform.position.z) - (entrantList[i].transform.position.z);
				leaderDist = leaderDist / 25;
				
				//Adjust for array starting at index 0, not 1
				PlayerPrefs.SetInt("PlayerFinishPosition", i+1);
				//Debug.Log("Player finishes in P" + i);
			} else {
				carNames[i] = "" + entrantList[i].name;
				//carNumber[i] = Regex.Replace(carNames[i], "[^0-9]", "");
				carNumber[i] = carNames[i].Remove(0,6);
				
				int carJsonIndex = 999;
				if(officialSeries == true){
					driverNames[i] = DriverNames.getName(seriesPrefix,int.Parse(carNumber[i]));
				} else {
					carJsonIndex = ModData.getJsonIndexFromCarNum(seriesPrefix, int.Parse(carNumber[i]));
					driverNames[i] = ModData.getName(seriesPrefix,carJsonIndex);
				}
					
				carDist[i] = (entrantList[0].transform.position.z) - (entrantList[i].transform.position.z);
				carDist[i] = carDist[i] / 25;
				if(i == 1){
					MomentsCriteria.checkMomentsCriteria("WinningMargin",carDist[i].ToString());
					MomentsCriteria.checkMomentsCriteria("AIMustWin",carDist[i].ToString());
				}
				if(i == 2){
					MomentsCriteria.checkMomentsCriteria("WinToThirdLessThan",carDist[i].ToString());
				}
				if(i < 2){
					MomentsCriteria.checkMomentsCriteria("Top2Finish",carNumber[i],i.ToString());
					MomentsCriteria.checkMomentsCriteria("Top2FinishAlso",carNumber[i],i.ToString());
				}
				if(i < 3){
					MomentsCriteria.checkMomentsCriteria("Top3Finish",carNumber[i],i.ToString());
					MomentsCriteria.checkMomentsCriteria("Top3FinishAlso",carNumber[i],i.ToString());
				}
				if(i < 5){
					MomentsCriteria.checkMomentsCriteria("Top5Finish",carNumber[i],i.ToString());
					MomentsCriteria.checkMomentsCriteria("Top5FinishAlso",carNumber[i],i.ToString());
				}
			}
			PlayerPrefs.SetInt("FinishPosition" + i + "", int.Parse(carNumber[i]));
			PlayerPrefs.SetInt("FinishTime" + i + "", (int)Mathf.Round(carDist[i] * 1000));
		}
		
		if(position == 0){
			PlayerPrefs.SetInt("TotalWins",PlayerPrefs.GetInt("TotalWins") + 1);
			if(PlayerPrefs.HasKey("TotalWins" + seriesPrefix + playerCarNum)){
				totalCarWins = PlayerPrefs.GetInt("TotalWins" + seriesPrefix + playerCarNum);
				PlayerPrefs.SetInt("TotalWins" + seriesPrefix + playerCarNum,totalCarWins + 1);
			} else {
				PlayerPrefs.SetInt("TotalWins" + seriesPrefix + playerCarNum, 1);
			}
			PlayFabManager.SendLeaderboard(PlayerPrefs.GetInt("TotalWins"), "AllTimeMostWins","");
		}
		if(position <= 4){
			PlayerPrefs.SetInt("TotalTop5s",PlayerPrefs.GetInt("TotalTop5s") + 1);
			if(PlayerPrefs.HasKey("TotalWins" + seriesPrefix + playerCarNum)){
				totalCarTopFives = PlayerPrefs.GetInt("TotalTop5s" + seriesPrefix + playerCarNum);
				PlayerPrefs.SetInt("TotalTop5s" + seriesPrefix + playerCarNum,totalCarTopFives + 1);
			} else {
				PlayerPrefs.SetInt("TotalTop5s" + seriesPrefix + playerCarNum, 1);
			}
		}
		maxFrameRedraws = 50;
		updateTickerDisplay();
	}

	public static void togglePause(){
		if(gamePaused == false){
			gamePaused = true;
			mainCam.GetComponent<AudioListener>().enabled = false;
			PlayerPrefs.SetInt("Volume",0);
			pauseMenu.SetActive(true);
		}
	}

	public static void endChallenge(){
		Time.timeScale = 1.0f;
		RaceHUD.raceOver = false;
		SceneManager.LoadScene("Menus/RaceResults");
	}
	
	public static void retryChallenge(){
		Time.timeScale = 1.0f;
		RaceHUD.raceOver = false;
		PlayerPrefs.DeleteKey("CautionLap");
		PlayerPrefs.DeleteKey("SpawnFromCaution");
		SceneManager.LoadScene(SceneManager.GetActiveScene().name);
	}
	
	public static void quitChallenge(){
		Time.timeScale = 1.0f;
		RaceHUD.raceOver = false;
		SceneManager.LoadScene("Menus/MainMenu");
	}

    // Update is called once per frame
    void Update(){
		
		//If the index isn't 0
		//The ticker hasn't finished updating yet
		if(tickerUpdateIndex != 0){
			updateTickerDisplay();
		}
		
		//ticker.transform.Translate(-1.5f,0,0);
		if(CameraRotate.overtime == true){
			tickerLapsLbl.text = "OVERTIME";
		} else {
			tickerLapsLbl.text = "LAP " + CameraRotate.lap + " OF " + CameraRotate.raceEnd;
		}
		if(CameraRotate.lap == CameraRotate.raceEnd){
			tickerFlagImg.color = new Color(255,255,255);
			tickerLapsLbl.text = "FINAL LAP";
		}
		if(carsTagged == false){
			if(carsArray.Length == 0){
				findAllCars();
			} else {
				populateTickerData();
			}
		}
		if(CameraRotate.cautionOut == true){
			tickerFlagImg.color = new Color(255,255,0);
		}
	}
}
