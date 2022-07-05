using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class EventsUI : MonoBehaviour
{
    public int week;
	string[] eventWeeks;
	public List<RectTransform> shuffleArray;
	public GameObject eventTile;
	public GameObject eventChildTile;
	public Transform tileFrame;
	string seriesPrefix;
    // Start is called before the first frame update
    void Start(){
		
		if(PlayerPrefs.HasKey("GameWeek")){
			//Get the last known cycle day
			week = PlayerPrefs.GetInt("GameWeek");
			//Debug.Log("Last known week: " + week);
		} else {
			//First login
			week = 1;
		}
		
		for(int i=0;i<4;i++){
			EventData.loadEvents();
			//Skip through the non-driver #s
			if(EventData.offlineEvent[i] == null){
				Debug.Log("No Event here: " + i);
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
				Debug.Log("Sort to end: " + eventName.text);
			} else {
				Debug.Log("Event is live: " + eventName.text);
			}
			sortCounter++;
		}
		
		sortTiles();
    }

	public void loadSubEvents(int subMenuId){
		foreach (Transform child in tileFrame){
			Destroy(child.gameObject);
		}
		for(int i=0;i<10;i++){
			//EventData.loadEvents();
			//Skip through the non-driver #s
			if(EventData.offlineEventChapter[subMenuId,i] == null){
				//Debug.Log("No Event here: " + i);
				continue;
			}
			
			GameObject tileInst = Instantiate(eventChildTile, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
			RectTransform tileObj = tileInst.GetComponent<RectTransform>();
			tileInst.transform.SetParent(tileFrame, false);
			tileInst.GetComponent<UIAnimate>().animOffset = i+1;
			tileInst.GetComponent<UIAnimate>().scaleIn();
			
			TMPro.TMP_Text eventName = tileInst.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
			RawImage eventImage = tileInst.transform.GetChild(1).GetComponent<RawImage>();
			TMPro.TMP_Text eventDesc = tileInst.transform.GetChild(2).GetComponent<TMPro.TMP_Text>();
			tileInst.transform.GetChild(3).GetComponent<EventUIFunctions>().subMenuId = subMenuId;
			tileInst.transform.GetChild(3).GetComponent<EventUIFunctions>().subEventId = i;
			
			eventName.text = EventData.offlineEventChapter[subMenuId,i];
			eventDesc.text = EventData.eventChapterDescriptions[subMenuId,i];
			eventImage.texture = Resources.Load<Texture2D>(EventData.offlineChapterImage[subMenuId,i]);
		}
	}

	public void showEventRewards(int subMenu, int subEvent){
	
	}

	public void sortTiles(){
		if (shuffleArray.Any()){
			RectTransform tile = shuffleArray.First();
			tile.SetAsLastSibling();
			shuffleArray.Remove(tile);
			sortTiles();
		}
	}

	public void closeRewardsPopup(){
		
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
