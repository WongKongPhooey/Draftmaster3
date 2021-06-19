using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;

public class PlayFabManager : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
       Login();
    }
	
	void Login(){
		var request = new LoginWithCustomIDRequest{
			CustomId = SystemInfo.deviceUniqueIdentifier,
			CreateAccount = true
		};
		PlayFabClientAPI.LoginWithCustomID(request, OnSuccess, OnError);
	}
	
	void OnSuccess(LoginResult result){
		Debug.Log("Login successful!");
		PlayFabManager.GetLeaderboard("Daytona");
	}
	
	static void OnError(PlayFabError error){
		Debug.Log("Something went wrong..");
		Debug.Log(error.GenerateErrorReport());
	}
	
	public static void SendLeaderboard(int score, string circuitName){
		var request = new UpdatePlayerStatisticsRequest {
			Statistics = new List<StatisticUpdate> {
				new StatisticUpdate {
					StatisticName = "FastestLapDaytona",
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
		foreach(var item in result.Leaderboard) {
			Debug.Log(item.Position + " " + item.PlayFabId + " " + item.StatValue);
		}
	}
}