using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.SceneManagement;

public class IAPStore : MonoBehaviour {
	
	private const string gears20 = "com.DuffetyWong.Draftmaster2RollingThunder.gears20";
	private const string gears60 = "com.DuffetyWong.Draftmaster2RollingThunder.gears60";
	private const string gears125 = "com.DuffetyWong.Draftmaster2RollingThunder.gears125";
	public GameObject restorePurchaseBtn;
	
	public GUISkin buttonSkin;
	public GUISkin redGUI;

	float widthblock = Mathf.Round(Screen.width/20);
	float heightblock = Mathf.Round(Screen.height/20);
	
	private void Awake(){
		DisableRestorePurchase();
	}
	
	void OnGUI(){
		GUI.skin = redGUI;
		GUI.skin.button.fontSize = 64 / FontScale.fontScale;
	
		CommonGUI.BackButton("Store");
		
		GUI.skin = buttonSkin;
		GUI.skin.button.fontSize = 64 / FontScale.fontScale;
		GUI.skin.button.alignment = TextAnchor.MiddleLeft;
		
		CommonGUI.TopBar();

		if (Input.GetKeyDown(KeyCode.Escape)){
			SceneManager.LoadScene("Store");
		}
	}
	
	public void OnPurchaseComplete(Product product){
		switch(product.definition.id){
			case gears20:
				Debug.Log("Added 20 gears");
				break;
			case gears60:
				Debug.Log("Added 60 gears");
				break;
			case gears125:
				Debug.Log("Added 125 gears");
				break;
			default:
				break;
		}
	}
	
	public void OnPurchaseFailed(Product product, PurchaseFailureReason reason){
		Debug.Log("Purchase of " + product.definition.id + " failed. Reason: " + reason);
	}
	
	private void DisableRestorePurchase(){
		if(Application.platform != RuntimePlatform.IPhonePlayer){
			restorePurchaseBtn.SetActive(false);
		}
	}
}
