using UnityEngine;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;

public class CircuitEditorGUI : MonoBehaviour {

	public GUISkin eightBitSkin;
	
	float widthblock; 
	float heightblock;
	static string circuitName = "Custom Circuit";
	int groundTypeNum;
	string[] groundTypeStr = new string[] {"Grass","Dust","Dirt","Concrete"};
	int circuitTurns;
	static string[] circuitTurnAngle = new string[5];
	int [] circuitTurnLength = new int[4];
	string[] circuitTurnLengthName = new string[] {"Short","Mid","Long","X Long"};
	int[] circuitTurnLengthValues = new int[] {1,2,4,8};
	static string[] circuitStraight = new string[5];
	
	static string errorOutput = "";
	
	public Vector2 scrollPosition = Vector2.zero;
	public float hSliderValue = 0.0F;

	void Awake(){
		widthblock = Screen.width/20;
		heightblock = Screen.height/20;

		if(PlayerPrefs.HasKey("CustomCircuitGround")){
			groundTypeNum = System.Array.IndexOf(groundTypeStr, PlayerPrefs.GetString("CustomCircuitGround"));
		} else {
			PlayerPrefs.SetString("CustomCircuitGround","Grass");
			groundTypeNum = 0;
		}

		if(PlayerPrefs.HasKey("CustomCircuitName")){
			circuitName = PlayerPrefs.GetString("CustomCircuitName");
		}

		if(!PlayerPrefs.HasKey("CustomCircuitLanes")){
			PlayerPrefs.SetInt("CustomCircuitLanes",3);
		}

		if(!PlayerPrefs.HasKey("CustomCircuitLaps")){
			PlayerPrefs.SetInt("CustomCircuitLaps",8);
		}

		if(!PlayerPrefs.HasKey("CustomCircuitTurns")){
			PlayerPrefs.SetInt("CustomCircuitTurns",4);
		}

		for(int i = 1;i < 5;i++){
			if(PlayerPrefs.HasKey("CustomTurnAngle" + i.ToString())){
				circuitTurnAngle[i] = (PlayerPrefs.GetInt("CustomTurnAngle" + i.ToString())).ToString();
			} else {
				circuitTurnAngle[i] = "90";
			}
		}

		for(int i = 0;i < 4;i++){
			if(PlayerPrefs.HasKey("CustomTurnLength" + (i+1).ToString())){
				circuitTurnLength[i] = System.Array.IndexOf(circuitTurnLengthValues, PlayerPrefs.GetInt("CustomTurnLength" + (i+1).ToString()));
			} else {
				circuitTurnLength[i] = 2;
			}
		}

		for(int i = 1;i < 5;i++){
			if(PlayerPrefs.HasKey("CustomStraightLength" + i.ToString())){
				circuitStraight[i] = (PlayerPrefs.GetInt("CustomStraightLength" + i.ToString())).ToString();
			} else {
				circuitStraight[i] = "200";
			}
		}

	}
	
	void OnGUI() {

		GUI.skin = eightBitSkin;

		GUI.skin.button.fontSize = 64 / FontScale.fontScale;

		GUI.skin.label.fontSize = 72 / FontScale.fontScale;

		GUI.skin.textField.fontSize = 72 / FontScale.fontScale;

		GUI.skin.label.normal.textColor = Color.black;

		GUI.skin.verticalScrollbar.fixedWidth = Screen.width / 20;
		GUI.skin.verticalScrollbarThumb.fixedWidth = Screen.width / 20;
		
		scrollPosition = GUI.BeginScrollView(new Rect(0, 0, Screen.width, Screen.height), scrollPosition, new Rect(0, 0, Screen.width - widthblock-10, Screen.height * 2.8f));

		GUI.skin.label.alignment = TextAnchor.UpperCenter;
		
		GUI.Label(new Rect(widthblock * 4, heightblock/2, widthblock * 12, heightblock * 2), "Circuit Editor");

		GUI.skin.label.alignment = TextAnchor.UpperLeft;

		if (GUI.Button(new Rect(widthblock * 16.5f, heightblock * 0.5f, widthblock * 1.5f, heightblock * 1.5f), "Back")){
			SceneManager.LoadScene("MainMenu");
		}
		
		//---------------CIRCUIT NAME-----------------//

		GUI.Label(new Rect(widthblock * 2, heightblock * 4, widthblock * 14, heightblock * 2), "Circuit Name:");

		circuitName = GUI.TextField(new Rect(widthblock * 8, heightblock * 4, widthblock * 7, heightblock * 2),circuitName, 15);

		//---------------GROUND TYPE------------------//

		GUI.Label(new Rect(widthblock * 2, heightblock * 8, widthblock * 11, heightblock * 2), "Ground Type: " + groundTypeStr[groundTypeNum]);
		if (GUI.Button(new Rect(widthblock * 13, heightblock * 8, widthblock * 1, heightblock * 2), "<")){
			if(groundTypeNum < 3){
				groundTypeNum++;
				PlayerPrefs.SetString("CustomCircuitGround",groundTypeStr[groundTypeNum]);
			} else {
				groundTypeNum = 0;
				PlayerPrefs.SetString("CustomCircuitGround",groundTypeStr[groundTypeNum]);
			}
		}
		if (GUI.Button(new Rect(widthblock * 14, heightblock * 8, widthblock * 1, heightblock * 2), ">")){
			if(groundTypeNum > 0){
				groundTypeNum--;
				PlayerPrefs.SetString("CustomCircuitGround",groundTypeStr[groundTypeNum]);
			} else {
				groundTypeNum = 3;
				PlayerPrefs.SetString("CustomCircuitGround",groundTypeStr[groundTypeNum]);
			}
		}

		//---------------CIRCUIT LANES------------------//

		GUI.Label(new Rect(widthblock * 2, heightblock * 12, widthblock * 11, heightblock * 2), "Circuit Lanes: " + PlayerPrefs.GetInt("CustomCircuitLanes").ToString());
		if(PlayerPrefs.GetInt("CustomCircuitLanes") > 3 ){
			if (GUI.Button(new Rect(widthblock * 13, heightblock * 12, widthblock * 1, heightblock * 2), "<")){
				PlayerPrefs.SetInt("CustomCircuitLanes",PlayerPrefs.GetInt("CustomCircuitLanes") - 1);
			}
		}
		if(PlayerPrefs.GetInt("CustomCircuitLanes") < 4){
			if (GUI.Button(new Rect(widthblock * 14, heightblock * 12, widthblock * 1, heightblock * 2), ">")){
				PlayerPrefs.SetInt("CustomCircuitLanes",PlayerPrefs.GetInt("CustomCircuitLanes") + 1);
			}
		}

		//---------------CIRCUIT LAPS------------------//
		
		GUI.Label(new Rect(widthblock * 2, heightblock * 16, widthblock * 11, heightblock * 2), "Circuit Laps: " + PlayerPrefs.GetInt("CustomCircuitLaps").ToString());
		if(PlayerPrefs.GetInt("CustomCircuitLaps") > 3 ){
			if (GUI.Button(new Rect(widthblock * 13, heightblock * 16, widthblock * 1, heightblock * 2), "<")){
				PlayerPrefs.SetInt("CustomCircuitLaps",PlayerPrefs.GetInt("CustomCircuitLaps") - 1);
			}
		}
		if(PlayerPrefs.GetInt("CustomCircuitLaps") < 12){
			if (GUI.Button(new Rect(widthblock * 14, heightblock * 16, widthblock * 1, heightblock * 2), ">")){
				PlayerPrefs.SetInt("CustomCircuitLaps",PlayerPrefs.GetInt("CustomCircuitLaps") + 1);
			}
		}

		//---------------CIRCUIT TURNS------------------//
		
		GUI.Label(new Rect(widthblock * 2, heightblock * 20, widthblock * 11, heightblock * 2), "Circuit Turns: " + PlayerPrefs.GetInt("CustomCircuitTurns").ToString());
		if(PlayerPrefs.GetInt("CustomCircuitTurns") > 2){
			if (GUI.Button(new Rect(widthblock * 13, heightblock * 20, widthblock * 1, heightblock * 2), "<")){
				PlayerPrefs.SetInt("CustomCircuitTurns",PlayerPrefs.GetInt("CustomCircuitTurns") - 1);
			}
		}
		if(PlayerPrefs.GetInt("CustomCircuitTurns") < 4){
			if (GUI.Button(new Rect(widthblock * 14, heightblock * 20, widthblock * 1, heightblock * 2), ">")){
				PlayerPrefs.SetInt("CustomCircuitTurns",PlayerPrefs.GetInt("CustomCircuitTurns") + 1);
			}
		}

		//---------------CIRCUIT TURN LOOP------------------//

		for(int i = 0;i < PlayerPrefs.GetInt("CustomCircuitTurns");i++){
			GUI.Label(new Rect(widthblock * 2, heightblock * (24 +(i * 3)), widthblock * 4, heightblock * 2), "Angle " + (i+1).ToString());
			circuitTurnAngle[i + 1] = GUI.TextField(new Rect(widthblock * 6, heightblock * (24 +(i * 3)), widthblock * 2, heightblock * 2),circuitTurnAngle[i + 1], 3);
			circuitTurnAngle[i + 1] = Regex.Replace(circuitTurnAngle[i + 1], "[^0-9]", "0");
			GUI.Label(new Rect(widthblock * 9, heightblock * (24 +(i * 3)), widthblock * 7, heightblock * 2), "Length: " + circuitTurnLengthName[circuitTurnLength[i]]);
			if (GUI.Button(new Rect(widthblock * 16, heightblock * (24 +(i * 3)), widthblock * 1, heightblock * 2), "<")){
				if(circuitTurnLength[i] > 0){
					circuitTurnLength[i]--;
				} else {
					circuitTurnLength[i] = 3;
				}
			}
			if (GUI.Button(new Rect(widthblock * 17, heightblock * (24 +(i * 3)), widthblock * 1, heightblock * 2), ">")){
				if(circuitTurnLength[i] < 3){
					circuitTurnLength[i]++;
				} else {
					circuitTurnLength[i] = 0;
				}
			}
		}

		//---------------CIRCUIT STRAIGHT LOOP------------------//

		for(int i = 0;i < PlayerPrefs.GetInt("CustomCircuitTurns");i++){
			GUI.Label(new Rect(widthblock * 2, heightblock * (24 + (PlayerPrefs.GetInt("CustomCircuitTurns") * 3) + (i * 3)), widthblock * 11, heightblock * 2), "Straight Length " + (i+1).ToString());
			circuitStraight[i + 1] = GUI.TextField(new Rect(widthblock * 12, heightblock * (24 + (PlayerPrefs.GetInt("CustomCircuitTurns") * 3) +(i * 3)), widthblock * 2, heightblock * 2),circuitStraight[i + 1], 3);
			circuitStraight[i + 1] = Regex.Replace(circuitStraight[i + 1], "[^0-9]", "0");
		}

		if (GUI.Button(new Rect(widthblock * 2, heightblock * (24 + (PlayerPrefs.GetInt("CustomCircuitTurns") * 6)) + heightblock, widthblock * 4, heightblock * 2), "Save")){
			if(circuitValidation() == true){

				PlayerPrefs.SetString("CustomCircuitName",circuitName);

				PlayerPrefs.SetInt("CustomTurnAngle1",int.Parse(circuitTurnAngle[1]));
				PlayerPrefs.SetInt("CustomTurnAngle2",int.Parse(circuitTurnAngle[2]));
				PlayerPrefs.SetInt("CustomTurnAngle3",int.Parse(circuitTurnAngle[3]));
				PlayerPrefs.SetInt("CustomTurnAngle4",int.Parse(circuitTurnAngle[4]));

				PlayerPrefs.SetInt("CustomTurnLength1",circuitTurnLengthValues[circuitTurnLength[0]]);
				PlayerPrefs.SetInt("CustomTurnLength2",circuitTurnLengthValues[circuitTurnLength[1]]);
				PlayerPrefs.SetInt("CustomTurnLength3",circuitTurnLengthValues[circuitTurnLength[2]]);
				PlayerPrefs.SetInt("CustomTurnLength4",circuitTurnLengthValues[circuitTurnLength[3]]);

				PlayerPrefs.SetInt("CustomStraightLength1",int.Parse(circuitStraight[1]));
				PlayerPrefs.SetInt("CustomStraightLength2",int.Parse(circuitStraight[2]));
				PlayerPrefs.SetInt("CustomStraightLength3",int.Parse(circuitStraight[3]));
				PlayerPrefs.SetInt("CustomStraightLength4",int.Parse(circuitStraight[4]));

				PlayerPrefs.SetInt("CustomCircuitLaps",PlayerPrefs.GetInt("CustomCircuitLaps"));

				SceneManager.LoadScene("MainMenu");
			}
		}

		GUI.skin.label.normal.textColor = Color.red;
		GUI.skin.label.fontSize = 64 / FontScale.fontScale;
		GUI.Label(new Rect(widthblock * 7, heightblock * (24 + (PlayerPrefs.GetInt("CustomCircuitTurns") * 6)) + heightblock, widthblock * 11, heightblock * 10), errorOutput);
		GUI.skin.label.normal.textColor = Color.black;
		GUI.skin.label.fontSize = 72 / FontScale.fontScale;

		GUI.EndScrollView();

		if (Input.GetKeyDown(KeyCode.Escape)){
			SceneManager.LoadScene("MainMenu");
		}
	}

	public static bool circuitValidation(){
		errorOutput = "";
		bool validCircuit = true;
		int totalTurnAngle = 0;
		for (int i=1;i<=PlayerPrefs.GetInt("CustomCircuitTurns");i++){
			totalTurnAngle += int.Parse(circuitTurnAngle[i]);
		}

		if(totalTurnAngle != 360){
			errorOutput += "Error: Total turn angle must add up to 360 degrees\n\n";
			validCircuit = false;
		}
		if((int.Parse(circuitTurnAngle[1]) > 270)||(int.Parse(circuitTurnAngle[2]) > 270)||(int.Parse(circuitTurnAngle[3]) > 270)||(int.Parse(circuitTurnAngle[4]) > 270)){
			errorOutput += "Error: No angle can be more than 270 degrees\n\n";
			validCircuit = false;
		}
		int homeStraight = int.Parse(circuitStraight[1]);
		if(homeStraight == 0){
			errorOutput += "Error: The home straight must be longer than 0\n\n";
			validCircuit = false;
		}
		if(circuitName == ""){
			errorOutput += "Error: Circuit name must not be blank\n\n";
			validCircuit = false;
		}
		if(validCircuit == false){
			return false;
		}
		return true;
	}
}
