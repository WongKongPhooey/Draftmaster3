using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GearsUI : MonoBehaviour
{
	int gears;
	int gearsVisual;
	int ticker;
    // Start is called before the first frame update
    void Start()
    {
		gears = PlayerPrefs.GetInt("Gears");
		gearsVisual = gears;
        updateGears();
    }

	void updateGears(){
		gears = PlayerPrefs.GetInt("Gears");
		this.transform.GetChild(0).GetComponent<TMPro.TMP_Text>().text = gearsVisual.ToString();
	}
	
    // Update is called once per frame
    void Update()
    {
		ticker++;
		if(ticker%10 == 0){
			updateGears();
		}
		if(gearsVisual > gears){
			if((gearsVisual - gears) > 100){
				gearsVisual-=10;
			} else {
				gearsVisual--;
			}
			updateGears();
		}
		if(gearsVisual < gears){
			if((gears - gearsVisual) > 100){
				gearsVisual+=10;
			} else {
				gearsVisual++;
			}
			updateGears();
		}
    }
}
