using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EventUIFunctions : MonoBehaviour
{
	public int subMenuId;
	public int subEventId;
	//public static int subMenuIdInst;

	public void openSubMenu(){
		//Debug.Log(subMenuId + " opened");
		EventsUI.subMenuId = subMenuId;
		GameObject.Find("Main").GetComponent<EventsUI>().loadSubEvents(subMenuId);
	}
	
	public void loadEventRequirements(){
		//Debug.Log("Loading Event Rewards for Event " + EventData.offlineEventChapter[subMenuId, subEventId]);
		GameObject.Find("Main").GetComponent<EventsUI>().showEntryReqsPopup(subMenuId, subEventId);
	}
	
	public void loadEventRewards(){
		//Debug.Log("Loading Event Rewards for Event " + EventData.offlineEventChapter[subMenuId, subEventId]);
		GameObject.Find("Main").GetComponent<EventsUI>().showRewardsPopup(subMenuId, subEventId);
	}
	
	public void loadEvent(){
		EventsUI.subMenuId = subMenuId;
		EventsUI.subEventId = subEventId;
		GameObject.Find("Main").GetComponent<EventsUI>().loadEvent();
	}
}
