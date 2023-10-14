using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class AlertManager : MonoBehaviour
{
	public static GameObject alertPopup;
	public static GameObject alertTitle;
	public static GameObject alertImage;
	public static GameObject alertText;
	public static GameObject alertLabel;
	public static GameObject alertProgressBar;
	public static GameObject alertProgress;
    // Start is called before the first frame update
    void Awake(){
        alertPopup = GameObject.Find("AlertPopup");
		alertPopup.GetComponent<UIAnimate>().hide();
		alertTitle = GameObject.Find("AlertTitle");
		//Assume image is always a 2:1 Rectangle
		alertImage = GameObject.Find("AlertImage");
		alertText = GameObject.Find("AlertText");
		alertLabel = GameObject.Find("AlertLabel");
		alertProgressBar = GameObject.Find("AlertProgressBar");
		alertProgress = GameObject.Find("AlertProgress");

		Scene currentScene = SceneManager.GetActiveScene ();
        string sceneName = currentScene.name;
        if (sceneName == "MainMenu"){
			alertPopup = GameObject.Find("Main Camera").GetComponent<MainMenuUI>().alertPopup;
		}
    }
    
    void Start(){
        hidePopup();
    }

	public void showPopup(string title, string content, string image, bool progress = false, int progressCurrent = 0, int progressTarget = 999, Texture2D rawImage = null){
		alertTitle.GetComponent<TMPro.TMP_Text>().text = title;
		alertText.GetComponent<TMPro.TMP_Text>().text = content;
		if(content == ""){
			return;
		}
		if(progress == true){
			alertLabel.GetComponent<TMPro.TMP_Text>().text = progressCurrent + "/" + progressTarget;
			RectTransform alertProgressRect = alertProgress.GetComponent<RectTransform>();
			
			float alertProgressWidth = Mathf.Round((330 / progressTarget) * progressCurrent) + 1;
			if(alertProgressWidth > 330){
				alertProgressWidth = 330;
			}			
			alertProgressRect.sizeDelta = new Vector2(alertProgressWidth, 25);
			alertProgressBar.GetComponent<UIAnimate>().scaleIn();
		} else {
			alertProgressBar.GetComponent<UIAnimate>().hide();
		}
		//alertPopup.SetActive(true);
		alertPopup.GetComponent<UIAnimate>().show();
		if(rawImage != null){
			alertImage.GetComponent<RawImage>().texture = rawImage;
			alertImage.GetComponent<UIAnimate>().scaleIn();
		} else {
			if(image != ""){
				alertImage.GetComponent<RawImage>().texture = Resources.Load<Texture2D>(image);
				alertImage.GetComponent<UIAnimate>().scaleIn();
			}
		}
		alertText.GetComponent<UIAnimate>().scaleIn();
	}
	
	public void hidePopup(){
		//alertPopup = GameObject.Find("AlertPopup");
		LeanTween.scale(alertPopup, new Vector3(0f,0f,0f), 0f);
		alertText.GetComponent<UIAnimate>().hide();
		alertImage.GetComponent<UIAnimate>().hide();
		alertProgressBar.GetComponent<UIAnimate>().hide();
		alertPopup.GetComponent<UIAnimate>().hide();
	}
}
