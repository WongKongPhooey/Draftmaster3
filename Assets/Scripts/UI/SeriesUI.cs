using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SeriesUI : MonoBehaviour
{

	public List<RectTransform> shuffleArray;
	public List<string> rewardsList = new List<string>();
	public GameObject scrollFrame;
	public GameObject seriesTile;
	public GameObject seriesChildTile;
	public Transform tileFrame;
	public GameObject rewardsPopup;
	public GameObject rewardsGrid;
	public GameObject rewardCar;
	public GameObject entryReqsPopup;
	public GameObject difficultyPopup;
	public GameObject lapsPopup;
	public GameObject reqListText;
	public GameObject lapsValue;
	public GameObject lapsSetting;
	public GameObject minDiffValue;
	public GameObject diffValue;
	public GameObject diffNote;
	public GameObject difficultySetting;
	
	public GameObject alertPopup;
	
	Slider lapsSlider;
	Slider difficultySlider;
	
	int lapSeries;
	int lapSubseries;
	int lapsMulti;
	
	int diffSeries;
	int diffSubseries;
	int defaultDiff;
	int diffMulti;
	
	TMPro.TMP_Text lapsSliderValue;
	TMPro.TMP_Text lapsSliderMin;
	TMPro.TMP_Text lapsSliderMax;
	TMPro.TMP_Text diffSliderValue;
	TMPro.TMP_Text diffSliderMin;
	TMPro.TMP_Text diffSliderMax;
	
	public static int seriesId;
	public static int subSeriesId;
	public static string modSeriesPrefix;
	string seriesPrefix;
	int level;
	
    // Start is called before the first frame update
    void Start(){
        
		level = PlayerPrefs.GetInt("Level");
		
		scrollFrame = GameObject.Find("Main");
		
		//Hide popups after storing the vars
		rewardsPopup = GameObject.Find("RewardsPopup");
		entryReqsPopup = GameObject.Find("EntryReqsPopup");
		lapsPopup = GameObject.Find("LapsPopup");
		difficultyPopup = GameObject.Find("DifficultyPopup");
		reqListText = GameObject.Find("RequirementsList");
		rewardsPopup.SetActive(false);
		entryReqsPopup.SetActive(false);
		lapsPopup.SetActive(false);
		difficultyPopup.SetActive(false);
		
		lapsSlider = lapsSetting.transform.GetChild(1).GetComponent<Slider>();
		difficultySlider = difficultySetting.transform.GetChild(1).GetComponent<Slider>();
		
		loadAllSeries();
    }

	public void loadAllSeries(){
		seriesId = 999;
		foreach (Transform child in tileFrame){
			Destroy(child.gameObject);
		}
		
		for(int i=0;i<20;i++){
			SeriesData.loadSeries();
			//Skip through the non-driver #s
			if(SeriesData.offlineMenu[i] == null){
				//Debug.Log("No Event here: " + i);
				continue;
			}
			
			
			GameObject tileInst = Instantiate(seriesTile, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
			RectTransform tileObj = tileInst.GetComponent<RectTransform>();
			tileInst.transform.SetParent(tileFrame, false);
			tileInst.GetComponent<UIAnimate>().animOffset = i+1;
			tileInst.GetComponent<UIAnimate>().scaleIn();
			
			RawImage seriesImage = tileInst.transform.GetChild(0).GetComponent<RawImage>();
			TMPro.TMP_Text seriesName = tileInst.transform.GetChild(1).GetComponent<TMPro.TMP_Text>();
			TMPro.TMP_Text seriesDesc = tileInst.transform.GetChild(2).GetComponent<TMPro.TMP_Text>();
			GameObject seriesCover = tileInst.transform.GetChild(4).transform.gameObject;
			tileInst.GetComponent<SeriesUIFunctions>().seriesId = i;
			
			seriesName.text = SeriesData.offlineMenu[i];
			seriesDesc.text = SeriesData.seriesDescriptions[i];
			seriesImage.texture = Resources.Load<Texture2D>(SeriesData.offlineImage[i]);

			if(SeriesData.offlineMinLevel[i,0] > level){
				seriesCover.SetActive(true);
			}
		}
	}

	public void loadSubSeries(int seriesId){
		
		//Reroute
		if(seriesId == 10){
			loadAllModSeries();
			return;
		}
		
		//Debug.Log("Loading sub events of event: " + seriesId);
		foreach (Transform child in tileFrame){
			Destroy(child.gameObject);
		}
		
		scrollFrame.GetComponent<RectTransform>().anchoredPosition = Vector3.zero;

		//Custom ordering
		string customListOrderStr = SeriesData.offlineCustomListOrder[seriesId];
		string[] customListOrder = null;
		if(customListOrderStr != null){
			customListOrder = customListOrderStr.Split(',');
		}
		
		for(int i=0;i<25;i++){
			
			int k=i;
			
			//Skip through the non-driver #s
			if(SeriesData.offlineSeries[seriesId,k] == null){
				//Debug.Log("No Series here: " + i);
				continue;
			}
			
			//Reordered index
			if(customListOrderStr != null){
				k=int.Parse(customListOrder[i]);
			}
			
			GameObject tileInst = Instantiate(seriesChildTile, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
			tileInst.name = "SeriesChild" + seriesId + k;
			RectTransform tileObj = tileInst.GetComponent<RectTransform>();
			tileInst.transform.SetParent(tileFrame, false);
			
			tileInst.GetComponent<UIAnimate>().animOffset = k+1;
			tileInst.GetComponent<UIAnimate>().scaleIn();
			
			TMPro.TMP_Text seriesName = tileInst.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
			RawImage seriesImage = tileInst.transform.GetChild(1).GetComponent<RawImage>();
			TMPro.TMP_Text seriesDesc = tileInst.transform.GetChild(2).GetComponent<TMPro.TMP_Text>();
			GameObject seriesRewardsBtn = tileInst.transform.GetChild(8).transform.gameObject;
			GameObject seriesCover = tileInst.transform.GetChild(4).transform.gameObject;
			TMPro.TMP_Text difficultyBtn = tileInst.transform.GetChild(5).transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
			TMPro.TMP_Text lapsBtn = tileInst.transform.GetChild(6).transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
			GameObject rewardCollected = tileInst.transform.GetChild(9).transform.gameObject;
			TMPro.TMP_Text champsProgress = tileInst.transform.GetChild(10).GetComponent<TMPro.TMP_Text>();
			
			if(PlayerPrefs.HasKey("SeriesChampionship" + seriesId + k + "Round")){
				if(PlayerPrefs.GetInt("SeriesChampionship" + seriesId + k + "Round") > 0){
					int nextRound = PlayerPrefs.GetInt("SeriesChampionship" + seriesId + k + "Round") + 1;
					int totalRounds = PlayerPrefs.GetInt("SeriesChampionship" + seriesId + k + "Length");
					if(nextRound > totalRounds){
						champsProgress.text = "Collect Championship Rewards";
					} else {
						champsProgress.text = "Continue Championship (R" + nextRound + "/" + totalRounds + ")";
					}
				}
			}
			
			tileInst.GetComponent<SeriesUIFunctions>().seriesId = seriesId;
			tileInst.GetComponent<SeriesUIFunctions>().subSeriesId = k;
			
			seriesName.text = SeriesData.offlineSeries[seriesId,k];
			seriesDesc.text = "";
			difficultyBtn.text = ((SeriesData.offlineAILevel[seriesId,k] * 10) + 50).ToString() + "% Difficulty";
			if(PlayerPrefs.HasKey("CustomDifficulty" + seriesId + k)){
				difficultyBtn.text = ((PlayerPrefs.GetInt("CustomDifficulty" + seriesId + k) + 5) * 10) + "% Difficulty";
			}
			lapsBtn.text = (calcDistPercent(SeriesData.offlineAILevel[seriesId,k]) + "% Laps").ToString();	
			if(PlayerPrefs.HasKey("CustomLaps" + seriesId + k)){
				lapsBtn.text = (PlayerPrefs.GetInt("CustomLaps" + seriesId + k) * 2) + "% Laps";
			}
			seriesImage.texture = Resources.Load<Texture2D>(SeriesData.offlineSeriesImage[seriesId,k]); 
		
			if(SeriesData.offlineMinLevel[seriesId,k] > level){
				seriesCover.SetActive(true);
			}
		}
	}

	public void loadSeries(){
		PlayerPrefs.SetString("SeriesTrackList",SeriesData.offlineTracklists[seriesId,subSeriesId]);
		PlayerPrefs.SetString("CurrentSeriesIndex", seriesId + "" + subSeriesId);
		PlayerPrefs.SetString("CurrentSeriesName",SeriesData.offlineSeries[seriesId,subSeriesId]);
		PlayerPrefs.SetInt("CurrentSeries", seriesId);
		PlayerPrefs.SetInt("CurrentSubseries", subSeriesId);
		PlayerPrefs.DeleteKey("CurrentModSeries");
		PlayerPrefs.SetInt("SubseriesMinClass", SeriesData.offlineMinClass[seriesId,subSeriesId]);
		PlayerPrefs.SetString("RestrictionType",SeriesData.offlineMinType[seriesId,subSeriesId]);
		PlayerPrefs.SetString("RestrictionValue",getRestrictionValue(seriesId,subSeriesId));
		//Debug.Log("Series Restriction: " + SeriesData.offlineMinType[seriesId,subSeriesId] + " - " + getRestrictionValue(seriesId,subSeriesId));
		PlayerPrefs.SetInt("AIDifficulty", SeriesData.offlineAILevel[seriesId,subSeriesId]);
		PlayerPrefs.SetInt("SeriesFuel",SeriesData.offlineFuel[seriesId,subSeriesId]);
		PlayerPrefs.SetString("SeriesPrize",SeriesData.offlinePrizes[seriesId,subSeriesId]);
		PlayerPrefs.SetString("ActivePath","SingleRace");
		
		if(PlayerPrefs.HasKey("SeriesChampionship" + seriesId + subSeriesId + "Round")){
			if(PlayerPrefs.GetInt("SeriesChampionship" + seriesId + subSeriesId + "Round") > 0){
				PlayerPrefs.SetString("carTexture", PlayerPrefs.GetString("SeriesChampionship" + seriesId + subSeriesId + "CarTexture"));
				PlayerPrefs.SetInt("CarChoice", PlayerPrefs.GetInt("SeriesChampionship" + seriesId + subSeriesId + "CarChoice"));
				PlayerPrefs.SetString("carSeries", PlayerPrefs.GetString("SeriesChampionship" + seriesId + subSeriesId + "CarSeries"));
				PlayerPrefs.SetString("ActivePath","ChampionshipRace");
				//Debug.Log("Championship Car Series is " + PlayerPrefs.GetString("ChampionshipCarSeries"));
				SceneManager.LoadScene("Menus/ChampionshipHub");
			} else {
				SceneManager.LoadScene("Menus/Garage");
			}
		} else {
			SceneManager.LoadScene("Menus/Garage");
		}
	}
	
	public void loadModSeries(){
		PlayerPrefs.SetString("SeriesTrackList",ModData.getModSeriesTracklist(modSeriesPrefix,subSeriesId));
		PlayerPrefs.SetString("CurrentSeriesIndex", seriesId + "" + subSeriesId);
		PlayerPrefs.SetString("CurrentSeriesName",ModData.getModSeriesName(modSeriesPrefix,subSeriesId));
		PlayerPrefs.SetInt("CurrentSeries", seriesId);
		PlayerPrefs.SetInt("CurrentSubseries", subSeriesId);
		PlayerPrefs.SetString("CurrentModSeries", modSeriesPrefix);
		PlayerPrefs.SetString("ActivePath","SingleRace");
		
		if(PlayerPrefs.HasKey("SeriesChampionship" + modSeriesPrefix + subSeriesId + "Round")){
			if(PlayerPrefs.GetInt("SeriesChampionship" + modSeriesPrefix + subSeriesId + "Round") > 0){
				PlayerPrefs.SetString("carTexture", PlayerPrefs.GetString("SeriesChampionship" + modSeriesPrefix + seriesId +subSeriesId + "CarTexture"));
				PlayerPrefs.SetInt("CarChoice", PlayerPrefs.GetInt("SeriesChampionship" + modSeriesPrefix + seriesId +subSeriesId + "CarChoice"));
				PlayerPrefs.SetString("carSeries", PlayerPrefs.GetString("SeriesChampionship" + modSeriesPrefix + seriesId + subSeriesId + "CarSeries"));
				PlayerPrefs.SetString("ActivePath","ChampionshipRace");
				//Debug.Log("Championship Car Series is " + PlayerPrefs.GetString("ChampionshipCarSeries"));
				SceneManager.LoadScene("Menus/ChampionshipHub");
			} else {
				SceneManager.LoadScene("Menus/Garage");
			}
		} else {
			SceneManager.LoadScene("Menus/Garage");
		}
	}
	
	public void loadAllModSeries(){
		
		//Debug.Log("Loading sub events of event: " + seriesId);
		foreach (Transform child in tileFrame){
			Destroy(child.gameObject);
		}
		
		scrollFrame.GetComponent<RectTransform>().anchoredPosition = Vector3.zero;
		
		string[] modsArray = PlayerPrefs.GetString("ModsList").Split(',');
		
		if(modsArray[0] == ""){
			alertPopup.GetComponent<AlertManager>().showPopup("No Modded Series Found","Install mods containing a custom race series to get started!","dm2logo");
			return;
		}
		
		int j=0;
		int totalModdedSeries=0;
		
		foreach(string modSet in modsArray){
			//Debug.Log(modSet);
			string[] modData = modSet.Split('|');
			string modSeriesPrefix = modData[0];
			
			for(int i=0;i<10;i++){
			
				if(ModData.getModSeriesName(modData[0], i) == null){
					continue;
				}
				
				GameObject tileInst = Instantiate(seriesChildTile, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
				tileInst.name = "SeriesChild10" + j;
				
				RectTransform tileObj = tileInst.GetComponent<RectTransform>();
				tileInst.transform.SetParent(tileFrame, false);
				
				tileInst.GetComponent<UIAnimate>().animOffset = 1;
				tileInst.GetComponent<UIAnimate>().scaleIn();
				
				TMPro.TMP_Text seriesName = tileInst.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
				RawImage seriesImage = tileInst.transform.GetChild(1).GetComponent<RawImage>();
				TMPro.TMP_Text seriesDesc = tileInst.transform.GetChild(2).GetComponent<TMPro.TMP_Text>();
				GameObject seriesRewardsBtn = tileInst.transform.GetChild(6).transform.gameObject;
				GameObject seriesCover = tileInst.transform.GetChild(4).transform.gameObject;
				GameObject rewardCollected = tileInst.transform.GetChild(7).transform.gameObject;
				TMPro.TMP_Text champsProgress = tileInst.transform.GetChild(8).GetComponent<TMPro.TMP_Text>();
			
				seriesName.text = ModData.getModSeriesName(modSeriesPrefix, i);
				seriesDesc.text = ModData.getModSeriesDesc(modSeriesPrefix, i);
				seriesImage.texture = ModData.getTexture(modSeriesPrefix, 97);
				
				if(PlayerPrefs.HasKey("SeriesChampionship" + modSeriesPrefix + i + "Round")){
					if(PlayerPrefs.GetInt("SeriesChampionship" + modSeriesPrefix + i + "Round") > 0){
						int nextRound = PlayerPrefs.GetInt("SeriesChampionship" + modSeriesPrefix + i + "Round") + 1;
						int totalRounds = PlayerPrefs.GetInt("SeriesChampionship" + modSeriesPrefix + i + "Length");
						if(nextRound > totalRounds){
							champsProgress.text = "Championship Finished \nCollect Rewards";
						} else {
							champsProgress.text = "Championship In Progress \nRound " + nextRound + "/" + totalRounds;
						}
					}
				}
				tileInst.GetComponent<SeriesUIFunctions>().seriesId = seriesId;
				tileInst.GetComponent<SeriesUIFunctions>().modSeriesPrefix = modSeriesPrefix;
				tileInst.GetComponent<SeriesUIFunctions>().subSeriesId = i;
				j++;
				
				totalModdedSeries++;
			}
		}
		
		if(totalModdedSeries == 0){
			alertPopup.GetComponent<AlertManager>().showPopup("No Modded Series Found","Install mods containing a custom race series to get started!","dm2logo");
			return;
		}
	}
	
	public string getEntryReqs(int series, int subSeries){
		string restrictionValue = "";
		restrictionValue += "Level: " + SeriesData.offlineMinLevel[series,subSeries] + "+ \n";
		restrictionValue += "Driver Class: " + classAbbr(SeriesData.offlineMinClass[series,subSeries]) + "+ \n";
		switch(SeriesData.offlineMinType[series,subSeries]){
			case "Team":
				restrictionValue += "Team: " + SeriesData.offlineMinTeam[series,subSeries];
			break;
			case "Manufacturer":
				restrictionValue += "Manufacturer: " + SeriesData.offlineMinManu[series,subSeries];
			break;
			case "Car":
				restrictionValue += "Exact Car: #" + SeriesData.offlineExactCar[series,subSeries];
			break;
			case "Type":
				restrictionValue += "Driver Type: " + SeriesData.offlineMinDriverType[series,subSeries];
			break;
			case "Rarity":
				restrictionValue += "Driver Rarity: " + SeriesData.offlineMinRarity[series,subSeries];
			break;
			default:
				restrictionValue += "";
			break;
		}
		return restrictionValue;
	}
	
	public string getRestrictionValue(int series, int subSeries){
		string restrictionValue = "";
		switch(SeriesData.offlineMinType[series,subSeries]){
			case "Team":
				restrictionValue = SeriesData.offlineMinTeam[series,subSeries];
			break;
			case "Manufacturer":
				restrictionValue = SeriesData.offlineMinManu[series,subSeries];
			break;
			case "Car":
				restrictionValue = SeriesData.offlineExactCar[series,subSeries].ToString();
			break;
			case "Type":
				restrictionValue = SeriesData.offlineMinDriverType[series,subSeries];
			break;
			case "Rarity":
				restrictionValue = SeriesData.offlineMinRarity[series,subSeries].ToString();
			break;
			default:
				restrictionValue = "";
			break;
		}
		return restrictionValue;
	}

	public void showEntryReqsPopup(int series, int subSeries){
		entryReqsPopup.SetActive(true);
		reqListText = GameObject.Find("RequirementsList");
		reqListText.GetComponent<TMPro.TMP_Text>().text = getEntryReqs(series,subSeries);
	}
	
	public void showLapsPopup(int series, int subSeries){
		lapSeries = series;
		lapSubseries = subSeries;
		lapsMulti = calcDistPercent(SeriesData.offlineAILevel[lapSeries,lapSubseries]);
		if(PlayerPrefs.HasKey("CustomLaps" + lapSeries + lapSubseries)){
			lapsMulti = PlayerPrefs.GetInt("CustomLaps" + lapSeries + lapSubseries) * 2;
		}
		//Debug.Log("Laps Multi: " + lapsMulti);
		lapsSlider.value = lapsMulti/2;
		lapsPopup.SetActive(true);
		lapsValue.GetComponent<TMPro.TMP_Text>().text = "Between " + (3 * (lapsMulti/2)) + " and " + (10 * (lapsMulti/2)) + " Laps";
	}
	
	public void SaveLapsSlider(){
		int customLaps = (int)lapsSlider.value;
		PlayerPrefs.SetInt("CustomLaps" + lapSeries + lapSubseries,customLaps);
		//Debug.Log("CustomLaps" + lapSeries + lapSubseries + ": " + customLaps);
		lapsValue.GetComponent<TMPro.TMP_Text>().text = "Between " + (3 * customLaps) + " and " + (10 * customLaps) + " Laps";
	}
	
	public void showDifficultyPopup(int series, int subSeries){
		diffSeries = series;
		diffSubseries = subSeries;
		defaultDiff = SeriesData.offlineAILevel[diffSeries,diffSubseries];
		diffMulti = defaultDiff;
		int minDiffMulti = diffMulti;
		difficultySlider.minValue = diffMulti;
		//Debug.Log("Diff Min: " + diffMulti);
		if(PlayerPrefs.HasKey("CustomDifficulty" + diffSeries + diffSubseries)){
			diffMulti = PlayerPrefs.GetInt("CustomDifficulty" + diffSeries + diffSubseries);
		}
		//Debug.Log("Diff Multi: " + diffMulti);
		difficultySlider.value = diffMulti;
		difficultyPopup.SetActive(true);
		minDiffValue.GetComponent<TMPro.TMP_Text>().text = ((minDiffMulti + 5) * 10) + "%";
		diffValue.GetComponent<TMPro.TMP_Text>().text = ((diffMulti + 5) * 10) + "% Difficulty";
		diffNote.GetComponent<TMPro.TMP_Text>().text = "Note: Difficulty cannot be set lower than the default (" + ((diffMulti + 5) * 10) + "%)";
	}
	
	public void SaveDifficultySlider(){
		int customDiff = (int)difficultySlider.value;
		PlayerPrefs.SetInt("CustomDifficulty" + diffSeries + diffSubseries,customDiff);
		//Debug.Log("CustomDifficulty" + diffSeries + diffSubseries + ": " + customDiff);
		diffValue.GetComponent<TMPro.TMP_Text>().text = ((customDiff + 5) * 10) + "% Difficulty";
	}

	public void closeEntryReqsPopup(){
		entryReqsPopup.SetActive(false);
	}

	public void showRewardsPopup(int subMenu, int subEvent){
		loadPossibleRewards(subMenu, subEvent);
	}

	public void loadPossibleRewards(int subMenu, int subEvent){
		rewardsList.Clear();
		rewardsPopup.SetActive(true);
		string rewardsCode = SeriesData.offlinePrizes[subMenu,subEvent];
		
		//Generate the list of possible rewards
		rewardsList = SeriesData.ListRewards(rewardsCode);
		
		foreach(string reward in rewardsList){
			GameObject rewardInst = Instantiate(rewardCar, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
			rewardInst.transform.SetParent(rewardsGrid.GetComponent<RectTransform>(), false);
			RawImage carPaint = rewardInst.GetComponent<RawImage>();
			string rewardString;
			if(SeriesData.offlinePrizes[subMenu,subEvent] != "AltPaint"){
				rewardString = reward.Insert(5, "livery");
			} else {
				rewardString = reward;
			}
			carPaint.texture = (Texture2D)Resources.Load(rewardString);
			//Debug.Log("Reward loaded: " + rewardString);
		}
	}

	public void closeRewardsPopup(){
		foreach(Transform reward in rewardsGrid.transform){
			GameObject.Destroy(reward.gameObject);
		}
		rewardsPopup.SetActive(false);
	}
	
	public void closeLapsPopup(){
		GameObject subSeriesTile = GameObject.Find("SeriesChild" + lapSeries + lapSubseries);
		TMPro.TMP_Text lapsBtn = subSeriesTile.transform.GetChild(6).transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
		lapsBtn.text = (calcDistPercent(SeriesData.offlineAILevel[lapSeries,lapSubseries]) + "% Laps").ToString();	
		if(PlayerPrefs.HasKey("CustomLaps" + lapSeries + lapSubseries)){
			lapsBtn.text = (PlayerPrefs.GetInt("CustomLaps" + lapSeries + lapSubseries) * 2) + "% Laps";
		}
		lapSeries = 999;
		lapSubseries = 999;
		lapsMulti = 1;
		lapsPopup.SetActive(false);
	}
	
	public void closeDifficultyPopup(){
		GameObject subSeriesTile = GameObject.Find("SeriesChild" + diffSeries + diffSubseries);
		TMPro.TMP_Text diffBtn = subSeriesTile.transform.GetChild(5).transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
		diffBtn.text = (((SeriesData.offlineAILevel[diffSeries,diffSubseries] + 5) * 10) + "% Difficulty").ToString();	
		if(PlayerPrefs.HasKey("CustomDifficulty" + diffSeries + diffSubseries)){
			diffBtn.text = ((PlayerPrefs.GetInt("CustomDifficulty" + diffSeries + diffSubseries) + 5) * 10) + "% Difficulty";
		}
		diffSeries = 999;
		diffSubseries = 999;
		diffMulti = 1;
		difficultyPopup.SetActive(false);
	}

	public static int calcDistPercent(int difficulty){
		if(difficulty < 3){
			difficulty = 3;
		}
		return (2 * (difficulty / 3));
	}

	public void dynamicBackButton(){
		if(seriesId == 999){
			PlayerPrefs.DeleteKey("ActivePath");
			SceneManager.LoadScene("Menus/MainMenu");
		} else {
			loadAllSeries();
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

    // Update is called once per frame
    void Update()
    {
        
    }
}
