using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SeriesUIFunctions : MonoBehaviour {
	
	public int seriesId;
	public int subSeriesId;
	public string modSeriesPrefix;
	//public static int subMenuIdInst;

	public void openSubMenu(){
		//Debug.Log(subMenuId + " opened");
		SeriesUI.seriesId = seriesId;
		GameObject.Find("Main").GetComponent<SeriesUI>().loadSubSeries(seriesId);
	}
	
	public void loadSeriesRequirements(){
		//Debug.Log("Loading Event Rewards for Event " + EventData.offlineEventChapter[subMenuId, subEventId]);
		SeriesUI.seriesId = seriesId;
		SeriesUI.subSeriesId = subSeriesId;
		GameObject.Find("Main").GetComponent<SeriesUI>().showEntryReqsPopup(seriesId, subSeriesId);
	}
	
	public void loadEventRewards(){
		//Debug.Log("Loading Event Rewards for Event " + EventData.offlineEventChapter[subMenuId, subEventId]);
		SeriesUI.seriesId = seriesId;
		SeriesUI.subSeriesId = subSeriesId;
		GameObject.Find("Main").GetComponent<SeriesUI>().showRewardsPopup(seriesId, subSeriesId);
	}
	
	public void loadLapsSlider(){
		//Debug.Log("Loading Event Rewards for Event " + EventData.offlineEventChapter[subMenuId, subEventId]);
		SeriesUI.seriesId = seriesId;
		SeriesUI.subSeriesId = subSeriesId;
		GameObject.Find("Main").GetComponent<SeriesUI>().showLapsPopup(seriesId, subSeriesId);
	}
	
	public void loadDifficultySlider(){
		//Debug.Log("Loading Event Rewards for Event " + EventData.offlineEventChapter[subMenuId, subEventId]);
		SeriesUI.seriesId = seriesId;
		SeriesUI.subSeriesId = subSeriesId;
		GameObject.Find("Main").GetComponent<SeriesUI>().showDifficultyPopup(seriesId, subSeriesId);
	}
	
	public void loadEvent(){
		SeriesUI.seriesId = seriesId;
		SeriesUI.subSeriesId = subSeriesId;
		SeriesUI.modSeriesPrefix = modSeriesPrefix;
		//The community mods category..
		if(seriesId == 10){
			Debug.Log("Load a mod series");
			GameObject.Find("Main").GetComponent<SeriesUI>().loadModSeries();
		} else {
			GameObject.Find("Main").GetComponent<SeriesUI>().loadSeries();
		}
	}
}

