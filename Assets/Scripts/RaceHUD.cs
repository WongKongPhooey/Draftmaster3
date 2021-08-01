using UnityEngine;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;

public class RaceHUD : MonoBehaviour {

	public float widthblock;
	public float heightblock;
	public static bool gamePaused;

	public GameObject raceCam;

	public static bool caution = false;
	public static bool goingGreen = false;
	public static bool raceOver = false;
	
	public GUISkin eightBitSkin;
	public GameObject thePlayer;
	public int player2Num;

	public float engineVol;
	public float crowdVol;
	public static int tutorialStage = 1;
	public static int tutorialSteeringCount;
	public static int tutorialBrakingCount;
	public static int tutorialDraftingCount;
	public static int tutorialBackdraftCount;
	public static int tutorialSteeringMax = 3;
	public static int tutorialBrakingMax = 1;
	public static int tutorialDraftingMax = 500;
	public static int tutorialBackdraftMax = 100;
	public static bool tutorialTopSpeed = false;
	public static bool tutorialBackingOut = false;
	
	
	void Start(){
		widthblock = Screen.width/20;
		heightblock = Screen.height/20;
		thePlayer = thePlayer = GameObject.Find("Player");
		tutorialSteeringCount = 0;
		tutorialBrakingCount = 0;
		tutorialDraftingCount = 0;
		tutorialBackdraftCount = 0;
		gamePaused = false;
		tutorialBackingOut = false;
	}
	
	
	void Awake(){
		tutorialStage = 1;
		gamePaused = false;
		tutorialBackingOut = false;
	}
	
	void FixedUpdate(){
		if (tutorialSteeringCount >= tutorialSteeringMax){
			tutorialStage++;
			tutorialSteeringCount = 0;
		}
		if (tutorialBrakingCount >= tutorialBrakingMax){
			tutorialStage++;
			tutorialBrakingCount = 0;
		}
		if (tutorialDraftingCount >= tutorialDraftingMax){
			tutorialStage++;
			tutorialDraftingCount = 0;
		}
		if (tutorialBackdraftCount >= tutorialBackdraftMax){
			tutorialStage++;
			tutorialBackdraftCount = 0;
		}
		if (tutorialStage == 5){
			Time.timeScale = 0.0f;
			raceCam.GetComponent<AudioListener>().enabled = false;
			PlayerPrefs.SetInt("Volume",0);
		}
	}
	
	void OnGUI() {

		if(FontScale.fontScale == 0){
			SceneManager.LoadScene("MainMenu");
		}

		GUI.skin = eightBitSkin;

		GUI.skin.label.fontSize = 80 / FontScale.fontScale;
		GUI.skin.button.fontSize = 120 / FontScale.fontScale;
		
		//-------CONTROLS--------

		if (Input.GetKeyDown(KeyCode.Escape)){
			if(raceOver == false){
				gamePaused = true;
				Time.timeScale = 0.0f;
				raceCam.GetComponent<AudioListener>().enabled = false;
				PlayerPrefs.SetInt("Volume",0);
			}
		}
		if (GUI.Button(new Rect(10, 10, widthblock, widthblock), "||")){
			if(raceOver == false){
				gamePaused = true;
				Time.timeScale = 0.0f;
				raceCam.GetComponent<AudioListener>().enabled = false;
				PlayerPrefs.SetInt("Volume",0);
			}
		}

		if(gamePaused == true){
			if (GUI.Button(new Rect(widthblock * 3, heightblock * 8, widthblock * 6, heightblock * 4), "Resume")){
				gamePaused = false;
				Time.timeScale = 1.0f;
				raceCam.GetComponent<AudioListener>().enabled = true;
				PlayerPrefs.SetInt("Volume",1);
			}
			if (GUI.Button(new Rect(widthblock * 11, heightblock * 8, widthblock * 6, heightblock * 4), "Quit")){
				SceneManager.LoadScene("MainMenu");
				gamePaused = false;
				Time.timeScale = 1.0f;
				raceCam.GetComponent<AudioListener>().enabled = true;
				PlayerPrefs.SetInt("Volume",1);
			}
		}

		if((raceOver == false)&&(caution == false)){
			if(CameraRotate.lap != 0){
				if((gamePaused == false)&&(tutorialStage != 5)){
					if(PlayerPrefs.GetInt("Local2Player") == 1){
						if (GUI.Button(new Rect(10, Screen.height - (heightblock * 2.5f) - 10, widthblock * 2.5f, heightblock * 2.5f), "<")){
							if((Movement.laneticker == 0)&&(Movement.lane < PlayerPrefs.GetInt("CircuitLanes"))){
								Movement.lane++;
								Movement.laneticker = Movement.laneChangeDuration;
								if(PlayerPrefs.GetInt("TutorialActive") == 1){
									if(tutorialStage == 1){
										tutorialBackingOut = false;
									}
								}
							}
						}
						if (GUI.Button(new Rect((widthblock * 5) + 10, Screen.height - (heightblock * 2.5f) - 10, widthblock * 2.5f, heightblock * 2.5f), ">")){
							if((Movement.laneticker == 0)&&(Movement.lane > 1)){
								Movement.lane--;
								Movement.laneticker = -Movement.laneChangeDuration;
								if(PlayerPrefs.GetInt("TutorialActive") == 1){
									if(tutorialStage == 1){
										tutorialBackingOut = false;
									}
								}
							}
						}
						if (GUI.Button(new Rect((widthblock * 2.5f) + 10, Screen.height - (heightblock * 2.5f) - 10, widthblock * 2.5f, heightblock * 2.5f), "Brake")){
							if(Movement.playerSpeed > 195){
								Movement.playerSpeed-=0.99f;
								if(tutorialStage == 2){
									tutorialBrakingCount++;
								}
							}
						}
						if (GUI.Button(new Rect(Screen.width - (widthblock * 7.5f) - 10, Screen.height - (heightblock * 2.5f) - 10, widthblock * 2.5f, heightblock * 2.5f), "<")){
							player2Num = PlayerPrefs.GetInt("Player2Num");
							Debug.Log("Player 2: Car " + player2Num);
							//GameObject.Find("AICar0" + player2Num).GetComponent<AIMovement>().MultiplayerChangeLane("Left");
						}
						if (GUI.Button(new Rect(Screen.width - (widthblock * 5) - 10, Screen.height - (heightblock * 2.5f) - 10, widthblock * 2.5f, heightblock * 2.5f), "Brake")){
							player2Num = PlayerPrefs.GetInt("Player2Num");
							Debug.Log("Player 2: Car " + player2Num);
							GameObject.Find("AICar0" + player2Num).GetComponent<AIMovement>().AISpeed-=0.99f;
						}
						if (GUI.Button(new Rect(Screen.width - (widthblock * 2.5f) - 10, Screen.height - (heightblock * 2.5f) - 10, widthblock * 2.5f, heightblock * 2.5f), ">")){
							player2Num = PlayerPrefs.GetInt("Player2Num");
							Debug.Log("Player 2: Car " + player2Num);
							//GameObject.Find("AICar0" + player2Num).GetComponent<AIMovement>().MultiplayerChangeLane("Right");
						}
					} else {
						if (GUI.Button(new Rect(10, Screen.height - (heightblock * 4) - 10, widthblock * 4, heightblock * 4), "<")){
							//if((Movement.laneticker == 0)&&(Movement.lane < PlayerPrefs.GetInt("CircuitLanes"))){
							if(Movement.laneticker == 0){
								Movement.lane++;
								Movement.laneticker = Movement.laneChangeDuration;
								if(PlayerPrefs.GetInt("TutorialActive") == 1){
									if(tutorialStage == 1){
										tutorialBackingOut = false;
									}
								}
							}
						}
						if (GUI.Button(new Rect(Screen.width - (widthblock * 4) - 10, Screen.height - (heightblock * 4) - 10, widthblock * 4, heightblock * 4), ">")){
							//if((Movement.laneticker == 0)&&(Movement.lane > 1)){
							if(Movement.laneticker == 0){
								Movement.lane--;
								Movement.laneticker = -Movement.laneChangeDuration;
								if(PlayerPrefs.GetInt("TutorialActive") == 1){
									if(tutorialStage == 1){
										tutorialBackingOut = false;
									}
								}
							}
						}
						if (GUI.Button(new Rect(widthblock*6, Screen.height - (heightblock * 4) - 10, widthblock * 8, heightblock * 4), "Brake")){
							if(Movement.playerSpeed > 195){
								Movement.playerSpeed-=0.99f;
								if(tutorialStage == 2){
									tutorialBrakingCount++;
								}
							}
						}
					}
				}
				if((Movement.canBuddy == true)&&(Movement.buddyUp == false)){
					string BuddyNum = getCarNum(Movement.draftBuddy);
					GUI.skin.button.fontSize = 48 / FontScale.fontScale;
					if (GUI.Button(new Rect(10, Screen.height - (heightblock * 6) - 20, widthblock * 4, heightblock * 2), "Team Up With #" + BuddyNum + "?")){
						Movement.buddyUp = true;
						Movement.buddyMax = Random.Range(5000,20000);
					}
					GUI.skin.button.fontSize = 120 / FontScale.fontScale;
				} else {
					if(Movement.buddyUp == true){
						GUI.skin.label.normal.textColor = Color.black;
						string BuddyNum = getCarNum(Movement.draftBuddy);
						GUI.skin.label.fontSize = 48 / FontScale.fontScale;
						GUI.skin.label.alignment = TextAnchor.LowerLeft;
						thiccLabel(10, Screen.height - (heightblock * 8), widthblock * 5, heightblock * 3, "Teamed Up (#" + BuddyNum + ")", Color.green);
						GUI.skin.label.fontSize = 80 / FontScale.fontScale;
						GUI.skin.label.alignment = TextAnchor.UpperLeft;
					}
				}
				if(Movement.mostHits > 2){
					string rivalsList = "";
					for(int r = 0; r < thePlayer.GetComponent<Movement>().rivals.Count; r++){
						PlayerPrefs.SetInt("TotalRivals",thePlayer.GetComponent<Movement>().rivals.Count);
						string rivalNum = getCarNum(thePlayer.GetComponent<Movement>().rivals[r]);
						if(rivalsList == ""){
							rivalsList = rivalsList + "#" + rivalNum;
						} else {
							rivalsList = rivalsList + ", #" + rivalNum;
						}
					}
					GUI.skin.label.alignment = TextAnchor.LowerRight;
					GUI.skin.label.fontSize = 48 / FontScale.fontScale;
					thiccLabel((widthblock * 13)-10, Screen.height - (heightblock * 10), widthblock * 7, heightblock * 5, "Rivals " + rivalsList, Color.red);
					GUI.skin.label.fontSize = 80 / FontScale.fontScale;
					//GUI.Label(new Rect((widthblock * 15)-10, Screen.height - (heightblock * 7), widthblock * 4, heightblock * 2), "Rivals " + rivalsList);
					GUI.skin.label.alignment = TextAnchor.UpperLeft;
				}
			}
		} else {
			if(raceOver == true){
				if((ChallengeSelectGUI.challengeMode == true)&&(PlayerPrefs.GetString("ChallengeType")=="LastToFirstLaps")){
					GUI.skin.label.fontSize = 256 / FontScale.fontScale;
					GUI.skin.label.normal.textColor = Color.black;
					GUI.Label(new Rect(widthblock + (8 / (FontScale.fontScale * 2)), Screen.height - (heightblock * 9) + (8 / (FontScale.fontScale * 2)), widthblock * 12, heightblock * 8), (CameraRotate.lap - 185) + " LAPS");
					GUI.Label(new Rect(widthblock - (8 / (FontScale.fontScale * 2)), Screen.height - (heightblock * 9) + (8 / (FontScale.fontScale * 2)), widthblock * 12, heightblock * 8), (CameraRotate.lap - 185) + " LAPS");
					GUI.Label(new Rect(widthblock + (8 / (FontScale.fontScale * 2)), Screen.height - (heightblock * 9) - (8 / (FontScale.fontScale * 2)), widthblock * 12, heightblock * 8), (CameraRotate.lap - 185) + " LAPS");
					GUI.Label(new Rect(widthblock - (8 / (FontScale.fontScale * 2)), Screen.height - (heightblock * 9) - (8 / (FontScale.fontScale * 2)), widthblock * 12, heightblock * 8), (CameraRotate.lap - 185) + " LAPS");
					GUI.skin.label.normal.textColor = Color.yellow;
					GUI.Label(new Rect(widthblock , Screen.height - (heightblock * 9), widthblock * 12, heightblock * 8), (CameraRotate.lap - 187) + " LAPS");
				} else {
					GUI.skin.label.alignment = TextAnchor.MiddleLeft;
					if((ChallengeSelectGUI.challengeMode == true)&&(PlayerPrefs.GetString("ChallengeType")=="TrafficJam")){
						GUI.skin.label.fontSize = 256 / FontScale.fontScale;
						GUI.skin.label.normal.textColor = Color.black;
						GUI.Label(new Rect(widthblock + (8 / (FontScale.fontScale * 2)), Screen.height - (heightblock * 9) + (8 / (FontScale.fontScale * 2)), widthblock * 12, heightblock * 8), (CameraRotate.lap - 148) + " LAPS");
						GUI.Label(new Rect(widthblock - (8 / (FontScale.fontScale * 2)), Screen.height - (heightblock * 9) + (8 / (FontScale.fontScale * 2)), widthblock * 12, heightblock * 8), (CameraRotate.lap - 148) + " LAPS");
						GUI.Label(new Rect(widthblock + (8 / (FontScale.fontScale * 2)), Screen.height - (heightblock * 9) - (8 / (FontScale.fontScale * 2)), widthblock * 12, heightblock * 8), (CameraRotate.lap - 148) + " LAPS");
						GUI.Label(new Rect(widthblock - (8 / (FontScale.fontScale * 2)), Screen.height - (heightblock * 9) - (8 / (FontScale.fontScale * 2)), widthblock * 12, heightblock * 8), (CameraRotate.lap - 148) + " LAPS");
						GUI.skin.label.normal.textColor = Color.yellow;
						GUI.Label(new Rect(widthblock , Screen.height - (heightblock * 9), widthblock * 12, heightblock * 8), (CameraRotate.lap - 150) + " LAPS");
					} else {
						GUI.skin.label.fontSize = 512 / FontScale.fontScale;
						GUI.skin.label.normal.textColor = Color.black;
						GUI.Label(new Rect(widthblock + (8 / (FontScale.fontScale * 2)), Screen.height - (heightblock * 9) + (8 / (FontScale.fontScale * 2)), widthblock * 12, heightblock * 8), "P" + (Scoreboard.position + 1));
						GUI.Label(new Rect(widthblock - (8 / (FontScale.fontScale * 2)), Screen.height - (heightblock * 9) + (8 / (FontScale.fontScale * 2)), widthblock * 12, heightblock * 8), "P" + (Scoreboard.position + 1));
						GUI.Label(new Rect(widthblock + (8 / (FontScale.fontScale * 2)), Screen.height - (heightblock * 9) - (8 / (FontScale.fontScale * 2)), widthblock * 12, heightblock * 8), "P" + (Scoreboard.position + 1));
						GUI.Label(new Rect(widthblock - (8 / (FontScale.fontScale * 2)), Screen.height - (heightblock * 9) - (8 / (FontScale.fontScale * 2)), widthblock * 12, heightblock * 8), "P" + (Scoreboard.position + 1));
						GUI.skin.label.normal.textColor = Color.yellow;
						GUI.Label(new Rect(widthblock , Screen.height - (heightblock * 9), widthblock * 12, heightblock * 8), "P" + (Scoreboard.position + 1));
					}
				}
				GUI.skin.label.fontSize = 80 / FontScale.fontScale;
				
				if (GUI.Button(new Rect(widthblock * 13, Screen.height - (heightblock * 6), widthblock * 6, heightblock * 4), "Results")){
					Time.timeScale = 1.0f;
					raceOver = false;
					SceneManager.LoadScene("RaceResults");
				}
			} else {
				if (GUI.Button(new Rect(widthblock * 13, Screen.height - (heightblock * 6), widthblock * 6, heightblock * 4), "Continue")){
					Time.timeScale = 1.0f;
					caution = false;
					string circuitReload = PlayerPrefs.GetString("CurrentCircuit");
					//PlayerPrefs.SetInt("ActiveCaution",0);
					SceneManager.LoadScene(circuitReload);
				}
			}
		}
		

		if(PlayerPrefs.GetString("ChallengeType")=="TeamPlayer"){
			GUI.skin.label.fontSize = 56 / FontScale.fontScale;
			GUI.skin.label.normal.textColor = Color.black;
			GUI.Label(new Rect(10 + (8 / (FontScale.fontScale * 2)), (heightblock * 2) + 10 + (8 / (FontScale.fontScale * 2)), widthblock * 7, heightblock * 2), "Pushing The 2: " + Movement.draftPercent.ToString("F0") + "%");
			GUI.Label(new Rect(10 + (8 / (FontScale.fontScale * 2)), (heightblock * 2) + 10 - (8 / (FontScale.fontScale * 2)), widthblock * 7, heightblock * 2), "Pushing The 2: " + Movement.draftPercent.ToString("F0") + "%");
			GUI.Label(new Rect(10 - (8 / (FontScale.fontScale * 2)), (heightblock * 2) + 10 + (8 / (FontScale.fontScale * 2)), widthblock * 7, heightblock * 2), "Pushing The 2: " + Movement.draftPercent.ToString("F0") + "%");
			GUI.Label(new Rect(10 - (8 / (FontScale.fontScale * 2)), (heightblock * 2) + 10 - (8 / (FontScale.fontScale * 2)), widthblock * 7, heightblock * 2), "Pushing The 2: " + Movement.draftPercent.ToString("F0") + "%");
			if(Movement.draftPercent > 30){
				GUI.skin.label.normal.textColor = Color.green;
			} else {
				if(Movement.pushingPartner == true){
					GUI.skin.label.normal.textColor = Color.yellow;
				} else {
					GUI.skin.label.normal.textColor = Color.red;
				}
			}
			GUI.Label(new Rect(10,(heightblock * 2) + 10, widthblock * 7, heightblock * 3), "Pushing The 2: " + Movement.draftPercent.ToString("F0") + "%");
			GUI.skin.label.fontSize = 80 / FontScale.fontScale;
		}
		
		if(CameraRotate.lap == 0){
			GUI.skin.label.normal.textColor = Color.yellow;
			GUI.skin.label.alignment = TextAnchor.MiddleCenter;
			GUI.skin.label.fontSize = 120 / FontScale.fontScale;
			GUI.skin.label.normal.textColor = Color.black;
			GUI.Label(new Rect((widthblock * 5) + (8 / (FontScale.fontScale * 2)),(heightblock * 3) + (8 / (FontScale.fontScale * 2)), widthblock * 10, heightblock * 4), "GET READY");
			GUI.Label(new Rect((widthblock * 5) + (8 / (FontScale.fontScale * 2)),(heightblock * 3) - (8 / (FontScale.fontScale * 2)), widthblock * 10, heightblock * 4), "GET READY");
			GUI.Label(new Rect((widthblock * 5) - (8 / (FontScale.fontScale * 2)),(heightblock * 3) + (8 / (FontScale.fontScale * 2)), widthblock * 10, heightblock * 4), "GET READY");
			GUI.Label(new Rect((widthblock * 5) - (8 / (FontScale.fontScale * 2)),(heightblock * 3) - (8 / (FontScale.fontScale * 2)), widthblock * 10, heightblock * 4), "GET READY");
			GUI.skin.label.normal.textColor = Color.yellow;
			GUI.Label(new Rect(widthblock * 5, heightblock * 3, widthblock * 10, heightblock * 4), "GET READY");
			GUI.skin.label.fontSize = 80 / FontScale.fontScale;
			GUI.skin.label.normal.textColor = Color.yellow;
		}

		if(goingGreen == true){
			GUI.skin.label.normal.textColor = Color.yellow;
			GUI.skin.label.alignment = TextAnchor.MiddleCenter;
			GUI.skin.label.fontSize = 120 / FontScale.fontScale;
			GUI.skin.label.normal.textColor = Color.black;
			GUI.Label(new Rect((widthblock * 5) + (8 / (FontScale.fontScale * 2)),(heightblock * 3) + (8 / (FontScale.fontScale * 2)), widthblock * 10, heightblock * 4), "GOING GREEN");
			GUI.Label(new Rect((widthblock * 5) + (8 / (FontScale.fontScale * 2)),(heightblock * 3) - (8 / (FontScale.fontScale * 2)), widthblock * 10, heightblock * 4), "GOING GREEN");
			GUI.Label(new Rect((widthblock * 5) - (8 / (FontScale.fontScale * 2)),(heightblock * 3) + (8 / (FontScale.fontScale * 2)), widthblock * 10, heightblock * 4), "GOING GREEN");
			GUI.Label(new Rect((widthblock * 5) - (8 / (FontScale.fontScale * 2)),(heightblock * 3) - (8 / (FontScale.fontScale * 2)), widthblock * 10, heightblock * 4), "GOING GREEN");
			GUI.skin.label.normal.textColor = Color.yellow;
			GUI.Label(new Rect(widthblock * 5, heightblock * 3, widthblock * 10, heightblock * 4), "GOING GREEN");
			GUI.skin.label.fontSize = 80 / FontScale.fontScale;
			GUI.skin.label.normal.textColor = Color.yellow;
		}
		if(caution == false){
			if(((CameraRotate.lap == 1)||(CameraRotate.lap == CameraRotate.restartLap))&&(CameraRotate.straight == 1)&&(CameraRotate.straightcounter >= PlayerPrefs.GetInt("StartLine"))){
				GUI.skin.label.alignment = TextAnchor.MiddleCenter;
				GUI.skin.label.fontSize = 120 / FontScale.fontScale;
				GUI.skin.label.normal.textColor = Color.black;
				GUI.Label(new Rect((widthblock * 5) + (8 / (FontScale.fontScale * 2)),(heightblock * 3) + (8 / (FontScale.fontScale * 2)), widthblock * 10, heightblock * 4), "GO! GO! GO!");
				GUI.Label(new Rect((widthblock * 5) + (8 / (FontScale.fontScale * 2)),(heightblock * 3) - (8 / (FontScale.fontScale * 2)), widthblock * 10, heightblock * 4), "GO! GO! GO!");
				GUI.Label(new Rect((widthblock * 5) - (8 / (FontScale.fontScale * 2)),(heightblock * 3) + (8 / (FontScale.fontScale * 2)), widthblock * 10, heightblock * 4), "GO! GO! GO!");
				GUI.Label(new Rect((widthblock * 5) - (8 / (FontScale.fontScale * 2)),(heightblock * 3) - (8 / (FontScale.fontScale * 2)), widthblock * 10, heightblock * 4), "GO! GO! GO!");
				GUI.skin.label.normal.textColor = Color.green;
				GUI.Label(new Rect(widthblock * 5, heightblock * 3, widthblock * 10, heightblock * 4), "GO! GO! GO!");
				GUI.skin.label.fontSize = 80 / FontScale.fontScale;
				GUI.skin.label.normal.textColor = Color.yellow;
			}
		}

		if(PlayerPrefs.GetInt("TutorialActive") == 1){
			if(tutorialStage == 1){
				GUI.skin.label.alignment = TextAnchor.MiddleCenter;
				GUI.skin.label.fontSize = 80 / FontScale.fontScale;
				GUI.skin.label.normal.textColor = Color.black;
				if (tutorialBackingOut == true) {
					GUI.Label(new Rect((widthblock * 3) + (8 / (FontScale.fontScale * 2)),(heightblock * 10) + (8 / (FontScale.fontScale * 2)), widthblock * 14, heightblock * 5), "CONTACT WILL SLOW YOU DOWN AND BACK YOU OUT OF A MOVE. WAIT FOR A GAP! (" + tutorialSteeringCount + "/3)");
					GUI.Label(new Rect((widthblock * 3) + (8 / (FontScale.fontScale * 2)),(heightblock * 10) - (8 / (FontScale.fontScale * 2)), widthblock * 14, heightblock * 5), "CONTACT WILL SLOW YOU DOWN AND BACK YOU OUT OF A MOVE. WAIT FOR A GAP! (" + tutorialSteeringCount + "/3)");
					GUI.Label(new Rect((widthblock * 3) - (8 / (FontScale.fontScale * 2)),(heightblock * 10) + (8 / (FontScale.fontScale * 2)), widthblock * 14, heightblock * 5), "CONTACT WILL SLOW YOU DOWN AND BACK YOU OUT OF A MOVE. WAIT FOR A GAP! (" + tutorialSteeringCount + "/3)");
					GUI.Label(new Rect((widthblock * 3) - (8 / (FontScale.fontScale * 2)),(heightblock * 10) - (8 / (FontScale.fontScale * 2)), widthblock * 14, heightblock * 5), "CONTACT WILL SLOW YOU DOWN AND BACK YOU OUT OF A MOVE. WAIT FOR A GAP! (" + tutorialSteeringCount + "/3)");
					GUI.skin.label.normal.textColor = Color.red;
					GUI.Label(new Rect(widthblock * 3, heightblock * 10, widthblock * 14, heightblock * 5), "CONTACT WILL SLOW YOU DOWN AND BACK YOU OUT OF A MOVE. WAIT FOR A GAP! (" + tutorialSteeringCount + "/3)");
					GUI.skin.label.fontSize = 80 / FontScale.fontScale;
					GUI.skin.label.normal.textColor = Color.yellow;

				} else {
					GUI.Label(new Rect((widthblock * 3) + (8 / (FontScale.fontScale * 2)),(heightblock * 10) + (8 / (FontScale.fontScale * 2)), widthblock * 14, heightblock * 5), "USE THE ARROW BUTTONS IN THE BOTTOM CORNERS TO CHANGE LANE. TRY IT NOW! (" + tutorialSteeringCount + "/3)");
					GUI.Label(new Rect((widthblock * 3) + (8 / (FontScale.fontScale * 2)),(heightblock * 10) - (8 / (FontScale.fontScale * 2)), widthblock * 14, heightblock * 5), "USE THE ARROW BUTTONS IN THE BOTTOM CORNERS TO CHANGE LANE. TRY IT NOW! (" + tutorialSteeringCount + "/3)");
					GUI.Label(new Rect((widthblock * 3) - (8 / (FontScale.fontScale * 2)),(heightblock * 10) + (8 / (FontScale.fontScale * 2)), widthblock * 14, heightblock * 5), "USE THE ARROW BUTTONS IN THE BOTTOM CORNERS TO CHANGE LANE. TRY IT NOW! (" + tutorialSteeringCount + "/3)");
					GUI.Label(new Rect((widthblock * 3) - (8 / (FontScale.fontScale * 2)),(heightblock * 10) - (8 / (FontScale.fontScale * 2)), widthblock * 14, heightblock * 5), "USE THE ARROW BUTTONS IN THE BOTTOM CORNERS TO CHANGE LANE. TRY IT NOW! (" + tutorialSteeringCount + "/3)");
					GUI.skin.label.normal.textColor = Color.yellow;
					GUI.Label(new Rect(widthblock * 3, heightblock * 10, widthblock * 14, heightblock * 5), "USE THE ARROW BUTTONS IN THE BOTTOM CORNERS TO CHANGE LANE. TRY IT NOW! (" + tutorialSteeringCount + "/3)");
					GUI.skin.label.fontSize = 80 / FontScale.fontScale;
					GUI.skin.label.normal.textColor = Color.yellow;
				}
			}
			if(tutorialStage == 2){
				GUI.skin.label.alignment = TextAnchor.MiddleCenter;
				GUI.skin.label.fontSize = 80 / FontScale.fontScale;
				GUI.skin.label.normal.textColor = Color.black;
				GUI.Label(new Rect((widthblock * 3) + (8 / (FontScale.fontScale * 2)),(heightblock * 10) + (8 / (FontScale.fontScale * 2)), widthblock * 14, heightblock * 5), "USE THE BRAKE TO SCRUB OFF SOME SPEED. BELOW THE MINIMUM SPEED YOU CANNOT GO ANY SLOWER! (" + tutorialBrakingCount + "/1)");
				GUI.Label(new Rect((widthblock * 3) + (8 / (FontScale.fontScale * 2)),(heightblock * 10) - (8 / (FontScale.fontScale * 2)), widthblock * 14, heightblock * 5), "USE THE BRAKE TO SCRUB OFF SOME SPEED. BELOW THE MINIMUM SPEED YOU CANNOT GO ANY SLOWER! (" + tutorialBrakingCount + "/1)");
				GUI.Label(new Rect((widthblock * 3) - (8 / (FontScale.fontScale * 2)),(heightblock * 10) + (8 / (FontScale.fontScale * 2)), widthblock * 14, heightblock * 5), "USE THE BRAKE TO SCRUB OFF SOME SPEED. BELOW THE MINIMUM SPEED YOU CANNOT GO ANY SLOWER! (" + tutorialBrakingCount + "/1)");
				GUI.Label(new Rect((widthblock * 3) - (8 / (FontScale.fontScale * 2)),(heightblock * 10) - (8 / (FontScale.fontScale * 2)), widthblock * 14, heightblock * 5), "USE THE BRAKE TO SCRUB OFF SOME SPEED. BELOW THE MINIMUM SPEED YOU CANNOT GO ANY SLOWER! (" + tutorialBrakingCount + "/1)");
				GUI.skin.label.normal.textColor = Color.yellow;
				GUI.Label(new Rect(widthblock * 3, heightblock * 10, widthblock * 14, heightblock * 5), "USE THE BRAKE TO SCRUB OFF SOME SPEED. BELOW THE MINIMUM SPEED YOU CANNOT GO ANY SLOWER! (" + tutorialBrakingCount + "/1)");
				GUI.skin.label.fontSize = 80 / FontScale.fontScale;
				GUI.skin.label.normal.textColor = Color.yellow;
			}
			if(tutorialStage == 3){
				GUI.skin.label.alignment = TextAnchor.MiddleCenter;
				GUI.skin.label.fontSize = 80 / FontScale.fontScale;
				GUI.skin.label.normal.textColor = Color.black;
				GUI.Label(new Rect((widthblock * 3) + (8 / (FontScale.fontScale * 2)),(heightblock * 10) + (8 / (FontScale.fontScale * 2)), widthblock * 14, heightblock * 5), "TIME TO DRAFT! TUCK IN CLOSE BEHIND ANOTHER CAR AND WATCH THE SPEED PICK UP! (" + tutorialDraftingCount + "/500)");
				GUI.Label(new Rect((widthblock * 3) + (8 / (FontScale.fontScale * 2)),(heightblock * 10) - (8 / (FontScale.fontScale * 2)), widthblock * 14, heightblock * 5), "TIME TO DRAFT! TUCK IN CLOSE BEHIND ANOTHER CAR AND WATCH THE SPEED PICK UP! (" + tutorialDraftingCount + "/500)");
				GUI.Label(new Rect((widthblock * 3) - (8 / (FontScale.fontScale * 2)),(heightblock * 10) + (8 / (FontScale.fontScale * 2)), widthblock * 14, heightblock * 5), "TIME TO DRAFT! TUCK IN CLOSE BEHIND ANOTHER CAR AND WATCH THE SPEED PICK UP! (" + tutorialDraftingCount + "/500)");
				GUI.Label(new Rect((widthblock * 3) - (8 / (FontScale.fontScale * 2)),(heightblock * 10) - (8 / (FontScale.fontScale * 2)), widthblock * 14, heightblock * 5), "TIME TO DRAFT! TUCK IN CLOSE BEHIND ANOTHER CAR AND WATCH THE SPEED PICK UP! (" + tutorialDraftingCount + "/500)");
				GUI.skin.label.normal.textColor = Color.yellow;
				GUI.Label(new Rect(widthblock * 3, heightblock * 10, widthblock * 14, heightblock * 5), "TIME TO DRAFT! TUCK IN CLOSE BEHIND ANOTHER CAR AND WATCH THE SPEED PICK UP! (" + tutorialDraftingCount + "/500)");
				GUI.skin.label.fontSize = 80 / FontScale.fontScale;
				GUI.skin.label.normal.textColor = Color.yellow;
			}
			if(tutorialStage == 4){
				GUI.skin.label.alignment = TextAnchor.MiddleCenter;
				GUI.skin.label.fontSize = 80 / FontScale.fontScale;
				GUI.skin.label.normal.textColor = Color.black;
				GUI.Label(new Rect((widthblock * 3) + (8 / (FontScale.fontScale * 2)),(heightblock * 10) + (8 / (FontScale.fontScale * 2)), widthblock * 14, heightblock * 5), "SLOW UP IN FRONT OF ANOTHER CAR AND THEY WILL GIVE YOU A BACKDRAFT! A GREAT WAY TO KEEP UP SPEED! (" + tutorialBackdraftCount + "/100)");
				GUI.Label(new Rect((widthblock * 3) + (8 / (FontScale.fontScale * 2)),(heightblock * 10) - (8 / (FontScale.fontScale * 2)), widthblock * 14, heightblock * 5), "SLOW UP IN FRONT OF ANOTHER CAR AND THEY WILL GIVE YOU A BACKDRAFT! A GREAT WAY TO KEEP UP SPEED! (" + tutorialBackdraftCount + "/100)");
				GUI.Label(new Rect((widthblock * 3) - (8 / (FontScale.fontScale * 2)),(heightblock * 10) + (8 / (FontScale.fontScale * 2)), widthblock * 14, heightblock * 5), "SLOW UP IN FRONT OF ANOTHER CAR AND THEY WILL GIVE YOU A BACKDRAFT! A GREAT WAY TO KEEP UP SPEED! (" + tutorialBackdraftCount + "/100)");
				GUI.Label(new Rect((widthblock * 3) - (8 / (FontScale.fontScale * 2)),(heightblock * 10) - (8 / (FontScale.fontScale * 2)), widthblock * 14, heightblock * 5), "SLOW UP IN FRONT OF ANOTHER CAR AND THEY WILL GIVE YOU A BACKDRAFT! A GREAT WAY TO KEEP UP SPEED! (" + tutorialBackdraftCount + "/100)");
				GUI.skin.label.normal.textColor = Color.yellow;
				GUI.Label(new Rect(widthblock * 3, heightblock * 10, widthblock * 14, heightblock * 5), "SLOW UP IN FRONT OF ANOTHER CAR AND THEY WILL GIVE YOU A BACKDRAFT! A GREAT WAY TO KEEP UP SPEED! (" + tutorialBackdraftCount + "/100)");
				GUI.skin.label.fontSize = 80 / FontScale.fontScale;
				GUI.skin.label.normal.textColor = Color.yellow;
			}
			if(tutorialStage == 5){
				GUI.skin.label.alignment = TextAnchor.MiddleCenter;
				GUI.skin.label.fontSize = 80 / FontScale.fontScale;
				GUI.skin.label.normal.textColor = Color.black;
				GUI.Label(new Rect((widthblock * 5) + (8 / (FontScale.fontScale * 2)),(heightblock * 6) + (8 / (FontScale.fontScale * 2)), widthblock * 10, heightblock * 6), "LOOKS LIKE YOU ARE READY TO RACE! SEE YOU ON THE TRACK!");
				GUI.Label(new Rect((widthblock * 5) + (8 / (FontScale.fontScale * 2)),(heightblock * 6) - (8 / (FontScale.fontScale * 2)), widthblock * 10, heightblock * 6), "LOOKS LIKE YOU ARE READY TO RACE! SEE YOU ON THE TRACK!");
				GUI.Label(new Rect((widthblock * 5) - (8 / (FontScale.fontScale * 2)),(heightblock * 6) + (8 / (FontScale.fontScale * 2)), widthblock * 10, heightblock * 6), "LOOKS LIKE YOU ARE READY TO RACE! SEE YOU ON THE TRACK!");
				GUI.Label(new Rect((widthblock * 5) - (8 / (FontScale.fontScale * 2)),(heightblock * 6) - (8 / (FontScale.fontScale * 2)), widthblock * 10, heightblock * 6), "LOOKS LIKE YOU ARE READY TO RACE! SEE YOU ON THE TRACK!");
				GUI.skin.label.normal.textColor = Color.green;
				GUI.Label(new Rect(widthblock * 5, heightblock * 6, widthblock * 10, heightblock * 6), "LOOKS LIKE YOU ARE READY TO RACE! SEE YOU ON THE TRACK!");
				GUI.skin.label.fontSize = 80 / FontScale.fontScale;
				GUI.skin.label.normal.textColor = Color.yellow;
				if (GUI.Button(new Rect(widthblock * 13, Screen.height - (heightblock * 6), widthblock * 6, heightblock * 4), "Continue")){
					Time.timeScale = 1.0f;
					gamePaused = false;
					raceOver = false;
					SceneManager.LoadScene("MainMenu");
				}
			}
		}
		
		if(PlayerPrefs.GetInt("ActiveCaution") == 1){
			GUI.skin.label.alignment = TextAnchor.MiddleCenter;
			GUI.skin.label.fontSize = 120 / FontScale.fontScale;
			GUI.skin.label.normal.textColor = Color.black;
			GUI.Label(new Rect((widthblock * 5) + (8 / (FontScale.fontScale * 2)),(heightblock * 4) + (8 / (FontScale.fontScale * 2)), widthblock * 10, heightblock * 4), "CAUTION\nDEBRIS!");
			GUI.Label(new Rect((widthblock * 5) + (8 / (FontScale.fontScale * 2)),(heightblock * 4) - (8 / (FontScale.fontScale * 2)), widthblock * 10, heightblock * 4), "CAUTION\nDEBRIS!");
			GUI.Label(new Rect((widthblock * 5) - (8 / (FontScale.fontScale * 2)),(heightblock * 4) + (8 / (FontScale.fontScale * 2)), widthblock * 10, heightblock * 4), "CAUTION\nDEBRIS!");
			GUI.Label(new Rect((widthblock * 5) - (8 / (FontScale.fontScale * 2)),(heightblock * 4) - (8 / (FontScale.fontScale * 2)), widthblock * 10, heightblock * 4), "CAUTION\nDEBRIS!");
			GUI.skin.label.normal.textColor = Color.yellow;
			GUI.Label(new Rect(widthblock * 5, heightblock * 4, widthblock * 10, heightblock * 4), "CAUTION\nDEBRIS!");
			GUI.skin.label.fontSize = 80 / FontScale.fontScale;
			GUI.skin.label.normal.textColor = Color.yellow;
		}

		if((CameraRotate.lap == PlayerPrefs.GetInt("RaceLaps"))&&(CameraRotate.straight == 1)&&(CameraRotate.straightcounter > (PlayerPrefs.GetInt("StartLine")+1))&&(raceOver == false)){
			GUI.skin.label.alignment = TextAnchor.MiddleCenter;
			GUI.skin.label.fontSize = 120 / FontScale.fontScale;
			GUI.skin.label.normal.textColor = Color.black;
			GUI.Label(new Rect((widthblock * 5) + (8 / (FontScale.fontScale * 2)),(heightblock * 3) + (8 / (FontScale.fontScale * 2)), widthblock * 10, heightblock * 4), "FINAL LAP!");
			GUI.Label(new Rect((widthblock * 5) + (8 / (FontScale.fontScale * 2)),(heightblock * 3) - (8 / (FontScale.fontScale * 2)), widthblock * 10, heightblock * 4), "FINAL LAP!");
			GUI.Label(new Rect((widthblock * 5) - (8 / (FontScale.fontScale * 2)),(heightblock * 3) + (8 / (FontScale.fontScale * 2)), widthblock * 10, heightblock * 4), "FINAL LAP!");
			GUI.Label(new Rect((widthblock * 5) - (8 / (FontScale.fontScale * 2)),(heightblock * 3) - (8 / (FontScale.fontScale * 2)), widthblock * 10, heightblock * 4), "FINAL LAP!");
			GUI.skin.label.normal.textColor = Color.white;
			GUI.Label(new Rect(widthblock * 5, heightblock * 3, widthblock * 10, heightblock * 4), "FINAL LAP!");
			GUI.skin.label.fontSize = 80 / FontScale.fontScale;
			GUI.skin.label.normal.textColor = Color.yellow;
		}

		GUI.skin.label.alignment = TextAnchor.UpperRight;
		GUI.skin.label.fontSize = 56 / FontScale.fontScale;
	}
	
	public string getCarNum(string name){
		string BuddyNum = Regex.Replace(name, "[^0-9]", "");
		BuddyNum = BuddyNum.Substring(1);
		return BuddyNum;
	}
	
	void thiccLabel(float posX, float posY, float sizeW, float sizeH, string text, UnityEngine.Color color){
		GUI.skin.label.normal.textColor = Color.black;
		GUI.Label(new Rect(posX + (8 / (FontScale.fontScale * 2)), posY + (8 / (FontScale.fontScale * 2)), sizeW, sizeH), text);
		GUI.Label(new Rect(posX - (8 / (FontScale.fontScale * 2)), posY + (8 / (FontScale.fontScale * 2)), sizeW, sizeH), text);
		GUI.Label(new Rect(posX + (8 / (FontScale.fontScale * 2)), posY - (8 / (FontScale.fontScale * 2)), sizeW, sizeH), text);
		GUI.Label(new Rect(posX - (8 / (FontScale.fontScale * 2)), posY - (8 / (FontScale.fontScale * 2)), sizeW, sizeH), text);
		GUI.skin.label.normal.textColor = color;
		GUI.Label(new Rect(posX, posY, sizeW, sizeH), text);
	} 
}
