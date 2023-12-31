using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GridSelector : MonoBehaviour{
	
	public string optionName;
	public string optionType;
	
    // Start is called before the first frame update
    void Start(){
        
    }

    // Update is called once per frame
    void Update(){
        
    }
	
	public void setDriverOption(){
		this.gameObject.GetComponent<TMPro.TMP_Text>().color = new Color32(255,0,0,255);
		GameObject.Find("Main").GetComponent<GarageUI>().chosenOption = optionName;
		GameObject.Find("Main").GetComponent<GarageUI>().optionType = optionType;
		GameObject.Find("Main").GetComponent<GarageUI>().optionObject = this.gameObject;
		GameObject.Find("Main").GetComponent<GarageUI>().dropOptionToPool();
	}
	
	public void setManufacturerOption(){
		this.gameObject.GetComponent<RawImage>().color = new Color32(255,255,255,100);
		GameObject.Find("Main").GetComponent<GarageUI>().chosenOption = optionName;
		GameObject.Find("Main").GetComponent<GarageUI>().optionType = optionType;
		GameObject.Find("Main").GetComponent<GarageUI>().optionObject = this.gameObject;
		GameObject.Find("Main").GetComponent<GarageUI>().dropOptionToPool();
	}
	
	public void setTeamOption(){
		this.gameObject.GetComponent<TMPro.TMP_Text>().color = new Color32(255,0,0,255);
		GameObject.Find("Main").GetComponent<GarageUI>().chosenOption = optionName;
		GameObject.Find("Main").GetComponent<GarageUI>().optionType = optionType;
		GameObject.Find("Main").GetComponent<GarageUI>().optionObject = this.gameObject;
		GameObject.Find("Main").GetComponent<GarageUI>().dropOptionToPool();
	}
	
	public void setNumberOption(){
		this.gameObject.GetComponent<TMPro.TMP_Text>().color = new Color32(255,0,0,255);
		GameObject.Find("Main").GetComponent<GarageUI>().chosenOption = optionName;
		GameObject.Find("Main").GetComponent<GarageUI>().optionType = optionType;
		GameObject.Find("Main").GetComponent<GarageUI>().optionObject = this.gameObject;
		GameObject.Find("Main").GetComponent<GarageUI>().dropOptionToPool();
	}
}
