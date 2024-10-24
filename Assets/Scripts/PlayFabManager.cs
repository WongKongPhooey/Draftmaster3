using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using PlayFab;
using PlayFab.ClientModels;

public class PlayFabManager : MonoBehaviour 
{
	
	public static GameObject rowPrefab;
	public static Transform rowsParent;
	
	public static string errorMessageBuffer;
	
	public static int garageValue;
	
	public Text messageText;
	public InputField usernameInput;
	public InputField emailInput;
	public InputField passwordInput;
	
	public InputField newPasswordInput;
	public InputField recoveryCodeInput;
	
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
	
	public static bool checkInternet(){
		if(Application.internetReachability != NetworkReachability.NotReachable){
			return true;
		} else {
			Debug.Log("No Internet Connection");
			return false;
		}
	}
	
	public static void LoginFromPrefs(){
		if(checkInternet() == false){return;}
		string playerEmail = PlayerPrefs.GetString("PlayerEmail");
		string playerPassword = PlayerPrefs.GetString("PlayerPassword");
		var request = new LoginWithEmailAddressRequest{
			Email = playerEmail,
			Password = playerPassword
		};
		if(PlayerPrefs.HasKey("PlayerEmail")){
			PlayFabClientAPI.LoginWithEmailAddress(request, OnPrefLoginSuccess, OnError);
		}
	}
	
	public static void OnPrefLoginSuccess(LoginResult result){
		//Debug.Log("Login from prefs successful!");
		GetTitleData();
		GetPlayerData();
		if(!PlayerPrefs.HasKey("ContactEmailSet")){
			Debug.Log("No Recovery Email Set.. Let's Set One");
			AddContactEmail();
		}
	}
	
	public void LoginButton(){
		if(checkInternet() == false){return;}
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
		if(checkInternet() == false){return;}
		var request = new GetPlayerProfileRequest{
			PlayFabId = playFabID
		};
		PlayFabClientAPI.GetPlayerProfile(request, OnGetUsernameSuccess, OnError);
	}
	
	static void OnGetUsernameSuccess(GetPlayerProfileResult result){
		string username = result.PlayerProfile.DisplayName;
		if(username.StartsWith("DELETED")){
			Debug.Log("That account was deleted..");
			PlayerPrefs.DeleteKey("PlayerUsername");
			PlayerPrefs.DeleteKey("PlayerEmail");
			PlayerPrefs.DeleteKey("PlayerPassword");
			SceneManager.LoadScene(SceneManager.GetActiveScene().name);
		} else {
			PlayerPrefs.SetString("PlayerUsername", result.PlayerProfile.DisplayName);
			AddContactEmail();
			//Attempt to load saved data
			GetSavedPlayerProgress();
			SceneManager.LoadScene("Menus/MainMenu");
		}
	}
	
	static void AddContactEmail(){
		if(checkInternet() == false){return;}
		var request = new AddOrUpdateContactEmailRequest{
			EmailAddress = PlayerPrefs.GetString("PlayerEmail")
		};
		PlayFabClientAPI.AddOrUpdateContactEmail(request, result => {
			PlayerPrefs.SetString("ContactEmailSet",PlayerPrefs.GetString("PlayerEmail"));
		}, OnError);
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
		if(checkInternet() == false){return;}
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
		if(checkInternet() == false){return;}
		messageText.text = "User Registered Successfully";
		PlayerPrefs.SetString("PlayerUsername", usernameInput.text);
		PlayerPrefs.SetString("PlayerEmail", emailInput.text);
		PlayerPrefs.SetString("PlayerPassword", passwordInput.text);
		LoginFromPrefs();
		SceneManager.LoadScene("Menus/MainMenu");
	}
	
	void OnLoginError(PlayFabError error){
		errorMessageBuffer = error.GenerateErrorReport();
		
		if (errorMessageBuffer.Length > 0){
		   int word = errorMessageBuffer.IndexOf(" ")+1;
		   errorMessageBuffer ="Error: " + errorMessageBuffer.Substring(word);
		}
	}
	
	public static void Logout(){
		PlayerPrefs.DeleteKey("PlayerUsername");
		PlayerPrefs.DeleteKey("PlayerEmail");
		PlayerPrefs.DeleteKey("PlayerPassword");
		SceneManager.LoadScene("MainMenu");
	}
	
	//Junks the username to DelXXXX and logs out.
	public static void DeleteAccount(){
		if(checkInternet() == false){return;}
		string playFabId = PlayerPrefs.GetString("PlayerPlayFabId");
		var request = new UpdateUserTitleDisplayNameRequest{
			DisplayName = "DELETED" + playFabId
		};
		PlayFabClientAPI.UpdateUserTitleDisplayName(request, OnDeletePlayerSuccess, OnError);
	}
	
	public static void OnDeletePlayerSuccess(UpdateUserTitleDisplayNameResult result){
		PlayerPrefs.DeleteKey("PlayerUsername");
		PlayerPrefs.DeleteKey("PlayerEmail");
		PlayerPrefs.DeleteKey("PlayerPassword");
		SceneManager.LoadScene("Menus/MainMenu");
	}
	
	public static void CheckLiveTimeTrial(){
		if(checkInternet() == false){return;}
		PlayFabClientAPI.GetTitleData(new GetTitleDataRequest(), OnLiveTimeTrialReceived, OnTitleError);
	}
	
	public static void OnLiveTimeTrialReceived(GetTitleDataResult result){
		if(checkInternet() == false){return;}
		if(result.Data == null){
			//Debug.Log("No Live Time Trial Found");
			PlayerPrefs.SetString("LatestVersion", Application.version);
			PlayerPrefs.SetInt("LiveTimeTrialActive",0);
			PlayerPrefs.SetString("LiveTimeTrial","");
		}

		//Live Race Time Trial
		if(result.Data.ContainsKey("LiveTimeTrial") == true){
			if(result.Data["TargetVersion"] != ""){
				PlayerPrefs.SetString("LatestVersion", result.Data["TargetVersion"]);
			} else {
				PlayerPrefs.SetString("LatestVersion", Application.version);
			}
			if(result.Data["LiveTimeTrial"] != ""){
				PlayerPrefs.SetString("LiveTimeTrial", result.Data["LiveTimeTrial"]);
				//Debug.Log("Live Time Trial At " + result.Data["LiveTimeTrial"]);
			} else {
				PlayerPrefs.SetInt("LiveTimeTrialActive",0);
				PlayerPrefs.SetString("LiveTimeTrial","");
			}
		} else {
			PlayerPrefs.SetInt("LiveTimeTrialActive",0);
			PlayerPrefs.SetString("LiveTimeTrial","");
		}
	}
	
	public static void GetTitleData(){
		if(checkInternet() == false){return;}
		PlayFabClientAPI.GetTitleData(new GetTitleDataRequest(), OnTitleDataReceived, OnTitleError);
	}
	
	public static void OnTitleDataReceived(GetTitleDataResult result){
		if(checkInternet() == false){return;}
		if(result.Data == null){
			//Debug.Log("No Title Data Found");
			//Remove the last known store values
			PlayerPrefs.SetString("StoreDailySelects", "");
			PlayerPrefs.SetInt("FreeFuel", 0);
			PlayerPrefs.SetInt("ShopDiscount", 0);
			PlayerPrefs.SetInt("EventActive", 0);
			PlayerPrefs.SetString("LiveTimeTrial","");
			PlayerPrefs.SetInt("LiveTimeTrialActive",0);
			PlayerPrefs.SetString("SpecialEvent","");
			PlayerPrefs.SetString("LatestVersion", Application.version);
		}
		
		//Testing
		#if UNITY_EDITOR
		//result.Data["LiveTimeTrialActive"] = "No";
		//result.Data["LiveTimeTrial"] = "Nashville";
		//result.Data["TargetVersion"] = "8.1.12";
		#endif
		
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
			//result.Data["StoreDailySelects"] = "1,2,3,4,5,6,cup221,cup222,cup223,cup224,dmc151,dmc152,dmc153,dmc154,dmc155,cup238,cup239,cup2315,cup2320,cup2322,cup2334,cup23livery34alt1,irl23livery5alt1,irl23livery6alt1,irl23livery7alt1,irl23livery28alt1,cup23livery8alt1,cup23livery9alt1,cup23livery9alt2,cup23livery15alt1,cup23livery4alt2,cup23livery5alt1,cup23livery12alt1,cup23livery20alt2,cup23livery24alt2";
			#endif
			
			PlayerPrefs.SetString("StoreDailySelects", result.Data["StoreDailySelects"]);
			//Debug.Log("Store Updated " + PlayerPrefs.GetString("StoreDailySelects"));
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
		
		if(result.Data.ContainsKey("TargetVersion") == true){
			//Debug.Log("What's the latest version?");
			PlayerPrefs.SetString("LatestVersion", result.Data["TargetVersion"]);
		} else {
			PlayerPrefs.SetString("LatestVersion", Application.version);
		}
		
		//Shop Discount Promo (Everything moves down 1 price tier)
		if(result.Data.ContainsKey("ShopDiscount") == true){
			if(result.Data["ShopDiscount"] == "Yes"){
				PlayerPrefs.SetInt("ShopDiscount", 1);
				//Debug.Log("Shop Discount Activated");
			} else {
				PlayerPrefs.SetInt("ShopDiscount", 0);
			}
		} else {
			PlayerPrefs.SetInt("ShopDiscount", 0);
		}
		
		//Free Modding Weekend etc.
		if(result.Data.ContainsKey("FreeModding") == true){
			if(result.Data["FreeModding"] == "Yes"){
				PlayerPrefs.SetInt("FreeModding", 1);
				//Debug.Log("Free Modding Activated");
			} else {
				PlayerPrefs.SetInt("FreeModding", 0);
			}
		} else {
			PlayerPrefs.SetInt("FreeModding", 0);
		}

		#if UNITY_EDITOR
		//result.Data["LiveTimeTrial"] = "Kansas";
		//result.Data["LiveTimeTrialActive"] = "No";
		//Debug.Log("Time Trial Testing");
		#endif

		//Live Race Time Trial
		if(result.Data.ContainsKey("LiveTimeTrial") == true){
			Debug.Log("There is a Time Trial field..");
			if((result.Data["LiveTimeTrial"] != "")&&(result.Data["LiveTimeTrialActive"] == "Yes")){
				if(isLatestVersion() == true){
					Debug.Log("You are on the latest version, and the TT is active.");
					PlayerPrefs.SetInt("LiveTimeTrialActive",1);
					PlayerPrefs.SetString("LiveTimeTrial", result.Data["LiveTimeTrial"]);
				} else {
					Debug.Log("Time Trial not set (not on latest version)");
					PlayerPrefs.SetInt("LiveTimeTrialActive",0);
					PlayerPrefs.SetString("LiveTimeTrial","");
				}
			} else {
				if(result.Data["LiveTimeTrialActive"] != "Yes"){
					Debug.Log("The TT is not active");
					PlayerPrefs.SetInt("LiveTimeTrialActive",0);
				} else {
					PlayerPrefs.SetInt("LiveTimeTrialActive",1);
				}
				if(result.Data["LiveTimeTrial"] == ""){
					Debug.Log("There's no TT circuit set");
					PlayerPrefs.SetString("LiveTimeTrial","");
				} else {
					PlayerPrefs.SetString("LiveTimeTrial", result.Data["LiveTimeTrial"]);
				}
				Debug.Log("TT: " + PlayerPrefs.GetString("LiveTimeTrial") + " - Active? " + PlayerPrefs.GetString("LiveTimeTrialActive"));
			}
		} else {
			PlayerPrefs.SetInt("LiveTimeTrialActive",0);
			PlayerPrefs.SetString("LiveTimeTrial","");
		}
		
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
					//Debug.Log("New Message!");
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
		}
	}
	
	static void OnTitleError(PlayFabError error){
		if(checkInternet() == false){return;}
		Debug.Log("Something went wrong..");
		Debug.Log(error.GenerateErrorReport());
		
		//Remove active online promos if cannot connect to PlayFab
		PlayerPrefs.SetString("StoreDailySelects", "");
		PlayerPrefs.SetInt("FreeFuel", 0);
	}
	
	public static void GetPlayerData(){
		if(checkInternet() == false){return;}
		PlayFabClientAPI.GetUserData(new GetUserDataRequest(), OnDataReceived, OnError);
	}
	
	static void OnDataReceived(GetUserDataResult result){
		if(checkInternet() == false){return;}
		if(result.Data != null){
			
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
					//Debug.Log("Rewarded Car #" + rewardCarNum);
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
					//Debug.Log("Rewarded Alt #" + rewardAlt);
					
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
			//Debug.Log("No player data found");
		}
	}
	
	public void ResetPassword(){
		if(checkInternet() == false){return;}
		var request = new SendAccountRecoveryEmailRequest{
			Email = emailInput.text,
			TitleId = "5A7C1",
			EmailTemplateId = "2A23AFCBE9D2560C"
		};
		PlayFabClientAPI.SendAccountRecoveryEmail(request, PasswordResetSent, OnError);
	}
	
	static void PasswordResetSent(SendAccountRecoveryEmailResult result){
		if(checkInternet() == false){return;}
		if(result != null){
			Debug.Log(result);
			errorMessageBuffer ="Account Recovery Email has been sent. Follow the instructions in the email to receive a Recovery Code, that can then be used to reset your password.";
			SceneManager.LoadScene("Menus/ResetPassword");
		}
	}

	public void ChangePassword(){
		if(checkInternet() == false){return;}
		StartCoroutine(PostPasswordChange());
	}
	
	IEnumerator PostPasswordChange(){
		
		using UnityWebRequest www = UnityWebRequest.PostWwwForm("https://5A7C1.playfabapi.com/Admin/ResetPassword?Password=" + newPasswordInput.text + "&Token=" + recoveryCodeInput.text,"");
		www.SetRequestHeader("X-SecretKey", "ZNGMGNF3TA3WKBID11HIHAQGARBWZUBKIG9EZKS4DBFAXOCWAA");
		www.SetRequestHeader("Content-Type", "application/json");
		
		errorMessageBuffer = "Waiting..";
		yield return www.SendWebRequest();

		if (www.result != UnityWebRequest.Result.Success){
			errorMessageBuffer = www.error;
		} else {
			errorMessageBuffer = "Password Changed Successfully!";
			Debug.Log("Password changed!");
		}
	}
	
	static void PasswordChanged(SendAccountRecoveryEmailResult result){
		if(checkInternet() == false){return;}
		if(result != null){
			Debug.Log(result);
			errorMessageBuffer ="Your password has been updated successfully.";
		}
	}

	static void receiveCarGift(string car){
		//Example: cup2212
		string rewardCar = car;
		string rewardCarSeries = rewardCar.Substring(0,5);
		//Debug.Log(rewardCar);
		int rewardCarNum = int.Parse(rewardCar.Substring(5));
		if(rewardCar != ""){
			//Debug.Log("Rewarded Car #" + rewardCarNum);
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
		if(checkInternet() == false){return;}
		var request = new UpdateUserDataRequest {
			Data = new Dictionary<string, string> {
				{playerDataKey, "0"}
			}
		};
		PlayFabClientAPI.UpdateUserData(request, OnDataSend, OnError);
	}
	
	public static void GetSavedPlayerProgress(){
		if(checkInternet() == false){return;}
		PlayFabClientAPI.GetUserData(new GetUserDataRequest(), OnProgressReceived, OnError);
	}
	
	static void OnProgressReceived(GetUserDataResult result){
		if(result.Data != null){
			string seriesPrefix = "cup20";
			string playerData = "";
			string seriesData = "";
			string saveType = "";
			int cloudLevel = 0;
			
			//Check the general player account save
			if(result.Data.ContainsKey("AutosavePlayerProgress")){
				//Debug.Log("No manual save.. looking for an autosave");
				playerData = result.Data["AutosavePlayerProgress"].Value;
				Player playerJson = JsonUtility.FromJson<Player>(playerData);
				if(cloudLevel < int.Parse(playerJson.playerLevel)){
					cloudLevel = int.Parse(playerJson.playerLevel);
					//Debug.Log("Autosave is at level " + cloudLevel);
				}
				saveType = "automatic";
			}
			
			//Check the series save
			if(result.Data.ContainsKey("AutosaveSeriesProgress" + seriesPrefix)){
				//Debug.Log("No manual save.. looking for an autosave");
				seriesData = result.Data["AutosaveSeriesProgress" + seriesPrefix].Value;
				Series seriesJson = JsonUtility.FromJson<Series>(seriesData);
				saveType = "automatic";
			}
			
			if(playerData != ""){
				
				//We found some form of save data, but should we load it?
				 PlayerPrefs.SetInt("NewUser",1);
				 Player playerJson = JsonUtility.FromJson<Player>(playerData);
				 int gears = int.Parse(playerJson.playerGears);
				 int money = int.Parse(playerJson.playerMoney);
				 int level = int.Parse(playerJson.playerLevel);
				 int totalStarts = 0;
				 int savedStarts = 0;
				 int totalWins = 0;
				 int savedWins = 0;
				 int garageValue = 0;
				 int savedGarageValue = 0;
				 int transferTokens = 0;
				 int savedTokens = 0;
				 
				 if(playerJson.playerStarts != null){
					totalStarts = int.Parse(playerJson.playerStarts);
					savedStarts = PlayerPrefs.GetInt("TotalStarts");
					if(savedStarts < totalStarts){
						PlayerPrefs.SetInt("TotalStarts", totalStarts);
						savedStarts = PlayerPrefs.GetInt("TotalStarts");
					}
				 }
				 
				 if(playerJson.playerWins != null){
					totalWins = int.Parse(playerJson.playerWins);
					savedWins = PlayerPrefs.GetInt("TotalWins");
					if(savedWins > savedStarts){
						savedWins = savedStarts;
					}
					if(savedWins < totalWins){
						PlayerPrefs.SetInt("TotalWins", totalWins);
						savedWins = PlayerPrefs.GetInt("TotalWins");
					}
					//Bugfix fallback
					if(savedWins > savedStarts){
						PlayerPrefs.SetInt("TotalWins", totalStarts);
						savedWins = PlayerPrefs.GetInt("TotalWins");
					}
				 }
				 
				 if(playerJson.playerGarageValue != null){
					garageValue = int.Parse(playerJson.playerGarageValue);
					savedGarageValue = PlayerPrefs.GetInt("GarageValue");
					if(savedGarageValue < garageValue){
						PlayerPrefs.SetInt("GarageValue", garageValue);
						savedGarageValue = PlayerPrefs.GetInt("GarageValue");
					}
				 }
				 
				 if(playerJson.transferTokens != null){
					transferTokens = int.Parse(playerJson.transferTokens);
					savedTokens = PlayerPrefs.GetInt("TransferTokens");
					if(savedTokens < transferTokens){
						PlayerPrefs.SetInt("TransferTokens", transferTokens);
						savedTokens = PlayerPrefs.GetInt("TransferTokens");
					}
				 }
				 int minTokens = GameData.minTransferTokensFromLevel(level);
				 if(minTokens > savedTokens){
					PlayerPrefs.SetInt("TransferTokens", minTokens);
				 }
				 
				 //PlayerPrefs.GetInt("TotalStarts").ToString() + "\",";
				 //JSONOutput += "\"playerWins\": \"" + PlayerPrefs.GetInt("TotalWins").ToString() + "\",";
				 //JSONOutput += "\"playerGarageValue\": \"" + PlayerPrefs.GetInt("GarageValue").ToString() + "\",";
				 
				 
				 
				 //Almost certainly a fresh install..
				 if((PlayerPrefs.GetInt("PrizeMoney") == 10000)&&(PlayerPrefs.GetInt("Gears") == 10)){
					 
					Debug.Log("Fresh install login");
					 
					//One time grab for fresh installs
					//upon logging back in for the first time
					if(gears > PlayerPrefs.GetInt("Gears")){
						PlayerPrefs.SetInt("Gears", gears);
						Debug.Log("Updated Gears from cloud save");
					}
					if(money > PlayerPrefs.GetInt("PrizeMoney")){
						PlayerPrefs.SetInt("PrizeMoney", money);
						Debug.Log("Updated Money from cloud save");
					} 
				 }
				 
				 
				 if(level > PlayerPrefs.GetInt("Level")){
					PlayerPrefs.SetInt("Level", level);
					//Debug.Log("Your save is not a lower level (" + level + ") than what you already have (" + PlayerPrefs.GetInt("Level") + ").");
				 }
			}
			
			if(seriesData != ""){
				 
				 //Car sets have to be in here to be loaded
				 string[] allSeries = DriverNames.getAllSeries();

				 int unlockedCars = 0;
				 foreach(string series in allSeries){
					 //Debug.Log("Loading " + series);
					 //Drop out if no save for this car set
					 try{
						if(result.Data["AutosaveSeriesProgress" + series].Value == null){
							//break;
							continue;
						}
					 } catch(Exception e){
						 Debug.Log(e.Message);
						 continue;
					 }
					 seriesData = result.Data["AutosaveSeriesProgress" + series].Value;
					 Series seriesJson = JsonUtility.FromJson<Series>(seriesData);
					 
					 for(int i=0;i<=99;i++){
						if(DriverNames.getName(series,i) != null){
							if(PlayerPrefs.GetInt(series + i + "Unlocked") < int.Parse(seriesJson.drivers[i].carUnlocked)){
								PlayerPrefs.SetInt(series + i + "Unlocked", int.Parse(seriesJson.drivers[i].carUnlocked));
								//Debug.Log("New unlock on load, " + series + " #" + i);
							}
							if(seriesJson.drivers[i].carUnlocked == "1"){
								unlockedCars++;
							}
							if(PlayerPrefs.GetInt(series + i + "Class") < int.Parse(seriesJson.drivers[i].carClass)){
								PlayerPrefs.SetInt(series + i + "Class", int.Parse(seriesJson.drivers[i].carClass));
								PlayerPrefs.SetInt(series + i + "Gears", int.Parse(seriesJson.drivers[i].carGears));
								//Debug.Log("Updated class on load, " + series + " #" + i);
							} else {
								if(PlayerPrefs.GetInt(series + i + "Class") == int.Parse(seriesJson.drivers[i].carClass)){
									//If the class hasn't changed but the gears have increased
									if(PlayerPrefs.GetInt(series + i + "Gears") < int.Parse(seriesJson.drivers[i].carGears)){
										PlayerPrefs.SetInt(series + i + "Gears", int.Parse(seriesJson.drivers[i].carGears));
									}
								}
							}
							string altsList = seriesJson.drivers[i].altPaints;
							if(altsList != "0"){
								string[] altsArray = altsList.Split(',');
								foreach(string alt in altsArray){
									//Debug.Log(series + " #" + i + " alt " + alt + " alts loop");
									if(int.Parse(alt) > 0){
										PlayerPrefs.SetInt(series + i + "Alt" + alt + "Unlocked", 1);
										//Debug.Log("Unlocked Alt on load, " + series + " #" + i + " alt " + alt);
									}
								}
							}
						}
					 }
				 }
				 //Debug.Log("Loaded data from server! " + unlockedCars + " unlocked cars.");
				 PlayerPrefs.SetString("LoadOutput","Loaded " + saveType + " save - " + unlockedCars + " unlocked cars.");
			} else {
				//Debug.Log("No player data found");
				PlayerPrefs.SetString("LoadOutput","No autosave or manual save data found for this player account.");
			}
		} else {
			//Debug.Log("No player data found");
		}
		
		//Save this newly merged load back to PlayFab
		autosaveGarageToCloud();
	}
	
	public static void OnDataSend(UpdateUserDataResult result){
		//Debug.Log("Rewards Collected, Server Reset");
	}
	
	public static void AutosavePlayerProgress(string progressJSON){
		if(checkInternet() == false){return;}
		var request = new UpdateUserDataRequest {
			Data = new Dictionary<string, string> {
				{"AutosavePlayerProgress", progressJSON}
			}
		};
		//Debug.Log("Saved player progress to cloud");
		PlayFabClientAPI.UpdateUserData(request, OnProgressSave, OnError);
	}
	
	public static void AutosaveSeriesProgress(string seriesPrefix, string progressJSON){
		if(checkInternet() == false){return;}
		var request = new UpdateUserDataRequest {
			Data = new Dictionary<string, string> {
				{"AutosaveSeriesProgress" + seriesPrefix, progressJSON}
			}
		};
		//Debug.Log("Saved series " + seriesPrefix + " progress to cloud");
		PlayFabClientAPI.UpdateUserData(request, OnProgressSave, OnError);
	}
	
	public static void SavePlayerProgress(string seriesPrefix, string progressJSON){
		if(checkInternet() == false){return;}
		var request = new UpdateUserDataRequest {
			Data = new Dictionary<string, string> {
				{"SavedPlayerProgress" + seriesPrefix, progressJSON}
			}
		};
		PlayFabClientAPI.UpdateUserData(request, OnProgressSave, OnError);
	}
	
	public static void OnProgressSave(UpdateUserDataResult result){
		//Debug.Log("Player Progress Saved");
		PlayerPrefs.SetString("SaveOutput","Saved progress to the server");
	}
	
	public static void autosaveGarageToCloud(){
		if(checkInternet() == false){return;}
		
		//Count Unlocks..
		int level = PlayerPrefs.GetInt("Level");
		int totalUnlocks = 0;
		
		//Add all autosavable series here
		string[] allSeries = DriverNames.getAllSeries();
		
		foreach(string series in allSeries){
			for(int i=0;i<100;i++){
				if(DriverNames.getName(series, i) != null){
					if(PlayerPrefs.GetInt(series + i + "Unlocked") == 1){
						totalUnlocks++;
					}
				}
			}
		}

		//If logged in as someone
		if(PlayerPrefs.HasKey("PlayerUsername")){
			
			garageValue = 0;
			//Try an autosave
			
			string playerProgressJSON = JSONifyPlayerProgress();
			PlayFabManager.AutosavePlayerProgress(playerProgressJSON);
			
			foreach(string series in allSeries){
				string progressJSON = JSONifySeriesProgress(series);
				try {
					PlayFabManager.AutosaveSeriesProgress(series, progressJSON);
				}
				catch(Exception e){
					Debug.Log("Cannot reach PlayFab");
				}
			}
			//Debug.Log("Garage Value: " + garageValue);
			PlayerPrefs.SetInt("GarageValue",garageValue);
		}
	}

	public static string JSONifyPlayerProgress(){
		string JSONOutput = "{";
		JSONOutput += "\"playerLevel\": \"" + PlayerPrefs.GetInt("Level").ToString() + "\",";
		JSONOutput += "\"playerMoney\": \"" + PlayerPrefs.GetInt("PrizeMoney").ToString() + "\",";
		JSONOutput += "\"playerGears\": \"" + PlayerPrefs.GetInt("Gears").ToString() + "\",";
		JSONOutput += "\"playerStarts\": \"" + PlayerPrefs.GetInt("TotalStarts").ToString() + "\",";
		JSONOutput += "\"playerWins\": \"" + PlayerPrefs.GetInt("TotalWins").ToString() + "\",";
		JSONOutput += "\"playerGarageValue\": \"" + PlayerPrefs.GetInt("GarageValue").ToString() + "\",";
		JSONOutput += "\"transferTokens\": \"" + PlayerPrefs.GetInt("TransferTokens").ToString() + "\"}";
		return JSONOutput;
	}

	public static string JSONifySeriesProgress(string seriesPrefix){
		string JSONOutput = "{";
		JSONOutput += "\"seriesName\": \"" + seriesPrefix + "\",";
		JSONOutput += "\"drivers\": [";
		int totalCars = 0;
		int unlockedCars = 0;
		for(int car = 0; car < 100; car++){
			//Initialise (can be used for dev reset)
			int carUnlocked = 0;
			int carClass = 0;
			int carGears = 0;
			if(PlayerPrefs.HasKey(seriesPrefix + car + "Gears")){
				carUnlocked = PlayerPrefs.GetInt(seriesPrefix + car + "Unlocked");
				carClass = PlayerPrefs.GetInt(seriesPrefix + car + "Class");
				carGears = PlayerPrefs.GetInt(seriesPrefix + car + "Gears");
				totalCars++;
				if(carUnlocked == 1){
					int carRarity = DriverNames.getRarity(seriesPrefix,car);
					garageValue += (GameData.unlockGears(carClass) * carRarity) + carGears;
					unlockedCars++;
				}
			}
			if(car > 0){
				JSONOutput += ",";
			}
			JSONOutput += "{";
			JSONOutput += "\"carNo\": \"" + car + "\",";
			JSONOutput += "\"carUnlocked\": \"" + carUnlocked + "\",";
			JSONOutput += "\"carClass\": \"" + carClass + "\",";
			JSONOutput += "\"carGears\": \"" + carGears + "\",";
			JSONOutput += "\"altPaints\": \"0";
			for(int paint=1;paint<10;paint++){
				if(AltPaints.getAltPaintName(seriesPrefix,car,paint) != null){
					if(PlayerPrefs.GetInt(seriesPrefix + car + "Alt" + paint + "Unlocked") == 1){
						JSONOutput += "," + paint + "";
					}
				}
			}
			JSONOutput += "\"";
			JSONOutput += "}";		
		}
		JSONOutput += "],";
		JSONOutput += "\"totalCars\": \"" + unlockedCars + "\"}";
		return JSONOutput;
	}
	
	public static void SendLeaderboard(int score, string circuitName, string prefix){
		if(checkInternet() == false){return;}
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
			//Debug.Log("Sent " + score + " To Leaderboard " + circuitName + ".");
		} catch (Exception e){
			//Debug.Log("Cannot reach Playfab to send " + score + " time to " + circuitName);
		}
	}
	
	static void OnLeaderboardUpdate(UpdatePlayerStatisticsResult result){
		//Debug.Log("Leaderboard Updated.");
	}
	
	public static void GetLeaderboard(string circuit){
		if(checkInternet() == false){return;}
		
		var request = new GetLeaderboardRequest {
			StatisticName = "FastestLap" + circuit,
			StartPosition = 0,
			MaxResultsCount = 5
		};
		PlayFabClientAPI.GetLeaderboard(request, OnLeaderboardGet, OnError);
	}
	
	public static void GetLeaderboardAroundPlayer(string circuit){
		if(checkInternet() == false){return;}
		
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
			
			//Debug.Log(item.Position + " " + item.PlayFabId + " " + item.StatValue);
		}
	}
	
	static void OnLeaderboardAroundPlayerGet(GetLeaderboardAroundPlayerResult result) {
		
		//Debug.Log("Got Leaderboard Around Player");
		
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
			
			//Debug.Log(item.Position + " " + item.PlayFabId + " " + item.StatValue);
		}
	}
	
	public static void GetRecordLeaderboard(string name){
		if(checkInternet() == false){return;}
		
		var request = new GetLeaderboardRequest {
			StatisticName = name,
			StartPosition = 0,
			MaxResultsCount = 50
		};
		PlayFabClientAPI.GetLeaderboard(request, OnRecordLeaderboardGet, OnError);
	}
	
	static void OnRecordLeaderboardGet(GetLeaderboardResult result) {
		
		foreach(var item in result.Leaderboard) {
			GameObject tableRows = Instantiate(rowPrefab, rowsParent);
			Text[] tableLabels = tableRows.GetComponentsInChildren<Text>();
			tableLabels[0].text = (item.Position + 1).ToString();
			tableLabels[1].text = item.DisplayName;
			tableLabels[2].text = item.StatValue.ToString();
			
			//Debug.Log(item.Position + " " + item.PlayFabId + " " + item.StatValue);
		}
	}
	
	public static void GetRecordAroundPlayer(string name){
		if(checkInternet() == false){return;}
		
		var request = new GetLeaderboardAroundPlayerRequest {
			StatisticName = name,
			MaxResultsCount = 1
		};
		PlayFabClientAPI.GetLeaderboardAroundPlayer(request, OnRecordAroundPlayerGet, OnError);
	}
	
	static void OnRecordAroundPlayerGet(GetLeaderboardAroundPlayerResult result) {
		
		//Debug.Log("Got Leaderboard Around Player");
		foreach(Transform item in rowsParent){
			Destroy(item.gameObject);
		}
		
		foreach(var item in result.Leaderboard) {
			GameObject tableRows = Instantiate(rowPrefab, rowsParent);
			Text[] tableLabels = tableRows.GetComponentsInChildren<Text>();
			tableLabels[0].text = (item.Position + 1).ToString();
			tableLabels[1].text = item.DisplayName;
			tableLabels[2].text = item.StatValue.ToString();
			
			if(item.PlayFabId.ToString() == PlayerPrefs.GetString("PlayerPlayFabId")){
				tableLabels[0].color = Color.red;
				tableLabels[1].color = Color.red;
				tableLabels[2].color = Color.red;
			} else {
				
			}
			
			//Debug.Log(item.Position + " " + item.PlayFabId + " " + item.StatValue);
		}
	}
	
	public static void GetLiveTimeTrialLeaderboard(){
		if(checkInternet() == false){return;}
		
		var request = new GetLeaderboardRequest {
			StatisticName = "FastestLapChallenge",
			StartPosition = 0,
			MaxResultsCount = 50
		};
		PlayFabClientAPI.GetLeaderboard(request, OnLiveTimeTrialLeaderboardGet, OnError);
	}
	
	static void OnLiveTimeTrialLeaderboardGet(GetLeaderboardResult result) {
		
		foreach(var item in result.Leaderboard) {
			GameObject tableRows = Instantiate(rowPrefab, rowsParent);
			Text[] tableLabels = tableRows.GetComponentsInChildren<Text>();
			tableLabels[0].text = (item.Position + 1).ToString();
			tableLabels[1].text = item.DisplayName;
			float leaderboardTime = (100000 - item.StatValue)/1000f;
			if(leaderboardTime == 0){
				leaderboardTime = 999.999f;
			}
			tableLabels[2].text = leaderboardTime.ToString() + "s";
		}
	}
	
	public static void GetLiveTimeTrialAroundPlayer(){
		if(checkInternet() == false){return;}
		
		var request = new GetLeaderboardAroundPlayerRequest {
			StatisticName = "FastestLapChallenge",
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
			float leaderboardTime = (100000 - item.StatValue)/1000f;
			if(leaderboardTime == 0){
				leaderboardTime = 999.999f;
			}
			tableLabels[2].text = leaderboardTime.ToString() + "s";
			
			if(item.PlayFabId.ToString() == PlayerPrefs.GetString("PlayerPlayFabId")){
				tableLabels[0].color = Color.red;
				tableLabels[1].color = Color.red;
				tableLabels[2].color = Color.red;
			} else {
				
			}
		}
		GetLiveTimeTrialLeaderboard();
	}
	
	public static bool isLatestVersion(){
		//Debug.Log("Latest Version? " + PlayerPrefs.GetString("LatestVersion"));
		if(PlayerPrefs.GetString("LatestVersion") == ""){
			return true;
		}
		string[] latestSubVersions = PlayerPrefs.GetString("LatestVersion").Split(".");
		string[] currentSubVersions = Application.version.Split(".");
		for(int i=0;i<latestSubVersions.Length;i++){
			//Lower version number
			if(int.Parse(latestSubVersions[i]) > int.Parse(currentSubVersions[i])){
				return false;
			}
			//Higher version number
			if(int.Parse(latestSubVersions[i]) < int.Parse(currentSubVersions[i])){
				return true;
			}
			//Else.. number matches, so search further along the string
		}
		return true;
	}
}