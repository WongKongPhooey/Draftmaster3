using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Event", menuName = "Event")] 
public class Event : ScriptableObject {
			
	public int eventIndex;
	public string eventTitle;
	public string eventImage;
	public string eventDescription;
	public int eventAILevel;
	public string eventTracklist;
	public string eventCarset;
	public string eventCustomField;
	public string eventPrizeset;
	public string eventSpecificPrize;
	
	public int eventMinLevel;
	public int eventMinClass;
	
	public string eventRequirement;
	public string eventReqTeam;
	public string eventReqManufacturer;
	public string eventReqRarity;
	public string eventReqDriverType;
	public string eventReqExactCar;
}