using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HUDFunctions : MonoBehaviour
{
	
	public GameObject HUDScreen1;
	public GameObject HUDScreen2;
	
    // Start is called before the first frame update
    void Start()
    {
		
    }

    // Update is called once per frame
    void Update()
    {
        
    }
	
	public void HUDScreenCycler(){
		if(HUDScreen1.activeSelf){
			HUDScreen1.SetActive(false);
			HUDScreen2.SetActive(true);
		} else {
			HUDScreen2.SetActive(false);
			HUDScreen1.SetActive(true);
		}
	}
}
