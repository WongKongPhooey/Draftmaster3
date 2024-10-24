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
			
			//Manually add the player
			entrantList.Add(playerCar);
			GameObject playerTick = Instantiate(tickerChild, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
			playerTick.transform.SetParent(tickerObj, false);
			
			for(int d=0;d<99;d++){
				//Initiate ticker objects for DNF cars
				if(!PlayerPrefs.HasKey("DNFPosition" + d + "")){
					continue;
				}
				
				//Debug.Log("DNF'd Car " + d);
				
				GameObject tickerInst = Instantiate(tickerChild, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
				tickerInst.transform.SetParent(tickerObj, false);
				
				int displayNumber = PlayerPrefs.GetInt("DNFPosition" + d + "");
				int customNumber = displayNumber;
				if(PlayerPrefs.HasKey("CustomNumber" + seriesPrefix + displayNumber)){
					customNumber = PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + displayNumber);
				}
				
				tickerInst.transform.GetChild(0).GetComponent<TMPro.TMP_Text>().text = d.ToString();

				//Debug.Log("DNF'd Car #" + customNumber + "");

				//Show the correct number icon, or a fallback number
				if(Resources.Load<Sprite>("cup20num" + customNumber) != null){
					tickerInst.transform.GetChild(1).GetComponent<Image>().overrideSprite = Resources.Load<Sprite>("cup20num" + customNumber);
					tickerInst.transform.GetChild(1).GetComponent<Image>().color = new Color32(255,255,225,255);
					tickerInst.transform.GetChild(2).GetComponent<TMPro.TMP_Text>().text = "";
				} else {
					tickerInst.transform.GetChild(1).GetComponent<Image>().color = new Color32(255,255,225,0);
					tickerInst.transform.GetChild(2).GetComponent<TMPro.TMP_Text>().text = customNumber.ToString();
				}
				tickerInst.transform.GetChild(3).GetComponent<TMPro.TMP_Text>().text = DriverNames.getName(seriesPrefix,displayNumber);
				tickerInst.transform.GetChild(4).GetComponent<TMPro.TMP_Text>().text = "DNF - LAP " + PlayerPrefs.GetInt("DNFLap" + d + "") + "";
			}
			
			carsTagged = true;
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

	public static void saveCautionPositions(bool waitForReload = false){
		
		//Lock in any alt paints for the restart
		PlayerPrefs.SetInt("RaceAltPaintsChosen",1);
		
		//Reset any previous saved caution data
		for(int i=0;i<99;i++){
			PlayerPrefs.DeleteKey("CautionPosition" + i + "");
		}
		
		entrantList.Clear();
		
		int j=0;
		playerCar = GameObject.FindGameObjectWithTag("Player");
		carsArray = GameObject.FindGameObjectsWithTag("AICar");
		playerPosition = playerCar.transform.position.z;
		
		//Load in all the race entries
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
		
		//Now we have our initial caution order..
		//Shuffle all wrecked cars to the back
		//Then shuffle heavily damaged cars to the back of that new set
		
		int restartPosition = 0;
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
			
			//If car is fine, join the restart queue
			int carNum = int.Parse(carNumber[i]);
			if((RaceControl.isWrecking[carNum]==false)&&
			  (RaceControl.hasWrecked[carNum]==false)&&
			  (RaceControl.hasBlownEngine[carNum]==false)){
				//Debug.Log("Car #" + carNumber[i] + " restarts P" + restartPosition);
				PlayerPrefs.SetInt("CautionPosition" + restartPosition + "", int.Parse(carNumber[i]));
				if(entrantList[i].name == playerCar.name){
					//Debug.Log("PLAYER WAS NOT DAMAGED");
					PlayerPrefs.SetInt("PlayerCautionPosition", restartPosition);
				}
				restartPosition++;
			}
		}
		
		//We've now counted all of the undamaged cars.
		//Now run loop #2 to add the damaged but not retired cars.
		
		for(int k=0;k<entrantList.Count;k++){
			//Damaged cars restart at the back
			//If damage <50, car is okay to continue and restart.
			int carNum = int.Parse(carNumber[k]);
			if(((RaceControl.isWrecking[carNum]==true)||
			  (RaceControl.hasWrecked[carNum]==true))&&
			  (RaceControl.wreckDamage[carNum] < 50f)){
				//Debug.Log("Car #" + carNumber[k] + " pits and restarts P" + restartPosition);
				if(entrantList[k].name == playerCar.name){
					//Debug.Log("PLAYER WAS DAMAGED");
					PlayerPrefs.SetInt("PlayerCautionPosition", restartPosition);
				}
				PlayerPrefs.SetInt("CautionPosition" + restartPosition + "", int.Parse(carNumber[k]));
				restartPosition++;
			}
		}
		//Debug.Log("Total cars undamaged: " + restartPosition);
		
		//Loop #3 counts the DNFs
		int DNFPosition = restartPosition;
		int cautionLap = CameraRotate.lap;
		
		for(int l=0;l<entrantList.Count;l++){
			int carNum = int.Parse(carNumber[l]);
			if(((RaceControl.isWrecking[carNum]==true)||
			  (RaceControl.hasWrecked[carNum]==true))&&
			  (RaceControl.wreckDamage[carNum] >= 50f)){
				//Car is heavily damaged and retires.
				if((RaceControl.wreckDamage[carNum] >= 50f)||((RaceControl.hasBlownEngine[carNum]==true))){
					Debug.Log(entrantList[l].name + " has retired. Pos " + DNFPosition + " (" + RaceControl.wreckDamage[carNum] + " damage)");
					PlayerPrefs.SetInt("DNFPosition" + DNFPosition + "", carNum);
					PlayerPrefs.SetInt("DNFLap" + DNFPosition + "", cautionLap);
					Debug.Log("Car #" + carNum + " - Retired on lap " + cautionLap);
					DNFPosition++;
				}
			}
		}
		
		//Update field size to however many cars were counted as restartable
		PlayerPrefs.SetInt("FieldSize", restartPosition);
		
		PlayerPrefs.SetInt("CautionLap", CameraRotate.lap);
		PlayerPrefs.SetInt("SpawnFromCaution", 1);
		
		//Only restart the scene if no manual action is required from the player.
		if(waitForReload == false){
			SceneManager.LoadScene(SceneManager.GetActiveScene().name);
		}
	}

	public static void checkFinishPositions(bool crossedFinish = true){
		
		bool isFinalLap = true;
		if(CameraRotate.lap < CameraRotate.raceEnd){
			isFinalLap = false;
		}

		playerPosition = playerCar.transform.position.z;
		
		entrantList.Clear();
		
		int j=0;
		playerCar = GameObject.FindGameObjectWithTag("Player");
		carsArray = GameObject.FindGameObjectsWithTag("AICar");
		playerPosition = playerCar.transform.position.z;

		//Load in all the race entries
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
		
		//Now we have our initial finish order..
		//Shuffle all wrecked cars to the back, as they never crossed the line

		int unclassifiedPosition = 0;
		for(int i=0;i<entrantList.Count;i++){
			if(entrantList[i].name == playerCar.name){
				position = i;
				carNames[i] = playerCar.transform.name;
				carNumber[i] = "" + playerCarNum + "";
				if(PlayerPrefs.HasKey(seriesPrefix + carNumber[i] + "AltDriver")){
					driverNames[i] = PlayerPrefs.GetString(seriesPrefix + carNumber[i] + "AltDriver");
				} else {
					driverNames[i] = cachedCarNames[int.Parse(carNumber[i])];
				}
				leaderDist = (entrantList[0].transform.position.z) - (entrantList[i].transform.position.z);
				leaderDist = leaderDist / 25;
				
				//Adjust for array starting at index 0, not 1
				PlayerPrefs.SetInt("PlayerFinishPosition", i+1);
				//Debug.Log("Player finishes in P" + i);
			} else {
				carNames[i] = "" + entrantList[i].name;
				carNumber[i] = carNames[i].Remove(0,6);	
				driverNames[i] = cachedCarNames[int.Parse(carNumber[i])];
					
				carDist[i] = (entrantList[0].transform.position.z) - (entrantList[i].transform.position.z);
				carDist[i] = carDist[i] / 25;
			}

			if(i == 1){
				MomentsCriteria.checkMomentsCriteria("WinningMargin",carDist[i].ToString());
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

			//*****************************//
			//Loop #1 - Undamaged finishers//
			//*****************************//

			int carNum = int.Parse(carNumber[i]);
			//If car is undamaged, add it to the finishers
			if((RaceControl.isWrecking[carNum]==false)&&
			  (RaceControl.hasWrecked[carNum]==false)&&
			  (RaceControl.hasBlownEngine[carNum]==false)){
				//Debug.Log("Car #" + carNumber[i] + " restarts P" + restartPosition);
				PlayerPrefs.SetInt("FinishPosition" + unclassifiedPosition + "", int.Parse(carNumber[i]));
				PlayerPrefs.SetInt("FinishTime" + i + "", (int)Mathf.Round(carDist[i] * 1000));
				if(entrantList[i].name == playerCar.name){
					PlayerPrefs.SetInt("PlayerFinishPosition", unclassifiedPosition);
				}
				unclassifiedPosition++;
			} else {
				//Car was damaged
				if(isFinalLap == true){
					//But it's the final lap, so it may not matter..
					if(crossedFinish == true){
						//If the finish line was crossed, the finish position is set regardless of damage
						PlayerPrefs.SetInt("FinishPosition" + unclassifiedPosition + "", int.Parse(carNumber[i]));
						PlayerPrefs.SetInt("FinishTime" + i + "", (int)Mathf.Round(carDist[i] * 1000));
						if(entrantList[i].name == playerCar.name){
							PlayerPrefs.SetInt("PlayerFinishPosition", unclassifiedPosition);
						}
						unclassifiedPosition++;
					} else {
						//The finish line was not crossed by the player
						//We will need to figure out who actually survived the final lap wreck in loop #2
					}
				} else {
					//The player has retired before the final lap, shuffle the order in loop #2
				}
			}
		}

		//********************************************//
		//Loop #2 - Damaged cars (non-terminal damage)//
		//********************************************//

		for(int k=0;k<entrantList.Count;k++){
			if(isFinalLap == true){
				//Final lap
				if(crossedFinish == false){
					//The player never crossed the line
					//Damaged cars are marked 1 lap down
					int carNum = int.Parse(carNumber[k]);
					if((RaceControl.isWrecking[carNum]==true)||
					   (RaceControl.hasWrecked[carNum]==true)){
						PlayerPrefs.SetInt("FinishPosition" + unclassifiedPosition + "", int.Parse(carNumber[k]));
						PlayerPrefs.SetInt("FinishTime" + k + "", 99999);
						unclassifiedPosition++;
						if(entrantList[k].name == playerCar.name){
							//Debug.Log("PLAYER WAS DAMAGED");
							PlayerPrefs.SetInt("PlayerFinishPosition", unclassifiedPosition);
						}
					}
				}
			} else {
				//Not the final lap
				//The player must have blown their engine
				//If damage <50, AI cars leapfrog the player and continue on.
				int carNum = int.Parse(carNumber[k]);
				if(((RaceControl.isWrecking[carNum]==true)||
				(RaceControl.hasWrecked[carNum]==true))&&
				(RaceControl.wreckDamage[carNum] < 50f)){
					if(entrantList[k].name == playerCar.name){
						//Skip the player here, as they have retired
					} else {
						PlayerPrefs.SetInt("FinishPosition" + unclassifiedPosition + "", int.Parse(carNumber[k]));
						unclassifiedPosition++;
					}
				}
			}
		}

		//********************************************//
		//Loop #3 - Retired cars (terminal damage)//
		//********************************************//

		int DNFPosition = unclassifiedPosition;
		int cautionLap = CameraRotate.lap;
		
		for(int l=0;l<entrantList.Count;l++){
			int carNum = int.Parse(carNumber[l]);
			//Cars that are either blown up or heavily damaged
			if((RaceControl.hasBlownEngine[carNum]==true)||(RaceControl.wreckDamage[carNum] >= 50f)){
				//Debug.Log(entrantList[l].name + " has retired. Pos " + DNFPosition + " (" + RaceControl.wreckDamage[carNum] + " damage)");
				PlayerPrefs.SetInt("DNFPosition" + DNFPosition + "", carNum);
				PlayerPrefs.SetInt("DNFLap" + DNFPosition + "", cautionLap);
				//Debug.Log("Car #" + carNum + " - Retired on lap " + cautionLap);
				DNFPosition++;
			}
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
