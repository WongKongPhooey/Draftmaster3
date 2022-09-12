using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AlertManager : MonoBehaviour
{
	public static GameObject alertPopup;
	public static GameObject alertTitle;
	public static GameObject alertImage;
	public static GameObject alertText;
    // Start is called before the first frame update
    void Start()
    {
        alertPopup = GameObject.Find("AlertPopup");
		alertPopup.GetComponent<UIAnimate>().hide();
		
		alertTitle = GameObject.Find("AlertTitle");
		//Assume image is always a 2:1 Rectangle
		alertImage = GameObject.Find("AlertImage");
		alertText = GameObject.Find("AlertText");
    }

	public static void showPopup(string title, string content, string image){
		alertTitle.GetComponent<TMPro.TMP_Text>().text = title;
		alertText.GetComponent<TMPro.TMP_Text>().text = content;
		if(content == ""){
			return;
		}
		alertPopup.GetComponent<UIAnimate>().show();
		if(image != ""){
			alertImage.GetComponent<RawImage>().texture = Resources.Load<Texture2D>(image);
			alertImage.GetComponent<UIAnimate>().scaleIn();
		}
		alertText.GetComponent<UIAnimate>().scaleIn();
	}
	public static void hidePopup(){
		alertText.GetComponent<UIAnimate>().hide();
		alertImage.GetComponent<UIAnimate>().hide();
		alertPopup.GetComponent<UIAnimate>().hide();
	}
}
