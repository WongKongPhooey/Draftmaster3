using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Linq;

public class ModData : MonoBehaviour
{
	//Root folder
	public static DirectoryInfo d;
	//Mod folder inside the root
	public static string modName;
    public static DirectoryInfo loadedMod;
	// Start is called before the first frame update
    void Start(){
        loadAllMods();
    }

	public static void loadAllMods(){
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
	
	public static DirectoryInfo loadMod(string seriesPrefix){
		if(modName == seriesPrefix){
			return loadedMod;
		}
		if(Directory.Exists(Application.persistentDataPath + "/Mods/" + seriesPrefix)){
			modName = seriesPrefix;
			return new DirectoryInfo(Application.persistentDataPath + "/Mods/" + seriesPrefix);
		}
		return null;
	}

	public static bool isModSeries(string seriesPrefix){
		if((loadedMod == null)||(modName != seriesPrefix)){
			loadedMod = loadMod(seriesPrefix);
		}	
		if(modName == seriesPrefix){
			return true;
		}
		return false;
	}

	public static string getSeriesNiceName(string seriesPrefix){
		if((loadedMod == null)||(modName != seriesPrefix)){
			loadedMod = loadMod(seriesPrefix);
		}
		string seriesNiceName = null;
		if(loadedMod != null){
			string modFolderName = loadJson(loadedMod.Name);
			try {
				modSet modJson = JsonUtility.FromJson<modSet>(modFolderName);
				Debug.Log("Parsed json, found: " + modJson.modName);
				seriesNiceName = modJson.modName;
			} catch(Exception e){
				seriesNiceName = "Error";
			}
		}
		return stringLimit(seriesNiceName,8);
	}

	public static string getPhysicsModel(string seriesPrefix){
		if((loadedMod == null)||(modName != seriesPrefix)){
			loadedMod = loadMod(seriesPrefix);
		}
		string physicsModel = "default";
		if(loadedMod != null){
			string modFolderName = loadJson(loadedMod.Name);
			try {
				modSet modJson = JsonUtility.FromJson<modSet>(modFolderName);
				//Debug.Log("Parsed json, found: " + modJson.modName);
				physicsModel = modJson.modPhysics.ToLower();
			} catch(Exception e){
				physicsModel = "Default";
			}
		}
		return stringLimit(physicsModel,10);
	}

	public static int getNumberPosition(string seriesPrefix){
		if((loadedMod == null)||(modName != seriesPrefix)){
			loadedMod = loadMod(seriesPrefix);
		}
		int numPos = 0;
		if(loadedMod != null){
			string modFolderName = loadJson(loadedMod.Name);
			try {
				modSet modJson = JsonUtility.FromJson<modSet>(modFolderName);
				//Debug.Log("Parsed json, found: " + modJson.modName);
				numPos = int.Parse(modJson.modNumberPosition);
			} catch(Exception e){
				numPos = 0;
			}
		}
		return numPos;
	}
	public static float getNumberScale(string seriesPrefix){
		if((loadedMod == null)||(modName != seriesPrefix)){
			loadedMod = loadMod(seriesPrefix);
		}
		float numScale = 1;
		if(loadedMod != null){
			string modFolderName = loadJson(loadedMod.Name);
			try {
				modSet modJson = JsonUtility.FromJson<modSet>(modFolderName);
				//Debug.Log("Parsed json, found: " + modJson.modName);
				numScale = float.Parse(modJson.modNumberScale);
			} catch(Exception e){
				numScale = 1;
			}
		}
		return numScale;
	}
	public static int getNumberRotation(string seriesPrefix){
		if((loadedMod == null)||(modName != seriesPrefix)){
			loadedMod = loadMod(seriesPrefix);
		}
		int numRot = 0;
		if(loadedMod != null){
			string modFolderName = loadJson(loadedMod.Name);
			try {
				modSet modJson = JsonUtility.FromJson<modSet>(modFolderName);
				//Debug.Log("Parsed json, found: " + modJson.modName);
				numRot = int.Parse(modJson.modNumberRotation);
			} catch(Exception e){
				numRot = 0;
			}
		}
		return numRot;
	}

	public static int getCarNum(string seriesPrefix, int index){
		if((loadedMod == null)||(modName != seriesPrefix)){
			loadedMod = loadMod(seriesPrefix);
		}
		int carNum = 999;
		if(loadedMod != null){
			string modFolderName = loadJson(loadedMod.Name);
			try {
				modSet modJson = JsonUtility.FromJson<modSet>(modFolderName);
				carNum = modJson.drivers[index].carNum;
			} catch(Exception e){
				return 999;
			}
		}
		//Debug.Log("Loaded car #" + carNum);
		return carNum;
	}
	
	//Heavy looping function to find the link between JSON index and car number
	//Should only run this once per car, to avoid performance issues.
	public static int getJsonIndexFromCarNum(string seriesPrefix, int carNum){
		if((loadedMod == null)||(modName != seriesPrefix)){
			loadedMod = loadMod(seriesPrefix);
		}
		int jsonIndex = 999;
		if(loadedMod != null){
			string modFolderName = loadJson(loadedMod.Name);
			try {
				modSet modJson = JsonUtility.FromJson<modSet>(modFolderName);
				for(int i=0;i<100;i++){
					if(modJson.drivers[i].carNum == carNum){
						return i;
					}
				}
			} catch(Exception e){
				return 999;
			}
		}
		return 999;
	}

	public static string getName(string seriesPrefix, int index, bool convertedIndex = false){
		if((loadedMod == null)||(modName != seriesPrefix)){
			loadedMod = loadMod(seriesPrefix);
		}
		string driverName = null;
		if(loadedMod != null){
			string modFolderName = loadJson(loadedMod.Name);
			if(convertedIndex){
				index = getJsonIndexFromCarNum(seriesPrefix, index);
			}
			try {
				modSet modJson = JsonUtility.FromJson<modSet>(modFolderName);
				driverName = modJson.drivers[index].carDriver;
			} catch(Exception e){
				return null;
			}
		}
		return stringLimit(driverName,12);
	}
	
	public static string getType(string seriesPrefix, int index, bool convertedIndex = false){
		if((loadedMod == null)||(modName != seriesPrefix)){
			loadedMod = loadMod(seriesPrefix);
		}
		string driverType = null;
		if(loadedMod != null){
			string modFolderName = loadJson(loadedMod.Name);
			if(convertedIndex){
				index = getJsonIndexFromCarNum(seriesPrefix, index);
			}
			try {
				modSet modJson = JsonUtility.FromJson<modSet>(modFolderName);
				driverType = modJson.drivers[index].carType;
			} catch(Exception e){
				driverType = "Error";
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
			case "Blocker":
			case "Block":
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
	
	public static string getTeam(string seriesPrefix, int index, bool convertedIndex = false){
		if((loadedMod == null)||(modName != seriesPrefix)){
			loadedMod = loadMod(seriesPrefix);
		}
		string driverTeam = null;
		if(loadedMod != null){
			string modFolderName = loadJson(loadedMod.Name);
			if(convertedIndex){
				index = getJsonIndexFromCarNum(seriesPrefix, index);
			}
			try {
				modSet modJson = JsonUtility.FromJson<modSet>(modFolderName);
				driverTeam = modJson.drivers[index].carTeam;
			} catch(Exception e){
				driverTeam = "Error";
			}
		}
		return stringLimit(driverTeam,3).ToUpper();
	}
	
	public static string getManufacturer(string seriesPrefix, int index, bool convertedIndex = false){
		if((loadedMod == null)||(modName != seriesPrefix)){
			loadedMod = loadMod(seriesPrefix);
		}
		string driverManufacturer = null;
		if(loadedMod != null){
			string modFolderName = loadJson(loadedMod.Name);
			if(convertedIndex){
				index = getJsonIndexFromCarNum(seriesPrefix, index);
			}
			try {
				modSet modJson = JsonUtility.FromJson<modSet>(modFolderName);
				driverManufacturer = modJson.drivers[index].carManufacturer;
				//Debug.Log(driverManufacturer);
			} catch(Exception e){
				driverManufacturer = "CHV";
			}
		}
		return stringLimit(driverManufacturer,3).ToUpper();
	}
	
	public static int getRarity(string seriesPrefix, int index, bool convertedIndex = false){
		if((loadedMod == null)||(modName != seriesPrefix)){
			loadedMod = loadMod(seriesPrefix);
		}
		int driverRarity = 0;
		if(loadedMod != null){
			string modFolderName = loadJson(loadedMod.Name);
			if(convertedIndex){
				index = getJsonIndexFromCarNum(seriesPrefix, index);
			}
			try {
				modSet modJson = JsonUtility.FromJson<modSet>(modFolderName);
				//modFullName = modJson.modName;
				//modAuthor = modJson.modAuthor;
				driverRarity = modJson.drivers[index].carRarity;
			} catch(Exception e){
				driverRarity = 1;
			}
		}
		//Max rarity is 4
		switch(driverRarity){
			case 1:
			case 2:
			case 3:
			case 4:
				//Valid, no change required
				break;
			default:
				driverRarity = 1;
				break;
		}
		return driverRarity;
	}
	
	public static string getModSeriesName(string seriesPrefix, int index){
		if((loadedMod == null)||(modName != seriesPrefix)){
			loadedMod = loadMod(seriesPrefix);
		}
		string seriesName = null;
		if(loadedMod != null){
			string modFolderName = loadJson(loadedMod.Name);
			try {
				modSet modJson = JsonUtility.FromJson<modSet>(modFolderName);
				seriesName = modJson.series[index].seriesName;
			} catch(Exception e){
				Debug.Log(e.Message);
				return null;
			}
		}
		return seriesName;
	}
	
	public static string getModSeriesDesc(string seriesPrefix, int index){
		if((loadedMod == null)||(modName != seriesPrefix)){
			loadedMod = loadMod(seriesPrefix);
		}
		string seriesDesc = null;
		if(loadedMod != null){
			string modFolderName = loadJson(loadedMod.Name);
			try {
				modSet modJson = JsonUtility.FromJson<modSet>(modFolderName);
				seriesDesc = modJson.series[index].seriesDescription;
			} catch(Exception e){
				Debug.Log(e.Message);
				return null;
			}
		}
		return seriesDesc;
	}
	
	public static string getModSeriesTracklist(string seriesPrefix, int index){
		if((loadedMod == null)||(modName != seriesPrefix)){
			loadedMod = loadMod(seriesPrefix);
		}
		string seriesTracklist = null;
		if(loadedMod != null){
			string modFolderName = loadJson(loadedMod.Name);
			try {
				modSet modJson = JsonUtility.FromJson<modSet>(modFolderName);
				seriesTracklist = modJson.series[index].seriesTracklist;
			} catch(Exception e){
				Debug.Log(e.Message);
				return null;
			}
		}
		return seriesTracklist;
	}
	
	public static Texture2D getTexture(string seriesPrefix, int index, bool convertToNum = false, string altPrefix = ""){
		if((loadedMod == null)||(modName != seriesPrefix)){
			loadedMod = loadMod(seriesPrefix);
		}
		//Debug.Log("Looking for a mod texture..");
		Texture2D carTex = null;
		if(loadedMod != null){
			string modFolderName = loadJson(loadedMod.Name);
			if(convertToNum == true){
				index = getCarNum(seriesPrefix, index);
			}
			d = new DirectoryInfo(Application.persistentDataPath + "/Mods/");
			if(System.IO.File.Exists(d + modName + "/" + seriesPrefix + "-" + index + altPrefix + ".png")){
				byte[] bytes = System.IO.File.ReadAllBytes(d + modName + "/" + seriesPrefix + "-" + index + altPrefix + ".png");
				carTex = new Texture2D(2,2,TextureFormat.RGBA32, false);
				carTex.filterMode = FilterMode.Point;
				carTex.LoadImage(bytes);
			}
		}
		return carTex;
	}
	
	public static Texture2D getAltTexture(string seriesPrefix, int index, int alt){
		if((loadedMod == null)||(modName != seriesPrefix)){
			loadedMod = loadMod(seriesPrefix);
		}
		//Debug.Log("Looking for a mod texture..");
		Texture2D carTex = null;
		if(loadedMod != null){
			string modFolderName = loadJson(loadedMod.Name);
			d = new DirectoryInfo(Application.persistentDataPath + "/Mods/");
			if(System.IO.File.Exists(d + modName + "/" + seriesPrefix + "-" + index + "alt" + alt + ".png")){
				byte[] bytes = System.IO.File.ReadAllBytes(d + modName + "/" + seriesPrefix + "-" + index + "alt" + alt + ".png");
				carTex = new Texture2D(2,2,TextureFormat.RGBA32, false);
				carTex.filterMode = FilterMode.Point;
				carTex.LoadImage(bytes);
			}
		}
		return carTex;
	}
	
	public static Texture2D getManuTexture(string seriesPrefix, string manu){
		if((loadedMod == null)||(modName != seriesPrefix)){
			loadedMod = loadMod(seriesPrefix);
		}
		Texture2D manuTex = null;
		if(loadedMod != null){
			string modFolderName = loadJson(loadedMod.Name);
			d = new DirectoryInfo(Application.persistentDataPath + "/Mods/");
			if(System.IO.File.Exists(d + modName + "/" + seriesPrefix + "-" + manu + ".png")){
				Debug.Log("Found a manufacturer!");
				byte[] bytes = System.IO.File.ReadAllBytes(d + modName + "/" + seriesPrefix + "-" + manu + ".png");
				manuTex = new Texture2D(16,16,TextureFormat.RGBA32, false);
				manuTex.filterMode = FilterMode.Point;
				manuTex.LoadImage(bytes);
			}
		}
		return manuTex;
	}
	
	public static Sprite getManuSprite(string seriesPrefix, string manu){
		if((loadedMod == null)||(modName != seriesPrefix)){
			loadedMod = loadMod(seriesPrefix);
		}
		Sprite manuSpr = null;
		if(loadedMod != null){
			string modFolderName = loadJson(loadedMod.Name);
			d = new DirectoryInfo(Application.persistentDataPath + "/Mods/");
			if(System.IO.File.Exists(d + modName + "/" + seriesPrefix + "-" + manu + ".png")){
				Debug.Log("Found a manufacturer!");
				byte[] bytes = System.IO.File.ReadAllBytes(d + modName + "/" + seriesPrefix + "-" + manu + ".png");
				Texture2D manuTex = new Texture2D(16,16,TextureFormat.RGBA32, false);
				manuTex.filterMode = FilterMode.Point;
				manuTex.LoadImage(bytes);
				manuSpr = Sprite.Create(manuTex,new Rect(0, 0, manuTex.width, manuTex.height),new Vector2(0,0));
			}
		}
		return manuSpr;
	}
	
	public static void resetAllTransfers(){
		foreach(var directory in d.GetDirectories()){
			for(int i=0;i<100;i++){
				if(PlayerPrefs.HasKey("CustomDriver" + directory.Name + i)){
					PlayerPrefs.DeleteKey("CustomDriver" + directory.Name + i);
				}
				if(PlayerPrefs.HasKey("CustomManufacturer" + directory.Name + i)){
					PlayerPrefs.DeleteKey("CustomManufacturer" + directory.Name + i);
				}
				if(PlayerPrefs.HasKey("CustomTeam" + directory.Name + i)){
					PlayerPrefs.DeleteKey("CustomTeam" + directory.Name + i);
				}
				if(PlayerPrefs.HasKey("CustomNumber" + directory.Name + i)){
					PlayerPrefs.DeleteKey("CustomNumber" + directory.Name + i);
				}
			}
		}
	}

	public static string loadJson(string folderName){
		
		TextAsset jsonFile;

		string directoryPath = Application.persistentDataPath + "/Mods/" + folderName;
		System.IO.Directory.CreateDirectory(directoryPath);
		string json = System.IO.File.ReadAllText(directoryPath + "/" + folderName + ".json");

		try{
			modSet modJson = JsonUtility.FromJson<modSet>(json);
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
public class modSet {
	public string modName;
	public string modFolder;
	public string modType;
	public string modVersion;
	public string modAuthor;
	public string modPhysics;
	public string modNumberPosition;
	public string modNumberScale;
	public string modNumberRotation;
    public List<modDriver> drivers;
	public List<modSeries> series;
}

[Serializable]
public class modTrackset {
	public string modName;
	public string modFolder;
	public string modType;
	public string[] straightLengths;
	public string[] turnAngles;
	public string[] turnRadius;
}

[Serializable]
public class modSeries {
	public string seriesName;
	public string seriesImage;
	public string seriesDescription;
	public string seriesTracklist;
}