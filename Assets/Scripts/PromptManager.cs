using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PromptManager : MonoBehaviour
{
	public static GameObject promptPopup;
	
    // Start is called before the first frame update
    void Awake(){
    }
	
	public static void showPopup(GameObject prompt){
		promptPopup = prompt;
		prompt.GetComponent<UIAnimate>().show();
	}
	
	public static void hidePopup(){
		LeanTween.scale(promptPopup, new Vector3(0f,0f,0f), 0f);
		promptPopup.GetComponent<UIAnimate>().hide();
	}
}
