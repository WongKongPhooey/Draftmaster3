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
	GameObject activeTile;
	
	string restrictionType;
	string restrictionValue;
	int seriesMinClass;
	string seriesTeam;
	string seriesManu;
	int seriesCar;
	string seriesDriverType;
	int seriesRarity;
	
	public static GameObject seriesDropdown;
	public static GameObject currentSeries;
	
	public Transform tileFrame;
	public static string seriesPrefix;
	public List<RectTransform> shuffleArray;
    // Start is called before the first frame update
    void Start(){
		seriesPrefix = "cup20";
		
		seriesDropdown = GameObject.Find("Dropdown");
		seriesDropdown.SetActive(false);
		currentSeries = GameObject.Find("CurrentSeries");
		currentSeries.GetComponent<TMPro.TMP_Text>().text = DriverNames.getSeriesNiceName(seriesPrefix);
		
		loadRaceRestrictions();
		
		if(PlayerPrefs.HasKey("CustomCar")){
			autoSelectCar();
		} else {
			loadAllCars();
		}
		
		//No starter cars? Auto-unlock them
		if(PlayerPrefs.GetInt("cup2278Unlocked") == 0){
			starterCars();
		}
	}
	
	public void loadAllCars(){
		
		foreach (Transform child in tileFrame){
			Destroy(child.gameObject);
		}
		
		Vector3 numXPos = new Vector3(DriverNames.getNumXPos(seriesPrefix),0,0);
		
		for(int i=0;i<100;i++){
			
			//Skip through the non-driver #s
			if(DriverNames.getName(seriesPrefix, i)== null){
				continue;
			}
			
			if(PlayerPrefs.HasKey("CustomNumber" + seriesPrefix + i)){
				
			}
			
			if(PlayerPrefs.HasKey(seriesPrefix + i + "AltPaint")){
				int altId = PlayerPrefs.GetInt(seriesPrefix + i + "AltPaint");
				activeTile = getAltPaintTileDesign(seriesPrefix,i,altId);
			} else {
				activeTile = carTile;
			}
			
			GameObject tileInst = Instantiate(activeTile, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
			tileInst.GetComponent<GarageUIFunctions>().seriesPrefix = seriesPrefix;
			tileInst.GetComponent<GarageUIFunctions>().carNum = i;
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
				if(carGears >= classMax){
					carActionBtn.SetActive(true);
					TMPro.TMP_Text carActionBtnLbl = carActionBtn.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
					carActionBtnLbl.text = "Upgrade";
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
			carManuUI.overrideSprite = Resources.Load<Sprite>("Icons/manu-" + carManu); 
			if(PlayerPrefs.HasKey("CustomNumber" + seriesPrefix + i)){
				int customNum = PlayerPrefs.GetInt("CustomNumber" + seriesPrefix + i);		
				if(PlayerPrefs.HasKey(seriesPrefix + i + "AltPaint")){
					if(PlayerPrefs.HasKey(seriesPrefix + i + "AltDriver")){
						carName.text = PlayerPrefs.GetString(seriesPrefix + i + "AltDriver");
					} else {
						carName.text = DriverNames.getName(seriesPrefix, i);
					}
					carPaint.texture = Resources.Load<Texture2D>(seriesPrefix + "livery" + i + "blankalt" + PlayerPrefs.GetInt(seriesPrefix + i + "AltPaint"));
				} else {
					carName.text = DriverNames.getName(seriesPrefix, i);
					carPaint.texture = Resources.Load<Texture2D>(seriesPrefix + "livery" + i + "blank"); 
				}
				carNumberObj.GetComponent<RectTransform>().anchoredPosition = numXPos;
				carNumber.texture = Resources.Load<Texture2D>("cup20num" + customNum);
			} else {
				if(PlayerPrefs.HasKey(seriesPrefix + i + "AltPaint")){
					if(PlayerPrefs.HasKey(seriesPrefix + i + "AltDriver")){
						carName.text = PlayerPrefs.GetString(seriesPrefix + i + "AltDriver");
					} else {
						carName.text = DriverNames.getName(seriesPrefix, i);
					}
					carPaint.texture = Resources.Load<Texture2D>(seriesPrefix + "livery" + i + "alt" + PlayerPrefs.GetInt(seriesPrefix + i + "AltPaint"));
				} else {
					carName.text = DriverNames.getName(seriesPrefix, i);
					carPaint.texture = Resources.Load<Texture2D>(seriesPrefix + "livery" + i); 
				}
				carNumberObj.SetActive(false);
			}
			float gearsProgressUIWidth = Mathf.Round((110 / classMax) * carGears) + 1;
			if(gearsProgressUIWidth > 110){
				gearsProgressUIWidth = 110;
			}
			
			if(carClass >= 6){
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
			
			if(PlayerPrefs.HasKey("ActivePath")){
				int minClass = PlayerPrefs.GetInt("SubseriesMinClass");

				if((minClass > carClass)||
				(carUnlocked == 0)){
					carClickable.SetActive(false);
					carDisabled.SetActive(true);
				}
				
				if(meetsRestrictions(seriesPrefix,i) == false){
					carClickable.SetActive(false);
					carDisabled.SetActive(true);
				}
			}
			
			if(carUnlocked == 0){
				carClickable.SetActive(false);
				carDisabled.SetActive(true);
			}
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
		Debug.Log("Restriction " + restrictionType);
		Debug.Log("Restricted to " + restrictionValue);
			
		seriesTeam = "";
		seriesManu = "";
		seriesCar = 999;
		seriesDriverType = "";
		seriesRarity = 0;
		
		switch(restrictionType){
			case "Team":
				seriesTeam = restrictionValue;
				Debug.Log("Driver Type: " + seriesTeam);
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
				Debug.Log("Driver Type: " + seriesDriverType);
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

	public void toggleDropdown(){
		if(seriesDropdown.activeSelf == true){
			seriesDropdown.SetActive(false);
		} else {
			seriesDropdown.SetActive(true);
		}
		currentSeries.GetComponent<TMPro.TMP_Text>().text = DriverNames.getSeriesNiceName(seriesPrefix);
	}

	public void starterCars(){
		//Give a free McLeod for Cup '22
		if(PlayerPrefs.GetInt("cup2278Unlocked") == 0){
			PlayerPrefs.SetInt("cup2278Unlocked",1);
			PlayerPrefs.SetInt("cup2278Gears",0);
			PlayerPrefs.SetInt("cup2278Class",1);
		}
		
		//Give a free Mcleod for Cup '20
		if(PlayerPrefs.GetInt("cup2052Unlocked") == 0){
			PlayerPrefs.SetInt("cup2052Unlocked",1);
			PlayerPrefs.SetInt("cup2052Gears",0);
			PlayerPrefs.SetInt("cup2052Class",1);
		}
		
		//Give free Starters for DM1 '15
		if(PlayerPrefs.GetInt("dmc150Unlocked") == 0){
			PlayerPrefs.SetInt("dmc150Unlocked",1);
			PlayerPrefs.SetInt("dmc150Gears",0);
			PlayerPrefs.SetInt("dmc150Class",1);
		}
		if(PlayerPrefs.GetInt("dmc151Unlocked") == 0){
			PlayerPrefs.SetInt("dmc151Unlocked",1);
			PlayerPrefs.SetInt("dmc151Gears",0);
			PlayerPrefs.SetInt("dmc151Class",1);
		}
		if(PlayerPrefs.GetInt("dmc152Unlocked") == 0){
			PlayerPrefs.SetInt("dmc152Unlocked",1);
			PlayerPrefs.SetInt("dmc152Gears",0);
			PlayerPrefs.SetInt("dmc152Class",1);
		}
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
