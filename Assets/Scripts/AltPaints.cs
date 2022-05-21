using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AltPaints : MonoBehaviour {
	
	public static string[,] cup2020AltPaintNames = new string[101,10];
	public static string[,] cup22AltPaintNames = new string[101,10];
	public static string[,] cup2020AltPaintDriver = new string[101,10];
	public static string[,] cup22AltPaintDriver = new string[101,10];
	public static int[,] cup2020AltPaintRarity = new int[101,10];
	
	public static bool[,] cup2020AltPaintAISpawning = new bool[101,10];
	public static bool[,] cup22AltPaintAISpawning = new bool[101,10];
	
    // Start is called before the first frame update
    void Start(){
		loadAlts();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
	
	public static void loadAlts(){
		cup2020AltPaintNames[2,1] = "#1 Patriot";
		cup2020AltPaintNames[3,1] = "#1 Halloween";
		cup2020AltPaintNames[3,2] = "#2 Icon";
		cup2020AltPaintNames[3,3] = "#3 Wrangled";
		cup2020AltPaintNames[3,4] = "#4 All Star";
		cup2020AltPaintNames[5,1] = "#1 Dirty";
		cup2020AltPaintNames[5,2] = "#2 Junked";
		cup2020AltPaintNames[6,1] = "#1 Taped";
		cup2020AltPaintNames[7,1] = "#1 Insure";
		cup2020AltPaintNames[9,1] = "#1 Hoot";
		cup2020AltPaintNames[12,1] = "#1 Kobe";
		cup2020AltPaintNames[13,1] = "#1 Military";
		cup2020AltPaintNames[17,1] = "#1 Sunny";
		cup2020AltPaintNames[18,1] = "#1 Halloween";
		cup2020AltPaintNames[19,1] = "#1 USO";
		cup2020AltPaintNames[20,1] = "#1 Clashed";
		cup2020AltPaintNames[22,1] = "#1 Tribute";
		cup2020AltPaintNames[27,1] = "#1 Cup";
		cup2020AltPaintNames[32,1] = "#1 Halloween";
		cup2020AltPaintNames[37,1] = "#1 Seltzer";
		cup2020AltPaintNames[41,1] = "#1 Un Aero";
		cup2020AltPaintNames[42,1] = "#1 Halloween";
		cup2020AltPaintNames[43,1] = "#1 BLM";
		cup2020AltPaintNames[47,1] = "#1 Nitro";
		cup2020AltPaintNames[48,1] = "#1 Champ";
		cup2020AltPaintNames[48,2] = "#2 Cobalt";
		cup2020AltPaintNames[48,3] = "#3 All Star";
		cup2020AltPaintNames[51,1] = "#1 Warhawk";
		cup2020AltPaintNames[66,1] = "#1 Bandit";
		cup2020AltPaintNames[77,1] = "#1 Melon";
		cup2020AltPaintNames[88,1] = "#1 Cup";
		cup2020AltPaintNames[88,2] = "#2 Halloween";
		cup2020AltPaintNames[88,3] = "#3 Duck Num";
		cup2020AltPaintNames[95,1] = "#1 Core";
		
		cup22AltPaintNames[15,1] = "Stock (Ragan)";
		cup22AltPaintNames[16,1] = "Stock (Hemric)";
		cup22AltPaintNames[27,1] = "Stock (Hezemans)";
		cup22AltPaintNames[66,1] = "Stock (Said)";
		cup22AltPaintNames[77,1] = "Stock (Bilicki)";
		cup22AltPaintNames[78,1] = "Stock (Lally)";
		cup22AltPaintNames[1,1] = "#1 LTT Winner";
		cup22AltPaintNames[99,1] = "#1 Lawnmower";
		
		cup2020AltPaintDriver[3,2] = "Earnhardt";
		cup2020AltPaintDriver[3,3] = "Earnhardt";
		cup2020AltPaintDriver[3,4] = "Earnhardt";
		cup2020AltPaintDriver[6,1] = "Martin";
		cup2020AltPaintDriver[41,1] = "Ku. Busch";
		cup2020AltPaintDriver[88,3] = "Earnhardt Jr";
		
		cup22AltPaintDriver[15,1] = "Ragan";
		cup22AltPaintDriver[16,1] = "Hemric";
		cup22AltPaintDriver[27,1] = "Hezemans";
		cup22AltPaintDriver[66,1] = "Said";
		cup22AltPaintDriver[77,1] = "Bilicki";
		cup22AltPaintDriver[78,1] = "Lally";
		cup22AltPaintDriver[99,1] = "Edwards";
		
		cup2020AltPaintAISpawning[5,2] = false;
		cup2020AltPaintAISpawning[6,1] = false;
		cup2020AltPaintAISpawning[88,1] = false;
		
		cup22AltPaintAISpawning[1,1] = false;
		cup22AltPaintAISpawning[99,1] = false;
	}
	
	public static string getAltPaintName(string seriesPrefix, int carNum, int altNum){
		switch(seriesPrefix){
			case "cup20":
				return cup2020AltPaintNames[carNum,altNum];
				break;
			case "cup22":
				return cup22AltPaintNames[carNum,altNum];
				break;
			default:
				return null;
				break;
		}
		return null;
	}
	
	public static string getAltPaintDriver(string seriesPrefix, int carNum, int altNum){
		switch(seriesPrefix){
			case "cup20":
				return cup2020AltPaintDriver[carNum,altNum];
				break;
			case "cup22":
				return cup22AltPaintDriver[carNum,altNum];
				break;
			default:
				return "Unknown";
				break;
		}
		return "Unknown";
	}
	
	public static bool getAltPaintAISpawning(string seriesPrefix, int carNum, int altNum){
		switch(seriesPrefix){
			case "cup20":
				return cup2020AltPaintAISpawning[carNum,altNum];
				break;
			case "cup22":
				return cup22AltPaintAISpawning[carNum,altNum];
				break;
			default:
				return false;
				break;
		}
		return false;
	}
}