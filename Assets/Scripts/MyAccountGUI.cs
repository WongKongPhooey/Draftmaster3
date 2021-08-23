using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MyAccountGUI : MonoBehaviour
{
	
	public GUISkin redGUI;
	public Text playerName;
	
    // Start is called before the first frame update
    void Start()
    {
        playerName.text = PlayerPrefs.GetString("PlayerUsername");
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
