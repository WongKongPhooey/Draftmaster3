using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MiscUIFunctions : MonoBehaviour
{
	public string functionName;
    // Start is called before the first frame update
    void Start()
    {
		if(functionName == "Fuel"){
			showFuel();
		}
    }

	void showFuel(){
		TMPro.TMP_Text fuelText = this.transform.gameObject.GetComponent<TMPro.TMP_Text>();
		fuelText.text = GameData.gameFuel + "/" + GameData.maxFuel;
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
