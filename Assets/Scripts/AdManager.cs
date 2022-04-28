using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Services.Mediation;

public class AdManager : MonoBehaviour
{
	#if UNITY_IOS
    private string gameId = "4061300";
    #elif UNITY_ANDROID
    private string gameId = "4061301";
    #endif

    static string myPlacementId = "rewardedVideo";
	public static int fuel;
	public static bool advertLocked;
    bool testMode = false;

    // Initialize the Ads listener and service:
    void Start () {
        //Advertisement.AddListener (this);
        //Advertisement.Initialize (gameId, testMode);	
    }

	/*public static void ShowRewardedVideo(){
		if(Advertisement.IsReady(myPlacementId)){
			advertLocked = true;
			fuel = PlayerPrefs.GetInt("GameFuel");
			Advertisement.Show(myPlacementId);
		} else {
			Debug.Log("Ad could not be loaded");
		}
	}

    // Implement IUnityAdsListener interface methods:
    public void OnUnityAdsDidFinish (string placementId, ShowResult showResult) {
        // Define conditional logic for each ad completion status:
        if (showResult == ShowResult.Finished) {
            // Reward the user for watching the ad to completion.
			if(advertLocked == true){
				fuel+=10;
				if(fuel > GameData.maxFuel){
					fuel = GameData.maxFuel;
				}
				Store.fuelOutput = "Fuel Added!";
				PlayerPrefs.SetInt("GameFuel",fuel);
				GameData.gameFuel = fuel;
				advertLocked = false;
			}
        } else if (showResult == ShowResult.Failed) {
            Debug.LogWarning ("The ad failed due to an error");
			advertLocked = false;
        } else {
			advertLocked = false;
		}
		Store.adWindow = false;
		Store.adFallbackStatic.SetActive(false);
    }

    public void OnUnityAdsReady (string placementId) {
        // If the ready Placement is rewarded, show the ad:
        if (placementId == myPlacementId) {
            //Advertisement.Show (myPlacementId);
        }
    }

    public void OnUnityAdsDidError (string message) {
        // Log the error.
    }

    public void OnUnityAdsDidStart (string placementId) {
        // Optional actions to take when the end-users triggers an ad.
    }*/
}