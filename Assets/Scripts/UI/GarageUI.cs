using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GarageUI : MonoBehaviour
{
	public GameObject carTile;
	public GameObject halloweenTile;
	public GameObject patriotTile;
	public GameObject wreckedTile;
	public GameObject twentyTwentyTile;
	public GameObject partTimersTile;
	public GameObject finaleTile;
	public GameObject throwbackTile;
	public GameObject modCarTile;
	public GameObject optionTile;
	public GameObject optionImageTile;
	GameObject activeTile;
	public GameObject seriesDropdownRow;
	
	string restrictionType;
	string restrictionValue;
	int seriesMinClass;
	string seriesTeam;
	string seriesManu;
	int seriesCar;
	string seriesDriverType;
	int seriesRarity;
	
	public GameObject restrictionLabel;
	public GameObject alertPopup;
	GameObject garagePopup;
	GameObject garagePopupFrame;
	GameObject garagePopupTile;
	GameObject actionPanel;
	public GameObject transferPanelContainer;
	public GameObject transferDriverPanel;
	public GameObject transferManufacturerPanel;
	public GameObject transferTeamPanel;
	public GameObject transferNumberPanel;
	Transform teamListFrame;
	public GameObject teammateIcon;
	
	public static GameObject seriesDropdown;
	public static GameObject currentSeries;
	
	public GameObject transferTokensLabel;
	int transfersLeft;
	int transfersMax;
	
	public string chosenOption;
	public GameObject optionObject;
	public string optionType;
	public int popupCarInd;
	
	public Transform seriesDropdownFrame;
	public Transform tileFrame;
	public Transform driverPanel;
	public Transform manufacturerPanel;
	public Transform teamPanel;
	public Transform numberPanel;
	public static string seriesPrefix;
	public List<RectTransform> shuffleArray;
    // Start is called before the first frame update
    void Start(){
		seriesPrefix = "cup23";
		
		//For returning PlayFab call outputs, reset on Awake
		PlayerPrefs.SetString("SaveLoadOutput","");
		
		//Run a load before any saves to merge in any missing unlocks
		if(PlayerPrefs.HasKey("PlayerUsername")){
			PlayFabManager.GetSavedPlayerProgress();
		} else {
			Debug.Log("Not Logged In");
		}
		
		seriesDropdown = GameObject.Find("Dropdown");
		seriesDropdown.SetActive(false);
		currentSeries = GameObject.Find("CurrentSeries");
		currentSeries.GetComponent<TMPro.TMP_Text>().text = DriverNames.getSeriesNiceName(seriesPrefix);
		if(PlayerPrefs.GetString("ExactSeries") != ""){
			seriesPrefix = PlayerPrefs.GetString("ExactSeries");
			GameObject seriesSelector = GameObject.Find("SeriesSelect");
			loadAllCars();
			seriesSelector.SetActive(false);
			PlayerPrefs.DeleteKey("ExactSeries");
		}
		
		loadRaceRestrictions();
		
		if((PlayerPrefs.HasKey("ModsList")) && (PlayerPrefs.GetString("ModsList") != "")){
			try{
			loadModCarsets(PlayerPrefs.GetString("ModsList"));
			} catch (Exception e){
				Debug.Log(e.Message);
			}
		}
		
		alertPopup = GameObject.Find("AlertPopup");
		garagePopup = GameObject.Find("GaragePopup");
		garagePopupFrame = GameObject.Find("GaragePopupFrame");
		garagePopupTile = GameObject.Find("GaragePopupTile");
		actionPanel = GameObject.Find("ActionPanel");
		teamListFrame = GameObject.Find("TeamList").transform;
		transfersMax = PlayerPrefs.GetInt("TransferTokens");
		transfersLeft = PlayerPrefs.GetInt("TransfersLeft");
		if(transfersMax >= 999){
			transferTokensLabel.GetComponent<TMPro.TMP_Text>().text = (transfersMax - transfersLeft).ToString() + " Used Transfers.";
		} else {
			transferTokensLabel.GetComponent<TMPro.TMP_Text>().text = transfersLeft.ToString() + "/" + transfersMax.ToString() + " Used Transfers.";
		}
		
		if(PlayerPrefs.HasKey("CustomCar")){
			autoSelectCar();
		} else {
			loadAllCars();
		}
		
		//No starter cars? Auto-unlock them
		starterCars();
		
		chosenOption = null;
		loadTransferPanels();
	}
	
	public void loadAllCars(){
		
		foreach (Transform child in tileFrame){
			Destroy(child.gameObject);
		}
		
		currentSeries.GetComponent<TMPro.TMP_Text>().text = DriverNames.getSeriesNiceName(seriesPrefix);
		
		Vector3 numXPos = new Vector3(DriverNames.getNumXPos(seriesPrefix),0,0);
		
		int validCars = 0;
		for(int i=0;i<100;i++){
			
			//Skip through the non-driver #s
			if(DriverNames.getName(seriesPrefix, i)== null){
				continue;
			}
			
			if(PlayerPrefs.HasKey(seriesPrefix + i + "AltPaint")){
				int altId = PlayerPrefs.GetInt(seriesPrefix + i + "AltPaint");
				activeTile = getAltPaintTileDesign(seriesPrefix,i,altId);
			} else {
				activeTile = carTile;
			}
			
			//Testing - Unlock All
			#if UNITY_EDITOR
			//PlayerPrefs.SetInt(seriesPrefix + i + "Unlocked",1);
			//PlayerPrefs.SetInt(seriesPrefix + i + "Gears",10);
			//PlayerPrefs.SetInt(seriesPrefix + i + "Class",4);
			#endif
			
			GameObject tileInst = Instantiate(activeTile, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
			tileInst.GetComponent<GarageUIFunctions>().seriesPrefix = seriesPrefix;
			tileInst.GetComponent<GarageUIFunctions>().carNum = i;
			tileInst.GetComponent<GarageUIFunctions>().modCar = false;
			tileInst.transform.SetParent(tileFrame, false);
			tileInst.GetComponent<UIAnimate>().animOffset = i+1;
			tileInst.GetComponent<UIAnimate>().scaleIn();
			
			Text carTeamUI = tileInst.transform.GetChild(0).GetComponent<Text>();
			Text carTypeUI = tileInst.transform.GetChild(1).GetComponent<Text>();
			Text carClassUI = tileInst.transform.GetChild(2).GetComponent<Text>();
			Image carRarityUI = tileInst.transform.GetChild(3).GetComponent<Image>();
			Image carManuUI = tileInst.transform.GetChild(4).GetComponent<Image>();
			RawImage carPaint = tileInst.transform.GetChild(5).GetComponent<RawImage>();
			RawImage carNumber = tileInst.transform.GetChild(5).transform.GetChild(0).GetComponent<RawImage>();
			GameObject carNumberObj = tileInst.transform.GetChild(5).transform.GetChild(0).gameObject;
			TMPro.TMP_Text carName = tileInst.transform.GetChild(6).GetComponent<TMPro.TMP_Text>();
			RectTransform carGearsProgressUI = tileInst.transform.GetChild(7).transform.GetChild(0).GetComponent<RectTransform>();
			TMPro.TMP_Text carGearsLabelUI = tileInst.transform.GetChild(7).transform.GetChild(1).GetComponent<TMPro.TMP_Text>();
			GameObject cardBack = tileInst.transform.GetChild(8).gameObject;
			GameObject carClickable = tileInst.transform.GetChild(9).transform.gameObject;
			GameObject carDisabled = tileInst.transform.GetChild(10).transform.gameObject;
			GameObject carActionBtn = tileInst.transform.GetChild(11).transform.gameObject;
			carActionBtn.SetActive(false);
			
			int carUnlocked = PlayerPrefs.GetInt(seriesPrefix + i + "Unlocked");
			int carGears = PlayerPrefs.GetInt(seriesPrefix + i + "Gears");
			int carClass = PlayerPrefs.GetInt(seriesPrefix + i + "Class");
			int classMax = getClassMax(carClass);
			int unlockClass = DriverNames.getRarity(seriesPrefix,i);
			int unlockGears = GameData.unlockGears(unlockClass);
			
			if(carUnlocked == 1){
				if((carGears >= classMax)&&(carClass < 6)){
					carActionBtn.SetActive(true);
					TMPro.TMP_Text carActionBtnLbl = carActionBtn.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
					carActionBtnLbl.text = "Upgrade";
				}
				if(carClass > 6){
					PlayerPrefs.SetInt(seriesPrefix + i + "Class",6);
					carClass = 6;
				}
			}
			
			if(carUnlocked == 0){
				if(carGears >= unlockGears){
					carActionBtn.SetActive(true);
					TMPro.TMP_Text carActionBtnLbl = carActionBtn.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
					carActionBtnLbl.text = "Unlock";
				}
			}
			
			string carTeam = DriverNames.getTeam(seriesPrefix, i);
			string carType = DriverNames.getType(seriesPrefix, i);
			string carRarity = DriverNames.getRarity(seriesPrefix, i).ToString();
			string carManu = DriverNames.getManufacturer(seriesPrefix, i);
			
			carTeamUI.text = carTeam;
			carTypeUI.text = DriverNames.shortenedType(DriverNames.getType(seriesPrefix, i));
			carClassUI.text = "Class " + classAbbr(carClass);
			carClassUI.color = classColours(carClass);
			carRarityUI.overrideSprite = Resources.Load<Sprite>("Icons/" + carRarity + "-star"); 
			if(PlayerPrefs.HasKey("CustomManufacturer" + seriesPrefix + i)){
				carManuUI.overrideSprite = Resources.Load<Sprite>("Icons/manu-" + PlayerPrefs.GetString("CustomManufacturer" + seriesPrefix + i));
			} else {
				carManuUI.overrideSprite = Resources.Load<Sprite>("Icons/manu-" + carManu);
			}
			if(PlayerPrefs.HasKey("CustomNumber" + seriesPrefix + i)){
				int customNum = PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + i);		
				if(PlayerPrefs.HasKey(seriesPrefix + i + "AltPaint")){
					if(PlayerPrefs.HasKey("CustomDriver" + seriesPrefix + i)){
						carName.text = PlayerPrefs.GetString("CustomDriver" + seriesPrefix + i);
					} else {
						if(PlayerPrefs.HasKey(seriesPrefix + i + "AltDriver")){
							carName.text = PlayerPrefs.GetString(seriesPrefix + i + "AltDriver");
						} else {
							carName.text = DriverNames.getName(seriesPrefix, i);
						}
					}
					carPaint.texture = Resources.Load<Texture2D>(seriesPrefix + "livery" + i + "blankalt" + PlayerPrefs.GetInt(seriesPrefix + i + "AltPaint"));
				} else {
					if(PlayerPrefs.HasKey("CustomDriver" + seriesPrefix + i)){
						carName.text = PlayerPrefs.GetString("CustomDriver" + seriesPrefix + i);
					} else {
						carName.text = DriverNames.getName(seriesPrefix, i);
					}
					carPaint.texture = Resources.Load<Texture2D>(seriesPrefix + "livery" + i + "blank"); 
				}
				carNumberObj.GetComponent<RectTransform>().anchoredPosition = numXPos;
				carNumber.texture = Resources.Load<Texture2D>("cup20num" + customNum);
			} else {
				if(PlayerPrefs.HasKey(seriesPrefix + i + "AltPaint")){
					if(PlayerPrefs.HasKey("CustomDriver" + seriesPrefix + i)){
						carName.text = PlayerPrefs.GetString("CustomDriver" + seriesPrefix + i);
					} else {
						if(PlayerPrefs.HasKey(seriesPrefix + i + "AltDriver")){
							carName.text = PlayerPrefs.GetString(seriesPrefix + i + "AltDriver");
						} else {
							carName.text = DriverNames.getName(seriesPrefix, i);
						}
					}
					carPaint.texture = Resources.Load<Texture2D>(seriesPrefix + "livery" + i + "alt" + PlayerPrefs.GetInt(seriesPrefix + i + "AltPaint"));
				} else {
					if(PlayerPrefs.HasKey("CustomDriver" + seriesPrefix + i)){
						carName.text = PlayerPrefs.GetString("CustomDriver" + seriesPrefix + i);
					} else {
						carName.text = DriverNames.getName(seriesPrefix, i);
					}
					carPaint.texture = Resources.Load<Texture2D>(seriesPrefix + "livery" + i); 
				}
				carNumberObj.SetActive(false);
			}
			float gearsProgressUIWidth = Mathf.Round((110 / classMax) * carGears) + 1;
			if(gearsProgressUIWidth > 110){
				gearsProgressUIWidth = 110;
			}
			
			if(carClass >= 6){
				carClass = 6;
				gearsProgressUIWidth = 110;
				carGearsLabelUI.text = "Max Class";
			} else {
				if(carUnlocked == 0){
					carGearsLabelUI.text = carGears + "/" + unlockGears;
					gearsProgressUIWidth = Mathf.Round((110 / unlockGears) * carGears) + 1;
				} else {
					carGearsLabelUI.text = carGears + "/" + classMax;
				}
			}
			carGearsProgressUI.sizeDelta = new Vector2(gearsProgressUIWidth, 20);
			
			//If choosing a car to race with..
			if(PlayerPrefs.HasKey("ActivePath")){
				bool isValid = true;
				int minClass = PlayerPrefs.GetInt("SubseriesMinClass");

				if((minClass > carClass)||(carUnlocked == 0)){
					carClickable.SetActive(false);
					carDisabled.SetActive(true);
					isValid = false;
				}
				if(meetsRestrictions(seriesPrefix,i) == false){
					carClickable.SetActive(false);
					carDisabled.SetActive(true);
					isValid = false;
				}
				if(isValid == true){
					validCars++;
				}
			}
			
			if(carUnlocked == 0){
				carClickable.SetActive(false);
				carDisabled.SetActive(true);
			}
		}
		
		if(PlayerPrefs.HasKey("ActivePath")){
			restrictionLabel = GameObject.Find("SeriesRestriction");
			restrictionLabel.GetComponent<TMPro.TMP_Text>().text = showRestrictions();
			if(validCars == 0){
				alertPopup.GetComponent<AlertManager>().showPopup("No Eligible Car","No Car In This Set Meets The Entry Requirements. \n\n" + showRestrictions() + "","dm2logo");
			}
			//Debug.Log("Valid cars: " + validCars);
		}
		
		int sortCounter=0;
		foreach (Transform child in tileFrame){
			GameObject eligibleEntry = child.transform.GetChild(9).transform.gameObject;

			if(eligibleEntry.activeSelf == false){
				RectTransform tileObj = child.GetComponent<RectTransform>();
				//Debug.Log(child);
				if(tileObj != null){
					shuffleArray.Add(tileObj);
				}
			}
			sortCounter++;
		}
		sortTiles();
	}

	public void loadAllModCars(){
		
		foreach (Transform child in tileFrame){
			Destroy(child.gameObject);
		}
		
		currentSeries.GetComponent<TMPro.TMP_Text>().text = ModData.getSeriesNiceName(seriesPrefix);
		
		int validCars = 0;
		for(int i=0;i<100;i++){
			//int carJsonIndex = ModData.getJsonIndexFromCarNum(seriesPrefix, carNum);
			
			//Skip through the non-driver #s
			if(ModData.getName(seriesPrefix, i) == null){
				continue;
			}
			int carNum = ModData.getCarNum(seriesPrefix, i);
			
			activeTile = modCarTile;
			
			GameObject tileInst = Instantiate(activeTile, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
			tileInst.GetComponent<GarageUIFunctions>().seriesPrefix = seriesPrefix;
			tileInst.GetComponent<GarageUIFunctions>().carNum = carNum;
			tileInst.GetComponent<GarageUIFunctions>().modCar = true;
			tileInst.transform.SetParent(tileFrame, false);
			tileInst.GetComponent<UIAnimate>().animOffset = i+1;
			tileInst.GetComponent<UIAnimate>().scaleIn();
			
			Text carTeamUI = tileInst.transform.GetChild(0).GetComponent<Text>();
			Text carTypeUI = tileInst.transform.GetChild(1).GetComponent<Text>();
			Text carClassUI = tileInst.transform.GetChild(2).GetComponent<Text>();
			Image carRarityUI = tileInst.transform.GetChild(3).GetComponent<Image>();
			Image carManuUI = tileInst.transform.GetChild(4).GetComponent<Image>();
			RawImage carPaint = tileInst.transform.GetChild(5).GetComponent<RawImage>();
			RawImage carNumber = tileInst.transform.GetChild(5).transform.GetChild(0).GetComponent<RawImage>();
			GameObject carNumberObj = tileInst.transform.GetChild(5).transform.GetChild(0).gameObject;
			TMPro.TMP_Text carName = tileInst.transform.GetChild(6).GetComponent<TMPro.TMP_Text>();
			GameObject carClickable = tileInst.transform.GetChild(9).transform.gameObject;
			GameObject carDisabled = tileInst.transform.GetChild(10).transform.gameObject;
			
			//Mod cars are auto-unlocked, but limited to a low class and rarity
			PlayerPrefs.SetInt(seriesPrefix + i + "Unlocked", 1);
			PlayerPrefs.SetInt(seriesPrefix + i + "Gears", 0);
			PlayerPrefs.SetInt(seriesPrefix + i + "Class", 4);
			
			int carUnlocked = PlayerPrefs.GetInt(seriesPrefix + i + "Unlocked");
			int carGears = PlayerPrefs.GetInt(seriesPrefix + i + "Gears");
			int carClass = PlayerPrefs.GetInt(seriesPrefix + i + "Class");
			int unlockClass = ModData.getRarity(seriesPrefix,i);
			
			string carTeam = ModData.getTeam(seriesPrefix, i);
			string carType = ModData.getType(seriesPrefix, i);
			string carRarity = ModData.getRarity(seriesPrefix, i).ToString();
			string carManu = ModData.getManufacturer(seriesPrefix, i);
			
			carTeamUI.text = carTeam;
			carTypeUI.text = DriverNames.shortenedType(ModData.getType(seriesPrefix, i));
			carClassUI.text = "";
			carRarityUI.overrideSprite = Resources.Load<Sprite>("Icons/" + carRarity + "-star"); 
			if(DriverNames.isOfficialManu(carManu) == true){
				carManuUI.overrideSprite = Resources.Load<Sprite>("Icons/manu-" + carManu); 
			} else {
				carManuUI.overrideSprite = ModData.getManuSprite(seriesPrefix, carManu); 
			}
			carName.text = ModData.getName(seriesPrefix,i,false);
			if(PlayerPrefs.HasKey("CustomDriver" + seriesPrefix + i)){
				carName.text = PlayerPrefs.GetString("CustomDriver" + seriesPrefix + i);
				//Debug.Log("Transferred Name! " + "Pref:" + ("CustomDriver" + seriesPrefix + i) + " Val:" + PlayerPrefs.GetString("CustomDriver" + seriesPrefix + i));
		
			}
			carPaint.texture = ModData.getTexture(seriesPrefix, carNum);
			//No custom numbers on mod cars.. for now.
			carNumberObj.SetActive(false);
			
			//If choosing a car to race with..
			if(PlayerPrefs.HasKey("ActivePath")){
				bool isValid = true;
				
				if(meetsModRestrictions(seriesPrefix,i) == false){
					carClickable.SetActive(false);
					carDisabled.SetActive(true);
					isValid = false;
				}
				if(isValid == true){
					validCars++;
				}
			}
		}
		
		if(PlayerPrefs.HasKey("ActivePath")){
			restrictionLabel = GameObject.Find("SeriesRestriction");
			restrictionLabel.GetComponent<TMPro.TMP_Text>().text = showRestrictions();
			if(validCars == 0){
				alertPopup.GetComponent<AlertManager>().showPopup("No Eligible Car","No Car In This Set Meets The Entry Requirements. \n\n" + showRestrictions() + "","dm2logo");
			}
			//Debug.Log("Valid cars: " + validCars);
		}
	}

	public void sortTiles(){
		if (shuffleArray.Any()){
			RectTransform tile = shuffleArray.First();
			tile.SetAsLastSibling();
			shuffleArray.Remove(tile);
			sortTiles();
		}
	}

	public void loadRaceRestrictions(){
		
		restrictionType = PlayerPrefs.GetString("RestrictionType");
		restrictionValue = PlayerPrefs.GetString("RestrictionValue");
		//Debug.Log("Restriction " + restrictionType);
		//Debug.Log("Restricted to " + restrictionValue);
			
		seriesTeam = "";
		seriesManu = "";
		seriesCar = 999;
		seriesDriverType = "";
		seriesRarity = 0;
		
		switch(restrictionType){
			case "Team":
				seriesTeam = restrictionValue;
				//Debug.Log("Driver Type: " + seriesTeam);
				break;
			case "Manufacturer":
				seriesManu = restrictionValue;
				break;
			case "Car":
				seriesCar = int.Parse(restrictionValue);
				break;
			case "Rarity":
				seriesRarity = int.Parse(restrictionValue);
				break;
			case "Type":
				seriesDriverType = restrictionValue;
				//Debug.Log("Driver Type: " + seriesDriverType);
				break;
			default:
				break;
		}
	}

	public bool meetsRestrictions(string series, int car){

		if((seriesTeam != "")&&(DriverNames.getTeam(series, car) != seriesTeam)){
			//Debug.Log(seriesTeam + " is not " + DriverNames.getTeam(series, car) + " on #" + car);
			return false;
		}
		if((seriesManu != "")&&(DriverNames.getManufacturer(series, car) != seriesManu)){
			return false;
		}
		if((seriesCar != 999)&&(car != seriesCar)){
			return false;
		}
		if((seriesDriverType != "")&&(DriverNames.getType(series, car) != seriesDriverType)){
			return false;
		}
		if((seriesRarity != 0)&&(DriverNames.getRarity(series, car) != seriesRarity)){
			return false;
		}
		return true;
	}
	
	public bool meetsModRestrictions(string series, int car){

		if((seriesTeam != "")&&(ModData.getTeam(series, car) != seriesTeam)){
			//Debug.Log(seriesTeam + " is not " + ModData.getTeam(series, car) + " on #" + car);
			return false;
		}
		if((seriesManu != "")&&(ModData.getManufacturer(series, car) != seriesManu)){
			return false;
		}
		if((seriesCar != 999)&&(car != seriesCar)){
			return false;
		}
		if((seriesDriverType != "")&&(ModData.getType(series, car) != seriesDriverType)){
			return false;
		}
		if((seriesRarity != 0)&&(ModData.getRarity(series, car) != seriesRarity)){
			return false;
		}
		return true;
	}
	
	public string showRestrictions(){
		string restriction = "";
		if(seriesTeam != ""){
			restriction += "Team " + seriesTeam + "\n";
		}
		if(seriesManu != ""){
			restriction += "Manufacturer " + seriesManu + "\n";
		}
		if(seriesCar != 999){
			restriction += "Car " + seriesCar + "\n";
		}
		if(seriesDriverType != ""){
			restriction += "Type " + seriesDriverType + "\n";
		}
		if(seriesRarity != 0){
			restriction += "Rarity " + seriesRarity + "\n";
		}
		int minClass = PlayerPrefs.GetInt("SubseriesMinClass");
		restriction += "Minimum Class " + classAbbr(minClass);
		return restriction;
	}

	public void loadModCarsets(string modList){
		//Debug.Log(modList);
		
		string[] modsArray = modList.Split(',');
		foreach(string modSet in modsArray){
			//Debug.Log(modSet);
			string[] modData = modSet.Split('|');
			
			GameObject modCarsetInst = Instantiate(seriesDropdownRow, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
			modCarsetInst.transform.SetParent(seriesDropdownFrame, false);
			
			modCarsetInst.name = modData[0];
			TMPro.TMP_Text modCarsetName = modCarsetInst.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
			modCarsetName.text = modData[1];
		}
	}

	public void loadTransferPanels(){
		loadTransferPanel("Driver",driverPanel);
		loadTransferPanel("Manufacturer",manufacturerPanel);
		loadTransferPanel("Team",teamPanel);
		loadTransferPanel("Number",numberPanel);
	}
	
	public void loadTransferPanel(string panelType, Transform parentGrid){
		//Empty the panel
		foreach (Transform child in parentGrid){
			Destroy(child.gameObject);
		}
		switch(panelType){
			case "Driver":
				string[] driverPool = DriverNames.getDriverPool();
				foreach(string driver in driverPool){
					GameObject optionInst = Instantiate(optionTile, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
					optionInst.GetComponent<GridSelector>().optionName = driver;
					optionInst.GetComponent<GridSelector>().optionType = panelType;
					optionInst.GetComponent<TMPro.TMP_Text>().text = driver;
					optionInst.transform.SetParent(parentGrid, false);
				}
				break;
			case "Manufacturer":
				string[] manufacturerPool = DriverNames.getManufacturerPool();
				foreach(string manu in manufacturerPool){
					GameObject optionInst = Instantiate(optionImageTile, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
					optionInst.GetComponent<GridSelector>().optionName = manu;
					optionInst.GetComponent<GridSelector>().optionType = panelType;
					optionInst.GetComponent<RawImage>().texture = Resources.Load<Texture2D>("Icons/manu-" + manu);
					optionInst.transform.SetParent(parentGrid, false);
				}
				break;
			case "Team":
				break;
			case "Number":
				break;
		}
	}
	
	public void dropOptionToPool(){
		Destroy(optionObject);
		if(chosenOption != null){
			GameObject optionInst;
			switch(optionType){
				case "Driver":
					optionInst = Instantiate(optionTile, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
					optionInst.GetComponent<GridSelector>().optionName = chosenOption;
					optionInst.GetComponent<GridSelector>().optionType = optionType;
					optionInst.GetComponent<TMPro.TMP_Text>().text = chosenOption;
					optionInst.transform.SetParent(driverPanel, false);
				break;
				case "Manufacturer":
					optionInst = Instantiate(optionImageTile, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
					optionInst.GetComponent<GridSelector>().optionName = chosenOption;
					optionInst.GetComponent<GridSelector>().optionType = optionType;
					optionInst.GetComponent<RawImage>().texture = Resources.Load<Texture2D>("Icons/manu-" + chosenOption);
					optionInst.transform.SetParent(manufacturerPanel, false);
				break;
				case "Team":
					optionInst = Instantiate(optionTile, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
					optionInst.GetComponent<GridSelector>().optionName = chosenOption;
					optionInst.GetComponent<GridSelector>().optionType = optionType;
					optionInst.GetComponent<TMPro.TMP_Text>().text = chosenOption;
					optionInst.transform.SetParent(teamPanel, false);
				break;
				default:
					//Throw error here
					break;
			}
		}
	}

	public void confirmTransfer(){
		if(transfersLeft <= 0){
			alertPopup.GetComponent<AlertManager>().showPopup("No Transfers Remaining","You have used all of your available transfer tokens. Level up to get more, or purchase the Editor pack in the Store for unlimited tokens.","dm2logo");
		} else {
			transfersLeft--;
			PlayerPrefs.SetInt("TransfersLeft", transfersLeft);
			int popupCarNum = 999;
			switch(optionType){
				case "Driver":
					PlayerPrefs.SetString("CustomDriver" + seriesPrefix + popupCarInd, chosenOption);
					//Debug.Log("Transfer made! " + "Pref:" + ("CustomDriver" + seriesPrefix + popupCarInd) + " Val:" + chosenOption);
					popupCarNum = popupCarInd;
					if(DriverNames.isOfficialSeries(seriesPrefix) == false){
						popupCarNum = ModData.getCarNum(seriesPrefix, popupCarInd);
					}
					break;
				case "Manufacturer":
					PlayerPrefs.SetString("CustomManufacturer" + seriesPrefix + popupCarInd, chosenOption);
					//Debug.Log("Transfer made! " + "Pref:" + ("CustomDriver" + seriesPrefix + popupCarInd) + " Val:" + chosenOption);
					popupCarNum = popupCarInd;
					if(DriverNames.isOfficialSeries(seriesPrefix) == false){
						popupCarNum = ModData.getCarNum(seriesPrefix, popupCarInd);
					}
					break;
				case "Team":
					break;
				default:
					break;
			}
			updateTransferCount();
			showGaragePopup(seriesPrefix,popupCarNum,3);
		}
		chosenOption = null;
		loadTransferPanels();
		if(DriverNames.isOfficialSeries(seriesPrefix) == true){
			loadAllCars();
		} else {
			loadAllModCars();
		}
	}
	
	public void updateTransferCount(){
		transfersMax = PlayerPrefs.GetInt("TransferTokens");
		transfersLeft = PlayerPrefs.GetInt("TransfersLeft");
		if(transfersMax >= 999){
			transferTokensLabel.GetComponent<TMPro.TMP_Text>().text = (transfersMax - transfersLeft).ToString() + " Used Transfers.";
		} else {
			transferTokensLabel.GetComponent<TMPro.TMP_Text>().text = transfersLeft.ToString() + "/" + transfersMax.ToString() + " Used Transfers.";
		}
	}

	string classAbbr(int carClass){
		string classLetter;
		switch(carClass){
			case 1:
				classLetter = "R";
				break;
		    case 2:
				classLetter = "D";
				break;
			case 3:
				classLetter = "C";
				break;
			case 4:
				classLetter = "B";
				break;
			case 5:
				classLetter = "A";
				break;
			case 6:
				classLetter = "S";
				break;
		    default:
				classLetter = "R";
				break;
		}
		return classLetter;
	}
	
	Color classColours(int carClass){
		Color classColour;
		switch(carClass){
			case 1:
				classColour = new Color32(164,6,6,255);
				break;
		    case 2:
				classColour = new Color32(255,165,0,255);
				break;
			case 3:
				classColour = new Color32(238,130,238,255);
				break;
			case 4:
				classColour = new Color32(0,128,0,255);
				break;
			case 5:
				classColour = new Color32(0,0,255,255);
				break;
			case 6:
				classColour = new Color32(75,0,130,255);
				break;
		    default:
				classColour = new Color32(164,6,6,255);
				break;
		}
		return classColour;
	}

	public static int getClassMax(int carClass){
		switch(carClass){
			case 0:
				return 10;
				break;
			case 1:
				return 20;
				break;
		    case 2:
				return 35;
				break;
			case 3:
				return 50;
				break;
			case 4:
				return 70;
				break;
			case 5:
				return 100;
				break;
			case 6:
				return 150;
				break;
		    default:
				return 999;
				break;
		}
	}

	GameObject getAltPaintTileDesign(string seriesPrefix, int carId, int altId){
		GameObject cardPrefab;
		string cardTheme = AltPaints.getAltPaintTheme(seriesPrefix, carId, altId);
		switch(cardTheme){
			case "Halloween":
				cardPrefab = halloweenTile;
				break;
			case "Patriot":
				cardPrefab = patriotTile;
				break;
			case "Wrecked":
				cardPrefab = wreckedTile;
				break;
			case "2020":
				cardPrefab = twentyTwentyTile;
				break;
			case "Throwback":
				cardPrefab = throwbackTile;
				break;
			case "Final4":
				cardPrefab = finaleTile;
				break;
			default:
				cardPrefab = carTile;
				break;
		}
		return cardPrefab;
	}

	public void autoSelectCar(){
		if(PlayerPrefs.HasKey("FixedSeries")){
			PlayerPrefs.SetString("carSeries", PlayerPrefs.GetString("FixedSeries"));
		}
		string carTex = PlayerPrefs.GetString("CustomCar");
		PlayerPrefs.SetString("carTexture",carTex);
		string keyword = "livery";
		string carNum = carTex.Substring(carTex.IndexOf(keyword) + keyword.Length);
		PlayerPrefs.SetInt("CarChoice",int.Parse(carNum));
		SceneManager.LoadScene("Menus/TrackSelect");
	}

	public void toggleDropdown(bool modSet = false){
		if(seriesDropdown.activeSelf == true){
			seriesDropdown.SetActive(false);
			//Debug.Log("Deactivate Dropdown");
		} else {
			seriesDropdown.SetActive(true);
			//Debug.Log("Activate Dropdown");
		}
		if(modSet == true){
			currentSeries.GetComponent<TMPro.TMP_Text>().text = ModData.getSeriesNiceName(seriesPrefix);
		}
	}

	public void starterCars(){
		//Debug.Log("Check starters");
		bool carsAdded = false;
		
		//Give a free Gragson for Cup '23
		if(PlayerPrefs.GetInt("cup2342Unlocked") == 0){
			PlayerPrefs.SetInt("cup2342Unlocked",1);
			PlayerPrefs.SetInt("cup2342Gears",0);
			PlayerPrefs.SetInt("cup2342Class",1);
			carsAdded = true;
		}
		
		//Give a free Pastrana for Cup '23
		if(PlayerPrefs.GetInt("cup2367Unlocked") == 0){
			PlayerPrefs.SetInt("cup2367Unlocked",1);
			PlayerPrefs.SetInt("cup2367Gears",0);
			PlayerPrefs.SetInt("cup2367Class",1);
			carsAdded = true;
		}
		
		//Give a free McLeod for Cup '22
		if(PlayerPrefs.GetInt("cup2278Unlocked") == 0){
			PlayerPrefs.SetInt("cup2278Unlocked",1);
			PlayerPrefs.SetInt("cup2278Gears",0);
			PlayerPrefs.SetInt("cup2278Class",1);
			carsAdded = true;
		}
		
		//Give a free Raikkonen for Cup '22
		if(PlayerPrefs.GetInt("cup2291Unlocked") == 0){
			PlayerPrefs.SetInt("cup2291Unlocked",1);
			PlayerPrefs.SetInt("cup2291Gears",0);
			PlayerPrefs.SetInt("cup2291Class",1);
			carsAdded = true;
		}
		
		//Give a free Mcleod for Cup '20
		if(PlayerPrefs.GetInt("cup2052Unlocked") == 0){
			PlayerPrefs.SetInt("cup2052Unlocked",1);
			PlayerPrefs.SetInt("cup2052Gears",0);
			PlayerPrefs.SetInt("cup2052Class",1);
			carsAdded = true;
		}	
		
		//Give a free Robb for IRL '23
		if(PlayerPrefs.GetInt("irl2351Unlocked") == 0){
			PlayerPrefs.SetInt("irl2351Unlocked",1);
			PlayerPrefs.SetInt("irl2351Gears",0);
			PlayerPrefs.SetInt("irl2351Class",1);
			carsAdded = true;
		}
		
		//Give a free Dismore for IROC '00
		if(PlayerPrefs.GetInt("irc008Unlocked") == 0){
			PlayerPrefs.SetInt("irc008Unlocked",1);
			PlayerPrefs.SetInt("irc008Gears",0);
			PlayerPrefs.SetInt("irc008Class",1);
			carsAdded = true;
		}
		
		//Give free Starters for DM1 '15
		if(PlayerPrefs.GetInt("dmc150Unlocked") == 0){
			PlayerPrefs.SetInt("dmc150Unlocked",1);
			PlayerPrefs.SetInt("dmc150Gears",0);
			PlayerPrefs.SetInt("dmc150Class",1);
			carsAdded = true;
		}
		if(PlayerPrefs.GetInt("dmc151Unlocked") == 0){
			PlayerPrefs.SetInt("dmc151Unlocked",1);
			PlayerPrefs.SetInt("dmc151Gears",0);
			PlayerPrefs.SetInt("dmc151Class",1);
			carsAdded = true;
		}
		if(PlayerPrefs.GetInt("dmc152Unlocked") == 0){
			PlayerPrefs.SetInt("dmc152Unlocked",1);
			PlayerPrefs.SetInt("dmc152Gears",0);
			PlayerPrefs.SetInt("dmc152Class",1);
			carsAdded = true;
		}
		if(carsAdded == true){
			loadAllCars();
		}
	}

	public void syncWithCloud(){
		try {
			PlayFabManager.GetSavedPlayerProgress();
			alertPopup.GetComponent<AlertManager>().showPopup("Cloud Sync",PlayerPrefs.GetString("LoadOutput"),"dm2logo");
		}
		catch(Exception e){
			Debug.Log("Cannot reach PlayFab");
			alertPopup.GetComponent<AlertManager>().showPopup("Cannot Reach Cloud","Sorry, we can't currently retrieve and sync your save data. Do you have an internet connection?","dm2logo");
		}
		loadAllCars();
	}

	public void showGaragePopup(string seriesPrefix, int carNum, int carClass){
		int carInd;
		Debug.Log("Series Prefix on Garage popup open: " + seriesPrefix);
		if(DriverNames.isOfficialSeries(seriesPrefix) == false){
			//Convert mods from number to index
			carInd = ModData.getJsonIndexFromCarNum(seriesPrefix, carNum);
		} else {
			carInd = carNum;
		}
		popupCarInd = carInd;
		
		Text carTeamUI = garagePopupTile.transform.GetChild(0).GetComponent<Text>();
		Text carTypeUI = garagePopupTile.transform.GetChild(1).GetComponent<Text>();
		Text carClassUI = garagePopupTile.transform.GetChild(2).GetComponent<Text>();
		Image carRarityUI = garagePopupTile.transform.GetChild(3).GetComponent<Image>();
		Image carManuUI = garagePopupTile.transform.GetChild(4).GetComponent<Image>();
		RawImage carPaint = garagePopupTile.transform.GetChild(5).GetComponent<RawImage>();
		TMPro.TMP_Text carNameUI = garagePopupTile.transform.GetChild(6).GetComponent<TMPro.TMP_Text>();
		
		TMPro.TMP_Text carManuText = garagePopupFrame.transform.GetChild(1).GetComponent<TMPro.TMP_Text>();
		TMPro.TMP_Text carTeamText = garagePopupFrame.transform.GetChild(2).GetComponent<TMPro.TMP_Text>();
		
		string carTeam = DriverNames.getTeam(seriesPrefix,carInd);
		string carDriver = DriverNames.getName(seriesPrefix,carInd);
		if(PlayerPrefs.HasKey("CustomDriver" + seriesPrefix + carInd)){
			carDriver = PlayerPrefs.GetString("CustomDriver" + seriesPrefix + carInd);
		}
		
		carTeamUI.text = carTeam;
		carTypeUI.text = DriverNames.shortenedType(DriverNames.getType(seriesPrefix, carInd));
		carClassUI.text = "Class " + classAbbr(carClass);
		carClassUI.color = classColours(carClass);
		carRarityUI.overrideSprite = Resources.Load<Sprite>("Icons/" + DriverNames.getRarity(seriesPrefix,carInd) + "-star"); 
		if(PlayerPrefs.HasKey("CustomManufacturer" + seriesPrefix + carInd)){
			carManuUI.overrideSprite = Resources.Load<Sprite>("Icons/manu-" + PlayerPrefs.GetString("CustomManufacturer" + seriesPrefix + carInd));
		} else {
			carManuUI.overrideSprite = Resources.Load<Sprite>("Icons/manu-" + DriverNames.getManufacturer(seriesPrefix,carInd)); 
		}
		if(DriverNames.isOfficialSeries(seriesPrefix) == true){
			carPaint.texture = Resources.Load<Texture2D>(seriesPrefix + "livery" + carInd);
		} else {
			//Debug.Log("Get Texture #" + carInd);
			carPaint.texture = ModData.getTexture(seriesPrefix, carInd, true);
		}
		carNameUI.text = carDriver;
		
		carManuText.text = DriverNames.getManufacturer(seriesPrefix,carInd) + " cars will push you for longer";
		carManuText.transform.GetChild(0).GetComponent<TMPro.TMP_Text>().text = DriverNames.getManufacturer(seriesPrefix,carInd);
		carTeamText.text = DriverNames.getTeam(seriesPrefix,carInd) + " teammates will block you less often";
		carTeamText.transform.GetChild(0).GetComponent<TMPro.TMP_Text>().text = DriverNames.getTeam(seriesPrefix,carInd);
		
		TMPro.TMP_Text driverName = GameObject.Find("DriverName").GetComponent<TMPro.TMP_Text>();
		TMPro.TMP_Text manuName = GameObject.Find("ManufacturerName").GetComponent<TMPro.TMP_Text>();
		TMPro.TMP_Text teamName = GameObject.Find("TeamName").GetComponent<TMPro.TMP_Text>();
		TMPro.TMP_Text numberName = GameObject.Find("NumberName").GetComponent<TMPro.TMP_Text>();
		
		driverName.text = carDriver;
		manuName.text = DriverNames.getManufacturer(seriesPrefix,carInd);
		teamName.text = DriverNames.getTeam(seriesPrefix,carInd);
		numberName.text = "#" + DriverNames.getNumber(seriesPrefix,carInd).ToString();
		
		//Empty the teammate frame
		foreach (Transform child in teamListFrame){
			Destroy(child.gameObject);
		}
		for(int car=0;car<100;car++){
			if((DriverNames.getTeam(seriesPrefix, car) == carTeam)&&(car != carInd)){
				//Add a new teammate tile
				GameObject teammateInst = Instantiate(teammateIcon, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
				teammateInst.transform.SetParent(teamListFrame, false);
				if(DriverNames.isOfficialSeries(seriesPrefix)){
					teammateInst.GetComponent<RawImage>().texture = Resources.Load<Texture2D>(seriesPrefix + "livery" + car);
				} else {
					teammateInst.GetComponent<RawImage>().texture = ModData.getTexture(seriesPrefix, car, true);
				}
			}
		}
		garagePopup.GetComponent<UIAnimate>().show();
	}

	public void showGarageTransferPanelContainer(){
		transferPanelContainer.GetComponent<UIAnimate>().show();
		actionPanel.GetComponent<UIAnimate>().show();
	}

	public void showGarageTransferDriverPanel(){
		showGarageTransferPanelContainer();
		//Dependant on chosen transfer action
		transferDriverPanel.GetComponent<UIAnimate>().show();
	}
	
	public void showGarageTransferManufacturerPanel(){
		showGarageTransferPanelContainer();
		//Dependant on chosen transfer action
		transferManufacturerPanel.GetComponent<UIAnimate>().show();
	}
	
	public void showGarageTransferTeamPanel(){
		showGarageTransferPanelContainer();
		//Dependant on chosen transfer action
		transferTeamPanel.GetComponent<UIAnimate>().show();
	}
	
	public void showGarageTransferNumberPanel(){
		showGarageTransferPanelContainer();
		//Dependant on chosen transfer action
		transferNumberPanel.GetComponent<UIAnimate>().show();
	}
	
	public void hideGarageTransferPanels(){
		transferPanelContainer.GetComponent<UIAnimate>().hide();
		actionPanel.GetComponent<UIAnimate>().hide();
		transferDriverPanel.GetComponent<UIAnimate>().hide();
		transferManufacturerPanel.GetComponent<UIAnimate>().hide();
		transferTeamPanel.GetComponent<UIAnimate>().hide();
		transferNumberPanel.GetComponent<UIAnimate>().hide();
	}
	
	public void hideGaragePopup(){
		LeanTween.scale(garagePopup, new Vector3(0f,0f,0f), 0f);
		popupCarInd = 999;
		hideGarageTransferPanels();
	}

    // Update is called once per frame
    void Update(){
    }
}

[Serializable]
public class Driver {
    public string carNo;
    public string carUnlocked;
    public string carClass;
    public string carGears;
    public string altPaints;
}

[Serializable]
public class Series {
	public string playerLevel;
	public int totalCars;
	public string transferTokens;
    public string seriesName;
    public List<Driver> drivers;
}