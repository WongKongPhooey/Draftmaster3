using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Linq;

public class ModsUI : MonoBehaviour {
	
	public GameObject pathName;
	public GameObject modRow;
	public Transform modsFrame;
	
	public GameObject alertPopup;
	
	public string pickedJSON;
	public string pickedCarPNG;
	
	public bool fileQueued;
	
    // Start is called before the first frame update
	void Start(){
        //Debug.Log(Application.persistentDataPath);
		TMPro.TMP_Text pathNameText = pathName.GetComponent<TMPro.TMP_Text>();
		pathNameText.text = Application.persistentDataPath + "/Mods";
		LoadMods();
		
		pickedJSON = null;
		pickedCarPNG = null;
		fileQueued = false;
    }

    // Update is called once per frame
    void Update(){
		//Keep checking for an uploaded file..
		bool isPicking = NativeFilePicker.IsFilePickerBusy();
		
		if(isPicking == false){
			//A Config file waiting
			if(pickedJSON != null){
				writeJSONToFolder(pickedJSON);
				fileQueued = false;
			}
			
			//A single car file waiting
			if(pickedCarPNG != null){
				writeCarToFolder(pickedCarPNG);
			}
			fileQueued = false;
		}
    }
	
	void LoadMods(){
		
		//Reset mods list to blank
		foreach (Transform child in modsFrame){
			Destroy(child.gameObject);
		}
		PlayerPrefs.DeleteKey("ModsList");
		
        if (Directory.Exists(Application.persistentDataPath)){
			
			DirectoryInfo d;
			if(!Directory.Exists(Application.persistentDataPath + "/Mods")){
				d = Directory.CreateDirectory(Application.persistentDataPath + "/Mods"); // returns a DirectoryInfo object
			} else {
				d = new DirectoryInfo(Application.persistentDataPath + "/Mods");
			}
			
			int i=0;
			string modList = "";
			foreach (var directory in d.GetDirectories()){
                //Avoid these default folders
				//All mod folders must be 5 characters long
				if((directory.Name == "Unity")||
				   (directory.Name == "il2cpp")||
				   (DriverNames.isOfficialSeries(directory.Name) == true)||
				   (directory.Name.Length > 5)){
					continue;
				}
				if(modList != ""){
					modList += ",";
				}
				
				string modJsonRaw;
				try {
					modJsonRaw = loadJson(directory.Name);
				} catch(Exception e){
					modJsonRaw = directory.Name;
				}
				
				string modFullName;
				string modAuthor;
				string modJsonValid;
				int totalCars = 0;
				
				//Check the json is valid
				try {
					modCarset modJson = JsonUtility.FromJson<modCarset>(modJsonRaw);
					modFullName = stringLimit(modJson.modName,8);
					modAuthor = stringLimit(modJson.modAuthor,14);					
					modJsonValid = "OK";
					
					modList += directory.Name + "-" + modFullName;
				} catch(Exception e){
					modFullName = "?";
					modAuthor = "?";
					modJsonValid = "Error";
				}
				
				GameObject modInst = Instantiate(modRow, new Vector3(transform.position.x,transform.position.y, transform.position.z) , Quaternion.identity);
				RectTransform modObj = modInst.GetComponent<RectTransform>();
				modInst.transform.SetParent(modsFrame, false);
				modInst.GetComponent<UIAnimate>().animOffset = i+1;
				modInst.GetComponent<UIAnimate>().scaleIn();
				
				TMPro.TMP_Text modName = modInst.transform.GetChild(0).GetComponent<TMPro.TMP_Text>();
				TMPro.TMP_Text modAuthorTxt = modInst.transform.GetChild(1).GetComponent<TMPro.TMP_Text>();
				TMPro.TMP_Text modSize = modInst.transform.GetChild(2).GetComponent<TMPro.TMP_Text>();
				TMPro.TMP_Text modJsonTxt = modInst.transform.GetChild(3).GetComponent<TMPro.TMP_Text>();
				TMPro.TMP_Text modFolder = modInst.transform.GetChild(4).GetComponent<TMPro.TMP_Text>();
				
				modName.text = modFullName;
				modAuthorTxt.text = modAuthor;
				modJsonTxt.text = modJsonValid;
				modFolder.text = directory.Name;
				
				int modCars=0;
				foreach (var file in directory.GetFiles("*.png")){
					modCars++;
				}
				//Debug.Log(modCars);
				modSize.text = modCars.ToString();
				
				i++;
            }
			PlayerPrefs.SetString("ModsList",modList);
        }
    }
	
	public string loadJson(string folderName){
		
		TextAsset jsonFile;

		string directoryPath = Application.persistentDataPath + "/Mods/" + folderName;
		System.IO.Directory.CreateDirectory(directoryPath);
		string json = System.IO.File.ReadAllText(directoryPath + "/" + folderName + ".json");

		try{
			modCarset modJson = JsonUtility.FromJson<modCarset>(json);
			string modName = modJson.modName;
		} catch(Exception e){
			string jsonValid = "Error";
		}
		
		//Debug.Log(json);
		return json;
	}
	
	public static void openCreate(){
		PromptManager.showPopup();
	}
	
	public void createModFolder(){
		DirectoryInfo d;
		GameObject promptInputValue = GameObject.Find("PromptInputValue");
		string folderName = promptInputValue.GetComponent<TMPro.TMP_Text>().text;
		if(folderName != null){
			if(!Directory.Exists(Application.persistentDataPath + "/Mods/" + folderName)){
				d = Directory.CreateDirectory(Application.persistentDataPath + "/Mods/" + folderName); // returns a DirectoryInfo object
			}
			LoadMods();
			PromptManager.hidePopup();
		}
	}
	
	public void pickJsonFile(){
		string fileType = NativeFilePicker.ConvertExtensionToFileType("txt");
		pickedJSON = null;
		
		NativeFilePicker.Permission hasPermission = NativeFilePicker.CheckPermission();
		//Debug.Log(hasPermission);
		if(hasPermission != NativeFilePicker.Permission.Granted){
			NativeFilePicker.Permission askedPermission = NativeFilePicker.RequestPermission();
			//Debug.Log(askedPermission);
		}
		
		NativeFilePicker.Permission permission = NativeFilePicker.PickFile((path) => {
			if(path != null){
				pickedJSON = path;
				//Debug.Log("Picked file: " + path);
			} else {
				return;
			}
		}, new string[]{ fileType });
		
		fileQueued = true;
	}
	
	public void pickCarFiles(){
		Texture2D carTex = null;
		pickedCarPNG = null;

		#if UNITY_ANDROID
			// Use MIMEs on Android
			string fileType = "image/*";
		#else
			// Use UTIs on iOS
			string fileType = "public.image";
		#endif
		
		NativeFilePicker.Permission hasPermission = NativeFilePicker.CheckPermission();
		if(hasPermission != NativeFilePicker.Permission.Granted){
			NativeFilePicker.Permission askedPermission = NativeFilePicker.RequestPermission();
		}
		
		NativeFilePicker.Permission permission = NativeFilePicker.PickFile((path) => {
			if(path != null){
				pickedCarPNG = path;
				Debug.Log("Picked file: " + path);
			}
		}, new string[]{ fileType });
	}
	
	public void writeJSONToFolder(string pickedTxt){
		string json = System.IO.File.ReadAllText(pickedTxt);
		string modName = null;
		string modFolder = null;
		pickedJSON = null;
		try{
			modCarset modJson = JsonUtility.FromJson<modCarset>(json);
			modName = modJson.modName;
			modFolder = modJson.modFolder;
		} catch(Exception e){
			alertPopup.GetComponent<AlertManager>().showPopup("JSON Upload Failed","The JSON file contains errors. Try using an online JSON validator to check.","dm2logo");
			return;
		}
		if(modName == null){
			alertPopup.GetComponent<AlertManager>().showPopup("JSON Upload Failed","The JSON file uploaded has no mod name specified. Download the example files to check the format required.","dm2logo");
			return;
		}
		if(modFolder == null){
			alertPopup.GetComponent<AlertManager>().showPopup("JSON Upload Failed","The JSON file uploaded has no folder specified. Download the example files to check the format required.","dm2logo");
			return;
		}
		
		string directoryPath = Application.persistentDataPath + "/Mods/" + modFolder;
		if(!Directory.Exists(directoryPath)){
			System.IO.Directory.CreateDirectory(directoryPath);
		}
		System.IO.File.WriteAllText(directoryPath + "/" + modFolder + ".json",json);
		LoadMods();
		alertPopup.GetComponent<AlertManager>().showPopup("JSON File Uploaded","A JSON file has been uploaded for the " + modFolder + " mod","dm2logo");
	}
	
	public void writeCarToFolder(string pickedCar){
		string[] pathDepths = pickedCar.Split("/");
		string fileName = pathDepths[pathDepths.Length - 1];
		string modFolder = fileName.Substring(0,5);
		string carNum = fileName.Substring(6);
		carNum = carNum.Split(".")[0];
		pickedCarPNG = null;
		bool hasNum = int.TryParse(carNum,out int carNumInt);
		if(hasNum == false){
			alertPopup.GetComponent<AlertManager>().showPopup("Car Upload Failed","File " + fileName + " is not in the correct format.\n\nExample: cup15-43.png","dm2logo");
			return;
		}
		byte[] bytes = System.IO.File.ReadAllBytes(pickedCar);
			
		string directoryPath = Application.persistentDataPath + "/Mods/" + modFolder;
		if(!Directory.Exists(Application.persistentDataPath + "/Mods/" + modFolder)){
			//Debug.Log("No mod folder " + modFolder + " was found.");
			alertPopup.GetComponent<AlertManager>().showPopup("Car Upload Failed","Folder " + modFolder + " was not found. Create this folder, or upload a " + modFolder + ".txt config file first.","dm2logo");
		} else {
			System.IO.File.WriteAllBytes(directoryPath + "/" + fileName,bytes);
			LoadMods();
			Texture2D uploadedCar = ModData.getTexture(modFolder,carNumInt);
			alertPopup.GetComponent<AlertManager>().showPopup("Car Uploaded","Car #" + carNum + " has been uploaded to the " + modFolder + " mod!","",false,0,999,uploadedCar);
		}
	}
	
	public static string stringLimit(string data, int limit){
		if(data.Length > limit){
			data = data.Substring(0,limit);
		}
		return data;
	}
}