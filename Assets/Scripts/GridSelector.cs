using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridSelector : MonoBehaviour{
	
	public string optionName;
	
    // Start is called before the first frame update
    void Start(){
        
    }

    // Update is called once per frame
    void Update(){
        
    }
	
	public void setDriverOption(){
		GameObject.Find("Main").GetComponent<GarageUI>().dropDriverToPool();
		this.gameObject.GetComponent<TMPro.TMP_Text>().color = new Color32(255,0,0,255);
		GameObject.Find("Main").GetComponent<GarageUI>().chosenOption = optionName;
		GameObject.Find("Main").GetComponent<GarageUI>().optionObject = this.gameObject;
	}
	
	public void setManufacturerOption(){
		GameObject.Find("Main").GetComponent<GarageUI>().dropDriverToPool();
		this.gameObject.GetComponent<TMPro.TMP_Text>().color = new Color32(255,0,0,255);
		GameObject.Find("Main").GetComponent<GarageUI>().chosenOption = optionName;
		GameObject.Find("Main").GetComponent<GarageUI>().optionObject = this.gameObject;
	}
	
	public void setTeamOption(){
		GameObject.Find("Main").GetComponent<GarageUI>().dropDriverToPool();
		this.gameObject.GetComponent<TMPro.TMP_Text>().color = new Color32(255,0,0,255);
		GameObject.Find("Main").GetComponent<GarageUI>().chosenOption = optionName;
		GameObject.Find("Main").GetComponent<GarageUI>().optionObject = this.gameObject;
	}
	
	public void setNumberOption(){
		GameObject.Find("Main").GetComponent<GarageUI>().dropDriverToPool();
		this.gameObject.GetComponent<TMPro.TMP_Text>().color = new Color32(255,0,0,255);
		GameObject.Find("Main").GetComponent<GarageUI>().chosenOption = optionName;
		GameObject.Find("Main").GetComponent<GarageUI>().optionObject = this.gameObject;
	}
}
