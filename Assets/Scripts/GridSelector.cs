using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GridSelector : MonoBehaviour{
	
	public string optionName;
	public string optionType;
	public string seriesPrefix;
	public int carNum;
	public int altNum;
	
    // Start is called before the first frame update
    void Start(){
        
    }

    // Update is called once per frame
    void Update(){
        
    }
	
	public void setDriverOption(){
		this.gameObject.GetComponent<TMPro.TMP_Text>().color = new Color32(255,0,0,255);
		GameObject mainUI = GameObject.Find("Main");
		mainUI.GetComponent<GarageUI>().chosenOption = optionName;
		mainUI.GetComponent<GarageUI>().optionType = optionType;
		mainUI.GetComponent<GarageUI>().optionObject = this.gameObject;
	}
	
	public void setManufacturerOption(){
		this.gameObject.GetComponent<RawImage>().color = new Color32(255,255,255,100);
		GameObject mainUI = GameObject.Find("Main");
		mainUI.GetComponent<GarageUI>().chosenOption = optionName;
		mainUI.GetComponent<GarageUI>().optionType = optionType;
		mainUI.GetComponent<GarageUI>().optionObject = this.gameObject;
	}
	
	public void setTeamOption(){
		this.gameObject.GetComponent<TMPro.TMP_Text>().color = new Color32(255,0,0,255);
		GameObject mainUI = GameObject.Find("Main");
		mainUI.GetComponent<GarageUI>().chosenOption = optionName;
		mainUI.GetComponent<GarageUI>().optionType = optionType;
		mainUI.GetComponent<GarageUI>().optionObject = this.gameObject;
	}
	
	public void setNumberOption(){
		this.gameObject.GetComponent<TMPro.TMP_Text>().color = new Color32(255,0,0,255);
		GameObject mainUI = GameObject.Find("Main");
		mainUI.GetComponent<GarageUI>().chosenOption = optionName;
		mainUI.GetComponent<GarageUI>().optionType = optionType;
		mainUI.GetComponent<GarageUI>().optionObject = this.gameObject;
	}

	public void setAltPaint(){
		if(altNum != 0){
			PlayerPrefs.SetInt(seriesPrefix + carNum + "AltPaint", altNum);
			if(AltPaints.getAltPaintDriver(seriesPrefix, carNum, altNum) != null){
				PlayerPrefs.SetString(seriesPrefix + carNum + "AltDriver", AltPaints.getAltPaintDriver(seriesPrefix, carNum, altNum));
			} else {
				PlayerPrefs.DeleteKey(seriesPrefix + carNum + "AltDriver");
			}
		} else {
			PlayerPrefs.DeleteKey(seriesPrefix + carNum + "AltPaint");
			PlayerPrefs.DeleteKey(seriesPrefix + carNum + "AltDriver");
		}
		//Debug.Log("Alt Paint Set: " + seriesPrefix + " " + carNum + " - #" + altNum);
		GameObject mainUI = GameObject.Find("Main");
		mainUI.GetComponent<GarageUI>().resetUI();
		mainUI.GetComponent<GarageUI>().reloadGaragePopupPaint();
	}
}
