using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FuelUI : MonoBehaviour
{
	int fuel;
	int maxFuel;
	int fuelVisual;
	int ticker;
    // Start is called before the first frame update
    void Start()
    {
        fuel = PlayerPrefs.GetInt("GameFuel");
		maxFuel = GameData.maxFuel;
		fuelVisual = fuel;
        updateFuel();
    }

	void updateFuel(){
		fuel = PlayerPrefs.GetInt("GameFuel");
		this.transform.GetChild(0).GetComponent<TMPro.TMP_Text>().text = fuelVisual.ToString() + "/" + maxFuel;
    }

    // Update is called once per frame
    void Update()
    {
		ticker++;
		if(ticker%10 == 0){
			updateFuel();
		}
		if(fuelVisual > fuel){
			fuelVisual--;
			updateFuel();
		}
		if(fuelVisual < fuel){
			fuelVisual++;
			updateFuel();
		}
	}
}
