using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class BuyProVersion : MonoBehaviour {

	public GUISkin eightBitSkin;
		
	float widthblock = Mathf.Round(Screen.width/20);
	float heightblock = Mathf.Round(Screen.height/20);
	

	void OnGUI() {
		
		GUI.skin = eightBitSkin;

		GUI.skin.button.fontSize = 64 / FontScale.fontScale;

		GUI.skin.label.fontSize = 64 / FontScale.fontScale;

		GUI.skin.label.normal.textColor = Color.black;

		GUI.skin.label.alignment = TextAnchor.MiddleCenter;

		GUI.Label(new Rect(widthblock * 3, heightblock * 4, widthblock * 14, heightblock * 6), "This feature is only available on the Pro version of Draftmaster");
		if (GUI.Button(new Rect(widthblock * 4, heightblock * 14, widthblock * 5, heightblock * 2), "Download Now")){
			Application.OpenURL("https://play.google.com/store/apps/details?id=com.JoshWongCreations.DraftMasterProRelease&hl=en_GB");
		}
		if (GUI.Button(new Rect(widthblock * 11, heightblock * 14, widthblock * 5, heightblock * 2), "Back")){
			SceneManager.LoadScene("MainMenu");
		}
	}
}
