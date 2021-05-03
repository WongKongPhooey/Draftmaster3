using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class CarEditorGUI : MonoBehaviour {

	public GUISkin eightBitSkin;
	public GameObject carModel;

	float widthblock = Mathf.Round(Screen.width/20);
	float heightblock = Mathf.Round(Screen.height/20);

	public static int currentCar;

	public Texture[] cars = new Texture[10];
	public Sprite[] numberDecals = new Sprite[10];

	void Awake(){
	}

	void Start(){

		carModel.GetComponent<MeshRenderer>().material.mainTexture = cars[currentCar];
	}
	
	void FixedUpdate(){
		carModel.transform.Rotate(0,1.0f,0);

		carModel.GetComponent<Renderer>().material.mainTexture = cars[currentCar];
	}
	
	void OnGUI() {
		
		GUI.skin = eightBitSkin;
		
		GUI.skin.label.fontSize = 144 / FontScale.fontScale;
		GUI.skin.button.fontSize = 128 / FontScale.fontScale;

		GUI.skin.label.normal.textColor = Color.black;

		GUI.skin.label.alignment = TextAnchor.MiddleCenter;
		GUI.Label(new Rect(0, heightblock/2, widthblock * 20, heightblock * 3), "Car Editor");
		GUI.skin.label.alignment = TextAnchor.UpperLeft;

		if (GUI.Button(new Rect(widthblock,heightblock * 5, widthblock * 2, heightblock * 2), "<")){
			if(currentCar >= 1){
				currentCar--;
			} else {
				currentCar = currentCar + 9;
			}

			while(cars[currentCar] == null){
				currentCar--;
			}
		}
		
		if (GUI.Button(new Rect(widthblock * 3 ,heightblock * 5, widthblock * 2, heightblock * 2), ">")){

			if(currentCar <= 8){
				currentCar++;
			} else {
				currentCar = currentCar - 9;
			}

			while(cars[currentCar] == null){
				currentCar++;
				if(currentCar > 9){
					currentCar = 0;
				}
			}
		}

		if (Input.GetKeyDown(KeyCode.Escape)){
			SceneManager.LoadScene("MainMenu");
		}
	}
}
