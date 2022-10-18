using UnityEngine;
using UnityEngine.Advertisements;
 
public class AdsInitializer : MonoBehaviour, IUnityAdsInitializationListener
{
    [SerializeField] bool _testMode = false;
    private string _gameId;
	
	[SerializeField] RewardedAdsButton rewardedAdsButton;
 
    void Awake()
    {
        InitializeAds();
    }
 
    public void InitializeAds()
    {
        _gameId = "4061301";
        
        #if UNITY_IOS
        _gameId = "4061300";
        #endif
        
        Advertisement.Initialize(_gameId, _testMode, this);
		rewardedAdsButton.LoadAd();
    }
 
    public void OnInitializationComplete()
    {
        Debug.Log("Unity Ads initialization complete.");
		rewardedAdsButton.LoadAd();
    }
 
    public void OnInitializationFailed(UnityAdsInitializationError error, string message)
    {
        Debug.Log($"Unity Ads Initialization Failed: {error.ToString()} - {message}");
    }
}