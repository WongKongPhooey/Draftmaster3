using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.SceneManagement;

public class IAPStore : MonoBehaviour {
	
	public static int gears;
	public static int transfersMax;
	public static int transfersLeft;
	
	private const string gears60 = "com.duffetywong.draftmaster2rollingthunder.gears60";
	private const string gears125 = "com.duffetywong.draftmaster2rollingthunder.gears125";
	private const string gears200 = "com.duffetywong.draftmaster2rollingthunder.gears200";
	private const string gears500 = "com.duffetywong.draftmaster2rollingthunder.gears500";
	
	private const string smallgears = "com.duffetywong.draftmaster2rollingthunder.smallgears";
	private const string mediumgears = "com.duffetywong.draftmaster2rollingthunder.mediumgears";
	private const string largegears = "com.duffetywong.draftmaster2rollingthunder.largegears";
	private const string extralargegears = "com.duffetywong.draftmaster2rollingthunder.extralargegears";
	
	private const string negotiator = "com.duffetywong.draftmaster2rollingthunder.negotiator";
	private const string negotiatorios = "com.duffetywong.draftmaster2rollingthunder.negotiatorios";
	
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
		
			
		GUI.skin.label.fontSize = 96 / FontScale.fontScale;

		GUI.skin.label.normal.textColor = Color.black;

		GUI.skin.label.alignment = TextAnchor.UpperLeft;
		GUI.Label(new Rect(widthblock * 4, 20, widthblock * 5, heightblock * 2), "Premium Store");

		if (Input.GetKeyDown(KeyCode.Escape)){
			SceneManager.LoadScene("Store");
		}
	}
	
	public void OnPurchaseComplete(Product product){
		
		gears = PlayerPrefs.GetInt("Gears");
		transfersMax = PlayerPrefs.GetInt("TransferTokens");
		transfersLeft = PlayerPrefs.GetInt("TransfersLeft");
		
		switch(product.definition.id){
			case gears60:
				Debug.Log("Added 80 gears");
				gears+=80;
				PlayerPrefs.SetInt("Gears",gears);
				break;
			case gears125:
				Debug.Log("Added 250 gears");
				gears+=250;
				PlayerPrefs.SetInt("Gears",gears);
				break;
			case gears200:
				Debug.Log("Added 600 gears");
				gears+=600;
				PlayerPrefs.SetInt("Gears",gears);
				break;
			case gears500:
				Debug.Log("Added 1500 gears");
				gears+=1500;
				PlayerPrefs.SetInt("Gears",gears);
				break;
			case smallgears:
				Debug.Log("Added 80 gears");
				gears+=80;
				PlayerPrefs.SetInt("Gears",gears);
				break;
			case mediumgears:
				Debug.Log("Added 250 gears");
				gears+=250;
				PlayerPrefs.SetInt("Gears",gears);
				break;
			case largegears:
				Debug.Log("Added 600 gears");
				gears+=600;
				PlayerPrefs.SetInt("Gears",gears);
				break;
			case extralargegears:
				Debug.Log("Added 1500 gears");
				gears+=1500;
				PlayerPrefs.SetInt("Gears",gears);
				break;
			case negotiator:
			case negotiatorios:
				Debug.Log("Added 999 contracts");
				transfersMax=999;
				transfersLeft=999;
				PlayerPrefs.SetInt("TransferTokens",transfersMax);
				PlayerPrefs.SetInt("TransfersLeft",transfersLeft);
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
