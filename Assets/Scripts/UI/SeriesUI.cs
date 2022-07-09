using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SeriesUI : MonoBehaviour
{
	public int week;
	string[] eventWeeks;
	public List<RectTransform> shuffleArray;
	public List<string> rewardsList = new List<string>();
	public GameObject seriesTile;
	public GameObject seriesChildTile;
	public Transform tileFrame;
	public GameObject rewardsPopup;
	public GameObject rewardsGrid;
	public GameObject rewardCar;
	public GameObject entryReqsPopup;
	public GameObject reqListText;
	public static int subMenuId;
	public static int subEventId;
	string seriesPrefix;
	
    // Start is called before the first frame update
    void Start(){
        
		//Hide rewards popup after storing the var
		rewardsPopup = GameObject.Find("RewardsPopup");
		rewardsPopup.SetActive(false);
		
		entryReqsPopup = GameObject.Find("EntryReqsPopup");
		entryReqsPopup.SetActive(false);
		
		reqListText = GameObject.Find("RequirementsList");
		
		loadSeries();
    }

	public void loadAllSeries(){
		subMenuId = 999;
		for(int i=0;i<9;i++){
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
			
			RawImage seriesImage = tileInst.transform.GetChild(1).GetComponent<RawImage>();
			TMPro.TMP_Text seriesName = tileInst.transform.GetChild(2).GetComponent<TMPro.TMP_Text>();
			TMPro.TMP_Text seriesDesc = tileInst.transform.GetChild(3).GetComponent<TMPro.TMP_Text>();
			tileInst.transform.GetChild(4).GetComponent<EventUIFunctions>().subMenuId = i;
			
			seriesName.text = SeriesData.offlineMenu[i];
			seriesDesc.text = SeriesData.seriesDescriptions[i];
			seriesImage.texture = Resources.Load<Texture2D>(SeriesData.offlineImage[i]); 
		}
	}

	public void loadSubSeries(int subMenuId){
		//Debug.Log("Loading sub events of event: " + subMenuId);
		foreach (Transform child in tileFrame){
			Destroy(child.gameObject);
		}
		for(int i=0;i<10;i++){
			//Skip through the non-driver #s
			if(SeriesData.offlineSeries[subMenuId,i] == null){
				//Debug.Log("No Event here: " + i);
				continue;
			}
			
			GameObject tileInst = Instantiate(seriesChildTile, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
			RectTransform tileObj = tileInst.GetComponent<RectTransform>();
			tileInst.transform.SetParent(tileFrame, false);
			
			tileInst.GetComponent<UIAnimate>().animOffset = i+1;
			tileInst.GetComponent<UIAnimate>().scaleIn();
			
			TMPro.TMP_Text eventName = tileInst.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
			RawImage eventImage = tileInst.transform.GetChild(1).GetComponent<RawImage>();
			TMPro.TMP_Text eventDesc = tileInst.transform.GetChild(2).GetComponent<TMPro.TMP_Text>();
			GameObject eventRewardsBtn = tileInst.transform.GetChild(5).transform.gameObject;
			GameObject eventCover = tileInst.transform.GetChild(6).transform.gameObject;
			GameObject rewardCollected = tileInst.transform.GetChild(7).transform.gameObject;
			tileInst.GetComponent<EventUIFunctions>().subMenuId = subMenuId;
			tileInst.GetComponent<EventUIFunctions>().subEventId = i;
			
			eventName.text = EventData.offlineEventChapter[subMenuId,i];
			eventDesc.text = EventData.eventChapterDescriptions[subMenuId,i];
			eventImage.texture = Resources.Load<Texture2D>(EventData.offlineChapterImage[subMenuId,i]);
			
			//No dupe rewards allowed in progression events
			if(EventData.offlineEventType[subMenuId] == "Progression"){
				if(PlayerPrefs.GetInt("BestFinishPosition" + subMenuId + "" + i + "EVENT0") == 1){
					eventRewardsBtn.SetActive(false);
					rewardCollected.SetActive(true);
				//} else {
					//Debug.Log("Best Finish on " + subMenuId + "/" + i + ": " + PlayerPrefs.GetInt("BestFinishPosition" + subMenuId + "" + i + "EVENT0"));
				}
			}
			
			//Never lock the first sub-event
			if(i>0){
				//If previous sub-event hasn't been won, lock.
				if(PlayerPrefs.GetInt("BestFinishPosition" + subMenuId + "" + (i-1) + "EVENT0") != 1){
					eventCover.SetActive(true);
				}
			}
		}
	}

	public void loadSeries(){
		Debug.Log("Load Series " + subMenuId + "," + subEventId + "");
		
		PlayerPrefs.SetString("SeriesTrackList",SeriesData.offlineTracklists[subMenuId,subEventId]);
		PlayerPrefs.SetString("CurrentSeriesIndex", subMenuId + "" + subEventId + "EVENT");
		PlayerPrefs.SetString("CurrentSeriesName",SeriesData.offlineSeries[subMenuId,subEventId]);
		PlayerPrefs.SetInt("CurrentSeries", subMenuId);
		PlayerPrefs.SetInt("CurrentSubseries", subEventId);
		PlayerPrefs.SetInt("SubseriesDailyPlays",999);
		PlayerPrefs.SetInt("SubseriesMinClass", SeriesData.offlineMinClass[subMenuId,subEventId]);
		PlayerPrefs.SetString("RestrictionType",SeriesData.offlineMinType[subMenuId,subEventId]);
		PlayerPrefs.SetString("RestrictionValue",getRestrictionValue());
		PlayerPrefs.SetInt("AIDifficulty", SeriesData.offlineAILevel[subMenuId,subEventId]);
		PlayerPrefs.SetInt("SeriesFuel",5);
		PlayerPrefs.SetString("SeriesPrize",SeriesData.offlinePrizes[subMenuId,subEventId]);
		PlayerPrefs.SetString("RaceType","Event");
		
		if(SeriesData.offlineSeries[subMenuId,subEventId] != null){
			PlayerPrefs.SetString("FixedSeries", SeriesData.offlineSeries[subMenuId,subEventId]);
		}
		
		SceneManager.LoadScene("CarSelect");
	}
	
	public string getRestrictionValue(){
		string restrictionValue = "";
		restrictionValue += "Driver Class: " + classAbbr(SeriesData.offlineMinClass[subMenuId,subEventId]) + "+ \n";
		switch(SeriesData.offlineMinType[subMenuId,subEventId]){
			case "Team":
				restrictionValue += "Team: " + SeriesData.offlineMinTeam[subMenuId,subEventId];
			break;
			case "Manufacturer":
				restrictionValue += "Manufacturer: " + SeriesData.offlineMinManu[subMenuId,subEventId];
			break;
			case "Car":
				restrictionValue += "Exact Car: #" + SeriesData.offlineExactCar[subMenuId,subEventId];
			break;
			case "Type":
				restrictionValue += "Driver Type: " + SeriesData.offlineMinDriverType[subMenuId,subEventId];
			break;
			case "Rarity":
				restrictionValue += "Driver Rarity: " + SeriesData.offlineMinRarity[subMenuId,subEventId];
			break;
			default:
				restrictionValue += "No Entry Requirements";
			break;
		}
		return restrictionValue;
	}

	public void showEntryReqsPopup(int subMenu, int subEvent){
		entryReqsPopup.SetActive(true);
		reqListText = GameObject.Find("RequirementsList");
		reqListText.GetComponent<TMPro.TMP_Text>().text = getRestrictionValue();
		//reqListText.GetComponent<TMPro.TMP_Text>().text = "Test";
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
		foreach (Transform reward in rewardsGrid.transform) {
			GameObject.Destroy(reward.gameObject);
		}
		rewardsPopup.SetActive(false);
	}

	public void dynamicBackButton(){
		if(subMenuId == 999){
			SceneManager.LoadScene("MainMenu");
		} else {
			loadSeries();
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
