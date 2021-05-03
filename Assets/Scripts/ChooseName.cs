using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class ChooseName : MonoBehaviour {

	public GUISkin eightBitSkin;

	float widthblock = Mathf.Round(Screen.width/20);
	float heightblock = Mathf.Round(Screen.height/20);

	public string firstName = "New";
	public string lastName = "Racer";

	// Use this for initialization
	void OnGUI() {

		GUI.skin = eightBitSkin;

		GUI.skin.label.fontSize = 72 / FontScale.fontScale;

		GUI.skin.textField.fontSize = 72 / FontScale.fontScale;

		GUI.skin.label.normal.textColor = Color.black;

		GUI.skin.label.alignment = TextAnchor.MiddleCenter;

		GUI.Label(new Rect(widthblock * 4, heightblock, widthblock * 12, heightblock * 3), "Enter Your Driver Name");

		GUI.skin.label.alignment = TextAnchor.MiddleRight;

		GUI.Label(new Rect(0, heightblock * 8, widthblock * 5, heightblock * 2), "First Name");

		firstName = GUI.TextField(new Rect(widthblock * 6, heightblock * 8, widthblock * 10, heightblock * 2), firstName, 10);

		GUI.Label(new Rect(0, heightblock * 12, widthblock * 5, heightblock * 2), "Last Name");

		lastName = GUI.TextField(new Rect(widthblock * 6, heightblock * 12, widthblock * 10, heightblock * 2), lastName, 10);
		
		if (GUI.Button(new Rect(widthblock * 12,heightblock * 16, widthblock * 4, heightblock * 2), "Continue")){
			PlayerPrefs.SetString("RacerName",firstName.Substring(0,1) + "." + lastName);
			SceneManager.LoadScene("Tutorial");
		}
	}
}
