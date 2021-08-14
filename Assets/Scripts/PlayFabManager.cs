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
	
	public Text messageText;
	public InputField usernameInput;
	public InputField emailInput;
	public InputField passwordInput;
	
	public GameObject thisRowPrefab;
	public Transform thisRowsParent;
	
    // Start is called before the first frame update
    void Start(){
	   rowPrefab = thisRowPrefab;
	   rowsParent = thisRowsParent;
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
	}
	
	public void LoginButton(){
		var request = new LoginWithEmailAddressRequest{
			Email = emailInput.text,
			Password = passwordInput.text
		};
		PlayFabClientAPI.LoginWithEmailAddress(request, OnLoginSuccess, OnError);
	}
	
	void OnLoginSuccess(LoginResult result){
		Debug.Log("Login successful!");
		PlayerPrefs.SetString("PlayerUsername", usernameInput.text);
		PlayerPrefs.SetString("PlayerEmail", emailInput.text);
		PlayerPrefs.SetString("PlayerPassword", passwordInput.text);
		SceneManager.LoadScene("MainMenu");
	}
	
	static void OnError(PlayFabError error){
		Debug.Log("Something went wrong..");
		Debug.Log(error.GenerateErrorReport());
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
		SceneManager.LoadScene("MainMenu");
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
		}
		
		//Custom store items in Daily Selects
		if(result.Data.ContainsKey("StoreDailySelects") == false){
			Debug.Log("No online Store Daily Selects");
			//Remove the last known store values
			PlayerPrefs.SetString("StoreDailySelects", "");
		} else {
			PlayerPrefs.SetString("StoreDailySelects", result.Data["StoreDailySelects"]);
			Debug.Log("Store Updated " + PlayerPrefs.GetString("StoreDailySelects"));
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
		
		//Shop Discount Promo (~40%)
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
	}
	
	static void OnTitleError(PlayFabError error){
		Debug.Log("Something went wrong..");
		Debug.Log(error.GenerateErrorReport());
		
		//Remove active online promos if cannot connect to PlayFab
		PlayerPrefs.SetString("StoreDailySelects", "");
		PlayerPrefs.SetInt("FreeFuel", 0);
	}
	
	public static void SendLeaderboard(int score, string circuitName){
		var request = new UpdatePlayerStatisticsRequest {
			Statistics = new List<StatisticUpdate> {
				new StatisticUpdate {
					StatisticName = "FastestLap" + circuitName + "",
					Value = score
				}
			}
		};
		PlayFabClientAPI.UpdatePlayerStatistics(request, OnLeaderboardUpdate, OnError);
		//Debug.Log("FastestLap" + circuitName);
	}
	
	static void OnLeaderboardUpdate(UpdatePlayerStatisticsResult result){
		//Debug.Log("Leaderboard Updated " + result);
	}
	
	public static void GetLeaderboard(string circuit){
		
		var request = new GetLeaderboardRequest {
			StatisticName = "FastestLap" + circuit,
			StartPosition = 0,
			MaxResultsCount = 10
		};
		PlayFabClientAPI.GetLeaderboard(request, OnLeaderboardGet, OnError);
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
}