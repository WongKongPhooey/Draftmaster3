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
	public GameObject reqListText;
	public static int seriesId;
	public static int subSeriesId;
	string seriesPrefix;
	int level;
	
    // Start is called before the first frame update
    void Start(){
        
		level = PlayerPrefs.GetInt("Level");
		
		scrollFrame = GameObject.Find("Main");
		
		//Hide rewards popup after storing the var
		rewardsPopup = GameObject.Find("RewardsPopup");
		rewardsPopup.SetActive(false);
		
		entryReqsPopup = GameObject.Find("EntryReqsPopup");
		entryReqsPopup.SetActive(false);
		
		reqListText = GameObject.Find("RequirementsList");
		
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
			//if((i==10)&&(PlayerPrefs.GetInt("FreeModding") == 0)&&(PlayerPrefs.GetInt("TransferTokens") < 999)){
			//	continue;
			//}
			
			
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
			RectTransform tileObj = tileInst.GetComponent<RectTransform>();
			tileInst.transform.SetParent(tileFrame, false);
			
			tileInst.GetComponent<UIAnimate>().animOffset = k+1;
			tileInst.GetComponent<UIAnimate>().scaleIn();
			
			TMPro.TMP_Text seriesName = tileInst.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
			RawImage seriesImage = tileInst.transform.GetChild(1).GetComponent<RawImage>();
			TMPro.TMP_Text seriesDesc = tileInst.transform.GetChild(2).GetComponent<TMPro.TMP_Text>();
			GameObject seriesRewardsBtn = tileInst.transform.GetChild(6).transform.gameObject;
			GameObject seriesCover = tileInst.transform.GetChild(4).transform.gameObject;
			GameObject rewardCollected = tileInst.transform.GetChild(7).transform.gameObject;
			TMPro.TMP_Text champsProgress = tileInst.transform.GetChild(8).GetComponent<TMPro.TMP_Text>();
			
			if(PlayerPrefs.HasKey("SeriesChampionship" + seriesId + k + "Round")){
				if(PlayerPrefs.GetInt("SeriesChampionship" + seriesId + k + "Round") > 0){
					int nextRound = PlayerPrefs.GetInt("SeriesChampionship" + seriesId + k + "Round") + 1;
					int totalRounds = PlayerPrefs.GetInt("SeriesChampionship" + seriesId + k + "Length");
					if(nextRound > totalRounds){
						champsProgress.text = "Championship Finished \nCollect Rewards";
					} else {
						champsProgress.text = "Championship In Progress \nRound " + nextRound + "/" + totalRounds;
					}
				}
			}
			
			tileInst.GetComponent<SeriesUIFunctions>().seriesId = seriesId;
			tileInst.GetComponent<SeriesUIFunctions>().subSeriesId = k;
			
			seriesName.text = SeriesData.offlineSeries[seriesId,k];
			seriesDesc.text = ((SeriesData.offlineAILevel[seriesId,k] * 10) + 50).ToString() + "% Difficulty";
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
	
	public void loadAllModSeries(){
		
		//Debug.Log("Loading sub events of event: " + seriesId);
		foreach (Transform child in tileFrame){
			Destroy(child.gameObject);
		}
		
		scrollFrame.GetComponent<RectTransform>().anchoredPosition = Vector3.zero;
		
		string[] modsArray = PlayerPrefs.GetString("ModsList").Split(',');
		foreach(string modSet in modsArray){
			//Debug.Log(modSet);
			string[] modData = modSet.Split('|');
			string modSeriesPrefix = modData[0];
			
			for(int i=0;i<100;i++){
			
				if(ModData.getModSeriesName(modData[0], i) == null){
					continue;
				}
				
				GameObject tileInst = Instantiate(seriesChildTile, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
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
			}
			
			//tileInst.GetComponent<SeriesUIFunctions>().seriesId = seriesId;
			//tileInst.GetComponent<SeriesUIFunctions>().subSeriesId = k;
			//seriesImage.texture = ModData.getModSeriesImage[seriesPrefix,k];
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

	public void closeEntryReqsPopup(){
		entryReqsPopup.SetActive(false);
	}

	public void showRewardsPopup(int subMenu, int subEvent){
		rewardsList.Clear();
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
		rewardsPopup.SetActive(true);
	}

	public void closeRewardsPopup(){
		foreach(Transform reward in rewardsGrid.transform){
			GameObject.Destroy(reward.gameObject);
		}
		rewardsPopup.SetActive(false);
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
