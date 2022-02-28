using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CommonUI : MonoBehaviour
{
	public string screenTitle;
	public string backLocation;
	public bool showMoney;
	public bool showGears;
	
    // Start is called before the first frame update
    void Start()
    {
        if(screenTitle != null){
			screenHeader();
		}
    }

    // Update is called once per frame
    void Update()
    {
        
    }
	
	void screenHeader(){
		
	}
}
