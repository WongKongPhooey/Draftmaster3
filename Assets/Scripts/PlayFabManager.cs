using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using PlayFab;
using PlayFab.ClientModels;

public class PlayFabManager : MonoBehaviour 
{
	
	public static GameObject rowPrefab;
	public static Transform rowsParent;
	
	public static string errorMessageBuffer;
	
	public Text messageText;
	public InputField usernameInput;
	public InputField emailInput;
	public InputField passwordInput;
	
	public GameObject thisRowPrefab;
	public Transform thisRowsParent;
	
	public static GameObject alertPopup;
	
    // Start is called before the first frame update
    void Awake(){
	   rowPrefab = thisRowPrefab;
	   rowsParent = thisRowsParent;
	   
	   alertPopup = GameObject.Find("AlertPopup");
    }
	
	void Update(){
        try {
            messageText.text = errorMessageBuffer;
        }       
        catch (NullReferenceException ex) {
        }
	}
	
	public static void LoginFromPrefs(){
		string playerEmail = PlayerPrefs.GetString("PlayerEmail");
		string playerPassword = PlayerPrefs.GetString("PlayerPassword");
		var request = new LoginWithEmailAddressRequest{
			Email = playerEmail,
			Password = playerPassword
		};
		if(PlayerPrefs.HasKey("PlayerEmail")){
			PlayFabClientAPI.LoginWithEmailAddress(request, OnPrefLoginSuccess, OnError);
		} else {
			Debug.Log("No saved login");
		}
	}
	
	public static void OnPrefLoginSuccess(LoginResult result){
		Debug.Log("Login from prefs successful!");
		GetTitleData();
		GetPlayerData();
	}
	
	public void LoginButton(){
		var request = new LoginWithEmailAddressRequest{
			Email = emailInput.text,
			Password = passwordInput.text
		};
		PlayFabClientAPI.LoginWithEmailAddress(request, OnLoginSuccess, OnError);
	}
	
	void OnLoginSuccess(LoginResult result){
		//Debug.Log("Login successful!");
		PlayerPrefs.SetString("PlayerEmail", emailInput.text);
		PlayerPrefs.SetString("PlayerPassword", passwordInput.text);
		PlayerPrefs.SetString("PlayerPlayFabId", result.PlayFabId);
		GetPlayerInfo(result.PlayFabId);
	}
	
	public static void GetPlayerInfo(string playFabID){
		var request = new GetPlayerProfileRequest{
			PlayFabId = playFabID
		};
		PlayFabClientAPI.GetPlayerProfile(request, OnGetUsernameSuccess, OnError);
	}
	
	static void OnGetUsernameSuccess(GetPlayerProfileResult result){
		//Debug.Log("Retrieved Username!");
		string username = result.PlayerProfile.DisplayName;
		if(username.StartsWith("DELETED")){
			Debug.Log("That account was deleted..");
			PlayerPrefs.DeleteKey("PlayerUsername");
			PlayerPrefs.DeleteKey("PlayerEmail");
			PlayerPrefs.DeleteKey("PlayerPassword");
			SceneManager.LoadScene(SceneManager.GetActiveScene().name);
		} else {
			PlayerPrefs.SetString("PlayerUsername", result.PlayerProfile.DisplayName);
			//Attempt to load saved data
			GetSavedPlayerProgress();
			SceneManager.LoadScene("Menus/MainMenu");
		}
	}
	
	public static void OnError(PlayFabError error){
		Debug.Log("Something went wrong..");
		Debug.Log(error.GenerateErrorReport());
		errorMessageBuffer = error.GenerateErrorReport();
		
		if (errorMessageBuffer.Length > 0){
		   int word = errorMessageBuffer.IndexOf(" ")+1;
		   errorMessageBuffer ="Error: " + errorMessageBuffer.Substring(word);
		}
	}
	
	public void RegisterButton(){
		var request = new RegisterPlayFabUserRequest {
			Username = usernameInput.text,
			DisplayName = usernameInput.text,
			Email = emailInput.text,
			Password = passwordInput.text,
			RequireBothUsernameAndEmail = true
		};
		PlayFabClientAPI.RegisterPlayFabUser(request, OnRegisterSuccess, OnLoginError);
	}
	
	void OnRegisterSuccess(RegisterPlayFabUserResult result){
		messageText.text = "User Registered Successfully";
		PlayerPrefs.SetString("PlayerUsername", usernameInput.text);
		PlayerPrefs.SetString("PlayerEmail", emailInput.text);
		PlayerPrefs.SetString("PlayerPassword", passwordInput.text);
		LoginFromPrefs();
		SceneManager.LoadScene("Menus/MainMenu");
	}
	
	void OnLoginError(PlayFabError error){
		messageText.text = error.GenerateErrorReport();
		Debug.Log(error.GenerateErrorReport());
	}
	
	public static void Logout(){
		PlayerPrefs.DeleteKey("PlayerUsername");
		PlayerPrefs.DeleteKey("PlayerEmail");
		PlayerPrefs.DeleteKey("PlayerPassword");
		SceneManager.LoadScene("MainMenu");
	}
	
	//Junks the username to DelXXXX and logs out.
	public static void DeleteAccount(){
		string playFabId = PlayerPrefs.GetString("PlayerPlayFabId");
		var request = new UpdateUserTitleDisplayNameRequest{
			DisplayName = "DELETED" + playFabId
		};
		PlayFabClientAPI.UpdateUserTitleDisplayName(request, OnDeletePlayerSuccess, OnError);
	}
	
	public static void OnDeletePlayerSuccess(UpdateUserTitleDisplayNameResult result){
		Debug.Log("Player Deleted" + result);
		PlayerPrefs.DeleteKey("PlayerUsername");
		PlayerPrefs.DeleteKey("PlayerEmail");
		PlayerPrefs.DeleteKey("PlayerPassword");
		SceneManager.LoadScene("Menus/MainMenu");
	}
	
	public static void CheckLiveTimeTrial(){
		PlayFabClientAPI.GetTitleData(new GetTitleDataRequest(), OnLiveTimeTrialReceived, OnTitleError);
	}
	
	public static void OnLiveTimeTrialReceived(GetTitleDataResult result){
		if(result.Data == null){
			Debug.Log("No Live Time Trial Found");
			PlayerPrefs.SetString("LiveTimeTrial","");
		}

		//Live Race Time Trial
		if(result.Data.ContainsKey("LiveTimeTrial") == true){
			if(result.Data["LiveTimeTrial"] != ""){
				PlayerPrefs.SetString("LiveTimeTrial", result.Data["LiveTimeTrial"]);
				Debug.Log("Live Time Trial At " + result.Data["LiveTimeTrial"]);
			} else {
				PlayerPrefs.SetString("LiveTimeTrial","");
			}
		} else {
			PlayerPrefs.SetString("LiveTimeTrial","");
		}
	}
	
	public static void GetTitleData(){
		PlayFabClientAPI.GetTitleData(new GetTitleDataRequest(), OnTitleDataReceived, OnTitleError);
	}
	
	public static void OnTitleDataReceived(GetTitleDataResult result){
		if(result.Data == null){
			Debug.Log("No Title Data Found");
			//Remove the last known store values
			PlayerPrefs.SetString("StoreDailySelects", "");
			PlayerPrefs.SetInt("FreeFuel", 0);
			PlayerPrefs.SetInt("ShopDiscount", 0);
			PlayerPrefs.SetInt("EventActive", 0);
			PlayerPrefs.SetString("LiveTimeTrial","");
			PlayerPrefs.SetString("SpecialEvent","");
		}
		
		//Testing
		//result.Data["LiveTimeTrial"] = "Atlanta";
		
		if(result.Data.ContainsKey("MomentName") == false){
			PlayerPrefs.SetString("MomentName", "");
		} else {
			//There's a Live Moment set
			if((result.Data["MomentName"] != "")
				&&(result.Data["MomentVisibility"] == "Public")){
				PlayerPrefs.SetString("MomentName", result.Data["MomentName"]);
				PlayerPrefs.SetString("MomentDescription", result.Data["MomentDescription"]);
				PlayerPrefs.SetString("MomentImage", result.Data["MomentImage"]);
				PlayerPrefs.SetString("MomentAILevel", result.Data["MomentAILevel"]);
				PlayerPrefs.SetString("MomentSeries", result.Data["MomentSeries"]);
				PlayerPrefs.SetString("MomentCar", result.Data["MomentCar"]);
				PlayerPrefs.SetString("MomentTrack", result.Data["MomentTrack"]);
				PlayerPrefs.SetString("MomentLap", result.Data["MomentLap"]);
				PlayerPrefs.SetString("MomentCriteria1", result.Data["MomentCriteria1"]);
				if(result.Data.ContainsKey("MomentCriteria2") == true){
					if(result.Data["MomentCriteria2"] != ""){
						PlayerPrefs.SetString("MomentCriteria2", result.Data["MomentCriteria2"]);
					}
				}
				if(result.Data.ContainsKey("MomentCriteria3") == true){
					if(result.Data["MomentCriteria3"] != ""){
						PlayerPrefs.SetString("MomentCriteria3", result.Data["MomentCriteria3"]);
					}
				}
				if(result.Data.ContainsKey("MomentCriteria4") == true){
					if(result.Data["MomentCriteria4"] != ""){	
						PlayerPrefs.SetString("MomentCriteria4", result.Data["MomentCriteria4"]);
					}
				}
				if(result.Data.ContainsKey("MomentCriteria5") == true){
					if(result.Data["MomentCriteria5"] != ""){
						PlayerPrefs.SetString("MomentCriteria5", result.Data["MomentCriteria5"]);
					}
				}
				if(result.Data.ContainsKey("MomentCustomField") == true){
					if(result.Data["MomentCustomField"] != ""){
						PlayerPrefs.SetString("LiveMomentCustomField", result.Data["MomentCustomField"]);
					}
				}
				if(result.Data.ContainsKey("MomentMods") == true){
					if(result.Data["MomentMods"] != ""){
						PlayerPrefs.SetString("LiveMomentMods", result.Data["MomentMods"]);
					}
				}
			} else {
				PlayerPrefs.SetString("MomentName", "");
				PlayerPrefs.DeleteKey("MomentDescription");
				PlayerPrefs.DeleteKey("MomentImage");
				PlayerPrefs.DeleteKey("MomentAILevel");
				PlayerPrefs.DeleteKey("MomentSeries");
				PlayerPrefs.DeleteKey("MomentCar");
				PlayerPrefs.DeleteKey("MomentTrack");
				PlayerPrefs.DeleteKey("MomentLap");
				PlayerPrefs.DeleteKey("MomentCriteria1");
				PlayerPrefs.DeleteKey("MomentCriteria2");
				PlayerPrefs.DeleteKey("MomentCriteria3");
				PlayerPrefs.DeleteKey("MomentCriteria4");
				PlayerPrefs.DeleteKey("MomentCriteria5");
				PlayerPrefs.DeleteKey("LiveMomentCustomField");
				PlayerPrefs.DeleteKey("LiveMomentMods");
				
				//Debug.Log("Removed Live Moment From Local Storage");
				
				#if UNITY_EDITOR
				if(result.Data["MomentName"] != ""){
					PlayerPrefs.SetString("MomentName", result.Data["MomentName"]);
					PlayerPrefs.SetString("MomentDescription", result.Data["MomentDescription"]);
					PlayerPrefs.SetString("MomentImage", result.Data["MomentImage"]);
					PlayerPrefs.SetString("MomentAILevel", result.Data["MomentAILevel"]);
					PlayerPrefs.SetString("MomentSeries", result.Data["MomentSeries"]);
					PlayerPrefs.SetString("MomentCar", result.Data["MomentCar"]);
					PlayerPrefs.SetString("MomentTrack", result.Data["MomentTrack"]);
					PlayerPrefs.SetString("MomentLap", result.Data["MomentLap"]);
					PlayerPrefs.SetString("MomentCriteria1", result.Data["MomentCriteria1"]);
					if(result.Data.ContainsKey("MomentCriteria2") == true){
						if(result.Data["MomentCriteria2"] != ""){
							PlayerPrefs.SetString("MomentCriteria2", result.Data["MomentCriteria2"]);
						}
					}
					if(result.Data.ContainsKey("MomentCriteria3") == true){
						if(result.Data["MomentCriteria3"] != ""){
							PlayerPrefs.SetString("MomentCriteria3", result.Data["MomentCriteria3"]);
						}
					}
					if(result.Data.ContainsKey("MomentCriteria4") == true){
						if(result.Data["MomentCriteria4"] != ""){	
							PlayerPrefs.SetString("MomentCriteria4", result.Data["MomentCriteria4"]);
						}
					}
					if(result.Data.ContainsKey("MomentCriteria5") == true){
						if(result.Data["MomentCriteria5"] != ""){
							PlayerPrefs.SetString("MomentCriteria5", result.Data["MomentCriteria5"]);
						}
					}
					if(result.Data.ContainsKey("MomentCustomField") == true){
						if(result.Data["MomentCustomField"] != ""){
							PlayerPrefs.SetString("LiveMomentCustomField", result.Data["MomentCustomField"]);
						}
					}
					if(result.Data.ContainsKey("MomentMods") == true){
						if(result.Data["MomentMods"] != ""){
							PlayerPrefs.SetString("LiveMomentMods", result.Data["MomentMods"]);
						}
					}
				}
				#endif
			}
		}
		
		//Custom store items in Daily Selects
		if(result.Data.ContainsKey("StoreDailySelects") == false){
			//Debug.Log("No online Store Daily Selects");
			//Remove the last known store values
			PlayerPrefs.SetString("StoreDailySelects", "");
		} else {
			//Fake store values for testing
			#if UNITY_EDITOR
			result.Data["StoreDailySelects"] = "1,2,3,4,5,6,7,8,9,10,11,12,cup221,cup222,cup223,cup224,dmc151,dmc152,dmc153,dmc154,dmc155,cup238,cup239,cup2315,cup2320,cup2322,cup2324,cup23livery8alt1,cup23livery9alt1,cup23livery9alt2,cup23livery15alt1,cup23livery20alt1,cup23livery22alt1,cup23livery24alt1";
			#endif
			
			PlayerPrefs.SetString("StoreDailySelects", result.Data["StoreDailySelects"]);
			Debug.Log("Store Updated " + PlayerPrefs.GetString("StoreDailySelects"));
		}
		
		//Trigger a Special Challenge Event
		if(result.Data.ContainsKey("SpecialEvent") == false){
			//Debug.Log("No online Store Daily Selects");
			//Remove the last known store values
			PlayerPrefs.SetString("SpecialEvent", "");
		} else {
			//Fake store values for testing
			//result.Data["SpecialEvent"] = "1,2,3,4,5,6,7,8,9,10,11,12,cup221,cup222,cup223,cup224,dmc151,dmc152,dmc153,dmc154,dmc155,cup20livery9alt1,cup20livery47alt1,cup20livery27alt1,cup20livery18alt1";
			
			PlayerPrefs.SetString("SpecialEvent", result.Data["SpecialEvent"]);
			//Debug.Log("Store Updated " + PlayerPrefs.GetString("SpecialEvent"));
		}
		
		//Debug Add Event for testing
		//result.Data["EventActive"] = "End Of An Era Pt.2";
		//result.Data["EventShortcode"] = "2020Pt2";
		//result.Data["EventDescription"] = "5 more of our favourite paints from the 2020 season. Featuring Bell, Hill, Stenhouse, Chastain and Preece!";
		//result.Data["EventRewards"] = "cup20livery37alt1,cup20livery47alt1,cup20livery66alt1,cup20livery77alt1,cup20livery95alt1";
		
		//result.Data["EventActive"] = "Part Timers";
		//result.Data["EventShortcode"] = "PartTimers";
		//result.Data["EventDescription"] = "6 of our favourite 2022 part-time drivers! Unlock and race as Ragan, Hemric, Hezemans, Said, Bilicki and Lally.";
		//result.Data["EventRewards"] = "cup22livery15alt1,cup22livery16alt1,cup22livery27alt1,cup22livery66alt1,cup22livery77alt1,cup22livery78alt1";
		
		//Event Store
		if(result.Data.ContainsKey("EventActive") == true){
			if(result.Data["EventActive"] != ""){
				PlayerPrefs.SetInt("EventActive", 1);
				//Last Known Event Name
				string previousEvent = PlayerPrefs.GetString("EventName");
				if(result.Data["EventActive"] != previousEvent){
					PlayerPrefs.SetString("EventName", result.Data["EventActive"]);
					Debug.Log("Event was: " + previousEvent + ". Now: " + result.Data["EventActive"]);
					PlayerPrefs.DeleteKey("PrizePositions");
				}
				Debug.Log(result.Data["EventActive"] + " Event Active");
				
				//Update Event Meta
				PlayerPrefs.SetString("EventImage", result.Data["EventShortcode"]);
				PlayerPrefs.SetString("EventDescription", result.Data["EventDescription"]);
				PlayerPrefs.SetString("EventPrizeset", result.Data["EventShortcode"]);
				
				//Retrieve the event rewards (assume set)
				PlayerPrefs.SetString("EventRewards", result.Data["EventRewards"]);
				Debug.Log("Event Prizes: " + result.Data["EventRewards"]);
				
			} else {
				PlayerPrefs.SetInt("EventActive", 0);

				PlayerPrefs.SetString("EventImage", "");
				PlayerPrefs.SetString("EventDescription", "No active event.");
				PlayerPrefs.SetString("EventPrizeset", "");
				PlayerPrefs.SetString("EventRewards", "");
				Debug.Log("No Active Event");
			}
		} else {
			PlayerPrefs.SetInt("EventActive", 0);
			
			PlayerPrefs.SetString("EventImage", "");
			PlayerPrefs.SetString("EventDescription", "No active event.");
			PlayerPrefs.SetString("EventPrizeset", "");
			PlayerPrefs.SetString("EventRewards", "");
			Debug.Log("No Active Event");
		}
		
		if(result.Data.ContainsKey("TargetVersion") == true){
			PlayerPrefs.SetString("TargetVersion", result.Data["TargetVersion"]);
		} else {
			PlayerPrefs.SetString("TargetVersion", Application.version);
		}
		
		if(result.Data.ContainsKey("GlobalGift") == true){
			if((result.Data["GlobalGift"] != "")
			&&(result.Data["GlobalGift"] != PlayerPrefs.GetString("GlobalGift"))){
				PlayerPrefs.SetString("GlobalGift", result.Data["GlobalGift"]);
				Debug.Log("Received Global Gift!");
			} else {
				PlayerPrefs.DeleteKey("InForm");
			}
		}
		
		//In Form Driver
		if(result.Data.ContainsKey("InForm") == true){
			//Format example: "Larson"
			if(result.Data["InForm"] != ""){
				PlayerPrefs.SetString("InForm", result.Data["InForm"]);
				Debug.Log("In Form Driver is " + result.Data["InForm"]);
			} else {
				PlayerPrefs.DeleteKey("InForm");
			}
		} else {
			PlayerPrefs.DeleteKey("InForm");
		}
		
		//Free Fuel Promo
		if(result.Data.ContainsKey("FreeFuel") == true){
			if(result.Data["FreeFuel"] == "Yes"){
				PlayerPrefs.SetInt("FreeFuel", 1);
				Debug.Log("Free Fuel Activated");
			} else {
				PlayerPrefs.SetInt("FreeFuel", 0);
			}
		} else {
			PlayerPrefs.SetInt("FreeFuel", 0);
		}
		
		//Shop Discount Promo (Everything moves down 1 price tier)
		if(result.Data.ContainsKey("ShopDiscount") == true){
			if(result.Data["ShopDiscount"] == "Yes"){
				PlayerPrefs.SetInt("ShopDiscount", 1);
				Debug.Log("Shop Discount Activated");
			} else {
				PlayerPrefs.SetInt("ShopDiscount", 0);
			}
		} else {
			PlayerPrefs.SetInt("ShopDiscount", 0);
		}
		
		//Testing - Time Trials
		//result.Data["LiveTimeTrial"] = "FortWorth";
		
		//Live Race Time Trial
		if(result.Data.ContainsKey("LiveTimeTrial") == true){
			//Debug.Log("Live Time Trial Check..");
			if(result.Data["LiveTimeTrial"] != ""){
				PlayerPrefs.SetString("LiveTimeTrial", result.Data["LiveTimeTrial"]);
				Debug.Log("Live Time Trial At " + result.Data["LiveTimeTrial"]);
			} else {
				PlayerPrefs.SetString("LiveTimeTrial","");
			}
		} else {
			PlayerPrefs.SetString("LiveTimeTrial","");
		}
		
		//Testing
		//result.Data["MessageAlert"] = "This is a testier message!";
		//result.Data["MessageAlertId"] = "6969";
		
		//Message Alerts
		if(result.Data.ContainsKey("MessageAlert") == true){
			//If there's a message to show..
			//Check for a message ID
			if(result.Data.ContainsKey("MessageAlertId") == true){
				//See if the ID has incremented
				int lastMessageId = 0;
				if(PlayerPrefs.HasKey("MessageAlertId")){
					lastMessageId = PlayerPrefs.GetInt("MessageAlertId");
				} else {
					PlayerPrefs.SetInt("MessageAlertId", int.Parse(result.Data["MessageAlertId"]));
				}
				if(lastMessageId != int.Parse(result.Data["MessageAlertId"])){
					Debug.Log("New Message!");
					PlayerPrefs.SetInt("MessageAlertId", int.Parse(result.Data["MessageAlertId"]));
					PlayerPrefs.SetString("MessageAlert", result.Data["MessageAlert"]);
					//MainMenuGUI.messageAlert = result.Data["MessageAlert"];
					//MainMenuGUI.newMessageAlert = true;
					alertPopup.GetComponent<AlertManager>().showPopup("News",result.Data["MessageAlert"],"dm2logo");
				} else {
					//Debug.Log("No new messages.");
				}
			} else {
				//Debug.Log("No message ID set");
			}
		} else {
			MainMenuGUI.messageAlert = "There are currently no new news items. Login to access the full online features";
		}
	}
	
	static void OnTitleError(PlayFabError error){
		Debug.Log("Something went wrong..");
		Debug.Log(error.GenerateErrorReport());
		
		//Remove active online promos if cannot connect to PlayFab
		PlayerPrefs.SetString("StoreDailySelects", "");
		PlayerPrefs.SetInt("FreeFuel", 0);
	}
	
	public static void GetPlayerData(){
		PlayFabClientAPI.GetUserData(new GetUserDataRequest(), OnDataReceived, OnError);
	}
	
	static void OnDataReceived(GetUserDataResult result){
		if(result.Data != null){
			Debug.Log("Player data found");
			
			//Fake testing values
			//result.Data["RewardGears"].Value = "69";
			
			string rewardMessage = "";
			string rewardCarImg = "dm2logo";
			if(result.Data.ContainsKey("RewardGears")){
				int gears = PlayerPrefs.GetInt("Gears");
				int rewardGears = int.Parse(result.Data["RewardGears"].Value);
				if(rewardGears != 0){
					gears += rewardGears;
					rewardMessage += "You've received a crate of " + rewardGears + " gears!\n";
					//MainMenuGUI.newGiftAlert = true;
					rewardGears = 0;
					PlayerPrefs.SetInt("Gears", gears);
					emptyPlayerData("RewardGears");
				}
			}

			if(result.Data.ContainsKey("RewardTokens")){
				int transferTokens = PlayerPrefs.GetInt("TransferTokens");
				int rewardTokens = int.Parse(result.Data["RewardTokens"].Value);
				if(rewardTokens != 0){
					transferTokens += rewardTokens;
					rewardMessage += "You've received " + rewardTokens + " transfer tokens!\n";
					rewardTokens = 0;
					PlayerPrefs.SetInt("TransferTokens", transferTokens);
					emptyPlayerData("RewardTokens");
				}
			}
			//Fake testing values
			//result.Data["RewardCar"].Value = "cup2291";

			if(result.Data.ContainsKey("RewardCar")){
				//Example: cup2212
				string rewardCar = result.Data["RewardCar"].Value;
				//Debug.Log("RewardCar: " + rewardCar);
				if((rewardCar != "")&&(rewardCar != "0")){
					string rewardCarSeries = rewardCar.Substring(0,5);
					//Debug.Log(rewardCar);
					int rewardCarNum = int.Parse(rewardCar.Substring(5));
					Debug.Log("Rewarded Car #" + rewardCarNum);
					int carClass = PlayerPrefs.GetInt(rewardCarSeries + rewardCarNum + "Class");
					if(carClass == 0){
						PlayerPrefs.SetInt(rewardCarSeries + rewardCarNum + "Unlocked", 1);
						PlayerPrefs.SetInt(rewardCarSeries + rewardCarNum + "Class", DriverNames.getRarity(rewardCarSeries,rewardCarNum));
					} else {
						PlayerPrefs.SetInt(rewardCarSeries + rewardCarNum + "Unlocked", 1);
						PlayerPrefs.SetInt(rewardCarSeries + rewardCarNum + "Class", carClass+1);
					}
					rewardMessage += "You've been gifted a new " + DriverNames.getSeriesNiceName(rewardCarSeries) + " " + DriverNames.getName(rewardCarSeries,rewardCarNum) + " car!\n";
					rewardCarImg = rewardCarSeries + "livery" + rewardCarNum;
					//MainMenuGUI.newGiftAlert = true;
					emptyPlayerData("RewardCar");
				}
			}
			if(result.Data.ContainsKey("RewardAlt")){
				string rewardAlt = result.Data["RewardAlt"].Value;
				//Example: cup20livery20alt1
				if((rewardAlt != "0")&&(rewardAlt != "")&&(rewardAlt != null)){
					Debug.Log("Rewarded Alt #" + rewardAlt);
					
					rewardCarImg = rewardAlt;
					
					//Reformat it to match the PlayerPrefs var..
					rewardAlt = rewardAlt.Replace("livery","");
					rewardAlt = rewardAlt.Replace("alt","Alt");
					
					PlayerPrefs.SetInt(rewardAlt + "Unlocked",1);
					rewardMessage += "You've been gifted a new alt paint! ";
					//MainMenuGUI.newGiftAlert = true;
					emptyPlayerData("RewardAlt");
				}
			}
			if(rewardMessage != ""){
				GameObject alertPopup = GameObject.Find("AlertPopup");
				alertPopup.GetComponent<AlertManager>().showPopup("Rewards",rewardMessage,rewardCarImg);
			}
			//GameObject mainMenuUI = GameObject.Find("Main Camera");
			//if(mainMenuUI.GetComponent<MainMenuUI>() != null){
			//mainMenuUI.GetComponent<MainMenuUI>().showAlert("Rewards",rewardMessage,rewardCarImg);
			//}
		} else {
			Debug.Log("No player data found");
		}
	}
	
	static void receiveCarGift(string car){
		//Example: cup2212
		string rewardCar = car;
		string rewardCarSeries = rewardCar.Substring(0,5);
		Debug.Log(rewardCar);
		int rewardCarNum = int.Parse(rewardCar.Substring(5));
		if(rewardCar != ""){
			Debug.Log("Rewarded Car #" + rewardCarNum);
			int carClass = PlayerPrefs.GetInt(rewardCarSeries + rewardCarNum + "Class");
			if(carClass == 0){
				PlayerPrefs.SetInt(rewardCarSeries + rewardCarNum + "Unlocked", 1);
				PlayerPrefs.SetInt(rewardCarSeries + rewardCarNum + "Class", DriverNames.getRarity(rewardCarSeries,rewardCarNum));
			} else {
				if(carClass < 6){
					PlayerPrefs.SetInt(rewardCarSeries + rewardCarNum + "Class", carClass+1);
				}
			}
			string rewardMessage = "You've been gifted a new " + DriverNames.getSeriesNiceName(rewardCarSeries) + " " + DriverNames.getName(rewardCarSeries,rewardCarNum) + " car!\n";
			string rewardCarImg = rewardCarSeries + "livery" + rewardCarNum;
			GameObject alertPopup = GameObject.Find("AlertPopup");
			alertPopup.GetComponent<AlertManager>().showPopup("Thanks For Playing",rewardMessage,rewardCarImg);
			//MainMenuGUI.newGiftAlert = true;
			emptyPlayerData("RewardCar");
		}
	}
	
	static void emptyPlayerData(string playerDataKey){
		var request = new UpdateUserDataRequest {
			Data = new Dictionary<string, string> {
				{playerDataKey, "0"}
			}
		};
		PlayFabClientAPI.UpdateUserData(request, OnDataSend, OnError);
	}
	
	public static void GetSavedPlayerProgress(){
		PlayFabClientAPI.GetUserData(new GetUserDataRequest(), OnProgressReceived, OnError);
	}
	
	static void OnProgressReceived(GetUserDataResult result){
		if(result.Data != null){
			string seriesPrefix = "cup20";
			string json = "";
			string saveType = "";
			int cloudLevel = 0;
			
			//Check the latest autosave
			if(result.Data.ContainsKey("AutosavePlayerProgress" + seriesPrefix)){
				Debug.Log("No manual save.. looking for an autosave");
				json = result.Data["AutosavePlayerProgress" + seriesPrefix].Value;
				Series playerJson = JsonUtility.FromJson<Series>(json);
				if(cloudLevel < int.Parse(playerJson.playerLevel)){
					cloudLevel = int.Parse(playerJson.playerLevel);
					Debug.Log("Autosave is at level " + cloudLevel);
				}
				saveType = "automatic";
			}
			
			//Check for manual save
			if(result.Data.ContainsKey("SavedPlayerProgress" + seriesPrefix)){
				json = result.Data["SavedPlayerProgresscup20"].Value;
				Series playerJson = JsonUtility.FromJson<Series>(json);
				if(cloudLevel < int.Parse(playerJson.playerLevel)){
					cloudLevel = int.Parse(playerJson.playerLevel);
					Debug.Log("Manual Save is at level " + cloudLevel);
					saveType = "manual";
				} else {
					//Try to revert back to using the autosave
					if(result.Data.ContainsKey("AutosavePlayerProgress" + seriesPrefix)){
						json = result.Data["AutosavePlayerProgress" + seriesPrefix].Value;
						playerJson = JsonUtility.FromJson<Series>(json);
					}
				}
			} 
			
			if(json != ""){
				//We found some form of save data, but should we load it?
				 PlayerPrefs.SetInt("NewUser",1);
				 Series playerJson = JsonUtility.FromJson<Series>(json);
				 int level = int.Parse(playerJson.playerLevel);
				 int transferTokens = 0;
				 //Old save files don't have this field, now skippable
				 if(playerJson.transferTokens != null){
					transferTokens = int.Parse(playerJson.transferTokens);
				 }
				 
				 if(level <= PlayerPrefs.GetInt("Level")){
					Debug.Log("Your save is not a higher level (" + level + ") than what you already have (" + PlayerPrefs.GetInt("Level") + "). Load aborted.");
					return;
				 } else {
					PlayerPrefs.SetInt("Level", level);
				 }
				 
				 ArrayList allSeries = new ArrayList(); 
				 allSeries.Add("cup20");
				 allSeries.Add("cup22");
				 allSeries.Add("irc00");
				 allSeries.Add("dmc15");
				 
				 int unlockedCars = 0;
				 foreach(string series in allSeries){
					 if(saveType == "manual"){
						json = result.Data["SavedPlayerProgress" + series].Value;
						playerJson = JsonUtility.FromJson<Series>(json);
					 }
					 if(saveType == "automatic"){
						//Drop out if no save for this car set
						if(result.Data["AutosavePlayerProgress" + series].Value == null){
							break;
						}
						json = result.Data["AutosavePlayerProgress" + series].Value;
						playerJson = JsonUtility.FromJson<Series>(json);
					 }
					 for(int i=0;i<=99;i++){
						if(DriverNames.getName(series,i) != null){
							if(PlayerPrefs.GetInt(series + i + "Unlocked") < int.Parse(playerJson.drivers[i].carUnlocked)){
								PlayerPrefs.SetInt(series + i + "Unlocked", int.Parse(playerJson.drivers[i].carUnlocked));
								Debug.Log("New unlock on load, " + series + " #" + i);
							}
							if(playerJson.drivers[i].carUnlocked == "1"){
								unlockedCars++;
							}
							if(PlayerPrefs.GetInt(series + i + "Class") < int.Parse(playerJson.drivers[i].carClass)){
								PlayerPrefs.SetInt(series + i + "Class", int.Parse(playerJson.drivers[i].carClass));
								PlayerPrefs.SetInt(series + i + "Gears", int.Parse(playerJson.drivers[i].carGears));
								Debug.Log("Updated class on load, " + series + " #" + i);
							}
							string altsList = playerJson.drivers[i].altPaints;
							string[] altsArray = altsList.Split(',');
							foreach(string alt in altsArray){
								if(int.Parse(alt) > 0){
									PlayerPrefs.SetInt(series + i + "Alt" + alt + "Unlocked", 1);
									Debug.Log("Unlocked Alt on load, " + series + " #" + i + " alt " + alt);
								}
							}
						}
					 }
				 }
				 Debug.Log("Loaded data from server! " + unlockedCars + " unlocked cars.");
				 PlayerPrefs.SetString("SaveLoadOutput","Loaded " + saveType + "save - " + unlockedCars + " unlocked cars.");
			} else {
				Debug.Log("No player data found");
				PlayerPrefs.SetString("SaveLoadOutput","No autosave or manual save data found for this player account.");
			}
		} else {
			Debug.Log("No player data found");
		}
	}
	
	public static void OnDataSend(UpdateUserDataResult result){
		Debug.Log("Rewards Collected, Server Reset");
	}
	
	public static void AutosavePlayerProgress(string seriesPrefix, string progressJSON){
		var request = new UpdateUserDataRequest {
			Data = new Dictionary<string, string> {
				{"AutosavePlayerProgress" + seriesPrefix, progressJSON}
			}
		};
		PlayFabClientAPI.UpdateUserData(request, OnProgressSave, OnError);
	}
	
	public static void SavePlayerProgress(string seriesPrefix, string progressJSON){
		var request = new UpdateUserDataRequest {
			Data = new Dictionary<string, string> {
				{"SavedPlayerProgress" + seriesPrefix, progressJSON}
			}
		};
		PlayFabClientAPI.UpdateUserData(request, OnProgressSave, OnError);
	}
	
	public static void OnProgressSave(UpdateUserDataResult result){
		//Debug.Log("Player Progress Saved");
		PlayerPrefs.SetString("SaveLoadOutput","Saved progress to the server");
	}
	
	public static void SendLeaderboard(int score, string circuitName, string prefix){
		var request = new UpdatePlayerStatisticsRequest {
			Statistics = new List<StatisticUpdate> {
				new StatisticUpdate {
					StatisticName = prefix + circuitName + "",
					Value = score
				}
			}
		};
		try {
			PlayFabClientAPI.UpdatePlayerStatistics(request, OnLeaderboardUpdate, OnError);
			Debug.Log("Sent " + score + " To Leaderboard " + circuitName + ".");
		} catch (Exception e){
			Debug.Log("Cannot reach Playfab to send " + score + " time to " + circuitName);
		}
	}
	
	static void OnLeaderboardUpdate(UpdatePlayerStatisticsResult result){
		//Debug.Log("Leaderboard Updated.");
	}
	
	public static void GetLeaderboard(string circuit){
		
		var request = new GetLeaderboardRequest {
			StatisticName = "FastestLap" + circuit,
			StartPosition = 0,
			MaxResultsCount = 5
		};
		PlayFabClientAPI.GetLeaderboard(request, OnLeaderboardGet, OnError);
	}
	
	public static void GetLeaderboardAroundPlayer(string circuit){
		
		var request = new GetLeaderboardAroundPlayerRequest {
			StatisticName = "FastestLap" + circuit,
			MaxResultsCount = 1
		};
		PlayFabClientAPI.GetLeaderboardAroundPlayer(request, OnLeaderboardAroundPlayerGet, OnError);
	}
	
	static void OnLeaderboardGet(GetLeaderboardResult result) {
		
		foreach(Transform item in rowsParent){
			Destroy(item.gameObject);
		}
		
		foreach(var item in result.Leaderboard) {
			GameObject tableRows = Instantiate(rowPrefab, rowsParent);
			Text[] tableLabels = tableRows.GetComponentsInChildren<Text>();
			tableLabels[0].text = (item.Position + 1).ToString();
			tableLabels[1].text = item.DisplayName;
			float leaderboardSpeed = item.StatValue/1000f;
			tableLabels[2].text = leaderboardSpeed.ToString() + " MpH";
			
			Debug.Log(item.Position + " " + item.PlayFabId + " " + item.StatValue);
		}
	}
	
	static void OnLeaderboardAroundPlayerGet(GetLeaderboardAroundPlayerResult result) {
		
		Debug.Log("Got Leaderboard Around Player");
		
		foreach(var item in result.Leaderboard) {
			GameObject tableRows = Instantiate(rowPrefab, rowsParent);
			Text[] tableLabels = tableRows.GetComponentsInChildren<Text>();
			tableLabels[0].text = (item.Position + 1).ToString();
			tableLabels[1].text = item.DisplayName;
			float leaderboardSpeed = item.StatValue/1000f;
			tableLabels[2].text = leaderboardSpeed.ToString() + " MpH";
			
			if(item.PlayFabId.ToString() == PlayerPrefs.GetString("PlayerPlayFabId")){
				tableLabels[0].color = Color.red;
				tableLabels[1].color = Color.red;
				tableLabels[2].color = Color.red;
			} else {
				
			}
			
			Debug.Log(item.Position + " " + item.PlayFabId + " " + item.StatValue);
		}
	}
	
	public static void GetLiveTimeTrialLeaderboard(){
		
		var request = new GetLeaderboardRequest {
			StatisticName = "LiveTimeTrialR112",
			StartPosition = 0,
			MaxResultsCount = 20
		};
		PlayFabClientAPI.GetLeaderboard(request, OnLiveTimeTrialLeaderboardGet, OnError);
	}
	
	static void OnLiveTimeTrialLeaderboardGet(GetLeaderboardResult result) {
		
		foreach(var item in result.Leaderboard) {
			GameObject tableRows = Instantiate(rowPrefab, rowsParent);
			Text[] tableLabels = tableRows.GetComponentsInChildren<Text>();
			tableLabels[0].text = (item.Position + 1).ToString();
			tableLabels[1].text = item.DisplayName;
			float leaderboardSpeed = item.StatValue/1000f;
			tableLabels[2].text = leaderboardSpeed.ToString() + " MpH";
			
			Debug.Log(item.Position + " " + item.PlayFabId + " " + item.StatValue);
		}
	}
	
	public static void GetLiveTimeTrialAroundPlayer(){
		
		var request = new GetLeaderboardAroundPlayerRequest {
			StatisticName = "LiveTimeTrialR112",
			MaxResultsCount = 1
		};
		PlayFabClientAPI.GetLeaderboardAroundPlayer(request, OnLiveTimeTrialAroundPlayerGet, OnError);
	}
	
	static void OnLiveTimeTrialAroundPlayerGet(GetLeaderboardAroundPlayerResult result) {
		
		//Debug.Log("Got Leaderboard Around Player");
		foreach(Transform item in rowsParent){
			Destroy(item.gameObject);
		}
		
		foreach(var item in result.Leaderboard) {
			GameObject tableRows = Instantiate(rowPrefab, rowsParent);
			Text[] tableLabels = tableRows.GetComponentsInChildren<Text>();
			tableLabels[0].text = (item.Position + 1).ToString();
			tableLabels[1].text = item.DisplayName;
			float leaderboardSpeed = item.StatValue/1000f;
			tableLabels[2].text = leaderboardSpeed.ToString() + " MpH";
			
			if(item.PlayFabId.ToString() == PlayerPrefs.GetString("PlayerPlayFabId")){
				tableLabels[0].color = Color.red;
				tableLabels[1].color = Color.red;
				tableLabels[2].color = Color.red;
			} else {
				
			}
			
			Debug.Log(item.Position + " " + item.PlayFabId + " " + item.StatValue);
		}
	}
}