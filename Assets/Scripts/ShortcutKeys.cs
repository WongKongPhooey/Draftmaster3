using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShortcutKeys : MonoBehaviour
{
	public static bool hideGUI;
	public static bool cinematicAspect;
    // Start is called before the first frame update
    void Start()
    {
        hideGUI = false;
		cinematicAspect = false;
    }

    // Update is called once per frame
    void Update(){
		
        if (Input.GetKeyDown("H")){
            if(hideGUI == true){
				hideGUI = false;
			} else {
				hideGUI = true;
			}
        }
    }
	
	
}
