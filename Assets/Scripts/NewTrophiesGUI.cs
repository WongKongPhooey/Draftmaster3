using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class NewTrophiesGUI : MonoBehaviour {

	public GUISkin eightBitSkin;
	
	float widthblock; 
	float heightblock;
	
	int moneyCount;
	
	public Vector2 scrollPosition;
	
	void Awake(){
		widthblock = Screen.width/20;
		heightblock = Screen.height/20;
		
		scrollPosition = Vector2.zero;
	}
	
	void OnGUI() {
		
		GUI.skin = eightBitSkin;
		
		GUI.DrawTexture(new Rect(widthblock, heightblock, widthblock * 4, heightblock * 4), Resources.Load("Trophy") as Texture, ScaleMode.ScaleToFit);
		
		GUI.DrawTexture(new Rect(widthblock * 5, heightblock, widthblock * 4, heightblock * 4), Resources.Load("WinAllRaces") as Texture, ScaleMode.ScaleToFit);
		
		GUI.DrawTexture(new Rect(widthblock * 9, heightblock, widthblock * 4, heightblock * 4), Resources.Load("BuyAll") as Texture, ScaleMode.ScaleToFit);
		
		GUI.DrawTexture(new Rect(widthblock, heightblock * 6, widthblock * 4, heightblock * 4), Resources.Load("CleanWin") as Texture, ScaleMode.ScaleToFit);
		
		GUI.DrawTexture(new Rect(widthblock * 5, heightblock * 6, widthblock * 4, heightblock * 4), Resources.Load("FirstLast") as Texture, ScaleMode.ScaleToFit);
		
		GUI.DrawTexture(new Rect(widthblock * 9, heightblock * 6, widthblock * 4, heightblock * 4), Resources.Load("PhotoFinish") as Texture, ScaleMode.ScaleToFit);
		
		if (GUI.Button(new Rect(widthblock * 15, heightblock * 17, widthblock * 4, heightblock * 2), "Next")){
			SceneManager.LoadScene("HomeScreen");
		}
	}
}
