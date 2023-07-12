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
	
    // Start is called before the first frame update
	void Start(){
        //Debug.Log(Application.persistentDataPath);
		TMPro.TMP_Text pathNameText = pathName.GetComponent<TMPro.TMP_Text>();
		pathNameText.text = Application.persistentDataPath + "/Mods";
		LoadMods();
    }

    // Update is called once per frame
    void Update(){  
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
				if((directory.Name == "Unity")||
				   (directory.Name == "il2cpp")){
					continue;
				}
				//Debug.Log(directory);
				if(modList != ""){
					modList += ",";
				}
				modList += directory.Name;
				
				string modFolderName = loadJson(directory.Name);
				string modFullName;
				string modAuthor;
				string modJsonValid;
				
				//Check the json is valid
				try {
					modCarset modJson = JsonUtility.FromJson<modCarset>(modFolderName);
					modFullName = modJson.modName;
					modAuthor = modJson.modAuthor;
					modJsonValid = "OK";
				} catch(Exception e){
					modFullName = "";
					modAuthor = "";
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
				Debug.Log(modCars);
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
		
		Debug.Log(json);
		return json;
	}
}

[Serializable]
public class modDriver {
    public int carNum;
	public string carDriver;
    public int carRarity;
    public string carTeam;
    public string carManufacturer;
	public string carType;
}

[Serializable]
public class modCarset {
	public string modName;
	public string modAuthor;
	public string modPhysics;
    public List<Driver> drivers;
}