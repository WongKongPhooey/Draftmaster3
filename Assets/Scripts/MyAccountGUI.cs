using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MyAccountGUI : MonoBehaviour
{
	
	public GUISkin redGUI;
	
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
	
	void OnGUI()
	{
		GUI.skin = redGUI;
		GUI.skin.button.fontSize = 64 / FontScale.fontScale;
		
		CommonGUI.BackButton("MainMenu");
	}
}
