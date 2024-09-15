using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ModCarsetFunctions : MonoBehaviour
{
	string seriesPrefix;
		
    // Start is called before the first frame update
    void Start(){
		seriesPrefix = this.gameObject.name;
    }
	
	public void setSeries(){
		GarageUI.seriesPrefix = seriesPrefix;
		GameObject.Find("Main").GetComponent<GarageUI>().reloadModCars();
		GameObject.Find("Main").GetComponent<GarageUI>().toggleDropdown(true);
	}
}
