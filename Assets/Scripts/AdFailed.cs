using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class AdFailed : MonoBehaviour {

	public GUISkin eightBitSkin;

	float widthblock = Mathf.Round(Screen.width/20);
	float heightblock = Mathf.Round(Screen.height/20);

	void OnGUI() {
		
		GUI.skin = eightBitSkin;

		GUI.skin.button.fontSize = 64 / FontScale.fontScale;

		GUI.skin.label.normal.textColor = Color.black;

		GUI.skin.label.alignment = TextAnchor.MiddleCenter;

		GUI.Label(new Rect(widthblock * 3, heightblock * 6, widthblock * 14, heightblock * 6), "Sorry! You must watch the advert completely to receive the prize money.");

		if (GUI.Button(new Rect(widthblock * 7, heightblock * 14, widthblock * 6, heightblock * 2), "Continue")){
			SceneManager.LoadScene("CarSelect");
		}

		if (Input.GetKeyDown(KeyCode.Escape)){
			SceneManager.LoadScene("CarSelect");
		}
	}
}
