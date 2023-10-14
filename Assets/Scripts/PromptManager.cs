using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PromptManager : MonoBehaviour
{
	public static GameObject promptPopup;
	public static GameObject promptTitle;
	public static GameObject promptImage;
	public static GameObject promptText;
	public static GameObject promptInput;
	
    // Start is called before the first frame update
    void Awake(){
        promptPopup = GameObject.Find("PromptPopup");
		promptPopup.GetComponent<UIAnimate>().hide();
		promptTitle = GameObject.Find("PromptTitle");
		//Assume image is always a 2:1 Rectangle
		promptImage = GameObject.Find("PromptImage");
		promptText = GameObject.Find("PromptText");

		Scene currentScene = SceneManager.GetActiveScene ();
        string sceneName = currentScene.name;
    }

    void Start(){
        hidePopup();
    }
	
	public static void showPopup(){
		promptPopup.GetComponent<UIAnimate>().show();
		promptImage.GetComponent<UIAnimate>().scaleIn();
		promptText.GetComponent<UIAnimate>().scaleIn();
	}
	
	public static void hidePopup(){
		LeanTween.scale(promptPopup, new Vector3(0f,0f,0f), 0f);
		promptText.GetComponent<UIAnimate>().hide();
		promptImage.GetComponent<UIAnimate>().hide();
		promptPopup.GetComponent<UIAnimate>().hide();
	}
}
