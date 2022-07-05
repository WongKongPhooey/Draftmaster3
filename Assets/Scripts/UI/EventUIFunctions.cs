using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EventUIFunctions : MonoBehaviour
{
	public int subMenuId;
	public int subEventId;
	public static int subMenuIdInst;

	public void openSubMenu(){
		Debug.Log(subMenuId + " opened");
		GameObject.Find("Main").GetComponent<EventsUI>().loadSubEvents(subMenuId);
	}
	
	public void loadEventRewards(){
		GameObject.Find("RewardsPopup").GetComponent<EventsUI>().showEventRewards(subMenuId, subMenuId);
	}
	
	public void loadEvent(){
		Debug.Log("Load Event " + subMenuId + "," + subEventId + "");
		
		PlayerPrefs.SetString("SeriesTrackList",EventData.offlineTracklists[subMenuId,subEventId]);
		PlayerPrefs.SetString("CurrentSeriesIndex", subMenuId + "" + subEventId + "EVENT");
		PlayerPrefs.SetString("CurrentSeriesName",EventData.offlineEventChapter[subMenuId,subEventId]);
		PlayerPrefs.SetInt("CurrentSeries", subMenuId);
		PlayerPrefs.SetInt("CurrentSubseries", subEventId);
		PlayerPrefs.SetInt("SubseriesDailyPlays",999);
		PlayerPrefs.SetInt("SubseriesMinClass", EventData.offlineMinClass[subMenuId,subEventId]);
		PlayerPrefs.SetString("RestrictionType",EventData.offlineMinType[subMenuId,subEventId]);
		//PlayerPrefs.SetString("RestrictionValue",restrictionValue);
		PlayerPrefs.SetInt("AIDifficulty", EventData.offlineAILevel[subMenuId,subEventId]);
		PlayerPrefs.SetInt("SeriesFuel",5);
		PlayerPrefs.SetString("SeriesPrize",EventData.offlinePrizes[subMenuId,subEventId]);
		PlayerPrefs.SetString("SeriesPrizeAmt",EventData.offlineSetPrizes[subMenuId,subEventId]);
		PlayerPrefs.SetString("RaceType","Event");
		
		if(EventData.offlineSeries[subMenuId,subEventId] != null){
			PlayerPrefs.SetString("FixedSeries", EventData.offlineSeries[subMenuId,subEventId]);
		}
		if(EventData.offlineCustomField[subMenuId,subEventId] != null){
			PlayerPrefs.SetString("CustomField", EventData.offlineCustomField[subMenuId,subEventId]);
		}
		
		SceneManager.LoadScene("CarSelect");
	}
}
