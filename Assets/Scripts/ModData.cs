using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Linq;

public class ModData : MonoBehaviour
{
	public static DirectoryInfo d;
    // Start is called before the first frame update
    void Start(){
        loadModData();
    }

	public static void loadModData(){
		if(d == null){
			if (Directory.Exists(Application.persistentDataPath)){
				if(!Directory.Exists(Application.persistentDataPath + "/Mods")){
					d = Directory.CreateDirectory(Application.persistentDataPath + "/Mods"); // returns a DirectoryInfo object
				} else {
					d = new DirectoryInfo(Application.persistentDataPath + "/Mods");
				}
			}
		}
	}

	public static bool isModSeries(string seriesPrefix){
		loadModData();
		foreach(var directory in d.GetDirectories()){
			//Avoid these default folders
			if(directory.Name == seriesPrefix){
				return true;
			}
		}
		return false;
	}

	public static string getSeriesNiceName(string seriesPrefix){
		loadModData();
		string seriesNiceName = null;
		foreach(var directory in d.GetDirectories()){
			//Avoid these default folders
			if(directory.Name == seriesPrefix){
				//Debug.Log("Found the mod..");
				string modFolderName = loadJson(directory.Name);
				try {
					modCarset modJson = JsonUtility.FromJson<modCarset>(modFolderName);
					//Debug.Log("Parsed json, found: " + modJson.modName);
					seriesNiceName = modJson.modName;
				} catch(Exception e){
					seriesNiceName = "Error";
				}
			}
		}
		return stringLimit(seriesNiceName,8);
	}

	public static int getCarNum(string seriesPrefix, int index){
		loadModData();
		int carNum = 999;
		foreach(var directory in d.GetDirectories()){
			//Avoid these default folders
			if(directory.Name == seriesPrefix){
				string modFolderName = loadJson(directory.Name);
				try {
					modCarset modJson = JsonUtility.FromJson<modCarset>(modFolderName);
					carNum = modJson.drivers[index].carNum;
				} catch(Exception e){
					Debug.Log(e.Message);
					return 999;
				}
			}
		}
		//Debug.Log("Loaded car #" + carNum);
		return carNum;
	}
	
	//Heavy looping function to find the link between JSON index and car number
	//Should only run this once per car, to avoid performance issues.
	public static int getJsonIndexFromCarNum(string seriesPrefix, int carNum){
		loadModData();
		int jsonIndex = 999;
		foreach(var directory in d.GetDirectories()){
			//Avoid these default folders
			if(directory.Name == seriesPrefix){
				string modFolderName = loadJson(directory.Name);
				try {
					modCarset modJson = JsonUtility.FromJson<modCarset>(modFolderName);
					
					for(int i=0;i<100;i++){
						if(modJson.drivers[i].carNum == carNum){
							return i;
						}
					}
				} catch(Exception e){
					return 999;
				}
			}
		}
		return 999;
	}

	public static string getName(string seriesPrefix, int index){
		loadModData();
		string driverName = null;
		foreach(var directory in d.GetDirectories()){
			//Avoid these default folders
			if(directory.Name == seriesPrefix){
				string modFolderName = loadJson(directory.Name);
				try {
					modCarset modJson = JsonUtility.FromJson<modCarset>(modFolderName);
					driverName = modJson.drivers[index].carDriver;
				} catch(Exception e){
					return null;
				}
			}
		}
		return stringLimit(driverName,12);
	}
	
	public static string getType(string seriesPrefix, int index){
		loadModData();
		string driverType = null;
		foreach(var directory in d.GetDirectories()){
			//Avoid these default folders
			if(directory.Name == seriesPrefix){
				string modFolderName = loadJson(directory.Name);
				try {
					modCarset modJson = JsonUtility.FromJson<modCarset>(modFolderName);
					driverType = modJson.drivers[index].carType;
				} catch(Exception e){
					driverType = "Error";
				}
			}
		}
		//Max rarity is 4
		switch(driverType){
			case "Rookie":
			case "Rook":
			case "Intimidator":
			case "Intim":
			case "Closer":
			case "Close":
			case "Strategist":
			case "Strat":
			case "Dominator":
			case "Domin":
			case "Legend":
			case "Legen":
				//Valid, no change required
				break;
			default:
				driverType = "Rookie";
				break;
		}
		return driverType;
	}
	
	public static string getTeam(string seriesPrefix, int index){
		loadModData();
		string driverTeam = null;
		foreach(var directory in d.GetDirectories()){
			//Avoid these default folders
			if(directory.Name == seriesPrefix){
				string modFolderName = loadJson(directory.Name);
				try {
					modCarset modJson = JsonUtility.FromJson<modCarset>(modFolderName);
					driverTeam = modJson.drivers[index].carTeam;
				} catch(Exception e){
					driverTeam = "Error";
				}
			}
		}
		return stringLimit(driverTeam,3).ToUpper();
	}
	
	public static string getManufacturer(string seriesPrefix, int index){
		loadModData();
		string driverManufacturer = null;
		foreach(var directory in d.GetDirectories()){
			//Avoid these default folders
			if(directory.Name == seriesPrefix){
				string modFolderName = loadJson(directory.Name);
				try {
					modCarset modJson = JsonUtility.FromJson<modCarset>(modFolderName);
					driverManufacturer = modJson.drivers[index].carManufacturer;
				} catch(Exception e){
					driverManufacturer = "CHV";
				}
			}
		}
		return stringLimit(driverManufacturer,3).ToUpper();
	}
	
	public static int getRarity(string seriesPrefix, int index){
		loadModData();
		int driverRarity = 0;
		foreach(var directory in d.GetDirectories()){
			//Avoid these default folders
			if(directory.Name == seriesPrefix){
				string modFolderName = loadJson(directory.Name);
				try {
					modCarset modJson = JsonUtility.FromJson<modCarset>(modFolderName);
					//modFullName = modJson.modName;
					//modAuthor = modJson.modAuthor;
					driverRarity = modJson.drivers[index].carRarity;
				} catch(Exception e){
					driverRarity = 1;
				}
			}
		}
		//Max rarity is 4
		switch(driverRarity){
			case 1:
			case 2:
			case 3:
				//Valid, no change required
				break;
			default:
				driverRarity = 1;
				break;
		}
		return driverRarity;
	}
	
	public static Texture2D getTexture(string seriesPrefix, int index){
		loadModData();
		Debug.Log("Looking for a mod texture..");
		Texture2D carTex = null;
		foreach(var directory in d.GetDirectories()){
			//Avoid these default folders
			if(directory.Name == seriesPrefix){
				//Debug.Log("Found the correct mod carset..");
				string modFolderName = loadJson(directory.Name);
				d = new DirectoryInfo(Application.persistentDataPath + "/Mods/");
				if(System.IO.File.Exists(d + directory.Name + "/" + seriesPrefix + "-" + index + ".png")){
				    byte[] bytes = System.IO.File.ReadAllBytes(d + directory.Name + "/" + seriesPrefix + "-" + index + ".png");
					carTex = new Texture2D(2,2,TextureFormat.RGBA32, false);
					carTex.filterMode = FilterMode.Point;
					carTex.LoadImage(bytes);
				} else {
					//Debug.Log("Texture isn't where it should be.. " + d + directory.Name + "/" + seriesPrefix + "-" + index + ".png" );
				}
			}
		}
		return carTex;
	}
	
	public static string loadJson(string folderName){
		
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
	
	public static string stringLimit(string data, int limit){
		if(data.Length > limit){
			data = data.Substring(0,limit);
		}
		return data;
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
    public List<modDriver> drivers;
}