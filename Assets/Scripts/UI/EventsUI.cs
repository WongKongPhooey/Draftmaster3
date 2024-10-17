using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class EventsUI : MonoBehaviour
{
    public int week;
	string[] eventWeeks;
	public List<RectTransform> shuffleArray;
	public List<string> rewardsList = new List<string>();
	public GameObject eventTile;
	public GameObject eventChildTile;
	public Transform tileFrame;
	public GameObject rewardsPopup;
	public GameObject rewardsGrid;
	public GameObject rewardCar;
	public GameObject entryReqsPopup;
	public GameObject reqListText;
	
	public GameObject alertPopup;
	
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
		
		if(PlayerPrefs.HasKey("GameWeek")){
			//Get the last known cycle day
			week = PlayerPrefs.GetInt("GameWeek");
			//Debug.Log("Last known week: " + week);
		} else {
			//First login
			week = 1;
		}
		loadEvents();
    }

	public void loadEvents(){
		subMenuId = 999;
		foreach (Transform child in tileFrame){
			Destroy(child.gameObject);
		}
		EventData.loadEvents();
		for(int i=9;i>=0;i--){
			//Skip through the non-driver #s
			if(EventData.offlineEvent[i] == null){
				//Debug.Log("No Event here: " + i);
				continue;
			}
			
			GameObject tileInst = Instantiate(eventTile, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
			RectTransform tileObj = tileInst.GetComponent<RectTransform>();
			tileInst.transform.SetParent(tileFrame, false);
			tileInst.GetComponent<UIAnimate>().animOffset = i+1;
			tileInst.GetComponent<UIAnimate>().scaleIn();
			
			TMPro.TMP_Text eventWeek = tileInst.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
			RawImage eventImage = tileInst.transform.GetChild(1).GetComponent<RawImage>();
			TMPro.TMP_Text eventName = tileInst.transform.GetChild(2).GetComponent<TMPro.TMP_Text>();
			TMPro.TMP_Text eventDesc = tileInst.transform.GetChild(3).GetComponent<TMPro.TMP_Text>();
			tileInst.transform.GetChild(4).GetComponent<EventUIFunctions>().subMenuId = i;
			
			//Is the event live?
			eventWeeks = EventData.offlineEventWeek[i].Split(',');				
			if(eventWeeks.Any((week.ToString()).Contains)){
				eventWeek.text = "Live!";
			} else {
				eventWeek.text = "Week " + EventData.offlineEventWeek[i];
			}
			eventName.text = EventData.offlineEvent[i];
			eventDesc.text = EventData.eventDescriptions[i];
			//carClass.text = DriverNames.getClass(seriesPrefix, i);
			eventImage.texture = Resources.Load<Texture2D>(EventData.offlineEventImage[i]); 
		}
		int sortCounter=0;
		foreach (Transform child in tileFrame){
			//Debug.Log("Found one!" + sortCounter);
			TMPro.TMP_Text eventWeek = child.GetChild(0).GetComponent<TMPro.TMP_Text>();
			TMPro.TMP_Text eventName = child.GetChild(2).GetComponent<TMPro.TMP_Text>();
			GameObject eventClickable = child.transform.GetChild(4).transform.gameObject;
			GameObject eventCover = child.transform.GetChild(5).transform.gameObject;

			if((!eventWeek.text.Contains(week.ToString()))
			&&(!eventWeek.text.Contains("Live!"))){
				RectTransform tileObj = child.GetComponent<RectTransform>();
				Debug.Log(child);
				if(tileObj != null){
					shuffleArray.Add(tileObj);
				}
				//tileObj.SetAsLastSibling();
				eventCover.SetActive(true);
				eventClickable.SetActive(false);
				//Debug.Log("Sort to end: " + eventName.text);
			} else {
				//Debug.Log("Event is live: " + eventName.text);
			}
			sortCounter++;
		}
		sortTiles();
	}

	public void loadSubEvent(int i, bool reversed = false){
		//EventData.loadEvents();
		
		GameObject tileInst = Instantiate(eventChildTile, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
		RectTransform tileObj = tileInst.GetComponent<RectTransform>();
		tileInst.transform.SetParent(tileFrame, false);
		
		tileInst.GetComponent<UIAnimate>().animOffset = i+1;
		tileInst.GetComponent<UIAnimate>().scaleIn();
		
		TMPro.TMP_Text eventName = tileInst.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
		RawImage eventImage = tileInst.transform.GetChild(1).GetComponent<RawImage>();
		TMPro.TMP_Text eventDesc = tileInst.transform.GetChild(2).GetComponent<TMPro.TMP_Text>();
		GameObject eventCover = tileInst.transform.GetChild(4).transform.gameObject;
		GameObject eventRewardsBtn = tileInst.transform.GetChild(6).transform.gameObject;
		GameObject rewardCollected = tileInst.transform.GetChild(7).transform.gameObject;
		tileInst.GetComponent<EventUIFunctions>().subMenuId = subMenuId;
		tileInst.GetComponent<EventUIFunctions>().subEventId = i;
		tileInst.GetComponent<EventUIFunctions>().rewardCollected = false;
		
		eventName.text = EventData.offlineEventChapter[subMenuId,i];
		eventDesc.text = EventData.eventChapterDescriptions[subMenuId,i];
		eventImage.texture = Resources.Load<Texture2D>(EventData.offlineChapterImage[subMenuId,i]);
		
		//Testing
		#if UNITY_EDITOR
		//PlayerPrefs.SetInt("BestFinishPosition" + subMenuId + "" + i + "EVENT1", 2);
		#endif
		
		//No dupe rewards allowed in progression events
		if((PlayerPrefs.GetInt("BestFinishPosition" + subMenuId + "" + i + "EVENT1") == 1)&&(EventData.offlineEventType[subMenuId] != "Replay")){
			eventRewardsBtn.SetActive(false);
			rewardCollected.SetActive(true);
			tileInst.GetComponent<EventUIFunctions>().rewardCollected = true;
			checkRewardCollected(subMenuId,i);
		}
			
		if(EventData.offlineEventType[subMenuId] == "Progression"){
			//Never lock the first sub-event
			if(i>0){
				//If previous sub-event hasn't been won, lock.
				if(PlayerPrefs.GetInt("BestFinishPosition" + subMenuId + "" + (i-1) + "EVENT1") != 1){
					eventCover.SetActive(true);
				}
			}
		}
	}

	public void loadSubEvents(int subMenuId){
		//Debug.Log("Loading sub events of event: " + subMenuId);
		foreach (Transform child in tileFrame){
			Destroy(child.gameObject);
		}
		//Moments show newest first
		if(subMenuId == 4){
			for(int i=14;i>=0;i--){
				//Skip through the empty chapters
				if(EventData.offlineEventChapter[subMenuId,i] == null){
					continue;
				}
				loadSubEvent(i,true);
			}
		} else {
			for(int i=0;i<=14;i++){
				if(EventData.offlineEventChapter[subMenuId,i] == null){
					continue;
				}
				loadSubEvent(i,false);
			}
		}
	}

	public void loadEvent(){
		PlayerPrefs.SetString("SeriesTrackList",EventData.offlineTracklists[subMenuId,subEventId]);
		PlayerPrefs.SetString("CurrentSeriesIndex", subMenuId + "" + subEventId + "EVENT");
		PlayerPrefs.SetString("CurrentSeriesName",EventData.offlineEventChapter[subMenuId,subEventId]);
		PlayerPrefs.SetInt("CurrentSeries", subMenuId);
		PlayerPrefs.SetInt("CurrentSubseries", subEventId);
		PlayerPrefs.SetInt("SubseriesMinClass", EventData.offlineMinClass[subMenuId,subEventId]);
		PlayerPrefs.SetString("ExactSeries",EventData.offlineExactSeries[subMenuId,subEventId]);
		PlayerPrefs.SetString("RestrictionType",EventData.offlineMinType[subMenuId,subEventId]);
		PlayerPrefs.SetString("RestrictionValue",getRestrictionValue(subMenuId,subEventId));
		PlayerPrefs.SetInt("AIDifficulty", EventData.offlineAILevel[subMenuId,subEventId]);
		PlayerPrefs.SetInt("SeriesFuel",5);
		PlayerPrefs.SetString("SeriesPrize",EventData.offlinePrizes[subMenuId,subEventId]);
		PlayerPrefs.SetString("SeriesPrizeAmt",EventData.offlineSetPrizes[subMenuId,subEventId]);
		PlayerPrefs.SetString("ActivePath","EventRace");
		PlayerPrefs.SetString("RaceType","Event");
		
		if(EventData.offlineSeries[subMenuId,subEventId] != null){
			PlayerPrefs.SetString("FixedSeries", EventData.offlineSeries[subMenuId,subEventId]);
		}
		if(EventData.offlineCustomCar[subMenuId,subEventId] != null){
			PlayerPrefs.SetString("CustomCar", EventData.offlineCustomCar[subMenuId,subEventId]);
		}
		if(EventData.offlineCustomField[subMenuId,subEventId] != null){
			PlayerPrefs.SetString("CustomField", EventData.offlineCustomField[subMenuId,subEventId]);
		}
		if(EventData.offlineCustomFieldOrder[subMenuId,subEventId] != null){
			PlayerPrefs.SetString("CustomFieldOrder", EventData.offlineCustomFieldOrder[subMenuId,subEventId]);
		}
		if(EventData.offlineStartingLap[subMenuId,subEventId] != 0){
			PlayerPrefs.SetInt("StartingLap", EventData.offlineStartingLap[subMenuId,subEventId]);
			//Debug.Log("Starting Lap set: " + EventData.offlineStartingLap[subMenuId,subEventId]);
		}
		if(EventData.offlineRaceLaps[subMenuId,subEventId] != 0){
			PlayerPrefs.SetInt("CustomRaceLaps", EventData.offlineRaceLaps[subMenuId,subEventId]);
			Debug.Log("Event Race Laps set: " + EventData.offlineRaceLaps[subMenuId,subEventId]);
		}
		if(EventData.offlineModifier[subMenuId,subEventId] != null){
			PlayerPrefs.SetString("RaceModifier", EventData.offlineModifier[subMenuId,subEventId]);
			//Debug.Log("Race Modifier set: " + EventData.offlineModifier[subMenuId,subEventId]);
		}
		if(EventData.offlineMoment[subMenuId,subEventId] != null){
			PlayerPrefs.SetString("RaceMoment", EventData.offlineMoment[subMenuId,subEventId]);
			//Debug.Log("Race Moment set: " + EventData.offlineMoment[subMenuId,subEventId]);
		}
		SceneManager.LoadScene("Menus/Garage");
	}
	
	public string getEntryReqs(int subMenu, int subEvent){
		string restrictionValue = "";
		restrictionValue += "Driver Class: " + classAbbr(EventData.offlineMinClass[subMenu,subEvent]) + "+ \n";
		switch(EventData.offlineMinType[subMenu,subEvent]){
			case "Team":
				restrictionValue += "Team: " + EventData.offlineMinTeam[subMenu,subEvent];
			break;
			case "Manufacturer":
				restrictionValue += "Manufacturer: " + EventData.offlineMinManu[subMenu,subEvent];
			break;
			case "Car":
				restrictionValue += "Exact Car: #" + EventData.offlineExactCar[subMenu,subEvent];
			break;
			case "Driver":
				restrictionValue += "Exact Driver: " + EventData.offlineExactDriver[subMenu,subEvent];
			break;
			case "Type":
				restrictionValue += "Driver Type: " + EventData.offlineMinDriverType[subMenu,subEvent];
			break;
			case "Rarity":
				restrictionValue += "Driver Rarity: " + EventData.offlineMinRarity[subMenu,subEvent];
			break;
			default:
				restrictionValue += "No Entry Requirements";
			break;
		}
		return restrictionValue;
	}

	public string getRestrictionValue(int subMenu, int subEvent){
		string restrictionValue = "";
		switch(EventData.offlineMinType[subMenu,subEvent]){
			case "Team":
				restrictionValue = EventData.offlineMinTeam[subMenu,subEvent];
			break;
			case "Manufacturer":
				restrictionValue = EventData.offlineMinManu[subMenu,subEvent];
			break;
			case "Car":
				restrictionValue = EventData.offlineExactCar[subMenu,subEvent].ToString();
			break;
			case "Type":
				restrictionValue = EventData.offlineMinDriverType[subMenu,subEvent];
			break;
			case "Rarity":
				restrictionValue = EventData.offlineMinRarity[subMenu,subEvent].ToString();
			break;
			default:
				restrictionValue = "";
			break;
		}
		return restrictionValue;
	}

	public void sortTiles(){
		if (shuffleArray.Any()){
			RectTransform tile = shuffleArray.First();
			tile.SetAsLastSibling();
			shuffleArray.Remove(tile);
			sortTiles();
		}
	}

	public void checkRewardCollected(int subMenu, int subEvent){
		string setPrize = "";
		if(EventData.offlinePrizes[subMenu,subEvent] == "AltPaint"){
			setPrize = EventData.offlineSetPrizes[subMenu,subEvent];
		} else {
			return;
		}
		
		//Win an alt paint rather than car parts
		//setPrize format example: cup20livery48alt2	
		string sanitisedAlt = setPrize.Replace("livery","");
		sanitisedAlt = sanitisedAlt.Replace("alt","Alt");
		
		//Sanitised example: cup2048Alt2Unlocked	
		string extractedCarNum = setPrize.Split('y').Last();
		string extractedAltNum = setPrize.Split('t').Last();
		
		extractedCarNum = extractedCarNum.Substring(0, extractedCarNum.IndexOf("alt")).Trim();
		int parsedNum = int.Parse(extractedCarNum);
		int parsedAlt = int.Parse(extractedAltNum);
		
		if(PlayerPrefs.GetInt(sanitisedAlt + "Unlocked") == 1){
			return;
		}

		PlayerPrefs.SetInt(sanitisedAlt + "Unlocked",1);
		if(AltPaints.cup2020AltPaintDriver[parsedNum,parsedAlt] != null){
			alertPopup.GetComponent<AlertManager>().showPopup("Unclaimed Rewards Collected","New " + AltPaints.cup2020AltPaintDriver[parsedNum,parsedAlt] + " Alt Paint Unlocked!", setPrize);
		} else {
			alertPopup.GetComponent<AlertManager>().showPopup("Unclaimed Rewards Collected","New " + DriverNames.cup2020Names[parsedNum] + " Alt Paint Unlocked!", setPrize);
		}
	}

	public void showEntryReqsPopup(int subMenu, int subEvent){
		entryReqsPopup.SetActive(true);
		reqListText = GameObject.Find("RequirementsList");
		reqListText.GetComponent<TMPro.TMP_Text>().text = getEntryReqs(subMenu,subEvent);
		//reqListText.GetComponent<TMPro.TMP_Text>().text = "Test";
	}

	public void closeEntryReqsPopup(){
		entryReqsPopup.SetActive(false);
	}

	public void showRewardsPopup(int subMenu, int subEvent){
		rewardsList.Clear();
		string rewardsCode = EventData.offlinePrizes[subMenu,subEvent];
		if(EventData.offlinePrizes[subMenu,subEvent] != "AltPaint"){
			//Generate the list of possible rewards
			Debug.Log("Show Rewards for: " + rewardsCode);
			rewardsList = EventData.ListRewards(rewardsCode);
		} else {
			//Throw the alt paint into the list instead
			rewardsList.Add(EventData.offlineSetPrizes[subMenu,subEvent]);
		}
		foreach(string reward in rewardsList){
			GameObject rewardInst = Instantiate(rewardCar, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
			rewardInst.transform.SetParent(rewardsGrid.GetComponent<RectTransform>(), false);
			RawImage carPaint = rewardInst.GetComponent<RawImage>();
			string rewardString;
			if(EventData.offlinePrizes[subMenu,subEvent] != "AltPaint"){
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
			PlayerPrefs.DeleteKey("ActivePath");
			SceneManager.LoadScene("MainMenu");
		} else {
			loadEvents();
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
